using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Combat;
using Combat.View;
using Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Reverse Shell / Bug Sweep: one pass over everything a release build needs, run before each build. It checks:
    ///   - every area (the Level Designer's validator, plus encounter enemies in the Bestiary, story flags, win blocks,
    ///     warp destinations and NPC story blocks);
    ///   - every BattleSetup (empty party or enemy slots, no backdrop);
    ///   - every text key the scripts ask for by name (UIText.Get / StoryText.* with a literal key) exists in
    ///     Resources/Story;
    ///   - every scene in the build for missing scripts;
    ///   - anything logged as an error or warning while it runs.
    /// The report goes to the Console and to design/BUG_SWEEP.txt at the repo root (git-ignored).
    ///   Batch: Unity -batchmode -quit -projectPath . -executeMethod Game.EditorTools.BugSweep.RunBatch (exit 1 on errors)
    /// </summary>
    public static class BugSweep
    {
        class Report
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<string> Info = new List<string>();
            public void Error(string s) { Errors.Add(s); }
            public void Warn(string s) { Warnings.Add(s); }
        }

        [MenuItem("Reverse Shell/Bug Sweep")]
        public static void RunMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var report = Run();
            EditorUtility.DisplayDialog("Bug Sweep",
                report.Errors.Count + " error(s), " + report.Warnings.Count + " warning(s).\n\nThe full list is in the Console and in design/BUG_SWEEP.txt.",
                "OK");
        }

        public static void RunBatch()
        {
            var report = Run();
            EditorApplication.Exit(report.Errors.Count > 0 ? 1 : 0);
        }

        static Report Run()
        {
            var report = new Report();
            Application.LogCallback log = (message, stack, type) =>
            {
                if (message.StartsWith("Bug Sweep")) return;
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.Error("Logged during the sweep: " + message);
                else if (type == LogType.Warning) report.Warn("Logged during the sweep: " + message);
            };
            Application.logMessageReceived += log;
            try
            {
                if (EditorUtility.scriptCompilationFailed) report.Error("The scripts don't compile (see the Console).");
                UIText.Reload();
                StoryText.Reload();
                WorldAreas.Reload();
                CheckAreas(report);
                CheckBattleSetups(report);
                CheckTextKeys(report);
                CheckScenes(report);
            }
            finally
            {
                Application.logMessageReceived -= log;
            }
            Write(report);
            return report;
        }

        // ------------------------------------------------------------------ Areas

        static void CheckAreas(Report report)
        {
            var areas = new List<AreaAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:AreaAsset"))
            {
                var a = AssetDatabase.LoadAssetAtPath<AreaAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) areas.Add(a);
            }
            var ids = new HashSet<string>();
            foreach (var a in areas) ids.Add(a.id);
            var flags = new HashSet<string>(Chapter1.All);
            var bestiary = EnemyCatalog.Get();
            if (bestiary == null) report.Error("No Resources/Bestiary.asset: run Tools > Combat > Generate Sample Content.");

            foreach (var area in areas)
            {
                string where = "Area '" + area.id + "' (" + area.name + ")";
                foreach (var p in LevelValidator.Check(area, areas))
                {
                    string text = where + ": " + p.Text + (p.Cell.HasValue ? " [cell " + p.Cell.Value.x + ", row " + p.Cell.Value.y + "]" : "");
                    if (p.Error) report.Error(text);
                    else report.Warn(text);
                }
                foreach (var warp in area.warps)
                    if (warp != null && !string.IsNullOrEmpty(warp.to) && !ids.Contains(WorldAreas.Migrate(warp.to)))
                        report.Error(where + ": warp '" + warp.tile + "' leads to '" + warp.to + "', which isn't an area.");
                for (int i = 0; i < area.encounters.Count; i++)
                {
                    var fight = area.encounters[i] != null ? area.encounters[i].Clean() : null;
                    if (fight == null) continue;
                    string f = where + ", fight " + (i + 1);
                    if (fight.Enemies == null || fight.Enemies.Length == 0) report.Error(f + ": no enemies.");
                    else if (bestiary != null)
                        foreach (var e in fight.Enemies)
                            if (bestiary.Find(e) == null) report.Error(f + ": enemy '" + e + "' isn't in the Bestiary.");
                    CheckFlags(report, f, fight.Requires, flags);
                    CheckFlags(report, f, fight.Blocks, flags);
                    if (fight.WinFlag != null && !flags.Contains(fight.WinFlag) && !fight.WinFlag.StartsWith("bond."))
                        report.Warn(f + ": win flag '" + fight.WinFlag + "' isn't a Chapter One flag.");
                    if (fight.WinKey != null && !StoryText.Has(fight.WinKey))
                        report.Error(f + ": its win block [" + fight.WinKey + "] isn't in Resources/Story.");
                }
                foreach (var npc in area.npcs)
                    if (npc != null && !string.IsNullOrEmpty(npc.LinesKey) && !StoryText.Has(npc.LinesKey))
                        report.Error(where + ": NPC '" + npc.Name + "' says [" + npc.LinesKey + "], which isn't in Resources/Story.");
            }
            report.Info.Add(areas.Count + " areas checked.");
        }

        static void CheckFlags(Report report, string where, string[] list, HashSet<string> flags)
        {
            if (list == null) return;
            foreach (var flag in list)
                if (!string.IsNullOrEmpty(flag) && !flags.Contains(flag) && !flag.StartsWith("bond.") && !flag.StartsWith("lost."))
                    report.Warn(where + ": flag '" + flag + "' isn't a Chapter One flag (typo?).");
        }

        // ------------------------------------------------------------------ Battle setups

        static void CheckBattleSetups(Report report)
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:BattleSetup"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var setup = AssetDatabase.LoadAssetAtPath<BattleSetup>(path);
                if (setup == null) continue;
                count++;
                string where = "BattleSetup " + path;
                if (setup.party.Count == 0) report.Warn(where + ": no party (fine if the overworld always fills it).");
                if (setup.party.Exists(p => p == null)) report.Error(where + ": an empty party slot.");
                if (setup.enemies.Exists(e => e == null)) report.Error(where + ": an empty enemy slot.");
                if (setup.backdrop == null) report.Warn(where + ": no backdrop (plain background).");
                foreach (var member in setup.party)
                    if (member != null && member.Anim == null) report.Warn(where + ": " + member.name + " has no animations.");
            }
            report.Info.Add(count + " battle setups checked.");
        }

        // ------------------------------------------------------------------ Text keys

        // Only whole literal keys: the literal must be followed by "," or ")" (a key built with "+" is skipped).
        static readonly Regex UiKey = new Regex("UIText\\.(?:Get|Has)\\(\\s*\"([^\"]+)\"\\s*[,)]");
        static readonly Regex StoryKey = new Regex("StoryText\\.(?:Texts|Lines|Text|Speaker|Get|Has)\\(\\s*\"([^\"]+)\"\\s*[,)]");
        static readonly Regex ScriptKey = new Regex("\\b(?:Script|First)\\(\\s*\"([^\"]+)\"\\s*[,)]");

        static void CheckTextKeys(Report report)
        {
            int ui = 0, story = 0;
            string scripts = Path.Combine(Application.dataPath, "_Project", "Scripts");
            foreach (var file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string code = File.ReadAllText(file);
                string name = Path.GetFileName(file);
                foreach (Match m in UiKey.Matches(code))
                {
                    ui++;
                    if (!UIText.Has(m.Groups[1].Value)) report.Error(name + ": UI text key '" + m.Groups[1].Value + "' is missing from UI.txt.");
                }
                foreach (Match m in StoryKey.Matches(code))
                {
                    story++;
                    if (!StoryText.Has(m.Groups[1].Value)) report.Error(name + ": story block [" + m.Groups[1].Value + "] is missing from Resources/Story.");
                }
                // Script("...") / First("...") read story blocks in the story scripts (PrologueSequence, ChapterOneDirector).
                if (file.Replace('\\', '/').Contains("/Story/"))
                    foreach (Match m in ScriptKey.Matches(code))
                    {
                        story++;
                        if (!StoryText.Has(m.Groups[1].Value)) report.Error(name + ": story block [" + m.Groups[1].Value + "] is missing from Resources/Story.");
                    }
            }
            report.Info.Add(ui + " UI text lookups and " + story + " story lookups checked (keys built at runtime are skipped).");
        }

        // ------------------------------------------------------------------ Scenes

        static void CheckScenes(Report report)
        {
            var open = new HashSet<string>();
            for (int i = 0; i < EditorSceneManager.sceneCount; i++) open.Add(EditorSceneManager.GetSceneAt(i).path);
            int count = 0;
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled) continue;
                count++;
                bool wasOpen = open.Contains(entry.path);
                var scene = wasOpen ? EditorSceneManager.GetSceneByPath(entry.path)
                    : EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                int missing = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missing > 0) report.Error("Scene " + entry.path + ": " + missing + " missing script(s).");
                if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
            }
            if (count == 0) report.Error("No scenes are enabled in the build settings.");
            report.Info.Add(count + " build scenes checked for missing scripts.");
        }

        // ------------------------------------------------------------------ Report

        static void Write(Report report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Bug Sweep, " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ": " + report.Errors.Count + " error(s), " + report.Warnings.Count + " warning(s)");
            foreach (var s in report.Info) sb.AppendLine("  " + s);
            sb.AppendLine();
            sb.AppendLine("ERRORS");
            foreach (var s in report.Errors) sb.AppendLine("  - " + s);
            if (report.Errors.Count == 0) sb.AppendLine("  (none)");
            sb.AppendLine();
            sb.AppendLine("WARNINGS");
            foreach (var s in report.Warnings) sb.AppendLine("  - " + s);
            if (report.Warnings.Count == 0) sb.AppendLine("  (none)");
            string text = sb.ToString();

            string design = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "design"));
            try
            {
                Directory.CreateDirectory(design);
                File.WriteAllText(Path.Combine(design, "BUG_SWEEP.txt"), text);
            }
            catch (IOException e)
            {
                Debug.LogWarning("Bug Sweep: couldn't write design/BUG_SWEEP.txt: " + e.Message);
            }
            if (report.Errors.Count > 0) Debug.LogError("Bug Sweep found problems:\n" + text);
            else Debug.Log("Bug Sweep: all clear.\n" + text);
        }
    }
}
