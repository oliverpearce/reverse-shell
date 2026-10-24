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

        /// <summary>The Collector: a scripted fight at the clinic gate, no map machine.</summary>
        public static readonly WorldAreas.EncounterDef CollectorFight = new WorldAreas.EncounterDef
        {
            Enemies = new[] { "Enemy_Collector" }, Level = 7, WinFlag = Chapter1.CollectorBeaten, WinKey = "collector.win",
            Reward = "Part_UltChip",
        };

        const float ScanRadius = 9f, RouteRadius = 6f;

        static bool InStory { get { return !WorldSession.InTraining; } }

        /// <summary>The voice a speaker in the script talks with.</summary>
        public static DialogueVoice VoiceFor(string speaker)
        {
            switch (speaker)
            {
                case "Scanner": return ScannerVoice;
                case "Collector": return CollectorVoice;
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
            if (!WorldSession.TitleDone || b.Area.Id != WorldAreas.City) return null;
            if (!StoryState.Has(Chapter1.Named)) return Opening(b);
            if (!StoryState.Has(Chapter1.Awake)) return Awakening(b);
            return null;
        }

        static IEnumerator Opening(OverworldBuilder b)
        {
            b.SetCutscene(true);
            b.UI.SetFade(Color.black);
            bool done = false;
            PrologueSequence.Play((RectTransform)b.UI.transform, () => done = true);
            while (!done) yield return null;
            b.RefreshParty();   // the HUD now shows the chosen name
            yield return Awakening(b);
        }

        static IEnumerator Awakening(OverworldBuilder b)
        {
            b.SetCutscene(true);
            b.UI.SetFade(Color.black);
            yield return new WaitForSeconds(0.8f);
            yield return b.UI.FadeTo(new Color(0f, 0f, 0f, 0f), 2.6f);
            yield return Talk(b, Script("awakening"));
            StoryState.Set(Chapter1.Awake);
            SaveGame.Save();
            b.SetCutscene(false);
            yield return b.UI.ShowTitle(b.Area.Name, b.Area.Subtitle);
        }

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
                Prop(b, StoryProps.Pod(b.transform, map.CellCenter(cell.x, cell.y)), w => Say(w, "pod"));

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

            if (!StoryState.Has(Chapter1.Route))
                foreach (var cell in map.FindAll('V'))
                    wall = StoryProps.CrackedWall(b.transform, map.CellCenter(cell.x, cell.y));

            foreach (var cell in map.FindAll('U'))
            {
                GameObject card;
                var pedestal = StoryProps.Pedestal(b.transform, map.CellCenter(cell.x, cell.y), out card);
                if (StoryState.Has(Chapter1.ResonanceVolt)) card.SetActive(false);
                Prop(b, pedestal, w => TakeSoulCard(w, card));
            }
        }

        static GameObject wall;

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
            if (StoryState.Has(Chapter1.ResonanceVolt)) { Say(b, "pedestal.empty"); return; }
            if (!StoryState.Has(Chapter1.DragonBeaten)) { Say(b, "pedestal.blocked"); return; }
            b.StartCoroutine(Absorb(b, card));
        }

        static IEnumerator Absorb(OverworldBuilder b, GameObject card)
        {
            b.SetCutscene(true);
            yield return Talk(b, Script("absorb"));
            if (card != null) card.SetActive(false);
            WorldSession.UnlockResonance(Afflatus.Volt);
            StoryState.Set(Chapter1.ResonanceVolt);
            WorldSession.BindSoulCard();
            SaveGame.Save();
            var banner = StoryText.Texts("absorb.banner");
            yield return b.UI.ShowTitle(banner.Length > 0 ? banner[0] : "", banner.Length > 1 ? banner[1] : "");
            yield return Talk(b, Script("absorb.core"));
            yield return Ending(b);
        }

        static IEnumerator Ending(OverworldBuilder b)
        {
            yield return Talk(b, Script("ending"));
            StoryState.Set(Chapter1.Complete);
            SaveGame.Save();
            bool closed = false;
            EndCard((RectTransform)b.UI.transform, () => closed = true);
            while (!closed) yield return null;
            b.SetCutscene(false);
        }

        /// <summary>"End of Chapter One" over the world (texts from the [endcard] block); Enter / click closes it.</summary>
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
            var hint = UIFactory.CreateText(rt, "Hint", Part(3), 28, InkStyle.Paper, TextAnchor.MiddleCenter);
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
