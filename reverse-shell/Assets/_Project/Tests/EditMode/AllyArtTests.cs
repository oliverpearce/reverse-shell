using System.Collections.Generic;
using Combat;
using Combat.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Combat.Tests
{
    /// <summary>Every ally on A3's model (Tools/ChibiForge: a3_model, a3_walks, a3_portrait, alt_recolour) has its own
    /// portrait and every walk style, each pointing at that ally's own sheet; and Ashe's card colour is exactly
    /// cornflower blue (#6495ED).</summary>
    public class AllyArtTests
    {
        static readonly string[] Allies = { "A3", "Ashe", "Erin", "Lotus", "Zuko" };

        static SpriteAnimationSet Anim(string name)
        {
            return AssetDatabase.LoadAssetAtPath<SpriteAnimationSet>("Assets/_Project/Content/Animations/Anim_" + name + ".asset");
        }

        [Test]
        public void EveryAlly_HasAPortrait_AndEveryWalkStyle()
        {
            foreach (var name in Allies)
            {
                var set = Anim(name);
                Assert.IsNotNull(set, name);
                Assert.IsTrue(set.Has(SpriteAnimationSet.Portrait), name + " portrait");
                for (int i = 1; i <= GameSettings.WalkStyles; i++)
                    Assert.IsTrue(set.Has("walk_s" + i), name + " walk_s" + i);
                Assert.AreEqual(SpriteAnimationSet.Portrait, set.MenuClip, name);
                // the Esc menu's living portrait: a3_study's idle loop (hair in the wind, a shrug, a blink), 16 frames (2048 px)
                var frames = set.Frames(SpriteAnimationSet.Portrait, Facing.Down, false);
                Assert.AreEqual(16, frames.Length, name + " portrait frames");
                Assert.IsTrue(set.Has("battle_idle"), name + " keeps the sheet's body in battle");
            }
        }

        [Test]
        public void EveryAlly_UsesTheirOwnSheets()
        {
            var seen = new Dictionary<Texture2D, string>();
            foreach (var name in Allies)
            {
                var set = Anim(name);
                foreach (var clip in new[] { SpriteAnimationSet.Portrait, "walk_s1", "walk_s2", "walk_s3", "idle" })
                {
                    var sheet = set.Find(clip).sheet;
                    string other;
                    Assert.IsFalse(seen.TryGetValue(sheet, out other), name + " " + clip + " is " + other + "'s sheet");
                    seen[sheet] = name + " " + clip;
                    StringAssert.Contains("/Characters/" + name + "/", AssetDatabase.GetAssetPath(sheet), name + " " + clip);
                }
            }
        }

        /// <summary>A sheet wider or taller than its import limit (maxTextureSize) is shrunk by Unity, and its frames then
        /// slice wrong (the scrambled sprites of 2026-11-04). Reads each PNG's real size from its header.</summary>
        [Test]
        public void EveryCharacterSheet_FitsItsImportLimit()
        {
            foreach (var png in System.IO.Directory.GetFiles("Assets/_Project/Art/Characters", "*.png", System.IO.SearchOption.AllDirectories))
            {
                var head = new byte[24];
                using (var f = System.IO.File.OpenRead(png)) f.Read(head, 0, 24);
                int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
                int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
                var importer = AssetImporter.GetAtPath(png.Replace('\\', '/')) as TextureImporter;
                int max = importer != null ? importer.maxTextureSize : 2048;
                Assert.LessOrEqual(Mathf.Max(w, h), max, png + " is " + w + "x" + h + ", over its " + max + " px import limit");
            }
        }

        [Test]
        public void Ashe_IsExactlyCornflowerBlue()
        {
            var ashe = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/_Project/Content/Characters/Char_Ashe.asset");
            Assert.IsNotNull(ashe);
            Color32 c = ashe.color;
            Assert.AreEqual(100, c.r);
            Assert.AreEqual(149, c.g);
            Assert.AreEqual(237, c.b);
        }
    }
}
