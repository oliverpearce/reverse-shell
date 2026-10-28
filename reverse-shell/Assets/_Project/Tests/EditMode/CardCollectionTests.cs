using System.Collections.Generic;
using Combat;
using Game.World;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    /// <summary>The card collection: copies rank cards up, Sun / Moon / Star caps, Soul levels, pack rolls and saving.
    /// Numbers come from PackRates.Current, so the tests follow whatever the asset is tuned to.</summary>
    public class CardCollectionTests
    {
        PackRates Rates { get { return PackRates.Current; } }
        CardDefinition strike, guard, ult;
        CharacterDefinition hero;

        [SetUp]
        public void SetUp()
        {
            SaveGame.Restore(null);
            strike = Card("Card_Test_Strike", false);
            guard = Card("Card_Test_Guard", false);
            ult = Card("Card_Test_Ult", true);
            hero = ScriptableObject.CreateInstance<CharacterDefinition>();
            hero.name = "Char_TestHero";
            hero.incantations = new List<CardDefinition> { strike, guard };
            hero.ritual = ult;
        }

        [TearDown]
        public void TearDown() { SaveGame.Restore(null); }

        static CardDefinition Card(string name, bool isUlt)
        {
            var c = ScriptableObject.CreateInstance<CardDefinition>();
            c.name = name;
            c.isRitual = isUlt;
            return c;
        }

        [Test]
        public void Copies_UpgradeCards_AtTheThresholds()
        {
            Assert.AreEqual(0, CardCollection.Upgrades(strike));
            CardCollection.AddCopy(strike, Rates.CopiesFor(1) - 1);
            Assert.AreEqual(0, CardCollection.Upgrades(strike));
            Assert.AreEqual(1, CardCollection.CopiesToNextUpgrade(strike));
            CardCollection.AddCopy(strike);
            Assert.AreEqual(1, CardCollection.Upgrades(strike));
            Assert.AreEqual(2, CardCollection.UpgradeLevel(strike));
            CardCollection.AddCopy(strike, Rates.CopiesFor(Rates.MaxUpgrades) - Rates.CopiesFor(1));
            Assert.AreEqual(Rates.MaxUpgrades, CardCollection.Upgrades(strike));
            Assert.AreEqual(0, CardCollection.CopiesToNextUpgrade(strike), "the last upgrade is the top");
        }

        [Test]
        public void Upgrades_Cost5Then10Then20()
        {
            var rates = ScriptableObject.CreateInstance<PackRates>();
            Assert.AreEqual(5, rates.CopiesFor(1));
            Assert.AreEqual(15, rates.CopiesFor(2));
            Assert.AreEqual(35, rates.CopiesFor(3));
            Assert.AreEqual(2, rates.Upgrades(20));
            Object.DestroyImmediate(rates);
        }

        [Test]
        public void CopiesPastTheTop_BecomeBlankCards_ThatImprintOtherCards()
        {
            CardCollection.AddCopy(strike, CardCollection.MaxCopies + 2);
            Assert.AreEqual(CardCollection.MaxCopies, CardCollection.Copies(strike), "copies stop at the top level");
            Assert.AreEqual(2, CardCollection.Blanks, "the rest became blank cards");
            Assert.IsFalse(CardCollection.Imprint(strike), "a maxed card can't take a blank");
            Assert.IsTrue(CardCollection.Imprint(guard));
            Assert.AreEqual(1, CardCollection.Copies(guard));
            Assert.AreEqual(1, CardCollection.Blanks);
        }

        [Test]
        public void CardLevels_FollowTheBibleExample()
        {
            // A level 6 card, fully upgraded: 12; merged to rank II: 14; rank III: 17.
            strike.level = 6;
            var loadout = new Loadout();
            loadout.Upgrades[strike] = 3;
            var unit = new BattleUnit(hero, Team.Player);
            unit.ApplyLoadout(loadout);
            Assert.AreEqual(12, new CardInstance(strike, unit, 1).Level);
            Assert.AreEqual(14, new CardInstance(strike, unit, 2).Level);
            Assert.AreEqual(17, new CardInstance(strike, unit, 3).Level);
            Assert.AreEqual(1f, CardLevels.Power(4), 1e-4f, "Lv 4 = 100% of ATK");
        }

        [Test]
        public void Bindings_AreCapped_OnePerCard_AndNeedAFreeOne()
        {
            Assert.IsFalse(CardCollection.Bind(strike, CardBinding.Sun), "none found yet");
            for (int i = 0; i < Rates.maxPerBinding + 2; i++) CardCollection.AddFound(CardBinding.Sun);
            Assert.AreEqual(Rates.maxPerBinding, CardCollection.Found(CardBinding.Sun), "never more than the cap");

            Assert.IsTrue(CardCollection.Bind(strike, CardBinding.Sun));
            Assert.AreEqual(CardBinding.Sun, CardCollection.BindingOf(strike));
            Assert.AreEqual(Rates.maxPerBinding - 1, CardCollection.Free(CardBinding.Sun));

            CardCollection.AddFound(CardBinding.Moon);
            Assert.IsTrue(CardCollection.Bind(strike, CardBinding.Moon), "replacing frees the Sun");
            Assert.AreEqual(CardBinding.Moon, CardCollection.BindingOf(strike));
            Assert.AreEqual(Rates.maxPerBinding, CardCollection.Free(CardBinding.Sun));

            Assert.IsFalse(CardCollection.Bind(ult, CardBinding.Sun), "Ults can't be bound");
            CardCollection.Unbind(strike);
            Assert.AreEqual(CardBinding.None, CardCollection.BindingOf(strike));
        }

        [Test]
        public void Soul_StopsAtTheCap()
        {
            for (int i = 0; i < Rates.maxSoulLevel + 2; i++) CardCollection.AddSoul(hero);
            Assert.AreEqual(Rates.maxSoulLevel, CardCollection.SoulLevel(hero));
            Assert.IsFalse(CardCollection.CanAddSoul(hero));
        }

        [Test]
        public void Packs_OnlyTheLastCardIsSpecial_AndNoRepeats()
        {
            var rng = new System.Random(99);
            var roster = new List<CharacterDefinition> { hero };
            int distinct = CardCollection.AbilitiesOf(hero).Count;
            for (int pack = 0; pack < 200; pack++)
            {
                var pulls = CardPacks.Roll(roster, rng);
                var seen = new List<CardDefinition>();
                for (int i = 0; i < pulls.Count; i++)
                {
                    if (i < pulls.Count - 1)
                        Assert.IsTrue(pulls[i].Tier == CardPacks.Tier.Common || (i == 1 && pulls[i].Tier == CardPacks.Tier.Blank),
                            "only the last card can be rare / Soul, and only the second a blank");
                    if (pulls[i].Tier != CardPacks.Tier.Common) continue;
                    if (seen.Count < distinct) Assert.IsFalse(seen.Contains(pulls[i].Card), "no card twice in one pack");
                    seen.Add(pulls[i].Card);
                }
            }
        }

        [Test]
        public void Packs_RespectEveryCap_OverManyOpenings()
        {
            var rng = new System.Random(1234);
            var roster = new List<CharacterDefinition> { hero };
            int commons = 0;
            for (int pack = 0; pack < 400; pack++)
            {
                var pulls = CardPacks.Roll(roster, rng);
                Assert.AreEqual(Rates.cardsPerPack, pulls.Count, "a pack is always full (commons fill in)");
                foreach (var pull in pulls)
                {
                    if (pull.Tier == CardPacks.Tier.Common)
                    {
                        commons++;
                        Assert.IsFalse(pull.Card.isRitual, "Ults never come as commons");
                    }
                    CardPacks.Claim(pull);
                }
            }
            Assert.Greater(commons, 0);
            foreach (var kind in new[] { CardBinding.Sun, CardBinding.Moon, CardBinding.Star })
                Assert.LessOrEqual(CardCollection.Found(kind), Rates.maxPerBinding, kind + " cap");
            Assert.LessOrEqual(CardCollection.SoulLevel(hero), Rates.maxSoulLevel);
        }

        [Test]
        public void Loadout_CarriesUpgrades_Bindings_AndSoulPower_IntoBattle()
        {
            CardCollection.AddCopy(strike, Rates.CopiesFor(1));
            CardCollection.AddFound(CardBinding.Star);
            CardCollection.Bind(guard, CardBinding.Star);
            CardCollection.AddSoul(hero);

            var loadout = new Loadout { Cards = new List<CardDefinition> { strike, guard } };
            CardCollection.FillLoadout(hero, loadout);
            var unit = new BattleUnit(hero, Team.Player);
            unit.ApplyLoadout(loadout);

            Assert.AreEqual(1, unit.UpgradesOf(strike));
            Assert.AreEqual(0, unit.UpgradesOf(guard));
            Assert.AreEqual(CardBinding.Star, unit.BindingOf(guard));
            Assert.AreEqual(Rates.ultPowerPerSoul, unit.GetStat(StatType.RitualMight), 1e-4f, "one Soul level of Ult power");
        }

        [Test]
        public void Session_LeavesTheRealCollectionAlone_AndIgnoresCaps()
        {
            CardCollection.AddCopy(strike, 2);
            CardCollection.BeginSession();
            try
            {
                Assert.AreEqual(0, CardCollection.Copies(strike), "a session starts empty");
                for (int i = 0; i < Rates.maxPerBinding + 3; i++) Assert.IsTrue(CardCollection.AddFound(CardBinding.Sun));
                for (int i = 0; i < Rates.maxSoulLevel + 3; i++) Assert.IsTrue(CardCollection.AddSoul(hero));
                CardCollection.AddCopy(strike, 5);
                PackKind kind;
                Assert.IsTrue(CardPacks.TakePack(out kind), "packs never run out");
                Assert.AreEqual(0, WorldSession.Packs);
            }
            finally { CardCollection.EndSession(); }
            Assert.AreEqual(2, CardCollection.Copies(strike), "the real copies are untouched");
            Assert.AreEqual(0, CardCollection.Found(CardBinding.Sun));
            Assert.AreEqual(0, CardCollection.SoulLevel(hero));
        }

        [Test]
        public void Session_Rarest_PrefersSoul_ThenRare_ThenMostCopies()
        {
            CardCollection.BeginSession();
            try
            {
                CardPacks.Tier tier; CharacterDefinition member; CardBinding binding; CardDefinition card; int count;
                Assert.IsFalse(CardCollection.Rarest(out tier, out member, out binding, out card, out count), "nothing pulled");

                CardPacks.Claim(new CardPacks.Pull { Tier = CardPacks.Tier.Common, Card = strike });
                CardPacks.Claim(new CardPacks.Pull { Tier = CardPacks.Tier.Common, Card = strike });
                CardPacks.Claim(new CardPacks.Pull { Tier = CardPacks.Tier.Common, Card = guard });
                Assert.IsTrue(CardCollection.Rarest(out tier, out member, out binding, out card, out count));
                Assert.AreEqual(CardPacks.Tier.Common, tier);
                Assert.AreEqual(strike, card);
                Assert.AreEqual(2, count);

                CardPacks.Claim(new CardPacks.Pull { Tier = CardPacks.Tier.Rare, Binding = CardBinding.Moon });
                CardCollection.Rarest(out tier, out member, out binding, out card, out count);
                Assert.AreEqual(CardPacks.Tier.Rare, tier);
                Assert.AreEqual(CardBinding.Moon, binding);

                CardPacks.Claim(new CardPacks.Pull { Tier = CardPacks.Tier.Soul, Member = hero });
                CardCollection.Rarest(out tier, out member, out binding, out card, out count);
                Assert.AreEqual(CardPacks.Tier.Soul, tier);
                Assert.AreEqual(hero, member);
                Assert.AreEqual(5, CardCollection.Current.CardsPulled);
            }
            finally { CardCollection.EndSession(); }
        }

        /// <summary>The card book's arrows: 3 x 2 per page, two pages per spread (pages 0-1, 2-3, ...).</summary>
        [Test]
        public void Book_ArrowsMoveAroundTheSpread_AndAcrossPages()
        {
            Assert.AreEqual(1, CardsScreen.Neighbour(0, 1, 0), "right along the row");
            Assert.AreEqual(6, CardsScreen.Neighbour(2, 1, 0), "across the gutter to the right page's first column");
            Assert.AreEqual(12, CardsScreen.Neighbour(8, 1, 0), "off the right page onto the next spread");
            Assert.AreEqual(2, CardsScreen.Neighbour(6, -1, 0), "back across the gutter");
            Assert.AreEqual(0, CardsScreen.Neighbour(0, -1, 0), "the first card stays put");
            Assert.AreEqual(3, CardsScreen.Neighbour(0, 0, 1), "down a row");
            Assert.AreEqual(3, CardsScreen.Neighbour(3, 0, 1), "the bottom row stays put");
        }

        /// <summary>The extra slot only offers cards you own, never the unit's own locked card.</summary>
        [Test]
        public void Eligible_OnlyOwnedCards_NotTheLockedOne()
        {
            var roster = new List<CharacterDefinition> { hero };
            var eligible = CardCollection.Eligible(hero, roster);
            CollectionAssert.DoesNotContain(eligible, strike, "the locked card is always in the deck already");
            if (!Rates.startWithSecondCard) CollectionAssert.DoesNotContain(eligible, guard, "not owned yet");
            CardCollection.AddCopy(guard);
            CollectionAssert.Contains(CardCollection.Eligible(hero, roster), guard, "a copy from a pack makes it available");
        }

        [Test]
        public void Collection_SurvivesASaveRoundTrip()
        {
            CardCollection.AddCopy(strike, 4);
            CardCollection.AddFound(CardBinding.Moon);
            CardCollection.Bind(strike, CardBinding.Moon);
            WorldSession.AddPacks("", 3);
            CardCollection.AddSoul(hero);

            var data = SaveGame.Capture();
            SaveGame.Restore(null);
            Assert.AreEqual(0, CardCollection.Copies(strike));
            SaveGame.Restore(data);

            Assert.AreEqual(4, CardCollection.Copies(strike));
            Assert.AreEqual(1, CardCollection.Found(CardBinding.Moon));
            Assert.AreEqual(CardBinding.Moon, CardCollection.BindingOf(strike));
            Assert.AreEqual(3, WorldSession.Packs);
            Assert.AreEqual(1, CardCollection.SoulLevel(hero));
        }
    }
}
