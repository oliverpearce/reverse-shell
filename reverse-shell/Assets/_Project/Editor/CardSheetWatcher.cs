using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Combat.EditorTools
{
    /// <summary>
    /// Watches the card sheet (design/CARDS.xlsx at the repo root). When it's saved while Unity is open, this runs
    /// Tools/CardForge/sheet_to_json.py and then the card import (<see cref="CardImporter"/>), so a sheet edit reaches
    /// the game with no sync.bat and no menu. It waits until the file has stopped changing for a couple of seconds,
    /// and never imports in Play mode (the import runs on the way back to edit mode).
    /// The first time it sees the sheet on this computer it only remembers it; it reacts to the saves after that.
    ///   Menu: Reverse Shell / Convert Card Sheet Now
    /// </summary>
    [InitializeOnLoad]
    public static class CardSheetWatcher
    {
        const double CheckEvery = 2.0;

        static readonly string ProjectDir = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
        static readonly string SheetPath = Path.GetFullPath(Path.Combine(ProjectDir, "..", "design", "CARDS.xlsx"));
        static string StampKey { get { return "ReverseShell.CardsSheetStamp." + UnityEngine.Application.dataPath; } }

        static double nextCheck;
        static long lastSeen = -1;
        static Process running;
        static long runningStamp;
        static readonly StringBuilder output = new StringBuilder();
        static volatile bool finished;

        static CardSheetWatcher()
        {
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (running != null)
            {
                if (finished) Finish();
                return;
            }
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + CheckEvery;
            if (EditorApplication.isCompiling || !File.Exists(SheetPath)) return;
            long stamp = File.GetLastWriteTimeUtc(SheetPath).Ticks;
            string known = EditorPrefs.GetString(StampKey, "");
            if (known == "") { EditorPrefs.SetString(StampKey, stamp.ToString()); return; }   // first sight: just remember it
            if (known == stamp.ToString()) return;
            // A save in progress: wait for the same time on two checks in a row.
            if (stamp != lastSeen) { lastSeen = stamp; return; }
            Convert(stamp);
        }

        [MenuItem("Reverse Shell/Convert Card Sheet Now")]
        static void ConvertNow()
        {
            if (running != null) { Debug.Log("Card sheet: already converting."); return; }
            if (!File.Exists(SheetPath)) { Debug.LogError("Card sheet: no sheet at " + SheetPath); return; }
            Convert(File.GetLastWriteTimeUtc(SheetPath).Ticks);
        }

        static void Convert(long stamp)
        {
            output.Length = 0;
            finished = false;
            runningStamp = stamp;
            running = Start("py") ?? Start("python");
            if (running == null)
            {
                EditorPrefs.SetString(StampKey, stamp.ToString());   // don't retry every two seconds
                Debug.LogError("Card sheet: couldn't run Python (tried py and python). Run design/sync.bat instead.");
            }
        }

        static Process Start(string python)
        {
            var info = new ProcessStartInfo(python, "\"" + Path.Combine("Tools", "CardForge", "sheet_to_json.py") + "\"")
            {
                WorkingDirectory = ProjectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            info.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            try
            {
                var p = new Process { StartInfo = info, EnableRaisingEvents = true };
                DataReceivedEventHandler collect = (s, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
                p.OutputDataReceived += collect;
                p.ErrorDataReceived += collect;
                p.Exited += (s, e) => finished = true;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                return p;
            }
            catch (Exception)
            {
                return null;   // not installed / not on the PATH
            }
        }

        static void Finish()
        {
            var p = running;
            running = null;
            p.WaitForExit();   // the last output lines
            int code = p.ExitCode;
            p.Dispose();
            string log;
            lock (output) log = output.ToString().Trim();
            EditorPrefs.SetString(StampKey, runningStamp.ToString());   // a failed one waits for the next save
            if (code != 0)
            {
                string hint = log.Contains("PermissionError") ? "\n(Excel may be holding the file: save it again, or close it.)" : "";
                Debug.LogError("Card sheet: not imported; fix the sheet and save again.\n" + log + hint);
                return;
            }
            Debug.Log("Card sheet converted.\n" + log);
            CardImporter.ImportIfChanged();
        }
    }
}
