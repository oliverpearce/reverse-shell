using System.Collections.Generic;
using System.Linq;
using Game.World;
using NUnit.Framework;

namespace Combat.Tests
{
    /// <summary>The Level Designer's palette (TileLegend): every tile filed in a known group, terrain by chapter section
    /// and kind, no character twice, and every character the maps use is in it.</summary>
    public class TileLegendTests
    {
        [Test]
        public void EveryTile_IsInAGroup_AndTerrainHasAKind()
        {
            foreach (var t in TileLegend.All)
            {
                Assert.Contains(t.Group, TileLegend.Groups, "tile " + t.Char);
                if (TileLegend.IsTerrain(t.Group))
                    Assert.Contains(t.Kind, TileLegend.Kinds, "terrain tile " + t.Char + " needs Deco / Solids / Misc");
                else
                    Assert.AreEqual("", t.Kind, "only terrain is split by kind: " + t.Char);
            }
        }

        [Test]
        public void NoCharacter_IsListedTwice()
        {
            var twice = TileLegend.All.GroupBy(t => t.Char).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            CollectionAssert.IsEmpty(twice);
        }

        [Test]
        public void EveryCharacterInTheMaps_IsInTheLegend()
        {
            var missing = new HashSet<char>();
            foreach (var area in WorldAreas.All)
                foreach (char c in area.Map)
                    if (c != '\n' && c != '\r' && TileLegend.Find(c) == null) missing.Add(c);
            CollectionAssert.IsEmpty(missing, "add these to TileLegend.All: " + new string(missing.ToArray()));
        }

        [Test]
        public void EveryArea_OpensAChapterSection()
        {
            Assert.AreEqual(TileLegend.Ch1Forest, TileLegend.SectionFor("intro_forest", "Forest"));
            Assert.AreEqual(TileLegend.Ch1City, TileLegend.SectionFor("city", "Forest"));
            Assert.AreEqual(TileLegend.Ch1Office, TileLegend.SectionFor("office", "Office"));
            Assert.AreEqual(TileLegend.Ch4, TileLegend.SectionFor("intro_cinderworks_foundry", "Cinderworks_Foundry"));
        }
    }
}
