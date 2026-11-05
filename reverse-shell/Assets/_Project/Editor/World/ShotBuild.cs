using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the development Windows player used for the screenshot run (Game.World.ShotTour) into builds/shots at the
    /// repo root (git-ignored), then puts the editor back on the platform it was on. Run the player with -shots:
    ///   builds/shots/ReverseShell.exe -shots -shotsOut "itch/screenshots/new" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
    ///   Menu: Reverse Shell / Build Screenshot Player
    ///   Batch: Unity -batchmode -quit -projectPath . -executeMethod Game.EditorTools.ShotBuild.Build
    /// </summary>
    static class ShotBuild
    {
        static string Output { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "builds", "shots", "ReverseShell.exe")); } }

        [MenuItem("Reverse Shell/Build Screenshot Player")]
        public static void Build()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(scenes, Output, BuildTarget.StandaloneWindows64, BuildOptions.Development);
            Debug.Log("Screenshot player: " + report.summary.result + " in " + Output);
            if (EditorUserBuildSettings.activeBuildTarget != target) EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
