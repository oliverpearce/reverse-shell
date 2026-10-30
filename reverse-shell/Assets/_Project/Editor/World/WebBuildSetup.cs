using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Readies the itch.io web (WebGL) build. Reverse Shell / Web Build Setup adds every shader the game makes materials
    /// from at runtime (Shader.Find) to Project Settings / Graphics / Always Included Shaders, so web builds don't strip
    /// them (pink or invisible effects), and sets the WebGL player settings (960 x 540 canvas, 256 MB memory, Gzip,
    /// explicitly thrown exceptions, not running in the background). Every WebGL build adds the shaders by itself too.
    /// </summary>
    class WebBuildSetup : IPreprocessBuildWithReport
    {
        /// <summary>Everything the scripts pass to Shader.Find (WorldArt.FindShader, UnitView, the UI).</summary>
        static readonly string[] RuntimeShaders =
        {
            "Standard",
            "Sprites/Default",
            "UI/Default",
            "Unlit/Transparent",
            "Legacy Shaders/Particles/Additive",
            "Legacy Shaders/Particles/Alpha Blended",
            "Legacy Shaders/Transparent/Cutout/Diffuse",
            "Reverse Shell/WorldToon",
            "Reverse Shell/Lava",
            "Reverse Shell/Silhouette",
        };

        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.WebGL) AddShaders();
        }

        [MenuItem("Reverse Shell/Web Build Setup")]
        static void Setup()
        {
            int added = AddShaders();
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 540;
            PlayerSettings.runInBackground = false;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            AssetDatabase.SaveAssets();
            Debug.Log("Web Build Setup: " + added + " shader(s) added to Always Included Shaders; WebGL player settings set " +
                      "(960 x 540, 256 MB, Gzip). If itch shows a decompression error, set Compression to Disabled.");
        }

        /// <summary>Adds the missing runtime shaders to Always Included Shaders. Returns how many it added.</summary>
        static int AddShaders()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return 0;
            var settings = assets[0];
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            int added = 0;
            foreach (var name in RuntimeShaders)
            {
                var shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogWarning("Web Build Setup: shader not found: " + name);
                    continue;
                }
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                added++;
            }
            if (added > 0) so.ApplyModifiedProperties();
            return added;
        }
    }
}
