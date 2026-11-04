using System.Collections.Generic;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    /// <summary>Parts, deck loadouts, the inventory in the save file, renamed-character migration and element names.</summary>
    public class LoadoutTests
    {
        CardDefinition strike, guard, sweep;
        CharacterDefinition hero;
        PartDefinition plating, blade, otherBlade;
        PartCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            strike = TestFactory.Card("Strike");
            guard = TestFactory.Card("Guard");
            sweep = TestFactory.Card("Sweep");
            hero = TestFactory.Character("Char_Hero", Resonance.Arc, 100f, null, strike, guard);
            hero.cardPool = new List<CardDefinition> { sweep };

            plating = Part("Part_Plating", PartSlot.Core, new StatModifier(StatType.MaxHP, ModifierKind.Percent, 0.2f));
            blade = Part("Part_Blade", PartSlot.Chip, new StatModifier(StatType.Attack, ModifierKind.Flat, 50f));
            otherBlade = Part("Part_OtherBlade", PartSlot.Chip, new StatModifier(StatType.Attack, ModifierKind.Flat, 10f));
            catalog = ScriptableObject.CreateInstance<PartCatalog>();
            catalog.parts = new List<PartDefinition> { plating, blade, otherBlade };
            catalog.starterParts = new List<PartDefinition> { plating, blade, otherBlade };

            Reset();
            WorldSession.Catalog = catalog;
        }

        [TearDown]
        public void TearDown()
        {
            Reset();
        }

        static void Reset()
        {
            WorldSession.Party.Clear();
            WorldSession.PartyOrder.Clear();
            WorldSession.DefeatedEncounters.Clear();
            WorldSession.Inventory.Clear();
            WorldSession.StarterPartsGiven = false;
            WorldSession.Catalog = null;
        }

        static PartDefinition Part(string name, PartSlot slot, params StatModifier[] modifiers)
        {
            var p = ScriptableObject.CreateInstance<PartDefinition>();
            p.name = name;
            p.displayName = name;
            p.slot = slot;
            p.modifiers = new List<StatModifier>(modifiers);
            return p;
        }

        [Test]
        public void Parts_ModifyStats_FlatAndPercent()
        {
            var unit = new BattleUnit(hero, Team.Player);
            unit.ApplyLoadout(new Loadout { Parts = new List<PartDefinition> { plating, blade } });
            Assert.AreEqual(150f, unit.GetStat(StatType.Attack), 1e-3f);
            Assert.AreEqual(1200, unit.MaxHP);
            Assert.AreEqual(1200, unit.HP, "fitting a loadout refills to the new max");
            Assert.AreEqual(1200, Leveling.MaxHP(hero, 1, new[] { plating }));
        }

        [Test]
        public void DeckGenerator_DrawsOnlyLoadoutCards()
        {
            var unit = new BattleUnit(hero, Team.Player);
            unit.ApplyLoadout(new Loadout { Cards = new List<CardDefinition> { sweep, guard } });
            var rng = new System.Random(3);
            for (int i = 0; i < 50; i++)
            {
                var card = DeckGenerator.Draw(new List<BattleUnit> { unit }, rng);
                Assert.AreNotEqual(strike, card.Definition, "swapped-out cards are never drawn");
            }
        }

        /// <summary>A deck is the locked (leftmost) card plus the chosen extra; Remove leaves just the locked card.</summary>
        [Test]
        public void Deck_IsLockedCardPlusChosenExtra_AndRemoveEmptiesIt()
        {
            var expected = PackRates.Current.startWithSecondCard ? new[] { strike, guard } : new[] { strike };
            CollectionAssert.AreEqual(expected, WorldSession.DeckOf(hero), "the default deck");
            CardCollection.SetExtra(hero, 0, sweep);
            CollectionAssert.AreEqual(new[] { strike, sweep }, WorldSession.DeckOf(hero));
            CardCollection.SetExtra(hero, 0, guard);
            CollectionAssert.AreEqual(new[] { strike, guard }, WorldSession.DeckOf(hero), "choosing again replaces it");
            CardCollection.SetExtra(hero, 0, null);
            CollectionAssert.AreEqual(new[] { strike }, WorldSession.DeckOf(hero), "Remove: just the locked card");
            CardCollection.SetExtra(hero, 0, strike);
            CollectionAssert.AreEqual(new[] { strike }, WorldSession.DeckOf(hero), "the locked card can't fill the extra slot too");
        }

        [Test]
        public void Equip_TakesFromInventory_AndReturnsTheOldPart()
        {
            WorldSession.GiveStarterParts();
            Assert.IsTrue(WorldSession.Equip(hero, blade));
            Assert.AreEqual(0, WorldSession.CountOf(blade));
            Assert.IsTrue(WorldSession.Equip(hero, otherBlade));
            Assert.AreEqual(otherBlade, WorldSession.PartIn(hero, PartSlot.Chip));
            Assert.AreEqual(1, WorldSession.CountOf(blade), "the replaced part goes back to the inventory");
            WorldSession.Unequip(hero, PartSlot.Chip);
            Assert.IsNull(WorldSession.PartIn(hero, PartSlot.Chip));
            Assert.AreEqual(1, WorldSession.CountOf(otherBlade));
            Assert.IsFalse(WorldSession.Equip(hero, plating) && WorldSession.Equip(hero, plating), "only owned parts can be fitted");
        }

        [Test]
        public void StarterCore_IsFittedOnTheProtagonist_WithNoSpares()
        {
            var core = Part("Part_Fragmented", PartSlot.Core);
            catalog.parts.Add(core);
            catalog.starterParts.Clear();
            catalog.starterCore = core;
            WorldSession.GiveStarterParts();
            var progress = WorldSession.Party[WorldSession.StartingMember];
            Assert.AreEqual(core.name, progress.Parts[(int)PartSlot.Core]);
            Assert.AreEqual(0, WorldSession.Inventory.Count, "no spare chips or cores");
        }

        [Test]
        public void StarterCore_IsBound_AndCannotBeRemovedOrReplaced()
        {
            var core = Part("Part_Fragmented", PartSlot.Core);
            catalog.parts.Add(core);
            catalog.starterCore = core;
            WorldSession.GiveStarterParts();   // also hands out the spare plating
            var a3 = TestFactory.Character(WorldSession.StartingMember, Resonance.Basic, 100f, null, strike);
            Assert.IsTrue(WorldSession.IsBound(WorldSession.PartIn(a3, PartSlot.Core)));
            WorldSession.Unequip(a3, PartSlot.Core);
            Assert.AreEqual(core, WorldSession.PartIn(a3, PartSlot.Core), "your own Core never comes out");
            Assert.IsFalse(WorldSession.Equip(a3, plating), "nor is it swapped for another Core");
            Assert.AreEqual(core, WorldSession.PartIn(a3, PartSlot.Core));
            Assert.AreEqual(1, WorldSession.CountOf(plating));
        }

        [Test]
        public void UltChip_StartsTheBattleWithTwoUltPoints()
        {
            var dummy = TestFactory.Enemy("Dummy");
            var plain = new BattleController(new[] { hero }, new[] { dummy }, CombatConfig.Default, 3);
            plain.StartBattle();
            var chipped = new BattleController(new[] { hero }, new[] { dummy }, CombatConfig.Default, 3);
            chipped.State.Party[0].Parts.Add(Part("Part_UltChip", PartSlot.Chip, new StatModifier(StatType.StartingMoxie, ModifierKind.Flat, 2f)));
            chipped.StartBattle();
            Assert.AreEqual(Mathf.Min(CombatConfig.Default.moxieCap, plain.State.Party[0].Moxie + 2), chipped.State.Party[0].Moxie);
        }

        [Test]
        public void Equip_KeepsMissingHP()
        {
            WorldSession.GiveStarterParts();
            WorldSession.ProgressOf(hero).HP = 700; // 300 missing
            WorldSession.Equip(hero, plating);      // max 1000 -> 1200
            Assert.AreEqual(900, WorldSession.HPOf(hero));
            WorldSession.Unequip(hero, PartSlot.Core);
            Assert.AreEqual(700, WorldSession.HPOf(hero));
        }

        [Test]
        public void SaveData_RoundTripsLoadoutAndInventory()
        {
            WorldSession.GiveStarterParts();
            WorldSession.Equip(hero, blade);
            CardCollection.SetExtra(hero, 0, sweep);

            string json = JsonUtility.ToJson(SaveGame.Capture());
            WorldSession.Party.Clear();
            WorldSession.Inventory.Clear();
            WorldSession.StarterPartsGiven = false;
            SaveGame.Restore(JsonUtility.FromJson<SaveGame.Data>(json));

            Assert.AreEqual(blade, WorldSession.PartIn(hero, PartSlot.Chip));
            CollectionAssert.AreEqual(new[] { strike, sweep }, WorldSession.DeckOf(hero));
            Assert.AreEqual(1, WorldSession.CountOf(plating));
            Assert.AreEqual(0, WorldSession.CountOf(blade));
            Assert.IsTrue(WorldSession.StarterPartsGiven);
        }

        [Test]
        public void Restore_MigratesRenamedCharacters()
        {
            var data = new SaveGame.Data();
            data.party.Add(new SaveGame.Member { id = "Char_2B", progress = new MemberProgress { Level = 5 } });
            data.party.Add(new SaveGame.Member { id = "Char_V1", progress = new MemberProgress { Level = 3 } });
            data.party.Add(new SaveGame.Member { id = "Char_V2", progress = new MemberProgress { Level = 2 } });
            data.partyOrder.Add("Char_V2");
            SaveGame.Restore(data);

            Assert.AreEqual(5, WorldSession.Party["Char_A3"].Level);
            Assert.AreEqual(3, WorldSession.Party["Char_Ashe"].Level, "V1 (Lv 3) and V2 (Lv 2, Lucy's old id) both became Ashe: the higher stays");
            CollectionAssert.AreEqual(new[] { "Char_Ashe" }, WorldSession.PartyOrder);
            Assert.IsFalse(WorldSession.StarterPartsGiven, "an old save gets the starter parts");
        }

        [Test]
        public void Restore_LucyAndBlueBecomeAshe()
        {
            var data = new SaveGame.Data();
            data.party.Add(new SaveGame.Member { id = "Char_Ashe", progress = new MemberProgress { Level = 4 } });
            data.party.Add(new SaveGame.Member { id = "Char_Lucy", progress = new MemberProgress { Level = 9 } });
            data.party.Add(new SaveGame.Member { id = "Char_Blue", progress = new MemberProgress { Level = 6 } });
            data.members.AddRange(new[] { "Char_A3", "Char_Ashe", "Char_Lucy", "Char_Blue" });
            data.partyOrder.AddRange(new[] { "Char_Lucy", "Char_Ashe", "Char_Blue" });
            SaveGame.Restore(data);

            Assert.AreEqual(9, WorldSession.Party["Char_Ashe"].Level, "the furthest-along progress is kept");
            Assert.IsFalse(WorldSession.Party.ContainsKey("Char_Lucy"));
            Assert.IsFalse(WorldSession.Party.ContainsKey("Char_Blue"));
            CollectionAssert.AreEqual(new[] { "Char_Ashe" }, WorldSession.PartyOrder);
            CollectionAssert.AreEquivalent(new[] { "Char_A3", "Char_Ashe" }, WorldSession.Members);
        }

        [Test]
        public void Restore_MigratesMcToA3()
        {
            var data = new SaveGame.Data();
            data.party.Add(new SaveGame.Member { id = "Char_MC", progress = new MemberProgress { Level = 7, Cards = new List<string> { "Card_MC_ArcEdge" } } });
            data.partyOrder.Add("Char_MC");
            data.members.Add("Char_MC");
            data.formation.Add("Char_MC");
            data.cardCopies.Add(new SaveGame.PartStack { id = "Card_MC_StaticField", count = 2 });
            data.unseenCards.Add("Card_MC_ArcTempest");
            SaveGame.Restore(data);

            Assert.AreEqual(7, WorldSession.Party["Char_A3"].Level);
            CollectionAssert.AreEqual(new[] { "Card_A3_ArcEdge" }, WorldSession.Party["Char_A3"].Cards);
            CollectionAssert.AreEqual(new[] { "Char_A3" }, WorldSession.PartyOrder);
            Assert.IsTrue(WorldSession.Members.Contains("Char_A3"));
            Assert.AreEqual("Char_A3", WorldSession.Formation[0]);
            Assert.AreEqual(2, WorldSession.CardCopies["Card_A3_StaticField"]);
            Assert.IsTrue(WorldSession.UnseenCards.Contains("Card_A3_ArcTempest"));
            Assert.AreEqual("Card_Ashe_Mend", SaveGame.MigrateCard("Card_HealBot_Mend"), "older renames still work");
        }

        [Test]
        public void StatBars_ReferenceAtLevelCapFillsNinetyPercent()
        {
            hero.growthPerLevel = 0.08f;
            hero.stats.critRate = 0.1f;
            float atCap = 1000f * Leveling.StatScale(Leveling.MaxLevel, 0.08f);
            Assert.AreEqual(60, Leveling.MaxLevel);
            Assert.AreEqual(0.9f, atCap / Leveling.BarMax(hero, StatType.MaxHP), 1e-4f);
            Assert.AreEqual(0.9f, hero.stats.critRate / Leveling.BarMax(hero, StatType.CritRate), 1e-4f, "flat stats too");
        }

        [Test]
        public void Elements_HaveGameNames_AndNeutralGlow()
        {
            Assert.AreEqual("Cryo", ResonanceInfo.Display(Resonance.Cryo));
            Assert.AreEqual("Arc", ResonanceInfo.Display(Resonance.Arc));
            Assert.AreEqual("Basic", ResonanceInfo.Display(Resonance.Basic));

            hero.neutralGlow = true;
            hero.glowColor = Color.white;
            Assert.AreEqual(Color.white, ResonanceInfo.GlowFor(hero));
            Assert.AreEqual(ResonanceInfo.Color(Resonance.Cryo), ResonanceInfo.GlowFor(hero, Resonance.Cryo), "channeling changes the hue");
            hero.neutralGlow = false;
            Assert.AreEqual(ResonanceInfo.Color(Resonance.Arc), ResonanceInfo.GlowFor(hero));
        }
    }
}
