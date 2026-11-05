using Game.World;
using NUnit.Framework;

namespace Combat.Tests
{
    /// <summary>The palette editor's world palettes (Resources/Palettes/WorldPalettes.json) as the game reads them.</summary>
    public class WorldPaletteTests
    {
        [Test]
        public void Parse_KeepsAreasWithColours_AndSkipsEmptyOnes()
        {
            var map = WorldPalette.Parse("{\"areas\":[{\"id\":\"city\",\"source\":\"endesga-32\",\"dither\":0.5,\"colors\":[\"6495ed\",\"222533\"]}," +
                                         "{\"id\":\"office\",\"source\":\"\",\"dither\":0,\"colors\":[]}]}");
            Assert.IsTrue(map.ContainsKey("city"));
            Assert.AreEqual(2, map["city"].colors.Count);
            Assert.AreEqual(0.5f, map["city"].dither, 1e-4f);
            Assert.IsFalse(map.ContainsKey("office"), "no colours = drawn as is");
        }

        [Test]
        public void Parse_Nothing_IsEmpty()
        {
            Assert.AreEqual(0, WorldPalette.Parse(null).Count);
            Assert.AreEqual(0, WorldPalette.Parse("").Count);
        }

        [Test]
        public void UnknownArea_HasNoPalette()
        {
            WorldPalette.Reload();
            Assert.IsNull(WorldPalette.For("no_such_area"));
        }
    }
}
