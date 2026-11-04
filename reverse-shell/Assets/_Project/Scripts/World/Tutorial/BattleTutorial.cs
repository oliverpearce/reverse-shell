using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Erin's battle tutorial: the first fight after she joins (Bonds), and Settings > Tutorials > Practice with Erin
    /// once you've met her. One weak machine; Erin talks you through four steps and only the step's action is allowed:
    ///   1. tap a Physical card (1 AP)
    ///   2. spend 1 AP moving a card next to its match, so they merge into Rank II
    ///   3. play the Rank II card: its Break damage empties the machine's Break value (BREAK, Staggered)
    ///   4. the BREAK banner, then the finishing strike
    /// Pressing Go is a step of its own whenever the AP runs out. The hands are set (each round the tutorial deals the
    /// cards the step needs), and the machine starts with exactly enough Break for steps 1 + 3, so it always works out;
    /// the Break and Stagger rules themselves are the normal ones. Erin's words: [tutorial.*] in Resources/Story/Bonds.txt.
    /// </summary>
    public static class BattleTutorial
    {
        public const string Id = "erin";
        const string StoryFight = "tutorial.erin", PracticeFight = "tutorial.erin.practice";

        /// <summary>The tutorial's machine: a Scrap Hound at level 1.</summary>
        public static readonly WorldAreas.EncounterDef Fight = new WorldAreas.EncounterDef
        {
            Enemies = new[] { "Enemy_ScrapHound" }, Level = 1,
        };

        /// <summary>Starts the guided fight (story: right after Erin joins; otherwise a practice replay).</summary>
        public static void Start(OverworldBuilder b, bool story)
        {
            b.StartStoryFight(Fight, story ? StoryFight : PracticeFight);
        }

        /// <summary>Which tutorial an encounter runs (its id ends with the fight's name), or null.</summary>
        public static string TutorialFor(string encounterId)
        {
            if (string.IsNullOrEmpty(encounterId)) return null;
            return encounterId.EndsWith("/" + StoryFight) || encounterId.EndsWith("/" + PracticeFight) ? Id : null;
        }

        /// <summary>A replay: no packs and nothing recorded.</summary>
        public static bool IsPractice(string encounterId)
        {
            return !string.IsNullOrEmpty(encounterId) && encounterId.EndsWith("/" + PracticeFight);
        }

        /// <summary>Settings > Tutorials offers the practice fight once you've met Erin.</summary>
        public static bool PracticeUnlocked { get { return StoryState.Has("bond.erin.1"); } }

        [RuntimeInitializeOnLoadMethod]
        static void Hook()
        {
            BattleBootstrap.TutorialStarted -= OnTutorial;
            BattleBootstrap.TutorialStarted += OnTutorial;
        }

        static void OnTutorial(BattleBootstrap boot, BattleView view, string id)
        {
            if (id != Id || view == null) return;
            var old = boot.GetComponent<ErinTutorial>();
            if (old != null) Object.Destroy(old);   // "fight again" restarts it
            boot.gameObject.AddComponent<ErinTutorial>().Begin(boot, view);
        }
    }

    /// <summary>Runs <see cref="BattleTutorial"/> inside the battle scene.</summary>
    public class ErinTutorial : MonoBehaviour
    {
        enum Step { Talking, Tap, Move, Cast, Go, Finish, Free }

        BattleView view;
        BattleController c;
        DialogueBox box;
        BattleUnit lead, enemy;
        CardDefinition strike;
        readonly List<CardDefinition> fillers = new List<CardDefinition>();
        Step step = Step.Talking, afterGo;
        CardInstance target, partner;   // the card the step is about (and, for the merge, its match)
        bool broken;
        Sprite portrait;

        public void Begin(BattleBootstrap boot, BattleView battleView)
        {
            view = battleView;
            c = boot.Controller;
            box = DialogueBox.Create(view.Root);
            box.BaseY = 330f;   // above the hand
            view.Allow = Gate;
            view.EventPlayed += OnEventPlayed;
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            if (view != null)
            {
                view.EventPlayed -= OnEventPlayed;
                if (view.Allow == (System.Func<string, CardInstance, int, bool>)Gate) view.Allow = null;
            }
        }

        void Update()
        {
            if (box != null && box.IsOpen && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)))
                box.Advance();
        }

        // ------------------------------------------------------------------ the steps

        IEnumerator Run()
        {
            while (!view.Idle) yield return null;
            Pick();
            if (strike == null || enemy == null) { Release(); yield break; }   // nothing to teach with: a normal fight

            // Exactly enough Break for a Rank I and then a Rank II Strike, and HP for a short fight.
            int breakNeeded = BreakRules.Value(strike, 1) + BreakRules.Value(strike, 2);
            view.SetUpEnemy(enemy, Mathf.Max(1, enemy.MaxHP / 3), breakNeeded);

            yield return Say("tutorial.erin.intro");
            DealForTap();
            yield return Say("tutorial.erin.1");
            Enter(Step.Tap);
        }

        /// <summary>The leader's Physical attack (the card the steps use), the machine, and filler cards (not Strikes).</summary>
        void Pick()
        {
            enemy = c.State.Enemies.Find(u => u != null && u.IsAlive);
            foreach (var u in c.State.Party)
            {
                if (u == null || !u.IsAlive) continue;
                foreach (var card in u.Incantations)
                {
                    if (card == null || card.isRitual) continue;
                    bool physicalHit = card.kind == CardKind.Attack && card.attackType == AttackType.Physical
                        && BreakRules.Value(card, 1) > 0;
                    if (strike == null && physicalHit) { strike = card; lead = u; }
                }
            }
            foreach (var u in c.State.Party)
                if (u != null && u.IsAlive)
                    foreach (var card in u.Incantations)
                        if (card != null && !card.isRitual && card != strike && !fillers.Contains(card)) fillers.Add(card);
            var erin = c.State.Party.Find(u => u != null && u.Definition != null && u.Definition.name == Bonds.Erin.Character);
            var erinDef = erin != null ? erin.Definition : null;
            if (erinDef == null)
                foreach (var d in Resources.FindObjectsOfTypeAll<CharacterDefinition>())
                    if (d.name == Bonds.Erin.Character) { erinDef = d; break; }
            portrait = erinDef != null ? OverworldBuilder.PortraitOf(erinDef.Anim) : null;
        }

        CardInstance Strike(int rank) { return new CardInstance(strike, lead, rank); }

        CardInstance Filler(int i)
        {
            if (fillers.Count == 0) return Strike(1);
            var def = fillers[i % fillers.Count];
            var owner = c.State.Party.Find(u => u != null && u.Incantations.Contains(def)) ?? lead;
            return new CardInstance(def, owner, 1);
        }

        /// <summary>Round 1: Strike, filler, Strike, filler, Strike. Taking the first leaves the other two apart.</summary>
        void DealForTap()
        {
            target = Strike(1);
            c.SetHand(new List<CardInstance> { target, Filler(0), Strike(1), Filler(1), Strike(1) });
        }

        /// <summary>The merge round (if Go came between): filler, Strike, filler, Strike.</summary>
        void DealForMove()
        {
            var a = Strike(1);
            var b = Strike(1);
            c.SetHand(new List<CardInstance> { Filler(0), a, Filler(1), b });
            partner = a;
            target = b;
        }

        /// <summary>The cast round: the Rank II card, with fillers (no Strikes, so nothing else merges).</summary>
        void DealForCast(CardInstance rank2)
        {
            var hand = new List<CardInstance> { rank2 ?? Strike(2), Filler(0), Filler(1) };
            c.SetHand(hand);
            target = hand[0];
        }

        void Enter(Step s)
        {
            step = s;
            if (s == Step.Move && (target == null || partner == null))
            {
                // the two Strikes left in the hand after the tap
                var strikes = new List<CardInstance>();
                foreach (var card in c.State.Hand.Cards) if (card.Definition == strike && card.Rank == 1) strikes.Add(card);
                if (strikes.Count >= 2) { partner = strikes[0]; target = strikes[strikes.Count - 1]; }
            }
            Highlight();
        }

        /// <summary>The step's card glows; the rest of the hand dims (nothing glows while Erin talks or on Go).</summary>
        void Highlight()
        {
            foreach (var card in c.State.Hand.Cards)
            {
                var v = view.ViewOf(card);
                if (v == null) continue;
                bool focus = card == target && (step == Step.Tap || step == Step.Move || step == Step.Cast);
                bool dim = step == Step.Tap || step == Step.Move || step == Step.Cast;
                v.SetMergeHighlight(focus, card.Rank);
                if (v.Group != null) v.Group.alpha = dim && !focus ? 0.45f : 1f;
            }
        }

        // ------------------------------------------------------------------ what's allowed

        bool Gate(string action, CardInstance card, int index)
        {
            switch (step)
            {
                case Step.Tap:
                case Step.Cast:
                    return action == "queue" && card == target;
                case Step.Move:
                    return (action == "drag" && card == target) || (action == "move" && card == target && Merges(index));
                case Step.Go:
                    return action == "go";
                case Step.Finish:
                    return action == "queue" || action == "go";
                case Step.Free:
                    return true;
                default:
                    return false;   // Erin is talking
            }
        }

        /// <summary>Whether moving the step's card to <paramref name="toIndex"/> puts it beside its match.</summary>
        bool Merges(int toIndex)
        {
            var hand = new List<CardInstance>(c.State.Hand.Cards);
            int from = hand.IndexOf(target);
            if (from < 0 || partner == null) return false;
            toIndex = Mathf.Clamp(toIndex, 0, hand.Count - 1);
            hand.RemoveAt(from);
            hand.Insert(toIndex, target);
            return Mathf.Abs(hand.IndexOf(target) - hand.IndexOf(partner)) == 1;
        }

        // ------------------------------------------------------------------ reacting to the battle

        void OnEventPlayed(BattleEvent e)
        {
            if (e is CardQueuedEvent q && (step == Step.Tap || step == Step.Cast) && q.Card == target)
            {
                StartCoroutine(AfterAction(step == Step.Tap ? Step.Move : Step.Finish));
            }
            else if (e is CardsMergedEvent m && step == Step.Move && m.Merge.Result.Rank == 2)
            {
                target = m.Merge.Result;
                StartCoroutine(AfterAction(Step.Cast));
            }
            else if (e is StaggeredEvent s && s.Unit == enemy && !broken)
            {
                broken = true;
            }
            else if (e is RoundStartedEvent && step == Step.Go)
            {
                StartCoroutine(NewRound());
            }
            else if (e is BattleEndedEvent)
            {
                Release();
            }
            else if (e is HandRestoredEvent || e is CardMovedEvent || e is CardDrawnEvent)
            {
                Highlight();
            }
        }

        /// <summary>The step's action is done: on to the next (in this round if there's AP left, else press Go first).</summary>
        IEnumerator AfterAction(Step next)
        {
            step = Step.Talking;
            Highlight();
            while (!view.Idle) yield return null;
            if (next == Step.Move && c.State.ActionPoints > 0)
            {
                yield return Say("tutorial.erin.2");
                Enter(Step.Move);
            }
            else if (next == Step.Cast && c.State.ActionPoints > 0)
            {
                yield return Say("tutorial.erin.3");
                Enter(Step.Cast);
            }
            else
            {
                afterGo = next;
                yield return Say(next == Step.Finish ? "tutorial.erin.go_break" : "tutorial.erin.go");
                Enter(Step.Go);
            }
        }

        /// <summary>After Go, the next round: the BREAK lesson (step 4), or the step that waited for AP.</summary>
        IEnumerator NewRound()
        {
            step = Step.Talking;
            while (!view.Idle) yield return null;
            if (broken && afterGo == Step.Finish)
            {
                yield return Say("tutorial.erin.4");
                Enter(Step.Finish);
                afterGo = Step.Free;
            }
            else if (afterGo == Step.Free)
            {
                yield return Say("tutorial.erin.done");
                Release();
            }
            else if (afterGo == Step.Move)
            {
                DealForMove();
                yield return Say("tutorial.erin.2");
                Enter(Step.Move);
            }
            else if (afterGo == Step.Cast)
            {
                CardInstance rank2 = null;
                foreach (var card in c.State.Hand.Cards) if (card.Definition == strike && card.Rank == 2) rank2 = card;
                DealForCast(rank2);
                yield return Say("tutorial.erin.3");
                Enter(Step.Cast);
            }
            else if (afterGo == Step.Finish && !broken)
            {
                // the Rank II hit didn't break it (shouldn't happen): let the fight go on as normal
                yield return Say("tutorial.erin.done");
                Release();
            }
        }

        /// <summary>The tutorial is over: everything allowed again, nothing dimmed.</summary>
        void Release()
        {
            step = Step.Free;
            if (view != null)
            {
                Highlight();
                view.Allow = null;
            }
        }

        IEnumerator Say(string key)
        {
            var lines = StoryText.Lines(key, ChapterOneDirector.VoiceFor);
            foreach (var l in lines) if (l.Speaker == "Erin") l.Portrait = portrait;
            if (lines.Count == 0) yield break;
            var before = step;
            step = Step.Talking;
            Highlight();
            bool done = false;
            box.Play(lines, () => done = true);
            while (!done) yield return null;
            step = before;
        }
    }
}
