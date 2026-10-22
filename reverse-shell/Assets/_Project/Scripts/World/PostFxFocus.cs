using UnityEngine;
#if NEONHEX_POSTFX
using UnityEngine.Rendering.PostProcessing;
#endif

namespace Game.World
{
    /// <summary>
    /// Keeps depth of field focused on the player so the foreground and background blur like a
    /// tilt-shift diorama (the HD-2D look). Does nothing if the Post Processing package is missing.
    /// </summary>
    public class PostFxFocus : MonoBehaviour
    {
        public Transform target;

#if NEONHEX_POSTFX
        public PostProcessVolume volume;
        DepthOfField depthOfField;

        void LateUpdate()
        {
            if (volume == null || target == null) return;
            if (depthOfField == null && !volume.profile.TryGetSettings(out depthOfField)) return;
            depthOfField.focusDistance.value = Vector3.Distance(transform.position, target.position + Vector3.up * 0.7f);
        }
#endif
    }
}
