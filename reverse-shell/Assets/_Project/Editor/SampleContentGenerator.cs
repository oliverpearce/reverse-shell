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
    /// the main party (2B the YoRHa Battler, V1 the Assault Unit, V2 the Support Unit), two machine enemies,
    /// their cards/statuses/animations and a Battle scene. Sprites come from Assets/_Project/Art
    /// (see Tools/SpriteForge for how the cast sheets are derived).
    /// Fan project: 2B (NieR:Automata) © SQUARE ENIX. V1 and V2 are original generic robots.
    /// Non-commercial use only.
    /// Everything it makes is plain data you can then edit in the inspector.
    /// </summary>
    public static class SampleContentGenerator
    {
        const string Root = "Assets/_Project/Content";
        const string ScenePath = "Assets/_Project/Scenes/Battle.unity";
        const string CharacterArt = "Assets/_Project/Art/Characters/";

        [MenuItem("Tools/Combat/Generate Sample Content")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AssetDatabase.Refresh(); // pick up sprite sheets written by Tools/SpriteForge
            foreach (var folder in new[] { "Cards", "Characters", "Enemies", "Statuses", "Sequences", "Animations" })
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

            // ---------------------------------------------------------------- 2B, the YoRHa Battler (Star, Physical)
            // Party leader. A heavy single-target sword strike plus a Pod-driven team damage buff.
            var battler1 = Card("Card_2B_VirtuousContract", "Virtuous Contract", CardKind.Attack, TargetMode.SingleEnemy, false, seqStrike,
                new CardEffect[] { new DamageEffect { multiplier = 1.8f } },
                new CardEffect[] { new DamageEffect { multiplier = 2.7f } },
                new CardEffect[] { new DamageEffect { multiplier = 4.5f } });
            var battler2 = Card("Card_2B_PodSupport", "Pod Support", CardKind.Buff, TargetMode.AllAllies, false, seqSupport,
                new CardEffect[] { new ApplyStatusEffect { status = mightUp, stacks = 1, duration = 2 } },
                new CardEffect[] { new ApplyStatusEffect { status = mightUp, stacks = 2, duration = 2 } },
                new CardEffect[]
                {
                    new ApplyStatusEffect { status = mightUp, stacks = 3, duration = 2 },
                    new GainMoxieEffect { target = EffectTarget.RandomAlly, amount = 1 }
                });
            var battlerUlt = Card("Card_2B_Ritual", "Pod Laser", CardKind.Attack, TargetMode.SingleEnemy, true, seqRitual,
                new CardEffect[]
                {
                    new DamageEffect { multiplier = 6f, hits = 2 },
                    new ApplyStatusEffect { target = EffectTarget.AllAllies, status = atkUp, stacks = 2, duration = 2 }
                });
            var twoB = Character("Char_2B", "2B", "YoRHa Battler", new Color(0.86f, 0.84f, 0.76f), Afflatus.Star, DamageType.Physical,
                2200, 240f, 130f, 130f, new[] { battler1, battler2 }, battlerUlt, AnimSet("2B"));

            // ---------------------------------------------------------------- V1, the Assault Unit (Mineral, Special)
            var van1 = Card("Card_V1_SpinSaw", "Spin Saw", CardKind.Attack, TargetMode.AllEnemies, false, seqSweep,
                new CardEffect[] { new DamageEffect { multiplier = 0.9f } },
                new CardEffect[] { new DamageEffect { multiplier = 1.35f } },
                new CardEffect[] { new DamageEffect { multiplier = 2.2f } });
            var van2 = Card("Card_V1_ArmorBreaker", "Armor Breaker", CardKind.Debuff, TargetMode.SingleEnemy, false, seqStrike,
                new CardEffect[] { new DamageEffect { multiplier = 1.3f }, new ApplyStatusEffect { status = defDown, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 1.95f }, new ApplyStatusEffect { status = defDown, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 3.2f }, new ApplyStatusEffect { status = defDown, duration = 3 } });
            var vanUlt = Card("Card_V1_Ritual", "Overdrive", CardKind.Attack, TargetMode.SingleEnemy, true, seqRitual,
                new CardEffect[] { new DamageEffect { multiplier = 5f, hits = 3 } });
            var v1 = Character("Char_V1", "V1", "Assault Unit", new Color(0.45f, 0.62f, 0.92f), Afflatus.Mineral, DamageType.Special,
                2800, 215f, 170f, 120f, new[] { van1, van2 }, vanUlt, AnimSet("V1"));

            // ---------------------------------------------------------------- V2, the Support Unit (Intellect, Special)
            var men1 = Card("Card_V2_PatchProtocol", "Patch Protocol", CardKind.Heal, TargetMode.SingleAlly, false, seqSupport,
                new CardEffect[] { new HealEffect { target = EffectTarget.CardTargets, multiplier = 1.4f } },
                new CardEffect[] { new HealEffect { target = EffectTarget.CardTargets, multiplier = 2.1f } },
                new CardEffect[]
                {
                    new HealEffect { target = EffectTarget.CardTargets, multiplier = 3.2f },
                    new ApplyStatusEffect { target = EffectTarget.CardTargets, status = regen, duration = 2 }
                });
            var men2 = Card("Card_V2_VirusSpike", "Virus Spike", CardKind.Attack, TargetMode.SingleEnemy, false, seqBolt,
                new CardEffect[] { new DamageEffect { multiplier = 1.2f }, new ApplyStatusEffect { status = poison, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 1.8f }, new ApplyStatusEffect { status = poison, stacks = 2, duration = 2 } },
                new CardEffect[] { new DamageEffect { multiplier = 2.8f }, new ApplyStatusEffect { status = poison, stacks = 3, duration = 3 } });
            var menUlt = Card("Card_V2_Ritual", "System Restore", CardKind.Heal, TargetMode.AllAllies, true, seqRitual,
                new CardEffect[]
                {
                    new HealEffect { target = EffectTarget.CardTargets, multiplier = 2.5f },
                    new ApplyStatusEffect { target = EffectTarget.CardTargets, status = atkUp, duration = 2 }
                });
            var v2 = Character("Char_V2", "V2", "Support Unit", new Color(0.4f, 0.86f, 0.72f), Afflatus.Intellect, DamageType.Special,
                2100, 200f, 120f, 150f, new[] { men1, men2 }, menUlt, AnimSet("V2"));

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
            setup.party = new List<CharacterDefinition> { twoB, v1, v2 };
            setup.enemies = new List<EnemyDefinition> { sentry, stalker };
            setup = Save(setup, Root + "/Battle_Sample.asset");

            AssetDatabase.SaveAssets();
            CreateScene(setup);

            Selection.activeObject = twoB;
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
        /// Creates/updates Content/Animations/Anim_{folder}.asset from the LPC sheets in Art/Characters/{folder}/
        /// (built by Tools/SpriteForge/lpc_compose.py). Returns null if the folder has no sheets.
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
                Debug.LogWarning("No sprite sheets in " + CharacterArt + folder + " - run Tools/SpriteForge/lpc_compose.py. Using placeholder.");
                Object.DestroyImmediate(set);
                return null;
            }
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
            };
            foreach (var file in legacy)
            {
                string path = Root + "/" + file + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) AssetDatabase.DeleteAsset(path);
            }
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
