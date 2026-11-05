using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Free 8-way movement (Octopath style, not grid-locked) with grid collision against the map.
    /// WASD / arrows to move, Shift to run, <see cref="interactKey"/> (F by default) to interact.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public float walkSpeed = 3.2f;
        public float runSpeed = 5.6f;
        [Tooltip("Collision half-size in world units (1 unit = 1 tile).")]
        public float radius = 0.28f;
        public KeyCode interactKey = KeyCode.F;

        /// <summary>Set while cutscenes / transitions run.</summary>
        public bool Frozen;

        public CharacterSprite Sprite { get; private set; }

        /// <summary>The last direction walked (x = east, y = north), for scripted moves that carry on from it.</summary>
        public Vector2 LastInput { get; private set; }

        Vector3 drift;
        float driftLeft, driftTotal;

        /// <summary>True while a <see cref="Drift"/> is still carrying the player.</summary>
        public bool Drifting { get { return driftLeft > 0f; } }

        /// <summary>Carries the player on in <paramref name="direction"/> for <paramref name="seconds"/>, slowing to a stop
        /// (with collisions), ignoring the keys. Works even while <see cref="Frozen"/>.</summary>
        public void Drift(Vector2 direction, float seconds)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.down;
            drift = new Vector3(direction.x, 0f, direction.y).normalized;
            driftTotal = driftLeft = Mathf.Max(0.05f, seconds);
        }

        OverworldMap map;
        OverworldBuilder world;

        public void Init(OverworldBuilder builder, OverworldMap overworldMap, CharacterSprite sprite)
        {
            world = builder;
            map = overworldMap;
            Sprite = sprite;
        }

        void Update()
        {
            if (map == null) return;

            if (Input.GetKeyDown(interactKey)) world.OnInteractPressed();

            // A scripted stumble (the first steps out of the capsule): keeps going the way you were walking, slowing to
            // a stop, whatever the keys say.
            if (driftLeft > 0f)
            {
                driftLeft -= Time.deltaTime;
                float k = Mathf.Clamp01(driftLeft / driftTotal);
                Move(drift * walkSpeed * (0.25f + 0.75f * k) * Time.deltaTime);
                Sprite.Face(new Vector2(drift.x, drift.z));
                Sprite.SetMoving(driftLeft > 0.08f);
                return;
            }

            if (Frozen)
            {
                Sprite.SetMoving(false);
                return;
            }

            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f) input.Normalize();
            bool moving = input.sqrMagnitude > 0.01f;
            bool running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (moving)
            {
                LastInput = input.normalized;
                float speed = running ? runSpeed : walkSpeed;
                Move(new Vector3(input.x, 0f, input.y) * speed * Time.deltaTime);
                Sprite.Face(input);
            }
            Sprite.SetMoving(moving, running);
        }

        /// <summary>Axis-separated so the player slides along walls instead of sticking.</summary>
        void Move(Vector3 delta)
        {
            var p = transform.position;
            var tryX = p + new Vector3(delta.x, 0f, 0f);
            if (!Blocked(tryX)) p = tryX;
            var tryZ = p + new Vector3(0f, 0f, delta.z);
            if (!Blocked(tryZ)) p = tryZ;
            transform.position = p;
        }

        bool Blocked(Vector3 p)
        {
            return map.IsSolidAt(p + new Vector3(-radius, 0f, -radius))
                   || map.IsSolidAt(p + new Vector3(radius, 0f, -radius))
                   || map.IsSolidAt(p + new Vector3(-radius, 0f, radius))
                   || map.IsSolidAt(p + new Vector3(radius, 0f, radius));
        }
    }
}
