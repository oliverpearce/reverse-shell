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
    /// Tools > Overworld > Generate Mossline Station: builds the HD-2D overworld scene (camera, post
    /// processing, OverworldBuilder wired to the ruins atlas and the sample battle) and puts it first
    /// in Build Settings so Play starts there.
    /// </summary>
    public static class OverworldGenerator
    {
        const string ArtRoot = "Assets/_Project/Art/ThirdParty/CyberCity/";
        const string TilesPath = "Assets/_Project/Art/Tiles/ruins.png";
        const string ScenePath = "Assets/_Project/Scenes/Overworld_Mossline.unity";
        const string LegacyScenePath = "Assets/_Project/Scenes/Overworld_Rainline.unity";
        const string BattleScenePath = "Assets/_Project/Scenes/Battle.unity";
        const string BattleSetupPath = "Assets/_Project/Content/Battle_Sample.asset";
        const string ProfilePath = "Assets/_Project/Content/PostFX_Overworld.asset";

        [MenuItem("Tools/Overworld/Generate Mossline Station")]
        public static void Generate()
        {
            var setup = AssetDatabase.LoadAssetAtPath<BattleSetup>(BattleSetupPath);
            if (setup == null || AssetDatabase.LoadAssetAtPath<SceneAsset>(BattleScenePath) == null)
            {
                SampleContentGenerator.Generate();
                setup = AssetDatabase.LoadAssetAtPath<BattleSetup>(BattleSetupPath);
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null && !EditorUtility.DisplayDialog("Overworld",
                    "Rebuild " + ScenePath + "?\n\nChanges you made to its objects in the Inspector (camera, Overworld "
                    + "builder settings) will be replaced. Maps, story and content assets are not affected.", "Rebuild", "Cancel"))
                return;

            // Make sure the pixel-art import settings are applied even if the textures arrived first.
            foreach (var file in new[] { "Yakuza1.png", "streetPeople.png" })
                AssetDatabase.ImportAsset(ArtRoot + file, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TilesPath, ImportAssetOptions.ForceUpdate);

            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesPath);
            if (atlas == null)
            {
                EditorUtility.DisplayDialog("Overworld", "Missing " + TilesPath + ". Run: python Tools/TileForge/ruins_atlas.py", "OK");
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
            world.tileAtlas = atlas;
            world.playerSheet = null; // uses the party leader's sprite (A3) from the battle setup
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
            world.encounterAnimations = SampleContentGenerator.AnimSet("RustSentry");
            world.encounterSetup = setup;
            world.battleScene = "Battle";
            world.partCatalog = AssetDatabase.LoadAssetAtPath<PartCatalog>(Combat.EditorTools.SampleContentGenerator.PartCatalogPath);

            bool postFx = SetUpPostProcessing(camGo, focus);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);

            EditorUtility.DisplayDialog("Overworld",
                "Mossline Station generated:\n" + ScenePath +
                (postFx ? "\n\nPost processing (bloom, depth of field, color grading) is on." :
                    "\n\nPost Processing package not found yet - let Unity finish importing it, then run this again for bloom + depth of field.") +
                "\n\nPress Play, walk to the red square in the plaza, and press F.", "OK");
        }

        /// <summary>Wipes every save slot (party levels, XP, HP and beaten encounters) so the next Play starts fresh.</summary>
        [MenuItem("Tools/Overworld/Delete Save Data")]
        static void DeleteSaveData()
        {
            if (!EditorUtility.DisplayDialog("Overworld", "Delete all " + SaveGame.SlotCount + " save slots?\n" + Application.persistentDataPath +
                    "\n\nParty levels, XP, HP and beaten encounters will reset.", "Delete", "Cancel"))
                return;
            SaveGame.DeleteAll();
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
                EditorUtility.DisplayDialog("Overworld", "Generate the overworld first (Tools > Overworld > Generate Mossline Station).", "OK");
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
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && s.path != BattleScenePath && s.path != LegacyScenePath).ToList();
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

            // An existing profile is kept as it is, so your tweaks to bloom, grading and vignette survive.
            var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PostProcessProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                FillProfile(profile);
            }

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
        static void FillProfile(PostProcessProfile profile)
        {
            var bloom = Add<Bloom>(profile);
            // Only true light sources bloom (threshold well above sunlit sprites), so characters stay sharp.
            bloom.intensity.Override(1f);
            bloom.threshold.Override(1.6f);
            bloom.softKnee.Override(0.3f);
            bloom.diffusion.Override(5f);

            // Washed-out, slightly warm and desaturated: a quiet ruin in hazy sunlight.
            var grading = Add<ColorGrading>(profile);
            grading.gradingMode.Override(GradingMode.HighDefinitionRange);
            grading.tonemapper.Override(Tonemapper.Neutral);
            grading.temperature.Override(8f);
            grading.saturation.Override(-12f);
            grading.contrast.Override(6f);
            grading.postExposure.Override(0f);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.12f, 0.13f, 0.08f));

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

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
