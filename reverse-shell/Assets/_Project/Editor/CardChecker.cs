using System.Collections.Generic;
using System.Linq;
using Combat.View;
using UnityEditor;
using UnityEngine;

namespace Combat.EditorTools
{
    /// <summary>
    /// Looks over every card and the pack numbers for mistakes that don't stop the game but show up as odd cards or
    /// packs: ability cards with no icon or no effects, two cards with the same name, kit cards that no pack gives,
    /// rarity shares that don't add up to 100%, a rarity a pack can roll but its set has no cards of, unknown area
    /// packs, and copy upgrades out of order. Each problem is a warning in the Console (click it to select the asset).
    ///   Menu: Reverse Shell / Check Cards (a summary pops up)
    ///   It also runs after every card import (<see cref="CardImporter"/>), in the Console only.
    /// </summary>
    public static class CardChecker
    {
        const string CardsDir = "Assets/_Project/Content/Cards";
        const string CharactersDir = "Assets/_Project/Content/Characters";

        class Problem
        {
            public string Text;
            public Object Asset;
        }

        [MenuItem("Reverse Shell/Check Cards")]
        public static void CheckFromMenu()
        {
            var problems = Check();
            EditorUtility.DisplayDialog("Check Cards", problems.Count == 0 ? "No problems found."
                : problems.Count + " problem(s) found; each one is a warning in the Console (click it to select the asset).\n\n"
                  + string.Join("\n", problems.Take(8).Select(p => "• " + p.Text).ToArray()) + (problems.Count > 8 ? "\n…" : ""), "OK");
        }

        /// <summary>Runs every check and logs each problem; returns them.</summary>
        static List<Problem> Check()
        {
            var problems = new List<Problem>();
            var cards = AssetDatabase.FindAssets("t:CardDefinition", new[] { CardsDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<CardDefinition>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c != null).ToList();
            CheckCards(cards, problems);
            var kitCards = KitCards();
            CheckPacks(PackRates.Current, kitCards, problems);
            foreach (var p in problems) Debug.LogWarning("Check Cards: " + p.Text, p.Asset);
            Debug.Log("Check Cards: " + cards.Count + " cards, " + PackRates.Current.packs.Count + " packs, "
                + (problems.Count == 0 ? "no problems." : problems.Count + " problem(s) (warnings above)."));
            return problems;
        }

        /// <summary>After an import: the same checks, in the Console only.</summary>
        public static void CheckAfterImport() { Check(); }

        static void CheckCards(List<CardDefinition> cards, List<Problem> problems)
        {
            foreach (var group in cards.GroupBy(c => c.DisplayName.Trim().ToLowerInvariant()).Where(g => g.Count() > 1))
                problems.Add(new Problem
                {
                    Text = "the name '" + group.First().DisplayName + "' is used by " + string.Join(", ", group.Select(c => c.name).ToArray()),
                    Asset = group.First(),
                });
            foreach (var card in cards)
            {
                if (string.IsNullOrEmpty(card.displayName)) problems.Add(new Problem { Text = card.name + " has no display name", Asset = card });
                // Ult (Soul) cards show the character's sprite instead of an icon; the others' come from the icon sheet
                // (Resources/Cards/CardArt, by asset name), or the card's own icon if one is set.
                if (!card.isRitual && card.icon == null && CardArt.Icon(card) == null)
                    problems.Add(new Problem { Text = card.name + " has no icon in Resources/Cards/CardArt", Asset = card });
                var first = card.GetRank(1);
                if (first == null || first.effects == null || first.effects.Count(e => e != null) == 0)
                    problems.Add(new Problem { Text = card.name + " has no effects at rank I", Asset = card });
                else if (first.effects.Any(e => e == null))
                    problems.Add(new Problem { Text = card.name + " has an empty effect slot at rank I", Asset = card });
                if (!card.isRitual && card.ranks.Count < 3)
                    problems.Add(new Problem { Text = card.name + " has " + card.ranks.Count + " rank(s), not 3", Asset = card });
            }
        }

        /// <summary>Every ability card on a resonance kit (what packs can give), with the kit's owner.</summary>
        static List<KeyValuePair<CardDefinition, CharacterDefinition>> KitCards()
        {
            var list = new List<KeyValuePair<CardDefinition, CharacterDefinition>>();
            foreach (var g in AssetDatabase.FindAssets("t:CharacterDefinition", new[] { CharactersDir }))
            {
                var member = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(AssetDatabase.GUIDToAssetPath(g));
                if (member == null || member.resonances == null) continue;
                foreach (var kit in member.resonances)
                    if (kit != null)
                        foreach (var card in kit.cards)
                            if (card != null && list.All(e => e.Key != card)) list.Add(new KeyValuePair<CardDefinition, CharacterDefinition>(card, member));
            }
            return list;
        }

        static void CheckPacks(PackRates rates, List<KeyValuePair<CardDefinition, CharacterDefinition>> kitCards, List<Problem> problems)
        {
            if (rates.packs.Count == 0) { problems.Add(new Problem { Text = "PackRates has no packs", Asset = rates }); return; }
            foreach (var group in rates.packs.Where(p => p != null).GroupBy(p => p.id).Where(g => g.Count() > 1))
                problems.Add(new Problem { Text = "pack id '" + group.Key + "' is used " + group.Count() + " times", Asset = rates });
            foreach (var entry in kitCards)
                if (!rates.packs.Any(p => p != null && p.pool == entry.Key.pool))
                    problems.Add(new Problem { Text = entry.Key.name + " (" + entry.Key.pool + ", on " + entry.Value.name + "'s kit) is in no pack", Asset = entry.Key });
            foreach (var kind in rates.packs)
            {
                if (kind == null) continue;
                string where = "pack " + kind.id + ": ";
                float shares = kind.commonShare + kind.rareShare + kind.epicShare + kind.legendaryShare;
                if (Mathf.Abs(shares - 1f) > 0.01f)
                    problems.Add(new Problem { Text = where + "Common + Rare + Epic + Legendary shares add up to " + Mathf.RoundToInt(shares * 100f) + "%, not 100% (packs scale them, so each tier's real odds differ from the sheet)", Asset = rates });
                if (kind.soulChance + kind.arcanaChance > 1f)
                    problems.Add(new Problem { Text = where + "Soul + Sun / Moon / Star chances are over 100%", Asset = rates });
                // A rarity the pack can roll, with no card of it in the pack's set: those rolls can't give a set card.
                var set = kitCards.Select(e => e.Key).Where(c => c.pool == kind.pool).ToList();
                if (set.Count == 0) { problems.Add(new Problem { Text = where + "its set (" + kind.pool + ") has no cards on any kit", Asset = rates }); continue; }
                Missing(where, kind.commonShare, CardRarity.Common, set, rates, problems);
                Missing(where, kind.rareShare, CardRarity.Rare, set, rates, problems);
                Missing(where, kind.epicShare, CardRarity.Epic, set, rates, problems);
                Missing(where, kind.legendaryShare, CardRarity.Legendary, set, rates, problems);
            }
            foreach (var area in rates.areaPacks)
                if (area != null && !string.IsNullOrEmpty(area.pack) && area.pack != PackRates.RandomPack && rates.Pack(area.pack) == null)
                    problems.Add(new Problem { Text = "area '" + area.area + "' gives pack '" + area.pack + "', which doesn't exist", Asset = rates });
            if (rates.upgradeCopies != null)
                for (int i = 0; i < rates.upgradeCopies.Length; i++)
                    if (rates.upgradeCopies[i] < 1 || (i > 0 && rates.upgradeCopies[i] < rates.upgradeCopies[i - 1]))
                    {
                        problems.Add(new Problem { Text = "copy upgrades should be 1 or more and go up (" + string.Join(", ", rates.upgradeCopies.Select(n => n.ToString()).ToArray()) + ")", Asset = rates });
                        break;
                    }
        }

        static void Missing(string where, float share, CardRarity rarity, List<CardDefinition> set, PackRates rates, List<Problem> problems)
        {
            if (share > 0f && !set.Any(c => c.rarity == rarity))
                problems.Add(new Problem { Text = where + Mathf.RoundToInt(share * 100f) + "% " + rarity + " share, but its set has no " + rarity + " cards (those rolls give another rarity)", Asset = rates });
        }
    }
}
