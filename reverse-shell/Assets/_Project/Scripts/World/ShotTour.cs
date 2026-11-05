using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.World
{
    /// <summary>
    /// The screenshot run, for the store page and the README: a development player started with <c>-shots</c> plays
    /// itself through the demo (title, the office opening, the awakening, the Sandbox crew, a fight, the forest) and saves
    /// a 1920 x 1080 PNG at each stop, then quits. It keeps its saves in a folder of its own (SaveGame.FolderOverride) and
    /// puts the player's settings back afterwards. Release builds never run it (development builds and the editor only).
    ///   ReverseShell.exe -shots -shotsOut "C:\path\to\folder" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
    /// Built by Reverse Shell / Build Screenshot Player (Game.EditorTools.ShotBuild).
    /// </summary>
    public class ShotTour : MonoBehaviour
    {
        const int Width = 1920, Height = 1080;
        string outDir;
        readonly List<string> log = new List<string>();
        static bool started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (started || !(Application.isEditor || Debug.isDebugBuild)) return;
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-shots") < 0) return;
            started = true;
            Application.runInBackground = true;   // keep going when another window has focus
            var go = new GameObject("ShotTour");
            DontDestroyOnLoad(go);
            var tour = go.AddComponent<ShotTour>();
            int o = Array.IndexOf(args, "-shotsOut");
            tour.outDir = o >= 0 && o + 1 < args.Length ? args[o + 1] : Path.Combine(Application.dataPath, "..", "shots");
            // Before the first scene builds: the run's own saves, and nothing from an earlier session.
            SaveGame.FolderOverride = Path.Combine(Application.persistentDataPath, "shot_tour");
            try { if (Directory.Exists(SaveGame.FolderOverride)) Directory.Delete(SaveGame.FolderOverride, true); } catch (IOException) { }
            Directory.CreateDirectory(SaveGame.FolderOverride);
        }

        // ------------------------------------------------------------------ Settings (put back at the end)

        struct Saved { public bool Fast, Zoom, Auto, Alt; public int Speed, Controls; public string Seen; }
        Saved saved;

        void SaveSettings()
        {
            saved = new Saved
            {
                Fast = GameSettings.FastGraphics, Zoom = GameSettings.BattleZoom, Auto = GameSettings.AutoAdvance,
                Speed = GameSettings.TextSpeed, Controls = (int)GameSettings.Controls, Seen = PlayerPrefs.GetString("dialogue.seen", ""),
                Alt = GameSettings.AltArt,
            };
            // -altLook: the whole run in Settings > Machine Look: Tech Dungeon.
            GameSettings.AltArt = Array.IndexOf(Environment.GetCommandLineArgs(), "-altLook") >= 0;
            GameSettings.FastGraphics = false;   // Pretty
            GameSettings.BattleZoom = true;
            GameSettings.AutoAdvance = true;
            GameSettings.TextSpeed = 2;          // Fast
            GameSettings.Controls = GameSettings.ControlMode.Both;
        }

        void RestoreSettings()
        {
            GameSettings.FastGraphics = saved.Fast;
            GameSettings.BattleZoom = saved.Zoom;
            GameSettings.AutoAdvance = saved.Auto;
            GameSettings.TextSpeed = saved.Speed;
            GameSettings.Controls = (GameSettings.ControlMode)saved.Controls;
            GameSettings.AltArt = saved.Alt;
            PlayerPrefs.SetString("dialogue.seen", saved.Seen);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ The run

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            SaveSettings();
            if (!Application.isEditor) Screen.SetResolution(Width, Height, false);
            yield return Step("title", Title());
            yield return Step("opening", Opening());
            yield return Step("awakening", Awakening());
            yield return Step("reflection", Reflection());
            yield return Step("city", City());
            yield return Step("battle", Battle());
            yield return Step("ending", Ending());
            // The finished areas (their names are in [brackets] in the area list): overworld shots only.
            yield return Step("complex", Area("intro_forest", "02-overgrown-forest"));
            yield return Step("overgrowth", Area("intro_forest_lab", "11-overgrowth"));
            yield return Step("conservatory", Area("intro_conservatory", "12-conservatory"));
            yield return Step("greenhouse", Area("intro_conservatory_hall", "13-greenhouse"));
            RestoreSettings();
            Note("done");
            File.WriteAllLines(Path.Combine(outDir, "shot_tour_log.txt"), log.ToArray());
            try { Directory.Delete(SaveGame.FolderOverride, true); } catch (IOException) { }
            yield return new WaitForSecondsRealtime(0.5f);
            if (Application.isEditor) Debug.Log("ShotTour finished: " + outDir);
            else Application.Quit();
        }

        /// <summary>Runs one part of the tour; an exception or a timeout is logged and the tour carries on.</summary>
        IEnumerator Step(string name, IEnumerator routine)
        {
            Note("-- " + name);
            float began = Time.realtimeSinceStartup;
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) break;
                    current = routine.Current;
                }
                catch (Exception e)
                {
                    Note("FAILED " + name + ": " + e);
                    break;
                }
                if (Time.realtimeSinceStartup - began > 240f) { Note("TIMEOUT " + name); break; }
                yield return current;
            }
            Time.timeScale = 1f;
        }

        void Note(string s)
        {
            log.Add(Time.realtimeSinceStartup.ToString("0.0") + "  " + s);
            Debug.Log("ShotTour: " + s);
        }

        // ------------------------------------------------------------------ Stops

        IEnumerator Title()
        {
            yield return Until(() => FindObjectOfType<TitleScreen>() != null, 40f, "title screen");
            yield return new WaitForSecondsRealtime(6f);   // the Soul cards drift down
            yield return Shoot("01-title-screen");
        }

        IEnumerator Opening()
        {
            WorldSession.StartSlot(1, true);
            Time.timeScale = 1f;
            ScreenFade.LoadScene(SceneManager.GetActiveScene().name);
            yield return Until(() => GameObject.Find("NameField") != null, 90f, "the name prompt");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shoot("story-office");
            PrologueSequence.AutoName = "A3";

            // The recording: press on through the narration (each press finishes a line, the next moves on) until the
            // gold word is typed.
            var advance = typeof(PrologueSequence).GetField("advance", BindingFlags.Instance | BindingFlags.NonPublic);
            float until = Time.realtimeSinceStartup + 120f;
            while (GameObject.Find("Everlasting") == null && Time.realtimeSinceStartup < until)
            {
                var seq = FindObjectOfType<PrologueSequence>();
                if (seq != null && advance != null) advance.SetValue(seq, true);
                yield return new WaitForSecondsRealtime(0.7f);
            }
            if (GameObject.Find("Everlasting") == null) Note("no Everlasting word");
            yield return new WaitForSecondsRealtime(2.8f);   // every letter popped, the glow breathing
            yield return Shoot("story-everlasting");
            // The transfer fails and the city loads.
            yield return Until(() => FindObjectOfType<PrologueSequence>() == null && WorldSession.CurrentArea != WorldAreas.Office
                                     && FindObjectOfType<OverworldBuilder>() != null, 120f, "the city");
        }

        IEnumerator Awakening()
        {
            // The awakening's thoughts type out (Auto-Advance is on): catch the capsule line, then the room with Erin.
            bool shot = false;
            float until = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < until)
            {
                var box = OpenDialogue();
                var text = box != null ? box.GetComponentInChildren<DynamicText>() : null;
                if (text != null && text.PlainText.Contains("capsule") && text.FullyRevealed)
                {
                    yield return new WaitForSecondsRealtime(0.3f);
                    yield return Shoot("story-awakening");
                    shot = true;
                    break;
                }
                yield return null;
            }
            if (!shot) Note("no capsule line");
            yield return Until(() => OpenDialogue() == null, 40f, "the awakening to end");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shoot("story-awakening-room");
        }

        /// <summary>The reflection, right where the awakening left off: the puddle, the chest plate open on the Core, and
        /// her Soul Card risen out of it (the move hint is cleared first so it isn't in the shots).</summary>
        IEnumerator Reflection()
        {
            var world = FindObjectOfType<OverworldBuilder>();
            if (world == null) { Note("no world for the reflection"); yield break; }
            var hint = GameObject.Find("MoveHint");
            if (hint != null) Destroy(hint);
            ChapterOneDirector.ReplayReflection(world);
            yield return Until(() => GameObject.Find("Puddle") != null, 20f, "the puddle");
            yield return new WaitForSecondsRealtime(2.4f);   // up out of black, the first line typing
            yield return Shoot("story-reflection");
            yield return Until(() => GameObject.Find("PlateL") != null, 90f, "the chest plate");
            yield return new WaitForSecondsRealtime(1.9f);   // the plates slid apart
            yield return Shoot("story-reflection-core");
            yield return Until(() => GameObject.Find("SoulCard") != null, 90f, "her Soul Card");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shoot("story-reflection-card");
            yield return Until(() => FindObjectOfType<ReflectionSequence>() == null, 90f, "the reflection to end");
            yield return new WaitForSecondsRealtime(1f);
        }

        /// <summary>The ending, in the dragon's hall by the pedestal: the cage of light among the server racks, then
        /// Erin in the old machine. The run moves on after that (the next stop reloads the scene).</summary>
        IEnumerator Ending()
        {
            foreach (var flag in new[] { Chapter1.Route, Chapter1.Tunnel, Chapter1.DragonSeen, Chapter1.DragonBeaten, Chapter1.ResonanceArc })
                StoryState.Set(flag);
            WorldSession.UnlockResonance(Resonance.Arc);
            WorldSession.CurrentArea = WorldAreas.Chamber;
            WorldSession.PlayerSpot = null;
            WorldSession.HasReturnPoint = false;
            WorldSession.ArrivedFrom = null;
            SaveGame.Save();
            yield return Reload("the dragon's hall");
            var world = FindObjectOfType<OverworldBuilder>();
            var pedestal = world != null ? world.Map.FindAll('U') : null;
            if (pedestal != null && pedestal.Count > 0)
            {
                // Two squares in front of the pedestal, facing the hall.
                WorldSession.HasReturnPoint = true;
                WorldSession.ReturnPosition = world.Map.CellCenter(pedestal[0].x, pedestal[0].y + 2);
                WorldSession.ReturnFacing = CharacterSprite.Down;
                yield return Reload("by the pedestal");
                world = FindObjectOfType<OverworldBuilder>();
            }
            if (world == null) { Note("no world for the ending"); yield break; }
            ChapterOneDirector.ReplayEnding(world);
            yield return Until(() => GameObject.Find("Cage") != null, 90f, "the cage");
            yield return new WaitForSecondsRealtime(1.1f);   // the EMP flash has faded
            yield return Shoot("story-ending-cage");
            yield return Until(() => GameObject.Find("Machine") != null && GameObject.Find("LidTop") == null, 150f, "the machine room");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shoot("story-ending-machine");
        }

        /// <summary>The menus and the fight, on a City Ruins story save: after the Collector, Erin in the party (and
        /// Ashe along for the screenshots, so her Leng Storm can be shown).</summary>
        IEnumerator City()
        {
            foreach (var flag in new[] { Chapter1.Named, Chapter1.Awake, Chapter1.Scanner, Chapter1.Binder, Chapter1.CollectorMet, Chapter1.CollectorBeaten })
                StoryState.Set(flag);
            for (int rank = 1; rank <= Bonds.SoulRank; rank++) StoryState.Set("bond." + Bonds.Erin.Id + "." + rank);
            WorldSession.Bonds[Bonds.Erin.Character] = new WorldSession.BondProgress { Rank = Bonds.SoulRank };
            WorldSession.Recruit(Bonds.Erin.Character);
            WorldSession.Recruit("Char_Ashe");
            WorldSession.CurrentArea = WorldAreas.City;
            WorldSession.PlayerSpot = null;
            WorldSession.HasReturnPoint = false;
            WorldSession.ArrivedFrom = null;
            WorldSession.TitleDone = true;
            SaveGame.Save();
            yield return Reload("the city");
            var world = FindObjectOfType<OverworldBuilder>();

            // Out on the street, a few steps from a machine on patrol.
            var patrol = world != null ? world.FindEncounter(e => e != null && e.setup != null) : null;
            if (patrol != null)
            {
                WorldSession.HasReturnPoint = true;
                WorldSession.ReturnPosition = patrol.transform.position + new Vector3(-2.2f, 0f, -0.6f);
                WorldSession.ReturnFacing = CharacterSprite.Right;
                yield return Reload("the street");
                world = FindObjectOfType<OverworldBuilder>();
                yield return Shoot("10-city-ruins");
            }
            else Note("no patrol in the city");

            // Layout checks (not for the store): the dialogue faces.
            foreach (var who in new[] { "Erin", "Collector", "Thought" })
            {
                if (world == null) break;
                world.UI.Say(who, "Checking the face beside this line, nothing more.", DialogueVoice.Default);
                yield return new WaitForSecondsRealtime(1.4f);
                yield return Shoot("check-face-" + who.ToLowerInvariant());
                var box = OpenDialogue();
                if (box != null) { box.Advance(); box.Advance(); }
                yield return Until(() => OpenDialogue() == null, 5f, "the check line to close");
            }

            // Units: the member card glowing behind A3.
            var menu = Field<CharacterMenu>(world, "menu");
            if (menu != null)
            {
                menu.Open();
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Shoot("units-page");
                menu.Close();
                yield return new WaitForSecondsRealtime(1f);
            }

            // Esc: the pause cards; then its Settings page (a layout check, not for the store).
            var pause = Field<PauseCards>(world, "pauseCards");
            if (pause != null)
            {
                pause.Open();
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Shoot("03-pause-menu");
                var settings = typeof(PauseCards).GetMethod("ShowSettings", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
                if (settings != null)
                {
                    settings.Invoke(pause, new object[] { 0 });
                    yield return new WaitForSecondsRealtime(1.2f);
                    yield return Shoot("check-settings");
                }
                pause.Close();
                yield return new WaitForSecondsRealtime(1.5f);
                Time.timeScale = 1f;
            }

            // Prepare for Battle, then into the fight.
            var fight = world.FindEncounter(e => e != null && e.setup != null);
            if (fight == null) { Note("no encounter in the Sandbox"); yield break; }
            world.StartEncounter(fight);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shoot("04-pre-combat");
            var formation = Field<FormationScreen>(world, "formation");
            var close = typeof(FormationScreen).GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);
            if (formation != null && close != null) close.Invoke(formation, new object[] { true });
            else Note("couldn't start the fight from the pre-fight screen");
        }

        IEnumerator Battle()
        {
            yield return Until(() => FindObjectOfType<BattleView>() != null, 40f, "the battle");
            var view = FindObjectOfType<BattleView>();
            if (view == null) yield break;
            var c = view.Controller;
            yield return Until(() => view.Idle && c.State.Phase == BattlePhase.Planning && !ScreenFade.Busy, 30f, "planning");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shoot("combat-battle");

            // A hand card's details.
            if (c.State.Hand.Count > 0)
            {
                view.OpenCardDetail(view.ViewOf(c.State.Hand[0]));
                yield return new WaitForSecondsRealtime(1.6f);
                yield return Shoot("combat-card-detail");
                CardDetailPopup.CloseCurrent();
                yield return new WaitForSecondsRealtime(1f);
            }

            // Hold an enemy: the zoomed inspect sheet.
            var enemy = c.State.Enemies.Find(e => e.IsAlive);
            var views = Field<Dictionary<BattleUnit, UnitView>>(view, "unitViews");
            if (enemy != null && views != null && views.ContainsKey(enemy))
            {
                view.OpenInspect(views[enemy]);
                yield return new WaitForSecondsRealtime(1.6f);
                yield return Shoot("05-unit-inspect");
                var inspect = Field<UnitInspectView>(view, "inspect");
                if (inspect != null) inspect.Close();
                yield return new WaitForSecondsRealtime(1.2f);
            }

            // A merge: two of the same card, moved together.
            if (!MergePair(c)) Note("no merge pair in the hand");
            else
            {
                yield return new WaitForSecondsRealtime(0.24f);
                yield return Shoot("combat-card-merge");
            }
            yield return Until(() => view.Idle, 10f, "the merge");
            yield return new WaitForSecondsRealtime(0.6f);

            // Set up a BREAK: the target's Break at 1 and a card that hits its weakness first.
            CardInstance breaker = enemy != null ? c.State.Hand.Snapshot().Find(card => BreakRules.Matches(card.Definition, enemy) != Weakness.None) : null;
            if (breaker != null)
            {
                view.SetUpEnemy(enemy, enemy.HP, 1);
                c.SelectEnemy(enemy);
                c.QueueCard(breaker);
            }
            else Note("no weakness card for a BREAK");
            QueueAttacks(c);
            yield return Until(() => view.Idle, 10f, "queueing");
            view.OnGo();

            // The first blow: the camera pushed in and the damage number popping (the controller has already resolved
            // the turn, so this watches the view's animation, not the phase).
            var zoom = typeof(BattleView).GetField("camZoom", BindingFlags.Instance | BindingFlags.NonPublic);
            bool pushed = false;
            float until = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < until)
            {
                if (zoom != null && (float)zoom.GetValue(view) > 1.1f && view.FxLayer.Find("DamageNumber") != null) { pushed = true; break; }
                yield return null;
            }
            if (pushed)
            {
                yield return new WaitForSecondsRealtime(0.08f);
                yield return Shoot("combat-turn-playing-out");
            }
            else Note("no camera push-in");

            until = Time.realtimeSinceStartup + 40f;
            bool broke = false;
            while (Time.realtimeSinceStartup < until && !(view.Idle && c.State.Phase == BattlePhase.Planning))
            {
                if (view.FxLayer != null && view.FxLayer.Find("Break") != null) { broke = true; break; }
                yield return null;
            }
            if (broke)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shoot("combat-break");
            }
            else Note("no BREAK this turn");
            yield return Until(() => (view.Idle && c.State.Phase == BattlePhase.Planning) || (c.State.Phase == BattlePhase.Victory || c.State.Phase == BattlePhase.Defeat), 90f, "the next turn");

            // Ashe's Leng Storm, if she's here and it's in her deck.
            if (c.State.Phase == BattlePhase.Planning) yield return LengStorm(view, c);

            yield return new WaitForSecondsRealtime(1f);
            view.LeaveFight();
            yield return new WaitForSecondsRealtime(1f);
            yield return Until(() => FindObjectOfType<OverworldBuilder>() != null && !ScreenFade.Busy, 60f, "back in the city");
        }

        IEnumerator LengStorm(BattleView view, BattleController c)
        {
            var ashe = c.State.Party.Find(u => u.Definition != null && u.Definition.name == "Char_Ashe" && u.IsAlive);
            CardDefinition def = null;
            var member = ashe != null ? ashe.Definition as CharacterDefinition : null;
            if (member != null) def = member.AllIncantations().Find(d => d != null && d.name.Contains("LengStorm"));
            if (def == null)   // a resonance kit's card: anything loaded
                foreach (var d in Resources.FindObjectsOfTypeAll<CardDefinition>())
                    if (d.name.Contains("LengStorm")) { def = d; break; }
            if (def == null) { Note("no Ashe / Leng Storm in this fight"); yield break; }
            var hand = c.State.Hand.Snapshot();
            var storm = new CardInstance(def, ashe, 1);
            hand.Insert(0, storm);
            c.SetHand(hand);
            yield return Until(() => view.Idle, 10f, "the new hand");
            c.QueueCard(storm);
            yield return Until(() => view.Idle, 10f, "queueing");
            view.OnGo();
            string channel = "Channel_" + storm.Name;
            yield return Until(() => view.FxLayer.Find(channel) != null, 20f, "the Leng Storm channel");
            yield return Until(() => view.FxLayer.Find(channel) == null, 10f, "the Leng Storm cast");
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Shoot("ashe-leng-storm");
            yield return Until(() => (view.Idle && c.State.Phase == BattlePhase.Planning) || (c.State.Phase == BattlePhase.Victory || c.State.Phase == BattlePhase.Defeat), 90f, "the turn");
        }

        /// <summary>An area's overworld at its start, once its title has faded.</summary>
        IEnumerator Area(string id, string shot)
        {
            WorldSession.CurrentArea = id;
            WorldSession.PlayerSpot = null;
            WorldSession.HasReturnPoint = false;
            WorldSession.ArrivedFrom = null;
            yield return Reload(id);
            yield return Shoot(shot);
        }

        /// <summary>Reloads the overworld scene into WorldSession's area and waits until it has settled (title gone, no
        /// dialogue open).</summary>
        IEnumerator Reload(string what)
        {
            Time.timeScale = 1f;
            ScreenFade.LoadScene(SceneManager.GetActiveScene().name);
            yield return new WaitForSecondsRealtime(1f);
            yield return Until(() => FindObjectOfType<OverworldBuilder>() != null && !ScreenFade.Busy, 60f, what);
            yield return new WaitForSecondsRealtime(6f);   // the area title fades
            yield return Until(() => OpenDialogue() == null, 30f, what + "'s lines");
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // ------------------------------------------------------------------ Battle helpers

        /// <summary>Moves one of two matching cards (same card, owner and rank) next to the other, which merges them.</summary>
        static bool MergePair(BattleController c)
        {
            var hand = c.State.Hand;
            for (int i = 0; i < hand.Count; i++)
                for (int j = i + 2; j < hand.Count; j++)
                    if (hand[i].Definition == hand[j].Definition && hand[i].Owner == hand[j].Owner && hand[i].Rank == hand[j].Rank && !hand[i].IsRitual)
                        return c.MoveCard(hand[j], i + 1);
            return false;
        }

        /// <summary>Queues attack cards (then anything) until the AP run out.</summary>
        static void QueueAttacks(BattleController c)
        {
            for (int pass = 0; pass < 2; pass++)
                for (int guard = 0; guard < 12 && c.State.ActionPoints > 0; guard++)
                {
                    var card = c.State.Hand.Snapshot().Find(k => pass == 1 || k.Definition.kind == CardKind.Attack);
                    if (card == null || !c.QueueCard(card)) break;
                }
        }

        // ------------------------------------------------------------------ Helpers

        static DialogueBox OpenDialogue()
        {
            foreach (var box in FindObjectsOfType<DialogueBox>())
                if (box.IsOpen) return box;
            return null;
        }

        static T Field<T>(object owner, string name) where T : class
        {
            if (owner == null) return null;
            var f = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f != null ? f.GetValue(owner) as T : null;
        }

        IEnumerator Until(Func<bool> condition, float timeout, string what)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < end)
            {
                bool ok;
                try { ok = condition(); }
                catch (Exception) { ok = false; }
                if (ok) yield break;
                yield return null;
            }
            Note("gave up waiting for " + what);
        }

        IEnumerator Shoot(string name)
        {
            yield return new WaitForEndOfFrame();
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            string size = shot.width + "x" + shot.height;
            var rt = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(shot, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var output = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            output.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            output.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            string path = Path.Combine(outDir, name + ".png");
            File.WriteAllBytes(path, output.EncodeToPNG());
            Destroy(shot);
            Destroy(output);
            Note("shot " + name + " (" + size + " screen)");
        }
    }
}
