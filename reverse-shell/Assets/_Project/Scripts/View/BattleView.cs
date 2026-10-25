using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Builds the battle UI and plays BattleEvents back one at a time. Input is only accepted while
    /// the controller is in Planning and every queued event has finished animating.
    /// </summary>
    public class BattleView : MonoBehaviour
    {
        static readonly Color Background = new Color(0.07f, 0.065f, 0.09f);
        static readonly Color Gold = new Color(1f, 0.8f, 0.3f);
        const float UnitSpacing = 280f; // room for the 4x battle sprites
        /// <summary>Units stand this far above the backdrop's street line (the Stage is centred on it).</summary>
        const float StageY = 12f;
        // A 4:3 view shows only ~1740 of the 2400-wide layout (Shrink matching crops the width), so the party sits
        // close enough in that the leftmost member's bars and type diamond stay on screen.
        const float PartyCenterX = -420f;
        const float EnemyCenterX = 470f;

        public BattleController Controller { get; private set; }
        public RectTransform Stage { get; private set; }
        public RectTransform FxLayer { get; private set; }

        BattleBootstrap bootstrap;
        HandView hand;
        HudView hud;
        UnitView focused;
        bool focusFromKeyboard;
        PauseMenu pause;
        UnitInspectView inspect;
        RectTransform root;
        RectTransform backdropLayer;               // zooms with the stage when inspecting
        CanvasGroup handGroup, hudGroup;           // fade out when inspecting
        readonly Dictionary<BattleUnit, UnitView> unitViews = new Dictionary<BattleUnit, UnitView>();
        bool playing;
        /// <summary>From Go until the next turn starts: the turn is playing out (battle speed applies, no lock-on).</summary>
        bool turnPlaying;
        BattleUnit selectedTarget;

        public bool IsPaused { get { return pause != null && pause.IsOpen; } }

        public bool InputEnabled
        {
            get
            {
                return !playing && Controller.Events.Count == 0 && Controller.State.Phase == BattlePhase.Planning
                       && !IsPaused && inspect == null && !CardDetailPopup.IsOpen;
            }
        }

        public static BattleView Create(BattleBootstrap bootstrap, BattleController controller)
        {
            var go = new GameObject("BattleCanvas", typeof(RectTransform));
            go.transform.SetParent(bootstrap.transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UIFactory.ReferenceResolution;
            // Shrink: the whole 1920x1080 layout always fits on screen, whatever the window shape.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
            go.AddComponent<GraphicRaycaster>();

            var view = go.AddComponent<BattleView>();
            view.Build(bootstrap, controller);
            return view;
        }

        void Build(BattleBootstrap boot, BattleController controller)
        {
            bootstrap = boot;
            Controller = controller;
            root = (RectTransform)transform;
            var center = new Vector2(0.5f, 0.5f);

            var backdrop = boot.ActiveSetup != null ? boot.ActiveSetup.backdrop : null;
            if (backdrop != null) backdropLayer = (RectTransform)BattleBackdrop.Create(root, backdrop).transform;
            else backdropLayer = UIFactory.CreateImage(root, "Background", Background).rectTransform.Fill();

            // The Stage is the screen's size but centred on the backdrop's street line, so units always stand on the street
            // wherever it's painted (BattleBackdropDefinition.streetLine, a fraction from the top). Its pivot stays at the
            // screen centre, so the inspect zoom scales it together with the backdrop.
            float street = backdrop != null ? backdrop.streetLine : 0.42f;
            Stage = UIFactory.CreateRect("Stage", root).Fill();
            Stage.anchorMin = new Vector2(0f, 0.5f - street);
            Stage.anchorMax = new Vector2(1f, 1.5f - street);
            Stage.offsetMin = Stage.offsetMax = Vector2.zero;
            Stage.pivot = new Vector2(0.5f, street);
            var state = controller.State;
            int cap = state.Config.moxieCap;
            for (int i = 0; i < state.Party.Count; i++)
                AddUnit(state.Party[i], new Vector2(PartyCenterX + (i - (state.Party.Count - 1) / 2f) * UnitSpacing, StageY), cap);
            for (int i = 0; i < state.Enemies.Count; i++)
                AddUnit(state.Enemies[i], new Vector2(EnemyCenterX + (i - (state.Enemies.Count - 1) / 2f) * UnitSpacing, StageY), cap);

            var handRect = UIFactory.CreateRect("Hand", root).Place(new Vector2(0.5f, 0f), center, new Vector2(-60f, 178f), Vector2.zero);
            hand = handRect.gameObject.AddComponent<HandView>();
            handGroup = handRect.gameObject.AddComponent<CanvasGroup>();

            var hudRect = UIFactory.CreateRect("HUD", root);
            hud = hudRect.gameObject.AddComponent<HudView>();
            hudGroup = hudRect.gameObject.AddComponent<CanvasGroup>();
            hud.Build(this);

            var previewLayer = UIFactory.CreateRect("CardPreview", root).Fill();
            hand.Init(this, previewLayer);

            FxLayer = UIFactory.CreateRect("FX", root).Fill();
            hud.BringOverlaysToFront();
            pause = PauseMenu.Create(root, this);
        }

        void AddUnit(BattleUnit unit, Vector2 position, int moxieCap)
        {
            var uv = UnitView.Create(Stage, unit, this, moxieCap);
            uv.Rect.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(UnitView.Width, UnitView.Height));
            unitViews[unit] = uv;
        }

        UnitView Get(BattleUnit unit)
        {
            UnitView uv;
            return unit != null && unitViews.TryGetValue(unit, out uv) ? uv : null;
        }

        void Update()
        {
            // Scripts recompiled during Play mode wipe in-memory battle state; rebuild instead of erroring every frame.
            if (Controller == null)
            {
                var boot = GetComponentInParent<BattleBootstrap>();
                Destroy(gameObject);
                if (boot != null) boot.Begin();
                return;
            }

            if (UIKeys.Back && !GuideBook.Blocking)   // the guide (from the pause menu) closes itself
            {
                if (CardDetailPopup.IsOpen) { }   // the card popup closes itself on Esc
                else if (inspect != null) inspect.Close();
                else TogglePause();
            }
            if (!IsPaused && UIKeys.Pressed(KeyCode.Tab)) pause.CycleSpeed();
            HandleTargetKeys();
            if (!playing && Controller.Events.Count > 0) StartCoroutine(PlayEvents());
            hud.SetInteractable(InputEnabled);
            // The chosen battle speed only while the turn plays out; planning (and the results) run at 1x.
            if (!IsPaused) Time.timeScale = turnPlaying ? GameSettings.BattleSpeed : 1f;
        }

        void SetTurnPlaying(bool on)
        {
            turnPlaying = on;
            RefreshLockOn();
        }

        /// <summary>The red lock-on shows on the targeted enemy while planning only.</summary>
        void RefreshLockOn()
        {
            foreach (var pair in unitViews)
                if (pair.Key.Team == Team.Enemy) pair.Value.SetSelected(!turnPlaying && pair.Key == selectedTarget);
        }

        // ------------------------------------------------------------------ Pause & inspect

        public void TogglePause()
        {
            if (IsPaused) pause.Close();
            else pause.Open();
        }

        public void RestartFight()
        {
            pause.Close();
            bootstrap.Begin();
        }

        public void LeaveFight()
        {
            pause.Close();
            bootstrap.Leave();
        }

        /// <summary>Long-press on a hand card: open its details (the full text; scroll for upgrades).</summary>
        public void OpenCardDetail(CardView card)
        {
            if (IsPaused || inspect != null || CardDetailPopup.IsOpen || card == null) return;
            CardDetailPopup.Open(root, card.Card.Owner, card.Card.Definition, card.Card.Rank, card.Rect);
        }

        /// <summary>Long-press on a unit: zoom in on it and show its sheet (only while the board is idle).</summary>
        public void OpenInspect(UnitView unit)
        {
            if (IsPaused || inspect != null || playing || Controller.Events.Count > 0) return;
            inspect =UnitInspectView.Open(root, unit, new[] { backdropLayer, Stage }, new[] { handGroup, hudGroup },
                () => inspect = null);
            pause.transform.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ Input

        public void OnCardClicked(CardInstance card)
        {
            if (InputEnabled) Controller.QueueCard(card);
        }

        public bool RequestMove(CardInstance card, int toIndex)
        {
            return InputEnabled && Controller.MoveCard(card, toIndex);
        }

        public void OnUnitClicked(BattleUnit unit)
        {
            if (InputEnabled) Controller.SelectEnemy(unit);
        }

        // ------------------------------------------------------------------ Card target preview (orange rings)

        /// <summary>Hovering a card: ring everyone it would hit or buff (the targeted enemy, all foes, the team...).</summary>
        public void ShowTargetPreview(CardInstance card)
        {
            ClearTargetPreview();
            if (card == null || Controller == null) return;
            foreach (var unit in Controller.PreviewTargets(card))
            {
                var uv = Get(unit);
                if (uv != null) uv.SetTargetPreview(true);
            }
        }

        public void ClearTargetPreview()
        {
            foreach (var uv in unitViews.Values)
                if (uv != null) uv.SetTargetPreview(false);
        }

        // ------------------------------------------------------------------ Focus (white ring) and keyboard targeting

        /// <summary>Mouse over / off a unit: the white ring follows the mouse.</summary>
        public void OnUnitHover(UnitView unit, bool entered)
        {
            if (entered) SetFocus(unit, false);
            else if (focused == unit && !focusFromKeyboard) SetFocus(null, false);
        }

        void SetFocus(UnitView unit, bool fromKeyboard)
        {
            if (focused != null) focused.SetFocused(false);
            focused = unit != null && unit.Unit.IsAlive ? unit : null;
            focusFromKeyboard = fromKeyboard && focused != null;
            if (focused != null) focused.SetFocused(true);
        }

        /// <summary>
        /// Left/Right step the white ring through every living unit in screen order; Up/Down jump between the
        /// party and the enemies. Enter locks the ringed enemy on as the target. Space inspects the ringed
        /// unit (the same as holding it), or closes the inspect screen.
        /// </summary>
        void HandleTargetKeys()
        {
            if (IsPaused || CardDetailPopup.IsOpen) return;   // an open card takes the keys
            if (UIKeys.Pressed(KeyCode.Space))
            {
                if (inspect != null) inspect.Close();
                else if (focused != null) focused.Inspect();
                return;
            }
            if (inspect != null) return;

            if (UIKeys.Pressed(KeyCode.Return) || UIKeys.Pressed(KeyCode.KeypadEnter))
            {
                // Enter on a ringed enemy targets it; otherwise it's Go (with the button's press flourish).
                if (focused != null && focused.Unit.Team == Team.Enemy) { if (InputEnabled) Controller.SelectEnemy(focused.Unit); }
                else if (InputEnabled) hud.PressGo();
                return;
            }

            int step = 0;
            bool switchSide = false;
            if (UIKeys.Pressed(KeyCode.RightArrow)) step = 1;
            else if (UIKeys.Pressed(KeyCode.LeftArrow)) step = -1;
            else if (UIKeys.Pressed(KeyCode.UpArrow) || UIKeys.Pressed(KeyCode.DownArrow)) switchSide = true;
            else return;

            var order = new List<UnitView>();
            foreach (var uv in unitViews.Values)
                if (uv.Unit.IsAlive) order.Add(uv);
            if (order.Count == 0) return;
            order.Sort((a, b) => a.Rect.anchoredPosition.x.CompareTo(b.Rect.anchoredPosition.x));

            var current = focused != null && focused.Unit.IsAlive ? focused : Get(Controller.State.SelectedEnemy);
            UnitView next;
            if (current == null) next = order[0];
            else if (!switchSide) next = order[(order.IndexOf(current) + step + order.Count) % order.Count];
            else
            {
                var mine = order.FindAll(u => u.Unit.Team == current.Unit.Team);
                var other = order.FindAll(u => u.Unit.Team != current.Unit.Team);
                next = other.Count == 0 ? current : other[Mathf.Min(mine.IndexOf(current), other.Count - 1)];
            }

            SetFocus(next, true);
        }

        public void OnGo()
        {
            if (!InputEnabled) return;
            Controller.EndPlanning();
            SetTurnPlaying(true);
        }

        /// <summary>Clicking the queue: every queued card goes back to the hand. It unwinds this turn's whole undo stack, so
        /// moves and merges made in the hand are undone too and all AP comes back.</summary>
        public void OnReturnAll()
        {
            if (!InputEnabled) return;
            while (Controller.CanUndo && Controller.Undo()) { }
        }

        public string ResultButtonLabel { get { return bootstrap.HasReturn ? "Continue" : "Fight Again"; } }

        public void OnRestart()
        {
            bootstrap.OnResultButton();
        }

        // ------------------------------------------------------------------ Event playback

        IEnumerator PlayEvents()
        {
            playing = true;
            hand.HideAnyPreview();
            while (Controller.Events.Count > 0)
            {
                var routine = Play(Controller.Events.Dequeue());
                if (routine != null) yield return StartCoroutine(routine);
            }
            playing = false;
        }

        IEnumerator Play(BattleEvent e)
        {
            // A run of end-of-turn ticks starts with a pause; anything else ends the run.
            var damageEvent = e as DamageEvent;
            if (!(damageEvent != null && damageEvent.IsTick) && !(e is StatusesChangedEvent) && !(e is HealEvent)) tickRun = false;
            switch (e)
            {
                case RoundStartedEvent round:
                    if (turnPlaying) SetTurnPlaying(false);
                    hud.SetRound(round.Round);
                    hud.ShowBanner("Turn " + round.Round, Gold, 0.4f);
                    hud.Log("-- Turn " + round.Round + " --");
                    return Tween.Wait(0.35f);
                case CardDrawnEvent drawn:
                    return PlayDraw(drawn);
                case CardsMergedEvent merged:
                    return PlayMerge(merged);
                case RitualSpawnedEvent ritual:
                    return PlayRitualSpawn(ritual);
                case CardMovedEvent moved:
                    hand.Layout(moved.HandAfter, 0.18f);
                    return Tween.Wait(0.18f);
                case CardQueuedEvent queued:
                    return PlayQueue(queued);
                case HandRestoredEvent restored:
                    hand.Layout(restored.HandAfter, 0.2f);
                    hud.SetQueue(restored.QueueAfter);
                    return Tween.Wait(0.2f);
                case ActionPointsChangedEvent ap:
                    hud.SetActionPoints(ap.Current, ap.Max, ap.Moves);
                    return null;
                case UnitEnteredEvent entered:
                    return PlayUnitEntered(entered);
                case MoxieChangedEvent moxie:
                    return PlayMoxie(moxie);
                case IntentChosenEvent intent:
                    var iv = Get(intent.Unit);
                    if (iv != null) iv.SetIntent(intent.Intent);
                    return null;
                case TargetSelectedEvent selected:
                    selectedTarget = selected.Target;
                    RefreshLockOn();
                    return null;
                case CardCastEvent cast:
                    return PlayCast(cast);
                case DamageEvent damage:
                    return PlayDamage(damage);
                case HealEvent heal:
                    return PlayHeal(heal);
                case StatusesChangedEvent statuses:
                    return PlayStatuses(statuses);
                case UnitDiedEvent died:
                    hud.Log(died.Unit.Name + " is defeated!");
                    var dv = Get(died.Unit);
                    if (dv != null) FxLibrary.PlayAt(FxLayer, BattleFx.Death, dv.Center);
                    return dv != null ? dv.Die() : null;
                case LogEvent log:
                    hud.Log(log.Message);
                    return null;
                case BattleEndedEvent ended:
                    SetTurnPlaying(false);
                    hud.ShowResult(ended.Victory, bootstrap.Result);
                    return null;
            }
            return null;
        }

        IEnumerator PlayDraw(CardDrawnEvent e)
        {
            var cv = hand.CreateView(e.Card, hand.IncomingPosition(e.HandAfter.Count));
            cv.Group.alpha = 0f;
            StartCoroutine(Tween.Fade(cv.Group, 1f, 0.15f));
            hand.Layout(e.HandAfter, 0.25f);
            yield return Tween.Wait(0.07f);
        }

        IEnumerator PlayMerge(CardsMergedEvent e)
        {
            var left = hand.Detach(e.Merge.Left);
            var right = hand.Detach(e.Merge.Right);
            Vector2 spot = left != null ? left.Rect.anchoredPosition : hand.SlotPosition(e.Merge.Index, e.HandAfter.Count);

            if (right != null)
            {
                right.StopAnimations();
                right.Rect.SetAsLastSibling();
                yield return Tween.MoveAnchored(right.Rect, spot, 0.14f, Tween.InOutQuad);
            }
            if (left != null) Destroy(left.gameObject);
            if (right != null) Destroy(right.gameObject);

            var merged = hand.CreateView(e.Merge.Result, spot);
            merged.Rect.localScale = Vector3.one * 1.3f;
            merged.ScaleTo(1f, 0.25f);
            hand.Layout(e.HandAfter, 0.2f);
            hud.Log(e.Merge.Result.Owner.Name + ": " + e.Merge.Result.Name + " -> " + UIFactory.Roman(e.Merge.Result.Rank));
            yield return Tween.Wait(0.2f);
        }

        IEnumerator PlayRitualSpawn(RitualSpawnedEvent e)
        {
            var cv = hand.CreateView(e.Card, hand.SlotPosition(e.HandAfter.Count - 1, e.HandAfter.Count) + new Vector2(0f, 120f));
            cv.Rect.localScale = Vector3.zero;
            cv.ScaleTo(1f, 0.35f);
            hand.Layout(e.HandAfter, 0.35f);
            hud.ShowBanner(e.Card.Owner.Name + "'s Ritual is ready!", CardView.RitualPastel(e.Card.Owner.Color), 0.6f);
            hud.Log(e.Card.Owner.Name + "'s Ritual is ready!");
            yield return Tween.Wait(0.4f);
        }

        IEnumerator PlayQueue(CardQueuedEvent e)
        {
            var cv = hand.Detach(e.Card);
            hand.Layout(e.HandAfter, 0.2f);
            if (cv != null)
            {
                var q = hud.QueueRow;
                var target = q.TransformPoint(q.rect.center);
                yield return cv.FlyAndVanish(target, 0.2f);
            }
            hud.SetQueue(e.QueueAfter);
        }

        IEnumerator PlayMoxie(MoxieChangedEvent e)
        {
            var uv = Get(e.Unit);
            if (uv == null) yield break;
            uv.SetMoxie(e.Value, Controller.State.Config.moxieCap);
            if (e.Delta > 0) StartCoroutine(uv.PunchMoxie());
        }

        IEnumerator PlayCast(CardCastEvent e)
        {
            // The card leaves its queue slot (enemies: from above their head) before the slot row updates.
            var caster = Get(e.Caster);
            Vector3 from = e.Caster.Team == Team.Player ? hud.FirstSlotPosition : (caster != null ? AboveHead(caster) : Vector3.zero);
            hud.SetQueue(e.QueueAfter);
            var targets = new List<UnitView>();
            foreach (var t in e.Targets)
            {
                var tv = Get(t);
                if (tv != null) targets.Add(tv);
            }

            var color = e.Card.IsRitual ? CardView.RitualPastel(e.Caster.Color) : e.Caster.Color;
            string title = e.Card.IsRitual ? "« " + e.Card.Name + " »" : e.Card.Name + " " + UIFactory.Roman(e.Card.Rank);
            hud.Log(e.Caster.Name + " uses " + title);
            if (caster != null && e.Caster.Team == Team.Enemy) caster.SetIntent(null);
            ritualHits = e.Card.IsRitual;

            if (e.Card.IsRitual)
            {
                // A Soul Card: its name gets its own title animation while the Ult charges.
                if (caster != null) FxLibrary.PlayAt(FxLayer, BattleFx.UltCharge, caster.Center);
                var titleLayer = UIFactory.CreateRect("UltTitle", root).Fill();
                pause.transform.SetAsLastSibling();
                yield return StartCoroutine(UltTitle.Play(titleLayer, e.Card.Name, color));
                Destroy(titleLayer.gameObject);
            }
            else hud.ShowBanner(title, color, 0.5f);
            if (caster != null) yield return StartCoroutine(ChannelCard(e.Card, caster, from));

            var def = e.Card.Definition;
            var sequence = def.castSequence != null ? def.castSequence : CastSequence.GetDefault(def.kind, e.Card.IsRitual);
            // Body animation: sequences can choose their own with PlayAnimationStep; otherwise attacks slash, the rest cast.
            if (caster != null && !sequence.steps.Exists(st => st is PlayAnimationStep))
            {
                bool attack = !e.Card.IsRitual && (def.kind == CardKind.Attack || def.kind == CardKind.Debuff);
                caster.PlayAnimation(attack ? "slash" : "spellcast");
            }
            yield return StartCoroutine(sequence.Play(new CastContext(this, caster, targets, color)));

            // Damage, heals and statuses get their effects (and their own beats) from their events.
            yield return Tween.Wait(CastPause);
        }

        // Pacing: how long each part of a cast takes (seconds).
        const float ChannelFly = 0.4f, ChannelHold = 0.55f, ChannelRelease = 0.22f, CastPause = 0.35f;
        // Statuses land a beat after the hit that applied them; end-of-turn ticks start after a pause, one at a time.
        const float StatusDelay = 0.45f, StatusHold = 0.55f, TickLead = 0.9f, TickBanner = 0.7f, TickHold = 0.8f;
        static readonly Color TickColor = new Color(0.75f, 0.5f, 1f);
        bool tickRun;

        /// <summary>
        /// A unit's statuses changed. Newly gained (or refreshed / stacked) ones land on their own beat: a pause so the
        /// hit reads first, then the buff / debuff effect and the status name floating up in its colour. Statuses
        /// only ticking down or wearing off just update the icons.
        /// </summary>
        IEnumerator PlayStatuses(StatusesChangedEvent e)
        {
            var uv = Get(e.Unit);
            if (uv == null) yield break;
            var gained = new List<StatusSnapshot>();
            foreach (var now in e.Statuses)
            {
                bool had = false;
                if (uv.Statuses != null)
                    foreach (var before in uv.Statuses)
                        if (before.Definition == now.Definition && before.Stacks >= now.Stacks && before.Remaining >= now.Remaining) had = true;
                if (!had) gained.Add(now);
            }
            if (gained.Count == 0 || !e.Unit.IsAlive)
            {
                uv.SetStatuses(e.Statuses);
                yield break;
            }

            yield return Tween.Wait(StatusDelay);
            uv.SetStatuses(e.Statuses);
            bool anyDebuff = gained.Exists(g => g.Definition.isDebuff), anyBuff = gained.Exists(g => !g.Definition.isDebuff);
            if (anyDebuff) FxLibrary.PlayAt(FxLayer, BattleFx.Debuff, uv.Center);
            if (anyBuff) FxLibrary.PlayAt(FxLayer, BattleFx.Buff, uv.Center);
            foreach (var g in gained) SpawnHeadText(uv, g.Definition.DisplayName, g.Definition.color, 64);
            yield return Tween.Wait(StatusHold);
        }

        Vector3 AboveHead(UnitView unit)
        {
            return unit.Center + new Vector3(0f, 250f * transform.lossyScale.y, 0f);
        }

        /// <summary>
        /// The card being used: it flies from its queue slot to above the caster, hangs there glowing in the owner's
        /// colour while the caster channels it, then flares and fades as the cast animation starts.
        /// </summary>
        IEnumerator ChannelCard(CardInstance card, UnitView caster, Vector3 from)
        {
            var rt = UIFactory.CreateRect("Channel_" + card.Name, FxLayer);
            rt.sizeDelta = CardView.Size * ChannelScale;
            CardView.BuildFace(rt, card, ChannelScale, true);
            if (!card.IsRitual) UltShine.Add(rt, null, CardArt.HandPixel * ChannelScale);   // Ult faces already shine
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            rt.position = from;
            rt.localScale = Vector3.one * 0.4f;

            Vector3 above = AboveHead(caster);
            StartCoroutine(Tween.Scale(rt, Vector3.one, ChannelFly, Tween.OutBack));
            yield return Tween.MoveWorld(rt, above, ChannelFly, Tween.InOutQuad);

            // Channel: the caster flashes in its colour and the card bobs gently.
            StartCoroutine(caster.Flash(card.Owner.Color, ChannelHold));
            float bob = 10f * transform.lossyScale.y;
            yield return Tween.Run(ChannelHold, p => { if (rt != null) rt.position = above + new Vector3(0f, Mathf.Sin(p * Mathf.PI * 2f) * bob, 0f); });

            // Release: a flare, then gone.
            StartCoroutine(Tween.Fade(group, 0f, ChannelRelease));
            yield return Tween.Scale(rt, Vector3.one * 1.3f, ChannelRelease, Tween.OutCubic);
            if (rt != null) Destroy(rt.gameObject);
        }

        /// <summary>The channelled card: 3x card pixels.</summary>
        const float ChannelScale = 0.6f;

        /// <summary>The backup steps in: the fallen member's view leaves and the newcomer fades in at the same spot.</summary>
        IEnumerator PlayUnitEntered(UnitEnteredEvent e)
        {
            var gone = Get(e.Replaces);
            Vector2 spot = gone != null ? gone.Rect.anchoredPosition : new Vector2(PartyCenterX, StageY);
            yield return Tween.Wait(0.4f);
            if (gone != null) gone.gameObject.SetActive(false);
            AddUnit(e.Unit, spot, Controller.State.Config.moxieCap);
            var uv = Get(e.Unit);
            var group = uv.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            hud.ShowBanner(e.Unit.Name + " steps in!", e.Unit.Color, 0.8f);
            FxLibrary.PlayAt(FxLayer, BattleFx.Buff, uv.Center);
            yield return Tween.Fade(group, 1f, 0.35f);
            yield return Tween.Wait(0.3f);
        }

        // True while the damage events of a Ritual (Ult) cast are playing: they add the big Ult impact.
        bool ritualHits;

        IEnumerator PlayDamage(DamageEvent e)
        {
            var tv = Get(e.Target);
            if (tv == null) yield break;
            tv.SetHP(e.HPAfter, e.MaxHP);

            if (e.IsTick && !tickRun)
            {
                // The attacks are done: a clear break, a banner, then the end-of-turn ticks one by one.
                tickRun = true;
                yield return Tween.Wait(TickLead);
                hud.ShowBanner("End of Turn", TickColor, TickBanner);
                yield return Tween.Wait(TickBanner);
            }

            Color color = Color.white;
            int size = 72;
            string text = e.Amount.ToString();
            if (e.IsTick) color = TickColor;   // just the purple number, no status name
            else if (e.Relation == AfflatusRelation.Advantage) color = new Color(1f, 0.85f, 0.35f);
            else if (e.Relation == AfflatusRelation.Disadvantage) color = new Color(0.7f, 0.7f, 0.75f);
            if (e.Crit)
            {
                size = 92;
                color = Color.Lerp(color, new Color(1f, 0.45f, 0.2f), 0.6f);
                text = UIFactory.Size(34) + "CRIT!</size>\n" + text;
            }
            SpawnDamageNumber(tv, text, color, size);
            if (e.IsTick) FxLibrary.PlayAt(FxLayer, BattleFx.PoisonTick, tv.Center);
            else
            {
                if (e.Source != null) FxLibrary.PlayAt(FxLayer, BattleFx.Hit(e.Source.Afflatus), tv.Center);
                if (ritualHits) FxLibrary.PlayAt(FxLayer, BattleFx.UltHit, tv.Center, BattleFx.UltHitScale);
                if (e.Crit) FxLibrary.PlayAt(FxLayer, BattleFx.Crit, tv.Center);
            }
            StartCoroutine(tv.Shake(e.Crit ? 16f : 10f, 0.22f));
            StartCoroutine(tv.Flash(e.IsTick ? new Color(0.7f, 0.4f, 1f) : new Color(1f, 0.35f, 0.35f), 0.2f));

            string note = e.Crit ? " (crit)" : "";
            if (e.Relation == AfflatusRelation.Advantage) note += " (afflatus+)";
            else if (e.Relation == AfflatusRelation.Disadvantage) note += " (afflatus-)";
            hud.Log(e.Target.Name + " takes " + e.Amount + note);
            yield return Tween.Wait(e.IsTick ? TickHold : 0.3f);
        }

        IEnumerator PlayHeal(HealEvent e)
        {
            var tv = Get(e.Target);
            if (tv == null) yield break;
            tv.SetHP(e.HPAfter, e.MaxHP);
            SpawnDamageNumber(tv, "+" + e.Amount, new Color(0.45f, 1f, 0.55f), 64);
            FxLibrary.PlayAt(FxLayer, BattleFx.Heal, tv.Center);
            hud.Log(e.Target.Name + " heals " + e.Amount);
            yield return Tween.Wait(0.25f);
        }

        // Numbers currently showing per unit, so multi-hits stack upward instead of overlapping.
        readonly Dictionary<UnitView, int> activeNumbers = new Dictionary<UnitView, int>();

        /// <summary>Big number that pops in just above the unit's HP bar, holds, then drifts up and fades.</summary>
        public void SpawnDamageNumber(UnitView unit, string text, Color color, int size)
        {
            SpawnFloatingText(unit, unit.DamageAnchor, activeNumbers, text, color, size, 18f);
        }

        // Status names showing per unit, stacked the same way.
        readonly Dictionary<UnitView, int> activeHeadTexts = new Dictionary<UnitView, int>();

        /// <summary>A status name popping in above the unit's head (above an enemy's intent card), then floating away.</summary>
        public void SpawnHeadText(UnitView unit, string text, Color color, int size)
        {
            SpawnFloatingText(unit, unit.HeadAnchor, activeHeadTexts, text, color, size, 0f);
        }

        void SpawnFloatingText(UnitView unit, Vector3 anchor, Dictionary<UnitView, int> active, string text, Color color, int size, float jitter)
        {
            int count;
            active.TryGetValue(unit, out count);
            active[unit] = count + 1;

            var t = UIFactory.CreateText(FxLayer, "DamageNumber", text, size, color, TextAnchor.LowerCenter, FontStyle.Bold);
            t.supportRichText = true;
            var rt = t.rectTransform;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(420f, 160f);
            float scale = transform.lossyScale.y;
            rt.position = anchor + new Vector3(Random.Range(-jitter, jitter) * scale, count * 44f * scale, 0f);
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
            t.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -5f);
            StartCoroutine(FloatAway(t, unit, active));
        }

        IEnumerator FloatAway(Text t, UnitView unit, Dictionary<UnitView, int> activeNumbers)
        {
            var rt = t.rectTransform;
            Vector3 from = rt.position;
            Vector3 rise = new Vector3(0f, 50f * transform.lossyScale.y, 0f);
            Color c = t.color;
            yield return Tween.Run(1.1f, p =>
            {
                if (t == null) return;
                float pop = Mathf.Clamp01(p / 0.12f);
                rt.localScale = Vector3.one * Mathf.Lerp(1.7f, 1f, Tween.OutBack(pop));
                float drift = Mathf.Clamp01((p - 0.55f) / 0.45f);
                rt.position = from + rise * drift;
                t.color = new Color(c.r, c.g, c.b, 1f - Mathf.Clamp01((p - 0.7f) / 0.3f));
            }, Tween.Linear);
            if (t != null) Destroy(t.gameObject);
            int active;
            if (activeNumbers.TryGetValue(unit, out active)) activeNumbers[unit] = Mathf.Max(0, active - 1);
        }
    }
}
