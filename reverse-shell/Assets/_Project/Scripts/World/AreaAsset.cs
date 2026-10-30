using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// One overworld area as an asset (Resources/Areas): its map, where its warps lead, its fights, people and chests.
    /// <see cref="WorldAreas"/> loads every one of these at runtime, so editing an area needs no code.
    /// The map uses the <see cref="OverworldMap"/> legend; one row per line, all rows the same length.
    /// </summary>
    [CreateAssetMenu(menuName = "Reverse Shell/Area", fileName = "Area_New")]
    public class AreaAsset : ScriptableObject
    {
        [System.Serializable]
        public class Warp
        {
            [Tooltip("The warp tile on the map (one character, e.g. W).")]
            public string tile = "W";
            [Tooltip("The id of the area it leads to.")]
            public string to = "";
        }

        [Tooltip("Used by saves and warps; never change it once players have saves in this area.")]
        public string id = "";
        [Tooltip("The big title shown on arrival.")]
        public string displayName = "NEW AREA";
        public string subtitle = "";
        [Tooltip("Underground: dim light, dark haze.")]
        public bool dim;
        [Tooltip("The world's look (Resources/Themes/Theme_<id>, e.g. Forest): light, haze, pixel size and nature models. " +
                 "Blank = the old look.")]
        public string theme = "";
        [TextArea(12, 50)] public string map = "";
        public List<Warp> warps = new List<Warp>();
        [Tooltip("One per 'E' on the map, in reading order (left to right, top to bottom). None = the shared practice fight.")]
        public List<WorldAreas.EncounterDef> encounters = new List<WorldAreas.EncounterDef>();
        [Tooltip("One per 'n' on the map, in reading order.")]
        public List<WorldAreas.NpcDef> npcs = new List<WorldAreas.NpcDef>();
        [Tooltip("The Part (asset name) in each loot chest 'c', in reading order.")]
        public List<string> chests = new List<string>();

        /// <summary>The runtime view of this area. Blank text fields become null, which the game reads as "not set".</summary>
        public WorldAreas.Area ToArea()
        {
            var area = new WorldAreas.Area
            {
                Id = id, Name = displayName, Subtitle = subtitle, Map = (map ?? "").Replace("\r", ""), Dim = dim, Theme = WorldAreas.Blank(theme),
                Chests = new List<string>(chests),
            };
            foreach (var w in warps)
                if (w != null && !string.IsNullOrEmpty(w.tile)) area.Warps[w.tile[0]] = w.to;
            // A fight with no enemies listed is the shared practice fight.
            foreach (var e in encounters) area.Encounters.Add(e != null && e.Enemies != null && e.Enemies.Length > 0 ? e.Clean() : null);
            foreach (var n in npcs) area.Npcs.Add((n ?? new WorldAreas.NpcDef()).Clean());
            return area;
        }
    }
}
