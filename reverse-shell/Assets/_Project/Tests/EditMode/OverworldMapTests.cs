using System.Collections.Generic;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public class OverworldMapTests
    {
        [Test]
        public void DefaultMap_IsRectangular_AndHasSpawnAndEncounter()
        {
            var lines = OverworldBuilder.DefaultMap.TrimEnd('\n').Split('\n');
            foreach (var line in lines)
                Assert.AreEqual(lines[0].Length, line.Length, "row: " + line);

            var map = new OverworldMap(OverworldBuilder.DefaultMap);
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
    }
}
