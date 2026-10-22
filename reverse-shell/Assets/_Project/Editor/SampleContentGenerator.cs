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
    /// Tools > Combat > Generate Sample Content. Creates (or overwrites in place, keeping GUIDs)
    /// the main party (MC the Prototype, Heal Bot and Lucy), two machine enemies, the Parts and their catalog,
    /// cards/statuses/animations and a Battle scene. Sprites come from Assets/_Project/Art
    /// (Tools/SpriteForge builds the cast sheets, Tools/PortraitForge the character-card portraits).
    /// All three party members are original robots. Non-commercial fan project.
    /// Everything it makes is plain data you can then edit in the inspector.
    /// </summary>
    public static class SampleContentGenerator
    {
        const string Root = "Assets/_Project/Content";
        const string ScenePath = "Assets/_Project/Scenes/Battle.unity";
        const string CharacterArt = "Assets/_Project/Art/Characters/";
        const string PortraitArt = "Assets/_Project/Art/Portraits/";
        public const string PartCatalogPath = Root + "/Parts/PartCatalog.asset";

        [MenuItem("Tools/Combat/Generate Sample Content")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

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
            poison.verb = "poison"; // card text: "deal 120% ATK damage and poison 1 enemy (x1, 2 turns)"
            EditorUtility.SetDirty(poison);
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

            // ---------------------------------------------------------------- MC, the main character (Star, Physical)
            // Party leader and the standout model. Glows white for now; it will channel several elements later
            // (channels + AfflatusInfo.GlowFor). A heavy single-target strike plus a team damage buff.
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
            var mcUlt = Card("Card_MC_Ritual", "Prism Burst", CardKind.Attack, TargetMode.SingleEnemy, true, seqRitual,
                new CardEffect[]
                {
                    new DamageEffect { multiplier = 6f, hits = 2 },
                    new ApplyStatusEffect { target = EffectTarget.AllAllies, status = atkUp, stacks = 2, duration = 2 }
                });
            var mc = Character("Char_MC", "MC", "Prototype", new Color(0.92f, 0.92f, 0.95f), Afflatus.Star, DamageType.Physical,
                2200, 240f, 130f, 130f, new[] { mc1, mc2 }, mcUlt, AnimSet("MC"));
            mc.cardPool = new List<CardDefinition> { mc3 };
            mc.neutralGlow = true;
            mc.glowColor = Color.white;
            mc.statBarReference = true; // crew-menu stat bars: MC at level 30 fills 90%
            mc.partSockets = new List<Vector2>(); // all crew cards share the reference knight's layout (CharacterMenu.SocketSpots)
            mc.channels = new List<Afflatus> { Afflatus.Star, Afflatus.Mineral, Afflatus.Beast, Afflatus.Plant, Afflatus.Spirit, Afflatus.Intellect };
            Portraits(mc, "MC");

            // ---------------------------------------------------------------- Heal Bot (Plant = "Earth", Special)
            // The shared robot model, overgrown like a dryad. A tanky healer.
            var heal1 = Card("Card_HealBot_SapMend", "Sap Mend", CardKind.Heal, TargetMode.SingleAlly, false, seqSupport,
                new CardEffect[] { new HealEffect { target = EffectTarget.CardTargets, multiplier = 1.4f } },
                new CardEffect[] { new HealEffect { target = EffectTarget.CardTargets, multiplier = 2.1f } },
                new CardEffect[]
                {
                    new HealEffect { target = EffectTarget.CardTargets, multiplier = 3.2f },
                    new ApplyStatusEffect { target = EffectTarget.CardTargets, status = regen, duration = 2 }
                });
            var heal2 = Card("Card_HealBot_BrambleLash", "Bramble Lash", CardKind.Debuff, TargetMode.SingleEnemy, false, seqStrike,
                new CardEffect[] { new DamageEffect { multiplier = 1.2f }, new ApplyStatusEffect { status = defDown, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 1.8f }, new ApplyStatusEffect { status = defDown, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 2.9f }, new ApplyStatusEffect { status = defDown, duration = 3 } });
            var heal3 = Card("Card_HealBot_Overgrowth", "Overgrowth", CardKind.Buff, TargetMode.AllAllies, false, seqSupport,
                new CardEffect[] { new ApplyStatusEffect { status = regen, duration = 2 } },
                new CardEffect[] { new ApplyStatusEffect { status = regen, duration = 3 } },
                new CardEffect[] { new ApplyStatusEffect { status = regen, duration = 4 } });
            var healUlt = Card("Card_HealBot_Ritual", "Verdant Bloom", CardKind.Heal, TargetMode.AllAllies, true, seqRitual,
                new CardEffect[]
                {
                    new HealEffect { target = EffectTarget.CardTargets, multiplier = 2.5f },
                    new ApplyStatusEffect { target = EffectTarget.CardTargets, status = regen, duration = 3 }
                });
            var healBot = Character("Char_HealBot", "Heal Bot", "Medic", new Color(0.5f, 0.86f, 0.45f), Afflatus.Plant, DamageType.Special,
                2800, 190f, 160f, 150f, new[] { heal1, heal2 }, healUlt, AnimSet("HealBot"));
            healBot.cardPool = new List<CardDefinition> { heal3 };
            Portraits(healBot, "HealBot");

            // ---------------------------------------------------------------- Lucy (Intellect = "Virus", Special)
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
            var lucyUlt = Card("Card_Lucy_Ritual", "Zero Day", CardKind.Attack, TargetMode.AllEnemies, true, seqRitual,
                new CardEffect[]
                {
                    new DamageEffect { multiplier = 3f },
                    new ApplyStatusEffect { status = poison, stacks = 2, duration = 3 },
                    new ApplyStatusEffect { status = defDown, duration = 2 }
                });
            var lucy = Character("Char_Lucy", "Lucy", "Hacker", new Color(0.72f, 0.45f, 0.95f), Afflatus.Intellect, DamageType.Special,
                2100, 215f, 120f, 150f, new[] { lucy1, lucy2 }, lucyUlt, AnimSet("Lucy"));
            lucy.cardPool = new List<CardDefinition> { lucy3 };
            Portraits(lucy, "Lucy");
            foreach (var c in new[] { mc, healBot, lucy }) EditorUtility.SetDirty(c);

            // ---------------------------------------------------------------- Parts (internal slots on the character card)
            var parts = new List<PartDefinition>
            {
                Part("Part_IonChip", "Ion Chip", PartSlot.Chip, new Color(0.6f, 0.75f, 1f), "+15% Ult damage.",
                    new StatModifier(StatType.RitualMight, ModifierKind.Flat, 0.15f)),
                Part("Part_ReinforcedCore", "Reinforced Core", PartSlot.Core, new Color(0.8f, 0.8f, 0.85f), "+12% max HP.",
                    new StatModifier(StatType.MaxHP, ModifierKind.Percent, 0.12f)),
                Part("Part_TargetingChip", "Targeting Chip", PartSlot.Chip, new Color(1f, 0.85f, 0.4f), "+8% crit rate.",
                    new StatModifier(StatType.CritRate, ModifierKind.Flat, 0.08f)),
                Part("Part_PatchCache", "Patch Cache", PartSlot.Chip, new Color(0.45f, 0.95f, 0.65f), "+12% healing.",
                    new StatModifier(StatType.HealingBonus, ModifierKind.Flat, 0.12f)),
                Part("Part_OverclockCore", "Overclock Core", PartSlot.Core, new Color(1f, 0.5f, 0.3f), "+8% card damage.",
                    new StatModifier(StatType.IncantationMight, ModifierKind.Flat, 0.08f)),
                Part("Part_StableCore", "Stable Core", PartSlot.Core, new Color(0.55f, 0.9f, 0.95f), "+8% P.DEF and S.DEF.",
                    new StatModifier(StatType.PhysicalDefense, ModifierKind.Percent, 0.08f),
                    new StatModifier(StatType.SpecialDefense, ModifierKind.Percent, 0.08f)),
            };
            var catalog = ScriptableObject.CreateInstance<PartCatalog>();
            catalog.parts = parts;
            catalog.starterParts = new List<PartDefinition>(parts); // one of each to start
            Save(catalog, PartCatalogPath);

            // ---------------------------------------------------------------- Enemies (machine lifeforms)
            var slam = Card("Card_Enemy_PistonSlam", "Piston Slam", CardKind.Attack, TargetMode.SingleEnemy, false, seqStrike,
                new CardEffect[] { new DamageEffect { multiplier = 1.6f } });
            var barrage = Card("Card_Enemy_MissileVolley", "Missile Volley", CardKind.Attack, TargetMode.AllEnemies, false, seqSweep,
                new CardEffect[] { new DamageEffect { multiplier = 0.8f, hits = 2 } });
            var harden = Card("Card_Enemy_ReinforcePlating", "Reinforce", CardKind.Buff, TargetMode.Self, false, seqSupport,
                new CardEffect[] { new ApplyStatusEffect { status = atkUp, duration = 3 } });
            var bite = Card("Card_Enemy_CorrodedBlade", "Corroded Blade", CardKind.Attack, TargetMode.SingleEnemy, false, seqStrike,
                new CardEffect[] { new DamageEffect { multiplier = 1.3f }, new ApplyStatusEffect { status = poison, duration = 2 } });
            var howl = Card("Card_Enemy_NetworkLink", "Network Link", CardKind.Buff, TargetMode.AllAllies, false, seqSupport,
                new CardEffect[] { new ApplyStatusEffect { status = atkUp, duration = 2 } });

            // Tuned as an introductory fight: roughly 6-8 rounds, enemies deal ~350 HP/round into ~7000 party HP.
            var sentry = Enemy("Enemy_RustSentry", "Rust Sentry", new Color(0.66f, 0.46f, 0.3f), Afflatus.Mineral, DamageType.Physical,
                4200, 150f, 140f, 110f, AnimSet("RustSentry"),
                new EnemySkill { card = slam, weight = 2f },
                new EnemySkill { card = barrage, weight = 1f },
                new EnemySkill { card = harden, weight = 0.5f });
            sentry.xpReward = 120;
            var stalker = Enemy("Enemy_ScrapStalker", "Scrap Stalker", new Color(0.5f, 0.58f, 0.32f), Afflatus.Beast, DamageType.Special,
                2600, 135f, 90f, 90f, AnimSet("ScrapStalker"),
                new EnemySkill { card = bite, weight = 2f },
                new EnemySkill { card = howl, weight = 0.6f });
            stalker.xpReward = 80;
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
            setup.party = new List<CharacterDefinition> { mc, healBot, lucy };
            setup.enemies = new List<EnemyDefinition> { sentry, stalker };
            setup = Save(setup, Root + "/Battle_Sample.asset");

            AssetDatabase.SaveAssets();
            CreateScene(setup);

            Selection.activeObject = mc;
            EditorUtility.DisplayDialog("Combat", "Sample content generated.\n\nScene: " + ScenePath + "\nPress Play to fight!", "OK");
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

        static CharacterDefinition Character(string file, string displayName, string archetype, Color color, Afflatus afflatus,
            DamageType damageType, int hp, float atk, float rdef, float mdef, CardDefinition[] incantations, CardDefinition ritual,
            SpriteAnimationSet animations)
        {
            var c = ScriptableObject.CreateInstance<CharacterDefinition>();
            Fill(c, displayName, color, afflatus, damageType, hp, atk, rdef, mdef);
            c.animations = animations;
            c.archetype = archetype;
            c.incantations = incantations.ToList();
            c.ritual = ritual;
            return Save(c, Root + "/Characters/" + file + ".asset");
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

        static EnemyDefinition Enemy(string file, string displayName, Color color, Afflatus afflatus, DamageType damageType,
            int hp, float atk, float rdef, float mdef, SpriteAnimationSet animations, params EnemySkill[] skills)
        {
            var e = ScriptableObject.CreateInstance<EnemyDefinition>();
            Fill(e, displayName, color, afflatus, damageType, hp, atk, rdef, mdef);
            e.animations = animations;
            e.skills = skills.ToList();
            return Save(e, Root + "/Enemies/" + file + ".asset");
        }

        static void Fill(UnitDefinition u, string displayName, Color color, Afflatus afflatus, DamageType damageType,
            int hp, float atk, float rdef, float mdef)
        {
            u.displayName = displayName;
            u.color = color;
            u.afflatus = afflatus;
            u.damageType = damageType;
            u.stats = new Stats { maxHP = hp, attack = atk, physicalDefense = rdef, specialDefense = mdef };
        }

        /// <summary>
        /// Creates/updates Content/Animations/Anim_{folder}.asset from the sheets in Art/Characters/{folder}/
        /// (built by Tools/ChibiForge). Returns null if the folder has no sheets.
        /// </summary>
        public static SpriteAnimationSet AnimSet(string folder)
        {
            EnsureFolder(Root + "/Animations");
            var set = ScriptableObject.CreateInstance<SpriteAnimationSet>();
            set.frameSize = 64;
            set.feetPivot = 0.04f;
            AddClip(set, folder, "idle", 2.5f, true, 0);
            AddClip(set, folder, "walk", 10f, true, 1); // frame 0 is the standing pose
            AddClip(set, folder, "slash", 14f, false, 0);
            AddClip(set, folder, "spellcast", 12f, false, 0);
            AddClip(set, folder, "hurt", 10f, false, 0);
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
            return Save(set, Root + "/Animations/Anim_" + folder + ".asset");
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
                "Cards/Card_Enemy_Bite", "Cards/Card_Enemy_Howl",
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
            // Retired character art (2B, V1 and V2 became MC, Heal Bot and Lucy).
            foreach (var folder in new[] { "2B", "V1", "V2" })
                if (AssetDatabase.IsValidFolder(CharacterArt + folder)) AssetDatabase.DeleteAsset(CharacterArt + folder);
        }

        /// <summary>Creates the asset, or copies into the existing one so references and GUIDs survive.</summary>
        static T Save<T>(T obj, string path) where T : ScriptableObject
        {
            obj.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(obj, existing);
                Object.DestroyImmediate(obj);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(obj, path);
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
