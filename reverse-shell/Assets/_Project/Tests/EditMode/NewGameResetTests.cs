using Game.World;
using NUnit.Framework;

namespace Combat.Tests
{
    /// <summary>A new game (SaveGame.Restore with no save) puts the chips back: their flags and their pickups.</summary>
    public class NewGameResetTests
    {
        [TearDown]
        public void TearDown()
        {
            SaveGame.Restore(null);
        }

        [Test]
        public void NewGame_ClearsTheChips_AndThePickedUpItems()
        {
            StoryState.Set(Chapter1.LanguageChip);
            StoryState.Set(Chapter1.ColoursChip);
            StoryState.Set(Chapter1.NumbersChip);
            WorldSession.UsedItems.Add(WorldSession.ItemKey(WorldAreas.City, 1, 1));

            SaveGame.Restore(null);

            Assert.IsFalse(StoryState.Has(Chapter1.LanguageChip));
            Assert.IsFalse(StoryState.Has(Chapter1.ColoursChip));
            Assert.IsFalse(StoryState.Has(Chapter1.NumbersChip));
            Assert.AreEqual(0, WorldSession.UsedItems.Count, "picked-up items come back");
        }
    }
}
