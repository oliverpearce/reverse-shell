using System;
using System.Collections;
using System.Collections.Generic;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The opening of a new game (bible §9, Prologue), seen first-person at a Retro PC in the office at night: the PC
    /// asks for the protagonist's name and thanks them for choosing Reverse Shell, an old public-information recording
    /// promising EVERLASTING life plays on its monitor (the word typed there pops up huge across the view), the
    /// procedure starts and fails, the room drops to red emergency lighting and it all cuts hard to black.
    /// Enter / Space / click advances a line; Esc skips the recording (never the login). Runs on unscaled time over the
    /// overworld canvas, then calls <c>done</c> (the cut to the city, where the awakening follows).
    /// The words are a screen-space panel laid over the monitor glass every frame, so they stay sharp over the pixel
    /// world. Without a PC (an office with no desk) the panel sits in the middle of a black screen.
    /// </summary>
    public class PrologueSequence : MonoBehaviour
    {
        // The narration (hopeful and reassuring, not sinister, bible §2) comes from the prologue.* blocks of
        // Resources/Story/Chapter1.txt. These screens are plain text, so any markup in those lines is dropped.
        static string[] Script(string key)
        {
            var lines = StoryText.Texts(key);
            for (int i = 0; i < lines.Length; i++) lines[i] = DialoguePager.Plain(lines[i]);
            return lines;
        }

        static string First(string key)
        {
            var lines = Script(key);
            return lines.Length > 0 ? lines[0] : "";
        }

        const float TypeSpeed = 42f;   // characters per second

        /// <summary>Set by the screenshot run (ShotTour): the name prompt takes this as soon as it's showing.</summary>
        public static string AutoName;

        // The monitor's phosphor.
        static readonly Color Phosphor = new Color(0.86f, 1f, 0.9f);
        static readonly Color PhosphorDim = new Color(0.7f, 0.88f, 0.8f, 0.8f);
        static readonly Color TerminalGreen = new Color(0.55f, 1f, 0.7f);
        static readonly Color ScreenGlow = new Color(0.16f, 0.3f, 0.26f);
        static readonly Color TerminalGlow = new Color(0.08f, 0.2f, 0.12f);
        static readonly Color GoldGlow = new Color(0.55f, 0.42f, 0.16f);

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        OverworldBuilder world;
        OfficePC pc;
        RectTransform root, screen, page;
        Text line, timecode, rec, hint;
        Image black;
        Action done;
        bool skip, advance, canSkip;
        float started;

        // The seat: the camera parked in front of the PC, and what it was before.
        Camera cam;
        OverworldCamera follow;
        Vector3 widePosition, seatPosition;
        Quaternion wideRotation, seatRotation;
        float push;   // 0 = the first, wide look at the desk; 1 = seated, reading the screen
        float oldFov, oldNear;
        readonly List<Renderer> hidden = new List<Renderer>();
        readonly Vector3[] corners = new Vector3[8];
        Vector2 jitter;
        Light sun;
        float sunIntensity;

        public static PrologueSequence Play(OverworldBuilder world, OfficePC pc, Action done)
        {
            var rt = UIFactory.CreateRect("Prologue", world.UI.transform).Fill();
            rt.SetAsLastSibling();
            var seq = rt.gameObject.AddComponent<PrologueSequence>();
            seq.root = rt;
            seq.world = world;
            seq.pc = pc;
            seq.done = done;
            seq.StartCoroutine(seq.Run());
            return seq;
        }

        void Update()
        {
            UpdateSkip();
            if (skippingAll) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                advance = true;
            if (UIKeys.Back && canSkip) skip = true;
            if (timecode != null)
            {
                float t = Time.unscaledTime - started;
                timecode.text = string.Format("{0:00}:{1:00}:{2:00}", (int)(t / 60f), (int)t % 60, (int)(t * 25f) % 25);
                rec.enabled = ((int)(t * 2f)) % 2 == 0;
            }
        }

        void LateUpdate()
        {
            if (cam != null && pc != null)
            {
                // From the wide look into the seat, then a slow breath.
                float t = Time.unscaledTime;
                float k = Mathf.SmoothStep(0f, 1f, push);
                var rotation = Quaternion.Slerp(wideRotation, seatRotation, k);
                cam.transform.rotation = rotation * Quaternion.Euler(Mathf.Sin(t * 0.41f) * 0.3f, Mathf.Sin(t * 0.27f) * 0.3f, 0f);
                cam.transform.position = Vector3.Lerp(widePosition, seatPosition, k);
            }
            if (alarms.Count > 0) PulseAlarms();
            FitScreen();
        }

        void OnDestroy()
        {
            // Back to the follow camera (the cut to the city reloads the scene anyway).
            if (cam != null)
            {
                cam.fieldOfView = oldFov;
                cam.nearClipPlane = oldNear;
            }
            if (follow != null) follow.enabled = true;
            foreach (var r in hidden) if (r != null) r.enabled = true;
            if (sun != null) sun.intensity = sunIntensity;
            if (pc != null) pc.SetGlow(OfficeKit.IdleGlow);
            if (emergency)
            {
                RenderSettings.ambientSkyColor = oldSky;
                RenderSettings.ambientEquatorColor = oldEquator;
                RenderSettings.ambientGroundColor = oldGround;
                RenderSettings.fogColor = oldFog;
                DynamicGI.UpdateEnvironment();
            }
            foreach (var light in switchedOff) if (light != null) light.enabled = true;
            foreach (var light in alarms) if (light != null) Destroy(light.gameObject);
            if (world != null && world.UI != null) world.UI.SetHudAlpha(1f);
        }

        IEnumerator Run()
        {
            black = UIFactory.CreateImage(root, "Black", Color.black, true);
            black.rectTransform.Fill();
            world.UI.SetHudAlpha(0f);
            Seat();
            world.UI.SetFade(new Color(0f, 0f, 0f, 0f));   // this black covers the screen now
            yield return new WaitForSecondsRealtime(0.8f);   // the area's first frames load and stutter

            hint = UIFactory.CreateText(root, "Hint", UIText.Get("prologue.hint.next"), UIFactory.TextSize.Small, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerCenter);
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(900f, 40f));
            hint.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            hint.gameObject.SetActive(false);
            BuildSkip();
            yield return Login();
            yield return Recording1();
            yield return Failure();
            yield return CutToBlack();
            Destroy(gameObject);
            if (done != null) done();
        }

        // ------------------------------------------------------------------ The seat

        /// <summary>The camera's two places: a wide look from behind the chair (the desk, the PC and the office around
        /// it) and the seat (eye height, the glass about a third of the view, the whole monitor, keyboard and desk with
        /// the office beyond). Hides the player and this desk's chair (you are the one sitting there).</summary>
        void Seat()
        {
            cam = world.Cam;
            if (cam == null || pc == null) return;
            follow = cam.GetComponent<OverworldCamera>();
            if (follow != null) follow.enabled = false;
            oldFov = cam.fieldOfView;
            oldNear = cam.nearClipPlane;

            const float Fov = 40f;
            float tan = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float h = pc.GlassHeight;
            var glass = pc.GlassCenter;
            float seat = h / 0.36f * 0.5f / tan;
            seatPosition = glass + pc.Facing * seat + Vector3.up * h * 0.25f;
            seatRotation = Quaternion.LookRotation(glass - Vector3.up * h * 0.3f - seatPosition, Vector3.up);
            float wide = h / 0.13f * 0.5f / tan;
            widePosition = glass + pc.Facing * wide + Vector3.up * wide * 0.4f;
            wideRotation = Quaternion.LookRotation(glass - Vector3.up * h * 0.6f - widePosition, Vector3.up);
            cam.fieldOfView = Fov;
            cam.nearClipPlane = Mathf.Min(oldNear, seat * 0.2f);
            cam.transform.SetPositionAndRotation(widePosition, wideRotation);

            Hide(world.PlayerSprite != null ? world.PlayerSprite.transform : null);
            Hide(pc.Chair);
            pc.AddScreenLight();

            sun = RenderSettings.sun;
            if (sun == null)
                foreach (var light in FindObjectsOfType<Light>())
                    if (light.type == LightType.Directional) { sun = light; break; }
            if (sun != null) sunIntensity = sun.intensity;
        }

        void Hide(Transform t)
        {
            if (t == null) return;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
                if (r.enabled)
                {
                    r.enabled = false;
                    hidden.Add(r);
                }
        }

        /// <summary>The camera moves in from the wide look to the seat (Esc jumps there).</summary>
        IEnumerator PushIn(float hold, float seconds)
        {
            for (float t = 0f; t < hold && !skip; t += Time.unscaledDeltaTime) yield return null;
            for (float t = 0f; t < seconds && !skip; t += Time.unscaledDeltaTime)
            {
                push = t / seconds;
                yield return null;
            }
            push = 1f;
        }

        /// <summary>Lays the screen panel over the monitor glass (its corners as seen by the camera, a little inside
        /// the edge), or in the middle of the screen without a PC.</summary>
        void FitScreen()
        {
            if (screen == null) return;
            if (cam == null || pc == null)
            {
                screen.anchoredPosition = jitter;
                screen.sizeDelta = new Vector2(1500f, 840f);
                return;
            }
            pc.GlassCorners(corners);
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in corners)
            {
                var point = PixelCamera.ToScreen(cam, cam.WorldToScreenPoint(corner));
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, point, null, out local);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            var size = (max - min) * 0.94f;
            screen.anchoredPosition = (min + max) * 0.5f + jitter;
            screen.sizeDelta = size;
        }

        /// <summary>A fresh page on the monitor: its dark glass, the phosphor glow, scanlines and the edge darkening.</summary>
        RectTransform NewPage(Color top, Color bottom)
        {
            if (screen == null)
            {
                screen = UIFactory.CreateRect("Screen", root);
                screen.Place(Center, Center, Vector2.zero, new Vector2(1500f, 840f));
                screen.SetSiblingIndex(black.rectTransform.GetSiblingIndex() + 1);
                FitScreen();
            }
            if (page != null) Destroy(page.gameObject);
            page = UIFactory.CreateRect("Page", screen).Fill();
            var glass = UIFactory.CreateImage(page, "Glass", new Color(0.03f, 0.06f, 0.05f, 0.96f));
            glass.rectTransform.Fill();
            var glow = InkStyle.Gradient(page, top, bottom);
            glow.rectTransform.Fill();
            Scanlines(page);
            var edge = NierStyle.Vignette(page, 0.5f);
            edge.rectTransform.Fill();
            page.localScale = new Vector3(1f, 0f, 1f);   // dark until PowerOn
            return page;
        }

        // ------------------------------------------------------------------ 1. Logging in

        /// <summary>The office fades in on a wide look at the desk, the camera sits down at the PC and it powers on as
        /// a terminal asking for your name; once it's in, the PC thanks you by name.</summary>
        IEnumerator Login()
        {
            var p = NewPage(new Color(0.06f, 0.16f, 0.1f, 0.5f), new Color(0.02f, 0.04f, 0.03f, 0.9f));
            StartCoroutine(Fade(black, 0f, 1.8f));
            yield return PushIn(1.4f, 3.2f);
            yield return PowerOn(TerminalGlow);
            hint.gameObject.SetActive(true);
            var prompt = UIFactory.CreateText(p, "Prompt", "", UIFactory.TextSize.Large, TerminalGreen, TextAnchor.MiddleCenter);
            prompt.rectTransform.anchorMin = new Vector2(0.06f, 0.58f);
            prompt.rectTransform.anchorMax = new Vector2(0.94f, 0.92f);
            prompt.rectTransform.offsetMin = prompt.rectTransform.offsetMax = Vector2.zero;
            prompt.resizeTextForBestFit = true;
            prompt.resizeTextMaxSize = prompt.fontSize;
            prompt.resizeTextMinSize = UIFactory.Px(UIFactory.TextSize.Small);
            line = prompt;
            advance = false;
            foreach (var text in Script("prologue.name")) yield return Type(text);
            prompt.text = First("prologue.name.question");

            string chosen = null;
            var field = NameField(p, n => chosen = n);
            var note = UIFactory.CreateText(p, "Note", UIText.Get("prologue.name.hint"), UIFactory.TextSize.Small, new Color(TerminalGreen.r, TerminalGreen.g, TerminalGreen.b, 0.55f), TextAnchor.UpperCenter);
            note.rectTransform.anchorMin = new Vector2(0.04f, 0.1f);
            note.rectTransform.anchorMax = new Vector2(0.96f, 0.2f);
            note.rectTransform.offsetMin = note.rectTransform.offsetMax = Vector2.zero;
            hint.gameObject.SetActive(false);   // the note on the screen says what to do
            while (chosen == null)
            {
                if (!field.isFocused) field.ActivateInputField();
                if (AutoName != null) chosen = AutoName;   // the screenshot run (ShotTour) types it
                yield return null;
            }
            Destroy(field.gameObject);
            Destroy(note.gameObject);
            hint.gameObject.SetActive(true);

            StoryState.SetName(chosen);
            StoryState.Set(Chapter1.Named);
            SaveGame.Save();

            // "Thank you, <name>, for choosing us!" in the middle of the screen.
            prompt.text = "";
            prompt.rectTransform.anchorMin = new Vector2(0.06f, 0.2f);
            prompt.rectTransform.anchorMax = new Vector2(0.94f, 0.8f);
            foreach (var text in Script("prologue.thanks")) yield return Type(text);
        }

        // ------------------------------------------------------------------ 2. The recording

        IEnumerator Recording1()
        {
            started = Time.unscaledTime;
            var p = NewPage(new Color(0.2f, 0.32f, 0.3f, 0.6f), new Color(0.04f, 0.06f, 0.07f, 0.9f));
            var brand = InkStyle.Title(p, "Brand", UIText.Get("title.name"), UIFactory.TextSize.Large, Phosphor, TextAnchor.UpperCenter);
            brand.rectTransform.TopStrip(16f, 44f);
            var tag = UIFactory.CreateText(p, "Tag", UIText.Get("prologue.tag"), UIFactory.TextSize.Min, PhosphorDim, TextAnchor.UpperCenter);
            tag.rectTransform.TopStrip(58f, 28f);
            rec = UIFactory.CreateText(p, "Rec", UIText.Get("prologue.rec"), UIFactory.TextSize.Small, new Color(1f, 0.35f, 0.3f), TextAnchor.UpperLeft);
            rec.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(140f, 32f));
            timecode = UIFactory.CreateText(p, "Time", "", UIFactory.TextSize.Small, PhosphorDim, TextAnchor.UpperRight);
            timecode.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -14f), new Vector2(200f, 32f));

            line = UIFactory.CreateText(p, "Line", "", UIFactory.TextSize.Large, Phosphor, TextAnchor.MiddleCenter);
            Middle(line.rectTransform);
            line.supportRichText = true;
            // The longest lines shrink a little to stay on the glass, never below the small readable size.
            line.resizeTextForBestFit = true;
            line.resizeTextMaxSize = line.fontSize;
            line.resizeTextMinSize = UIFactory.Px(UIFactory.TextSize.Small);

            // The recording starts on its own: the screen fills in piece by piece (no power-on, no flash). Esc skips it
            // from here.
            canSkip = true;
            hint.text = UIText.Get("prologue.hint");
            p.localScale = Vector3.one;
            if (pc != null) pc.SetGlow(ScreenGlow);
            var pieces = new GameObject[] { brand.gameObject, tag.gameObject, rec.gameObject, timecode.gameObject };
            foreach (var piece in pieces) piece.SetActive(false);
            foreach (var piece in pieces)
            {
                yield return new WaitForSecondsRealtime(0.18f);
                piece.SetActive(true);
            }
            yield return new WaitForSecondsRealtime(0.3f);
            foreach (var text in Script("prologue.recording"))
            {
                if (skip) break;
                yield return Type(text);
            }
            if (!skip)
            {
                foreach (var text in Script("prologue.tagline"))
                    if (!skip) yield return Type(text);
                if (!skip)
                {
                    line.text = "";
                    yield return Everlasting(UIText.Get("prologue.everlasting"));
                    if (pc != null) pc.SetGlow(ScreenGlow);
                }
            }
            canSkip = false;
            hint.text = UIText.Get("prologue.hint.next");
        }

        /// <summary>The tagline's last word, typed on the monitor in gold while each letter pops up huge across the
        /// view as it's typed, over the dimmed office. It holds with a breathing glow, then the letters fade away in
        /// turn.</summary>
        IEnumerator Everlasting(string word)
        {
            if (pc != null) pc.SetGlow(GoldGlow);
            var dim = UIFactory.CreateImage(root, "Dim", new Color(0f, 0f, 0f, 0f));
            dim.rectTransform.Fill();
            StartCoroutine(Fade(dim, 0.6f, 0.4f));
            var typed = InkStyle.Title(page, "Everlasting", "", UIFactory.TextSize.Title, InkStyle.GoldBright, TextAnchor.MiddleCenter);
            Middle(typed.rectTransform);
            typed.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Measured at the base size, then sized so the row spans most of the view.
            var letters = new List<Text>();
            var glows = new List<Text>();
            foreach (char c in word)
            {
                var t = InkStyle.Title(root, "Letter", c.ToString(), 120, InkStyle.GoldBright, TextAnchor.MiddleCenter);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                letters.Add(t);
            }
            float total = 0f;
            foreach (var t in letters) total += t.preferredWidth + Gap(t);
            float fit = Mathf.Clamp(root.rect.width * 0.82f / Mathf.Max(1f, total), 0.5f, 3f);
            total = 0f;
            foreach (var t in letters)
            {
                t.fontSize = Mathf.RoundToInt(t.fontSize * fit);
                total += t.preferredWidth + Gap(t);
            }
            var slots = new List<Vector2>();
            float x = -total * 0.5f;
            foreach (var t in letters)
            {
                float w = t.preferredWidth;
                slots.Add(new Vector2(x + w * 0.5f, 0f));
                x += w + Gap(t);
                t.rectTransform.Place(Center, Center, slots[slots.Count - 1], new Vector2(w + 40f, t.fontSize * 1.6f));
                // A soft gold halo behind each letter (copied before the letter gets its dark edge).
                var glow = Instantiate(t.gameObject, root, false).GetComponent<Text>();
                glow.name = "Glow";
                var halo = glow.gameObject.AddComponent<Outline>();
                halo.effectColor = new Color(1f, 0.75f, 0.3f, 0.3f);
                halo.effectDistance = new Vector2(7f, 7f);
                glow.color = new Color(1f, 0.82f, 0.4f, 0.3f);
                glow.rectTransform.SetSiblingIndex(t.rectTransform.GetSiblingIndex());
                glows.Add(glow);
                var edge = t.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(0.25f, 0.16f, 0.04f, 0.9f);
                edge.effectDistance = new Vector2(3f, 3f);
                t.gameObject.SetActive(false);
                glow.gameObject.SetActive(false);
            }
            yield return new WaitForSecondsRealtime(0.3f);

            // Typed: on the monitor, and big across the view at the same time.
            for (int i = 0; i < letters.Count; i++)
            {
                if (skip) break;
                typed.text = word.Substring(0, i + 1);
                StartCoroutine(PopIn(letters[i], glows[i]));
                yield return new WaitForSecondsRealtime(0.12f);
            }
            typed.text = word;
            for (int i = 0; i < letters.Count; i++)   // Esc: all of them at once
                if (skip || !letters[i].gameObject.activeSelf) Show(letters[i], glows[i]);
            yield return new WaitForSecondsRealtime(0.2f);

            // Hold, the glow breathing.
            for (float t = 0f; t < 2.6f && !skip && !(advance && t > 0.8f); t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < glows.Count; i++)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3.5f - i * 0.35f);
                    glows[i].color = new Color(1f, 0.82f, 0.4f, 0.15f + pulse * 0.25f);
                    glows[i].rectTransform.localScale = Vector3.one * (1.02f + pulse * 0.05f);
                }
                yield return null;
            }
            advance = false;

            // Away in turn, drifting up as they fade; the office comes back.
            StartCoroutine(Fade(dim, 0f, 0.9f));
            const float Away = 0.45f;
            float end = Away + letters.Count * 0.05f;
            for (float t = 0f; t < end && !skip; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < letters.Count; i++)
                {
                    float k = Mathf.Clamp01((t - i * 0.05f) / Away);
                    var lift = slots[i] + Vector2.up * (k * 40f);
                    letters[i].rectTransform.anchoredPosition = lift;
                    glows[i].rectTransform.anchoredPosition = lift;
                    letters[i].color = new Color(InkStyle.GoldBright.r, InkStyle.GoldBright.g, InkStyle.GoldBright.b, 1f - k);
                    glows[i].color = new Color(1f, 0.82f, 0.4f, 0.3f * (1f - k));
                }
                yield return null;
            }
            foreach (var t in letters) Destroy(t.gameObject);
            foreach (var g in glows) Destroy(g.gameObject);
            Destroy(typed.gameObject);
            Destroy(dim.gameObject, 1f);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        /// <summary>The space after a title letter.</summary>
        static float Gap(Text t) { return t.fontSize * 0.14f; }

        /// <summary>One big letter popping up where it stands: from small to a little past full size and back.</summary>
        IEnumerator PopIn(Text letter, Text glow)
        {
            letter.gameObject.SetActive(true);
            glow.gameObject.SetActive(true);
            const float Seconds = 0.16f;
            for (float t = 0f; t < Seconds && !skip; t += Time.unscaledDeltaTime)
            {
                float k = t / Seconds;
                var scale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, BackOut(k));
                letter.rectTransform.localScale = scale;
                glow.rectTransform.localScale = scale;
                letter.color = new Color(InkStyle.GoldBright.r, InkStyle.GoldBright.g, InkStyle.GoldBright.b, Mathf.Clamp01(k * 3f));
                yield return null;
            }
            Show(letter, glow);
        }

        static void Show(Text letter, Text glow)
        {
            letter.gameObject.SetActive(true);
            glow.gameObject.SetActive(true);
            letter.rectTransform.localScale = Vector3.one;
            letter.color = InkStyle.GoldBright;
            glow.rectTransform.localScale = Vector3.one;
        }

        static float BackOut(float t)
        {
            const float s = 1.4f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        // ------------------------------------------------------------------ 3. The procedure fails

        IEnumerator Failure()
        {
            skip = false;
            string procedure = First("prologue.procedure");
            line.text = procedure;
            var track = UIFactory.CreateImage(page, "Track", new Color(1f, 1f, 1f, 0.12f));
            track.rectTransform.pivot = Center;
            track.rectTransform.anchorMin = new Vector2(0.14f, 0.22f);
            track.rectTransform.anchorMax = new Vector2(0.86f, 0.22f);
            track.rectTransform.sizeDelta = new Vector2(0f, 28f);
            var fill = UIFactory.CreateImage(track.rectTransform, "Fill", new Color(0.55f, 0.9f, 0.8f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var percent = UIFactory.CreateText(page, "Percent", UIText.Get("prologue.transfer", "pct", "0"), UIFactory.TextSize.Large, Phosphor, TextAnchor.MiddleCenter);
            percent.rectTransform.Place(new Vector2(0.5f, 0.14f), Center, Vector2.zero, new Vector2(900f, 44f));

            // It climbs, slows, and stalls.
            float p = 0f;
            while (p < 0.63f)
            {
                p = Mathf.MoveTowards(p, 0.63f, Time.unscaledDeltaTime * (p < 0.5f ? 0.22f : 0.06f));
                fill.rectTransform.anchorMax = new Vector2(p, 1f);
                percent.text = UIText.Get("prologue.transfer", "pct", Mathf.RoundToInt(p * 100f).ToString());
                yield return null;
            }
            yield return Wait(0.8f);

            // Glitch: the screen tears, the room light stutters and the readout fails.
            for (int i = 0; i < 26; i++)
            {
                jitter = new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(-8f, 8f));
                fill.color = i % 2 == 0 ? new Color(1f, 0.35f, 0.3f) : new Color(0.55f, 0.9f, 0.8f);
                percent.text = UIText.Get("prologue.transfer", "pct", i % 3 == 0 ? "##" : "63");
                line.text = i % 4 == 0 ? procedure.Substring(0, Mathf.Min(procedure.Length, 16)) + "—" : "";
                if (pc != null) pc.SetGlow(i % 2 == 0 ? new Color(0.5f, 0.12f, 0.1f) : ScreenGlow);
                if (sun != null) sun.intensity = sunIntensity * (i % 3 == 0 ? 0.55f : 1f);
                yield return new WaitForSecondsRealtime(0.05f);
            }
            jitter = Vector2.zero;
            if (sun != null) sun.intensity = sunIntensity;
            line.text = "";
            percent.color = new Color(1f, 0.4f, 0.35f);
            percent.text = UIText.Get("prologue.transfer.failed");

            // The room goes to emergency lighting: everything else off, red and black, alarm lights pulsing.
            Emergency();
            for (float t = 0f; t < 2.8f && !(advance && t > 1.2f); t += Time.unscaledDeltaTime) yield return null;
            advance = false;
        }

        /// <summary>The crash: a hard cut to black, held a moment before the city (whose awakening fades in).</summary>
        IEnumerator CutToBlack()
        {
            black.rectTransform.SetAsLastSibling();
            black.color = Color.black;
            hint.gameObject.SetActive(false);
            timecode = null;
            yield return new WaitForSecondsRealtime(0.9f);
        }

        // ------------------------------------------------------------------ Emergency lighting

        readonly List<Light> alarms = new List<Light>();
        readonly List<Light> switchedOff = new List<Light>();
        bool emergency;
        Color oldSky, oldEquator, oldGround, oldFog;

        /// <summary>Kills the room's light (sun, ambient, the capsules' glow) down to a dark red and hangs two red alarm
        /// lights over the desk, pulsing; the monitor glows red.</summary>
        void Emergency()
        {
            emergency = true;
            oldSky = RenderSettings.ambientSkyColor;
            oldEquator = RenderSettings.ambientEquatorColor;
            oldGround = RenderSettings.ambientGroundColor;
            oldFog = RenderSettings.fogColor;
            RenderSettings.ambientSkyColor = new Color(0.1f, 0.01f, 0.01f);
            RenderSettings.ambientEquatorColor = new Color(0.05f, 0f, 0f);
            RenderSettings.ambientGroundColor = Color.black;
            RenderSettings.fogColor = new Color(0.06f, 0f, 0f);
            DynamicGI.UpdateEnvironment();
            foreach (var light in FindObjectsOfType<Light>())
                if (light.enabled && light != sun && light.transform.parent != (pc != null ? pc.transform : null))
                {
                    light.enabled = false;
                    switchedOff.Add(light);
                }
            if (sun != null) sun.intensity = 0f;
            if (pc != null) pc.SetGlow(new Color(0.45f, 0.04f, 0.03f));
            if (page != null)
            {
                var red = UIFactory.CreateImage(page, "Emergency", new Color(0.8f, 0.05f, 0.03f, 0.22f));
                red.rectTransform.Fill();
            }

            var centre = cam != null ? seatPosition : Vector3.zero;
            var side = cam != null ? cam.transform.right : Vector3.right;
            for (int i = 0; i < 2; i++)
            {
                var alarm = new GameObject("AlarmLight").AddComponent<Light>();
                alarm.transform.position = centre + Vector3.up * 1.4f + side * (i == 0 ? -1.6f : 1.6f) + (pc != null ? -pc.Facing * 1.2f : Vector3.zero);
                alarm.type = LightType.Point;
                alarm.color = new Color(1f, 0.08f, 0.05f);
                alarm.range = 6f;
                alarm.shadows = LightShadows.None;
                alarms.Add(alarm);
            }
        }

        /// <summary>The alarm lights' pulse: a hard red swell and a stretch of near dark, the two out of step.</summary>
        void PulseAlarms()
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < alarms.Count; i++)
            {
                if (alarms[i] == null) continue;
                float wave = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 5.5f + i * Mathf.PI)), 2f);
                alarms[i].intensity = 0.15f + wave * 3.2f;
            }
        }

        /// <summary>A terminal line on the monitor: 1-12 letters, digits, spaces, - and ', a green block caret. Enter
        /// confirms (an empty name becomes "A3").</summary>
        static InputField NameField(RectTransform parent, Action<string> confirmed)
        {
            var box = UIFactory.CreateImage(parent, "NameField", new Color(0f, 0f, 0f, 0.35f), true);
            box.rectTransform.anchorMin = new Vector2(0.12f, 0.27f);
            box.rectTransform.anchorMax = new Vector2(0.88f, 0.5f);
            box.rectTransform.offsetMin = box.rectTransform.offsetMax = Vector2.zero;
            var rule = UIFactory.CreateImage(box.transform, "Rule", new Color(TerminalGreen.r, TerminalGreen.g, TerminalGreen.b, 0.6f));
            rule.rectTransform.anchorMin = new Vector2(0.06f, 0f);
            rule.rectTransform.anchorMax = new Vector2(0.94f, 0f);
            rule.rectTransform.pivot = new Vector2(0.5f, 0f);
            rule.rectTransform.offsetMin = new Vector2(0f, 6f);
            rule.rectTransform.offsetMax = new Vector2(0f, 9f);
            var text = UIFactory.CreateText(box.transform, "Text", "", UIFactory.TextSize.XLarge, TerminalGreen, TextAnchor.MiddleCenter);
            text.rectTransform.Fill(8f);
            text.supportRichText = false;
            var placeholder = UIFactory.CreateText(box.transform, "Placeholder", "A3", UIFactory.TextSize.XLarge, new Color(TerminalGreen.r, TerminalGreen.g, TerminalGreen.b, 0.25f), TextAnchor.MiddleCenter);
            placeholder.rectTransform.Fill(8f);
            var field = box.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = 12;
            field.lineType = InputField.LineType.SingleLine;
            field.customCaretColor = true;
            field.caretColor = TerminalGreen;
            field.caretWidth = 8;
            field.caretBlinkRate = 1.6f;
            field.selectionColor = new Color(TerminalGreen.r, TerminalGreen.g, TerminalGreen.b, 0.3f);
            field.onValidateInput += (s, i, c) => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '\'' ? c : '\0';
            field.onEndEdit.AddListener(value =>
            {
                if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
                string name = (value ?? "").Trim();
                confirmed(name.Length == 0 ? "A3" : name);
            });
            return field;
        }

        // ------------------------------------------------------------------ Skip Intro
        // Bottom right, like the card packs' Skip: an ink plate that fills with gold while it's held (the mouse, or Esc).
        // Full: the whole opening is skipped and you wake in the city (named A3, or what's typed in the name box so far).
        // Faint until the mouse is over it or it's being held. A tap of Esc still only skips the recording.
        RectTransform skipPlate, skipFill;
        CanvasGroup skipGroup;
        float skipHeld;
        bool mouseOnSkip, mouseOverSkip, skippingAll;
        const float SkipHoldSeconds = 0.6f, SkipIdleAlpha = 0.25f;

        void BuildSkip()
        {
            var plate = InkStyle.Panel(root, "SkipIntro", InkStyle.InkPanel, true);
            skipPlate = plate.rectTransform;
            skipGroup = plate.gameObject.AddComponent<CanvasGroup>();
            skipGroup.alpha = SkipIdleAlpha;
            var corner = new Vector2(1f, 0f);
            skipPlate.Place(corner, corner, new Vector2(-60f, 60f), new Vector2(480f, HudView.GoSize.y));
            InkStyle.Frame(skipPlate, InkStyle.Rule, 2f);
            var fill = UIFactory.CreateImage(skipPlate, "Fill", UIFactory.WithAlpha(InkStyle.Gold, 0.6f));
            fill.raycastTarget = false;
            skipFill = fill.rectTransform;
            skipFill.anchorMin = Vector2.zero;
            skipFill.anchorMax = new Vector2(0f, 1f);
            skipFill.offsetMin = skipFill.offsetMax = Vector2.zero;
            var label = InkStyle.Title(skipPlate, "Label", UIText.Get("prologue.skip_intro"), UIFactory.TextSize.Large, InkStyle.Paper, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            label.rectTransform.Fill(8f);

            var trigger = plate.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            void On(UnityEngine.EventSystems.EventTriggerType type, Action act)
            {
                var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(_ => act());
                trigger.triggers.Add(entry);
            }
            On(UnityEngine.EventSystems.EventTriggerType.PointerDown, () => mouseOnSkip = true);
            On(UnityEngine.EventSystems.EventTriggerType.PointerUp, () => mouseOnSkip = false);
            On(UnityEngine.EventSystems.EventTriggerType.PointerEnter, () => mouseOverSkip = true);
            On(UnityEngine.EventSystems.EventTriggerType.PointerExit, () => { mouseOverSkip = false; mouseOnSkip = false; });
        }

        void UpdateSkip()
        {
            if (skipPlate == null || skippingAll) return;
            skipPlate.SetAsLastSibling();   // over the pages the opening adds as it goes
            bool held = mouseOnSkip || Input.GetKey(KeyCode.Escape);
            skipHeld = held ? skipHeld + Time.unscaledDeltaTime : 0f;
            skipFill.anchorMax = new Vector2(Mathf.Clamp01(skipHeld / SkipHoldSeconds), 1f);
            float alpha = mouseOverSkip || skipHeld > 0f ? 1f : SkipIdleAlpha;
            skipGroup.alpha = Mathf.MoveTowards(skipGroup.alpha, alpha, 6f * Time.unscaledDeltaTime);
            if (skipHeld >= SkipHoldSeconds) SkipAll();
        }

        /// <summary>Ends the opening now: keeps (or gives) the name, cuts to black, and hands over to the city.</summary>
        void SkipAll()
        {
            if (skippingAll) return;
            skippingAll = true;
            StopAllCoroutines();
            if (!StoryState.Has(Chapter1.Named))
            {
                var box = GetComponentInChildren<InputField>();
                string typed = box != null ? (box.text ?? "").Trim() : "";
                StoryState.SetName(typed.Length > 0 ? typed : "A3");
                StoryState.Set(Chapter1.Named);
                SaveGame.Save();
            }
            if (black != null)
            {
                black.rectTransform.SetAsLastSibling();
                black.color = Color.black;
            }
            var then = done;
            done = null;
            Destroy(gameObject);
            if (then != null) then();
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>The middle of the monitor, where the narration sits.</summary>
        static void Middle(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.06f, 0.08f);
            rt.anchorMax = new Vector2(0.94f, 0.72f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>A CRT switching on: a bright line opening to the full page, the glass lighting up.</summary>
        IEnumerator PowerOn(Color glow)
        {
            page.localScale = new Vector3(1f, 0.02f, 1f);
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.35f;
                page.localScale = new Vector3(1f, Mathf.Lerp(0.02f, 1f, k * k), 1f);
                if (pc != null) pc.SetGlow(Color.Lerp(Color.white, glow, k));
                yield return null;
            }
            page.localScale = Vector3.one;
            if (pc != null) pc.SetGlow(glow);
        }

        /// <summary>Types a line out; a press finishes it, a second press moves on (Esc skips the recording).</summary>
        IEnumerator Type(string text)
        {
            advance = false;
            float shown = 0f;
            while (shown < text.Length && !skip)
            {
                if (advance) { shown = text.Length; advance = false; }
                shown += Time.unscaledDeltaTime * TypeSpeed;
                line.text = text.Substring(0, Mathf.Min(text.Length, (int)shown));
                yield return null;
            }
            line.text = text;
            float wait = 0f;
            while (!advance && !skip && wait < 2.6f + text.Length * 0.03f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            advance = false;
        }

        IEnumerator Wait(float seconds)
        {
            float t = 0f;
            advance = false;
            while (t < seconds && !advance)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            advance = false;
        }

        static IEnumerator Fade(Image image, float alpha, float seconds)
        {
            var from = image.color;
            var to = new Color(from.r, from.g, from.b, alpha);
            for (float t = 0f; t < seconds && image != null; t += Time.unscaledDeltaTime)
            {
                image.color = Color.Lerp(from, to, t / seconds);
                yield return null;
            }
            if (image != null) image.color = to;
        }

        static void Scanlines(RectTransform parent)
        {
            var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            tex.SetPixels(new[] { new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.15f) });
            tex.Apply();
            var img = new GameObject("Scanlines", typeof(RectTransform)).AddComponent<RawImage>();
            img.rectTransform.SetParent(parent, false);
            img.rectTransform.Fill();
            img.texture = tex;
            img.raycastTarget = false;
            img.uvRect = new Rect(0f, 0f, 1f, 840f / 4f);
        }
    }
}
