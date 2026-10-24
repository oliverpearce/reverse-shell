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
            hero = TestFactory.Character("Char_Hero", Afflatus.Volt, 100f, null, strike, guard);
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

        [Test]
        public void SetDeckCard_SwapsPoolCardIn_AndSwapsPlacesWhenAlreadyInDeck()
        {
            CollectionAssert.AreEqual(new[] { strike, guard }, WorldSession.DeckOf(hero));
            WorldSession.SetDeckCard(hero, 0, sweep);
            CollectionAssert.AreEqual(new[] { sweep, guard }, WorldSession.DeckOf(hero));
            WorldSession.SetDeckCard(hero, 0, guard);
            CollectionAssert.AreEqual(new[] { guard, sweep }, WorldSession.DeckOf(hero));
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
            var mc = TestFactory.Character(WorldSession.StartingMember, Afflatus.None, 100f, null, strike);
            Assert.IsTrue(WorldSession.IsBound(WorldSession.PartIn(mc, PartSlot.Core)));
            WorldSession.Unequip(mc, PartSlot.Core);
            Assert.AreEqual(core, WorldSession.PartIn(mc, PartSlot.Core), "your own Core never comes out");
            Assert.IsFalse(WorldSession.Equip(mc, plating), "nor is it swapped for another Core");
            Assert.AreEqual(core, WorldSession.PartIn(mc, PartSlot.Core));
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
            WorldSession.SetDeckCard(hero, 1, sweep);

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

            Assert.AreEqual(5, WorldSession.Party["Char_MC"].Level);
            Assert.AreEqual(3, WorldSession.Party["Char_HealBot"].Level);
            Assert.AreEqual(2, WorldSession.Party["Char_Lucy"].Level);
            CollectionAssert.AreEqual(new[] { "Char_Lucy" }, WorldSession.PartyOrder);
            Assert.IsFalse(WorldSession.StarterPartsGiven, "an old save gets the starter parts");
        }

        [Test]
        public void StatBars_ReferenceAtLevelCapFillsNinetyPercent()
        {
            hero.growthPerLevel = 0.08f;
            hero.stats.critRate = 0.1f;
            float atCap = 1000f * Leveling.StatScale(Leveling.MaxLevel, 0.08f);
            Assert.AreEqual(30, Leveling.MaxLevel);
            Assert.AreEqual(0.9f, atCap / Leveling.BarMax(hero, StatType.MaxHP), 1e-4f);
            Assert.AreEqual(0.9f, hero.stats.critRate / Leveling.BarMax(hero, StatType.CritRate), 1e-4f, "flat stats too");
        }

        [Test]
        public void Elements_HaveGameNames_AndNeutralGlow()
        {
            Assert.AreEqual("Earth", AfflatusInfo.Display(Afflatus.Earth));
            Assert.AreEqual("Void", AfflatusInfo.Display(Afflatus.Void));
            Assert.AreEqual("Electric", AfflatusInfo.Display(Afflatus.Volt));
            Assert.AreEqual("Physical", AfflatusInfo.Display(Afflatus.None));

            hero.neutralGlow = true;
            hero.glowColor = Color.white;
            Assert.AreEqual(Color.white, AfflatusInfo.GlowFor(hero));
            Assert.AreEqual(AfflatusInfo.Color(Afflatus.Earth), AfflatusInfo.GlowFor(hero, Afflatus.Earth), "channeling changes the hue");
            hero.neutralGlow = false;
            Assert.AreEqual(AfflatusInfo.Color(Afflatus.Volt), AfflatusInfo.GlowFor(hero));
        }
    }
}
