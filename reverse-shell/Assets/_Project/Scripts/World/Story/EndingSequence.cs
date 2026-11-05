using System;
using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// Chapter One's ending after the blackout (bible §4, <i>Erin and the Soul Card Event</i>), as full-screen pixel
    /// panels over the world:
    /// <list type="number">
    /// <item>darkness: a heartbeat, muffled voices ([ending.blackout]);</item>
    /// <item>waking: eyelids open on a dark room of server racks, Erin lying in the old machine ([ending.wake]);</item>
    /// <item>the conversion: light pours through her, she streams into a card, and her Soul Card forms, bright
    /// ([ending.convert]);</item>
    /// <item>the close-up: the Collector's face, the bright card in his dark glasses, which read it as BLANK
    /// ([ending.readout] scrolls across them, then [ending.closeup]);</item>
    /// <item>the rip: he tears her card in two and it decays pixel by pixel (<see cref="CardRip"/>), then
    /// [ending.rip].</item>
    /// </list>
    /// The dialogue box stays above the panels. Hold to Skip (top right, or hold Esc) ends it at once.
    /// </summary>
    public class EndingSequence : StoryPanels
    {
        static readonly Color Ember = new Color(1f, 0.54f, 0.2f);
        static readonly Color Cyan = WorldArt.WarpCyan;

        CharacterDefinition erin;
        UnitDefinition collector;

        /// <summary>Plays the panels; <paramref name="finished"/> runs once they're over (the screen is left black).</summary>
        public static EndingSequence Play(OverworldBuilder world, CharacterDefinition erin, UnitDefinition collector, Action finished)
        {
            var seq = Open<EndingSequence>(world, "Ending", finished);
            seq.erin = erin;
            seq.collector = collector;
            seq.Begin();
            return seq;
        }

        // ------------------------------------------------------------------ the beats

        protected override IEnumerator Run()
        {
            // 1. Darkness: a heartbeat (a dull red pulse), muffled voices.
            yield return Heartbeat(2);
            yield return Talk("ending.blackout");
            yield return Heartbeat(1);

            // 2. Waking in the machine room.
            Image erinImage, glass, glow;
            RectTransform machine;
            BuildRoom(out machine, out erinImage, out glass, out glow);
            yield return Eyelids();
            yield return Talk("ending.wake");

            // 3. The conversion: light through her, her pixels streaming into a card, the card forming, bright.
            RectTransform cardRt;
            yield return Convert(machine, erinImage, glass, glow);
            cardRt = FormCard(machine);
            yield return Tween.Run(0.7f, p => cardRt.localScale = Vector3.one * Tween.OutBack(p), null, true);
            yield return Talk("ending.convert");

            // 4. The close-up: his face, her bright card in his dark glasses, which call it blank.
            yield return Cut();
            yield return CloseUp();
            yield return Talk("ending.closeup");

            // 5. The rip.
            yield return Cut();
            bool ripped = false;
            CardRip.Play(stage, CardArt.Ult(erin) ?? CardArt.SoulSprite(erin), Mathf.Round(size.y * 0.6f / CardArt.Height), Ember, () => ripped = true);
            yield return Tween.Run(0.4f, p => black.color = new Color(0f, 0f, 0f, 1f - p), null, true);
            while (!ripped) yield return null;
            yield return Talk("ending.rip");
            yield return Tween.Run(1.2f, p => black.color = new Color(0f, 0f, 0f, p), null, true);
            Finish();
        }

        // ------------------------------------------------------------------ the machine room

        void BuildRoom(out RectTransform machine, out Image erinImage, out Image glass, out Image glow)
        {
            var center = new Vector2(0.5f, 0.5f);
            var floor = UIFactory.CreateImage(stage, "Floor", new Color(0.06f, 0.07f, 0.08f));
            floor.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(size.x, size.y * 0.42f));

            // Rows of server racks behind, blinking (like the office's), darker the further back.
            var rackA = Rack(1);
            var rackB = Rack(2);
            int count = Mathf.CeilToInt(size.x / (RackW * px + px * 2f)) + 1;
            for (int row = 0; row < 2; row++)
            {
                float y = size.y * (row == 0 ? 0.32f : 0.26f);   // above the dialogue box
                float scale = row == 0 ? 0.8f : 1f;
                float shade = row == 0 ? 0.55f : 1f;
                for (int i = 0; i < count; i++)
                {
                    var raw = Raw(stage, "Rack", (i + row) % 2 == 0 ? rackA : rackB);
                    raw.color = new Color(shade, shade, shade, 1f);
                    float x = -size.x * 0.5f + (i + (row == 0 ? 0.5f : 0f)) * (RackW * px * scale + px * 2f);
                    raw.rectTransform.Place(center, new Vector2(0.5f, 0f), new Vector2(x, -size.y * 0.5f + y),
                        new Vector2(RackW, RackH) * px * scale);
                    var blink = raw.gameObject.AddComponent<RackBlink>();
                    blink.a = rackA;
                    blink.b = rackB;
                }
            }
            // A cold light from above, cables across the floor.
            var lamp = UIFactory.CreateImage(stage, "Lamp", new Color(0.55f, 0.85f, 0.9f, 0.16f));
            lamp.sprite = UiParticles.GlowSprite();
            lamp.rectTransform.Place(center, center, new Vector2(0f, size.y * 0.12f), new Vector2(size.y * 1.1f, size.y * 1.1f));
            for (int i = 0; i < 5; i++)
            {
                var cable = UIFactory.CreateImage(stage, "Cable", new Color(0.02f, 0.02f, 0.025f));
                cable.rectTransform.Place(center, center, new Vector2((i - 2) * size.x * 0.16f, -size.y * 0.22f + (i % 2) * px * 3f),
                    new Vector2(size.x * 0.5f, px * 1.5f));
                cable.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (i - 2) * 6f);
            }

            // The old machine, Erin lying inside it.
            var machineSize = new Vector2(MachineW, MachineH) * px;
            machine = UIFactory.CreateRect("Machine", stage).Place(center, new Vector2(0.5f, 0f), new Vector2(0f, -size.y * 0.24f), machineSize);   // its base above the dialogue box
            glow = UIFactory.CreateImage(machine, "Glow", UIFactory.WithAlpha(Cyan, 0.12f));
            glow.sprite = UiParticles.GlowSprite();
            glow.rectTransform.Place(center, center, new Vector2(0f, machineSize.y * 0.08f), machineSize * 1.7f);
            Raw(machine, "Back", MachineBack()).rectTransform.Fill();
            var inside = UIFactory.CreateRect("Inside", machine);
            inside.anchorMin = new Vector2(GlassX0 / (float)MachineW, 1f - GlassY1 / (float)MachineH);
            inside.anchorMax = new Vector2(GlassX1 / (float)MachineW, 1f - GlassY0 / (float)MachineH);
            inside.offsetMin = inside.offsetMax = Vector2.zero;
            erinImage = UIFactory.CreateImage(inside, "Erin", new Color(0.7f, 0.75f, 0.8f, 1f));
            erinImage.sprite = CardArt.SoulSprite(erin);
            erinImage.preserveAspect = true;
            erinImage.rectTransform.Fill(px * 2f);
            var front = Raw(machine, "Glass", MachineGlass());
            front.rectTransform.Fill();
            glass = UIFactory.CreateImage(inside, "Light", UIFactory.WithAlpha(Color.white, 0f));
            glass.rectTransform.Fill();
        }

        IEnumerator Convert(RectTransform machine, Image erinImage, Image light, Image glow)
        {
            // The machine wakes: its glow swells from cold cyan to her ember, the light floods the glass.
            yield return Tween.Run(1.4f, p =>
            {
                glow.color = UIFactory.WithAlpha(Color.Lerp(Cyan, Ember, p), Mathf.Lerp(0.12f, 0.7f, p));
                light.color = UIFactory.WithAlpha(Color.Lerp(Cyan, Color.white, p), 0.6f * p);
                machine.anchoredPosition = new Vector2(UnityEngine.Random.Range(-1f, 1f) * px * 0.5f * p, machine.anchoredPosition.y);
            }, null, true);
            // She comes apart into light: specks stream up out of the glass to where the card will be.
            var from = erinImage.rectTransform;
            var target = new Vector2(0f, size.y * 0.1f);
            var specks = new List<KeyValuePair<RectTransform, Vector2>>();
            float t = 0f, spawn = 0f;
            const float Stream = 2.6f;
            while (t < Stream + 0.8f)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                if (t < Stream)
                {
                    erinImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - t / Stream) * (UnityEngine.Random.value < 0.15f ? 0.4f : 1f));
                    for (spawn += dt * 70f; spawn >= 1f; spawn -= 1f)
                    {
                        var s = UIFactory.CreateImage(stage, "Speck", UnityEngine.Random.value < 0.5f ? Ember : Color.white);
                        var r = from.rect;
                        var local = new Vector2(UnityEngine.Random.Range(r.xMin, r.xMax) * 0.6f, UnityEngine.Random.Range(r.yMin, r.yMax));
                        s.rectTransform.anchorMin = s.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        s.rectTransform.position = from.TransformPoint(local);
                        s.rectTransform.sizeDelta = Vector2.one * px;
                        specks.Add(new KeyValuePair<RectTransform, Vector2>(s.rectTransform, s.rectTransform.anchoredPosition));
                    }
                }
                for (int i = specks.Count - 1; i >= 0; i--)
                {
                    var s = specks[i].Key;
                    var to = Vector2.MoveTowards(s.anchoredPosition, target, size.y * 0.55f * dt);
                    s.anchoredPosition = Vector2.Lerp(to, target, 2.5f * dt);
                    if ((s.anchoredPosition - target).sqrMagnitude < px * px)
                    {
                        Destroy(s.gameObject);
                        specks.RemoveAt(i);
                    }
                }
                yield return null;
            }
            foreach (var s in specks) if (s.Key != null) Destroy(s.Key.gameObject);
            erinImage.color = Color.clear;
            yield return Tween.Run(0.8f, p => light.color = UIFactory.WithAlpha(Color.white, 0.6f * (1f - p)), null, true);
        }

        RectTransform FormCard(RectTransform machine)
        {
            var center = new Vector2(0.5f, 0.5f);
            var holder = UIFactory.CreateRect("SoulCard", stage).Place(center, center, new Vector2(0f, size.y * 0.1f),
                new Vector2(CardArt.Width, CardArt.Height) * px * 1.6f);
            var halo = UIFactory.CreateImage(holder, "Halo", UIFactory.WithAlpha(Color.Lerp(Ember, Color.white, 0.35f), 0.85f));
            halo.sprite = UiParticles.GlowSprite();
            halo.rectTransform.Place(center, center, Vector2.zero, holder.sizeDelta * 2.4f);
            var card = UIFactory.CreateImage(holder, "Card", Color.white);
            card.sprite = CardArt.Ult(erin);
            card.rectTransform.Fill();
            holder.localScale = Vector3.zero;
            holder.gameObject.AddComponent<Breathe>().glow = halo;
            return holder;
        }

        // ------------------------------------------------------------------ the close-up

        IEnumerator CloseUp()
        {
            var center = new Vector2(0.5f, 0.5f);
            float fps;
            var set = collector != null ? collector.Anim : null;
            var faces = CardArt.FaceFrames(set, out fps);
            float faceH = size.y * 0.95f;
            var face = UIFactory.CreateImage(stage, "Face", Color.white);
            face.preserveAspect = true;
            if (faces != null && faces.Length > 0) face.sprite = faces[0];
            else face.color = new Color(0.08f, 0.08f, 0.09f);   // no sprite: a dark silhouette
            float aspect = face.sprite != null ? face.sprite.rect.width / face.sprite.rect.height : 1f;
            var faceSize = new Vector2(faceH * aspect, faceH);
            face.rectTransform.Place(center, center, new Vector2(0f, -size.y * 0.04f), faceSize);
            // A still frame: his idle bob looked silly this close up.

            // The dark glasses: two near-black lenses on a bar, her bright card reflected in each.
            var band = UIFactory.CreateRect("Glasses", face.rectTransform).Place(center, center, new Vector2(0f, faceSize.y * 0.06f),
                new Vector2(faceSize.x * 0.92f, faceSize.y * 0.2f));
            var bar = UIFactory.CreateImage(band, "Bar", new Color(0.02f, 0.02f, 0.025f));
            bar.rectTransform.Place(center, center, new Vector2(0f, band.sizeDelta.y * 0.3f), new Vector2(band.sizeDelta.x, px * 2.5f));
            var lensSize = new Vector2(band.sizeDelta.x * 0.4f, band.sizeDelta.y);
            var lenses = new List<RectTransform>();
            foreach (float side in new[] { -1f, 1f })
            {
                var lens = UIFactory.CreateImage(band, "Lens", new Color(0.03f, 0.03f, 0.04f, 0.97f));
                lens.rectTransform.Place(center, center, new Vector2(side * band.sizeDelta.x * 0.26f, 0f), lensSize);
                var shine = UIFactory.CreateImage(lens.rectTransform, "Shine", new Color(1f, 1f, 1f, 0.12f));
                shine.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(lensSize.x * 0.12f, -px * 2f), new Vector2(lensSize.x * 0.5f, px * 1.5f));
                var glint = UIFactory.CreateImage(lens.rectTransform, "Glow", UIFactory.WithAlpha(Ember, 0.55f));
                glint.sprite = UiParticles.GlowSprite();
                glint.rectTransform.Place(center, center, Vector2.zero, Vector2.one * lensSize.y * 1.3f);
                var reflected = UIFactory.CreateImage(lens.rectTransform, "Card", new Color(1f, 1f, 1f, 0.85f));
                reflected.sprite = CardArt.Ult(erin);
                reflected.preserveAspect = true;
                reflected.rectTransform.Place(center, center, Vector2.zero, new Vector2(lensSize.y * 0.6f, lensSize.y * 0.8f));
                lenses.Add(lens.rectTransform);
            }

            // The push in, and the readout typing across the lenses.
            // Big, in the title type like the opening's EVERLASTING: sized so the longest line spans the glasses, with a
            // cyan glow and a dark edge so it reads over the lenses and his face.
            var lines = StoryText.Has("ending.readout") ? StoryText.Texts("ending.readout") : new string[0];
            var readout = InkStyle.Title(band, "Readout", "", 120, Cyan, TextAnchor.MiddleCenter);
            readout.supportRichText = false;
            readout.horizontalOverflow = HorizontalWrapMode.Overflow;
            readout.verticalOverflow = VerticalWrapMode.Overflow;
            float widest = 1f;
            foreach (var line in lines)
            {
                readout.text = StripMarkup(line) + "_";
                widest = Mathf.Max(widest, readout.preferredWidth);
            }
            readout.text = "";
            readout.fontSize = Mathf.Clamp(Mathf.RoundToInt(readout.fontSize * band.sizeDelta.x * 0.95f / widest), 24, 200);
            readout.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, band.sizeDelta.y * 0.1f),
                new Vector2(band.sizeDelta.x, readout.fontSize * 1.6f));
            var halo = readout.gameObject.AddComponent<Outline>();
            halo.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f);
            halo.effectDistance = new Vector2(6f, 6f);
            var edge = readout.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0f, 0f, 0f, 0.9f);
            edge.effectDistance = new Vector2(3f, 3f);
            StartCoroutine(Tween.Run(9f, p => face.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.14f, p), null, true));
            yield return Tween.Run(0.5f, p => black.color = new Color(0f, 0f, 0f, 1f - p), null, true);
            yield return new WaitForSecondsRealtime(0.7f);
            foreach (var line in lines)
            {
                string plain = StripMarkup(line);
                for (int i = 1; i <= plain.Length; i++)
                {
                    readout.text = plain.Substring(0, i) + (i < plain.Length ? "_" : "");
                    yield return new WaitForSecondsRealtime(0.035f);
                }
                yield return new WaitForSecondsRealtime(0.55f);
            }
            // The last word holds, flickering.
            for (int i = 0; i < 4; i++)
            {
                readout.enabled = !readout.enabled;
                yield return new WaitForSecondsRealtime(0.12f);
            }
            readout.enabled = true;   // the push-in carries on under his line
        }

        // ------------------------------------------------------------------ pixel art drawn in code

        const int RackW = 16, RackH = 44;
        const int MachineW = 46, MachineH = 62;
        const int GlassX0 = 9, GlassX1 = 37, GlassY0 = 9, GlassY1 = 50;   // the glass window, in machine pixels (top-left origin)

        static Texture2D rackA, rackB, machineBack, machineGlass;

        /// <summary>A server rack: a dark cabinet of drive bays, each with a few indicator lights (two light patterns).</summary>
        static Texture2D Rack(int seed)
        {
            if (seed == 1 && rackA != null) return rackA;
            if (seed == 2 && rackB != null) return rackB;
            var frame = new Color32(26, 29, 34, 255);
            var inner = new Color32(12, 14, 17, 255);
            var bay = new Color32(32, 36, 42, 255);
            var lights = new[] { new Color32(90, 230, 140, 255), new Color32(255, 180, 70, 255), new Color32(120, 230, 240, 255), new Color32(40, 60, 50, 255) };
            var tex = Draw(RackW, RackH, (x, y) =>
            {
                if (x == 0 || x == RackW - 1 || y == 0 || y == RackH - 1) return frame;
                if (y < 2 || y > RackH - 3) return frame;
                int slot = (y - 2) % 4;
                if (slot == 0) return frame;
                if (slot == 2 && x >= RackW - 6 && x <= RackW - 3 && (x % 2 == 1))
                {
                    int h = (x * 7 + y * 13 + seed * 31) % 11;
                    return h < 5 ? lights[h % lights.Length] : lights[3];
                }
                return slot == 1 ? bay : inner;
            });
            if (seed == 1) rackA = tex; else rackB = tex;
            return tex;
        }

        /// <summary>The old machine's body: a tall capsule frame on a heavy base, cables into its crown; the glass window
        /// is left clear so Erin shows through (the glass itself is <see cref="MachineGlass"/>).</summary>
        static Texture2D MachineBack()
        {
            if (machineBack != null) return machineBack;
            var metal = new Color32(46, 52, 60, 255);
            var dark = new Color32(22, 25, 30, 255);
            var rim = new Color32(78, 88, 98, 255);
            var rust = new Color32(92, 60, 42, 255);
            var inside = new Color32(10, 16, 20, 255);
            machineBack = Draw(MachineW, MachineH, (x, y) =>
            {
                bool inGlass = x >= GlassX0 && x < GlassX1 && y >= GlassY0 && y < GlassY1;
                if (inGlass) return inside;
                // cables hanging into the crown
                if (y < 6) return (x == 12 || x == 13 || x == 22 || x == 30 || x == 31) ? dark : new Color32(0, 0, 0, 0);
                if (x < 3 || x >= MachineW - 3) return y > 52 ? metal : new Color32(0, 0, 0, 0);
                if (y >= 54) return (x + y) % 9 == 0 ? rust : (y == 54 ? rim : dark);   // the base
                bool edge = x == 3 || x == MachineW - 4 || y == 6 || y == 53;
                bool windowRim = (x == GlassX0 - 1 || x == GlassX1) && y >= GlassY0 - 1 && y <= GlassY1
                                 || (y == GlassY0 - 1 || y == GlassY1) && x >= GlassX0 - 1 && x <= GlassX1;
                if (edge || windowRim) return rim;
                if ((x * 3 + y * 5) % 23 == 0) return rust;
                return metal;
            });
            return machineBack;
        }

        /// <summary>The glass over the window: a faint teal with two highlight streaks.</summary>
        static Texture2D MachineGlass()
        {
            if (machineGlass != null) return machineGlass;
            machineGlass = Draw(MachineW, MachineH, (x, y) =>
            {
                if (x < GlassX0 || x >= GlassX1 || y < GlassY0 || y >= GlassY1) return new Color32(0, 0, 0, 0);
                int d = x - GlassX0 - (y - GlassY0) / 3;
                if (d == 4 || d == 5 || d == 9) return new Color32(200, 240, 245, 70);
                return new Color32(60, 130, 140, 46);
            });
            return machineGlass;
        }

        /// <summary>A rack's lights changing now and then (between its two patterns).</summary>
        class RackBlink : MonoBehaviour
        {
            public Texture a, b;
            float next;
            RawImage raw;
            void Start() { raw = GetComponent<RawImage>(); next = UnityEngine.Random.Range(0.2f, 1.5f); }
            void Update()
            {
                next -= Time.unscaledDeltaTime;
                if (next > 0f || raw == null) return;
                next = UnityEngine.Random.Range(0.3f, 1.8f);
                raw.texture = raw.texture == a ? b : a;
            }
        }

        /// <summary>The freshly made Soul Card breathing light.</summary>
        class Breathe : MonoBehaviour
        {
            public Image glow;
            void Update()
            {
                if (glow == null) return;
                var c = glow.color;
                c.a = 0.65f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f);
                glow.color = c;
            }
        }

        /// <summary>Steps through a face's frames (his idle, slowed down).</summary>
        class FaceFlip : MonoBehaviour
        {
            Sprite[] frames;
            float fps, t;
            Image image;
            public void Set(Sprite[] f, float rate) { frames = f; fps = Mathf.Max(1f, rate); image = GetComponent<Image>(); }
            void Update()
            {
                if (frames == null || image == null) return;
                t += Time.unscaledDeltaTime;
                image.sprite = frames[(int)(t * fps) % frames.Length];
            }
        }
    }
}
