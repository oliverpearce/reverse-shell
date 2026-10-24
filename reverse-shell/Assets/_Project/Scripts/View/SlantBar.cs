using UnityEngine;
using UnityEngine.UI;

namespace Combat.View
{
    /// <summary>
    /// A Reverse: 1999-style bar: a parallelogram leaning right instead of a rectangle. <see cref="Fill"/> (0..1) draws
    /// that share of it from the left, and the filled part's right edge keeps the same slant. Used for HP bars.
    /// </summary>
    public class SlantBar : MaskableGraphic
    {
        float fill = 1f;
        float skew = 8f;

        /// <summary>How much of the bar is drawn (0..1).</summary>
        public float Fill
        {
            get { return fill; }
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(v, fill)) return;
                fill = v;
                SetVerticesDirty();
            }
        }

        /// <summary>A bar filling <paramref name="parent"/>'s rect (offset outward by <paramref name="grow"/>), leaning
        /// <paramref name="slant"/> canvas units over its height.</summary>
        public static SlantBar Create(Transform parent, string name, Color color, float slant, float grow = 0f)
        {
            var rt = UIFactory.CreateRect(name, parent).Fill(-grow);
            var bar = rt.gameObject.AddComponent<SlantBar>();
            bar.color = color;
            bar.skew = slant;
            bar.raycastTarget = false;
            return bar;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (fill <= 0f) return;
            var r = GetPixelAdjustedRect();
            float run = Mathf.Max(0f, r.width - skew);
            float x0 = r.xMin, x1 = r.xMin + run * fill;
            Color32 c = color;
            vh.AddVert(new Vector3(x0, r.yMin), c, Vector2.zero);
            vh.AddVert(new Vector3(x0 + skew, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector3(x1 + skew, r.yMax), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, r.yMin), c, Vector2.zero);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
