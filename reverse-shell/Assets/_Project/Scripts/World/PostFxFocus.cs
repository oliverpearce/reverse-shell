using UnityEngine;
#if NEONHEX_POSTFX
using UnityEngine.Rendering.PostProcessing;
#endif

namespace Game.World
{
    /// <summary>
    /// Depth-of-field control for the overworld camera. The tilt-shift blur was removed at the user's request, so
    /// this now just makes sure depth of field is off, even in post-processing profiles made before that change.
    /// <see cref="target"/> is kept for scenes that already reference it. Does nothing without the Post Processing package.
    /// </summary>
    public class PostFxFocus : MonoBehaviour
    {
        public Transform target;

#if NEONHEX_POSTFX
        public PostProcessVolume volume;
        bool done;

        void LateUpdate()
        {
            if (done || volume == null) return;
            DepthOfField depthOfField;
            if (volume.profile.TryGetSettings(out depthOfField)) depthOfField.active = false; // runtime copy of the profile
            done = true;
        }
#endif
    }
}
