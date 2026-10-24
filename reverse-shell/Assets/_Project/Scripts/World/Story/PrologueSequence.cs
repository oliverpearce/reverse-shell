using System;
using System.Collections;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The opening of a new game (bible §9, Prologue): an old public-information recording promising EVERLASTING life
    /// through Reverse Shell, the procedure starting and failing, then the player naming the protagonist.
    /// Enter / Space / click advances a line; Esc skips the recording (never the naming). Runs on unscaled time over the
    /// overworld canvas, then calls <c>done</c> (the awakening follows).
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

        RectTransform root, screen;
        Text line, timecode, rec;
        Action done;
        bool skip, advance;
        float started;

        public static PrologueSequence Play(RectTransform canvas, Action done)
        {
            var rt = UIFactory.CreateRect("Prologue", canvas).Fill();
            rt.SetAsLastSibling();
            var seq = rt.gameObject.AddComponent<PrologueSequence>();
            seq.root = rt;
            seq.done = done;
            seq.StartCoroutine(seq.Run());
            return seq;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                advance = true;
            if (Input.GetKeyDown(KeyCode.Escape)) skip = true;
            if (timecode != null)
            {
                float t = Time.unscaledTime - started;
                timecode.text = string.Format("{0:00}:{1:00}:{2:00}", (int)(t / 60f), (int)t % 60, (int)(t * 25f) % 25);
                rec.enabled = ((int)(t * 2f)) % 2 == 0;
            }
        }

        IEnumerator Run()
        {
            var black = UIFactory.CreateImage(root, "Black", Color.black, true);
            black.rectTransform.Fill();
            yield return Recording1();
            yield return Failure();
            yield return Naming();
            Destroy(gameObject);
            if (done != null) done();
        }

        // ------------------------------------------------------------------ 1. The recording

        IEnumerator Recording1()
        {
            started = Time.unscaledTime;
            var center = new Vector2(0.5f, 0.5f);
            var frame = UIFactory.CreateImage(root, "Screen", new Color(0.09f, 0.14f, 0.15f), false);
            screen = frame.rectTransform;
            screen.Place(center, center, Vector2.zero, new Vector2(1500f, 840f));
            var glow = InkStyle.Gradient(screen, new Color(0.2f, 0.32f, 0.3f, 0.6f), new Color(0.04f, 0.06f, 0.07f, 0.9f));
            glow.rectTransform.Fill();
            Scanlines(screen);
            InkStyle.Frame(screen, new Color(0.6f, 0.75f, 0.7f, 0.35f), 3f);

            var brand = InkStyle.Title(screen, "Brand", "REVERSE SHELL", 54, new Color(0.85f, 0.95f, 0.9f), TextAnchor.UpperCenter);
            brand.rectTransform.TopStrip(48f, 70f);
            var tag = UIFactory.CreateText(screen, "Tag", "A PUBLIC INFORMATION RECORDING", 24, new Color(0.7f, 0.85f, 0.8f, 0.8f), TextAnchor.UpperCenter);
            tag.rectTransform.TopStrip(118f, 34f);
            rec = UIFactory.CreateText(screen, "Rec", "• REC", 28, new Color(1f, 0.35f, 0.3f), TextAnchor.UpperLeft);
            rec.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -28f), new Vector2(200f, 40f));
            timecode = UIFactory.CreateText(screen, "Time", "", 28, new Color(0.85f, 0.95f, 0.9f, 0.8f), TextAnchor.UpperRight);
            timecode.rectTransform.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -28f), new Vector2(300f, 40f));

            line = UIFactory.CreateText(screen, "Line", "", 44, new Color(0.92f, 0.97f, 0.94f), TextAnchor.MiddleCenter);
            line.rectTransform.Place(center, center, new Vector2(0f, -20f), new Vector2(1240f, 360f));
            line.supportRichText = true;
            var hint = UIFactory.CreateText(root, "Hint", "Enter  next      Esc  skip", 26, new Color(1f, 1f, 1f, 0.45f), TextAnchor.LowerCenter);
            hint.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(900f, 40f));

            yield return FadeIn(screen.gameObject, 1.2f);
            foreach (var text in Script("prologue.recording"))
            {
                if (skip) break;
                yield return Type(text);
            }
            if (!skip)
            {
                // The tagline: EVERLASTING, slow and gold.
                line.fontSize = UIFactory.Px(44);
                foreach (var text in Script("prologue.tagline"))
                    if (!skip) yield return Type(text);
                if (!skip)
                {
                    line.text = "";
                    var word = InkStyle.Title(screen, "Everlasting", "", 120, InkStyle.GoldBright, TextAnchor.MiddleCenter);
                    word.rectTransform.Place(center, center, new Vector2(0f, -20f), new Vector2(1400f, 200f));
                    const string everlasting = "EVERLASTING";
                    for (int i = 1; i <= everlasting.Length && !skip; i++)
                    {
                        word.text = everlasting.Substring(0, i);
                        yield return new WaitForSecondsRealtime(0.12f);
                    }
                    yield return Wait(2.2f);
                    Destroy(word.gameObject);
                }
            }
            hint.text = "Enter  next";
        }

        // ------------------------------------------------------------------ 2. The procedure fails

        IEnumerator Failure()
        {
            skip = false;
            string procedure = First("prologue.procedure");
            line.text = procedure;
            var center = new Vector2(0.5f, 0.5f);
            var track = UIFactory.CreateImage(screen, "Track", new Color(1f, 1f, 1f, 0.12f));
            track.rectTransform.Place(center, center, new Vector2(0f, -170f), new Vector2(900f, 28f));
            var fill = UIFactory.CreateImage(track.rectTransform, "Fill", new Color(0.55f, 0.9f, 0.8f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            var percent = UIFactory.CreateText(screen, "Percent", "TRANSFER 0%", 30, new Color(0.85f, 0.95f, 0.9f), TextAnchor.MiddleCenter);
            percent.rectTransform.Place(center, center, new Vector2(0f, -220f), new Vector2(900f, 44f));

            // It climbs, slows, and stalls.
            float p = 0f;
            while (p < 0.63f)
            {
                p = Mathf.MoveTowards(p, 0.63f, Time.unscaledDeltaTime * (p < 0.5f ? 0.22f : 0.06f));
                fill.rectTransform.anchorMax = new Vector2(p, 1f);
                percent.text = "TRANSFER " + Mathf.RoundToInt(p * 100f) + "%";
                yield return null;
            }
            yield return Wait(0.8f);

            // Glitch: the screen tears and the readout fails.
            var origin = screen.anchoredPosition;
            for (int i = 0; i < 26; i++)
            {
                screen.anchoredPosition = origin + new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(-8f, 8f));
                fill.color = i % 2 == 0 ? new Color(1f, 0.35f, 0.3f) : new Color(0.55f, 0.9f, 0.8f);
                percent.text = i % 3 == 0 ? "TRANSFER ##%" : "TRANSFER 63%";
                line.text = i % 4 == 0 ? procedure.Substring(0, Mathf.Min(procedure.Length, 16)) + "—" : "";
                yield return new WaitForSecondsRealtime(0.05f);
            }
            screen.anchoredPosition = origin;
            line.text = "";
            percent.color = new Color(1f, 0.4f, 0.35f);
            percent.text = "TRANSFER INCOMPLETE";
            yield return Wait(1.6f);
            screen.gameObject.SetActive(false);
            timecode = null;
            yield return new WaitForSecondsRealtime(1.2f);
        }

        // ------------------------------------------------------------------ 3. A name

        IEnumerator Naming()
        {
            var center = new Vector2(0.5f, 0.5f);
            var prompt = UIFactory.CreateText(root, "Prompt", "", 40, new Color(0.9f, 0.9f, 0.86f), TextAnchor.MiddleCenter);
            prompt.rectTransform.Place(center, center, new Vector2(0f, 150f), new Vector2(1400f, 120f));
            line = prompt;
            advance = false;
            foreach (var text in Script("prologue.name")) yield return Type(text);
            prompt.text = First("prologue.name.question");

            string chosen = null;
            var field = NameField(root, center, n => chosen = n);
            var note = UIFactory.CreateText(root, "Note", "Type a name, then Enter", 26, new Color(1f, 1f, 1f, 0.45f), TextAnchor.UpperCenter);
            note.rectTransform.Place(center, center, new Vector2(0f, -90f), new Vector2(900f, 40f));
            while (chosen == null)
            {
                if (!field.isFocused) field.ActivateInputField();
                yield return null;
            }
            Destroy(field.gameObject);
            Destroy(note.gameObject);

            StoryState.SetName(chosen);
            StoryState.Set(Chapter1.Named);
            SaveGame.Save();
            prompt.text = "";
            yield return Type(chosen + ".");
            yield return Wait(1.0f);
        }

        /// <summary>A one-line text box in the pixel font: 1-12 letters, digits, spaces, - and '. Enter confirms
        /// (an empty name becomes "MC").</summary>
        static InputField NameField(RectTransform parent, Vector2 center, Action<string> confirmed)
        {
            var box = UIFactory.CreateImage(parent, "NameField", new Color(0.08f, 0.075f, 0.07f, 0.95f), true);
            box.rectTransform.Place(center, center, new Vector2(0f, 0f), new Vector2(640f, 100f));
            InkStyle.Frame(box.rectTransform, InkStyle.GoldBright, 3f);
            var text = UIFactory.CreateText(box.transform, "Text", "", 56, InkStyle.Paper, TextAnchor.MiddleCenter);
            text.rectTransform.Fill(12f);
            text.supportRichText = false;
            var placeholder = UIFactory.CreateText(box.transform, "Placeholder", "MC", 56, new Color(1f, 1f, 1f, 0.25f), TextAnchor.MiddleCenter);
            placeholder.rectTransform.Fill(12f);
            var field = box.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = 12;
            field.lineType = InputField.LineType.SingleLine;
            field.onValidateInput += (s, i, c) => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '\'' ? c : '\0';
            field.onEndEdit.AddListener(value =>
            {
                if (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) return;
                string name = (value ?? "").Trim();
                confirmed(name.Length == 0 ? "MC" : name);
            });
            return field;
        }

        // ------------------------------------------------------------------ Helpers

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

        static IEnumerator FadeIn(GameObject go, float seconds)
        {
            var group = go.AddComponent<CanvasGroup>();
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = t / seconds;
                yield return null;
            }
            group.alpha = 1f;
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
