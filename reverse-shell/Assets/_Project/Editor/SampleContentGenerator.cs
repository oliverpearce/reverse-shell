using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Combat.EditorTools
{
    /// <summary>
    /// Tools > Combat > Generate Sample Content. Creates whatever is missing of
    /// the main party (MC the Prototype, Ashe and Lucy), two machine enemies, the Parts and their catalog,
    /// cards/statuses/animations and a Battle scene. Sprites come from Assets/_Project/Art
    /// (Tools/SpriteForge builds the cast sheets, Tools/PortraitForge the character-card portraits).
    /// All three party members are original robots. Non-commercial fan project.
    /// Everything it makes is plain data you can then edit in the inspector. Assets that already exist are never
    /// touched, so your edits are safe; only Tools > Combat > Rebuild Sample Content (it asks first) overwrites them.
    /// </summary>
    public static class SampleContentGenerator
    {
        const string Root = "Assets/_Project/Content";
        const string ScenePath = "Assets/_Project/Scenes/Battle.unity";
        const string CharacterArt = "Assets/_Project/Art/Characters/";
        const string PortraitArt = "Assets/_Project/Art/Portraits/";
        public const string PartCatalogPath = Root + "/Parts/PartCatalog.asset";

        // Rebuild mode: existing assets are overwritten with the generator's values (GUIDs kept).
        static bool overwrite;
        // Assets made (or rebuilt) during this run; only these get their follow-up settings.
        static readonly HashSet<Object> fresh = new HashSet<Object>();
        static int kept;
        static bool running;

        [MenuItem("Tools/Combat/Generate Sample Content")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            fresh.Clear();
            kept = 0;
            running = true;
            try { GenerateAll(); }
            finally { running = false; }
        }

        static void GenerateAll()
        {

            AssetDatabase.Refresh(); // pick up sprite sheets written by Tools/SpriteForge
            foreach (var folder in new[] { "Cards", "Characters", "Enemies", "Statuses", "Sequences", "Animations", "Parts" })
                EnsureFolder(Root + "/" + folder);
            DeleteLegacyAssets();
            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));

            var config = Save(ScriptableObject.CreateInstance<CombatConfig>(), Root + "/CombatConfig.asset");

            // ---------------------------------------------------------------- Statuses
            var mightUp = Status("Status_IncantationMightUp", "Incantation Might Up", "IM", new Color(0.95f, 0.8f, 0.35f), false,
                StackRule.AddStacks, 3, "+12% incantation damage per stack.", 0f, 0f, new StatModifier(StatType.IncantationMight, ModifierKind.Flat, 0.12f));
            var atkUp = Status("Status_AtkUp", "ATK Up", "ATK", new Color(0.95f, 0.45f, 0.35f), false,
                StackRule.AddStacks, 3, "+15% ATK per stack.", 0f, 0f, new StatModifier(StatType.Attack, ModifierKind.Percent, 0.15f));
            var defDown = Status("Status_PhysicalDefDown", "Physical DEF Down", "DEF", new Color(0.6f, 0.45f, 0.9f), true,
                StackRule.RefreshDuration, 1, "-25% Physical DEF.", 0f, 0f, new StatModifier(StatType.PhysicalDefense, ModifierKind.Percent, -0.25f));
            var poison = Status("Status_Poison", "Poison", "PSN", new Color(0.5f, 0.85f, 0.3f), true,
                StackRule.AddStacks, 3, "Lose 3% max HP per stack at round end.", 0.03f, 0f);
            if (Fresh(poison))
            {
                poison.verb = "poison"; // card text: "deal 120% ATK damage and poison 1 enemy (x1, 2 turns)"
                EditorUtility.SetDirty(poison);
            }
            var regen = Status("Status_Regen", "Regeneration", "RGN", new Color(0.4f, 0.95f, 0.6f), false,
                StackRule.RefreshDuration, 1, "Recover 5% max HP at round end.", 0f, 0.05f);

            // ---------------------------------------------------------------- Cast sequences
            var seqStrike = Sequence("Seq_Strike",
                new LungeStep(),
                new FlashStep { at = StepTarget.Targets, color = Color.white, duration = 0.15f });
            var seqBolt = Sequence("Seq_Bolt",
                new PulseStep { at = StepTarget.Caster },
                new ProjectileStep { useCasterColor = true },
                new FlashStep { at = StepTarget.Targets, useCasterColor = true, duration = 0.2f });
            var seqSweep = Sequence("Seq_Sweep",
                new LungeStep { distance = 140f, duration = 0.4f },
                new ScreenShakeStep { strength = 8f, duration = 0.2f, runInParallel = true },
                new FlashStep { at = StepTarget.Targets, color = Color.white, duration = 0.2f });
            var seqSupport = Sequence("Seq_Support",
                new PulseStep { at = StepTarget.Caster, amount = 0.15f },
                new FlashStep { at = StepTarget.Targets, useCasterColor = true, duration = 0.35f });
            var seqRitual = Sequence("Seq_Ritual",
                new FlashStep { at = StepTarget.Caster, color = new Color(1f, 0.8f, 0.3f), duration = 0.4f },
                new PulseStep { at = StepTarget.Caster, amount = 0.2f, duration = 0.35f },
                new ProjectileStep { color = new Color(1f, 0.85f, 0.4f), useCasterColor = false, size = 60f, duration = 0.3f },
                new ScreenShakeStep { strength = 16f, duration = 0.35f, runInParallel = true },
                new FlashStep { at = StepTarget.Targets, color = new Color(1f, 0.9f, 0.6f), duration = 0.3f });

            var atkDown = Status("Status_AtkDown", "ATK Down", "ATK", new Color(0.72f, 0.42f, 0.95f), true,
                StackRule.RefreshDuration, 1, "-20% ATK.", 0f, 0f, new StatModifier(StatType.Attack, ModifierKind.Percent, -0.2f));

            // ---------------------------------------------------------------- MC, the main character (Physical, Arc)
            // Party leader and the standout model. Glows white for now; it will channel several elements later
            // (channels + ResonanceInfo.GlowFor). A heavy single-target strike plus a team damage buff.
            var mc1 = Card("Card_MC_EdgeStrike", "Edge Strike", CardKind.Attack, TargetMode.SingleEnemy, false, seqStrike,
                new CardEffect[] { new DamageEffect { multiplier = 1.8f } },
                new CardEffect[] { new DamageEffect { multiplier = 2.7f } },
                new CardEffect[] { new DamageEffect { multiplier = 4.5f } });
            var mc2 = Card("Card_MC_SyncField", "Sync Field", CardKind.Buff, TargetMode.AllAllies, false, seqSupport,
                new CardEffect[] { new ApplyStatusEffect { status = mightUp, stacks = 1, duration = 2 } },
                new CardEffect[] { new ApplyStatusEffect { status = mightUp, stacks = 2, duration = 2 } },
                new CardEffect[]
                {
                    new ApplyStatusEffect { status = mightUp, stacks = 3, duration = 2 },
                    new GainMoxieEffect { target = EffectTarget.RandomAlly, amount = 1 }
                });
            var mc3 = Card("Card_MC_CrossSlash", "Cross Slash", CardKind.Attack, TargetMode.AllEnemies, false, seqSweep,
                new CardEffect[] { new DamageEffect { multiplier = 1f } },
                new CardEffect[] { new DamageEffect { multiplier = 1.5f } },
                new CardEffect[] { new DamageEffect { multiplier = 2.4f } });
            var mcUlt = Card("Card_MC_EdgeBreaker", "Edge Breaker", CardKind.Attack, TargetMode.SingleEnemy, true, seqRitual,
                new CardEffect[]
                {
                    new DamageEffect { multiplier = 6f, hits = 2 },
                    new ApplyStatusEffect { target = EffectTarget.AllAllies, status = atkUp, stacks = 2, duration = 2 }
                });
            var mc = Character("Char_MC", "MC", "Prototype", new Color(0.92f, 0.92f, 0.95f), Resonance.Arc, DamageType.Physical,
                2200, 240f, 130f, 130f, new[] { mc1, mc2 }, mcUlt, AnimSet("MC"));
            if (Fresh(mc))
            {
                mc.cardPool = new List<CardDefinition> { mc3 };
                mc.neutralGlow = true;
                mc.glowColor = new Color(1f, 0.55f, 0.18f); // orange, matching the sprite's glow pads
                mc.statBarReference = true; // crew-menu stat bars: MC at level 30 fills 90%
                mc.partSockets = new List<Vector2>(); // all crew cards share the reference knight's layout (CharacterMenu.SocketSpots)
                mc.channels = new List<Resonance> { Resonance.Arc, Resonance.Root, Resonance.Cryo, Resonance.Cinder, Resonance.Prism };
                Portraits(mc, "MC");
            }

            // ---------------------------------------------------------------- Ashe (Earth, Special)
            // The shared robot model, overgrown like a dryad. A tanky healer.
            var heal1 = Card("Card_Ashe_SapMend", "Sap Mend", CardKind.Heal, TargetMode.SingleAlly, false, seqSupport,
                new CardEffect[] { new HealEffect { target = EffectTarget.CardTargets, multiplier = 1.4f } },
                new CardEffect[] { new HealEffect { target = EffectTarget.CardTargets, multiplier = 2.1f } },
                new CardEffect[]
                {
                    new HealEffect { target = EffectTarget.CardTargets, multiplier = 3.2f },
                    new ApplyStatusEffect { target = EffectTarget.CardTargets, status = regen, duration = 2 }
                });
            var heal2 = Card("Card_Ashe_BrambleLash", "Bramble Lash", CardKind.Debuff, TargetMode.SingleEnemy, false, seqStrike,
                new CardEffect[] { new DamageEffect { multiplier = 1.2f }, new ApplyStatusEffect { status = defDown, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 1.8f }, new ApplyStatusEffect { status = defDown, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 2.9f }, new ApplyStatusEffect { status = defDown, duration = 3 } });
            var heal3 = Card("Card_Ashe_Overgrowth", "Overgrowth", CardKind.Buff, TargetMode.AllAllies, false, seqSupport,
                new CardEffect[] { new ApplyStatusEffect { status = regen, duration = 2 } },
                new CardEffect[] { new ApplyStatusEffect { status = regen, duration = 3 } },
                new CardEffect[] { new ApplyStatusEffect { status = regen, duration = 4 } });
            var healUlt = Card("Card_Ashe_VerdantBloom", "Verdant Bloom", CardKind.Heal, TargetMode.AllAllies, true, seqRitual,
                new CardEffect[]
                {
                    new HealEffect { target = EffectTarget.CardTargets, multiplier = 2.5f },
                    new ApplyStatusEffect { target = EffectTarget.CardTargets, status = regen, duration = 3 }
                });
            var ashe = Character("Char_Ashe", "Ashe", "Druid", new Color(0.5f, 0.86f, 0.45f), Resonance.Root, DamageType.Special,
                2800, 190f, 160f, 150f, new[] { heal1, heal2 }, healUlt, AnimSet("Ashe"));
            if (Fresh(ashe))
            {
                ashe.cardPool = new List<CardDefinition> { heal3 };
                Portraits(ashe, "Ashe");
            }

            // ---------------------------------------------------------------- Lucy (Arc, Special)
            // The same robot model with purple accents. Viruses: damage over time and debuffs.
            var lucy1 = Card("Card_Lucy_VirusSpike", "Virus Spike", CardKind.Attack, TargetMode.SingleEnemy, false, seqBolt,
                new CardEffect[] { new DamageEffect { multiplier = 1.2f }, new ApplyStatusEffect { status = poison, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 1.8f }, new ApplyStatusEffect { status = poison, stacks = 2, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 2.8f }, new ApplyStatusEffect { status = poison, stacks = 3, duration = 3 } });
            var lucy2 = Card("Card_Lucy_Corrupt", "Corrupt", CardKind.Debuff, TargetMode.SingleEnemy, false, seqBolt,
                new CardEffect[] { new DamageEffect { multiplier = 1f }, new ApplyStatusEffect { status = atkDown, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 1.5f }, new ApplyStatusEffect { status = atkDown, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 2.4f }, new ApplyStatusEffect { status = atkDown, duration = 3 } });
            var lucy3 = Card("Card_Lucy_Worm", "Worm", CardKind.Attack, TargetMode.RandomEnemy, false, seqBolt,
                new CardEffect[] { new DamageEffect { multiplier = 0.5f, hits = 3 }, new ApplyStatusEffect { status = poison, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 0.75f, hits = 3 }, new ApplyStatusEffect { status = poison, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 1.2f, hits = 3 }, new ApplyStatusEffect { status = poison, stacks = 2, duration = 3 } });
            var lucyUlt = Card("Card_Lucy_ZeroDay", "Zero Day", CardKind.Attack, TargetMode.AllEnemies, true, seqRitual,
                new CardEffect[]
                {
                    new DamageEffect { multiplier = 3f },
                    new ApplyStatusEffect { status = poison, stacks = 2, duration = 3 },
                    new ApplyStatusEffect { status = defDown, duration = 2 }
                });
            var lucy = Character("Char_Lucy", "Lucy", "Hacker", new Color(0.72f, 0.45f, 0.95f), Resonance.Arc, DamageType.Special,
                2100, 215f, 120f, 150f, new[] { lucy1, lucy2 }, lucyUlt, AnimSet("Lucy"));
            if (Fresh(lucy))
            {
                lucy.cardPool = new List<CardDefinition> { lucy3 };
                Portraits(lucy, "Lucy");
            }
            foreach (var c in new[] { mc, ashe, lucy }) EditorUtility.SetDirty(c);

            // ---------------------------------------------------------------- Parts (internal slots on the character card)
            var parts = new List<PartDefinition>
            {
                // The Collector's: the only Part found in Chapter One. Cores never drop: the protagonist's own Core
                // binds with the Soul Card, so nothing replaces it.
                Part("Part_UltChip", "Ult Chip", PartSlot.Chip, new Color(1f, 0.82f, 0.4f), "Start every battle with 2 Ult points.",
                    new StatModifier(StatType.StartingMoxie, ModifierKind.Flat, 2f)),
                Part("Part_PatchCache", "Patch Cache", PartSlot.Chip, new Color(0.45f, 0.95f, 0.65f), "+12% healing.",
                    new StatModifier(StatType.HealingBonus, ModifierKind.Flat, 0.12f)),
            };
            var fragmented = FragmentedCore();
            var resonant = ResonantCore();
            parts.Add(fragmented);
            parts.Add(resonant);
            var catalog = Save(ScriptableObject.CreateInstance<PartCatalog>(), PartCatalogPath);
            if (Fresh(catalog))
            {
                catalog.parts = parts;
                catalog.starterParts = new List<PartDefinition>();   // a new game starts with no spare Parts
                catalog.starterCore = fragmented;                     // only the protagonist's own, damaged Core
                catalog.boundCore = resonant;                         // ...which the Soul Card turns into this
            }
            else
            {
                // An existing catalog only gains Parts that were just made.
                foreach (var added in parts)
                    if (Fresh(added) && !catalog.parts.Contains(added)) catalog.parts.Add(added);
            }
            EditorUtility.SetDirty(catalog);

            // ---------------------------------------------------------------- Enemies (machine lifeforms)
            var goldenHammer = Card("Card_Enemy_GoldenHammer", "Golden Hammer", CardKind.Attack, TargetMode.SingleEnemy, false, seqStrike,
                new CardEffect[] { new DamageEffect { multiplier = 1.6f } });
            var reinforce = Card("Card_Enemy_Reinforce", "Reinforce", CardKind.Buff, TargetMode.Self, false, seqSupport,
                new CardEffect[] { new ApplyStatusEffect { status = atkUp, duration = 3 } });
            var bite = Card("Card_Enemy_CorrodedBlade", "Corroded Blade", CardKind.Attack, TargetMode.SingleEnemy, false, seqStrike,
                new CardEffect[] { new DamageEffect { multiplier = 1.3f }, new ApplyStatusEffect { status = poison, duration = 2 } });
            var howl = Card("Card_Enemy_NetworkLink", "Network Link", CardKind.Buff, TargetMode.AllAllies, false, seqSupport,
                new CardEffect[] { new ApplyStatusEffect { status = atkUp, duration = 2 } });

            // Tuned as an introductory fight: roughly 6-8 rounds, enemies deal ~350 HP/round into ~7000 party HP.
            var sentry = Enemy("Enemy_RustSentry", "Rust Sentry", new Color(0.66f, 0.46f, 0.3f), Resonance.Cinder, DamageType.Physical,
                4200, 150f, 140f, 110f, AnimSet("RustSentry"),
                new EnemySkill { card = goldenHammer, weight = 2f },
                new EnemySkill { card = reinforce, weight = 0.5f });
            if (Fresh(sentry)) sentry.xpReward = 120;
            var stalker = Enemy("Enemy_ScrapStalker", "Scrap Stalker", new Color(0.5f, 0.58f, 0.32f), Resonance.Root, DamageType.Special,
                2600, 135f, 90f, 90f, AnimSet("ScrapStalker"),
                new EnemySkill { card = bite, weight = 2f },
                new EnemySkill { card = howl, weight = 0.6f });
            if (Fresh(stalker)) stalker.xpReward = 80;
            EditorUtility.SetDirty(sentry);
            EditorUtility.SetDirty(stalker);

            // ---------------------------------------------------------------- Setup + scene
            // ---------------------------------------------------------------- Backdrop (painted from the overworld atlas)
            const string atlasPath = "Assets/_Project/Art/Tiles/ruins.png";
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate); // applies Read/Write from PixelArtImporter
            var backdrop = ScriptableObject.CreateInstance<BattleBackdropDefinition>();
            backdrop.displayName = "Mossline Station";
            backdrop.tileAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            backdrop = Save(backdrop, Root + "/Backdrop_MosslineStation.asset");

            var setup = ScriptableObject.CreateInstance<BattleSetup>();
            setup.config = config;
            setup.backdrop = backdrop;
            setup.party = new List<CharacterDefinition> { mc, ashe, lucy };
            setup.enemies = new List<EnemyDefinition> { sentry, stalker };
            setup = Save(setup, Root + "/Battle_Sample.asset");
            EnsureBlue();
            EnsureChapterOne();
            EnsurePackRates();

            AssetDatabase.SaveAssets();
            if (overwrite || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateScene(setup);

            Selection.activeObject = mc;
            EditorUtility.DisplayDialog("Combat", (overwrite ? "Sample content rebuilt." : "Sample content generated: "
                + fresh.Count + " assets made, " + kept + " existing ones left as they were.")
                + "\n\nScene: " + ScenePath + "\nPress Play to fight!", "OK");
        }

        [MenuItem("Tools/Combat/Rebuild Sample Content (overwrites edits)")]
        static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog("Rebuild Sample Content",
                    "This puts every generated card, character, enemy, status and Part back to the generator's values, "
                    + "and rebuilds the Battle scene.\n\nAny changes you made to them in the Inspector will be lost.",
                    "Overwrite my edits", "Cancel"))
                return;
            overwrite = true;
            try { Generate(); }
            finally { overwrite = false; }
        }

        /// <summary>True if the asset was made (or rebuilt) during this run, so its generator settings may be applied.</summary>
        static bool Fresh(Object asset)
        {
            return asset != null && fresh.Contains(asset);
        }

        /// <summary>A standalone run (the bootstrap) starts its own list of fresh assets.</summary>
        static void BeginStandalone()
        {
            if (running) return;
            fresh.Clear();
            kept = 0;
        }

        [MenuItem("Tools/Combat/Open Battle Scene")]
        public static void OpenBattleScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("Combat", "No battle scene yet. Run Tools > Combat > Generate Sample Content first.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        }

        // -------------------------------------------------------------------- Blue (the backup)

        public const string BluePath = Root + "/Characters/Char_Blue.asset";

        /// <summary>
        /// Blue, the blue robot backup who joins in the Sandbox: a Light support / tank. Builds the DEF Up
        /// status, Blue's cards and character from Art/Characters/Blue (Tools/ChibiForge/knight_model.py), and adds
        /// Blue to the end of Battle_Sample's roster (a 4th member fights as the backup). Safe to run again: only
        /// missing assets are made.
        /// </summary>
        public static void EnsureBlue()
        {
            BeginStandalone();
            var defUp = Status("Status_DefUp", "DEF Up", "DF", new Color(0.45f, 0.75f, 1f), false,
                StackRule.AddStacks, 2, "+20% Physical and Special DEF per level.", 0f, 0f,
                new StatModifier(StatType.PhysicalDefense, ModifierKind.Percent, 0.2f),
                new StatModifier(StatType.SpecialDefense, ModifierKind.Percent, 0.2f));

            var bash = Card("Card_Blue_ShieldBash", "Shield Bash", CardKind.Attack, TargetMode.SingleEnemy, false, null,
                new CardEffect[] { new DamageEffect { multiplier = 1.1f }, new ApplyStatusEffect { target = EffectTarget.Self, status = defUp, stacks = 1, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 1.6f }, new ApplyStatusEffect { target = EffectTarget.Self, status = defUp, stacks = 1, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 2.4f }, new ApplyStatusEffect { target = EffectTarget.Self, status = defUp, stacks = 2, duration = 2 } });
            var bulwark = Card("Card_Blue_Bulwark", "Bulwark", CardKind.Buff, TargetMode.AllAllies, false, null,
                new CardEffect[] { new ApplyStatusEffect { status = defUp, stacks = 1, duration = 2 } },
                new CardEffect[] { new ApplyStatusEffect { status = defUp, stacks = 1, duration = 3 } },
                new CardEffect[] { new ApplyStatusEffect { status = defUp, stacks = 2, duration = 3 } });
            var aegis = Card("Card_Blue_AegisDawn", "Aegis Dawn", CardKind.Heal, TargetMode.AllAllies, true, null,
                new CardEffect[] { new HealEffect { target = EffectTarget.CardTargets, multiplier = 1.4f },
                                   new ApplyStatusEffect { status = defUp, stacks = 2, duration = 2 } });

            var anim = AnimSet("Blue");
            var blue = Character("Char_Blue", "Blue", "Guardian", new Color(0.45f, 0.65f, 1f), Resonance.Prism, DamageType.Physical,
                3200, 120f, 190f, 170f, new[] { bash, bulwark }, aegis, anim);

            var setup = AssetDatabase.LoadAssetAtPath<BattleSetup>(Root + "/Battle_Sample.asset");
            if (setup != null && Fresh(blue) && !setup.party.Contains(blue))
            {
                setup.party.Add(blue);
                EditorUtility.SetDirty(setup);
            }
            AssetDatabase.SaveAssets();
        }

        // -------------------------------------------------------------------- Chapter One

        public const string BestiaryPath = "Assets/_Project/Resources/Bestiary.asset";
        /// <summary>Bump when Chapter One gains new assets: the bootstrap re-runs EnsureChapterOne on older projects, which
        /// adds whatever is missing (it never changes assets that already exist).</summary>
        public const int ChapterOneVersion = 3;

        /// <summary>
        /// Chapter One's content (bible §9; numbers are first drafts, bible §13): the protagonist's resonance kits
        /// (Physical = the existing cards, Arc = Arc Edge, Static Field, Ult Thunderheart), Status_Stunned, the
        /// chapter's enemies (all on the red Rust Sentry sprite: Scrap Hound, Wire Crawler, Collector, Electric Dragon)
        /// with their skills and Ults, and Resources/Bestiary.asset listing every enemy. Safe to run again: only missing
        /// assets are made, and the bestiary only gains enemies.
        /// </summary>
        public static void EnsureChapterOne()
        {
            BeginStandalone();
            var stunned = Status("Status_Stunned", "Stunned", "ST", new Color(1f, 0.9f, 0.35f), true,
                StackRule.RefreshDuration, 1, "Can't act on their next turn.", 0f, 0f);
            if (Fresh(stunned))
            {
                stunned.preventsAction = true;
                EditorUtility.SetDirty(stunned);
            }
            var atkUp = AssetDatabase.LoadAssetAtPath<StatusDefinition>(Root + "/Statuses/Status_AtkUp.asset");
            var defDown = AssetDatabase.LoadAssetAtPath<StatusDefinition>(Root + "/Statuses/Status_PhysicalDefDown.asset");

            // ---- The protagonist's Arc kit: chains and stuns rather than just "the same hit, but yellow".
            var arcEdge = Card("Card_MC_ArcEdge", "Arc Edge", CardKind.Attack, TargetMode.SingleEnemy, false, null,
                new CardEffect[] { new DamageEffect { multiplier = 1.4f }, new DamageEffect { target = EffectTarget.RandomEnemy, multiplier = 0.5f } },
                new CardEffect[] { new DamageEffect { multiplier = 2.1f }, new DamageEffect { target = EffectTarget.RandomEnemy, multiplier = 0.8f } },
                new CardEffect[] { new DamageEffect { multiplier = 3.4f }, new DamageEffect { target = EffectTarget.RandomEnemy, multiplier = 1.3f } });
            var staticField = Card("Card_MC_StaticField", "Static Field", CardKind.Debuff, TargetMode.AllEnemies, false, null,
                new CardEffect[] { new DamageEffect { multiplier = 0.5f }, new ApplyStatusEffect { status = stunned, duration = 1, chance = 0.25f } },
                new CardEffect[] { new DamageEffect { multiplier = 0.8f }, new ApplyStatusEffect { status = stunned, duration = 1, chance = 0.4f } },
                new CardEffect[] { new DamageEffect { multiplier = 1.2f }, new ApplyStatusEffect { status = stunned, duration = 1, chance = 0.6f } });
            var thunderheart = Card("Card_MC_ArcTempest", "Arc Tempest", CardKind.Attack, TargetMode.AllEnemies, true, null,
                new CardEffect[] { new DamageEffect { multiplier = 3.2f, hits = 3 }, new ApplyStatusEffect { status = stunned, duration = 1, chance = 0.5f } });

            var mc = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "/Characters/Char_MC.asset");
            if (mc != null && (overwrite || mc.resonances == null || mc.resonances.Count == 0))
            {
                mc.resonances = new List<ResonanceKit>
                {
                    new ResonanceKit { element = Resonance.Physical, cards = new List<CardDefinition>(mc.incantations), ultimate = mc.ritual },
                    new ResonanceKit { element = Resonance.Arc, cards = new List<CardDefinition> { arcEdge, staticField }, ultimate = thunderheart },
                };
                EditorUtility.SetDirty(mc);
            }

            // ---- Enemies: all on the red Rust Sentry sprite for now (placeholder art).
            var red = AssetDatabase.LoadAssetAtPath<SpriteAnimationSet>(Root + "/Animations/Anim_RustSentry.asset");
            var bite = Card("Card_Enemy_Bite", "Bite", CardKind.Attack, TargetMode.SingleEnemy, false, null,
                new CardEffect[] { new DamageEffect { multiplier = 1.0f } });
            // Poison Strike (it was Pounce): a hit that also poisons.
            var poison = AssetDatabase.LoadAssetAtPath<StatusDefinition>(Root + "/Statuses/Status_Poison.asset");
            var poisonStrike = Card("Card_Enemy_PoisonStrike", "Poison Strike", CardKind.Attack, TargetMode.SingleEnemy, false, null,
                new CardEffect[] { new DamageEffect { multiplier = 1.5f }, new ApplyStatusEffect { status = poison, duration = 2 } });
            var hound = Enemy("Enemy_ScrapHound", "Scrap Hound", new Color(0.78f, 0.42f, 0.28f), Resonance.Cinder, DamageType.Physical,
                2400, 150f, 100f, 95f, red, new EnemySkill { card = bite, weight = 2f }, new EnemySkill { card = poisonStrike, weight = 1f });
            if (Fresh(hound)) hound.xpReward = 50;

            var spark = Card("Card_Enemy_Spark", "Spark", CardKind.Attack, TargetMode.SingleEnemy, false, null,
                new CardEffect[] { new DamageEffect { multiplier = 1.0f } });
            var coilUp = Card("Card_Enemy_CoilUp", "Coil Up", CardKind.Buff, TargetMode.Self, false, null,
                new CardEffect[] { new ApplyStatusEffect { target = EffectTarget.Self, status = atkUp, stacks = 1, duration = 2 } });
            var crawler = Enemy("Enemy_WireCrawler", "Wire Crawler", new Color(0.5f, 0.62f, 0.85f), Resonance.Arc, DamageType.Special,
                2700, 160f, 120f, 110f, red, new EnemySkill { card = spark, weight = 2f }, new EnemySkill { card = coilUp, weight = 1f });
            if (Fresh(crawler)) crawler.xpReward = 70;

            var grapple = Card("Card_Enemy_GrappleHook", "Grapple Hook", CardKind.Attack, TargetMode.SingleEnemy, false, null,
                new CardEffect[] { new DamageEffect { multiplier = 1.2f } });
            var suppress = Card("Card_Enemy_SuppressingShot", "Suppressing Shot", CardKind.Debuff, TargetMode.SingleEnemy, false, null,
                new CardEffect[] { new DamageEffect { multiplier = 0.9f }, new ApplyStatusEffect { status = defDown, duration = 2 } });
            var reclaim = Card("Card_Enemy_Reclaim", "Reclaim", CardKind.Attack, TargetMode.SingleEnemy, true, null,
                new CardEffect[] { new DamageEffect { multiplier = 2.6f } });
            var collector = Enemy("Enemy_Collector", "Collector", new Color(0.62f, 0.66f, 0.72f), Resonance.Physical, DamageType.Physical,
                4400, 185f, 140f, 130f, red, new EnemySkill { card = grapple, weight = 2f }, new EnemySkill { card = suppress, weight = 1f });
            if (Fresh(collector))
            {
                collector.ritual = reclaim;
                collector.xpReward = 150;
            }

            var breath = Card("Card_Enemy_ThunderBreath", "Thunder Breath", CardKind.Attack, TargetMode.AllEnemies, false, null,
                new CardEffect[] { new DamageEffect { multiplier = 0.9f } });
            var tail = Card("Card_Enemy_TailLash", "Tail Lash", CardKind.Attack, TargetMode.SingleEnemy, false, null,
                new CardEffect[] { new DamageEffect { multiplier = 1.5f } });
            var coil = Card("Card_Enemy_StaticCoil", "Static Coil", CardKind.Debuff, TargetMode.SingleEnemy, false, null,
                new CardEffect[] { new DamageEffect { multiplier = 0.7f }, new ApplyStatusEffect { status = stunned, duration = 1, chance = 0.35f } });
            var storm = Card("Card_Enemy_StormHeart", "Storm Heart", CardKind.Attack, TargetMode.AllEnemies, true, null,
                new CardEffect[] { new DamageEffect { multiplier = 1.8f, hits = 3 } });
            var dragon = Enemy("Enemy_ElectricDragon", "Electric Dragon", new Color(0.45f, 0.75f, 1f), Resonance.Arc, DamageType.Special,
                2700, 170f, 150f, 160f, red, new EnemySkill { card = breath, weight = 1.5f }, new EnemySkill { card = tail, weight = 1.5f },
                new EnemySkill { card = coil, weight = 1f });
            if (Fresh(dragon))
            {
                dragon.ritual = storm;
                dragon.xpReward = 300;
            }
            foreach (var e in new[] { hound, crawler, collector, dragon }) EditorUtility.SetDirty(e);

            // ---- The protagonist starts with their own Fragmented Core fitted and no spare Parts (bible §3, §9).
            var partCatalog = AssetDatabase.LoadAssetAtPath<PartCatalog>(PartCatalogPath);
            if (partCatalog != null)
            {
                var fragmented = FragmentedCore();
                var resonant = ResonantCore();
                if (Fresh(fragmented) && !partCatalog.parts.Contains(fragmented)) partCatalog.parts.Add(fragmented);
                if (Fresh(resonant) && !partCatalog.parts.Contains(resonant)) partCatalog.parts.Add(resonant);
                if (overwrite) partCatalog.starterParts.Clear();
                if (overwrite || partCatalog.starterCore == null) partCatalog.starterCore = fragmented;
                if (overwrite || partCatalog.boundCore == null) partCatalog.boundCore = resonant;
                EditorUtility.SetDirty(partCatalog);
            }

            // ---- The bestiary: every enemy, by asset name, for story encounters at runtime. It only ever gains enemies.
            EnsureFolder("Assets/_Project/Resources");
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(BestiaryPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EnemyCatalog>();
                AssetDatabase.CreateAsset(catalog, BestiaryPath);
            }
            catalog.contentVersion = Mathf.Max(catalog.contentVersion, ChapterOneVersion);
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition", new[] { Root + "/Enemies" }))
            {
                var e = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (e != null && !catalog.enemies.Contains(e)) catalog.enemies.Add(e);
            }
            foreach (var e in new[] { hound, crawler, collector, dragon })
                if (!catalog.enemies.Contains(e)) catalog.enemies.Add(e);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        // -------------------------------------------------------------------- Builders

        static StatusDefinition Status(string file, string displayName, string label, Color color, bool debuff,
            StackRule rule, int maxStacks, string description, float damagePerRound, float healPerRound, params StatModifier[] modifiers)
        {
            var s = ScriptableObject.CreateInstance<StatusDefinition>();
            s.displayName = displayName;
            s.shortLabel = label;
            s.color = color;
            s.isDebuff = debuff;
            s.stackRule = rule;
            s.maxStacks = maxStacks;
            s.description = description;
            s.modifiers = modifiers.ToList();
            s.damagePerRoundPercentMaxHP = damagePerRound;
            s.healPerRoundPercentMaxHP = healPerRound;
            return Save(s, Root + "/Statuses/" + file + ".asset");
        }

        static CastSequence Sequence(string file, params CastStep[] steps)
        {
            var seq = ScriptableObject.CreateInstance<CastSequence>();
            seq.steps = steps.ToList();
            return Save(seq, Root + "/Sequences/" + file + ".asset");
        }

        static CardDefinition Card(string file, string displayName, CardKind kind, TargetMode target, bool ritual,
            CastSequence sequence, params CardEffect[][] ranks)
        {
            var card = ScriptableObject.CreateInstance<CardDefinition>();
            card.displayName = displayName;
            card.kind = kind;
            card.targetMode = target;
            card.isRitual = ritual;
            card.castSequence = sequence;
            card.ranks = ranks.Select(effects => new CardRank { effects = effects.ToList() }).ToList();
            return Save(card, Root + "/Cards/" + file + ".asset");
        }

        static CharacterDefinition Character(string file, string displayName, string archetype, Color color, Resonance resonance,
            DamageType damageType, int hp, float atk, float rdef, float mdef, CardDefinition[] incantations, CardDefinition ritual,
            SpriteAnimationSet animations)
        {
            var c = ScriptableObject.CreateInstance<CharacterDefinition>();
            Fill(c, displayName, color, resonance, damageType, hp, atk, rdef, mdef);
            c.animations = animations;
            c.archetype = archetype;
            c.incantations = incantations.ToList();
            c.ritual = ritual;
            return Save(c, Root + "/Characters/" + file + ".asset");
        }

        /// <summary>The Core the protagonist wakes up with: cracked by the failed transfer, no bonuses. Arc resonance
        /// binds to it later in the chapter.</summary>
        static PartDefinition FragmentedCore()
        {
            return Part("Part_FragmentedCore", "Fragmented Core", PartSlot.Core, new Color(0.75f, 0.7f, 0.62f),
                "Your own Core, cracked by the failed transfer. It still turns.");
        }

        /// <summary>The Fragmented Core after the Soul Card binds with it: +10% damage while Arc.</summary>
        static PartDefinition ResonantCore()
        {
            var core = Part("Part_ResonantCore", "Resonant Core", PartSlot.Core, new Color(0.45f, 0.78f, 1f),
                "Your Core, bound with the Soul Card. +10% damage while Arc.",
                new StatModifier(StatType.DamageBonus, ModifierKind.Flat, 0.1f));
            if (Fresh(core))
            {
                core.elementOnly = true;
                core.element = Resonance.Arc;
                EditorUtility.SetDirty(core);
            }
            return core;
        }

        static PartDefinition Part(string file, string displayName, PartSlot slot, Color color, string description, params StatModifier[] modifiers)
        {
            var p = ScriptableObject.CreateInstance<PartDefinition>();
            p.displayName = displayName;
            p.slot = slot;
            p.color = color;
            p.description = description;
            p.modifiers = modifiers.ToList();
            return Save(p, Root + "/Parts/" + file + ".asset");
        }

        /// <summary>Hooks up Art/Portraits/{name}_*.png (Tools/PortraitForge) as the character-card art.</summary>
        static void Portraits(CharacterDefinition c, string name)
        {
            c.portraitFront = PortraitSprite(name + "_front");
            c.portraitInternals = PortraitSprite(name + "_internals");
            c.portraitGlow = PortraitSprite(name + "_front_glow");
            c.portraitInternalsGlow = PortraitSprite(name + "_internals_glow");
            if (c.portraitFront == null) Debug.LogWarning("No portraits for " + name + " - run Tools/PortraitForge/portrait_forge.py.");
        }

        static Sprite PortraitSprite(string file)
        {
            string path = PortraitArt + file + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); // applies the Sprite import settings
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static EnemyDefinition Enemy(string file, string displayName, Color color, Resonance resonance, DamageType damageType,
            int hp, float atk, float rdef, float mdef, SpriteAnimationSet animations, params EnemySkill[] skills)
        {
            var e = ScriptableObject.CreateInstance<EnemyDefinition>();
            Fill(e, displayName, color, resonance, damageType, hp, atk, rdef, mdef);
            e.animations = animations;
            e.skills = skills.ToList();
            return Save(e, Root + "/Enemies/" + file + ".asset");
        }

        static void Fill(UnitDefinition u, string displayName, Color color, Resonance resonance, DamageType damageType,
            int hp, float atk, float rdef, float mdef)
        {
            u.displayName = displayName;
            u.color = color;
            u.resonance = resonance;
            u.damageType = damageType;
            u.stats = new Stats { maxHP = hp, attack = atk, physicalDefense = rdef, specialDefense = mdef };
        }

        /// <summary>
        /// Creates Content/Animations/Anim_{folder}.asset (if it doesn't exist yet) from the sheets in Art/Characters/{folder}/
        /// (built by Tools/ChibiForge). Returns null if the folder has no sheets.
        /// </summary>
        public static SpriteAnimationSet AnimSet(string folder)
        {
            EnsureFolder(Root + "/Animations");
            var set = ScriptableObject.CreateInstance<SpriteAnimationSet>();
            set.frameSize = 64;
            set.feetPivot = 0.04f;
            // ChibiForge draws smooth, many-frame clips (8 / 13 / 10 / 12 / 9 frames); these rates keep the old timings.
            AddClip(set, folder, "idle", 6f, true, 0);
            AddClip(set, folder, "walk", 15f, true, 1); // frame 0 is the standing pose
            AddClip(set, folder, "slash", 22f, false, 0);
            AddClip(set, folder, "spellcast", 20f, false, 0);
            AddClip(set, folder, "hurt", 15f, false, 0);
            AddClip(set, folder, "sit", 12f, false, 0);   // the Esc menu: sitting down on the mat (holds the last frame)
            AddClip(set, folder, "read", 10f, false, 0);  // Items → Cards: opening the book (holds the last frame)
            if (set.clips.Count == 0)
            {
                Debug.LogWarning("No sprite sheets in " + CharacterArt + folder + " - run Tools/ChibiForge/chibi_forge.py. Using placeholder.");
                Object.DestroyImmediate(set);
                return null;
            }
            // Frame size comes from the sheets: 4 direction rows (64 px for the rig characters, 128 px for the crew's
            // base model). Frames are one world unit / one UI sprite box either way, so both show at the same size.
            var idle = set.Find("idle");
            if (idle != null && idle.sheet.height >= 4 && idle.sheet.height % 4 == 0) set.frameSize = idle.sheet.height / 4;
            // One walk cycle takes 0.8 s whatever its frame count (12 frames at 15 fps; MC's hand-drawn 24 at 30 fps).
            var walk = set.Find("walk");
            if (walk != null)
            {
                int cycle = walk.sheet.width / set.frameSize - walk.firstFrame;
                if (cycle > 0) walk.fps = cycle / 0.8f;
            }
            return Save(set, Root + "/Animations/Anim_" + folder + ".asset");
        }

        public const string PackRatesPath = "Assets/_Project/Resources/Cards/PackRates.asset";

        /// <summary>Makes Resources/Cards/PackRates.asset (the card collection's numbers) if it's missing, with Moon's
        /// status set to DEF Up. An existing one is never touched, so tuned rates are kept. True if it was made.</summary>
        public static bool EnsurePackRates()
        {
            if (AssetDatabase.LoadAssetAtPath<PackRates>(PackRatesPath) != null) return false;
            EnsureFolder(Path.GetDirectoryName(PackRatesPath).Replace('\\', '/'));
            var rates = ScriptableObject.CreateInstance<PackRates>();
            rates.moonStatus = AssetDatabase.LoadAssetAtPath<StatusDefinition>(Root + "/Statuses/Status_DefUp.asset");
            AssetDatabase.CreateAsset(rates, PackRatesPath);
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>Adds the held-pose clips to every existing Anim_*.asset that has their sheet but not the clip (sets made
        /// before they existed): "sit" (the Esc menu's kneel) and "read" (the Cards book). Returns how many sets changed.</summary>
        public static int EnsureSitClips()
        {
            int updated = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:SpriteAnimationSet", new[] { Root + "/Animations" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var set = AssetDatabase.LoadAssetAtPath<SpriteAnimationSet>(path);
                if (set == null) continue;
                string folder = System.IO.Path.GetFileNameWithoutExtension(path).Replace("Anim_", "");
                int before = set.clips.Count;
                if (!set.Has("sit")) AddClip(set, folder, "sit", 12f, false, 0);
                if (!set.Has("read")) AddClip(set, folder, "read", 10f, false, 0);
                if (set.clips.Count == before) continue;
                EditorUtility.SetDirty(set);
                updated++;
            }
            if (updated > 0) AssetDatabase.SaveAssets();
            return updated;
        }

        static void AddClip(SpriteAnimationSet set, string folder, string clip, float fps, bool loop, int firstFrame)
        {
            string path = CharacterArt + folder + "/" + clip + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return;
            set.clips.Add(new SpriteAnimationClip { name = clip, sheet = tex, fps = fps, loop = loop, firstFrame = firstFrame });
        }

        /// <summary>Removes sample assets from earlier versions of this generator (Sonetto -> Vesper -> Miku -> 2B, etc.).</summary>
        static void DeleteLegacyAssets()
        {
            var legacy = new[]
            {
                "Characters/Char_Sonetto", "Cards/Card_Sonetto_Recitation", "Cards/Card_Sonetto_Encourage", "Cards/Card_Sonetto_Ritual",
                "Characters/Char_Vesper", "Cards/Card_Vesper_StaticHymn", "Cards/Card_Vesper_RallyChorus", "Cards/Card_Vesper_Ritual",
                "Characters/Char_Vanguard", "Cards/Card_Vanguard_Cleave", "Cards/Card_Vanguard_Sunder", "Cards/Card_Vanguard_Ritual",
                "Characters/Char_Mender", "Cards/Card_Mender_Soothe", "Cards/Card_Mender_Thorn", "Cards/Card_Mender_Ritual",
                "Enemies/Enemy_ShardSentinel", "Enemies/Enemy_AshenHound",
                "Statuses/Status_RealityDefDown",
                "Characters/Char_Miku", "Cards/Card_Miku_StaticHymn", "Cards/Card_Miku_RallyChorus", "Cards/Card_Miku_Ritual",
                "Enemies/Enemy_StaticEnforcer", "Enemies/Enemy_StaticThug",
                "Cards/Card_Enemy_ShardSlam", "Cards/Card_Enemy_CrystalBarrage", "Cards/Card_Enemy_Harden",
                "Cards/Card_Enemy_Howl", // (the old Card_Enemy_Bite name is used again by the Scrap Hound, so it stays)
                "Animations/Anim_Miku", "Animations/Anim_StaticEnforcer", "Animations/Anim_StaticThug",
                "Backdrop_RainlineStation",
                "Characters/Char_Teto", "Cards/Card_Teto_DrillSpin", "Cards/Card_Teto_BaguetteBash", "Cards/Card_Teto_Ritual",
                "Characters/Char_Rei", "Cards/Card_Rei_PatchProtocol", "Cards/Card_Rei_VirusSpike", "Cards/Card_Rei_Ritual",
                "Animations/Anim_Teto", "Animations/Anim_Rei",
                "Characters/Char_2B", "Cards/Card_2B_VirtuousContract", "Cards/Card_2B_PodSupport", "Cards/Card_2B_Ritual",
                "Characters/Char_V1", "Cards/Card_V1_SpinSaw", "Cards/Card_V1_ArmorBreaker", "Cards/Card_V1_Ritual",
                "Characters/Char_V2", "Cards/Card_V2_PatchProtocol", "Cards/Card_V2_VirusSpike", "Cards/Card_V2_Ritual",
                "Animations/Anim_2B", "Animations/Anim_V1", "Animations/Anim_V2",
                "Parts/Part_ServoBlade", "Parts/Part_IonEmitter", "Parts/Part_ReinforcedPlating", "Parts/Part_MossFilter",
            };
            foreach (var file in legacy)
            {
                string path = Root + "/" + file + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            }
            // Retired character art (2B, V1 and V2 became MC, Ashe and Lucy).
            foreach (var folder in new[] { "2B", "V1", "V2" })
                if (AssetDatabase.IsValidFolder(CharacterArt + folder)) AssetDatabase.DeleteAsset(CharacterArt + folder);
        }

        /// <summary>
        /// Creates the asset if it's missing. An existing asset is returned untouched (your edits win); only Rebuild
        /// copies the new values into it, keeping its GUID so references survive.
        /// </summary>
        static T Save<T>(T obj, string path) where T : ScriptableObject
        {
            obj.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                if (overwrite && !fresh.Contains(existing))
                {
                    EditorUtility.CopySerialized(obj, existing);
                    EditorUtility.SetDirty(existing);
                    fresh.Add(existing);
                }
                else if (!fresh.Contains(existing)) kept++;
                Object.DestroyImmediate(obj);
                return existing;
            }
            AssetDatabase.CreateAsset(obj, path);
            fresh.Add(obj);
            return obj;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void CreateScene(BattleSetup setup)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.7f, 0.76f, 0.72f);
            }

            var go = new GameObject("Battle");
            var boot = go.AddComponent<BattleBootstrap>();
            boot.setup = setup;
            boot.leaveScene = "Overworld_Mossline"; // built by Tools > Overworld > Generate Mossline Station
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
