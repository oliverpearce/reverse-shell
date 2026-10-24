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

    /// <summary>Steps an effect Image through its frames (unscaled time, so it also runs on paused screens), then removes it.</summary>
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
            time += Time.unscaledDeltaTime;
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
    /// Which effects play for each battle moment (the user's picks, 2026-10-23). Several names layer together.
    /// Hits use the attacker's element.
    /// </summary>
    public static class BattleFx
    {
        public static readonly string[] HitVolt = { "scifi_spark_burst_001_small_yellow", "lightning_aura_002_small_blue" };
        public static readonly string[] HitFire = { "symmetrical_explosion_004_small_orange" };
        public static readonly string[] HitToxin = { "round_sparkle_burst_003_small_red" };
        public static readonly string[] HitEarth = { "symmetrical_smoke_burst_001_small_brown" };
        public static readonly string[] HitLight = { "round_firework_burst_002_small_yellow" };
        /// <summary>Physical (no element): a plain impact burst.</summary>
        public static readonly string[] HitPhysical = { "directional_impact_004_small_yellow" };
        public static readonly string[] HitVoid = { "lightning_burst_001_small_violet", "stylized_explosion_002_small_violet" };

        public static readonly string[] Crit = { "symmetrical_impact_001_small_yellow", "lightning_burst_003_small_violet" };
        public static readonly string[] Heal = { "spell_heal_001_small_red", "round_sparkle_burst_002_small_green" };
        public static readonly string[] Buff = { "status_sparkling_001_small_yellow", "directional_impact_003_small_violet" };
        public static readonly string[] Debuff = { "spell_absorb_001_small_violet", "directional_impact_004_small_yellow" };
        public static readonly string[] PoisonTick = { "spell_poison_001_small_green", "status_poison_001_small_green" };
        public static readonly string[] Death = { "symmetrical_explosion_002_small_orange" };
        /// <summary>On the caster when a Ritual (Ult) card starts.</summary>
        public static readonly string[] UltCharge = { "scifi_charge_up_001_small_yellow" };
        /// <summary>On each target hit by a Ritual. A 128 px sheet, so it plays at <see cref="UltHitScale"/>.</summary>
        public static readonly string[] UltHit = { "epic_explosion_001_large_orange" };
        public const float UltHitScale = 4f;
        /// <summary>Loops beside "LEVEL UP!" on the results screen.</summary>
        public const string LevelUp = "symbol_alert_002_small_red";

        public static string[] Hit(Afflatus element)
        {
            switch (element)
            {
                case Afflatus.Volt: return HitVolt;
                case Afflatus.Fire: return HitFire;
                case Afflatus.Toxin: return HitToxin;
                case Afflatus.Earth: return HitEarth;
                case Afflatus.Light: return HitLight;
                case Afflatus.Void: return HitVoid;
                case Afflatus.None: return HitPhysical;
            }
            return HitVolt;
        }
    }
}
