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
