using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Chapter One (bible §9): the only place with the chapter's beats. The overworld calls in at a few points:
    /// <see cref="Arrival"/> (the opening and the awakening), <see cref="OnAreaBuilt"/> (story props),
    /// <see cref="OnEnterCell"/> (story triggers), <see cref="Scan"/> (the scanner, Q) and the story props' interact.
    /// Progress lives in <see cref="StoryState"/> flags (<see cref="Chapter1"/>); every beat saves. Every word shown comes
    /// from Resources/Story/Chapter1.txt (<see cref="StoryText"/>), by block key, so the script can be edited without code.
    /// </summary>
    public static class ChapterOneDirector
    {
        static readonly DialogueVoice Narrator = new DialogueVoice(0.85f, 0.9f, new Color(0.85f, 0.8f, 0.7f));
        static readonly DialogueVoice MemoryVoice = new DialogueVoice(1.05f, 0.7f, new Color(1f, 0.86f, 0.6f));
        static readonly DialogueVoice ScannerVoice = new DialogueVoice(1.4f, 1.3f, WorldArt.WarpCyan);
        static readonly DialogueVoice CollectorVoice = new DialogueVoice(0.8f, 1.05f, new Color(0.75f, 0.8f, 0.9f));
        /// <summary>Erin: warm, quick, ember-orange.</summary>
        static readonly DialogueVoice ErinVoice = new DialogueVoice(1.18f, 1.15f, new Color(1f, 0.6f, 0.28f));

        /// <summary>The Collector: a scripted fight at the clinic gate, no map machine.</summary>
        public static readonly WorldAreas.EncounterDef CollectorFight = new WorldAreas.EncounterDef
        {
            Enemies = new[] { "Enemy_Collector" }, Level = 7, WinFlag = Chapter1.CollectorBeaten, WinKey = "collector.win",
            Reward = "Part_UltChip",
            PartyCantFall = true,   // your first real fight: nobody drops below 1 HP
        };

        const float ScanRadius = 9f, RouteRadius = 6f;

        static bool InStory { get { return !WorldSession.InSandbox; } }

        /// <summary>The voice a speaker in the script talks with.</summary>
        public static DialogueVoice VoiceFor(string speaker)
        {
            switch (speaker)
            {
                case "Scanner": return ScannerVoice;
                case "Collector": return CollectorVoice;
                case "Erin": return ErinVoice;
                case "Memory":
                case "Memory Fragment": return MemoryVoice;
                default: return Narrator;
            }
        }

        static List<DialogueLine> Script(string key) { return StoryText.Lines(key, VoiceFor); }

        static void Say(OverworldBuilder b, string key, System.Action done = null) { b.UI.Talk(Script(key), done); }

        // ------------------------------------------------------------------ Arrival: the opening and the awakening

        /// <summary>A scripted arrival for this area, or null for the usual fade-in.</summary>
        public static IEnumerator Arrival(OverworldBuilder b)
        {
            if (!WorldSession.TitleDone) return null;
            // The office plays the opening until you've woken in the city (it saves the name early, so a quit during the
            // recording comes back here and plays it again rather than leaving you in a room with no way out).
            if (b.Area.Id == WorldAreas.Office) return StoryState.Has(Chapter1.Awake) ? null : Opening(b);
            if (b.Area.Id != WorldAreas.City) return null;
            if (!StoryState.Has(Chapter1.Named)) return ToOffice(b);   // a save from before the office opening
            if (!StoryState.Has(Chapter1.Awake)) return Awakening(b);
            // Quit before the reflection (it saves at the end): the first steps again.
            if (!StoryState.Has(Chapter1.Reflection) && !StoryState.Has(Chapter1.Scanner)) return FirstStepsAgain(b);
            return null;
        }

        /// <summary>The opening, at the office desk just north of the start square: the login (your name), the
        /// recording and the failed transfer on its PC, then the cut to the city, where the awakening plays.</summary>
        static IEnumerator Opening(OverworldBuilder b)
        {
            b.SetCutscene(true);
            b.UI.SetFade(Color.black);
            bool done = false;
            PrologueSequence.Play(b, NearestPC(b.PlayerPosition), () => done = true);
            while (!done) yield return null;
            b.UI.SetFade(Color.black);
            b.GoTo(WorldAreas.StartArea);
        }

        static IEnumerator ToOffice(OverworldBuilder b)
        {
            b.SetCutscene(true);
            b.UI.SetFade(Color.black);
            yield return null;
            b.GoTo(WorldAreas.Office);
        }

        /// <summary>The office PC closest to <paramref name="spot"/> (null if the area has none).</summary>
        static OfficePC NearestPC(Vector3 spot)
        {
            OfficePC best = null;
            float bestDistance = float.MaxValue;
            foreach (var pc in Object.FindObjectsOfType<OfficePC>())
            {
                float d = (pc.GlassCenter - spot).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = pc;
                }
            }
            return best;
        }

        static IEnumerator Awakening(OverworldBuilder b)
        {
            b.SetCutscene(true);
            b.UI.SetFade(Color.black);
            yield return new WaitForSeconds(0.8f);
            yield return b.UI.FadeTo(new Color(0f, 0f, 0f, 0f), 2.6f);
            yield return Talk(b, Script("awakening"));
            StoryState.Set(Chapter1.Awake);   // Erin waits in the pod room (SpawnErin)
            SaveGame.Save();
            yield return FirstSteps(b);
        }

        /// <summary>
        /// Right after waking: a hint to move (WASD / arrow keys) that goes once you do. Her first steps carry on a
        /// moment on their own, unsteady, and stop at a puddle; then the reflection (<see cref="ReflectionSequence"/>):
        /// she sees she's a machine, her cracked Core, and her Soul Card. Then the area's title and you're free.
        /// </summary>
        static IEnumerator FirstSteps(OverworldBuilder b)
        {
            if (StoryState.Has(Chapter1.Reflection))
            {
                b.SetCutscene(false);
                yield return b.UI.ShowTitle(b.Area.Name, b.Area.Subtitle);
                yield break;
            }
            b.SetCutscene(false);
            var hint = MoveHint((RectTransform)b.UI.transform, UIText.Get("awakening.move_hint"));
            var start = b.PlayerPosition;
            while ((b.PlayerPosition - start).sqrMagnitude < 0.09f) yield return null;
            b.StartCoroutine(FadeOut(hint));

            // She keeps going a moment on her own, unsteady, slowing to a stop.
            b.SetCutscene(true);
            var player = b.Player;
            if (player != null)
            {
                player.Drift(player.LastInput, 0.9f);
                while (player.Drifting) yield return null;
            }
            yield return new WaitForSeconds(0.45f);
            if (b.PlayerSprite != null) b.PlayerSprite.SetFacing(CharacterSprite.Down);   // looking down
            yield return Talk(b, Script("reflection.notice"));

            var self = b.AllCharacters.Find(c => c != null && c.name == WorldSession.StartingMember);
            bool over = false;
            ReflectionSequence.Play(b, self, () => over = true);
            while (!over) yield return null;
            StoryState.Set(Chapter1.Reflection);
            SaveGame.Save();
            b.SetCutscene(false);
            yield return b.UI.ShowTitle(b.Area.Name, b.Area.Subtitle);
        }

        static IEnumerator FirstStepsAgain(OverworldBuilder b)
        {
            b.SetCutscene(true);
            yield return new WaitForSeconds(0.5f);
            yield return FirstSteps(b);
        }

        /// <summary>The "how to move" hint: an ink plate low on the screen that fades in and waits.</summary>
        static CanvasGroup MoveHint(RectTransform canvas, string text)
        {
            var plate = InkStyle.Panel(canvas, "MoveHint", InkStyle.InkPanel);
            var rt = plate.rectTransform;
            var bottom = new Vector2(0.5f, 0f);
            rt.Place(bottom, bottom, new Vector2(0f, 220f), new Vector2(760f, 110f));
            InkStyle.Frame(rt, InkStyle.Rule, 2f);
            var label = UIFactory.CreateText(rt, "Text", text, UIFactory.TextSize.Large, InkStyle.Paper, TextAnchor.MiddleCenter);
            label.supportRichText = true;
            label.rectTransform.Fill(12f);
            var group = plate.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            plate.gameObject.AddComponent<Fader>().StartCoroutine(Tween.Run(0.5f, p => group.alpha = p, null, true));
            return group;
        }

        static IEnumerator FadeOut(CanvasGroup group)
        {
            yield return Tween.Run(0.4f, p => { if (group != null) group.alpha = 1f - p; }, null, true);
            if (group != null) Object.Destroy(group.gameObject);
        }

        /// <summary>Somewhere to run a small UI coroutine.</summary>
        class Fader : MonoBehaviour { }

        // ------------------------------------------------------------------ Story props

        /// <summary>Places this area's story props (pod, memories, records, scanner, hidden wall, pedestal).</summary>
        public static void OnAreaBuilt(OverworldBuilder b)
        {
            var map = b.Map;
            var id = b.Area.Id;
            if (id == WorldAreas.Tunnel && !StoryState.Has(Chapter1.Tunnel))
            {
                StoryState.Set(Chapter1.Tunnel);
                SaveGame.Save();
            }
            if (id != WorldAreas.City && id != WorldAreas.Tunnel && id != WorldAreas.Chamber) return;

            foreach (var cell in map.FindAll('K'))
            {
                Prop(b, StoryProps.Pod(b.transform, map.CellCenter(cell.x, cell.y)), w => Say(w, "pod"));
                if (id == WorldAreas.City) SpawnErin(b, cell);
            }
            if (id == WorldAreas.City) SpawnErinByGoggles(b);
            if (id == WorldAreas.City || id == WorldAreas.Tunnel || id == WorldAreas.Chamber) b.StartCoroutine(ErinCatchesUp(b));

            for (char c = '1'; c <= '3'; c++)
            {
                string flag = "ch1.memory." + c;
                foreach (var cell in map.FindAll(c))
                {
                    var shard = StoryProps.Shard(b.transform, map.CellCenter(cell.x, cell.y));
                    if (StoryState.Has(flag)) { shard.SetActive(false); continue; }
                    string key = "memory." + c;
                    Prop(b, shard, w => Remember(w, key, flag, shard));
                }
            }

            for (char c = '4'; c <= '7'; c++)
                foreach (var cell in map.FindAll(c))
                {
                    string key = "record." + id + "." + c;
                    if (!StoryText.Has(key)) continue;
                    Prop(b, StoryProps.Terminal(b.transform, map.CellCenter(cell.x, cell.y)), w => Say(w, key));
                }

            foreach (var cell in map.FindAll('O'))
            {
                if (StoryState.Has(Chapter1.Scanner)) continue;
                var item = StoryProps.ScannerItem(b.transform, map.CellCenter(cell.x, cell.y));
                Prop(b, item, w => TakeScanner(w, item));
            }
            SpawnChip(b);

            if (!StoryState.Has(Chapter1.Route))
                foreach (var cell in map.FindAll('V'))
                    wall = StoryProps.CrackedWall(b.transform, map.CellCenter(cell.x, cell.y));

            foreach (var cell in map.FindAll('U'))
            {
                GameObject card;
                var pedestal = StoryProps.Pedestal(b.transform, map.CellCenter(cell.x, cell.y), out card);
                if (StoryState.Has(Chapter1.ResonanceArc)) card.SetActive(false);
                Prop(b, pedestal, w => TakeSoulCard(w, card));
            }
        }

        static GameObject wall;

        // ------------------------------------------------------------------ Erin (her bond, bible §2 Bonds)

        static ChapterOneDirector()
        {
            Bonds.Erin.Scenes[1].OnDone = ErinGivesBinder;   // rank 2, by the goggles
            Bonds.Erin.Scenes[2].OnDone = ErinGivesChipLate; // rank 3, after the Collector: joins, the Soul Card
        }

        // Erin's bond in Chapter One:
        //   rank 1  the pod room. She talks, tells you to find her, and vanishes.
        //   rank 2  by the goggles (the optical scanner, O), near the Collector's gate. She gives you the card binder and
        //           asks if you can even read and write: "No" gets the language chip now, "Yes" after the Collector. She
        //           tells you to grab the goggles and offers her battle tutorial (she coaches; she hasn't joined). She
        //           waits by the goggles while you face the Collector alone.
        //   rank 3  back from beating the Collector she finds you: you fight well enough to be worth following. She joins
        //           (Bonds.Erin.JoinRank = the Soul rank), gives her Soul Card, and the chip if you said you could read.

        /// <summary>Erin waits in the room you wake in (3 squares right of the pod, 1 up) for your first talk.</summary>
        static void SpawnErin(OverworldBuilder b, Vector2Int pod)
        {
            if (!InStory || Bonds.Seen(Bonds.Erin, 1) || StoryState.Has(Chapter1.ErinLost)
                || WorldSession.Members.Contains(Bonds.Erin.Character)) return;
            var npc = SpawnErinAt(b, new Vector2Int(pod.x + 3, pod.y - 1), 1);   // facing the pod
            if (npc != null) Prop(b, npc, w => w.StartCoroutine(TalkToErin(w, npc)));
        }

        /// <summary>After the pod room she waits by the goggles (a free square beside the O tile) until she joins: after the
        /// Collector her rank-3 scene finds you (or plays right after her rank-2 talk, if you beat him first).</summary>
        static void SpawnErinByGoggles(OverworldBuilder b)
        {
            if (!InStory || !Bonds.Seen(Bonds.Erin, 1) || Bonds.Joined(Bonds.Erin.Character)
                || WorldSession.Members.Contains(Bonds.Erin.Character) || StoryState.Has(Chapter1.ErinLost)) return;
            var tries = new[] { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 1),
                                new Vector2Int(1, 1), new Vector2Int(0, -1), new Vector2Int(-2, 0), new Vector2Int(2, 0) };
            foreach (var o in b.Map.FindAll('O'))
                foreach (var d in tries)
                {
                    var at = o + d;
                    if (b.Map.IsSolidCell(at.x, at.y)) continue;
                    var npc = SpawnErinAt(b, at, 0);
                    if (npc != null) Prop(b, npc, w => w.StartCoroutine(TalkToErin(w, npc)));
                    return;
                }
        }

        static GameObject SpawnErinAt(OverworldBuilder b, Vector2Int cell, int facing)
        {
            var erin = b.AllCharacters.Find(c => c != null && c.name == Bonds.Erin.Character);
            if (erin == null || erin.Anim == null) return null;
            var npc = b.SpawnCharacter(erin.DisplayName, erin.Anim, b.Map.CellCenter(cell.x, cell.y));
            npc.SetFacing(facing);
            return npc.gameObject;
        }

        static IEnumerator TalkToErin(OverworldBuilder b, GameObject npc)
        {
            bool firstTalk = !Bonds.Seen(Bonds.Erin, 1);
            bool binderTalk = Bonds.Seen(Bonds.Erin, 1) && !Bonds.Seen(Bonds.Erin, 2);
            bool joinedBefore = Bonds.Joined(Bonds.Erin.Character);
            yield return Bonds.Play(b, Bonds.Erin, VoiceFor);
            // Rank 2 done and the Collector already beaten: her rank-3 scene (she joins) follows straight on.
            if (binderTalk && Bonds.Seen(Bonds.Erin, 2) && Bonds.WhatsNext(Bonds.Erin) == Bonds.Next.Scene)
                yield return Bonds.Play(b, Bonds.Erin, VoiceFor);
            // She just joined: her battle tutorial (after the whole scene, so the rank-up is saved before the fight loads).
            if (!joinedBefore && Bonds.Joined(Bonds.Erin.Character))
            {
                if (npc != null) npc.SetActive(false);   // she walks with you now
                yield return OfferTutorialRoutine(b);
                yield break;
            }
            if (binderTalk && Bonds.Seen(Bonds.Erin, 2)) yield break;   // she waits here while you face the Collector
            if (npc == null) yield break;
            if (firstTalk && Bonds.Seen(Bonds.Erin, 1))
            {
                // "Find me." Then she flickers out, and the spot is empty.
                yield return Vanish(npc);
                yield return Talk(b, Script("erin.vanished"));
            }
            else if (Bonds.Joined(Bonds.Erin.Character))
            {
                npc.SetActive(false);   // she walks with you now
                b.StartCoroutine(ErinCatchesUp(b));   // the goggles already in hand: her Soul Card scene follows
            }
        }

        /// <summary>Erin flickers out (a few blinks, then gone).</summary>
        static IEnumerator Vanish(GameObject npc)
        {
            var sprite = npc.GetComponentInChildren<SpriteRenderer>();
            for (int i = 0; i < 6 && sprite != null; i++)
            {
                sprite.enabled = !sprite.enabled;
                yield return new WaitForSeconds(0.07f + 0.03f * i);
            }
            npc.SetActive(false);
        }

        /// <summary>Rank 2: the card binder (its words are in the scene), and the language chip now if you said you can't
        /// read (the second answer). "Yes" is remembered: the tutorial teases you about it.</summary>
        static IEnumerator ErinGivesBinder(OverworldBuilder b, int answer)
        {
            StoryState.Set(Chapter1.Binder);
            if (answer == 0) StoryState.Set(Chapter1.SaidCanRead);
            if (answer == 1) yield return GiveChip(b);
            yield return Talk(b, Script("erin.goggles"));
            SaveGame.Save();
        }

        /// <summary>Rank 3 (after the Collector): the language chip, if she held on to it at rank 2.</summary>
        static IEnumerator ErinGivesChipLate(OverworldBuilder b, int answer)
        {
            if (!StoryState.Has(Chapter1.LanguageChip)) yield return GiveChip(b);
        }

        static IEnumerator GiveChip(OverworldBuilder b)
        {
            yield return Talk(b, Script("erin.chip"));
            StoryState.Set(Chapter1.LanguageChip);
            SaveGame.Save();
        }

        /// <summary>From rank 2 her next scenes find you: after an area loads (back from the Collector fight, say), if one
        /// has unlocked, she pulls you aside.</summary>
        static IEnumerator ErinCatchesUp(OverworldBuilder b)
        {
            if (!InStory || StoryState.Has(Chapter1.ErinLost)) yield break;
            if (!Bonds.Seen(Bonds.Erin, 1))
            {
                // Never talked to her before the Collector (bond 0): she fought beside you anyway, so afterwards she
                // makes sure you have the binder (and the language chip) and points you at the tutorials.
                if (StoryState.Has(Chapter1.CollectorBeaten) && !StoryState.Has(Chapter1.Binder)
                    && WorldSession.Members.Contains(Bonds.Erin.Character))
                    yield return ErinCatchUpGifts(b);
                yield break;
            }
            // Before the Collector she only finds you from rank 2 on; after it (she fought beside you) she catches up
            // on every scene that's waiting: the binder if you never talked to her by the goggles, then rank 3.
            if (!Bonds.Seen(Bonds.Erin, 2) && !StoryState.Has(Chapter1.CollectorBeaten)) yield break;
            yield return new WaitForSeconds(1.2f);
            while (b.UI == null || b.UI.DialogueOpen) yield return null;
            bool joinedBefore = Bonds.Joined(Bonds.Erin.Character);
            for (int i = 0; i < 2 && !Bonds.Joined(Bonds.Erin.Character) && Bonds.WhatsNext(Bonds.Erin) == Bonds.Next.Scene; i++)
                yield return Bonds.Play(b, Bonds.Erin, VoiceFor);
            if (joinedBefore && Bonds.WhatsNext(Bonds.Erin) == Bonds.Next.Scene) yield return Bonds.Play(b, Bonds.Erin, VoiceFor);
            // Back from the Collector she joins, then offers her battle tutorial (fighting together).
            if (!joinedBefore && Bonds.Joined(Bonds.Erin.Character)) yield return OfferTutorialRoutine(b);
        }

        /// <summary>After the Collector, for a player who never talked to Erin before the fight: the card binder
        /// ([erin.catchup]), the language chip if they haven't got it, and where the tutorials are
        /// ([erin.catchup.tutorial]: Esc, Settings; they picked it up fast, so they probably won't need it).</summary>
        static IEnumerator ErinCatchUpGifts(OverworldBuilder b)
        {
            yield return new WaitForSeconds(1.2f);
            while (b.UI == null || b.UI.DialogueOpen) yield return null;
            b.SetCutscene(true);
            yield return Talk(b, Script("erin.catchup"));
            StoryState.Set(Chapter1.Binder);
            if (!StoryState.Has(Chapter1.LanguageChip)) yield return GiveChip(b);
            yield return Talk(b, Script("erin.catchup.tutorial"));
            SaveGame.Save();
            b.SetCutscene(false);
        }

        /// <summary>After her rank-2 scene: her battle tutorial (Yes), or straight on (I know how).</summary>
        static IEnumerator OfferTutorialRoutine(OverworldBuilder b)
        {
            b.SetCutscene(true);
            int answer = -1;
            var ask = Script("tutorial.offer");
            if (ask.Count > 1) yield return Talk(b, ask.GetRange(0, ask.Count - 1));
            if (ask.Count > 0)
            {
                b.UI.Dialogue.Ask(ask[ask.Count - 1], StoryText.Texts("tutorial.offer.options"), i => answer = i);
                while (answer < 0) yield return null;
            }
            b.SetCutscene(false);
            if (answer == 0) BattleTutorial.Start(b, true);
            else yield return Talk(b, Script("tutorial.skip"));
        }

        static void Prop(OverworldBuilder b, GameObject go, System.Action<OverworldBuilder> interact)
        {
            var it = go.AddComponent<StoryInteractable>();
            it.onInteract = interact;
            b.AddInteractable(it);
        }

        static void Remember(OverworldBuilder b, string key, string flag, GameObject shard)
        {
            var lines = Script(key);
            lines.AddRange(Script("memory.after"));
            b.UI.Talk(lines, () =>
            {
                StoryState.Set(flag);
                SaveGame.Save();
                if (shard != null) shard.SetActive(false);
            });
        }

        // ------------------------------------------------------------------ The scanner

        static void TakeScanner(OverworldBuilder b, GameObject item)
        {
            Say(b, "scanner.pickup", () =>
            {
                StoryState.Set(Chapter1.Scanner);
                SaveGame.Save();
                if (item != null) Object.Destroy(item);
            });
        }

        // ------------------------------------------------------------------ The card binder
        // Erin gives you the binder at bond rank 2 (ErinGivesBinder); there is no pickup in the world any more.

        // ------------------------------------------------------------------ The language chip

        /// <summary>
        /// The chips that make cards readable, each lying where its map tile is until you take it: the colours chip (Q,
        /// the city's bottom-right corner: text colours) and the numbers chip (M, the Buried Line tunnel: numbers). The
        /// language chip (words) comes from Erin (GiveChip), at bond rank 2 or 3.
        /// </summary>
        static void SpawnChip(OverworldBuilder b)
        {
            SpawnChip(b, 'Q', Chapter1.ColoursChip, "chip.colours.pickup", "ColoursChip", new Color(0.8f, 0.5f, 1f));
            SpawnChip(b, 'M', Chapter1.NumbersChip, "chip.numbers.pickup", "NumbersChip", new Color(1f, 0.82f, 0.35f));
        }

        static void SpawnChip(OverworldBuilder b, char tile, string flag, string script, string name, Color core)
        {
            if (StoryState.Has(flag)) return;
            foreach (var cell in b.Map.FindAll(tile))
            {
                var chip = StoryProps.Chip(b.transform, b.Map.CellCenter(cell.x, cell.y), name, core);
                Prop(b, chip, w => TakeChip(w, chip, flag, script));
            }
        }

        static void TakeChip(OverworldBuilder b, GameObject chip, string flag, string script)
        {
            if (StoryState.Has(flag)) return;
            Say(b, script, () =>
            {
                StoryState.Set(flag);
                SaveGame.Save();
                if (chip != null) Object.Destroy(chip);
            });
        }

        /// <summary>Q: a pulse that reveals traces and weak points nearby (never elemental weaknesses).</summary>
        public static void Scan(OverworldBuilder b)
        {
            if (!StoryState.Has(Chapter1.Scanner)) return;
            var at = b.PlayerPosition;
            StoryProps.ScanPulse(b.transform, at, ScanRadius);
            var map = b.Map;
            int traces = 0;
            foreach (var cell in map.FindAll(':'))
            {
                var p = map.CellCenter(cell.x, cell.y);
                if (Vector3.Distance(p, at) > ScanRadius) continue;
                StoryProps.TraceMark(b.transform, p, 10f);
                traces++;
            }

            // The hidden route's weak point.
            foreach (var cell in map.FindAll('V'))
            {
                if (Vector3.Distance(map.CellCenter(cell.x, cell.y), at) > RouteRadius) continue;
                if (!StoryState.Has(Chapter1.CollectorBeaten))
                {
                    Say(b, "scan.jammed");
                    return;
                }
                OpenRoute(b, cell);
                return;
            }

            if (traces > 0) Say(b, "scan.trace");
            else if (b.Area.Id == WorldAreas.City && StoryState.Has(Chapter1.CollectorBeaten) && !StoryState.Has(Chapter1.Route)) Say(b, "scan.none.hint");
            else Say(b, "scan.none");
        }

        static void OpenRoute(OverworldBuilder b, Vector2Int cell)
        {
            Say(b, "route.open", () =>
            {
                StoryState.Set(Chapter1.Route);
                if (wall != null) Object.Destroy(wall);
                b.OpenWarp(cell, WorldAreas.Tunnel);
                SaveGame.Save();
            });
        }

        // ------------------------------------------------------------------ Triggers: the Collector and the guardian

        /// <summary>The player stepped onto a new square (<paramref name="tile"/> is its map tile).</summary>
        public static void OnEnterCell(OverworldBuilder b, Vector2Int cell, char tile)
        {
            if (tile != 'Z' || !InStory) return;
            // The Collector stops you once you have the goggles (you face him alone; Erin joins you after, impressed,
            // and then teaches you to fight together). Talking to her by the goggles first is optional.
            if (b.Area.Id == WorldAreas.City && StoryState.Has(Chapter1.Scanner) && !StoryState.Has(Chapter1.CollectorBeaten))
                b.StartCoroutine(Collector(b));
            else if (b.Area.Id == WorldAreas.Chamber && !StoryState.Has(Chapter1.DragonBeaten))
                b.StartCoroutine(Guardian(b));
        }

        static IEnumerator Collector(OverworldBuilder b)
        {
            b.SetCutscene(true);
            if (StoryState.Has(Chapter1.CollectorMet))
                yield return Talk(b, Script("collector.again"));
            else
            {
                yield return Talk(b, Script("collector.intro"));
                int answer = -1;
                var question = Script("collector.question");
                b.UI.Dialogue.Ask(question[question.Count - 1], StoryText.Texts("collector.options"), i => answer = i);
                while (answer < 0) yield return null;
                var reply = Script("collector.answer." + (answer + 1));
                reply.AddRange(Script("collector.final"));
                yield return Talk(b, reply);
                StoryState.Set(Chapter1.CollectorMet);
                SaveGame.Save();
            }
            // Erin steps in: you're interesting and she's bored, so she's coming with you (she fights beside you).
            if (!WorldSession.Members.Contains(Bonds.Erin.Character) && !StoryState.Has(Chapter1.ErinLost))
            {
                yield return Talk(b, Script("collector.erin"));
                WorldSession.Recruit(Bonds.Erin.Character);
                b.RefreshParty();
                SaveGame.Save();
            }
            b.SetCutscene(false);
            b.StartStoryFight(CollectorFight, "collector");
        }

        static IEnumerator Guardian(OverworldBuilder b)
        {
            b.SetCutscene(true);
            if (!StoryState.Has(Chapter1.DragonSeen))
            {
                yield return Talk(b, Script("guardian.intro"));
                StoryState.Set(Chapter1.DragonSeen);
                SaveGame.Save();
            }
            else yield return Talk(b, Script("guardian.again"));
            b.SetCutscene(false);
            var dragon = b.FindEncounter(e => e.def != null && e.def.WinFlag == Chapter1.DragonBeaten);
            if (dragon != null) b.StartEncounter(dragon);
        }

        // ------------------------------------------------------------------ The Soul Card and the ending

        static void TakeSoulCard(OverworldBuilder b, GameObject card)
        {
            if (StoryState.Has(Chapter1.ResonanceArc)) { Say(b, "pedestal.empty"); return; }
            if (!StoryState.Has(Chapter1.DragonBeaten)) { Say(b, "pedestal.blocked"); return; }
            b.StartCoroutine(Absorb(b, card));
        }

        static IEnumerator Absorb(OverworldBuilder b, GameObject card)
        {
            b.SetCutscene(true);
            yield return Talk(b, Script("absorb"));
            if (card != null) card.SetActive(false);
            WorldSession.UnlockResonance(Resonance.Arc);
            StoryState.Set(Chapter1.ResonanceArc);
            WorldSession.BindSoulCard();
            SaveGame.Save();
            var banner = StoryText.Texts("absorb.banner");
            yield return b.UI.ShowTitle(banner.Length > 0 ? banner[0] : "", banner.Length > 1 ? banner[1] : "");
            yield return Talk(b, Script("absorb.core"));
            yield return Ending(b);
        }

        /// <summary>
        /// The end of Chapter One (bible §4, <i>Erin and the Soul Card Event</i>). The Collector comes back for the
        /// scanner and traps you in a cage of light; Erin stands between you and he beats her down, then you; you black
        /// out (<see cref="Trap"/>). Then the panels (<see cref="EndingSequence"/>): you wake to Erin in the old machine,
        /// her Soul Card forms bright, his dark glasses read it as blank, and he rips it apart. Then the end card and the
        /// thank-you screen. Every line is an [ending.*] block in Chapter1.txt.
        /// </summary>
        static IEnumerator Ending(OverworldBuilder b)
        {
            // She's with you whether or not your bond got that far (she joins for the Collector fight either way).
            bool erinWithYou = Bonds.Joined(Bonds.Erin.Character) || WorldSession.Members.Contains(Bonds.Erin.Character);
            if (erinWithYou && !StoryState.Has(Chapter1.ErinLost))
            {
                var erin = b.AllCharacters.Find(c => c != null && c.name == Bonds.Erin.Character);
                var catalog = EnemyCatalog.Get();
                UnitDefinition collector = catalog != null ? catalog.Find("Enemy_Collector") : null;
                var props = new List<GameObject>();
                yield return Trap(b, erin, collector, props);
                bool over = false;
                EndingSequence.Play(b, erin, collector, () => over = true);
                while (!over) yield return null;
                foreach (var go in props) if (go != null) Object.Destroy(go);
                if (b.PlayerSprite != null) b.PlayerSprite.ClearPose();
                WorldSession.Leave(Bonds.Erin.Character);
                StoryState.Set(Chapter1.ErinLost);
                SaveGame.Save();
                b.RefreshParty();
            }
            StoryState.Set(Chapter1.Complete);
            SaveGame.Save();
            b.UI.SetFade(Color.black);
            bool closed = false;
            EndCard((RectTransform)b.UI.transform, () => closed = true);
            while (!closed) yield return null;
            closed = false;
            ThankYouCard((RectTransform)b.UI.transform, () => closed = true);
            while (!closed) yield return null;
            yield return b.UI.FadeTo(new Color(0f, 0f, 0f, 0f), 1.4f);
            b.SetCutscene(false);
        }

        /// <summary>Debug: the reflection (the puddle, the Core, her Soul Card) again, here and now.</summary>
        public static void ReplayReflection(OverworldBuilder b)
        {
            b.SetCutscene(true);
            var self = b.AllCharacters.Find(c => c != null && c.name == WorldSession.StartingMember);
            ReflectionSequence.Play(b, self, () => b.SetCutscene(false));
        }

        /// <summary>Debug panel: the ending again from the start, on any save (Erin is put back in the party first).</summary>
        public static void ReplayEnding(OverworldBuilder b)
        {
            StoryState.Flags.Remove(Chapter1.ErinLost);
            StoryState.Flags.Remove(Chapter1.Complete);
            if (!Bonds.Joined(Bonds.Erin.Character))
                WorldSession.Bonds[Bonds.Erin.Character] = new WorldSession.BondProgress { Rank = Bonds.Erin.JoinRank };
            WorldSession.Recruit(Bonds.Erin.Character);
            b.RefreshParty();
            b.SetCutscene(true);
            b.StartCoroutine(Ending(b));
        }

        /// <summary>
        /// The trap, in the guardian's hall: footsteps, and the Collector walks in from the entrance (S) to face you
        /// ([ending.trap]). An EMP flash and a cage of light slams down around you ([ending.cage]). Erin steps between
        /// you and him and he beats her down, a blow before each line of [ending.beating]; then he turns on you, and the
        /// screen goes black. The props it spawns go in <paramref name="props"/> (removed once the panels are over).
        /// </summary>
        static IEnumerator Trap(OverworldBuilder b, CharacterDefinition erinDef, UnitDefinition collectorDef, List<GameObject> props)
        {
            var player = b.PlayerSprite;
            var you = b.PlayerPosition;
            var map = b.Map;
            if (player != null) player.SetFacing(CharacterSprite.Down);

            // Erin, at your side (she walks with you, so the world has no sprite of her until now).
            CharacterSprite erin = null;
            if (erinDef != null && erinDef.Anim != null)
            {
                var side = map.IsSolidAt(you + Vector3.right) ? you + Vector3.left : you + Vector3.right;
                erin = b.SpawnCharacter(erinDef.DisplayName, erinDef.Anim, side);
                erin.SetFacing(CharacterSprite.Down);
                props.Add(erin.gameObject);
            }
            yield return new WaitForSeconds(0.7f);

            // He walks in from the hall's entrance and stops three squares short of you.
            CharacterSprite collector = null;
            var entrance = map.FindAll('S');
            var from = entrance.Count > 0 ? map.CellCenter(entrance[0].x, entrance[0].y) : you + Vector3.back * 9f;
            var stop = you + Vector3.back * 3f;
            if (collectorDef != null && collectorDef.Anim != null)
            {
                collector = b.SpawnCharacter(collectorDef.DisplayName, collectorDef.Anim, from);
                props.Add(collector.gameObject);
                yield return Walk(collector, stop, 2.6f);
                collector.SetFacing(CharacterSprite.Up);
            }
            yield return Talk(b, Script("ending.trap"));

            // The EMP: a white-cyan flash, the pulse, and the cage slams down around you.
            StoryProps.ScanPulse(b.transform, collector != null ? collector.transform.position : stop, 7f);
            b.UI.SetFade(new Color(0.75f, 0.95f, 1f, 0.9f));
            props.Add(StoryProps.Cage(b.transform, you));
            b.StartCoroutine(Shake(0.25f, 0.35f));
            yield return b.UI.FadeTo(new Color(0.75f, 0.95f, 1f, 0f), 0.6f);
            yield return Talk(b, Script("ending.cage"));

            // Erin steps between you and him.
            if (erin != null)
            {
                yield return Walk(erin, you + Vector3.back * 1.4f, 4.5f);
                erin.SetFacing(CharacterSprite.Down);
            }

            // The beating: a blow before each line.
            var beating = Script("ending.beating");
            for (int i = 0; i < beating.Count; i++)
            {
                if (collector != null && erin != null) yield return Strike(b, collector, erin, i == beating.Count - 1 ? 0.45f : 0.25f);
                yield return Talk(b, beating.GetRange(i, 1));
            }
            if (erin != null)
            {
                erin.PlayPose("hurt");   // she goes down and stays down
                yield return new WaitForSeconds(1f);
            }

            // Then you.
            if (collector != null && player != null)
            {
                yield return Walk(collector, you + Vector3.back * 0.9f, 3f);
                yield return Strike(b, collector, player, 0.6f);
                player.PlayPose("hurt");
                yield return new WaitForSeconds(0.25f);
            }
            b.UI.SetFade(Color.black);
            yield return new WaitForSecondsRealtime(0.8f);
        }

        /// <summary>One blow: a lunge, a red flash and a knock back on the one hit, the screen shaking, a short hit-stop.</summary>
        static IEnumerator Strike(OverworldBuilder b, CharacterSprite attacker, CharacterSprite target, float shake)
        {
            var back = attacker.transform.position;
            var toward = target.transform.position - back;
            toward.y = 0f;
            var hit = target.transform.position - toward.normalized * 0.55f;
            yield return Walk(attacker, hit, 14f);
            var r = target.Renderer;
            if (r != null) r.color = new Color(1f, 0.35f, 0.3f, 1f);
            target.transform.position += toward.normalized * 0.18f;
            b.StartCoroutine(Shake(shake, 0.3f));
            yield return new WaitForSecondsRealtime(0.08f);   // hit-stop
            if (r != null) r.color = WorldArt.SpriteTint;
            yield return Walk(attacker, Vector3.Lerp(hit, back, 0.5f), 6f);
        }

        /// <summary>Walks a sprite in a straight line (no collisions: it's a cutscene).</summary>
        static IEnumerator Walk(CharacterSprite s, Vector3 to, float speed)
        {
            if (s == null) yield break;
            to.y = s.transform.position.y;
            var d = to - s.transform.position;
            if (d.sqrMagnitude > 0.0001f) s.Face(new Vector2(d.x, d.z));
            s.SetMoving(true);
            while (s != null && (s.transform.position - to).sqrMagnitude > 0.0004f)
            {
                s.transform.position = Vector3.MoveTowards(s.transform.position, to, speed * Time.deltaTime);
                yield return null;
            }
            if (s != null) s.SetMoving(false);
        }

        /// <summary>Shakes the overworld camera's aim for a moment.</summary>
        static IEnumerator Shake(float strength, float seconds)
        {
            var cam = Camera.main != null ? Camera.main.GetComponent<OverworldCamera>() : null;
            if (cam == null) yield break;
            var rest = cam.aimOffset;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                cam.aimOffset = rest + Random.insideUnitSphere * strength * (1f - t / seconds);
                yield return null;
            }
            cam.aimOffset = rest;
        }

        /// <summary>
        /// After the end card: thanks for playing, where to leave feedback (UI.txt thanks.feedback_url; "none" points to
        /// the itch.io comments instead) and a short look ahead at Chapter Two (texts from the [thanks] block). Fades in;
        /// Enter / click closes it.
        /// </summary>
        static void ThankYouCard(RectTransform canvas, System.Action closed)
        {
            var text = StoryText.Texts("thanks");
            string Part(int i) { return i < text.Length ? text[i] : ""; }
            var rt = UIFactory.CreateRect("ThankYou", canvas).Fill();
            rt.SetAsLastSibling();
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var bg = UIFactory.CreateImage(rt, "Wash", new Color(0.03f, 0.028f, 0.025f, 0.96f), true);
            bg.rectTransform.Fill();
            InkStyle.Grain(rt, new Color(0.2f, 0.18f, 0.14f, 0.05f));
            var center = new Vector2(0.5f, 0.5f);

            var title = InkStyle.Title(rt, "Title", Part(0), 96, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            title.rectTransform.Place(center, center, new Vector2(0f, 300f), new Vector2(1600f, 140f));
            var version = UIFactory.CreateText(rt, "Version", TitleScreen.GameTitle + "  ·  " + TitleScreen.Version, 32, InkStyle.PaperDim, TextAnchor.MiddleCenter);
            version.rectTransform.Place(center, center, new Vector2(0f, 210f), new Vector2(1400f, 50f));

            string url = UIText.Get("thanks.feedback_url").Trim();
            bool hasUrl = url.Length > 0 && url != "none";
            string feedback = hasUrl ? UIText.Get("thanks.feedback", "url", url) : UIText.Get("thanks.feedback.none");
            var ask = UIFactory.CreateText(rt, "Feedback", feedback, 36, InkStyle.Paper, TextAnchor.MiddleCenter);
            ask.supportRichText = true;
            ask.rectTransform.Place(center, center, new Vector2(0f, 110f), new Vector2(1500f, 110f));

            var rule = InkStyle.Line(rt, "Rule", InkStyle.Rule);
            rule.rectTransform.Place(center, center, new Vector2(0f, 20f), new Vector2(900f, 2f));
            var next = InkStyle.Title(rt, "Next", Part(1), 48, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            next.rectTransform.Place(center, center, new Vector2(0f, -50f), new Vector2(1600f, 70f));
            var teaser = UIFactory.CreateText(rt, "Teaser", Part(2), 34, InkStyle.Paper, TextAnchor.UpperCenter);
            teaser.supportRichText = true;
            teaser.rectTransform.Place(center, center, new Vector2(0f, -180f), new Vector2(1300f, 150f));
            var hint = UIFactory.CreateText(rt, "Hint", Part(3), 28, InkStyle.Paper, TextAnchor.MiddleCenter);
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(900f, 44f));

            var closer = rt.gameObject.AddComponent<CloseOnConfirm>();
            closer.closed = () => { Object.Destroy(rt.gameObject); closed(); };
            closer.StartCoroutine(Tween.Run(0.6f, p => { if (group != null) group.alpha = p; }, Tween.Linear, true));
        }

        /// <summary>"End of Chapter One" over the world, pointing to Sandbox Mode (texts from the [endcard] block); Enter / click closes it.</summary>
        static void EndCard(RectTransform canvas, System.Action closed)
        {
            var text = StoryText.Texts("endcard");
            string Part(int i) { return i < text.Length ? text[i] : ""; }
            var rt = UIFactory.CreateRect("EndCard", canvas).Fill();
            rt.SetAsLastSibling();
            var bg = UIFactory.CreateImage(rt, "Wash", new Color(0.04f, 0.035f, 0.03f, 0.92f), true);
            bg.rectTransform.Fill();
            var center = new Vector2(0.5f, 0.5f);
            var title = InkStyle.Title(rt, "Title", Part(0), 96, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            title.rectTransform.Place(center, center, new Vector2(0f, 120f), new Vector2(1600f, 140f));
            var sub = UIFactory.CreateText(rt, "Sub", Part(1), 40, InkStyle.Paper, TextAnchor.MiddleCenter);
            sub.rectTransform.Place(center, center, new Vector2(0f, 20f), new Vector2(1400f, 60f));
            var thanks = UIFactory.CreateText(rt, "Thanks", Part(2), 32, InkStyle.PaperDim, TextAnchor.MiddleCenter);
            thanks.rectTransform.Place(center, center, new Vector2(0f, -80f), new Vector2(1400f, 50f));
            var sandbox = UIFactory.CreateText(rt, "Sandbox", Part(3), 32, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            sandbox.rectTransform.Place(center, center, new Vector2(0f, -150f), new Vector2(1400f, 50f));
            var hint = UIFactory.CreateText(rt, "Hint", Part(4), 28, InkStyle.Paper, TextAnchor.MiddleCenter);
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(900f, 44f));
            rt.gameObject.AddComponent<CloseOnConfirm>().closed = () => { Object.Destroy(rt.gameObject); closed(); };
        }

        class CloseOnConfirm : MonoBehaviour
        {
            public System.Action closed;
            int frame;
            void Start() { frame = Time.frameCount; }
            void Update()
            {
                if (Time.frameCount <= frame + 1) return;
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                {
                    enabled = false;
                    closed();
                }
            }
        }

        static IEnumerator Talk(OverworldBuilder b, IList<DialogueLine> lines)
        {
            bool done = false;
            b.UI.Talk(lines, () => done = true);
            while (!done) yield return null;
        }
    }
}
