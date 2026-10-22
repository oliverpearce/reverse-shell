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
        const float UnitSpacing = 250f;
        const float StageY = 120f;
        const float PartyCenterX = -470f;
        const float EnemyCenterX = 430f;

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
        readonly Dictionary<BattleUnit, UnitView> unitViews = new Dictionary<BattleUnit, UnitView>();
        bool playing;

        public bool IsPaused { get { return pause != null && pause.IsOpen; } }

        public bool InputEnabled
        {
            get
            {
                return !playing && Controller.Events.Count == 0 && Controller.State.Phase == BattlePhase.Planning
                       && !IsPaused && inspect == null;
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
            if (backdrop != null) BattleBackdrop.Create(root, backdrop);
            else UIFactory.CreateImage(root, "Background", Background).rectTransform.Fill();

            Stage = UIFactory.CreateRect("Stage", root).Fill();
            var state = controller.State;
            int cap = state.Config.moxieCap;
            for (int i = 0; i < state.Party.Count; i++)
                AddUnit(state.Party[i], new Vector2(PartyCenterX + (i - (state.Party.Count - 1) / 2f) * UnitSpacing, StageY), cap);
            for (int i = 0; i < state.Enemies.Count; i++)
                AddUnit(state.Enemies[i], new Vector2(EnemyCenterX + (i - (state.Enemies.Count - 1) / 2f) * UnitSpacing, StageY), cap);

            var handRect = UIFactory.CreateRect("Hand", root).Place(new Vector2(0.5f, 0f), center, new Vector2(-60f, 150f), Vector2.zero);
            hand = handRect.gameObject.AddComponent<HandView>();

            var hudRect = UIFactory.CreateRect("HUD", root);
            hud = hudRect.gameObject.AddComponent<HudView>();
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

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (inspect != null) inspect.Close();
                else TogglePause();
            }
            HandleTargetKeys();
            if (!playing && Controller.Events.Count > 0) StartCoroutine(PlayEvents());
            hud.SetInteractable(InputEnabled);
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

        /// <summary>Long-press on a unit: open its full character sheet (only while the board is idle).</summary>
        public void OpenInspect(UnitView unit)
        {
            if (IsPaused || inspect != null || playing || Controller.Events.Count > 0) return;
            inspect = UnitInspectView.Open(root, unit, () => inspect = null);
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
            if (IsPaused) return;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (inspect != null) inspect.Close();
                else if (focused != null) focused.Inspect();
                return;
            }
            if (inspect != null) return;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (focused != null && focused.Unit.Team == Team.Enemy && InputEnabled) Controller.SelectEnemy(focused.Unit);
                return;
            }

            int step = 0;
            bool switchSide = false;
            if (Input.GetKeyDown(KeyCode.RightArrow)) step = 1;
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) step = -1;
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)) switchSide = true;
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
            if (InputEnabled) Controller.EndPlanning();
        }

        public void OnUndo()
        {
            if (InputEnabled) Controller.Undo();
        }

        public string ResultButtonLabel { get { return bootstrap.HasReturn ? "CONTINUE" : "FIGHT AGAIN"; } }

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
            switch (e)
            {
                case RoundStartedEvent round:
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
                    hud.SetActionPoints(ap.Current, ap.Max);
                    return null;
                case MoxieChangedEvent moxie:
                    return PlayMoxie(moxie);
                case IntentChosenEvent intent:
                    var iv = Get(intent.Unit);
                    if (iv != null) iv.SetIntent(intent.Intent);
                    return null;
                case TargetSelectedEvent selected:
                    foreach (var pair in unitViews)
                        if (pair.Key.Team == Team.Enemy) pair.Value.SetSelected(pair.Key == selected.Target);
                    return null;
                case CardCastEvent cast:
                    return PlayCast(cast);
                case DamageEvent damage:
                    return PlayDamage(damage);
                case HealEvent heal:
                    return PlayHeal(heal);
                case StatusesChangedEvent statuses:
                    var sv = Get(statuses.Unit);
                    if (sv != null) sv.SetStatuses(statuses.Statuses);
                    return null;
                case UnitDiedEvent died:
                    hud.Log(died.Unit.Name + " is defeated!");
                    var dv = Get(died.Unit);
                    return dv != null ? dv.Die() : null;
                case LogEvent log:
                    hud.Log(log.Message);
                    return null;
                case BattleEndedEvent ended:
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
            hud.ShowBanner(e.Card.Owner.Name + "'s Ritual is ready!", CardView.RitualPastel(e.Card.Owner.Definition.color), 0.6f);
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
            hud.SetQueue(e.QueueAfter);
            var caster = Get(e.Caster);
            var targets = new List<UnitView>();
            foreach (var t in e.Targets)
            {
                var tv = Get(t);
                if (tv != null) targets.Add(tv);
            }

            var color = e.Card.IsRitual ? CardView.RitualPastel(e.Caster.Definition.color) : e.Caster.Definition.color;
            string title = e.Card.IsRitual ? "« " + e.Card.Name + " »" : e.Card.Name + " " + UIFactory.Roman(e.Card.Rank);
            hud.ShowBanner(title, color, e.Card.IsRitual ? 0.9f : 0.5f);
            hud.Log(e.Caster.Name + " uses " + title);
            if (caster != null && e.Caster.Team == Team.Enemy) caster.SetIntent(null);

            var def = e.Card.Definition;
            var sequence = def.castSequence != null ? def.castSequence : CastSequence.GetDefault(def.kind, e.Card.IsRitual);
            // Body animation: sequences can choose their own with PlayAnimationStep; otherwise attacks slash, the rest cast.
            if (caster != null && !sequence.steps.Exists(st => st is PlayAnimationStep))
            {
                bool attack = !e.Card.IsRitual && (def.kind == CardKind.Attack || def.kind == CardKind.Debuff);
                caster.PlayAnimation(attack ? "slash" : "spellcast");
            }
            yield return StartCoroutine(sequence.Play(new CastContext(this, caster, targets, color)));
        }

        IEnumerator PlayDamage(DamageEvent e)
        {
            var tv = Get(e.Target);
            if (tv == null) yield break;
            tv.SetHP(e.HPAfter, e.MaxHP);

            Color color = Color.white;
            int size = 72;
            string text = e.Amount.ToString();
            if (e.IsTick) color = new Color(0.75f, 0.5f, 1f);
            else if (e.Relation == AfflatusRelation.Advantage) color = new Color(1f, 0.85f, 0.35f);
            else if (e.Relation == AfflatusRelation.Disadvantage) color = new Color(0.7f, 0.7f, 0.75f);
            if (e.Crit)
            {
                size = 92;
                color = Color.Lerp(color, new Color(1f, 0.45f, 0.2f), 0.6f);
                text = "<size=34>CRIT!</size>\n" + text;
            }
            SpawnDamageNumber(tv, text, color, size);
            StartCoroutine(tv.Shake(e.Crit ? 16f : 10f, 0.22f));
            StartCoroutine(tv.Flash(e.IsTick ? new Color(0.7f, 0.4f, 1f) : new Color(1f, 0.35f, 0.35f), 0.2f));

            string note = e.Crit ? " (crit)" : "";
            if (e.Relation == AfflatusRelation.Advantage) note += " (afflatus+)";
            else if (e.Relation == AfflatusRelation.Disadvantage) note += " (afflatus-)";
            hud.Log(e.Target.Name + " takes " + e.Amount + note);
            yield return Tween.Wait(0.16f);
        }

        IEnumerator PlayHeal(HealEvent e)
        {
            var tv = Get(e.Target);
            if (tv == null) yield break;
            tv.SetHP(e.HPAfter, e.MaxHP);
            SpawnDamageNumber(tv, "+" + e.Amount, new Color(0.45f, 1f, 0.55f), 64);
            hud.Log(e.Target.Name + " heals " + e.Amount);
            yield return Tween.Wait(0.12f);
        }

        // Numbers currently showing per unit, so multi-hits stack upward instead of overlapping.
        readonly Dictionary<UnitView, int> activeNumbers = new Dictionary<UnitView, int>();

        /// <summary>Big number that pops in just above the unit's HP bar, holds, then drifts up and fades.</summary>
        public void SpawnDamageNumber(UnitView unit, string text, Color color, int size)
        {
            int active;
            activeNumbers.TryGetValue(unit, out active);
            activeNumbers[unit] = active + 1;

            var t = UIFactory.CreateText(FxLayer, "DamageNumber", text, size, color, TextAnchor.LowerCenter, FontStyle.Bold);
            t.supportRichText = true;
            var rt = t.rectTransform;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(420f, 160f);
            float scale = transform.lossyScale.y;
            rt.position = unit.DamageAnchor + new Vector3(Random.Range(-18f, 18f) * scale, active * 44f * scale, 0f);
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
            t.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -5f);
            StartCoroutine(FloatAway(t, unit));
        }

        IEnumerator FloatAway(Text t, UnitView unit)
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
