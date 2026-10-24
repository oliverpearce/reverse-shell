using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Octopath-style camera: narrow field of view, pitched down at the player from the south,
    /// smoothly following. Pair with depth of field (PostFxFocus) for the tilt-shift diorama look.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OverworldCamera : MonoBehaviour
    {
        public Transform target;
        [Range(10f, 80f)] public float pitch = 38f;
        public float distance = 15f;
        [Tooltip("Aim point above the target's feet.")]
        public float lookHeight = 0.8f;
        [Range(10f, 60f)] public float fieldOfView = 28f;
        public float smoothTime = 0.15f;
        [Tooltip("Distance multiplier: 1 = normal; the Esc menu eases it down to zoom in on the player.")]
        public float zoom = 1f;
        [Tooltip("World offset of the aim point from the target (the Esc menu aims south of the player, so they stand "
                 + "above the cards with their mat in front of them).")]
        public Vector3 aimOffset = Vector3.zero;

        /// <summary>Follow exactly, no smoothing (while the Esc menu drives <see cref="zoom"/> with its own easing).</summary>
        public bool Direct { get; set; }

        Vector3 velocity;

        void Start()
        {
            GetComponent<Camera>().fieldOfView = fieldOfView;
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.position = Desired();
            velocity = Vector3.zero;
        }

        Vector3 Desired()
        {
            return target.position + aimOffset + Vector3.up * lookHeight + Quaternion.Euler(pitch, 0f, 0f) * new Vector3(0f, 0f, -distance * zoom);
        }

        void LateUpdate()
        {
            if (target == null) return;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            // Unscaled, so the zoom still eases while a menu has paused time.
            if (Direct)
            {
                transform.position = Desired();
                velocity = Vector3.zero;
                return;
            }
            transform.position = Vector3.SmoothDamp(transform.position, Desired(), ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }
    }
}
