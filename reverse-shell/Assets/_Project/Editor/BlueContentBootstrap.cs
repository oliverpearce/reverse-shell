using UnityEditor;

namespace Combat.EditorTools
{
    /// <summary>
    /// Creates content the project needs the first time the editor loads without it: Chapter One's enemies, kits and
    /// bestiary (SampleContentGenerator.EnsureChapterOne), the pack rates and the sit clips. (Blue was removed 2026-11-03.)
    /// </summary>
    [InitializeOnLoad]
    static class BlueContentBootstrap
    {
        static BlueContentBootstrap()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
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
