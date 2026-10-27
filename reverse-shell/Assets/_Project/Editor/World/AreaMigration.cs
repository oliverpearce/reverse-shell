using System.Collections.Generic;
using Game.World;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Creates Chapter One's area and chapter assets (Resources/Areas, Resources/Chapters) the first time the editor
    /// loads with no areas at all, from the definitions below (what used to live in WorldAreas). Only missing assets are made:
    /// once they exist, the assets are the real thing and this file is never read again for them.
    /// Reverse Shell > Restore Missing Areas runs it by hand.
    /// </summary>
    [InitializeOnLoad]
    static class AreaMigration
    {
        public const string AreasFolder = "Assets/_Project/Resources/Areas";
        public const string ChaptersFolder = "Assets/_Project/Resources/Chapters";

        static AreaMigration()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                // Only on a project that has no areas at all, so an area you delete on purpose stays deleted.
                if (AssetDatabase.IsValidFolder(AreasFolder)
                    && AssetDatabase.FindAssets("t:AreaAsset", new[] { AreasFolder }).Length > 0) return;
                int made = Run();
                if (made > 0) Debug.Log("Created " + made + " area/chapter assets in Resources/Areas and Resources/Chapters.");
            };
        }

        [MenuItem("Reverse Shell/Restore Missing Areas")]
        static void RestoreMenu()
        {
            int made = Run();
            EditorUtility.DisplayDialog("Areas", made == 0 ? "Every Chapter One area and chapter asset is already there; nothing changed."
                : "Created " + made + " missing area/chapter assets. Existing ones were left as they are.", "OK");
        }

        /// <summary>Makes whichever of the built-in areas and the Chapter 1 asset are missing. Returns how many.</summary>
        public static int Run()
        {
            EnsureFolder(AreasFolder);
            EnsureFolder(ChaptersFolder);
            int made = 0;
            var byId = new Dictionary<string, AreaAsset>();
            foreach (var area in BuiltIn())
            {
                string path = AreasFolder + "/Area_" + char.ToUpper(area.id[0]) + area.id.Substring(1) + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<AreaAsset>(path);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(area, path);
                    existing = area;
                    made++;
                }
                else Object.DestroyImmediate(area);
                byId[existing.id] = existing;
            }

            string chapterPath = ChaptersFolder + "/Chapter_1.asset";
            if (AssetDatabase.LoadAssetAtPath<ChapterAsset>(chapterPath) == null)
            {
                var chapter = ScriptableObject.CreateInstance<ChapterAsset>();
                chapter.id = "chapter1";
                chapter.title = "Chapter One";
                chapter.order = 1;
                chapter.startArea = byId[WorldAreas.City];
                chapter.areas = new List<AreaAsset> { byId[WorldAreas.City], byId[WorldAreas.Tunnel], byId[WorldAreas.Chamber] };
                chapter.objectives = new List<ChapterAsset.Objective>
                {
                    Objective(Chapter1.Awake, "objective.explore"),
                    Objective(Chapter1.Scanner, "objective.scanner"),
                    Objective(Chapter1.CollectorMet, "objective.collector"),
                    Objective(Chapter1.CollectorBeaten, "objective.scan"),
                    Objective(Chapter1.Route, "objective.route"),
                    Objective(Chapter1.Tunnel, "objective.tunnel"),
                    Objective(Chapter1.DragonSeen, "objective.guardian"),
                    Objective(Chapter1.DragonBeaten, "objective.card"),
                    Objective(Chapter1.ResonanceVolt, "objective.leave"),
                    Objective(Chapter1.Complete, "objective.complete"),
                };
                AssetDatabase.CreateAsset(chapter, chapterPath);
                made++;
            }
            if (made > 0)
            {
                AssetDatabase.SaveAssets();
                WorldAreas.Reload();
            }
            return made;
        }

        static ChapterAsset.Objective Objective(string flag, string key)
        {
            return new ChapterAsset.Objective { flag = flag, key = key };
        }

        static AreaAsset Area(string id, string name, string subtitle, string map, bool dim, params string[] warps)
        {
            var a = ScriptableObject.CreateInstance<AreaAsset>();
            a.id = id;
            a.displayName = name;
            a.subtitle = subtitle;
            a.map = map;
            a.dim = dim;
            for (int i = 0; i + 1 < warps.Length; i += 2) a.warps.Add(new AreaAsset.Warp { tile = warps[i], to = warps[i + 1] });
            return a;
        }

        static WorldAreas.EncounterDef Fight(string enemy, int level)
        {
            return new WorldAreas.EncounterDef { Enemies = new[] { enemy }, Level = level };
        }

        /// <summary>Chapter One's areas and the Sandbox exactly as they were in code (maps from Tools/MapForge/chapter1_maps.py).</summary>
        static IEnumerable<AreaAsset> BuiltIn()
        {
            var city = Area(WorldAreas.City, "THE CITY RUINS", "Overgrown District", CityMap, false, "V", WorldAreas.Tunnel);
            city.encounters.Add(Fight("Enemy_ScrapHound", 4));
            city.encounters.Add(Fight("Enemy_ScrapHound", 3));
            city.encounters.Add(Fight("Enemy_ScrapHound", 5));
            city.npcs.Add(new WorldAreas.NpcDef { Name = "Small Machine", Machine = true, LinesKey = "npc.small_machine" });
            yield return city;

            var tunnel = Area(WorldAreas.Tunnel, "THE BURIED LINE", "Beneath the City Ruins", TunnelMap, true,
                "W", WorldAreas.City, "J", WorldAreas.Chamber);
            tunnel.encounters.Add(Fight("Enemy_WireCrawler", 5));
            tunnel.encounters.Add(Fight("Enemy_WireCrawler", 6));
            yield return tunnel;

            var chamber = Area(WorldAreas.Chamber, "THE HUMMING HALL", "Where the trace ends", ChamberMap, true, "W", WorldAreas.Tunnel);
            chamber.encounters.Add(new WorldAreas.EncounterDef
            {
                Enemies = new[] { "Enemy_ElectricDragon" }, Level = 7, Boss = true, WinFlag = Chapter1.DragonBeaten,
                Blocks = new[] { Chapter1.DragonBeaten },
                WinKey = "guardian.win",   // its reward is the Soul Card on the pedestal behind it
            });
            yield return chamber;

            yield return Area(WorldAreas.Sandbox, "SANDBOX", "Mossline Station", SandboxMap, false, "W", WorldAreas.City);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        const string CityMap =
            "HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH\n" +
            "H............==T.LLL.......*.T===...........,,,.==...........r.H\n" +
            "H.HHHHBBBHHH.==.BLLLBBBLLLBBB.===.HHHHHHHHHH,,,T==.BBBBBBBBBBB.H\n" +
            "H.HHHHBBBHHHr==.BLLLBBBLLLBBB.===*HHHHHHHHHH,,,.==.B7.......3B.H\n" +
            "HbHHHHBBBH,,,==.BBBBBBBLLLBBB.===bH,,,HHHHHHHHHb==.B....O....B.H\n" +
            "H.HHHHHHHHLLL==.BBBBBBBBBBBBB,===.H,,,HHHHHHHHH.==.B.........B.H\n" +
            "H.HHHHHHHHLLL==.BBBBBBBBBBBBB.===.H,,,HHHBBBHHH.==.B.........B.H\n" +
            "H,HHHHHHHHLLL==.BBBBBBBBBBBBB.==,.HHHHHHHBBBHHH.==.B...,.....B.H\n" +
            "H............==,..............===b.......BBB...r==.BBBBBZBBBBB.H\n" +
            "H==============================================================H\n" +
            "H==============================================================H\n" +
            "H....Tb.....r==TPPTPPTPPTPPTPP===.BBB...........==,............H\n" +
            "H.BBB,,,BBBB.==PPPPPPPPPPPPPPP===bBB,,,########.==T###########.H\n" +
            "H.BBB,,,BBBB,=,PP5PPPPPPPPPPPP===.BB,,,########.==.###########.H\n" +
            "H.BBB,,,BBBB.==PPPPPPPPPPPPPPP===.##,,,########b==.###########.H\n" +
            "H.B,,,BBBBBB.==PPPPPPPAPPPPPPP===T#############.==LLL#######LLbH\n" +
            "H.B,,,BBBBBB.==PPPPPPPPPPPPPPP===.###########LLL==LLL###,,,#LLLH\n" +
            "HTB,,,BBBBBB.==PPPPPPPPPPP2PPP===.###########LLL==LLL###,,,#LLTH\n" +
            "HbBBBBBBBBBB.==PPPPPPPPPPPPPPP,==.###########LLL==.#####,,,###rH\n" +
            "H....BBB.....==TPPTPPTPPTPPTPP===..............b==....,........H\n" +
            "H==============================,=======================*=======H\n" +
            "H====T==E=========r==================r==E===T=======*==========H\n" +
            "H=======X==============T================X======================H\n" +
            "HBBBBBPB=BBBB==,.............b===,,,,,,,=,,,,*,,,=.............H\n" +
            "HBP*PP*PP*PPB==.HHHHHHHHHHHHH.=,=,6,,,T,,,,,,b,*==.BBBBBBBBBBB.H\n" +
            "HBPPPPPPPPPPB==.HHHHHHHHHLLLH.===,,,,,,,TT,TT,,,==.BBBBBBBBBBB.H\n" +
            "HBP1PPPPPPPPB==.HHHHHH,,,LLLHb===,,,*b*****,,T,,==.BBBBBBBBBBB.H\n" +
            "HBPPPPKPPPPPB==,HHHHHH,,,LLLH.===*,,b***n**,,,T,==.BBBBBBBBBBB.H\n" +
            "HBPPPPSPPPPPB==bHHHHHH,,,BHHH.===,,,,,*****Tb,,,=*rBBBBBBBBBBB.H\n" +
            "HBPPPPPPPPPPB==.HHHHHHHHHHHHH.,==,,,,,*****,,,,,==,,,,BBBBBBBBbH\n" +
            "HBP4PPPPPPPPB==.HHHHHHHHHHVHH.===,,,,,,,b*T*,,*,==.,,,,BBBBBBBrH\n" +
            "HBBBBBPBBBBBB==...............===,,,,,,,,,,,,,,,==.,,,,.......,H\n" +
            "H=b====,====*=========:::::::=====b=========E==================H\n" +
            "H===========================================X==================H\n" +
            "H,....*...BBB==T.............b===LLL........=...==T......,.....H\n" +
            "H.#######BBBB==TBB###########.===LLLHHHHHHHHHHHb==.BBBBBBBBBBB.H\n" +
            "Hr#######BBBb==BBB###########.===LLLHHHHHHHHHBB,==,BBBBBBBBBBB.H\n" +
            "H.#######BBB.==BBB##LLLB#####,===,HHHHHHLLLHHBBB==.BBBBBBBBBBBTH\n" +
            "H.#######BBBb==.####LLLB#####.===.HHHHHHLLLHHBBB==.BBBB,,,BBBB.H\n" +
            "H.#######BBB.*=.####LLLB#####b===,HHHHHHLLLHHHH.==rBBBB,,,BBBB.H\n" +
            "H........BBB.==..............b===,..............==..BBB,,,.....H\n" +
            "HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH\n";

        const string TunnelMap =
            "HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH\n" +
            "HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH\n" +
            "HH...4.HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH\n" +
            "HH.W...HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH\n" +
            "HH..S..HHHHHHHHHHHHHHH=========E====...HHHHH\n" +
            "HH.....HHHHHHHHHHHHHHH========:X====...HHHHH\n" +
            "HH.....HHHHHHHHHHHHHHH=====r========...HHHHH\n" +
            "HHHH...HHHHHHHHHHHHHHH...HHHHHHHHHHH.r.HHHHH\n" +
            "HHHH.:.HHHHHHHHHHHHHHH.:.HHHHHHHHHHH...HHHHH\n" +
            "HHHH...HHHHHHHHHHHHHHH...HHHHHHHHHHH...HHHHH\n" +
            "HHHH==========E=====r=...HHHHHHHHHHH...A..HH\n" +
            "HHHH======:===X=:=====...HHHHHHHHHHH.....JHH\n" +
            "HHHH====r=============...HHHHHHHHHHH......HH\n" +
            "HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH\n" +
            "HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH\n" +
            "HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH\n";

        const string ChamberMap =
            "HHHHHHHHHHHHHHHHHHHHHHHHHH\n" +
            "HHHHHHHHHHHHHHHHHHHHHHHHHH\n" +
            "HHHHHHHHHHHHHHHHHHHHHHHHHH\n" +
            "HHH....................HHH\n" +
            "HHH.,,,,,,,,U,,,,,,,,,.HHH\n" +
            "HHH.r................r.HHH\n" +
            "HHH....................HHH\n" +
            "HHH....................HHH\n" +
            "HHH.........E..........HHH\n" +
            "HHH....r....X.....r....HHH\n" +
            "HHH....................HHH\n" +
            "HHH....................HHH\n" +
            "HHHZZZZZZZZZZZZZZZZZZZZHHH\n" +
            "HHH.r................r.HHH\n" +
            "HHH....................HHH\n" +
            "HHHHHHHHHHH..S.HHHHHHHHHHH\n" +
            "HHHHHHHHHHH.W..HHHHHHHHHHH\n" +
            "HHHHHHHHHHHHHHHHHHHHHHHHHH\n";


        const string SandboxMap =
            "HHHHHH####HHHHH#BBBBBBBBBBBB\n" +
            "HHHHHH####HHHHH#BPPP,,PPPPPB\n" +
            "HHHHHH####HHHHH#BPT,PPEPP,TB\n" +
            "HHHHHH####HHHHH#B,,PPPXPPP,B\n" +
            "HHHHHH####HHHHH#BPRCPPPPPbPB\n" +
            "HHHHHH####HHHHH#BLLLLP.PLLLB\n" +
            "B.T,,...b..n.r,,....*..,.,TB\n" +
            "B=====,,==========,,=======B\n" +
            "B=======,===========r======B\n" +
            "BWb..,.n..T,,......b...,T..B\n" +
            "B.S.A,,......*....n..,,..*.B\n" +
            "LLLLLLLLLLLLLLLLLLLLLLLLLLLL\n";
    }
}
