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
            return target.position + Vector3.up * lookHeight + Quaternion.Euler(pitch, 0f, 0f) * new Vector3(0f, 0f, -distance);
        }

        void LateUpdate()
        {
            if (target == null) return;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, Desired(), ref velocity, smoothTime);
        }
    }
}
