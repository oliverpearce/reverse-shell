using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>A card in the hand. Click to queue it; drag to move it; hold it to open its details (the full text and
    /// upgrades, <see cref="CardDetailPopup"/>). Hovering only lifts it a little.</summary>
    public class CardView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IScrollHandler
    {
        /// <summary>How long to hold a card to open its details.</summary>
        const float LongPressSeconds = 0.45f;
        /// <summary>The hand card: a 48 x 64 px pixel card at 5x (<see cref="CardArt"/>).</summary>
        public static readonly Vector2 Size = new Vector2(CardArt.Width, CardArt.Height) * CardArt.HandPixel;

        /// <summary>Card faces drawn at least this big (the zoomed-in cards, 2x) show their name on the plate; smaller ones
        /// leave the plate blank (the user's call: small card text reads badly).</summary>
        public const float ZoomTextScale = 1.5f;
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);
        static readonly Color NumberGold = new Color(1f, 0.84f, 0.38f);

        public CardInstance Card { get; private set; }
        public RectTransform Rect { get; private set; }
        public CanvasGroup Group { get; private set; }

        HandView hand;
        Coroutine moveRoutine, scaleRoutine;
        bool dragging, longPressed;
        // Hold-to-open: polled in Update so hover, re-layouts and small mouse jitter can't cancel it.
        bool pressing;
        float pressTime;
        Vector2 pressPosition;
        Vector3 dragOffset;
        int shownRank;   // the rank the face shows: the card's own, or another one previewed with the mouse wheel

        // Fan pose (set by HandView): where the card rests in the curved hand and its tilt. LateUpdate eases the card
        // toward it, lifted, straightened and enlarged while hovered. Off while the card is dragged or flying away.
        Vector2 posePosition;
        float poseAngle;
        bool posed, hovered;
        /// <summary>The hovered card's size, and the height (canvas units above the hand's middle line) it rises to:
        /// enough that the enlarged card's bottom edge stays where the resting card's was, so the pointer never slips
        /// off it (which would flicker the hover).</summary>
        public const float HoverScale = 1.25f;
        public static float HoverLift { get { return Size.y * (HoverScale - 1f) / 2f; } }
        const float PoseSpeed = 14f;

        public static CardView Create(Transform parent, CardInstance card, HandView hand)
        {
            var rt = UIFactory.CreateRect("Card_" + card.Name, parent);
            rt.sizeDelta = Size;
            var cv = rt.gameObject.AddComponent<CardView>();
            cv.Card = card;
            cv.hand = hand;
            cv.Rect = rt;
            cv.Group = rt.gameObject.AddComponent<CanvasGroup>();
            BuildFace(rt, card, 1f);
            // Hovered (not while dragged), the card leans a little toward the mouse.
            var tilt = CardTilt.Add(rt);
            tilt.MaxAngle = tilt.HoverMax;
            tilt.HoverWhen = () => cv.hovered && !cv.dragging && !Input.GetMouseButton(0);
            return cv;
        }

        /// <summary>Soft pastel version of the owner's color, used for Ritual cards.</summary>
        public static Color RitualPastel(Color owner)
        {
            return Color.Lerp(owner, Color.white, 0.62f);
        }

        /// <summary>
        /// Builds the card's visuals into <paramref name="root"/> (sized Size * s): the pixel card from
        /// <see cref="CardArt"/> (Tools/CardForge), one card pixel = CardArt.HandPixel * s canvas units. A normal card is the owner-tinted
        /// swirl, the frame, rank pips along the top, the type icon top-right, the card's own centre icon and its name
        /// on the plate. A Ritual (Ult) card is its owner's full-art card with no text. Used for the hand card, the
        /// card popup and the icon rows, so bigger versions are crisp re-layouts rather than blurry upscales.
        /// </summary>
        public static void BuildFace(RectTransform root, CardInstance card, float s)
        {
            BuildFace(root, card, s, false);
        }

        /// <summary><paramref name="iconOnly"/>: just the card (the party menu and the inspect sheet). Otherwise a big
        /// face (s &gt; 1, the card popup) also gets the text panel beside it.</summary>
        public static void BuildFace(RectTransform root, CardInstance card, float s, bool iconOnly)
        {
            var owner = card.Owner.Definition;
            bool detailed = s > 1f && !iconOnly;
            float u = CardArt.HandPixel * s;   // canvas units per card pixel

            // Transparent hit area on the root (click / drag / hover), then the face.
            var hit = root.GetComponent<Image>();
            if (hit == null) hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            // A party member's Soul (Ult) card: the same card format as everything else, the character's own sprite as the
            // art, their colour as a pale swirl, the Ult's name on the plate, and the Ult glisten.
            var soulSprite = card.IsRitual ? CardArt.SoulSprite(owner) : null;
            if (soulSprite != null && owner is CharacterDefinition)
            {
                // A strong wash of the unit's colour, so whose Soul card it is reads at a glance.
                BuildSimpleFace(root, soulSprite, Color.Lerp(card.Owner.Color, Color.white, 0.2f), card.Name, s, true);
                UltShine.Add(root, null, u);
                if (detailed) DetailPanel(root, card, s);
                return;
            }
            var ult = card.IsRitual ? CardArt.Ult(card.Definition, owner) : null;
            if (ult != null)
            {
                // A tarot Soul card is taller than a card: fitted to the card's height, centred, not stretched.
                var artRect = root;
                if (CardArt.IsTarot(ult))
                {
                    float h = Size.y * s;
                    artRect = UIFactory.CreateRect("Tarot", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                        new Vector2(h * ult.rect.width / ult.rect.height, h));
                }
                Pixel(artRect, "UltArt", ult, Color.white).rectTransform.Fill();
                UltShine.Add(artRect, ult, u);   // every Ult card glistens, in the hand and in every menu
            }
            else
            {
                Pixel(root, "Swirl", CardArt.Swirl, card.Owner.Color).rectTransform.Fill();
                Pixel(root, "Frame", CardArt.Frame, Color.white).rectTransform.Fill();

                if (!card.IsRitual)
                    for (int i = 0; i < card.Definition.MaxRank; i++)
                    {
                        var pip = Pixel(root, "Rank" + i, CardArt.Pip(i < card.Rank), Color.white);
                        CardArt.PlacePixels(pip.rectTransform, CardArt.PipX + i * CardArt.PipStep, CardArt.PipY, CardArt.PipSize, CardArt.PipSize, u);
                    }

                // The type icon in the pip strip, drawn at the card's own pixel size like the pips. Buff and Debuff keep
                // their Raven arrows (the user preferred them to the pixel ones), in the same spot.
                var cardType = CardIcons.TypeOf(card.Definition);
                var iconSprite = CardIcons.Glyph(cardType);
                bool ravenArrow = cardType == CardIcons.Type.Buff || cardType == CardIcons.Type.Debuff;
                var typeSprite = ravenArrow ? iconSprite : CardArt.Type(cardType);
                if (typeSprite != null)
                {
                    var type = Pixel(root, "TypeIcon", typeSprite, Color.white);
                    type.preserveAspect = true;
                    var r = CardArt.TypeIcon;
                    CardArt.PlacePixels(type.rectTransform, r.x, r.y, r.width, r.height, u);
                }

                // The card's own centre icon; cards CardForge hasn't drawn yet show their type icon big instead.
                var art = CardArt.Icon(card.Definition);
                var centre = Pixel(root, "Art", art != null ? art : iconSprite, Color.white);
                centre.preserveAspect = true;
                CardArt.PlaceIcon(centre.rectTransform, u);

                // The name on the plate only on zoomed-in cards (small cards read badly; the name shows in their panels).
                if (s >= ZoomTextScale && CardTranslation.On) CardArt.NameLabel(root, card.Name, InkStyle.Paper, s);
                else CardArt.RuneLabel(root, card.Name, InkStyle.Paper, s);   // small cards: the name in runes
            }

            if (detailed) DetailPanel(root, card, s);
        }

        /// <summary>A point-filtered pixel-art Image (the card pieces).</summary>
        /// <summary>
        /// The shared card format without rank pips or a type icon: the swirl in <paramref name="tint"/>, the frame, a
        /// picture in the centre window (or, <paramref name="bigArt"/>, filling most of the card: a character's sprite) and
        /// the name on the plate. Used for Sun / Moon / Star and for Soul cards, so every card looks like one set.
        /// </summary>
        public static void BuildSimpleFace(RectTransform root, Sprite art, Color tint, string name, float s, bool bigArt)
        {
            float u = CardArt.HandPixel * s;
            var hit = root.GetComponent<Image>();
            if (hit == null) hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            Pixel(root, "Swirl", CardArt.Swirl, tint).rectTransform.Fill();
            Pixel(root, "Frame", CardArt.Frame, Color.white).rectTransform.Fill();
            if (art != null)
            {
                var centre = Pixel(root, "Art", art, Color.white);
                centre.preserveAspect = true;
                // A character fills the card above the plate (the sprite is cropped to the figure, CardArt.SoulSprite).
                if (bigArt) CardArt.PlacePixels(centre.rectTransform, 3, 3, CardArt.Width - 6, CardArt.NamePlate.y - 4, u);
                else CardArt.PlaceIcon(centre.rectTransform, u);
            }
            if (s >= ZoomTextScale && CardTranslation.On) CardArt.NameLabel(root, name, InkStyle.Paper, s);
            else CardArt.RuneLabel(root, name, InkStyle.Paper, s);
        }

        static Image Pixel(Transform parent, string name, Sprite sprite, Color color)
        {
            var img = UIFactory.CreateImage(parent, name, color);
            img.sprite = sprite;
            if (sprite == null) img.color = new Color(0f, 0f, 0f, 0f);
            return img;
        }

        /// <summary>
        /// The card popup's text, on an ink panel to the right of the card: name, kind and rank, the effect text (every
        /// number bigger and gold) and the status chips it applies.
        /// </summary>
        static void DetailPanel(RectTransform root, CardInstance card, float s)
        {
            int F(float size) { return Mathf.RoundToInt(size * s); }
            var panel = InkStyle.Panel(root, "DetailPanel", InkStyle.InkPanel);   // CardTilt keeps it flat by this name
            panel.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f * s, 0f), new Vector2(PanelWidth, Size.y * 0.92f) * s);
            InkStyle.Frame(panel.rectTransform, InkStyle.Rule, 2f);
            var prt = panel.rectTransform;

            // The statuses it gives: big icons in the top-right corner, beside the name.
            float chipsWidth = StatusChips(prt, card, s);
            bool read = CardTranslation.On;   // without the language chip: runes, but numbers and status colours show
            string rank = card.IsRitual ? UIText.Get("card.ult") : UIText.Get("card.rank", "n", UIFactory.Roman(card.Rank));
            string kindLine = UnitView.KindLabel(card.Definition.kind).ToUpperInvariant() + "   " + rank;
            if (read)
            {
                var title = UIFactory.CreateText(prt, "Title", card.Name, F(22), InkStyle.Paper, TextAnchor.UpperLeft).BestFit(F(12), F(22));
                title.rectTransform.TopStrip(10f * s, 32f * s, 12f * s);
                title.rectTransform.offsetMax = new Vector2(-(12f + chipsWidth) * s, title.rectTransform.offsetMax.y);
                // The kind and rank: big enough to read at a glance.
                var kind = UIFactory.CreateText(prt, "Kind", kindLine, F(18), UnitView.KindColor(card.Definition.kind), TextAnchor.UpperLeft);
                kind.rectTransform.TopStrip(44f * s, 26f * s, 12f * s);
            }
            else
            {
                var titleArea = UIFactory.CreateRect("Title", prt);
                titleArea.TopStrip(12f * s, 26f * s, 12f * s);
                titleArea.offsetMax = new Vector2(-(12f + chipsWidth) * s, titleArea.offsetMax.y);
                float titleWidth = PanelWidth * s - (24f + chipsWidth) * s;
                RuneText.Build(titleArea, card.Name, InkStyle.Paper, titleWidth, 26f * s, 16f * s, true);
                var kindArea = UIFactory.CreateRect("Kind", prt);
                kindArea.TopStrip(46f * s, 26f * s, 12f * s);
                RuneText.Paragraph(kindArea, kindLine, UnitView.KindColor(card.Definition.kind), 11f * s, 6f * s, null,
                    UnitView.KindColor(card.Definition.kind), 1.4f);
            }
            var rule = InkStyle.Line(prt, "Rule", InkStyle.Rule);
            rule.rectTransform.TopStrip(76f * s, 2f, 12f * s);

            // The real numbers with the owner's current stats (before the target's DEF), in big gold, at the bottom.
            var live = LiveNumbers(card, F(30), !read);
            float liveH = live.Count * 40f;
            if (live.Count > 0 && !read)
            {
                var numbersArea = UIFactory.CreateRect("Numbers", prt);
                numbersArea.anchorMin = Vector2.zero;
                numbersArea.anchorMax = new Vector2(1f, 0f);
                numbersArea.pivot = new Vector2(0.5f, 0f);
                numbersArea.offsetMin = new Vector2(12f, 8f) * s;
                numbersArea.offsetMax = new Vector2(-12f, 8f + liveH) * s;
                RuneText.Paragraph(numbersArea, string.Join("\n", live.ToArray()), InkStyle.Paper, 9f * s, 5f * s, null, NumberGold, 2.2f);
                liveH += 8f;
            }
            else if (live.Count > 0)
            {
                var numbers = UIFactory.CreateText(prt, "Numbers", string.Join("\n", live.ToArray()), F(17), InkStyle.Paper, TextAnchor.LowerLeft);
                numbers.supportRichText = true;
                numbers.rectTransform.anchorMin = Vector2.zero;
                numbers.rectTransform.anchorMax = new Vector2(1f, 0f);
                numbers.rectTransform.pivot = new Vector2(0.5f, 0f);
                numbers.rectTransform.offsetMin = new Vector2(12f, 8f) * s;
                numbers.rectTransform.offsetMax = new Vector2(-12f, 8f + liveH) * s;
                liveH += 8f;
            }

            var middle = UIFactory.CreateRect("Middle", prt);
            middle.anchorMin = Vector2.zero;
            middle.anchorMax = Vector2.one;
            middle.offsetMin = new Vector2(12f, 8f + liveH) * s;
            middle.offsetMax = new Vector2(-12f, -86f) * s;
            if (!read)
            {
                // Runes, with the numbers readable in gold and each status in its colour: you can guess what it does.
                RuneText.Paragraph(middle, card.Describe(), new Color(0.92f, 0.92f, 0.95f), 9f * s, 4f * s, StatusPhrases(card), NumberGold, 1.4f);
                return;
            }
            var desc = UIFactory.CreateText(middle, "Description", "", F(15), new Color(0.92f, 0.92f, 0.95f), TextAnchor.UpperLeft);
            desc.font = UIFactory.BodyFont;
            desc.lineSpacing = 1.05f;
            desc.rectTransform.Fill();
            string text = card.Describe();
            UIFactory.FitRich(desc, size => ColorStatuses(UIFactory.EmphasizeNumbers(text, Mathf.RoundToInt(size * 1.3f), NumberGold), card), F(8), F(15));
        }

        /// <summary>The card info panel's width at scale 1.</summary>
        const float PanelWidth = 48f * CardArt.HandPixel * 1.2f;

        /// <summary>The statuses the card gives, by name, with their colours (for coloured runes).</summary>
        static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Color>> StatusPhrases(CardInstance card)
        {
            var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, Color>>();
            var rank = card.RankData;
            if (rank == null) return list;
            foreach (var effect in rank.effects)
            {
                var apply = effect as ApplyStatusEffect;
                if (apply != null && apply.status != null)
                    list.Add(new System.Collections.Generic.KeyValuePair<string, Color>(apply.status.DisplayName, apply.status.color));
            }
            return list;
        }

        /// <summary>Each status the card gives, named in <paramref name="text"/>, in that status's own colour (Stunned
        /// yellow, Poison green...), matching its icon.</summary>
        public static string ColorStatuses(string text, CardInstance card)
        {
            var rank = card.RankData;
            if (rank == null || string.IsNullOrEmpty(text)) return text;
            var done = new System.Collections.Generic.List<string>();
            foreach (var effect in rank.effects)
            {
                var apply = effect as ApplyStatusEffect;
                if (apply == null || apply.status == null) continue;
                string name = apply.status.DisplayName;
                if (string.IsNullOrEmpty(name) || done.Contains(name)) continue;
                done.Add(name);
                text = text.Replace(name, "<color=#" + ColorUtility.ToHtmlStringRGB(apply.status.color) + ">" + name + "</color>");
            }
            return text;
        }

        /// <summary>
        /// "DAMAGE 312" / "HEAL 240" lines worked out from the owner's current ATK and bonuses, the same way the battle
        /// does (CombatMath), but before the target's DEF, element and crits. Multi-hit attacks also show each hit.
        /// </summary>
        static System.Collections.Generic.List<string> LiveNumbers(CardInstance card, int numberSize, bool plain = false)
        {
            var lines = new System.Collections.Generic.List<string>();
            var rank = card.RankData;
            var unit = card.Owner;
            if (rank == null || unit == null) return lines;
            string gold = "<color=#" + ColorUtility.ToHtmlStringRGB(NumberGold) + ">";
            float atk = unit.GetStat(StatType.Attack);
            float might = Mathf.Max(0f, 1f + unit.GetStat(StatType.DamageBonus)
                                        + unit.GetStat(card.IsRitual ? StatType.RitualMight : StatType.IncantationMight));
            foreach (var effect in rank.effects)
            {
                var damage = effect as DamageEffect;
                if (damage != null)
                {
                    int total = Mathf.RoundToInt(atk * damage.multiplier * might);
                    int hits = Mathf.Max(1, damage.hits);
                    string each = hits > 1 ? "  (" + hits + " x " + Mathf.RoundToInt((float)total / hits) + ")" : "";
                    lines.Add(plain ? UIText.Get("card.damage") + " " + total + each
                        : UIText.Get("card.damage") + "  " + gold + "<size=" + UIFactory.Px(numberSize) + ">" + total + "</size></color>" + each);
                    continue;
                }
                var heal = effect as HealEffect;
                if (heal != null)
                {
                    int amount = Mathf.RoundToInt(atk * heal.multiplier * (1f + unit.GetStat(StatType.HealingBonus)));
                    lines.Add(plain ? UIText.Get("card.heal") + " " + amount
                        : UIText.Get("card.heal") + "  " + gold + "<size=" + UIFactory.Px(numberSize) + ">" + amount + "</size></color>");
                }
            }
            return lines;
        }

        /// <summary>
        /// One big chip per status the card's rank applies, in the panel's top-right corner going left: the status icon
        /// with its turns in the corner ("x2" stacks over it when more than one). Returns the width used (unscaled), 0 if
        /// none.
        /// </summary>
        static float StatusChips(RectTransform root, CardInstance card, float s)
        {
            var rank = card.RankData;
            if (rank == null) return 0f;
            const float chip = 56f, gap = 8f;
            float x = 10f;
            var topRight = new Vector2(1f, 1f);
            foreach (var effect in rank.effects)
            {
                var apply = effect as ApplyStatusEffect;
                if (apply == null || apply.status == null) continue;
                var holder = UIFactory.CreateRect(apply.status.DisplayName, root).Place(topRight, topRight, new Vector2(-x, -8f) * s, new Vector2(chip, chip) * s);
                var sprite = CardIcons.Status(apply.status);
                if (sprite != null)
                {
                    var icon = UIFactory.CreateImage(holder, "Icon", Color.white);
                    icon.sprite = sprite;
                    icon.preserveAspect = true;
                    icon.rectTransform.Fill();
                }
                else
                {
                    var plate = UIFactory.CreateImage(holder, "Plate", UIFactory.Darken(apply.status.color, 0.35f));
                    plate.rectTransform.Fill();
                    UIFactory.CreateText(holder, "Label", apply.status.shortLabel, Mathf.RoundToInt(20f * s), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Fill();
                }
                // Hover the chip for what the status does (the box says how long it lasts).
                var hitArea = holder.gameObject.AddComponent<Image>();
                hitArea.color = new Color(0f, 0f, 0f, 0f);
                StatusTooltip.Attach(holder.gameObject, new StatusSnapshot(apply.status, apply.duration, apply.stacks), true);
                StatusChip.AddAmount(holder, apply.status, apply.stacks, s);
                var turns = UIFactory.CreateText(holder, "Turns", apply.duration.ToString(), Mathf.RoundToInt(20f * s), InkStyle.Paper, TextAnchor.LowerRight, FontStyle.Bold);
                turns.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                turns.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(6f, -6f) * s, new Vector2(60f, 28f) * s);
                x += chip + gap;
            }
            return x > 10f ? x : 0f;
        }

        /// <summary>Rebuilds this card's face (e.g. after the rank display setting changes).</summary>
        public void RebuildFace()
        {
            for (int i = Rect.childCount - 1; i >= 0; i--)
            {
                var child = Rect.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            mergeGlow = null;
            shownRank = Card.Rank;
            BuildFace(Rect, Card, 1f);
        }

        /// <summary>Moves the card (eased) to a spot in the fan with a tilt.</summary>
        public void SetPose(Vector2 anchored, float angle)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = null;
            posePosition = anchored;
            poseAngle = angle;
            posed = true;
        }

        public bool Hovered { get { return hovered; } }

        void LateUpdate()
        {
            if (!posed || dragging) return;
            float k = 1f - Mathf.Exp(-PoseSpeed * Time.unscaledDeltaTime);
            var target = hovered ? new Vector2(posePosition.x, HoverLift) : posePosition;
            Rect.anchoredPosition = Vector2.Lerp(Rect.anchoredPosition, target, k);
            float angle = Mathf.LerpAngle(Rect.localEulerAngles.z, hovered ? 0f : poseAngle, k);
            Rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (scaleRoutine == null)
                Rect.localScale = Vector3.Lerp(Rect.localScale, Vector3.one * (hovered ? HoverScale : 1f), k);
        }

        public void ScaleTo(float scale, float duration)
        {
            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            scaleRoutine = StartCoroutine(ScaleRoutine(scale, duration));
        }

        IEnumerator ScaleRoutine(float scale, float duration)
        {
            yield return Tween.Scale(Rect, Vector3.one * scale, duration, Tween.OutBack);
            scaleRoutine = null;   // the fan pose takes the scale back
        }

        GameObject mergeGlow;

        /// <summary>Gold frame (no text) shown while a dragged card would merge here.</summary>
        public void SetMergeHighlight(bool on, int resultRank)
        {
            if (on && mergeGlow == null)
            {
                var glowRoot = UIFactory.CreateRect("MergeGlow", transform).Fill(-5f);
                mergeGlow = glowRoot.gameObject;
                const float t = 5f;
                var edges = new[]
                {
                    new[] { 0f, 1f, 1f, 1f, 0f, -t, 0f, 0f },   // top
                    new[] { 0f, 0f, 1f, 0f, 0f, 0f, 0f, t },    // bottom
                    new[] { 0f, 0f, 0f, 1f, 0f, 0f, t, 0f },    // left
                    new[] { 1f, 0f, 1f, 1f, -t, 0f, 0f, 0f },   // right
                };
                foreach (var e in edges)
                {
                    var rt = UIFactory.CreateImage(glowRoot, "Edge", Gold).rectTransform;
                    rt.anchorMin = new Vector2(e[0], e[1]);
                    rt.anchorMax = new Vector2(e[2], e[3]);
                    rt.offsetMin = new Vector2(e[4], e[5]);
                    rt.offsetMax = new Vector2(e[6], e[7]);
                }
            }
            if (mergeGlow == null) return;

            bool wasOn = mergeGlow.activeSelf;
            mergeGlow.SetActive(on);
            if (!on) return;
            mergeGlow.transform.SetAsLastSibling();
            if (!wasOn) StartCoroutine(Tween.Punch(mergeGlow.transform, 0.08f, 0.18f));
        }

        public void StopAnimations()
        {
            StopAllCoroutines();
            moveRoutine = scaleRoutine = null;
            posed = false;
        }

        public IEnumerator FlyAndVanish(Vector3 worldTarget, float duration)
        {
            hand.HidePreview(this);
            StopAnimations();
            Group.alpha = 1f;
            Group.blocksRaycasts = false;
            SetHovered(false);
            StartCoroutine(Tween.Scale(Rect, Vector3.one * 0.45f, duration));
            StartCoroutine(Tween.Fade(Group, 0f, duration));
            yield return Tween.MoveWorld(Rect, worldTarget, duration);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (hand != null) hand.HidePreview(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (longPressed)
            {
                longPressed = false;   // the hold opened the details; releasing doesn't also queue the card
                return;
            }
            hand.HidePreview(this);
            hand.View.OnCardClicked(Card);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            longPressed = false;
            pressing = true;
            pressTime = 0f;
            pressPosition = eventData.position;
            // A drag only starts past the hold slop, so a slightly shaky hold still opens the card.
            if (EventSystem.current != null) EventSystem.current.pixelDragThreshold = Mathf.RoundToInt(HoldSlop);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressing = false;
        }

        /// <summary>How far (screen px) the mouse may wander during a hold before it counts as a drag instead.</summary>
        static float HoldSlop { get { return Mathf.Max(12f, Screen.height * 0.03f); } }

        void Update()
        {
            if (!pressing) return;
            if (!Input.GetMouseButton(0) || dragging || ((Vector2)Input.mousePosition - pressPosition).magnitude > HoldSlop)
            {
                pressing = false;
                return;
            }
            pressTime += Time.unscaledDeltaTime;
            if (pressTime < LongPressSeconds) return;
            pressing = false;
            longPressed = true;
            hand.View.OpenCardDetail(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Once the hold has opened the card, releasing / moving doesn't drag it.
            if (longPressed)
            {
                eventData.pointerDrag = null;
                return;
            }
            pressing = false;
            if (!hand.View.InputEnabled || !hand.View.Controller.CanAct) return; // moving costs 1 AP
            hand.HidePreview(this);
            dragging = true;
            SetHovered(false);
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            dragOffset = Rect.position - (Vector3)eventData.position;
            Group.blocksRaycasts = false;
            Rect.SetAsLastSibling();
            Rect.localRotation = Quaternion.identity;
            ScaleTo(1.1f, 0.12f);
            hand.BeginDrag(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            Rect.position = (Vector3)eventData.position + dragOffset;
            hand.UpdateDrag(this);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            Group.blocksRaycasts = true;
            ScaleTo(1f, 0.12f);
            hand.EndDrag(this);
            hand.OnCardDropped(this);
        }

        /// <summary>Slay the Spire-style hover: the card rises out of the fan, straight and bigger, in front of the others
        /// (its neighbours lean aside). The full text is in the long-press details.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!dragging && !eventData.dragging) SetHovered(true);
        }

        void SetHovered(bool on)
        {
            if (hovered == on) return;
            hovered = on;
            if (on) Rect.SetAsLastSibling();
            hand.OnCardHover(this, on);
        }

        /// <summary>Wheel over a hand card: preview its other ranks in place (down = higher). The real card doesn't change,
        /// and moving off it shows its own rank again.</summary>
        public void OnScroll(PointerEventData eventData)
        {
            int max = Card.IsRitual ? 1 : Card.Definition.MaxRank;
            if (max <= 1 || dragging) return;
            if (shownRank == 0) shownRank = Card.Rank;
            int next = Mathf.Clamp(shownRank + (eventData.scrollDelta.y < 0 ? 1 : -1), 1, max);
            if (next == shownRank) return;
            ShowRank(next);
        }

        void ShowRank(int rank)
        {
            shownRank = rank;
            for (int i = Rect.childCount - 1; i >= 0; i--)
            {
                var child = Rect.GetChild(i).gameObject;
                if (child == mergeGlow) continue;
                child.SetActive(false);
                Destroy(child);
            }
            BuildFace(Rect, rank == Card.Rank ? Card : new CardInstance(Card.Definition, Card.Owner, rank), 1f);
            if (mergeGlow != null) mergeGlow.transform.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (shownRank != 0 && shownRank != Card.Rank) ShowRank(Card.Rank);
            if (!dragging) SetHovered(false);
        }
    }
}
