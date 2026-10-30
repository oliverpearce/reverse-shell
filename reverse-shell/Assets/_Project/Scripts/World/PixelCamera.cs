using System;
using Combat.View;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Track C's pixel look, and its biggest saving: the overworld camera draws straight into a small render texture
    /// (<see cref="PixelHeight"/> pixels tall, the screen's shape), which a second, empty camera then puts on screen
    /// point-filtered. The 3D world, the ruins and the sprites share one chunky pixel grid, and the GPU fills about a
    /// ninth of the pixels (a big win in a browser). Graphics: Fast uses bigger pixels still.
    /// The menus are screen-space overlays, so they stay sharp. Code that turns world positions into screen positions
    /// goes through <see cref="ToScreen"/>, which scales from the small texture back to the screen.
    /// <see cref="Bypass"/> turns it off while it returns true (the Esc menu scene keeps its full-resolution look).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PixelCamera : MonoBehaviour
    {
        /// <summary>How many pixels tall the world is drawn (0 = off).</summary>
        public int PixelHeight = 360;

        /// <summary>While true, the camera draws to the screen at full resolution.</summary>
        public Func<bool> Bypass;

        Camera cam;
        Camera presenter;
        RenderTexture target;

        /// <summary>The small texture the world is drawn into (null while off).</summary>
        public RenderTexture Target { get { return cam != null ? cam.targetTexture : null; } }

        void Awake()
        {
            cam = GetComponent<Camera>();
            var go = new GameObject("PixelPresenter");
            go.transform.SetParent(transform, false);
            presenter = go.AddComponent<Camera>();
            presenter.cullingMask = 0;
            presenter.clearFlags = CameraClearFlags.SolidColor;
            presenter.backgroundColor = Color.black;
            presenter.depth = cam.depth + 1;
            presenter.useOcclusionCulling = false;
            presenter.allowHDR = false;
            presenter.allowMSAA = false;
            go.AddComponent<Present>().Owner = this;
            presenter.enabled = false;
        }

        void LateUpdate()
        {
            int height = GameSettings.FastGraphics ? PixelHeight * 3 / 4 : PixelHeight;
            bool on = height > 0 && height < Screen.height && (Bypass == null || !Bypass());
            if (!on)
            {
                if (cam.targetTexture != null) cam.targetTexture = null;
                presenter.enabled = false;
                return;
            }
            int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * height / (float)Screen.height));
            bool hdr = cam.allowHDR && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
            var format = hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            if (target == null || target.width != width || target.height != height || target.format != format)
            {
                Release();
                target = new RenderTexture(width, height, 24, format) { filterMode = FilterMode.Point, antiAliasing = 1, name = "PixelWorld" };
                target.Create();
            }
            if (cam.targetTexture != target) cam.targetTexture = target;
            presenter.enabled = true;
        }

        void OnDestroy()
        {
            if (cam != null) cam.targetTexture = null;
            Release();
        }

        void Release()
        {
            if (target == null) return;
            if (cam != null && cam.targetTexture == target) cam.targetTexture = null;
            target.Release();
            Destroy(target);
            target = null;
        }

        /// <summary>A position from <paramref name="camera"/>'s WorldToScreenPoint, in real screen pixels (the camera may be
        /// drawing into the small pixel texture).</summary>
        public static Vector3 ToScreen(Camera camera, Vector3 screenPoint)
        {
            var rt = camera != null ? camera.targetTexture : null;
            if (rt == null) return screenPoint;
            return new Vector3(screenPoint.x * Screen.width / rt.width, screenPoint.y * Screen.height / rt.height, screenPoint.z);
        }

        /// <summary>The empty camera's job: the small texture, point-scaled to fill the screen.</summary>
        class Present : MonoBehaviour
        {
            public PixelCamera Owner;

            void OnRenderImage(RenderTexture source, RenderTexture destination)
            {
                var rt = Owner != null ? Owner.target : null;
                Graphics.Blit(rt != null ? (Texture)rt : source, destination);
            }
        }
    }
}
