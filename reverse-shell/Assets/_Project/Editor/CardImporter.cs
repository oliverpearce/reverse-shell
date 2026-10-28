using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Combat.EditorTools
{
    /// <summary>
    /// Builds the card assets from the card sheet (Planning/CARDS.xlsx). Tools/CardForge/sheet_to_json.py turns the sheet
    /// into Tools/CardForge/cards.json; this reads that file and, for every row, creates or updates its CardDefinition
    /// (name, kind, target, level and each rank's effects; icons and cast sequences are kept). It then fills the
    /// protagonist's resonance kits: each pool's cards (its Main card first) and its Ult, with the Physical cards as the
    /// card pool every resonance can use. The Packs and Areas tabs go into Resources/Cards/PackRates (the pack pictures
    /// are cut by the script). The new statuses (Burn, Chill, Frozen, Shield, Counter, Covered) are made if
    /// missing; existing status assets keep their tuning.
    ///   Menu: Reverse Shell / Import Cards From Sheet
    ///   Batch: Unity -batchmode -quit -projectPath . -executeMethod Combat.EditorTools.CardImporter.Import
    /// </summary>
    public static class CardImporter
    {
        const string Root = "Assets/_Project/Content";
        const string CardsDir = Root + "/Cards";
        const string StatusDir = Root + "/Statuses";
        const string McPath = Root + "/Characters/Char_MC.asset";
        const string RatesPath = "Assets/_Project/Resources/Cards/PackRates.asset";
        static readonly string JsonPath = Path.Combine(Application.dataPath, "../Tools/CardForge/cards.json");

        /// <summary>Kit order on the protagonist: the starting Physical Core, then the chapters' resonances.</summary>
        static readonly Resonance[] KitOrder = { Resonance.Physical, Resonance.Arc, Resonance.Root, Resonance.Cryo, Resonance.Cinder, Resonance.Prism };

#pragma warning disable 0649   // filled by JsonUtility
        [Serializable]
        class Sheet
        {
            public List<Row> cards = new List<Row>();
            public List<PackRow> packs = new List<PackRow>();
            public List<AreaRow> areas = new List<AreaRow>();
            public List<EnemyRow> enemies = new List<EnemyRow>();
        }

        [Serializable]
        class EnemyRow
        {
            public string enemy, innate, ult;
            public int levels = 3;
            public float weight = 1f;
        }

        [Serializable]
        class PackRow
        {
            public string id, name, pool;
            public int cardsPerPack = 3, packsPerWin = 1;
            public float poolShare, common, rare, epic, soulChance, rareChance, blankChance;
        }

        [Serializable] class AreaRow { public string area, pack; }

        [Serializable]
        class Row
        {
            public string id, name, pool, category, target, asset, resonance, rarity;
            public int level = 4;
            public bool main, ult, enemy;
            public List<Fx> rank1 = new List<Fx>(), rank2 = new List<Fx>(), rank3 = new List<Fx>();
        }

        [Serializable]
        class Fx
        {
            public string type, target = "card", status, bonusVs, onHit;
            public float mult = 1f, chance = 1f, bonusPerStack, lowhp;
            public int hits = 1, turns = 2, stacks = 1, triggers = 1, onHitTurns = 2, amount = 1;
            public bool consume, attacksOnly;
        }

#pragma warning restore 0649

        [MenuItem("Reverse Shell/Import Cards From Sheet")]
        public static void Import()
        {
            if (!File.Exists(JsonPath))
            {
                Debug.LogError("CardImporter: " + JsonPath + " is missing. Run: py Tools/CardForge/sheet_to_json.py");
                return;
            }
            var sheet = JsonUtility.FromJson<Sheet>(File.ReadAllText(JsonPath));
            EnsureStatuses();
            var statuses = AssetDatabase.FindAssets("t:StatusDefinition", new[] { StatusDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<StatusDefinition>(AssetDatabase.GUIDToAssetPath(g))).Where(s => s != null).ToList();
            Func<string, StatusDefinition> status = n =>
            {
                if (string.IsNullOrEmpty(n)) return null;
                var s = statuses.Find(x => string.Equals(x.DisplayName, n, StringComparison.OrdinalIgnoreCase) || x.name == "Status_" + n.Replace(" ", ""));
                if (s == null) throw new Exception("CardImporter: no status called '" + n + "' in " + StatusDir);
                return s;
            };

            var built = new List<KeyValuePair<Row, CardDefinition>>();
            foreach (var row in sheet.cards)
                built.Add(new KeyValuePair<Row, CardDefinition>(row, Build(row, status)));
            FillKits(built);
            FillPacks(sheet);
            FillEnemies(sheet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CardImporter: " + built.Count + " cards imported from " + JsonPath);
        }

        static CardDefinition Build(Row row, Func<string, StatusDefinition> status)
        {
            string path = CardsDir + "/" + row.asset + ".asset";
            var card = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
            bool fresh = card == null;
            if (fresh) card = ScriptableObject.CreateInstance<CardDefinition>();
            card.displayName = row.name;
            card.kind = (CardKind)Enum.Parse(typeof(CardKind), row.category);
            card.targetMode = (TargetMode)Enum.Parse(typeof(TargetMode), row.target);
            card.isRitual = row.ult;
            card.level = Mathf.Clamp(row.level, 1, 20);
            card.pool = (Resonance)Enum.Parse(typeof(Resonance), row.ult ? row.resonance : row.pool);
            card.rarity = string.IsNullOrEmpty(row.rarity) ? CardRarity.Common : (CardRarity)Enum.Parse(typeof(CardRarity), row.rarity);
            var ranks = row.ult ? new[] { row.rank1 } : new[] { row.rank1, row.rank2, row.rank3 };
            card.ranks = ranks.Select(fx => new CardRank { effects = fx.Select(f => Effect(f, status)).ToList() }).ToList();
            if (fresh) AssetDatabase.CreateAsset(card, path);
            else EditorUtility.SetDirty(card);
            return card;
        }

        static CardEffect Effect(Fx f, Func<string, StatusDefinition> status)
        {
            CardEffect e;
            switch (f.type)
            {
                case "dmg":
                    e = new DamageEffect { multiplier = f.mult, hits = Mathf.Max(1, f.hits), bonusVs = status(f.bonusVs), bonusPerStack = f.bonusPerStack, consumeBonus = f.consume };
                    break;
                case "heal": e = new HealEffect { multiplier = f.mult }; break;
                case "status": e = new ApplyStatusEffect { status = status(f.status), duration = Mathf.Max(1, f.turns), stacks = Mathf.Max(1, f.stacks), chance = f.chance }; break;
                case "shield": e = new ShieldEffect { status = status("Shield"), multiplier = f.mult, duration = Mathf.Max(1, f.turns) }; break;
                case "counter":
                    e = new CounterEffect { status = status("Counter"), multiplier = f.mult, triggers = Mathf.Max(1, f.triggers), lowHPBonus = f.lowhp, onHit = status(f.onHit), onHitTurns = Mathf.Max(1, f.onHitTurns) };
                    break;
                case "cover": e = new CoverEffect { status = status("Covered"), duration = Mathf.Max(1, f.turns) }; break;
                case "interrupt": e = new InterruptEffect { attacksOnly = f.attacksOnly }; break;
                case "cleanse": e = new CleanseEffect(); break;
                case "ult": e = new GainMoxieEffect { amount = f.amount }; break;
                case "ap": e = new ModifyAPEffect { amount = f.amount }; break;
                default: throw new Exception("CardImporter: unknown effect '" + f.type + "'");
            }
            // A heal / Ult point / cleanse with no target of its own follows the card's target (their defaults differ).
            e.target = Target(f.target);
            return e;
        }

        static EffectTarget Target(string t)
        {
            switch (t)
            {
                case "self": return EffectTarget.Self;
                case "enemy": return EffectTarget.SingleEnemy;
                case "enemies": return EffectTarget.AllEnemies;
                case "random": return EffectTarget.RandomEnemy;
                case "ally": return EffectTarget.SingleAlly;
                case "allies": return EffectTarget.AllAllies;
                case "randomally": return EffectTarget.RandomAlly;
                default: return EffectTarget.CardTargets;
            }
        }

        static void FillKits(List<KeyValuePair<Row, CardDefinition>> built)
        {
            var mc = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(McPath);
            if (mc == null) { Debug.LogWarning("CardImporter: " + McPath + " not found; kits not filled."); return; }
            var kits = new List<ResonanceKit>();
            foreach (var element in KitOrder)
            {
                string pool = element.ToString();
                var old = mc.KitFor(element);
                var rows = built.Where(b => !b.Key.ult && b.Key.pool == pool).ToList();
                var main = rows.Where(b => b.Key.main).Select(b => b.Value).FirstOrDefault() ?? rows.Select(b => b.Value).FirstOrDefault();
                var ult = built.Where(b => b.Key.ult && !b.Key.enemy && b.Key.resonance == pool).Select(b => b.Value).FirstOrDefault();
                var kit = new ResonanceKit { element = element, ultimate = ult != null ? ult : old != null ? old.ultimate : null };
                if (main != null) kit.cards.Add(main);
                // Physical's kit is just its Main card: the rest are the card pool, usable under every resonance.
                if (element != Resonance.Physical)
                    foreach (var b in rows) if (b.Value != main) kit.cards.Add(b.Value);
                kits.Add(kit);
            }
            mc.resonances = kits;
            var physical = built.Where(b => !b.Key.ult && b.Key.pool == "Physical").Select(b => b.Value).ToList();
            mc.cardPool = physical;
            if (kits[0].cards.Count > 0) mc.incantations = new List<CardDefinition> { kits[0].cards[0] };
            if (kits[0].ultimate != null) mc.ritual = kits[0].ultimate;
            EditorUtility.SetDirty(mc);
        }

        static void FillPacks(Sheet sheet)
        {
            var rates = AssetDatabase.LoadAssetAtPath<PackRates>(RatesPath);
            if (rates == null) { Debug.LogWarning("CardImporter: " + RatesPath + " not found; packs not filled."); return; }
            rates.packs = sheet.packs.Select(p => new PackKind
            {
                id = p.id, displayName = p.name, pool = (Resonance)Enum.Parse(typeof(Resonance), p.pool),
                cardsPerPack = Mathf.Max(1, p.cardsPerPack), packsPerWin = Mathf.Max(0, p.packsPerWin), poolShare = p.poolShare,
                commonShare = p.common, rareShare = p.rare, epicShare = p.epic,
                soulChance = p.soulChance, arcanaChance = p.rareChance, blankChance = p.blankChance
            }).ToList();
            rates.areaPacks = sheet.areas.Select(a => new AreaPack { area = a.area, pack = a.pack }).ToList();
            EditorUtility.SetDirty(rates);
        }

        /// <summary>The Enemies tab: each enemy's innate card (a Physical card), its level bonus and weight, and its Ult.</summary>
        static void FillEnemies(Sheet sheet)
        {
            foreach (var row in sheet.enemies)
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(Root + "/Enemies/" + row.enemy + ".asset");
                if (enemy == null) { Debug.LogWarning("CardImporter: no enemy " + row.enemy); continue; }
                enemy.innate = string.IsNullOrEmpty(row.innate) ? null : AssetDatabase.LoadAssetAtPath<CardDefinition>(CardsDir + "/" + row.innate + ".asset");
                enemy.innateLevels = Mathf.Max(0, row.levels);
                enemy.innateWeight = Mathf.Max(0f, row.weight);
                if (!string.IsNullOrEmpty(row.ult))
                    enemy.ritual = AssetDatabase.LoadAssetAtPath<CardDefinition>(CardsDir + "/" + row.ult + ".asset") ?? enemy.ritual;
                EditorUtility.SetDirty(enemy);
            }
        }

        // ---------------------------------------------------------------- Statuses

        static void EnsureStatuses()
        {
            var frozen = MakeStatus("Status_Frozen", "Frozen", "FRZ", new Color(0.55f, 0.85f, 1f), true, StackRule.RefreshDuration, 1,
                "Can't act. Takes 15% more damage.", s =>
                {
                    s.preventsAction = true;
                    s.modifiers.Add(new StatModifier(StatType.DamageTaken, ModifierKind.Flat, 0.15f));
                });
            MakeStatus("Status_Chill", "Chill", "CHL", new Color(0.62f, 0.88f, 1f), true, StackRule.AddStacks, 3,
                "-5% ATK per stack. At 3 stacks it becomes Frozen.", s =>
                {
                    s.verb = "chill";
                    s.modifiers.Add(new StatModifier(StatType.Attack, ModifierKind.Percent, -0.05f));
                    s.becomesAtMaxStacks = frozen;
                    s.becomesTurns = 1;
                });
            MakeStatus("Status_Burn", "Burn", "BRN", new Color(1f, 0.5f, 0.22f), true, StackRule.AddStacks, 5,
                "Loses 12% of the burner's ATK each turn, per stack.", s => { s.verb = "burn"; s.damagePerRoundSourceATK = 0.12f; });
            MakeStatus("Status_Shield", "Shield", "SHD", new Color(0.55f, 0.8f, 1f), false, StackRule.Replace, 1,
                "Soaks card damage before HP this turn.", s => s.special = StatusSpecial.Shield);
            MakeStatus("Status_Counter", "Counter", "CTR", new Color(0.95f, 0.75f, 0.3f), false, StackRule.AddStacks, 9,
                "Hits back when attacked this turn.", s => s.special = StatusSpecial.Counter);
            MakeStatus("Status_Covered", "Covered", "CVR", new Color(0.7f, 0.8f, 0.95f), false, StackRule.RefreshDuration, 1,
                "An ally takes the hits aimed at this unit this turn.", s => s.special = StatusSpecial.Cover);
        }

        static StatusDefinition MakeStatus(string file, string display, string label, Color color, bool debuff, StackRule rule,
            int maxStacks, string description, Action<StatusDefinition> setup)
        {
            string path = StatusDir + "/" + file + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<StatusDefinition>(path);
            if (existing != null) return existing;
            var s = ScriptableObject.CreateInstance<StatusDefinition>();
            s.displayName = display;
            s.shortLabel = label;
            s.color = color;
            s.isDebuff = debuff;
            s.stackRule = rule;
            s.maxStacks = maxStacks;
            s.description = description;
            setup(s);
            AssetDatabase.CreateAsset(s, path);
            return s;
        }
    }
}
