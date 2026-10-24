using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// A unit's element, made to pop: the element orb (<see cref="CardIcons.Element"/>) with a thick dark outline, on a
    /// soft radial glow in the element's colour that breathes slowly (unscaled time, so it also runs in paused menus).
    /// No box. Used by the battle inspect sheet and the party menu.
    /// </summary>
    public class ElementBadge : MonoBehaviour
    {
        static Texture2D radial;
        RawImage glow;
        Color glowColor;

        /// <summary>Creates the badge centred at <paramref name="position"/> (anchored / pivoted as given), the orb
        /// <paramref name="size"/> px square; the glow spreads about twice as wide.</summary>
        public static RectTransform Create(RectTransform parent, Afflatus afflatus, float size, Vector2 anchor, Vector2 position)
        {
            var root = UIFactory.CreateRect("Element", parent).Place(anchor, new Vector2(0.5f, 0.5f), position, new Vector2(size, size));
            var badge = root.gameObject.AddComponent<ElementBadge>();

            badge.glowColor = AfflatusInfo.Color(afflatus);
            badge.glow = new GameObject("Glow", typeof(RectTransform)).AddComponent<RawImage>();
            badge.glow.rectTransform.SetParent(root, false);
            badge.glow.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size) * 2.2f);
            badge.glow.texture = Radial();
            badge.glow.raycastTarget = false;

            var orb = CardIcons.Element(afflatus);
            if (orb != null)
            {
                var icon = UIFactory.CreateImage(root, "Orb", Color.white);
                icon.sprite = orb;
                icon.preserveAspect = true;
                icon.rectTransform.Fill();
                var edge = icon.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(0.05f, 0.04f, 0.04f, 0.95f);
                edge.effectDistance = new Vector2(3f, -3f);
                var edge2 = icon.gameObject.AddComponent<Outline>();   // a second pass makes the border thick all round
                edge2.effectColor = new Color(0.05f, 0.04f, 0.04f, 0.95f);
                edge2.effectDistance = new Vector2(-3f, 3f);
            }
            badge.Update();
            return root;
        }

        void Update()
        {
            float pulse = 0.55f + 0.15f * Mathf.Sin(Time.unscaledTime * 2.2f);
            var c = glowColor;
            c.a = pulse;
            glow.color = c;
            glow.rectTransform.localScale = Vector3.one * (0.96f + 0.06f * pulse);
        }

        static Texture2D Radial()
        {
            if (radial != null) return radial;
            const int size = 128;
            radial = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    radial.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * a));
                }
            radial.Apply();
            return radial;
        }
    }
}
