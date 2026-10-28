using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Chapter One's story props, built from primitives like the other world props (WorldFx): the procedure pod,
    /// memory shards, record terminals, the scanner, the cracked wall of the hidden route, the Soul Card pedestal, the
    /// scanner's pulse and the trace markers it reveals. Placeholder art in the project's style.
    /// </summary>
    public static class StoryProps
    {
        static readonly Color Cyan = WorldArt.WarpCyan;
        static readonly Color Memory = new Color(1f, 0.86f, 0.6f);

        static GameObject Root(Transform parent, string name, Vector3 at)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = at;
            return root;
        }

        static Transform Block(Transform parent, string name, Material m, Vector3 pos, Vector3 scale, float tilt = 0f)
        {
            var go = WorldArt.Primitive(PrimitiveType.Cube, name, parent, m);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            return go.transform;
        }

        /// <summary>The cracked procedure capsule the protagonist wakes beside; a dim red light still blinks on it.</summary>
        public static GameObject Pod(Transform parent, Vector3 at)
        {
            var root = Root(parent, "ProcedurePod", at);
            var shell = WorldArt.DarkMetal();
            Block(root.transform, "Base", shell, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.4f, 1.6f));
            Block(root.transform, "Lid", shell, new Vector3(0.35f, 0.62f, 0f), new Vector3(0.12f, 0.5f, 1.5f), -38f);
            var glass = WorldArt.Primitive(PrimitiveType.Quad, "Glass", root.transform, WorldArt.Emissive(new Color(0.55f, 0.8f, 0.75f), 0.5f));
            glass.transform.localPosition = new Vector3(0f, 0.41f, 0f);
            glass.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glass.transform.localScale = new Vector3(0.7f, 1.4f, 1f);
            var light = WorldArt.Primitive(PrimitiveType.Quad, "Status", root.transform, WorldArt.AdditiveTinted(WorldArt.FrameTexture(), new Color(1f, 0.3f, 0.25f)));
            light.transform.localPosition = new Vector3(0f, 0.25f, -0.81f);
            light.transform.localScale = new Vector3(0.18f, 0.12f, 1f);
            light.AddComponent<Pulse>().baseColor = new Color(1f, 0.3f, 0.25f);
            WorldFx.Motes(root.transform, at + Vector3.up * 0.6f, 0.6f, 2f);
            return root;
        }

        /// <summary>A memory fragment: a warm glowing shard hanging just off the ground.</summary>
        public static GameObject Shard(Transform parent, Vector3 at)
        {
            var root = Root(parent, "MemoryShard", at);
            var shard = Block(root.transform, "Shard", WorldArt.Emissive(Memory, 2.2f), new Vector3(0f, 0.75f, 0f), new Vector3(0.22f, 0.42f, 0.22f), 45f);
            shard.gameObject.AddComponent<Bob>();
            WorldFx.Motes(root.transform, at + Vector3.up * 0.7f, 0.35f, 6f, Memory);
            return root;
        }

        /// <summary>A record: an old public terminal / notice board with a faint screen.</summary>
        public static GameObject Terminal(Transform parent, Vector3 at)
        {
            var root = Root(parent, "Record", at);
            Block(root.transform, "Post", WorldArt.DarkMetal(), new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 1.1f, 0.18f));
            var screen = WorldArt.Primitive(PrimitiveType.Quad, "Screen", root.transform, WorldArt.Emissive(new Color(0.85f, 0.8f, 0.6f), 0.8f));
            screen.transform.localPosition = new Vector3(0f, 0.75f, -0.1f);
            screen.transform.localScale = new Vector3(0.46f, 0.5f, 1f);
            return root;
        }

        /// <summary>The optical scanner lying among glass: a small visor with a cyan glint.</summary>
        public static GameObject ScannerItem(Transform parent, Vector3 at)
        {
            var root = Root(parent, "Scanner", at);
            Block(root.transform, "Visor", WorldArt.DarkMetal(), new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.12f, 0.22f));
            var lens = Block(root.transform, "Lens", WorldArt.Emissive(Cyan, 2.5f), new Vector3(0f, 0.14f, -0.12f), new Vector3(0.36f, 0.06f, 0.02f));
            lens.gameObject.AddComponent<Bob>().height = 0.02f;
            WorldFx.Motes(root.transform, at + Vector3.up * 0.4f, 0.3f, 6f, Cyan);
            return root;
        }

        /// <summary>The card binder: a small worn leather binder lying shut on the ground, a gold strap across it and a faint
        /// glint of gold drifting up so it reads as something to pick up.</summary>
        public static GameObject Binder(Transform parent, Vector3 at)
        {
            var gold = new Color(1f, 0.82f, 0.45f);
            var root = Root(parent, "CardBinder", at);
            Block(root.transform, "Cover", WorldArt.Matte(new Color(0.36f, 0.2f, 0.12f)), new Vector3(0f, 0.05f, 0f), new Vector3(0.34f, 0.08f, 0.44f), 12f);
            Block(root.transform, "Pages", WorldArt.Matte(new Color(0.88f, 0.82f, 0.68f)), new Vector3(0.02f, 0.05f, 0f), new Vector3(0.3f, 0.05f, 0.4f), 12f);
            Block(root.transform, "Strap", WorldArt.Emissive(gold, 0.8f), new Vector3(0f, 0.1f, 0f), new Vector3(0.36f, 0.015f, 0.06f), 12f);
            WorldFx.Motes(root.transform, at + Vector3.up * 0.3f, 0.25f, 5f, gold);
            return root;
        }

        /// <summary>The language chip: a small dark chip with a glowing green core, lying on the ground.</summary>
        public static GameObject LanguageChip(Transform parent, Vector3 at)
        {
            return Chip(parent, at, "LanguageChip", new Color(0.45f, 1f, 0.6f));
        }

        /// <summary>A chip like the language chip with a core of its own colour (the numbers chip: gold, the colours chip: violet).</summary>
        public static GameObject Chip(Transform parent, Vector3 at, string name, Color core)
        {
            var root = Root(parent, name, at);
            Block(root.transform, "Chip", WorldArt.DarkMetal(), new Vector3(0f, 0.05f, 0f), new Vector3(0.3f, 0.05f, 0.3f));
            var glow = Block(root.transform, "Core", WorldArt.Emissive(core, 2.5f), new Vector3(0f, 0.09f, 0f), new Vector3(0.14f, 0.03f, 0.14f));
            glow.gameObject.AddComponent<Bob>().height = 0.02f;
            WorldFx.Motes(root.transform, at + Vector3.up * 0.3f, 0.25f, 5f, core);
            return root;
        }

        /// <summary>The hidden route while closed: a slab of cracked wall, faintly warm along its seams.</summary>
        public static GameObject CrackedWall(Transform parent, Vector3 at)
        {
            var root = Root(parent, "CrackedWall", at);
            Block(root.transform, "Wall", WorldArt.DarkMetal(), new Vector3(0f, 0.9f, 0f), new Vector3(1f, 1.8f, 1f));
            var seam = WorldArt.Primitive(PrimitiveType.Quad, "Seam", root.transform, WorldArt.AdditiveTinted(WorldArt.FrameTexture(), new Color(0.4f, 0.6f, 0.7f)));
            seam.transform.localPosition = new Vector3(0f, 0.8f, -0.51f);
            seam.transform.localScale = new Vector3(0.5f, 1.2f, 1f);
            return root;
        }

        /// <summary>The pedestal and the blank Soul Card on it (the card is a separate child so it can vanish).</summary>
        public static GameObject Pedestal(Transform parent, Vector3 at, out GameObject card)
        {
            var root = Root(parent, "Pedestal", at);
            Block(root.transform, "Stone", WorldArt.DarkMetal(), new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 0.7f));
            var c = Block(root.transform, "SoulCard", WorldArt.Emissive(new Color(0.9f, 0.95f, 1f), 3f), new Vector3(0f, 1.25f, 0f), new Vector3(0.28f, 0.4f, 0.03f));
            c.gameObject.AddComponent<Bob>();
            c.gameObject.AddComponent<Spin>();
            card = c.gameObject;
            WorldFx.Motes(root.transform, at + Vector3.up * 1.2f, 0.4f, 8f, new Color(0.85f, 0.95f, 1f));
            return root;
        }

        /// <summary>The scanner's pulse: a cyan ring that sweeps outward over the ground and fades.</summary>
        public static void ScanPulse(Transform parent, Vector3 at, float radius)
        {
            var ring = WorldArt.Primitive(PrimitiveType.Quad, "ScanPulse", parent, WorldArt.AdditiveTinted(WorldArt.FrameTexture(), Cyan));
            ring.transform.position = at + Vector3.up * 0.05f;
            ring.transform.rotation = Quaternion.Euler(90f, 45f, 0f);
            ring.AddComponent<Sweep>().radius = radius;
        }

        /// <summary>A revealed trace: a faint cyan mark on the ground that fades after a while.</summary>
        public static void TraceMark(Transform parent, Vector3 at, float lifetime)
        {
            var mark = WorldFx.FloorMarker(parent, at, Cyan);
            mark.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            Object.Destroy(mark, lifetime);
        }
    }

    /// <summary>A gentle up-and-down float.</summary>
    public class Bob : MonoBehaviour
    {
        public float height = 0.08f, speed = 1.6f;
        Vector3 origin;
        void Start() { origin = transform.localPosition; }
        void Update() { transform.localPosition = origin + Vector3.up * Mathf.Sin(Time.time * speed) * height; }
    }

    /// <summary>A slow turn around the vertical axis.</summary>
    public class Spin : MonoBehaviour
    {
        public float degreesPerSecond = 40f;
        void Update() { transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World); }
    }

    /// <summary>Grows a quad out to <see cref="radius"/> while fading, then removes it (the scan pulse).</summary>
    public class Sweep : MonoBehaviour
    {
        public float radius = 8f, seconds = 0.9f;
        float t;
        Material material;
        void Start() { material = GetComponent<Renderer>().material; }
        void Update()
        {
            t += Time.deltaTime / seconds;
            float s = Mathf.Lerp(0.5f, radius * 2f, t);
            transform.localScale = new Vector3(s, s, 1f);
            var c = WorldArt.WarpCyan * (1f - t);
            c.a = 1f;
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", c);
            material.color = c;
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
