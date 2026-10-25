using UnityEngine;
#if NEONHEX_POSTFX
using UnityEngine.Rendering.PostProcessing;
#endif

namespace Game.World
{
    /// <summary>
    /// Keeps the overworld sharp, even with post-processing profiles made before these changes: depth of field and
    /// film grain are switched off, and bloom only catches real light sources (not sunlit sprites). With Graphics: Fast
    /// (Combat.View.GameSettings) the post-processing is off altogether (the camera's layer and the volume), which is
    /// the biggest saving in a browser; switching the setting turns it back on or off right away.
    /// <see cref="target"/> is kept for scenes that already reference it. Does nothing without the Post Processing package.
    /// </summary>
    public class PostFxFocus : MonoBehaviour
    {
        public Transform target;

#if NEONHEX_POSTFX
        public PostProcessVolume volume;
        bool done;

        void OnEnable()
        {
            Combat.View.GameSettings.GraphicsChanged += ApplyGraphics;
            ApplyGraphics();
        }

        void OnDisable()
        {
            Combat.View.GameSettings.GraphicsChanged -= ApplyGraphics;
        }

        void ApplyGraphics()
        {
            bool on = !Combat.View.GameSettings.FastGraphics;
            if (volume != null) volume.enabled = on;
            foreach (var layer in FindObjectsOfType<PostProcessLayer>()) layer.enabled = on;
        }

        void LateUpdate()
        {
            if (done || volume == null) return;
            // volume.profile is a runtime copy: the asset on disk is untouched.
            DepthOfField depthOfField;
            if (volume.profile.TryGetSettings(out depthOfField)) depthOfField.active = false;
            Grain grain;
            if (volume.profile.TryGetSettings(out grain)) grain.active = false;
            Bloom bloom;
            if (volume.profile.TryGetSettings(out bloom))
            {
                bloom.threshold.Override(Mathf.Max(bloom.threshold.value, 1.6f));
                bloom.diffusion.Override(Mathf.Min(bloom.diffusion.value, 5f));
            }
            done = true;
        }
#endif
    }
}
