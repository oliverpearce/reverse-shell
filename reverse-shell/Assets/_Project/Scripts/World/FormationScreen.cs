using System;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The formation, Reverse: 1999-style: three active slots and a backup slot, each shown as the member's full-art
    /// Ult card with their name and level, and a strip of everyone who has joined underneath.
    /// Click a slot to select it, then click someone in the strip to put them there (if they were in another slot, the
    /// two swap); Remove (or Backspace) empties the selected slot. There is always at least one active member.
    ///
    /// Two modes, one formation (WorldSession.Formation, saved):
    ///   Party      from the Esc cards: an X (or Esc) closes it, back to the Esc cards.
    ///   Pre-fight  before every battle: the enemies on the left (with their level / BOSS), Start (or Space) fights
    ///              with this formation, Esc (or the X) backs out of the fight.
    /// Keyboard: ←/→ move, ↓ to the strip / ↑ to the slots, Enter places the chosen member in the selected slot.
    /// </summary>
    public class FormationScreen : MonoBehaviour
    {
        const float SlotScale = 1f;            // 5x card pixels (240 x 320)
        const float StripScale = 0.6f;         // 3x
        const float SlotGap = 30f, BackupGap = 70f, StripGap = 26f;
        const float SlotsY = 170f, StripY = -300f;

        OverworldBuilder world;
        RectTransform page;
        Func<BattleSetup> preview;   // the fight as it will run (pre-fight mode), rebuilt after every change
        Action onStart, onClosed;
        int slot;                    // selected formation slot (0-2 active, 3 backup)
        int pick;                    // highlighted member in the strip
        bool inStrip;
        int openedFrame;

        public bool IsOpen { get { return gameObject.activeSelf; } }
        public int LastToggleFrame { get; private set; }
        bool PreFight { get { return onStart != null; } }

        List<CharacterDefinition> Roster { get { return world.Roster; } }
        IList<CharacterDefinition> FullRoster { get { return world.encounterSetup != null ? world.encounterSetup.party : new List<CharacterDefinition>(); } }

        public static FormationScreen Create(RectTransform parent, OverworldBuilder world)
        {
            var rt = UIFactory.CreateRect("FormationScreen", parent).Fill();
            var screen = rt.gameObject.AddComponent<FormationScreen>();
            screen.world = world;
            var bg = UIFactory.CreateImage(rt, "Wash", new Color(0.05f, 0.045f, 0.04f, 0.92f), true);
            bg.rectTransform.Fill();
            InkStyle.Grain(rt, new Color(1f, 0.95f, 0.85f, 0.035f));
            screen.page = UIFactory.CreateRect("Page", rt).Fill();
            rt.gameObject.SetActive(false);
            return screen;
        }

        /// <summary>The Party screen (from the Esc cards). <paramref name="whenClosed"/> runs after X / Esc.</summary>
        public void OpenMenu(Action whenClosed)
        {
            onStart = null;
            preview = null;
            onClosed = whenClosed;
            Show();
        }

        /// <summary>The pre-fight screen: <paramref name="fight"/> builds the battle as it would run now (so the enemy list
        /// follows formation changes); <paramref name="start"/> begins it.</summary>
        public void OpenPreFight(Func<BattleSetup> fight, Action start)
        {
            onStart = start;
            onClosed = null;
            preview = fight;
            Show();
        }

        void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            openedFrame = LastToggleFrame = Time.frameCount;
            Time.timeScale = 0f;
            WorldSession.EnsureFormation(FullRoster);
            slot = 0;
            pick = 0;
            inStrip = false;
            Rebuild();
        }

        void Close(bool started)
        {
            LastToggleFrame = Time.frameCount;
            gameObject.SetActive(false);
            Time.timeScale = 1f;
            SaveGame.Save();
            world.RefreshParty();
            var then = started ? onStart : onClosed;
            onStart = null;
            onClosed = null;
            if (then != null) then();
        }

        void OnDisable()
        {
            if (Time.timeScale == 0f) Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ Layout

        float CenterX { get { return PreFight ? 220f : 0f; } }

        void Rebuild()
        {
            UIFactory.ClearChildren(page);
            var center = new Vector2(0.5f, 0.5f);

            var title = InkStyle.Title(page, "Title", PreFight ? "Prepare for Battle" : "Party", 64, InkStyle.Paper, TextAnchor.UpperLeft);
            title.rectTransform.Place(center, new Vector2(0f, 1f), new Vector2(-860f, 610f), new Vector2(900f, 84f));
            var sub = UIFactory.CreateText(page, "Sub", "Choose up to 3 to fight and 1 backup", 28, InkStyle.PaperDim, TextAnchor.UpperLeft);
            sub.rectTransform.Place(center, new Vector2(0f, 1f), new Vector2(-860f, 524f), new Vector2(900f, 40f));

            if (PreFight) BuildEnemies();
            BuildSlots();
            BuildStrip();
            BuildButtons();

            string keys = "<color=#C2A061>← →</color>  Move      <color=#C2A061>↑ ↓</color>  Slots / Members      <color=#C2A061>Enter</color>  Place      "
                          + "<color=#C2A061>Backspace</color>  Remove      <color=#C2A061>Esc</color>  " + (PreFight ? "Back" : "Close")
                          + (PreFight ? "      <color=#C2A061>Space</color>  Start" : "");
            var hint = UIFactory.CreateText(page, "Keys", keys, 26, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            hint.supportRichText = true;
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1760f, 44f));
        }

        void BuildEnemies()
        {
            var fight = preview != null ? preview() : null;
            var tl = new Vector2(0f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            var goal = InkStyle.Label(page, "GoalLabel", "Goal", 28, InkStyle.Gold, TextAnchor.UpperLeft);
            goal.rectTransform.Place(center, tl, new Vector2(-860f, 420f), new Vector2(380f, 40f));
            var goalText = UIFactory.CreateText(page, "Goal", "Defeat all enemies.", 28, InkStyle.Paper, TextAnchor.UpperLeft);
            goalText.rectTransform.Place(center, tl, new Vector2(-860f, 376f), new Vector2(380f, 40f));
            var label = InkStyle.Label(page, "EnemyLabel", "Enemies", 28, InkStyle.Gold, TextAnchor.UpperLeft);
            label.rectTransform.Place(center, tl, new Vector2(-860f, 300f), new Vector2(380f, 40f));
            if (fight == null) return;
            float y = 252f;
            foreach (var enemy in fight.enemies)
            {
                if (enemy == null) continue;
                var row = InkStyle.Panel(page, enemy.DisplayName, new Color(0.08f, 0.075f, 0.07f, 0.9f));
                row.rectTransform.Place(center, tl, new Vector2(-860f, y), new Vector2(390f, 92f));
                InkStyle.Frame(row.rectTransform, UIFactory.WithAlpha(InkStyle.Crimson, 0.7f), 2f);
                var badge = ElementBadgeFor(row.rectTransform, enemy.afflatus);
                badge.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(56f, 56f));
                var name = UIFactory.CreateText(row.rectTransform, "Name", enemy.DisplayName, 30, InkStyle.Paper, TextAnchor.UpperLeft);
                name.rectTransform.Place(tl, tl, new Vector2(80f, -10f), new Vector2(300f, 40f));
                string lv = "Lv " + fight.enemyLevel + (fight.boss ? "   <color=#E05A50>BOSS</color>" : "");
                var level = UIFactory.CreateText(row.rectTransform, "Level", lv, 28, InkStyle.GoldBright, TextAnchor.UpperLeft);
                level.supportRichText = true;
                level.rectTransform.Place(tl, tl, new Vector2(80f, -48f), new Vector2(300f, 36f));
                y -= 106f;
            }
        }

        static RectTransform ElementBadgeFor(RectTransform parent, Afflatus element)
        {
            var img = UIFactory.CreateImage(parent, "Element", Color.white);
            img.sprite = CardIcons.Element(element);
            img.preserveAspect = true;
            return img.rectTransform;
        }

        Vector2 SlotPosition(int i)
        {
            var size = CardView.Size * SlotScale;
            float total = 4 * size.x + 2 * SlotGap + BackupGap;
            float x = -total / 2f + size.x / 2f + i * (size.x + SlotGap) + (i == WorldSession.BackupSlot ? BackupGap - SlotGap : 0f);
            return new Vector2(CenterX + x, SlotsY);
        }

        void BuildSlots()
        {
            var size = CardView.Size * SlotScale;
            var center = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < WorldSession.FormationSlots; i++)
            {
                int index = i;
                var member = WorldSession.Find(FullRoster, WorldSession.Formation[i]);
                bool selected = i == slot && !inStrip;
                var rt = UIFactory.CreateRect("Slot" + i, page).Place(center, center, SlotPosition(i) + (i == slot ? new Vector2(0f, 24f) : Vector2.zero), size);
                var hit = rt.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);

                string caption = i == WorldSession.BackupSlot ? "BACKUP" : i == 0 ? "LEADER" : "";
                if (caption.Length > 0)
                {
                    var cap = InkStyle.Label(rt, "Caption", caption, 26, i == WorldSession.BackupSlot ? new Color(0.55f, 0.75f, 1f) : InkStyle.Gold, TextAnchor.LowerCenter);
                    cap.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(size.x + 40f, 40f));
                }

                if (member != null)
                {
                    var art = CardArt.Ult(member);
                    var face = UIFactory.CreateImage(rt, "Art", art != null ? Color.white : member.color);
                    face.sprite = art;
                    face.rectTransform.Fill();
                    if (art != null) UltShine.Add(rt, art, CardArt.HandPixel * SlotScale);
                    var plate = InkStyle.Panel(rt, "Plate", new Color(0.05f, 0.045f, 0.04f, 0.88f));
                    plate.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(size.x, 92f));
                    var name = InkStyle.Title(plate.rectTransform, "Name", member.DisplayName, 32, InkStyle.Paper, TextAnchor.UpperCenter);
                    name.rectTransform.TopStrip(6f, 44f, 6f);
                    var lv = InkStyle.Title(plate.rectTransform, "Lv", "Lv " + WorldSession.ProgressOf(member).Level, 30, InkStyle.GoldBright, TextAnchor.UpperCenter);
                    lv.rectTransform.TopStrip(48f, 40f, 6f);
                }
                else
                {
                    var empty = UIFactory.CreateImage(rt, "Empty", new Color(0.1f, 0.095f, 0.09f, 0.85f));
                    empty.rectTransform.Fill();
                    InkStyle.Frame(empty.rectTransform, InkStyle.Rule, 2f);
                    var plus = InkStyle.Title(rt, "Plus", "+", 110, InkStyle.PaperDim, TextAnchor.MiddleCenter);
                    plus.rectTransform.Fill();
                }
                InkStyle.Frame(rt, selected ? InkStyle.GoldBright : i == slot ? InkStyle.Gold : UIFactory.WithAlpha(InkStyle.Rule, 0.5f), selected ? 6f : 3f, -6f);

                Clickable(rt.gameObject, () => { inStrip = false; slot = index; Rebuild(); }, null);
            }
        }

        void BuildStrip()
        {
            var roster = Roster;
            var size = CardView.Size * StripScale;
            var center = new Vector2(0.5f, 0.5f);
            float total = roster.Count * size.x + Mathf.Max(0, roster.Count - 1) * StripGap;
            var label = InkStyle.Label(page, "StripLabel", "Members", 26, InkStyle.Gold, TextAnchor.LowerLeft);
            label.rectTransform.Place(center, new Vector2(0f, 0f), new Vector2(CenterX - total / 2f, StripY + size.y / 2f + 10f), new Vector2(400f, 36f));
            for (int i = 0; i < roster.Count; i++)
            {
                var member = roster[i];
                int index = i;
                bool highlighted = inStrip && i == pick;
                var rt = UIFactory.CreateRect(member.DisplayName, page).Place(center, center,
                    new Vector2(CenterX - total / 2f + size.x / 2f + i * (size.x + StripGap), StripY + (highlighted ? 16f : 0f)), size);
                var art = CardArt.Ult(member);
                var face = UIFactory.CreateImage(rt, "Art", art != null ? Color.white : member.color, true);
                face.sprite = art;
                face.rectTransform.Fill();
                int at = Array.IndexOf(WorldSession.Formation, member.name);
                if (at >= 0)
                {
                    // Where they are in the formation: 1-3, or B for the backup.
                    var badge = InkStyle.Panel(rt, "Badge", at == WorldSession.BackupSlot ? new Color(0.3f, 0.45f, 0.8f) : InkStyle.Gold);
                    badge.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(48f, 48f));
                    var n = InkStyle.Title(badge.rectTransform, "N", at == WorldSession.BackupSlot ? "B" : (at + 1).ToString(), 32, InkStyle.Ink, TextAnchor.MiddleCenter);
                    n.rectTransform.Fill();
                }
                var name = UIFactory.CreateText(rt, "Name", member.DisplayName, 26, InkStyle.Paper, TextAnchor.UpperCenter);
                name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(size.x + 60f, 36f));
                if (highlighted) InkStyle.Frame(rt, InkStyle.GoldBright, 5f, -5f);
                Clickable(rt.gameObject, () => Place(roster[index]), () => { inStrip = true; pick = index; });
            }
        }

        void BuildButtons()
        {
            var member = WorldSession.Find(FullRoster, WorldSession.Formation[slot]);
            if (member != null)
            {
                var remove = InkStyle.Button(page, "Remove", "Remove", RemoveSelected, false, 32);
                var p = SlotPosition(slot);
                remove.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                    new Vector2(p.x, p.y - CardView.Size.y * SlotScale / 2f - 120f), new Vector2(200f, 64f));
            }
            if (PreFight)
            {
                var start = InkStyle.Button(page, "Start", "Start", () => Close(true), true, 56);
                start.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 100f), new Vector2(340f, 110f));
            }
            // The same drawn X in both modes (top-right): before a fight it backs out of the fight, like Esc.
            var x = InkStyle.CloseButton(page, () => Close(false));
            x.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(90f, 90f));
        }

        static void Clickable(GameObject go, Action click, Action hover)
        {
            var trigger = go.AddComponent<EventTrigger>();
            var c = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            c.callback.AddListener(_ => click());
            trigger.triggers.Add(c);
            if (hover == null) return;
            var h = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            h.callback.AddListener(_ => hover());
            trigger.triggers.Add(h);
        }

        // ------------------------------------------------------------------ Actions

        void Place(CharacterDefinition member)
        {
            WorldSession.Assign(slot, member, FullRoster);
            inStrip = false;
            Rebuild();
        }

        void RemoveSelected()
        {
            WorldSession.ClearSlot(slot, FullRoster);
            Rebuild();
        }

        void Update()
        {
            if (Time.frameCount == openedFrame) return;
            int count = inStrip ? Roster.Count : WorldSession.FormationSlots;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Move(-1, count);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Move(1, count);
            if ((Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) && !inStrip && Roster.Count > 0) { inStrip = true; pick = Mathf.Clamp(pick, 0, Roster.Count - 1); Rebuild(); }
            if ((Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) && inStrip) { inStrip = false; Rebuild(); }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.F))
            {
                if (inStrip && pick < Roster.Count) Place(Roster[pick]);
                else { inStrip = true; Rebuild(); }
            }
            if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete)) RemoveSelected();
            if (PreFight && Input.GetKeyDown(KeyCode.Space)) Close(true);
            else if (Input.GetKeyDown(KeyCode.Escape)) Close(false);
        }

        void Move(int step, int count)
        {
            if (count <= 0) return;
            if (inStrip) pick = (pick + step + count) % count;
            else slot = (slot + step + count) % count;
            Rebuild();
        }
    }
}
