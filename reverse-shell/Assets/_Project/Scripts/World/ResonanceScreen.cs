using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The Esc menu's Resonance card (bible §3): the protagonist's resonances as pixel cards in a row, only the ones
    /// they have (Physical, and Electric once absorbed, or always in Sandbox Mode); the row centres and shrinks to fit
    /// as more unlock. Below it are the shown resonance's innate cards and Ult, in its colour; clicking or holding one
    /// opens it (<see cref="CardDetailPopup"/>: the full text). Left/Right (or the mouse) moves, Enter / F / Space / a
    /// click switches (free outside combat), Esc goes back to the Esc cards. The active resonance sits in a gold frame.
    /// (Locked resonances can be shown again by building the row from <see cref="Order"/> with Open = false.)
    /// </summary>
    public class ResonanceScreen : MonoBehaviour
    {
        const float MaxCardScale = 1f, MaxRowWidth = 1700f;   // the row shrinks below 1 only when it would overflow
        const float Gap = 36f;
        const float RowY = 170f, SelectedScale = 1.1f, SelectedLift = 26f;
        const float PreviewY = -250f, PreviewScale = 0.8f, PreviewGap = 20f;

        /// <summary>The order shown: the starting Core, the story's Electric, then the future resonances.</summary>
        static readonly Afflatus[] Order =
            { Afflatus.None, Afflatus.Volt, Afflatus.Fire, Afflatus.Toxin, Afflatus.Earth, Afflatus.Light, Afflatus.Void };

        class Entry
        {
            public Afflatus Element;
            public bool Open;
            public RectTransform Rect;
            public CanvasGroup Group;
            public RectTransform ActiveFrame;
        }

        OverworldBuilder world;
        RectTransform root, row, preview;
        readonly List<Entry> entries = new List<Entry>();
        CharacterDefinition member;
        BattleUnit previewUnit;
        int index;
        int openedFrame;
        System.Action onClosed;

        public bool IsOpen { get { return gameObject.activeSelf; } }

        /// <summary>The frame it last opened or closed (so the Esc that closes it doesn't reopen the pause menu).</summary>
        public int LastToggleFrame { get; private set; }

        public static ResonanceScreen Create(RectTransform parent, OverworldBuilder world)
        {
            var rt = UIFactory.CreateRect("ResonanceScreen", parent).Fill();
            var screen = rt.gameObject.AddComponent<ResonanceScreen>();
            screen.world = world;
            screen.root = rt;
            var dim = UIFactory.CreateImage(rt, "Dim", new Color(0.05f, 0.045f, 0.04f, 0.9f), true);
            dim.rectTransform.Fill();
            InkStyle.Grain(rt, new Color(1f, 0.95f, 0.85f, 0.035f));
            var title = InkStyle.Title(rt, "Title", "RESONANCE", 72, InkStyle.Paper, TextAnchor.MiddleCenter);
            title.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 110f));
            screen.row = UIFactory.CreateRect("Row", rt).Fill();
            screen.preview = UIFactory.CreateRect("Preview", rt).Fill();
            var keys = UIFactory.CreateText(rt, "Keys",
                "<color=#C2A061>← →</color>  Choose      <color=#C2A061>Enter</color>  Resonate      <color=#C2A061>Esc</color>  Back",
                28, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            keys.supportRichText = true;
            keys.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1600f, 50f));
            rt.gameObject.SetActive(false);
            return screen;
        }

        public void Open(System.Action whenClosed)
        {
            if (IsOpen) return;
            onClosed = whenClosed;
            member = world.Roster.Find(m => m != null && m.resonances.Count > 0);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            openedFrame = LastToggleFrame = Time.frameCount;
            BuildRow();
            index = Mathf.Max(0, entries.FindIndex(e => e.Element == WorldSession.ElementOf(member)));
            foreach (var e in entries) e.Rect.localScale = Vector3.one;
            BuildPreview();
        }

        public void Close()
        {
            if (!IsOpen) return;
            CardDetailPopup.CloseCurrent();
            LastToggleFrame = Time.frameCount;
            gameObject.SetActive(false);
            var then = onClosed;
            onClosed = null;
            if (then != null) then();
        }

        // ------------------------------------------------------------------ The element row

        void BuildRow()
        {
            UIFactory.ClearChildren(row);
            entries.Clear();
            var available = WorldSession.AvailableResonances(member);
            var shown = new List<Afflatus>();
            foreach (var element in Order) if (available.Contains(element)) shown.Add(element);
            int n = Mathf.Max(1, shown.Count);
            float s = Mathf.Min(MaxCardScale, (MaxRowWidth - (n - 1) * Gap) / (n * CardView.Size.x));
            var size = CardView.Size * s;
            float total = n * size.x + (n - 1) * Gap;
            for (int i = 0; i < shown.Count; i++)
            {
                var e = new Entry { Element = shown[i], Open = true };
                e.Rect = UIFactory.CreateRect("Res_" + e.Element, row);
                e.Rect.sizeDelta = size;
                e.Rect.anchoredPosition = new Vector2(-total / 2f + size.x / 2f + i * (size.x + Gap), RowY);
                e.Group = e.Rect.gameObject.AddComponent<CanvasGroup>();
                BuildFace(e, s);
                int captured = i;
                var hit = e.Rect.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                var trigger = e.Rect.gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => Select(captured));
                trigger.triggers.Add(enter);
                var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                click.callback.AddListener(_ => { Select(captured); Choose(); });
                trigger.triggers.Add(click);
                entries.Add(e);
            }
            RefreshActive();
        }

        /// <summary>
        /// A pixel card like the Esc menu's: the swirl in the element's colour, the frame, a centre icon (the
        /// resonance's first card, or a lock) and the name (or LOCKED) on the plate. The gold frame marks the active one.
        /// </summary>
        void BuildFace(Entry e, float s)
        {
            float u = CardArt.HandPixel * s;
            var color = e.Open ? AfflatusInfo.ResonanceColor(member != null ? member.color : Color.white, e.Element)
                               : UIFactory.Darken(AfflatusInfo.Color(e.Element), 0.45f);
            var swirl = UIFactory.CreateImage(e.Rect, "Swirl", color);
            swirl.sprite = CardArt.Swirl;
            swirl.rectTransform.Fill();
            var frame = UIFactory.CreateImage(e.Rect, "Frame", e.Open ? Color.white : new Color(0.6f, 0.58f, 0.62f));
            frame.sprite = CardArt.Frame;
            frame.rectTransform.Fill();

            Sprite art = null;
            if (e.Open)
            {
                var kit = member != null ? member.KitFor(e.Element) : null;
                if (kit != null && kit.cards.Count > 0) art = CardArt.Icon(kit.cards[0]);
                if (art == null) art = CardIcons.Element(e.Element);
            }
            else art = CardArt.Icon("Ui_Lock");
            if (art != null)
            {
                var icon = UIFactory.CreateImage(e.Rect, "Icon", Color.white);
                icon.sprite = art;
                icon.preserveAspect = true;
                CardArt.PlacePixels(icon.rectTransform, CardArt.ArtX, CardArt.ArtY, CardArt.ArtSize, CardArt.ArtSize, u);
            }

            var plate = CardArt.NamePlate;
            string label = e.Open ? AfflatusInfo.Display(e.Element).ToUpperInvariant() : "LOCKED";
            var name = UIFactory.CreateText(e.Rect, "Name", label, Mathf.RoundToInt(26 * s), e.Open ? InkStyle.Paper : InkStyle.Muted, TextAnchor.MiddleCenter)
                .BestFitCardPlate(Mathf.RoundToInt(8 * s), Mathf.RoundToInt(26 * s));
            CardArt.PlacePixels(name.rectTransform, plate.x, plate.y - 1, plate.width, plate.height + 2, u);

            e.ActiveFrame = UIFactory.CreateRect("Active", e.Rect).Fill();
            e.ActiveFrame.offsetMin = new Vector2(-12f, -12f);   // just outside the card
            e.ActiveFrame.offsetMax = new Vector2(12f, 12f);
            InkStyle.HeroFrame(e.ActiveFrame, InkStyle.GoldBright, 6f);
        }

        void RefreshActive()
        {
            var active = WorldSession.ElementOf(member);
            foreach (var e in entries) e.ActiveFrame.gameObject.SetActive(e.Open && e.Element == active);
        }

        // ------------------------------------------------------------------ The shown resonance's cards

        /// <summary>The shown resonance's innate cards and Ult, coloured as they'll be in battle. Nothing when locked.</summary>
        void BuildPreview()
        {
            UIFactory.ClearChildren(preview);
            if (member == null || index < 0 || index >= entries.Count || !entries[index].Open) return;
            var kit = member.KitFor(entries[index].Element);
            if (kit == null) return;
            var cards = new List<CardDefinition>();
            foreach (var c in kit.cards) if (c != null) cards.Add(c);
            var ult = kit.ultimate != null ? kit.ultimate : member.ritual;   // a kit without its own Ult uses the ritual
            if (ult != null) cards.Add(ult);

            var unit = previewUnit = new BattleUnit(member, Team.Player);
            unit.ApplyLoadout(new Loadout { Cards = cards, Parts = WorldSession.PartsOf(member), Element = kit.element, Ultimate = ult });
            unit.SetLevel(WorldSession.ProgressOf(member).Level);

            var size = CardView.Size * PreviewScale;
            float total = cards.Count * size.x + Mathf.Max(0, cards.Count - 1) * PreviewGap;
            var center = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < cards.Count; i++)
            {
                var holder = UIFactory.CreateRect("Slot", preview).Place(center, center,
                    new Vector2(-total / 2f + size.x / 2f + i * (size.x + PreviewGap), PreviewY), size);
                var ability = AbilityCard.Create(holder, unit, cards[i], 0, PreviewScale, true);
                var rt = (RectTransform)ability.transform;
                rt.Fill();
                var card = cards[i];
                // Click or hold to read it, like the Units page and the battle inspect sheet.
                ability.OnClick = () => OpenCard(card, rt);
                ability.OnLongPress = () => OpenCard(card, rt);
            }
        }

        void OpenCard(CardDefinition card, RectTransform from)
        {
            if (CardDetailPopup.IsOpen || previewUnit == null) return;
            CardDetailPopup.Open(root, previewUnit, card, 1, from);
        }

        // ------------------------------------------------------------------ Input + animation

        void Select(int i)
        {
            int next = Mathf.Clamp(i, 0, entries.Count - 1);
            if (next == index) return;
            index = next;
            BuildPreview();
        }

        /// <summary>Switches to the shown resonance (free outside combat); a locked one just shakes.</summary>
        void Choose()
        {
            if (index < 0 || index >= entries.Count) return;
            var e = entries[index];
            if (!e.Open || member == null)
            {
                StartCoroutine(Tween.Punch(e.Rect, 0.06f, 0.18f));
                return;
            }
            WorldSession.SetResonance(e.Element);
            SaveGame.Save();
            world.RefreshPartyHud();
            RefreshActive();
            StartCoroutine(Tween.Punch(e.Rect, 0.1f, 0.22f));
        }

        void Update()
        {
            if (Time.frameCount == openedFrame) return;
            if (CardDetailPopup.IsOpen) return;   // the open card takes Esc / clicks first
            if (UIKeys.Pressed(KeyCode.LeftArrow) || UIKeys.Pressed(KeyCode.A)) Select(index - 1);
            if (UIKeys.Pressed(KeyCode.RightArrow) || UIKeys.Pressed(KeyCode.D)) Select(index + 1);
            if (UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.Space) || UIKeys.Pressed(KeyCode.F))
                Choose();
            else if (UIKeys.Back || UIKeys.Pressed(KeyCode.Backspace)) Close();
        }

        void LateUpdate()
        {
            // Like the Esc cards: the shown card rises, grows and is fully lit; locked ones stay dimmer.
            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                bool on = i == index;
                e.Rect.localScale = Vector3.Lerp(e.Rect.localScale, Vector3.one * (on ? SelectedScale : 1f), k);
                var p = e.Rect.anchoredPosition;
                p.y = Mathf.Lerp(p.y, on ? RowY + SelectedLift : RowY, k);
                e.Rect.anchoredPosition = p;
                float alpha = on ? 1f : (e.Open ? 0.7f : 0.45f);
                e.Group.alpha = Mathf.Lerp(e.Group.alpha, alpha, k);
                if (on) e.Rect.SetAsLastSibling();
            }
        }
    }
}
