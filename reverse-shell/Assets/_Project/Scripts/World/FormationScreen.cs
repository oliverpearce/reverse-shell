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
    /// Ult card with their name and level. Everyone who has joined lies on the Members mat (grey with a pearl trim),
    /// which rolls out from the scroll in the bottom-left: click the scroll or the mat (or press Down) to unroll it,
    /// again to roll it back up.
    /// Click a slot to select it, then click someone in the strip to put them there (if they were in another slot, the
    /// two swap); Remove (or Backspace) empties the selected slot. There is always at least one active member.
    /// Or drag and drop: a member from the strip onto a slot puts them there, a slot onto another slot swaps the two
    /// (or moves into an empty one), and a slot dropped anywhere else empties it (never the last active member).
    ///
    /// Two modes, one formation (WorldSession.Formation, saved):
    ///   Party      from the Esc cards: an X (or Esc) closes it, back to the Esc cards.
    ///   Pre-fight  before every battle: the enemies on the left (with their level / BOSS; hover or click one to see
    ///              its cards beside the list, click a card to read it), the Start button fights with this formation,
    ///              Esc (or the X) backs out of the fight.
    /// Keyboard: ←/→ move, ↓ to the strip / ↑ to the slots, Enter places the chosen member in the selected slot.
    /// </summary>
    public class FormationScreen : MonoBehaviour
    {
        const float SlotScale = 1f;            // 5x card pixels (240 x 320)
        const float StripScale = 0.6f;         // 3x
        const float SlotGap = 30f, BackupGap = 70f, StripGap = 26f;
        const float SlotsY = 170f, StripY = -300f;
        const float StartRight = 60f, StartBottom = 100f;   // the Start button's corner offset (bottom-right)
        // The Members mat: its left end (bottom-left of the screen), height, inner padding and roll time.
        const float MembersLeft = -840f, MembersH = 300f, MembersPad = 36f, MembersRoll = 0.35f;

        OverworldBuilder world;
        RectTransform page;
        Func<BattleSetup> preview;   // the fight as it will run (pre-fight mode), rebuilt after every change
        Action onStart, onClosed;
        int slot;                    // selected formation slot (0-2 active, 3 backup)
        int pick;                    // highlighted member in the strip
        bool inStrip;
        int openedFrame;

        // Drag and drop: the floating card, where it came from (-1 = the strip) and the slot under it.
        readonly List<RectTransform> slotRects = new List<RectTransform>();
        RectTransform ghost, dropFrame;
        CanvasGroup dragSource;
        CharacterDefinition dragMember;
        int dragFrom = -1, dropTarget = -1;
        bool dragging;

        // The Members mat: open or rolled up (kept across rebuilds), and how far it is unrolled now.
        bool membersOpen;
        float membersReveal;
        float membersMatW;
        RectTransform membersMask, membersRoll;
        CanvasGroup membersCaption, membersLabel;

        // Hold a member card to see their name (it shows above the card while held).
        const float HoldForName = 0.3f;
        RectTransform holdTag;
        float holdStart;
        bool holdShown;
        int suppressClickFrame = -1;

        // Pre-fight: the hovered enemy's cards, shown beside the enemy list until the pointer leaves both.
        RectTransform enemyCards;
        int enemyShown = -1;
        bool overEnemyRow, overEnemyCards;
        float enemyHideAt;

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
            enemyCards = null;
            enemyShown = -1;
            overEnemyRow = overEnemyCards = false;
            var center = new Vector2(0.5f, 0.5f);

            var title = InkStyle.Title(page, "Title", UIText.Get(PreFight ? "party.title.prefight" : "party.title"), 64, InkStyle.Paper, TextAnchor.UpperLeft);
            title.rectTransform.Place(center, new Vector2(0f, 1f), new Vector2(-860f, 610f), new Vector2(900f, 84f));

            if (PreFight) BuildEnemies();
            BuildSlots();
            BuildStrip();
            BuildButtons();
            // Who beats whom, always in view. Before a fight it sits right-aligned with the Start button, centred in the
            // space between the backup note and the top of Start, as large as that space allows (up to 1.1x).
            if (PreFight)
            {
                float half = page.rect.height > 0f ? page.rect.height / 2f : UIFactory.ReferenceResolution.y / 2f;
                float noteBottom = SlotsY - CardView.Size.y * SlotScale / 2f - 112f - 60f;
                float startTop = -half + StartBottom + HudView.GoSize.y;
                float scale = Mathf.Clamp((noteBottom - startTop - 30f) / ResonanceChartView.Size.y, 0.6f, 1.1f);
                float pageRight = page.rect.width > 0f ? page.rect.width / 2f : UIFactory.ReferenceResolution.x / 2f;
                float chartX = pageRight - StartRight - ResonanceChartView.Size.x * scale / 2f;
                ResonanceChartView.Create(page, new Vector2(chartX, (noteBottom + startTop) / 2f), scale);
            }
            else ResonanceChartView.Create(page, new Vector2(440f, -320f));

            string keys = UIText.Get("party.keys", "close", UIText.Get(PreFight ? "menu.back" : "menu.close"));
            var hint = UIFactory.CreateText(page, "Keys", keys, 26, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            hint.supportRichText = true;
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1760f, 44f));
        }

        void BuildEnemies()
        {
            var fight = preview != null ? preview() : null;
            var tl = new Vector2(0f, 1f);
            var center = new Vector2(0.5f, 0.5f);
            var goal = InkStyle.Label(page, "GoalLabel", UIText.Get("party.goal"), 28, InkStyle.Gold, TextAnchor.UpperLeft);
            goal.rectTransform.Place(center, tl, new Vector2(-860f, 420f), new Vector2(380f, 40f));
            var goalText = UIFactory.CreateText(page, "Goal", UIText.Get("party.goal.text"), 28, InkStyle.Paper, TextAnchor.UpperLeft);
            goalText.rectTransform.Place(center, tl, new Vector2(-860f, 376f), new Vector2(380f, 40f));
            var label = InkStyle.Label(page, "EnemyLabel", UIText.Get("party.enemies"), 28, InkStyle.Gold, TextAnchor.UpperLeft);
            label.rectTransform.Place(center, tl, new Vector2(-860f, 300f), new Vector2(380f, 40f));
            if (fight == null) return;
            float y = 252f;
            for (int i = 0; i < fight.enemies.Count; i++)
            {
                var enemy = fight.enemies[i];
                if (enemy == null) continue;
                var row = InkStyle.Panel(page, enemy.DisplayName, new Color(0.08f, 0.075f, 0.07f, 0.9f), true);
                row.rectTransform.Place(center, tl, new Vector2(-860f, y), new Vector2(390f, 92f));
                HoverEnemy(row.rectTransform, i, enemy, fight.enemyLevel, y);
                InkStyle.Frame(row.rectTransform, UIFactory.WithAlpha(InkStyle.Crimson, 0.7f), 2f);
                var badge = ElementBadgeFor(row.rectTransform, enemy.afflatus);
                badge.Place(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(56f, 56f));
                var name = UIFactory.CreateText(row.rectTransform, "Name", enemy.DisplayName, 30, InkStyle.Paper, TextAnchor.UpperLeft);
                name.rectTransform.Place(tl, tl, new Vector2(80f, -10f), new Vector2(300f, 40f));
                string lv = UIText.Get("common.lv") + " " + fight.enemyLevel + (fight.boss ? "   <color=#E05A50>" + UIText.Get("common.boss") + "</color>" : "");
                var level = UIFactory.CreateText(row.rectTransform, "Level", lv, 28, InkStyle.GoldBright, TextAnchor.UpperLeft);
                level.supportRichText = true;
                level.rectTransform.Place(tl, tl, new Vector2(80f, -48f), new Vector2(300f, 36f));
                y -= 106f;
            }
        }

        // ------------------------------------------------------------------ Enemy cards (pre-fight)

        /// <summary>Hovering (or clicking) an enemy row shows its cards beside the list.</summary>
        void HoverEnemy(RectTransform row, int index, EnemyDefinition enemy, int level, float rowY)
        {
            var trigger = row.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { overEnemyRow = true; ShowEnemyCards(index, enemy, level, rowY); });
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { overEnemyRow = false; enemyHideAt = Time.unscaledTime + 0.15f; });
            trigger.triggers.Add(exit);
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(_ => ShowEnemyCards(index, enemy, level, rowY));
            trigger.triggers.Add(click);
        }

        /// <summary>An ink panel to the right of the enemy list: the enemy's name, then its skill cards and its Ult at
        /// this fight's level (icon cards like the Units page; click one for its full text).</summary>
        void ShowEnemyCards(int index, EnemyDefinition enemy, int level, float rowY)
        {
            if (enemyShown == index && enemyCards != null) return;
            HideEnemyCards();
            var cards = new List<KeyValuePair<CardDefinition, int>>();
            foreach (var skill in enemy.skills)
                if (skill != null && skill.card != null) cards.Add(new KeyValuePair<CardDefinition, int>(skill.card, skill.rank));
            if (enemy.ritual != null) cards.Add(new KeyValuePair<CardDefinition, int>(enemy.ritual, 1));
            if (cards.Count == 0) return;

            var unit = new BattleUnit(enemy, Team.Enemy);
            unit.SetLevel(level);
            const float scale = 0.8f, gap = 16f, pad = 20f, head = 54f;
            var size = CardView.Size * scale;
            var center = new Vector2(0.5f, 0.5f);
            var tl = new Vector2(0f, 1f);
            var panel = InkStyle.Panel(page, "EnemyCards", new Color(0.06f, 0.055f, 0.05f, 0.96f), true);
            enemyCards = panel.rectTransform;
            enemyShown = index;
            float w = cards.Count * size.x + (cards.Count - 1) * gap + pad * 2f;
            enemyCards.Place(center, tl, new Vector2(-450f, rowY + 10f), new Vector2(w, size.y + head + pad * 2f));
            InkStyle.Frame(enemyCards, UIFactory.WithAlpha(InkStyle.Crimson, 0.8f), 2f);
            var name = InkStyle.Label(enemyCards, "Name", enemy.DisplayName, 28, InkStyle.Gold, TextAnchor.UpperLeft);
            name.rectTransform.Place(tl, tl, new Vector2(pad, -pad + 4f), new Vector2(w - pad * 2f, 40f));

            var trigger = enemyCards.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => overEnemyCards = true);
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { overEnemyCards = false; enemyHideAt = Time.unscaledTime + 0.15f; });
            trigger.triggers.Add(exit);

            for (int i = 0; i < cards.Count; i++)
            {
                var holder = UIFactory.CreateRect("Slot", enemyCards).Place(tl, tl, new Vector2(pad + i * (size.x + gap), -pad - head), size);
                var ability = AbilityCard.Create(holder, unit, cards[i].Key, cards[i].Value, scale, true);
                var rt = (RectTransform)ability.transform;
                rt.Fill();
                var card = cards[i].Key;
                int rank = cards[i].Value;
                ability.OnClick = () => CardDetailPopup.Open((RectTransform)transform, unit, card, rank, rt);
                ability.OnLongPress = ability.OnClick;
            }
        }

        void HideEnemyCards()
        {
            if (enemyCards != null) Destroy(enemyCards.gameObject);
            enemyCards = null;
            enemyShown = -1;
            overEnemyCards = false;
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
            slotRects.Clear();
            for (int i = 0; i < WorldSession.FormationSlots; i++)
            {
                int index = i;
                var member = WorldSession.Find(FullRoster, WorldSession.Formation[i]);
                bool selected = i == slot && !inStrip;
                // The backup slot only opens once all three fighting slots are filled; until then it's greyed out.
                bool closedBackup = i == WorldSession.BackupSlot && member == null && !WorldSession.BackupAllowed;
                var rt = UIFactory.CreateRect("Slot" + i, page).Place(center, center, SlotPosition(i) + (i == slot ? new Vector2(0f, 24f) : Vector2.zero), size);
                var hit = rt.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                slotRects.Add(rt);
                if (closedBackup) rt.gameObject.AddComponent<CanvasGroup>().alpha = 0.35f;

                string caption = i == WorldSession.BackupSlot ? UIText.Get("party.backup") : i == 0 ? UIText.Get("party.leader") : "";
                if (caption.Length > 0)
                {
                    var cap = InkStyle.Label(rt, "Caption", caption, 26, i == WorldSession.BackupSlot ? new Color(0.55f, 0.75f, 1f) : InkStyle.Gold, TextAnchor.LowerCenter);
                    cap.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(size.x + 40f, 40f));
                }
                if (i == WorldSession.BackupSlot)
                {
                    // What the backup does, under the slot (below a member's name plate when there is one).
                    var note = UIFactory.CreateText(rt, "BackupNote", UIText.Get("party.backup.note"), 22,
                        closedBackup ? InkStyle.Muted : InkStyle.PaperDim, TextAnchor.UpperCenter);
                    note.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, member != null ? -112f : -14f), new Vector2(size.x + 60f, 60f));
                }

                if (member != null)
                {
                    var art = CardArt.Ult(WorldSession.UltimateOf(member), member);
                    var face = UIFactory.CreateImage(rt, "Art", art != null ? Color.white : member.color);
                    face.sprite = art;
                    face.rectTransform.Fill();
                    if (art != null) UltShine.Add(rt, art, CardArt.HandPixel * SlotScale);
                    var plate = InkStyle.Panel(rt, "Plate", new Color(0.05f, 0.045f, 0.04f, 0.88f));
                    plate.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(size.x, 92f));
                    var name = InkStyle.Title(plate.rectTransform, "Name", member.DisplayName, 32, InkStyle.Paper, TextAnchor.UpperCenter);
                    name.rectTransform.TopStrip(6f, 44f, 6f);
                    var lv = InkStyle.Title(plate.rectTransform, "Lv", UIText.Get("common.lv") + " " + WorldSession.ProgressOf(member).Level, 30, InkStyle.GoldBright, TextAnchor.UpperCenter);
                    lv.rectTransform.TopStrip(48f, 40f, 6f);
                }
                else
                {
                    var empty = UIFactory.CreateImage(rt, "Empty", new Color(0.1f, 0.095f, 0.09f, 0.85f));
                    empty.rectTransform.Fill();   // one set of corners: the slot's own frame below
                    var plus = InkStyle.Title(rt, "Plus", "+", 110, InkStyle.PaperDim, TextAnchor.MiddleCenter);
                    plus.rectTransform.Fill();
                }
                InkStyle.Frame(rt, selected ? InkStyle.GoldBright : i == slot ? InkStyle.Gold : UIFactory.WithAlpha(InkStyle.Rule, 0.5f), selected ? 6f : 3f, -6f);

                Clickable(rt.gameObject, () => { inStrip = false; slot = index; Rebuild(); }, null);
                if (member != null) Draggable(rt, member, i);
            }
        }

        /// <summary>
        /// The Members mat: a grey cloth with a pearl trim (PauseMat's pearl cloth) inside a mask that widens from the
        /// left as it unrolls, everyone who has joined lying on it, and the rolled-up end riding its right edge. Rolled
        /// up, only the scroll shows (bottom-left) with "Members" under it. The scroll and the cloth toggle it.
        /// </summary>
        void BuildStrip()
        {
            var roster = Roster;
            var size = CardView.Size * StripScale;
            var midLeft = new Vector2(0f, 0.5f);
            var topLeft = new Vector2(0f, 1f);
            float total = roster.Count * size.x + Mathf.Max(0, roster.Count - 1) * StripGap;
            membersMatW = total + MembersPad * 2f;

            membersMask = UIFactory.CreateRect("MembersMat", page).Place(new Vector2(0.5f, 0.5f), midLeft, new Vector2(MembersLeft, StripY), new Vector2(0f, MembersH));
            membersMask.gameObject.AddComponent<RectMask2D>();
            var shadow = UIFactory.CreateImage(membersMask, "Shadow", new Color(0f, 0f, 0f, 0.35f));
            shadow.rectTransform.Place(midLeft, midLeft, new Vector2(8f, -10f), new Vector2(membersMatW, MembersH));
            var cloth = new GameObject("Cloth", typeof(RectTransform)).AddComponent<RawImage>();
            cloth.rectTransform.SetParent(membersMask, false);
            cloth.texture = PauseMat.ClothTexture(Mathf.Max(24, Mathf.RoundToInt(PauseMat.ClothHeight * membersMatW / MembersH)), true);
            cloth.rectTransform.Place(midLeft, midLeft, Vector2.zero, new Vector2(membersMatW, MembersH));
            Clickable(cloth.gameObject, ToggleMembers, null);
            // "Members" sits above the mat (on the dark background, easy to read), showing while it's unrolled.
            var label = InkStyle.Label(page, "StripLabel", UIText.Get("party.members"), 26, InkStyle.Paper, TextAnchor.LowerLeft);
            label.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(MembersLeft + 6f, StripY + MembersH / 2f + 6f), new Vector2(400f, 36f));
            membersLabel = label.gameObject.AddComponent<CanvasGroup>();
            membersLabel.blocksRaycasts = false;

            for (int i = 0; i < roster.Count; i++)
            {
                var member = roster[i];
                int index = i;
                bool highlighted = inStrip && i == pick;
                var rt = UIFactory.CreateRect(member.DisplayName, membersMask).Place(midLeft, new Vector2(0.5f, 0.5f),
                    new Vector2(MembersPad + size.x / 2f + i * (size.x + StripGap), (highlighted ? 16f : 0f) - 4f), size);
                var art = CardArt.Ult(WorldSession.UltimateOf(member), member);
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
                // The name only shows while the card is held (or highlighted with the keyboard): an ink tag above it.
                var tag = InkStyle.Panel(rt, "NameTag", new Color(0.05f, 0.045f, 0.04f, 0.95f));
                tag.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(size.x + 50f, 34f));
                var name = UIFactory.CreateText(tag.rectTransform, "Name", member.DisplayName, 24, InkStyle.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
                name.rectTransform.Fill(2f);
                tag.gameObject.SetActive(highlighted);
                if (highlighted) InkStyle.Frame(rt, InkStyle.GoldBright, 5f, -5f);
                Clickable(rt.gameObject, () => Place(roster[index]), () => { inStrip = true; pick = index; });
                HoldForNameTag(rt, tag.rectTransform);
                Draggable(rt, member, -1);
            }

            // The rolled-up end: the whole mat when it's closed (bottom-left), riding the edge as it unrolls.
            var roll = new GameObject("MembersRoll", typeof(RectTransform)).AddComponent<RawImage>();
            roll.rectTransform.SetParent(page, false);
            roll.texture = PauseMat.RollTexture(true);
            membersRoll = roll.rectTransform;
            membersRoll.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(MembersLeft, StripY), new Vector2(64f, MembersH + 30f));
            Clickable(roll.gameObject, ToggleMembers, null);
            var caption = InkStyle.Label(membersRoll, "Caption", UIText.Get("party.members"), 24, InkStyle.Paper, TextAnchor.UpperLeft);
            caption.rectTransform.Place(new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(-8f, -8f), new Vector2(200f, 34f));
            membersCaption = caption.gameObject.AddComponent<CanvasGroup>();
            membersCaption.blocksRaycasts = false;
            ApplyMembers();
        }

        /// <summary>Rolls the Members mat out or back up.</summary>
        void ToggleMembers()
        {
            if (dragging) return;
            membersOpen = !membersOpen;
            if (!membersOpen && inStrip)
            {
                inStrip = false;
                Rebuild();
            }
        }

        /// <summary>Lays the mat out for how far it's unrolled (eased).</summary>
        void ApplyMembers()
        {
            if (membersMask == null) return;
            float k = membersReveal * membersReveal * (3f - 2f * membersReveal);
            membersMask.sizeDelta = new Vector2(membersMatW * k, MembersH);
            membersRoll.anchoredPosition = new Vector2(MembersLeft + membersMatW * k, StripY);
            membersRoll.sizeDelta = new Vector2(Mathf.Lerp(64f, 30f, k), MembersH + Mathf.Lerp(30f, 12f, k));
            membersCaption.alpha = 1f - k;
            if (membersLabel != null) membersLabel.alpha = k;
        }

        /// <summary>Pressing and holding <paramref name="card"/> shows its name tag until released (the release then
        /// isn't a click); a drag hides it.</summary>
        void HoldForNameTag(RectTransform card, RectTransform tag)
        {
            var trigger = card.GetComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ =>
            {
                holdTag = tag;
                holdStart = Time.unscaledTime;
                holdShown = false;
            });
            trigger.triggers.Add(down);
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ =>
            {
                if (holdShown) suppressClickFrame = Time.frameCount;
                if (holdTag != null && holdShown) holdTag.gameObject.SetActive(false);
                holdTag = null;
                holdShown = false;
            });
            trigger.triggers.Add(up);
        }

        void BuildButtons()
        {
            // No Remove button: Backspace (or dragging a member out) empties a slot.
            if (PreFight)
            {
                // Exactly the battle's Go button (size, font, chevrons, punch).
                var start = HudView.GoStyleButton(page, "Start", UIText.Get("party.start"), () => Close(true));
                start.GetComponent<RectTransform>().Place(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-StartRight, StartBottom), HudView.GoSize);
            }
            // The same X in both modes (top-right): before a fight it backs out of the fight, like Esc.
            var x = InkStyle.CloseButton(page, () => Close(false));
            x.GetComponent<RectTransform>().Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(InkStyle.CloseSize, InkStyle.CloseSize));
        }

        void Clickable(GameObject go, Action click, Action hover)
        {
            var trigger = go.AddComponent<EventTrigger>();
            var c = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            c.callback.AddListener(_ => { if (!dragging && Time.frameCount != suppressClickFrame) click(); });
            trigger.triggers.Add(c);
            if (hover == null) return;
            var h = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            h.callback.AddListener(_ => hover());
            trigger.triggers.Add(h);
        }

        // ------------------------------------------------------------------ Drag and drop

        /// <summary>Lets <paramref name="rt"/> (a slot card, or a strip card when <paramref name="from"/> is -1) be dragged.</summary>
        void Draggable(RectTransform rt, CharacterDefinition member, int from)
        {
            var trigger = rt.GetComponent<EventTrigger>();
            if (trigger == null) trigger = rt.gameObject.AddComponent<EventTrigger>();
            var begin = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
            begin.callback.AddListener(e => BeginDrag(rt, member, from, (PointerEventData)e));
            trigger.triggers.Add(begin);
            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(e => DragTo((PointerEventData)e));
            trigger.triggers.Add(drag);
            var end = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
            end.callback.AddListener(e => EndDrag((PointerEventData)e));
            trigger.triggers.Add(end);
        }

        void BeginDrag(RectTransform source, CharacterDefinition member, int from, PointerEventData e)
        {
            if (holdTag != null && holdShown) holdTag.gameObject.SetActive(false);
            holdTag = null;
            holdShown = false;
            dragging = true;
            dragMember = member;
            dragFrom = from;
            dropTarget = -1;
            dragSource = source.GetComponent<CanvasGroup>();
            if (dragSource == null) dragSource = source.gameObject.AddComponent<CanvasGroup>();
            dragSource.alpha = 0.35f;

            // The card lifted off the table: its art, a little smaller and tilted, following the pointer.
            var art = CardArt.Ult(WorldSession.UltimateOf(member), member);
            var img = UIFactory.CreateImage(page, "Ghost", art != null ? new Color(1f, 1f, 1f, 0.92f) : member.color);
            img.sprite = art;
            img.raycastTarget = false;
            ghost = img.rectTransform;
            var center = new Vector2(0.5f, 0.5f);
            ghost.Place(center, center, Vector2.zero, CardView.Size * SlotScale * 0.85f);
            ghost.localRotation = Quaternion.Euler(0f, 0f, 6f);
            DragTo(e);
        }

        void DragTo(PointerEventData e)
        {
            if (!dragging || ghost == null) return;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(page, e.position, null, out local))
                ghost.anchoredPosition = local;
            int target = SlotAt(e.position);
            if (target == dropTarget) return;
            dropTarget = target;
            if (dropFrame != null) Destroy(dropFrame.gameObject);
            dropFrame = null;
            if (target < 0) return;
            dropFrame = UIFactory.CreateRect("DropFrame", slotRects[target]).Fill();
            InkStyle.Frame(dropFrame, InkStyle.GoldBright, 6f, -10f);
        }

        void EndDrag(PointerEventData e)
        {
            if (!dragging) return;
            int target = SlotAt(e.position);
            if (ghost != null) Destroy(ghost.gameObject);
            if (dropFrame != null) Destroy(dropFrame.gameObject);
            ghost = dropFrame = null;
            if (dragSource != null) dragSource.alpha = 1f;
            dragging = false;

            if (target >= 0 && target != dragFrom)
            {
                WorldSession.Assign(target, dragMember, FullRoster);   // a member already in a slot swaps with the target
                slot = target;
                inStrip = false;
            }
            else if (target < 0 && dragFrom >= 0) WorldSession.ClearSlot(dragFrom, FullRoster);   // dropped off the slots
            dragMember = null;
            dragFrom = -1;
            Rebuild();
        }

        /// <summary>The formation slot under a screen point, or -1.</summary>
        int SlotAt(Vector2 screen)
        {
            for (int i = 0; i < slotRects.Count; i++)
                if (slotRects[i] != null && RectTransformUtility.RectangleContainsScreenPoint(slotRects[i], screen, null)) return i;
            return -1;
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
            // The Members mat rolls toward open or closed.
            float target = membersOpen ? 1f : 0f;
            if (!Mathf.Approximately(membersReveal, target))
            {
                membersReveal = Mathf.MoveTowards(membersReveal, target, Time.unscaledDeltaTime / MembersRoll);
                ApplyMembers();
            }
            // A held member card shows its name.
            if (holdTag != null && !holdShown && !dragging && Time.unscaledTime - holdStart >= HoldForName)
            {
                holdShown = true;
                holdTag.gameObject.SetActive(true);
            }
            if (Time.frameCount == openedFrame || dragging) return;
            if (CardDetailPopup.IsOpen) return;   // an open card takes Esc / clicks first
            if (enemyCards != null && !overEnemyRow && !overEnemyCards && Time.unscaledTime >= enemyHideAt) HideEnemyCards();
            int count = inStrip ? Roster.Count : WorldSession.FormationSlots;
            if (UIKeys.Pressed(KeyCode.LeftArrow) || UIKeys.Pressed(KeyCode.A)) Move(-1, count);
            if (UIKeys.Pressed(KeyCode.RightArrow) || UIKeys.Pressed(KeyCode.D)) Move(1, count);
            if ((UIKeys.Pressed(KeyCode.DownArrow) || UIKeys.Pressed(KeyCode.S)) && !inStrip && Roster.Count > 0) { membersOpen = true; inStrip = true; pick = Mathf.Clamp(pick, 0, Roster.Count - 1); Rebuild(); }
            if ((UIKeys.Pressed(KeyCode.UpArrow) || UIKeys.Pressed(KeyCode.W)) && inStrip) { inStrip = false; Rebuild(); }
            if (UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter) || UIKeys.Pressed(KeyCode.F))
            {
                if (inStrip && pick < Roster.Count) Place(Roster[pick]);
                else { membersOpen = true; inStrip = true; Rebuild(); }
            }
            if (UIKeys.Pressed(KeyCode.Backspace) || UIKeys.Pressed(KeyCode.Delete)) RemoveSelected();
            if (UIKeys.Back) Close(false);   // only the Start button starts a fight
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
