using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// Plays the pixel effect animations in Resources/Fx (Super Pixel Effects Gigapack, free version, by Will Tice /
    /// unTied Games). Each effect is a sheet "name.png" plus "name.txt" listing its frames ("frame0000 x y w h", y from
    /// the top). Effects run at the pack's 15 fps and are drawn at the battle sprites' 6x so pixels match.
    /// Which effect plays when is in <see cref="BattleFx"/>.
    /// </summary>
    public static class FxLibrary
    {
        public const float Fps = 15f;
        /// <summary>Canvas units per effect pixel (battle sprites are 64 px frames drawn at 6x).</summary>
        public const float PixelScale = 6f;

        static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        /// <summary>The effect's frames, or null if it isn't in Resources/Fx.</summary>
        public static Sprite[] Frames(string name)
        {
            Sprite[] frames;
            if (cache.TryGetValue(name, out frames)) return frames;
            var tex = Resources.Load<Texture2D>("Fx/" + name);
            var meta = Resources.Load<TextAsset>("Fx/" + name);
            if (tex == null || meta == null)
            {
                Debug.LogWarning("FxLibrary: missing effect '" + name + "' in Resources/Fx");
                return cache[name] = null;
            }
            tex.filterMode = FilterMode.Point;
            var list = new List<Sprite>();
            foreach (var line in meta.text.Split('\n'))
            {
                var parts = line.Trim().Split(' ');
                if (parts.Length < 5) continue;
                int x = int.Parse(parts[1]), y = int.Parse(parts[2]), w = int.Parse(parts[3]), h = int.Parse(parts[4]);
                var sprite = Sprite.Create(tex, new Rect(x, tex.height - y - h, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                sprite.name = name + "_" + list.Count;
                list.Add(sprite);
            }
            return cache[name] = list.ToArray();
        }

        /// <summary>How long one run of the effect takes, in seconds.</summary>
        public static float Duration(string name)
        {
            var frames = Frames(name);
            return frames == null ? 0f : frames.Length / Fps;
        }

        /// <summary>Spawns the effect under <paramref name="parent"/> (centered; position it with the returned rect).</summary>
        public static RectTransform Play(Transform parent, string name, float pixelScale = PixelScale, bool loop = false)
        {
            var frames = Frames(name);
            if (frames == null || frames.Length == 0) return null;
            var img = UIFactory.CreateImage(parent, "Fx_" + name, Color.white);
            img.sprite = frames[0];
            var size = frames[0].rect.size;
            img.rectTransform.sizeDelta = size * pixelScale;
            img.gameObject.AddComponent<FxSprite>().Init(frames, loop);
            return img.rectTransform;
        }

        /// <summary>Plays every effect in <paramref name="names"/> layered at one world position.</summary>
        public static void PlayAt(Transform parent, string[] names, Vector3 worldPosition, float pixelScale = PixelScale)
        {
            if (names == null) return;
            foreach (var name in names)
            {
                var rt = Play(parent, name, pixelScale);
                if (rt != null) rt.position = worldPosition;
            }
        }
    }

    /// <summary>Steps an effect Image through its frames (unscaled time, so it also runs on paused screens, sped up
    /// with the battle speed), then removes it.</summary>
    public class FxSprite : MonoBehaviour
    {
        Image image;
        Sprite[] frames;
        bool loop;
        float time;

        public void Init(Sprite[] sheet, bool looping)
        {
            image = GetComponent<Image>();
            frames = sheet;
            loop = looping;
        }

        void Update()
        {
            if (frames == null) return;
            time += Time.unscaledDeltaTime * Mathf.Max(1f, Time.timeScale);   // keeps up with the battle speed
            int frame = Mathf.FloorToInt(time * FxLibrary.Fps);
            if (frame >= frames.Length)
            {
                if (!loop)
                {
                    Destroy(gameObject);
                    return;
                }
                frame %= frames.Length;
            }
            image.sprite = frames[frame];
        }
    }

    /// <summary>
    /// Which effects play for each battle moment (the user's picks in the effect picker, 2026-10-28). Several names
    /// layer together. Hits use the attacker's resonance.
    /// </summary>
    public static class BattleFx
    {
        public static readonly string[] HitArc = { "lightning_aura_002_small_blue", "scifi_spark_burst_001_small_yellow" };
        public static readonly string[] HitRoot = { "burst_splatter_003_small_green", "symmetrical_impact_003_small_yellow" };
        public static readonly string[] HitCryo = { "round_sparkle_burst_001_small_blue", "symmetrical_impact_002_small_blue" };
        public static readonly string[] HitCinder = { "symmetrical_explosion_001_small_orange" };
        public static readonly string[] HitPrism = { "status_sparkling_001_small_yellow", "symmetrical_impact_004_small_yellow" };
        /// <summary>Physical (no element): a plain impact burst.</summary>
        public static readonly string[] HitPhysical = { "directional_impact_004_small_yellow" };

        public static readonly string[] Crit = { "symmetrical_impact_001_small_yellow", "lightning_burst_003_small_violet" };
        public static readonly string[] Heal = { "round_sparkle_burst_002_small_green" };
        public static readonly string[] Buff = { "status_sparkling_001_small_yellow" };
        public static readonly string[] Debuff = { "spell_absorb_001_small_violet" };

        /// <summary>A unit gains a shield.</summary>
        public static readonly string[] Shield = { "spell_defense_up_001_small_blue" };
        /// <summary>A shield soaks a hit (in place of the hit effect when it soaks all of it).</summary>
        public static readonly string[] Block = { "directional_impact_001_small_blue" };
        /// <summary>On the attacker a counter hits back.</summary>
        public static readonly string[] Counter = { "symmetrical_impact_006_small_yellow" };
        /// <summary>On the unit stepping in front of an ally.</summary>
        public static readonly string[] Cover = { "symbol_defense_up_001_small_blue" };
        /// <summary>On the enemy whose move is cancelled.</summary>
        public static readonly string[] Interrupt = { "symbol_failure_001_small_red" };
        public static readonly string[] Cleanse = { "round_sparkle_burst_001_small_blue" };

        public static readonly string[] PoisonTick = { "status_poison_001_small_green" };
        public static readonly string[] BurnTick = { "fire_looping_004A_small_orange" };
        public static readonly string[] Chill = { "round_sparkle_burst_001_small_blue" };
        public static readonly string[] Frozen = { "symmetrical_impact_002_small_blue" };

        public static readonly string[] Death = { "epic_explosion_001_small_orange" };
        /// <summary>On the caster when a Ritual (Ult) card starts.</summary>
        public static readonly string[] UltCharge = { "scifi_charge_up_001_small_yellow" };
        /// <summary>On each target hit by a Ritual. A 96 px sheet, so it plays at <see cref="UltHitScale"/> (512 units across).</summary>
        public static readonly string[] UltHit = { "stylized_explosion_002_large_violet" };
        public const float UltHitScale = 512f / 96f;
        /// <summary>Behind "LEVEL UP!" on the results screen.</summary>
        public static readonly string[] LevelUp = { "round_firework_burst_002_small_yellow" };

        /// <summary>A battle cue's effect (<see cref="FxCueEvent"/>), or null.</summary>
        public static string[] Cue(string cue)
        {
            switch (cue)
            {
                case FxCueEvent.Shield: return Shield;
                case FxCueEvent.Cover: return Cover;
                case FxCueEvent.Interrupt: return Interrupt;
                case FxCueEvent.Cleanse: return Cleanse;
            }
            return null;
        }

        /// <summary>An end-of-turn tick's effect: Burn's fire, else the poison bubbles.</summary>
        public static string[] Tick(StatusDefinition status)
        {
            return status != null && status.damagePerRoundSourceATK > 0f ? BurnTick : PoisonTick;
        }

        public static string[] Hit(Resonance element)
        {
            switch (element)
            {
                case Resonance.Arc: return HitArc;
                case Resonance.Cinder: return HitCinder;
                case Resonance.Root: return HitRoot;
                case Resonance.Cryo: return HitCryo;
                case Resonance.Prism: return HitPrism;
            }
            return HitPhysical;
        }
    }
}
