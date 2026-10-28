using UnityEditor;

namespace Combat.EditorTools
{
    /// <summary>
    /// Creates content the project needs the first time the editor loads without it: Blue (SampleContentGenerator.EnsureBlue)
    /// and Chapter One's enemies, kits and bestiary (SampleContentGenerator.EnsureChapterOne).
    /// </summary>
    [InitializeOnLoad]
    static class BlueContentBootstrap
    {
        static BlueContentBootstrap()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<CharacterDefinition>(SampleContentGenerator.BluePath) == null
                    && AssetDatabase.IsValidFolder("Assets/_Project/Art/Characters/Blue"))
                {
                    SampleContentGenerator.EnsureBlue();
                    UnityEngine.Debug.Log("Created Blue (the backup robot) and added Blue to Battle_Sample's roster.");
                }
                var bestiary = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(SampleContentGenerator.BestiaryPath);
                if (bestiary == null || bestiary.contentVersion < SampleContentGenerator.ChapterOneVersion)
                {
                    SampleContentGenerator.EnsureChapterOne();
                    UnityEngine.Debug.Log("Added any missing Chapter One content v" + SampleContentGenerator.ChapterOneVersion
                        + " (resonance kits, enemies, Resources/Bestiary.asset). Existing assets were left as they are.");
                }
                if (SampleContentGenerator.EnsurePackRates())
                    UnityEngine.Debug.Log("Created Resources/Cards/PackRates.asset (card pack odds, copies, bindings, Soul levels). Tune it in the Inspector.");
                int sits = SampleContentGenerator.EnsureSitClips();
                if (sits > 0) UnityEngine.Debug.Log("Added the sit / read clips (the Esc menu's kneel, the Cards book) to " + sits + " animation sets.");
            };
        }
    }
}
