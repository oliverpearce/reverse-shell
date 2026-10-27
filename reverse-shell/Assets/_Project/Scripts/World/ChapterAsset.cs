using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// One chapter as an asset (Resources/Chapters): its areas, where a new game starts, and the objective shown
    /// top-right for each story flag. Chapters are ordered by <see cref="order"/>; the first one starts a new game.
    /// </summary>
    [CreateAssetMenu(menuName = "Reverse Shell/Chapter", fileName = "Chapter_New")]
    public class ChapterAsset : ScriptableObject
    {
        [System.Serializable]
        public class Objective
        {
            [Tooltip("The story flag that makes this the objective (e.g. ch1.scanner).")]
            public string flag = "";
            [Tooltip("The Resources/Story block with the objective's words (e.g. objective.scanner).")]
            public string key = "";
        }

        public string id = "";
        public string title = "New Chapter";
        [Tooltip("Lower comes first. The first chapter's start area is where a new game wakes up.")]
        public int order;
        public AreaAsset startArea;
        public List<AreaAsset> areas = new List<AreaAsset>();
        [Tooltip("In story order. The last one whose flag is set is shown; none set = no objective.")]
        public List<Objective> objectives = new List<Objective>();

        /// <summary>The objective's story key for the current flags, or null.</summary>
        public string ObjectiveKey()
        {
            for (int i = objectives.Count - 1; i >= 0; i--)
            {
                var o = objectives[i];
                if (o != null && !string.IsNullOrEmpty(o.flag) && StoryState.Has(o.flag)) return o.key;
            }
            return null;
        }
    }
}
