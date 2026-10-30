using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The overworld areas. One Unity scene (Overworld_Mossline) builds whichever area <see cref="WorldSession.CurrentArea"/>
    /// names. A warp tile (see <see cref="Area.Warps"/>) travels to another area: the scene reloads through the ink fade
    /// and you arrive beside the warp in the new area that leads back.
    /// Every area is an <see cref="AreaAsset"/> in Resources/Areas and every chapter a <see cref="ChapterAsset"/> in
    /// Resources/Chapters; edit those, not code. Chapter One's ids:
    ///   city      The City Ruins: Chapter One's city (a new game wakes here).
    ///   tunnel    The buried tunnel behind the city's hidden route (dim).
    ///   chamber   The electric dragon's chamber (dim).
    ///   sandbox   The Sandbox: the old Mossline Station test map, reached only through Sandbox Mode.
    ///   intro_*   The chapter intro areas behind the Sandbox's portals (Track C; Sandbox-only too, IsSandboxArea).
    ///   office    The bright office where a new game opens, at a Retro PC (the opening, PrologueSequence); a
    ///             later teleport comes back here.
    /// Maps use the <see cref="OverworldMap"/> legend.
    /// </summary>
    public static class WorldAreas
    {
        public const string City = "city";
        public const string Sandbox = "sandbox";
        /// <summary>Chapter One: the buried tunnel behind the hidden route, and the electric dragon's chamber.</summary>
        public const string Tunnel = "tunnel";
        public const string Chamber = "chamber";
        /// <summary>The office: a new game's opening plays at its PC, before the city.</summary>
        public const string Office = "office";

        /// <summary>
        /// One fight on the map: the enemies (bestiary asset names), their level, boss mode, and the story flag a win
        /// sets. <see cref="Requires"/> / <see cref="Blocks"/> decide whether it is there at all.
        /// </summary>
        [System.Serializable]
        public class EncounterDef
        {
            public string[] Enemies;
            public int Level = Combat.BattleUnit.DefaultEnemyLevel;
            public bool Boss;
            public string WinFlag;
            public string[] Requires;
            public string[] Blocks;
            /// <summary>Said by the protagonist after a win (null = the usual line).</summary>
            public string WinMessage;
            /// <summary>A Resources/Story block said after a win instead (takes precedence over <see cref="WinMessage"/>).</summary>
            public string WinKey;
            /// <summary>A Part (asset name) given on a win. Only for one-off story fights (patrols respawn).</summary>
            public string Reward;

            /// <summary>A copy with blank text as null and empty flag lists as null (the Inspector saves "" and []).</summary>
            public EncounterDef Clean()
            {
                var c = (EncounterDef)MemberwiseClone();
                c.WinFlag = Blank(WinFlag);
                c.WinMessage = Blank(WinMessage);
                c.WinKey = Blank(WinKey);
                c.Reward = Blank(Reward);
                if (Requires != null && Requires.Length == 0) c.Requires = null;
                if (Blocks != null && Blocks.Length == 0) c.Blocks = null;
                return c;
            }
        }

        /// <summary>A person (or harmless machine) on the map, in 'n' order.</summary>
        [System.Serializable]
        public class NpcDef
        {
            public string Name;
            public string[] Lines;
            /// <summary>A Resources/Story block to say instead of <see cref="Lines"/>.</summary>
            public string LinesKey;
            public bool Machine;   // drawn with the machine sprite rather than a survivor

            public NpcDef Clean()
            {
                var c = (NpcDef)MemberwiseClone();
                c.LinesKey = Blank(LinesKey);
                if (c.Lines == null) c.Lines = new string[0];
                return c;
            }
        }

        public class Area
        {
            public string Id, Name, Subtitle, Map;
            /// <summary>Warp tiles and where they lead ('V' is the city's hidden route, a wall until it is opened).</summary>
            public Dictionary<char, string> Warps = new Dictionary<char, string>();
            /// <summary>One per 'E' cell, in reading order (none = the shared practice fight).</summary>
            public List<EncounterDef> Encounters = new List<EncounterDef>();
            public List<NpcDef> Npcs = new List<NpcDef>();
            /// <summary>The Part (asset name) in each loot chest ('c'), in reading order; each opens once.</summary>
            public List<string> Chests = new List<string>();
            /// <summary>Underground: dim light, dark haze.</summary>
            public bool Dim;
            /// <summary>The world's look (<see cref="WorldTheme"/> id), or null for the old look.</summary>
            public string Theme;
        }

        static List<AreaAsset> areaAssets;
        static List<ChapterAsset> chapterAssets;

        /// <summary>Forgets the loaded assets, so newly made or deleted ones are picked up (the editor tools call this).</summary>
        public static void Reload()
        {
            areaAssets = null;
            chapterAssets = null;
        }

        static List<AreaAsset> AreaAssets
        {
            get
            {
                if (areaAssets == null || areaAssets.Exists(a => a == null))
                    areaAssets = new List<AreaAsset>(Resources.LoadAll<AreaAsset>("Areas"));
                return areaAssets;
            }
        }

        /// <summary>Every chapter, in <see cref="ChapterAsset.order"/>.</summary>
        public static List<ChapterAsset> Chapters
        {
            get
            {
                if (chapterAssets == null || chapterAssets.Exists(c => c == null))
                {
                    chapterAssets = new List<ChapterAsset>(Resources.LoadAll<ChapterAsset>("Chapters"));
                    chapterAssets.Sort((a, b) => a.order.CompareTo(b.order));
                }
                return chapterAssets;
            }
        }

        /// <summary>The chapter with this id, or null.</summary>
        public static ChapterAsset Chapter(string id)
        {
            return Chapters.Find(c => c.id == id);
        }

        /// <summary>Every area (Resources/Areas), freshly read from its asset.</summary>
        public static List<Area> All
        {
            get { return AreaAssets.ConvertAll(a => a.ToArea()); }
        }

        /// <summary>Where a new game wakes up: the first chapter's start area (Chapter One: the city). A new game opens
        /// in the <see cref="Office"/> first.</summary>
        public static string StartArea
        {
            get
            {
                var first = Chapters.Count > 0 ? Chapters[0] : null;
                return first != null && first.startArea != null ? first.startArea.id : City;
            }
        }

        /// <summary>Old area ids to the new ones (the Training Center became the Sandbox).</summary>
        public static string Migrate(string id)
        {
            return id == "training" ? Sandbox : id;
        }

        public static Area Get(string id)
        {
            id = Migrate(id);
            var asset = AreaAssets.Find(a => a.id == id) ?? AreaAssets.Find(a => a.id == StartArea);
            if (asset != null) return asset.ToArea();
            Debug.LogError("No area assets in Resources/Areas. Open the project in Unity once (or use Reverse Shell > Restore Missing Areas).");
            return new Area { Id = id, Name = "MISSING AREA", Subtitle = "", Map = "HHHHH\nH...H\nH.S.H\nH...H\nHHHHH\n" };
        }

        /// <summary>Areas only Sandbox Mode reaches: the Sandbox and its chapter intro areas (ids starting "intro_",
        /// Track C's portals). In them the story is off and Leave Sandbox Mode works.</summary>
        public static bool IsSandboxArea(string id)
        {
            return id == Sandbox || (id != null && id.StartsWith(IntroPrefix, System.StringComparison.Ordinal));
        }

        /// <summary>The id prefix of the chapter intro areas behind the Sandbox's portals.</summary>
        public const string IntroPrefix = "intro_";

        internal static string Blank(string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }
}
