using UnityEngine;

namespace Game.World
{
    /// <summary>One Retro PC on the map: where its screen is (for the opening's camera and the text laid over it) and
    /// its glow.</summary>
    public class OfficePC : MonoBehaviour
    {
        /// <summary>The renderer the glass belongs to (the monitor), its box in that renderer's space, and its material.</summary>
        public Renderer Monitor;
        public Bounds GlassLocal;
        public Material Screen;
        /// <summary>The way the screen faces (flat).</summary>
        public Vector3 Facing = Vector3.back;
        /// <summary>The desk's chair (the opening hides it: you are sitting in it), or null.</summary>
        public Transform Chair;

        public Vector3 GlassCenter { get { return Monitor != null ? Monitor.transform.TransformPoint(GlassLocal.center) : transform.position; } }

        /// <summary>The glass box's eight corners in the world.</summary>
        public void GlassCorners(Vector3[] into)
        {
            var c = GlassLocal.center;
            var e = GlassLocal.extents;
            for (int i = 0; i < 8; i++)
            {
                var local = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                into[i] = Monitor != null ? Monitor.transform.TransformPoint(local) : transform.position;
            }
        }

        /// <summary>How tall the glass stands in the world.</summary>
        public float GlassHeight
        {
            get
            {
                var corners = new Vector3[8];
                GlassCorners(corners);
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var p in corners)
                {
                    lo = Mathf.Min(lo, p.y);
                    hi = Mathf.Max(hi, p.y);
                }
                return Mathf.Max(0.01f, hi - lo);
            }
        }

        Light screenLight;

        /// <summary>A light in front of the glass in the screen's colour, so it lights the desk and its clutter (the
        /// opening adds one to its PC only).</summary>
        public void AddScreenLight()
        {
            if (screenLight != null || Monitor == null) return;
            screenLight = new GameObject("ScreenLight").AddComponent<Light>();
            screenLight.transform.SetParent(transform, true);
            screenLight.transform.position = GlassCenter + Facing * 0.25f;
            screenLight.type = LightType.Point;
            screenLight.range = 1.6f;
            screenLight.shadows = LightShadows.None;
            SetGlow(Screen != null && Screen.HasProperty("_EmissionColor") ? Screen.GetColor("_EmissionColor") : Color.black);
        }

        public void SetGlow(Color color)
        {
            if (Screen != null && Screen.HasProperty("_EmissionColor")) Screen.SetColor("_EmissionColor", color);
            if (screenLight == null) return;
            float strength = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            screenLight.color = strength > 0.001f ? color / strength : Color.black;
            screenLight.intensity = Mathf.Min(2.5f, strength * 4f);
        }
    }
}
