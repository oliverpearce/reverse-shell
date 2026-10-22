using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The Escape menu, in a Persona 5 style: a lilac page with slowly turning black burst rays and halftone dots,
    /// a huge tilted title, slanted black name plates (the selected one flips to white with a black offset and
    /// a little jitter), a giant cut-in of the selected member and tilted stat cards.
    /// Up/Down (W/S) select, F / Enter / Space makes the selected member the leader (the character you play as),
    /// Esc closes. The game is paused while it's open; everything animates on unscaled time.
    /// </summary>
    public class PartyMenu : MonoBehaviour
    {
        static readonly Color Page = PersonaStyle.Lilac;
        static readonly Color Accent = PersonaStyle.LilacDeep;        // on white surfaces
        static readonly Color Glow = PersonaStyle.LilacLight;         // on black surfaces
        static readonly Color DeepShadow = new Color(0.2f, 0.1f, 0.38f);
        static readonly Color Black = PersonaStyle.Black;
        static readonly Color White = PersonaStyle.White;

        OverworldBuilder world;
        CanvasGroup group;
        RectTransform list, detail, cutIn, burst;
        readonly List<RectTransform> rows = new List<RectTransform>();
        List<CharacterDefinition> party = new List<CharacterDefinition>();
        int selected;
        float openT, cutT, popT;
        float savedTimeScale = 1f;

        static Texture2D halftone, rays;
        static Sprite slant;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        /// <summary>Frame the menu last opened or closed: the same key press must not toggle it twice.</summary>
        public int LastToggleFrame { get; private set; }

        public static PartyMenu Create(RectTransform parent, OverworldBuilder world)
        {
            var rt = UIFactory.CreateRect("PartyMenu", parent).Fill();
            var menu = rt.gameObject.AddComponent<PartyMenu>();
            menu.world = world;
            menu.group = rt.gameObject.AddComponent<CanvasGroup>();
            menu.BuildFrame(rt);
            rt.gameObject.SetActive(false);
            return menu;
        }

        // ------------------------------------------------------------------ Open / close

        public void Open()
        {
            if (IsOpen) return;
            LastToggleFrame = Time.frameCount;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            openT = 0f;
            Refresh(0);
        }

        public void Close()
        {
            if (!IsOpen) return;
            LastToggleFrame = Time.frameCount;
            Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
            gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (Time.timeScale == 0f) Time.timeScale = 1f; // never leave the world paused
        }

        void Refresh(int select)
        {
            party = world.Party;
            selected = Mathf.Clamp(select, 0, Mathf.Max(0, party.Count - 1));
            BuildList();
            ShowDetail();
        }

        // ------------------------------------------------------------------ Input + animation

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            openT = Mathf.Min(1f, openT + dt / 0.28f);
            cutT = Mathf.Min(1f, cutT + dt / 0.2f);
            popT = Mathf.Min(1f, popT + dt / 0.16f);
            if (burst != null) burst.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 6f);

            if (Time.frameCount != LastToggleFrame) // ignore the key that opened the menu
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
                {
                    Close();
                    return;
                }
                if (party.Count > 0)
                {
                    if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Select((selected - 1 + party.Count) % party.Count);
                    if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Select((selected + 1) % party.Count);
                    if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) MakeLeader();
                }
            }

            // Open: the page punches in, plates slam in from the left one after another.
            group.alpha = Mathf.Clamp01(openT * 3f);
            transform.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, Ease(openT));
            for (int i = 0; i < rows.Count; i++)
            {
                float t = BackOut(Mathf.Clamp01(openT * 1.8f - i * 0.18f));
                bool on = i == selected;
                float jitter = on ? Mathf.Sin(Time.unscaledTime * 9f) * 0.6f : 0f;
                rows[i].anchoredPosition = new Vector2(Mathf.LerpUnclamped(-700f, on ? 40f : 0f, t), rows[i].anchoredPosition.y);
                rows[i].localRotation = Quaternion.Euler(0f, 0f, RowAngle(i) + jitter);
                rows[i].localScale = Vector3.one * (on ? Mathf.Lerp(1.15f, 1.06f, BackOut(popT)) : 1f);
            }
            if (detail != null) detail.anchoredPosition = new Vector2(Mathf.Lerp(200f, 0f, Ease(openT)), 0f);
            if (cutIn != null)
            {
                float c = BackOut(cutT);
                cutIn.anchoredPosition = new Vector2(Mathf.LerpUnclamped(260f, 0f, c), Mathf.LerpUnclamped(-40f, 0f, c));
                var cg = cutIn.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = Mathf.Clamp01(cutT * 2f);
            }
        }

        static float RowAngle(int i) { return i % 2 == 0 ? 4f : -2.5f; }

        void Select(int index)
        {
            if (index == selected) return;
            selected = index;
            popT = 0f;
            BuildList();
            ShowDetail();
        }

        void MakeLeader()
        {
            if (party.Count == 0 || selected == 0) return; // already leading
            world.SetLeader(party[selected]);
            Refresh(0); // the new leader moves to the top
            popT = 0f;
        }

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        static float BackOut(float t)
        {
            const float c1 = 1.9f, c3 = c1 + 1f;
            float x = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        // ------------------------------------------------------------------ Static frame

        void BuildFrame(RectTransform root)
        {
            UIFactory.CreateImage(root, "Page", Page, true).rectTransform.Fill();

            // Black burst rays turning slowly behind everything, from the cut-in.
            var burstImg = new GameObject("Burst", typeof(RectTransform)).AddComponent<RawImage>();
            burst = burstImg.rectTransform;
            burst.SetParent(root, false);
            burst.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, -40f), new Vector2(3200f, 3200f));
            burstImg.texture = RayTexture();
            burstImg.color = new Color(0f, 0f, 0f, 0.18f);
            burstImg.raycastTarget = false;

            // Halftone dots.
            var dots = new GameObject("Halftone", typeof(RectTransform)).AddComponent<RawImage>();
            dots.rectTransform.SetParent(root, false);
            dots.rectTransform.Fill();
            dots.texture = HalftoneTexture();
            dots.color = new Color(0f, 0f, 0f, 0.22f);
            dots.raycastTarget = false;
            var size = UIFactory.ReferenceResolution;
            dots.uvRect = new Rect(0f, 0f, size.x / 22f, size.y / 22f);

            // A big black diagonal slab along the bottom-left, like a torn page.
            var slab = UIFactory.CreateImage(root, "Slab", Black);
            slab.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(380f, 60f), new Vector2(1500f, 420f));
            slab.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);

            // Title: huge, tilted, white with a hard black shadow.
            var titleShadow = UIFactory.CreateText(root, "TitleShadow", "PARTY", 190, Black, TextAnchor.UpperLeft);
            titleShadow.font = UIFactory.DisplayFont;
            titleShadow.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(102f, -40f), new Vector2(900f, 230f));
            titleShadow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);
            var title = UIFactory.CreateText(root, "Title", "PARTY", 190, White, TextAnchor.UpperLeft);
            title.font = UIFactory.DisplayFont;
            title.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(86f, -26f), new Vector2(900f, 230f));
            title.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 6f);

            // Hint bar.
            var bar = UIFactory.CreateImage(root, "HintBar", Black);
            bar.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(40f, 40f), new Vector2(1400f, 70f));
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2f);
            var hint = UIFactory.CreateText(bar.transform, "Hint", "↑↓  SELECT     F  SET AS LEADER     ESC  BACK", 30, White, TextAnchor.MiddleCenter);
            hint.font = UIFactory.DisplayFont;
            hint.rectTransform.Fill(10f);

            list = UIFactory.CreateRect("List", root).Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, -330f), new Vector2(680f, 700f));
            var detailHolder = UIFactory.CreateRect("DetailHolder", root).Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-110f, -120f), new Vector2(720f, 1020f));
            detail = UIFactory.CreateRect("Detail", detailHolder).Fill();
        }

        // ------------------------------------------------------------------ Party list

        void BuildList()
        {
            UIFactory.ClearChildren(list);
            rows.Clear();
            const float rowH = 140f, gap = 34f;
            for (int i = 0; i < party.Count; i++)
            {
                var member = party[i];
                var progress = WorldSession.ProgressOf(member);
                bool on = i == selected;
                int captured = i;

                var row = UIFactory.CreateRect(member.DisplayName, list);
                row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
                row.pivot = new Vector2(0f, 0.5f);
                row.anchoredPosition = new Vector2(0f, -i * (rowH + gap) - rowH / 2f);
                row.sizeDelta = new Vector2(620f, rowH);
                rows.Add(row);

                // Plate: black slant (white when selected, with a red offset shadow behind).
                if (on)
                {
                    var shadow = Slant(row, "Shadow", Black);
                    shadow.rectTransform.Fill();
                    shadow.rectTransform.anchoredPosition = new Vector2(16f, -14f);
                }
                var plate = Slant(row, "Plate", on ? White : Black);
                plate.rectTransform.Fill();
                plate.raycastTarget = true;
                var click = plate.gameObject.AddComponent<Button>();
                click.transition = Selectable.Transition.None;
                var nav = click.navigation;
                nav.mode = Navigation.Mode.None;
                click.navigation = nav;
                click.onClick.AddListener(() =>
                {
                    if (selected == captured) MakeLeader();
                    else Select(captured);
                });

                // Member color as a slashed stripe on the plate's left.
                var stripe = Slant(row, "Color", member.color);
                stripe.rectTransform.anchorMin = Vector2.zero;
                stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
                stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
                stripe.rectTransform.offsetMin = new Vector2(10f, 14f);
                stripe.rectTransform.offsetMax = new Vector2(46f, -14f);

                var ink = on ? Black : White;
                var name = UIFactory.CreateText(row, "Name", member.DisplayName.ToUpperInvariant(), 84, ink, TextAnchor.MiddleLeft);
                name.font = UIFactory.DisplayFont;
                name.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(66f, 6f), new Vector2(360f, 110f));
                var cls = UIFactory.CreateText(row, "Class", (member.archetype ?? "").ToUpperInvariant(), 20, on ? Accent : new Color(1f, 1f, 1f, 0.7f), TextAnchor.LowerLeft, FontStyle.Bold);
                cls.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(72f, 8f), new Vector2(360f, 30f));

                var lv = UIFactory.CreateText(row, "Level", "<size=30>LV</size>" + progress.Level, 76, on ? Accent : White, TextAnchor.MiddleRight);
                lv.font = UIFactory.DisplayFont;
                lv.supportRichText = true;
                lv.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 4f), new Vector2(180f, 110f));

                if (i == 0)
                {
                    // "LEADER" tag sticking off the top edge, tilted the other way.
                    var tag = Slant(row, "LeaderTag", Accent);
                    tag.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(380f, 2f), new Vector2(170f, 44f));
                    tag.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                    var tagText = UIFactory.CreateText(tag.transform, "Text", "LEADER", 30, White, TextAnchor.MiddleCenter);
                    tagText.font = UIFactory.DisplayFont;
                    tagText.rectTransform.Fill();
                }
            }
        }

        // ------------------------------------------------------------------ Cut-in + details

        void ShowDetail()
        {
            UIFactory.ClearChildren(detail);
            if (cutIn != null) Destroy(cutIn.parent.gameObject);
            cutIn = null;
            if (party.Count == 0) return;
            var member = party[selected];
            var progress = WorldSession.ProgressOf(member);

            BuildCutIn(member);

            // Name card: white, tilted, black type, red under-slash.
            var nameCard = Card(detail, "NameCard", White, new Vector2(0f, 0f), new Vector2(720f, 170f), -3f);
            var name = UIFactory.CreateText(nameCard, "Name", member.DisplayName.ToUpperInvariant(), 120, Black, TextAnchor.MiddleLeft).BestFit(40, 120);
            name.font = UIFactory.DisplayFont;
            name.rectTransform.Fill(24f);
            var cls = UIFactory.CreateText(nameCard, "Class", member.archetype + "  /  " + member.afflatus + "  /  " + member.damageType, 20, Accent, TextAnchor.LowerRight, FontStyle.Bold);
            cls.rectTransform.Fill(14f);
            var slash = Slant(detail, "Slash", Accent);
            slash.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -176f), new Vector2(560f, 16f));
            slash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);

            // Level + XP: black card.
            int need = Leveling.XpToNext(progress.Level);
            var lvCard = Card(detail, "LevelCard", Black, new Vector2(30f, -210f), new Vector2(330f, 200f), 2f);
            Big(lvCard, "LV", progress.Level.ToString(), White, Glow);
            Bar(lvCard, progress.Xp / (float)Mathf.Max(1, need), White, new Color(1f, 1f, 1f, 0.2f), progress.Xp + " / " + need + " XP");

            // HP: red-on-black card.
            int hp = WorldSession.HPOf(member), max = WorldSession.MaxHPOf(member);
            var hpCard = Card(detail, "HpCard", Black, new Vector2(390f, -220f), new Vector2(330f, 200f), -2f);
            Big(hpCard, "HP", hp.ToString(), White, Glow);
            Bar(hpCard, hp / (float)Mathf.Max(1, max), Glow, new Color(1f, 1f, 1f, 0.2f), "MAX " + max);

            // Stats: four small tilted white tags.
            float scale = Leveling.StatScale(progress.Level, member.growthPerLevel);
            var s = member.stats;
            StatTag(0, "ATK", Mathf.RoundToInt(s.attack * scale).ToString());
            StatTag(1, "P.DEF", Mathf.RoundToInt(s.physicalDefense * scale).ToString());
            StatTag(2, "S.DEF", Mathf.RoundToInt(s.specialDefense * scale).ToString());
            StatTag(3, "CRIT", Mathf.RoundToInt(s.critRate * 100f) + "%");

            // Abilities: black slanted strips.
            float y = -640f;
            var head = UIFactory.CreateText(detail, "SkillsHeader", "SKILLS", 54, White, TextAnchor.LowerLeft);
            head.font = UIFactory.DisplayFont;
            head.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, y), new Vector2(400f, 70f));
            head.gameObject.AddComponent<Shadow>().effectColor = Black;
            y -= 78f;
            int n = 0;
            foreach (var card in member.incantations)
                if (card != null) Skill(card, ref y, n++);
            if (member.ritual != null) Skill(member.ritual, ref y, n);
        }

        /// <summary>Persona-style cut-in: the member's sprite huge over a white slash, with a black offset silhouette.</summary>
        void BuildCutIn(CharacterDefinition member)
        {
            var holder = UIFactory.CreateRect("CutInHolder", transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-70f, -30f), new Vector2(760f, 1000f));
            holder.SetSiblingIndex(list.GetSiblingIndex()); // behind the name plates and the detail column
            cutIn = UIFactory.CreateRect("CutIn", holder).Fill();
            cutIn.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            cutT = 0f;

            var slashA = Slant(cutIn, "WhiteSlash", White);
            slashA.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(1100f, 190f));
            slashA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var slashB = Slant(cutIn, "ColorSlash", member.color);
            slashB.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, -290f), new Vector2(1100f, 34f));
            slashB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 18f);

            Sprite frame = null;
            if (member.animations != null)
            {
                var frames = member.animations.Frames("idle", Facing.Down, true) ?? member.animations.Frames("walk", Facing.Down, true);
                if (frames != null && frames.Length > 0) frame = frames[0];
            }
            if (frame == null) return;
            var shadow = UIFactory.CreateImage(cutIn, "Silhouette", Black);
            shadow.sprite = frame;
            shadow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, -24f), new Vector2(832f, 832f)); // 64px at 13x
            var sprite = UIFactory.CreateImage(cutIn, "Sprite", Color.white);
            sprite.sprite = frame;
            sprite.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(832f, 832f));
            sprite.gameObject.AddComponent<UISpriteAnimator>().Init(member.animations, Facing.Down, sprite);
        }

        static RectTransform Card(RectTransform parent, string name, Color color, Vector2 position, Vector2 size, float angle)
        {
            var shadow = UIFactory.CreateImage(parent, name + "Shadow", color == Black ? DeepShadow : Black);
            shadow.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), position + new Vector2(12f, -12f), size);
            shadow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            var card = UIFactory.CreateImage(parent, name, color);
            card.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
            card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return card.rectTransform;
        }

        static void Big(RectTransform card, string label, string value, Color valueColor, Color labelColor)
        {
            var l = UIFactory.CreateText(card, "Label", label, 40, labelColor, TextAnchor.UpperLeft);
            l.font = UIFactory.DisplayFont;
            l.rectTransform.Fill(14f);
            var v = UIFactory.CreateText(card, "Value", value, 110, valueColor, TextAnchor.UpperRight).BestFit(40, 110);
            v.font = UIFactory.DisplayFont;
            v.rectTransform.anchorMin = Vector2.zero;
            v.rectTransform.anchorMax = Vector2.one;
            v.rectTransform.offsetMin = new Vector2(70f, 56f);
            v.rectTransform.offsetMax = new Vector2(-16f, -4f);
        }

        static void Bar(RectTransform card, float fill, Color color, Color track, string caption)
        {
            var bg = UIFactory.CreateImage(card, "Track", track);
            bg.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 34f), new Vector2(card.sizeDelta.x - 32f, 14f));
            var f = UIFactory.CreateImage(bg.transform, "Fill", color);
            f.rectTransform.anchorMin = Vector2.zero;
            f.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            f.rectTransform.offsetMin = f.rectTransform.offsetMax = Vector2.zero;
            var c = UIFactory.CreateText(card, "Caption", caption, 18, new Color(1f, 1f, 1f, 0.75f), TextAnchor.LowerRight, FontStyle.Bold);
            c.rectTransform.Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 6f), new Vector2(card.sizeDelta.x - 32f, 26f));
        }

        void StatTag(int i, string label, string value)
        {
            float x = (i % 2) * 360f + 20f, y = -460f - (i / 2) * 86f;
            var tag = Card(detail, label, White, new Vector2(x, y), new Vector2(320f, 72f), i % 2 == 0 ? 2f : -2f);
            var l = UIFactory.CreateText(tag, "Label", label, 32, Accent, TextAnchor.MiddleLeft);
            l.font = UIFactory.DisplayFont;
            l.rectTransform.Fill(16f);
            var v = UIFactory.CreateText(tag, "Value", value, 50, Black, TextAnchor.MiddleRight);
            v.font = UIFactory.DisplayFont;
            v.rectTransform.Fill(16f);
        }

        void Skill(CardDefinition card, ref float y, int i)
        {
            var strip = Slant(detail, card.DisplayName, Black);
            strip.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f + (i % 2) * 24f, y), new Vector2(660f, 70f));
            strip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 1.5f : -1.5f);
            var name = UIFactory.CreateText(strip.transform, "Name", card.DisplayName.ToUpperInvariant(), 40, White, TextAnchor.MiddleLeft);
            name.font = UIFactory.DisplayFont;
            name.rectTransform.Fill(28f);
            string kind = card.isRitual ? "ULT" : UnitView.KindLabel(card.kind).ToUpperInvariant();
            var tag = Slant(strip.rectTransform, "Kind", card.isRitual ? Glow : White);
            tag.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(150f, 44f));
            var tagText = UIFactory.CreateText(tag.transform, "Text", kind, 28, Black, TextAnchor.MiddleCenter);
            tagText.font = UIFactory.DisplayFont;
            tagText.rectTransform.Fill();
            y -= 82f;
        }

        // ------------------------------------------------------------------ Generated shapes

        /// <summary>An image using the parallelogram sprite (slanted left and right edges).</summary>
        static Image Slant(RectTransform parent, string name, Color color)
        {
            var img = UIFactory.CreateImage(parent, name, color);
            img.sprite = SlantSprite();
            img.raycastTarget = false;
            return img;
        }

        static Sprite SlantSprite()
        {
            if (slant != null) return slant;
            const int w = 256, h = 64, skew = 18;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < h; y++)
            {
                float left = skew * (1f - y / (float)(h - 1)); // bottom edge shifted right: leans forward
                float right = w - skew * (y / (float)(h - 1));
                for (int x = 0; x < w; x++)
                {
                    float a = Mathf.Clamp01(x + 0.5f - left) * Mathf.Clamp01(right - x - 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return slant = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        static Texture2D HalftoneTexture()
        {
            if (halftone != null) return halftone;
            const int size = 16;
            halftone = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                    halftone.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(3.2f - d)));
                }
            halftone.Apply();
            return halftone;
        }

        static Texture2D RayTexture()
        {
            if (rays != null) return rays;
            const int size = 256, count = 14;
            rays = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float angle = Mathf.Atan2(y - size / 2f, x - size / 2f);
                    float s = Mathf.Sin(angle * count);
                    rays.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(s * 6f)));
                }
            rays.Apply();
            return rays;
        }
    }
}
