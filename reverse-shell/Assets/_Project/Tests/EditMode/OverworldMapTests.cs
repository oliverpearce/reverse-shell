using System.Collections.Generic;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public class OverworldMapTests
    {
        [Test]
        public void SandboxMap_IsRectangular_AndHasSpawnAndEncounter()
        {
            string sandbox = WorldAreas.Get(WorldAreas.Sandbox).Map;
            var lines = sandbox.TrimEnd('\n').Split('\n');
            foreach (var line in lines)
                Assert.AreEqual(lines[0].Length, line.Length, "row: " + line);

            var map = new OverworldMap(sandbox);
            Assert.AreEqual(1, map.FindAll('S').Count);
            Assert.AreEqual(1, map.FindAll('E').Count);
            Assert.AreEqual(1, map.FindAll('X').Count);
            Assert.AreEqual(1, map.FindAll('A').Count, "one access point near the spawn");
            Assert.AreEqual(1, map.FindAll('R').Count, "one summon beacon in the plaza");
        }

        [Test]
        public void EveryArea_IsRectangular_WithOneSpawn_FightsMatchDefinitions_AndWarpsPair()
        {
            foreach (var area in WorldAreas.All)
            {
                var lines = area.Map.TrimEnd('\n').Split('\n');
                foreach (var line in lines)
                    Assert.AreEqual(lines[0].Length, line.Length, area.Id + " row: " + line);
                var map = new OverworldMap(area.Map);
                Assert.AreEqual(1, map.FindAll('S').Count, area.Id + ": one spawn");
                Assert.AreEqual(map.FindAll('E').Count, map.FindAll('X').Count, area.Id + ": a fighting square per machine");
                if (area.Encounters.Count > 0)
                    Assert.AreEqual(map.FindAll('E').Count, area.Encounters.Count, area.Id + ": one fight definition per machine");
                Assert.AreEqual(map.FindAll('c').Count, area.Chests.Count, area.Id + ": one Part per loot chest");
                foreach (var warp in area.Warps)
                {
                    Assert.Greater(map.FindAll(warp.Key).Count, 0, area.Id + ": warp tile " + warp.Key + " is on the map");
                    if (area.Id == WorldAreas.Sandbox) continue;   // the debug area is one-way
                    var to = WorldAreas.Get(warp.Value);
                    Assert.IsTrue(to.Warps.ContainsValue(area.Id), area.Id + " -> " + warp.Value + ": a warp leads back");
                }
            }
        }

        /// <summary>Every warp, fighting square and trigger is reachable from the spawn (the hidden route counted as
        /// open), and every solid story prop has a reachable side, so Chapter One can't softlock on the map.</summary>
        [Test]
        public void ChapterOneMaps_EverythingImportantIsReachable()
        {
            foreach (var id in new[] { WorldAreas.City, WorldAreas.Tunnel, WorldAreas.Chamber })
            {
                var map = new OverworldMap(WorldAreas.Get(id).Map);
                var start = map.FindAll('S')[0];
                var seen = new HashSet<Vector2Int> { start };
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                var steps = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    foreach (var d in steps)
                    {
                        var n = c + d;
                        if (seen.Contains(n) || !map.InBounds(n.x, n.y)) continue;
                        if (map.IsSolidCell(n.x, n.y) && map.At(n.x, n.y) != 'V') continue;
                        seen.Add(n);
                        queue.Enqueue(n);
                    }
                }
                foreach (var tile in "WJXZV")
                    foreach (var cell in map.FindAll(tile))
                        Assert.IsTrue(seen.Contains(cell), id + ": " + tile + " at " + cell + " is reachable");
                foreach (var tile in "KO1234567UnAc")
                    foreach (var cell in map.FindAll(tile))
                    {
                        bool side = false;
                        foreach (var d in steps) side |= seen.Contains(cell + d);
                        Assert.IsTrue(side, id + ": " + tile + " at " + cell + " can be reached");
                    }
            }
        }

        /// <summary>The office, where a new game opens: a desk with its PC just north of the start square (the opening
        /// sits you there) and every floor square reachable, for when you come back later.</summary>
        [Test]
        public void Office_HasTheOpeningDesk_AndIsAllReachable()
        {
            var area = WorldAreas.Get(WorldAreas.Office);
            Assert.AreEqual(WorldAreas.Office, area.Id, "Resources/Areas has the office");
            Assert.IsFalse(WorldAreas.IsSandboxArea(area.Id), "the story runs in the office");
            var map = new OverworldMap(area.Map);
            var start = map.FindAll('S')[0];
            Assert.AreEqual('a', map.At(start.x, start.y - 1), "a desk just north of the start square");
            var seen = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            var steps = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in steps)
                {
                    var n = c + d;
                    if (seen.Contains(n) || map.IsSolidCell(n.x, n.y)) continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            for (int row = 0; row < map.Height; row++)
                for (int x = 0; x < map.Width; x++)
                    if (!map.IsSolidCell(x, row))
                        Assert.IsTrue(seen.Contains(new Vector2Int(x, row)), "office: " + x + "," + row + " is reachable");
        }

        [Test]
        public void CellCenter_RoundTripsThroughWorldToCell()
        {
            var map = new OverworldMap("##\n#.\n..\n");
            int x, row;
            map.WorldToCell(map.CellCenter(1, 1), out x, out row);
            Assert.AreEqual(1, x);
            Assert.AreEqual(1, row);
        }

        [Test]
        public void Solidity_FollowsLegend_AndOutOfBoundsIsSolid()
        {
            var map = new OverworldMap("#.m\nPXE\n");
            Assert.IsTrue(map.IsSolidCell(0, 0));   // building
            Assert.IsFalse(map.IsSolidCell(1, 0));  // sidewalk
            Assert.IsTrue(map.IsSolidCell(2, 0));   // tree
            Assert.IsFalse(map.IsSolidCell(0, 2));  // tall grass
            Assert.IsFalse(map.IsSolidCell(1, 2));  // flowers
            Assert.IsTrue(map.IsSolidCell(2, 2));   // bush
            Assert.IsFalse(map.IsSolidCell(1, 1));  // stand square
            Assert.IsTrue(map.IsSolidCell(2, 1));   // enemy
            Assert.IsTrue(map.IsSolidCell(-1, 0));
            Assert.IsTrue(map.IsSolidAt(new Vector3(-0.5f, 0f, 0.5f)));
        }

        /// <summary>Every chapter asset has a start area among its areas, unique area ids, and objective words that exist.</summary>
        [Test]
        public void Chapters_HaveStartArea_AndEveryObjectiveHasText()
        {
            Assert.IsNotNull(WorldAreas.Chapter(Chapter1.ChapterId), "Resources/Chapters has Chapter One");
            var ids = new HashSet<string>();
            foreach (var area in WorldAreas.All) Assert.IsTrue(ids.Add(area.Id), "area id used twice: " + area.Id);
            foreach (var chapter in WorldAreas.Chapters)
            {
                Assert.IsNotNull(chapter.startArea, chapter.name + ": start area");
                Assert.Contains(chapter.startArea, chapter.areas, chapter.name + ": the start area is one of its areas");
                foreach (var o in chapter.objectives)
                    Assert.IsTrue(StoryText.Has(o.key), chapter.name + ": objective text " + o.key);
            }
            Assert.AreEqual(WorldAreas.City, WorldAreas.StartArea, "a new game wakes in the city");
        }

        /// <summary>The Level Designer's palette knows every tile the areas use, and lists each tile once.</summary>
        [Test]
        public void TileLegend_CoversEveryTileInTheMaps()
        {
            var seen = new HashSet<char>();
            foreach (var t in TileLegend.All) Assert.IsTrue(seen.Add(t.Char), "listed twice: " + t.Char);
            foreach (var area in WorldAreas.All)
                foreach (char c in area.Map.Replace("\n", ""))
                    Assert.IsNotNull(TileLegend.Find(c), area.Id + " uses '" + c + "', which isn't in TileLegend");
        }
    }
}
