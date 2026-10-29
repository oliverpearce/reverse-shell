using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The title's terminal look: the name types itself in letter by letter behind a blinking "_" cursor, over a gold
    /// copy of itself a few pixels off (the doubled text) that now and then jumps, like a screen echo. Left-aligned in
    /// a box as wide as the whole name, so the letters never shift while it types. Unscaled time (the title pauses
    /// the world).
    /// </summary>
    public class TerminalTitle : MonoBehaviour
    {
        const float StartDelay = 0.35f, CharSeconds = 0.09f, BlinkSeconds = 0.5f;
        const float GlitchSeconds = 0.12f, GlitchMin = 2.5f, GlitchMax = 5f;
        const string Cursor = "_", HiddenCursor = "<color=#00000000>_</color>";
        static readonly Vector2 EchoOffset = new Vector2(6f, -5f);

        Text main, echo;
        string full, shownText;
        float time, glitchUntil, nextGlitch;

        public static TerminalTitle Create(Transform parent, string text, int size, Vector2 position, float height)
        {
            var center = new Vector2(0.5f, 0.5f);
            var box = UIFactory.CreateRect("Title", parent);
            var title = box.gameObject.AddComponent<TerminalTitle>();
            title.full = text ?? "";

            title.echo = InkStyle.Title(box, "Echo", title.full + Cursor, size, UIFactory.WithAlpha(InkStyle.Gold, 0.55f), TextAnchor.MiddleLeft);
            title.main = InkStyle.Title(box, "Name", title.full + Cursor, size, InkStyle.Paper, TextAnchor.MiddleLeft);
            title.main.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            foreach (var t in new[] { title.echo, title.main })
            {
                t.supportRichText = true;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            float width = title.main.preferredWidth + 20f;
            box.Place(center, center, position, new Vector2(width, height));
            title.main.rectTransform.Fill();
            title.echo.rectTransform.Fill();
            title.echo.rectTransform.anchoredPosition = EchoOffset;
            title.nextGlitch = StartDelay + title.full.Length * CharSeconds + GlitchMin;
            title.Show(0, true);
            return title;
        }

        void Update()
        {
            time += Time.unscaledDeltaTime;
            int shown = Mathf.Clamp(Mathf.FloorToInt((time - StartDelay) / CharSeconds), 0, full.Length);
            bool typing = shown < full.Length;
            Show(shown, typing || Mathf.Repeat(time, BlinkSeconds * 2f) < BlinkSeconds);

            // Once typed, the echo jumps sideways for a moment every few seconds.
            if (!typing && time >= nextGlitch)
            {
                glitchUntil = time + GlitchSeconds;
                nextGlitch = time + Random.Range(GlitchMin, GlitchMax);
                echo.rectTransform.anchoredPosition = EchoOffset + new Vector2(Random.Range(-10f, 10f), Random.Range(-3f, 3f));
            }
            else if (time >= glitchUntil && echo.rectTransform.anchoredPosition != EchoOffset)
                echo.rectTransform.anchoredPosition = EchoOffset;
        }

        void Show(int shown, bool cursorOn)
        {
            string s = full.Substring(0, shown) + (cursorOn ? Cursor : HiddenCursor);
            if (s == shownText) return;
            shownText = s;
            main.text = s;
            echo.text = s;
        }
    }
}
