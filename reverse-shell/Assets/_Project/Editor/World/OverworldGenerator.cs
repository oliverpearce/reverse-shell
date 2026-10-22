using System.Linq;
using Combat;
using Combat.EditorTools;
using Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if NEONHEX_POSTFX
using UnityEngine.Rendering.PostProcessing;
#endif

namespace Game.EditorTools
{
    /// <summary>
    /// Tools > Overworld > Generate Rainline Station: builds the HD-2D overworld scene (camera, post
    /// processing, OverworldBuilder wired to the CyberCity art and the sample battle) and puts it first
    /// in Build Settings so Play starts there.
    /// </summary>
    public static class OverworldGenerator
    {
        const string ArtRoot = "Assets/_Project/Art/ThirdParty/CyberCity/";
        const string ScenePath = "Assets/_Project/Scenes/Overworld_Rainline.unity";
        const string BattleScenePath = "Assets/_Project/Scenes/Battle.unity";
        const string BattleSetupPath = "Assets/_Project/Content/Battle_Sample.asset";
        const string ProfilePath = "Assets/_Project/Content/PostFX_Overworld.asset";

        [MenuItem("Tools/Overworld/Generate Rainline Station")]
        public static void Generate()
        {
            var setup = AssetDatabase.LoadAssetAtPath<BattleSetup>(BattleSetupPath);
            if (setup == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(BattleScenePath) == null)
            {
                SampleContentGenerator.Generate();
                setup = AssetDatabase.LoadAssetAtPath<BattleSetup>(BattleSetupPath);
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Make sure the pixel-art import settings are applied even if the textures arrived first.
            foreach (var file in new[] { "city.png", "lady.png", "Yakuza1.png", "YakuzaBoss.png", "streetPeople.png" })
                AssetDatabase.ImportAsset(ArtRoot + file, ImportAssetOptions.ForceUpdate);
            foreach (var file in new[] { "Miku.png", "Teto.png", "Rei.png" })
                AssetDatabase.ImportAsset("Assets/_Project/Art/Characters/" + file, ImportAssetOptions.ForceUpdate);

            var atlas = Load("city.png");
            if (atlas == null)
            {
                EditorUtility.DisplayDialog("Overworld", "Missing art in " + ArtRoot + ". See CREDITS.md there for the source.", "OK");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.renderingPath = RenderingPath.Forward; // forward keeps fog on opaque geometry
            cam.allowHDR = true;
            cam.allowMSAA = false;
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 80f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<OverworldCamera>();
            var focus = camGo.AddComponent<PostFxFocus>();

            var world = new GameObject("Overworld").AddComponent<OverworldBuilder>();
            world.cityAtlas = atlas;
            world.playerSheet = null; // uses the party leader's sheet (Miku) from the battle setup
            world.enemySheet = Load("Yakuza1.png");
            world.npcSheet = Load("streetPeople.png");
            // LPC animated look (falls back to the CyberCity sheets above if SpriteForge output is missing).
            world.npcAnimations = new System.Collections.Generic.List<SpriteAnimationSet>
            {
                SampleContentGenerator.AnimSet("NoodleVendor"),
                SampleContentGenerator.AnimSet("Courier"),
                SampleContentGenerator.AnimSet("Drifter"),
            };
            world.npcAnimations.RemoveAll(a => a == null);
            world.encounterAnimations = SampleContentGenerator.AnimSet("StaticThug");
            world.encounterSetup = setup;
            world.battleScene = "Battle";

            bool postFx = SetUpPostProcessing(camGo, focus);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

            EditorUtility.DisplayDialog("Overworld",
                "Rainline Station generated:\n" + ScenePath +
                (postFx ? "\n\nPost processing (bloom, depth of field, grain) is on." :
                    "\n\nPost Processing package not found yet - let Unity finish importing it, then run this again for bloom + depth of field.") +
                "\n\nPress Play, walk to the glowing square in the plaza, and press F.", "OK");
        }

        const string PlayFromOverworldMenu = "Tools/Overworld/Always Play From Overworld";

        /// <summary>
        /// The editor's Play button normally runs whatever scene is open. When this is on, Play always
        /// starts in the overworld (like a real build), no matter which scene you're editing.
        /// </summary>
        [MenuItem(PlayFromOverworldMenu)]
        static void TogglePlayFromOverworld()
        {
            var overworld = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (overworld == null)
            {
                EditorUtility.DisplayDialog("Overworld", "Generate the overworld first (Tools > Overworld > Generate Rainline Station).", "OK");
                return;
            }
            EditorSceneManager.playModeStartScene = EditorSceneManager.playModeStartScene == overworld ? null : overworld;
        }

        [MenuItem(PlayFromOverworldMenu, true)]
        static bool TogglePlayFromOverworldValidate()
        {
            var overworld = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Menu.SetChecked(PlayFromOverworldMenu, overworld != null && EditorSceneManager.playModeStartScene == overworld);
            return true;
        }

        static Texture2D Load(string file)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + file);
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != BattleScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            scenes.Insert(1, new EditorBuildSettingsScene(BattleScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static bool SetUpPostProcessing(GameObject camGo, PostFxFocus focus)
        {
#if NEONHEX_POSTFX
            var resources = AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
            if (resources == null) return false;

            var layer = camGo.AddComponent<PostProcessLayer>();
            layer.Init(resources);
            layer.volumeLayer = 1; // Default layer
            layer.antialiasingMode = PostProcessLayer.Antialiasing.None;

            var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PostProcessProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile.settings.Clear();
            foreach (var sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(ProfilePath))
                Object.DestroyImmediate(sub, true);

            var bloom = Add<Bloom>(profile);
            bloom.intensity.Override(3.5f);
            bloom.threshold.Override(0.95f);
            bloom.softKnee.Override(0.6f);
            bloom.diffusion.Override(7.5f);

            var dof = Add<DepthOfField>(profile);
            dof.focusDistance.Override(15f);
            dof.aperture.Override(2.4f);
            dof.focalLength.Override(65f);
            dof.kernelSize.Override(KernelSize.Medium);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.06f, 0f, 0.1f));

            var grain = Add<Grain>(profile);
            grain.intensity.Override(0.22f);
            grain.size.Override(0.9f);

            var aberration = Add<ChromaticAberration>(profile);
            aberration.intensity.Override(0.12f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var volumeGo = new GameObject("PostFX Volume");
            var volume = volumeGo.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            focus.volume = volume;
            return true;
#else
            return false;
#endif
        }

#if NEONHEX_POSTFX
        static T Add<T>(PostProcessProfile profile) where T : PostProcessEffectSettings
        {
            var settings = profile.AddSettings<T>();
            settings.enabled.Override(true);
            settings.name = typeof(T).Name;
            settings.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(settings, profile);
            return settings;
        }
#endif
    }
}
