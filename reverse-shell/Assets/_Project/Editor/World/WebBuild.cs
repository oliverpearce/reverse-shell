using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the itch.io web demo into builds/webgl at the repo root (a release build: no Development Build label).
    /// The WebGL settings and shaders come from <see cref="WebBuildSetup"/>. Zip the folder's contents (index.html at the
    /// zip's root) for itch.
    ///   Menu: Reverse Shell / Build Web Demo
    ///   Batch: Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod Game.EditorTools.WebBuild.Build
    /// </summary>
    static class WebBuild
    {
        static string Output { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "builds", "webgl")); } }

        [MenuItem("Reverse Shell/Build Web Demo")]
        public static void Build()
        {
            EditorApplication.ExecuteMenuItem("Reverse Shell/Web Build Setup");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(scenes, Output, BuildTarget.WebGL, BuildOptions.None);
            Debug.Log("Web build: " + report.summary.result + " (" + report.summary.totalSize / (1024 * 1024) + " MB) in " + Output);
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
