using System;
using System.Collections;
using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// What a full-screen story cutscene is built on (Chapter One's reflection at the start, its ending at the end):
    /// a dark root over the world but under the dialogue box (so the lines show over the panels), a stage to build each
    /// panel on, a black layer for cuts, Hold to Skip (top right, or hold Esc), and pixel art drawn in code at one art
    /// pixel = <see cref="px"/> canvas units. A sequence overrides <see cref="Run"/> and calls <see cref="Finish"/>.
    /// </summary>
    public abstract class StoryPanels : MonoBehaviour
    {
        protected static readonly Color Night = new Color(0.035f, 0.04f, 0.05f);

        protected OverworldBuilder world;
        protected RectTransform root, stage;
        protected Image black;
        protected float px;      // canvas units per art pixel
        protected Vector2 size;  // the canvas
        CanvasGroup group;
        Action done;

        /// <summary>Makes the panels' root (named <paramref name="name"/>) and the sequence on it; the caller sets its own
        /// fields, then calls <see cref="Begin"/>. <paramref name="finished"/> runs when it's over (or skipped).</summary>
        protected static T Open<T>(OverworldBuilder world, string name, Action finished) where T : StoryPanels
        {
            var canvas = (RectTransform)world.UI.transform;
            var root = UIFactory.CreateRect(name, canvas).Fill();
            var dialogue = world.UI.Dialogue != null ? world.UI.Dialogue.transform : null;
            while (dialogue != null && dialogue.parent != canvas) dialogue = dialogue.parent;
            if (dialogue != null) root.SetSiblingIndex(dialogue.GetSiblingIndex());
            else root.SetAsLastSibling();
            // The world's fade sits over the dialogue box; the panels have their own black.
            world.UI.SetFade(new Color(0f, 0f, 0f, 0f));
            var seq = root.gameObject.AddComponent<T>();
            seq.world = world;
            seq.root = root;
            seq.done = finished;
            seq.Build();
            return seq;
        }

        protected void Begin() { StartCoroutine(Run()); }

        protected abstract IEnumerator Run();

        void Build()
        {
            group = root.gameObject.AddComponent<CanvasGroup>();
            var bg = UIFactory.CreateImage(root, "Night", Night, true);
            bg.rectTransform.Fill();
            stage = UIFactory.CreateRect("Stage", root).Fill();
            black = UIFactory.CreateImage(root, "Black", Color.black);
            black.rectTransform.Fill();
            black.raycastTarget = false;
            size = root.rect.size;
            if (size.y < 10f) size = UIFactory.ReferenceResolution;
            px = Mathf.Max(2f, Mathf.Round(size.y / 150f));
            BuildSkip();
        }

        // ------------------------------------------------------------------ shared beats

        protected IEnumerator Talk(string key)
        {
            if (!StoryText.Has(key)) yield break;
            bool finished = false;
            world.UI.Talk(StoryText.Lines(key, ChapterOneDirector.VoiceFor), () => finished = true);
            while (!finished) yield return null;
        }

        /// <summary>A hard cut to black and away from the last panel.</summary>
        protected IEnumerator Cut()
        {
            black.color = Color.black;
            yield return new WaitForSecondsRealtime(0.35f);
            UIFactory.ClearChildren(stage);
            yield return null;
        }

        protected IEnumerator FromBlack(float seconds)
        {
            yield return Tween.Run(seconds, p => black.color = new Color(0f, 0f, 0f, 1f - p), null, true);
        }

        protected IEnumerator ToBlack(float seconds)
        {
            yield return Tween.Run(seconds, p => black.color = new Color(0f, 0f, 0f, p), null, true);
        }

        /// <summary>In the dark: a heartbeat, a dull red pulse (lub-dub) per beat.</summary>
        protected IEnumerator Heartbeat(int beats)
        {
            black.color = Color.black;
            var pulse = UIFactory.CreateImage(root, "Pulse", new Color(0.5f, 0.05f, 0.05f, 0f));
            pulse.sprite = UiParticles.GlowSprite();
            pulse.rectTransform.Fill(-size.x * 0.2f);
            pulse.raycastTarget = false;
            for (int i = 0; i < beats; i++)
            {
                foreach (float peak in new[] { 0.55f, 0.35f })
                {
                    yield return Tween.Run(0.12f, p => pulse.color = new Color(0.5f, 0.05f, 0.05f, peak * p), null, true);
                    yield return Tween.Run(0.3f, p => pulse.color = new Color(0.5f, 0.05f, 0.05f, peak * (1f - p)), null, true);
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Destroy(pulse.gameObject);
        }

        /// <summary>Opening your eyes: the lids part a little, close, then open, the panel coming up out of the dark.</summary>
        protected IEnumerator Eyelids()
        {
            var top = UIFactory.CreateImage(root, "LidTop", Color.black);
            var bottom = UIFactory.CreateImage(root, "LidBottom", Color.black);
            foreach (var lid in new[] { top, bottom }) lid.raycastTarget = false;
            float half = size.y * 0.5f;
            void Lids(float open)
            {
                float h = half * (1f - open) + 2f;
                top.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(size.x + 40f, h));
                bottom.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(size.x + 40f, h));
            }
            Lids(0f);
            black.color = Color.clear;
            yield return Tween.Run(0.9f, p => Lids(0.25f * Tween.InOutQuad(p)), null, true);
            yield return Tween.Run(0.35f, p => Lids(0.25f * (1f - p)), null, true);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Tween.Run(1.6f, p => Lids(Tween.OutCubic(p)), null, true);
            Destroy(top.gameObject);
            Destroy(bottom.gameObject);
        }

        /// <summary>The panels fade away to show the world again, then the sequence ends.</summary>
        protected IEnumerator FadeAway(float seconds)
        {
            if (skipPlate != null) skipPlate.gameObject.SetActive(false);
            yield return Tween.Run(seconds, p => group.alpha = 1f - p, null, true);
            Finish();
        }

        protected void Finish()
        {
            if (done == null) return;
            var then = done;
            done = null;
            StopAllCoroutines();
            Destroy(root.gameObject);
            then();
        }

        /// <summary>Plain text (no {colour} markup), for machine readouts drawn outside the dialogue box.</summary>
        protected static string StripMarkup(string s)
        {
            return System.Text.RegularExpressions.Regex.Replace(s ?? "", @"\{[^}]*\}", "");
        }

        // ------------------------------------------------------------------ Hold to Skip

        RectTransform skipPlate, skipFill;
        CanvasGroup skipGroup;
        float skipHeld;
        bool mouseOnSkip, mouseOverSkip, skipping;
        const float SkipHoldSeconds = 0.6f, SkipIdleAlpha = 0.25f;

        void BuildSkip()
        {
            var plate = InkStyle.Panel(root, "Skip", InkStyle.InkPanel, true);
            skipPlate = plate.rectTransform;
            skipGroup = plate.gameObject.AddComponent<CanvasGroup>();
            skipGroup.alpha = SkipIdleAlpha;
            var corner = new Vector2(1f, 1f);
            skipPlate.Place(corner, corner, new Vector2(-60f, -60f), new Vector2(360f, HudView.GoSize.y));
            InkStyle.Frame(skipPlate, InkStyle.Rule, 2f);
            var fill = UIFactory.CreateImage(skipPlate, "Fill", UIFactory.WithAlpha(InkStyle.Gold, 0.6f));
            skipFill = fill.rectTransform;
            skipFill.anchorMin = Vector2.zero;
            skipFill.anchorMax = new Vector2(0f, 1f);
            skipFill.offsetMin = skipFill.offsetMax = Vector2.zero;
            var label = InkStyle.Title(skipPlate, "Label", UIText.Get("ending.skip"), UIFactory.TextSize.Large, InkStyle.Paper, TextAnchor.MiddleCenter);
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

        void Update()
        {
            if (skipping)
            {
                // Run the open conversation out (each frame finishes a line), then end.
                if (world.UI.DialogueOpen) world.UI.Dialogue.Advance();
                else Finish();
                return;
            }
            if (skipPlate == null || !skipPlate.gameObject.activeSelf) return;
            skipPlate.SetAsLastSibling();
            bool held = mouseOnSkip || Input.GetKey(KeyCode.Escape);
            skipHeld = held ? skipHeld + Time.unscaledDeltaTime : 0f;
            skipFill.anchorMax = new Vector2(Mathf.Clamp01(skipHeld / SkipHoldSeconds), 1f);
            float alpha = mouseOverSkip || skipHeld > 0f ? 1f : SkipIdleAlpha;
            skipGroup.alpha = Mathf.MoveTowards(skipGroup.alpha, alpha, 6f * Time.unscaledDeltaTime);
            if (skipHeld >= SkipHoldSeconds)
            {
                skipping = true;
                StopAllCoroutines();
                UIFactory.ClearChildren(stage);
                black.color = Color.black;
                skipPlate.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ pixel art drawn in code

        /// <summary>A point-filtered texture from a function of (x, y), y counting from the top.</summary>
        protected static Texture2D Draw(int w, int h, Func<int, int, Color32> at)
        {
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pixels[(h - 1 - y) * w + x] = at(x, y);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        protected static RawImage Raw(Transform parent, string name, Texture tex)
        {
            var raw = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
            raw.transform.SetParent(parent, false);
            raw.texture = tex;
            raw.raycastTarget = false;
            return raw;
        }
    }
}
