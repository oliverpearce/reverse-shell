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
