using Combat.View;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
    /// <summary>
    /// The scanner's HUD slot, bottom-left, once it's picked up (Chapter One): the binoculars icon (Tools/CardForge
    /// "Ui_Binoculars") in an ink box with a gold hairline and a Q key badge. After a scan it goes dark and a clock wipe
    /// unwinds over the cooldown (no numbers), then it flashes once when it's ready again. Pressing Q too early
    /// shakes it. The cooldown runs on game time, so it waits while a menu pauses the world.
    /// </summary>
    public class ScannerHud : MonoBehaviour
    {
        const float BoxSize = 120f, IconSize = 96f;   // the 32 px icon at 3x

        RectTransform box;
        CanvasGroup group;
        Image icon, wipe, flash;
        Text key;
        float cooldown, cooldownLength = 1f;
        float shake;

        /// <summary>True when Q can scan again.</summary>
        public bool Ready { get { return cooldown <= 0f; } }

        public static ScannerHud Create(RectTransform parent)
        {
            var rt = UIFactory.CreateRect("ScannerHud", parent)
                .Place(Vector2.zero, Vector2.zero, new Vector2(28f, 28f), new Vector2(BoxSize, BoxSize));
            var hud = rt.gameObject.AddComponent<ScannerHud>();
            hud.Build(rt);
            rt.gameObject.SetActive(false);
            return hud;
        }

        void Build(RectTransform rt)
        {
            group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            box = UIFactory.CreateRect("Box", rt).Fill();
            var center = new Vector2(0.5f, 0.5f);

            UIFactory.CreateImage(box, "Panel", InkStyle.InkPanel).rectTransform.Fill();
            InkStyle.Frame(box, InkStyle.Gold, 2f);

            icon = UIFactory.CreateImage(box, "Icon", Color.white);
            icon.sprite = CardArt.Icon("Ui_Binoculars");
            icon.preserveAspect = true;
            icon.rectTransform.Place(center, center, Vector2.zero, new Vector2(IconSize, IconSize));

            // The cooldown: a dark clock wipe that unwinds.
            wipe = UIFactory.CreateImage(box, "Wipe", new Color(0.05f, 0.045f, 0.04f, 0.72f));
            wipe.sprite = WhiteSprite();
            wipe.type = Image.Type.Filled;
            wipe.fillMethod = Image.FillMethod.Radial360;
            wipe.fillOrigin = (int)Image.Origin360.Top;
            wipe.fillClockwise = false;
            wipe.rectTransform.Fill();

            flash = UIFactory.CreateImage(box, "Flash", new Color(1f, 1f, 1f, 0f));
            flash.rectTransform.Fill();

            // The Q badge on the top-right corner: a paper key with the letter in ink.
            var badge = UIFactory.CreateImage(box, "Key", InkStyle.Paper);
            badge.rectTransform.Place(new Vector2(1f, 1f), center, new Vector2(-6f, -6f), new Vector2(46f, 46f));
            InkStyle.Frame(badge.rectTransform, InkStyle.Ink, 2f);
            key = UIFactory.CreateText(badge.rectTransform, "Q", "Q", 34, InkStyle.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            key.rectTransform.Fill();

            Refresh();
        }

        /// <summary>Shows or hides the slot (only once the scanner is picked up, and not over a conversation).</summary>
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        /// <summary>A scan just went off: dark for <paramref name="length"/> seconds.</summary>
        public void StartCooldown(float length)
        {
            cooldownLength = Mathf.Max(0.01f, length);
            cooldown = cooldownLength;
            Refresh();
        }

        /// <summary>Q pressed while cooling down: a short shake.</summary>
        public void Deny() { shake = 0.25f; }

        void Update()
        {
            if (cooldown > 0f)
            {
                cooldown -= Time.deltaTime;
                if (cooldown <= 0f)
                {
                    cooldown = 0f;
                    flash.color = new Color(1f, 0.95f, 0.8f, 0.55f);   // ready again
                }
                Refresh();
            }
            if (flash.color.a > 0f)
            {
                var c = flash.color;
                c.a = Mathf.Max(0f, c.a - Time.unscaledDeltaTime * 2f);
                flash.color = c;
            }
            shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime);
            box.anchoredPosition = shake > 0f ? new Vector2(Mathf.Sin(shake * 90f) * 7f * (shake / 0.25f), 0f) : Vector2.zero;
        }

        void Refresh()
        {
            bool cooling = cooldown > 0f;
            wipe.fillAmount = cooling ? cooldown / cooldownLength : 0f;
            icon.color = cooling ? new Color(0.55f, 0.55f, 0.55f) : Color.white;
            key.color = cooling ? InkStyle.Muted : InkStyle.Ink;
        }

        static Sprite white;

        static Sprite WhiteSprite()
        {
            if (white != null) return white;
            var tex = Texture2D.whiteTexture;
            white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return white;
        }
    }
}
