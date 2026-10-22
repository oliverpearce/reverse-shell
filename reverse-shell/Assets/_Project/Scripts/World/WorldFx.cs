using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Atmosphere props for the overgrown ruins: trees, bushes, rubble, grass tufts, hanging vines,
    /// drifting leaves and dust motes, and pulsing floor markers. Lighting is just the dappled sun (see CanopySway).
    /// Props are textured from single cells of the ruins atlas (see WorldArt.CellMaterial).
    /// </summary>
    public static class WorldFx
    {
        public static void Tree(Transform parent, Texture atlas, Vector3 basePosition, int seed)
        {
            var root = new GameObject("Tree").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var rng = new System.Random(seed);

            float trunkHeight = 1.5f + (float)rng.NextDouble() * 0.8f;
            var trunk = Block(root, "Trunk", WorldArt.CellMaterial(atlas, WorldArt.BarkCell));
            trunk.localPosition = new Vector3(0f, trunkHeight / 2f, 0f);
            trunk.localScale = new Vector3(0.32f, trunkHeight, 0.32f);

            // A few overlapping leaf blocks make a chunky, diorama-style canopy.
            var leaves = WorldArt.CellMaterial(atlas, WorldArt.LeafCell);
            int blobs = 3 + rng.Next(0, 2);
            for (int i = 0; i < blobs; i++)
            {
                float size = 1.1f + (float)rng.NextDouble() * 0.7f;
                var canopy = Block(root, "Canopy", leaves);
                canopy.localPosition = new Vector3(Jitter(rng, 0.55f), trunkHeight + 0.3f + i * 0.35f, Jitter(rng, 0.45f));
                canopy.localScale = new Vector3(size, size * 0.8f, size);
                canopy.localRotation = Quaternion.Euler(0f, rng.Next(0, 4) * 22.5f, 0f);
            }
        }

        public static void Bush(Transform parent, Texture atlas, Vector3 basePosition, int seed)
        {
            var root = new GameObject("Bush").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var rng = new System.Random(seed);
            var leaves = WorldArt.CellMaterial(atlas, WorldArt.LeafCell);
            for (int i = 0; i < 2; i++)
            {
                float size = 0.65f + (float)rng.NextDouble() * 0.3f;
                var b = Block(root, "Leaves", leaves);
                b.localPosition = new Vector3(Jitter(rng, 0.2f), size * 0.4f, Jitter(rng, 0.2f));
                b.localScale = new Vector3(size, size * 0.8f, size);
            }
            Tuft(parent, atlas, basePosition + new Vector3(0.35f, 0f, -0.42f), false, 0.7f);
        }

        /// <summary>Broken concrete chunks.</summary>
        public static void Rubble(Transform parent, Texture atlas, Vector3 basePosition, int seed)
        {
            var root = new GameObject("Rubble").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var rng = new System.Random(seed);
            var concrete = WorldArt.CellMaterial(atlas, WorldArt.ConcreteCell);
            int chunks = 3 + rng.Next(0, 3);
            for (int i = 0; i < chunks; i++)
            {
                float s = 0.25f + (float)rng.NextDouble() * 0.35f;
                var c = Block(root, "Chunk", concrete);
                c.localPosition = new Vector3(Jitter(rng, 0.3f), s * 0.35f, Jitter(rng, 0.3f));
                c.localScale = new Vector3(s, s * 0.7f, s * 0.9f);
                c.localRotation = Quaternion.Euler(Jitter(rng, 18f), rng.Next(0, 90), Jitter(rng, 18f));
            }
            Tuft(parent, atlas, basePosition + new Vector3(-0.3f, 0f, -0.4f), false, 0.6f);
        }

        /// <summary>An upright grass (or flower) billboard facing the camera, like the character sprites.</summary>
        public static void Tuft(Transform parent, Texture atlas, Vector3 basePosition, bool flowers, float size = 0.8f)
        {
            var quad = WorldArt.Primitive(PrimitiveType.Quad, flowers ? "Flowers" : "Grass", parent,
                WorldArt.CellMaterial(atlas, flowers ? WorldArt.FlowerCell : WorldArt.GrassCell, true));
            quad.transform.position = basePosition + new Vector3(0f, size / 2f, 0f);
            quad.transform.localScale = new Vector3(size, size, 1f);
            quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            quad.AddComponent<Sway>().phase = basePosition.x * 1.7f + basePosition.z;
        }

        /// <summary>Vines spilling down a wall face from its top edge.</summary>
        public static void Vine(Transform parent, Texture atlas, DecalSpot spot)
        {
            var vine = WorldArt.Primitive(PrimitiveType.Quad, "Vines", parent, WorldArt.CellMaterial(atlas, WorldArt.VineCell, true));
            vine.transform.position = spot.Position;
            // A quad's visible side faces -forward, so look away from the wall normal.
            vine.transform.rotation = Quaternion.LookRotation(-spot.Normal);
            vine.transform.localScale = new Vector3(1f, spot.Length, 1f);
        }

        /// <summary>Leaves and pollen drifting down around a target (move it with the player).</summary>
        public static ParticleSystem Drift(Transform parent)
        {
            var go = new GameObject("DriftingLeaves");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.62f, 0.25f, 0.9f), new Color(0.95f, 0.92f, 0.7f, 0.8f));
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 28f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(24f, 0.5f, 20f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            velocity.y = new ParticleSystem.MinMaxCurve(-1.2f, -0.6f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.4f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = WorldArt.AlphaBlended();

            ps.Play();
            return ps;
        }

        /// <summary>Slow, sunlit dust motes hanging in the air.</summary>
        public static ParticleSystem Motes(Transform parent, Vector3 position, float radius, float rate, Color? tint = null)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            var c = tint ?? new Color(1f, 0.95f, 0.75f);
            main.startColor = new ParticleSystem.MinMaxGradient(c * 0.7f, c);
            main.gravityModifier = -0.01f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = WorldArt.Additive();

            ps.Play();
            return ps;
        }

        /// <summary>A slim field terminal with a softly glowing screen. Returns its root (attach the interactable there).</summary>
        public static GameObject AccessPoint(Transform parent, Vector3 basePosition)
        {
            var root = new GameObject("AccessPoint");
            root.transform.SetParent(parent, false);
            root.transform.position = basePosition;

            var body = Block(root.transform, "Body", WorldArt.DarkMetal());
            body.localPosition = new Vector3(0f, 0.6f, 0f);
            body.localScale = new Vector3(0.5f, 1.2f, 0.32f);

            var screen = WorldArt.Primitive(PrimitiveType.Quad, "Screen", root.transform, WorldArt.Emissive(new Color(1f, 0.95f, 0.75f), 2.2f));
            screen.transform.localPosition = new Vector3(0f, 0.85f, -0.17f);
            screen.transform.localScale = new Vector3(0.36f, 0.3f, 1f);

            Motes(root.transform, basePosition + Vector3.up * 1f, 0.4f, 4f);
            return root;
        }

        public static GameObject FloorMarker(Transform parent, Vector3 center, Color color)
        {
            var marker = WorldArt.Primitive(PrimitiveType.Quad, "StandMarker", parent, WorldArt.AdditiveTinted(WorldArt.FrameTexture(), color));
            marker.transform.position = center + new Vector3(0f, 0.02f, 0f);
            marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            marker.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
            marker.AddComponent<Pulse>().baseColor = color;
            return marker;
        }

        static Transform Block(Transform parent, string name, Material material)
        {
            var go = WorldArt.Primitive(PrimitiveType.Cube, name, parent, material);
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
            return go.transform;
        }

        static float Jitter(System.Random rng, float amount)
        {
            return ((float)rng.NextDouble() * 2f - 1f) * amount;
        }
    }

    /// <summary>Breathing glow on an additive material.</summary>
    public class Pulse : MonoBehaviour
    {
        public Color baseColor = Color.white;
        public float speed = 3f;
        Material material;

        void Start()
        {
            material = GetComponent<Renderer>().material;
        }

        void Update()
        {
            float k = 0.45f + 0.35f * Mathf.Sin(Time.time * speed);
            var c = baseColor * k;
            c.a = 1f;
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", c);
            material.color = c;
        }
    }

    /// <summary>
    /// Slowly drifts the sun so its dappled cookie moves like leaves stirring overhead. A directional light's
    /// cookie is projected from the light's position, so a small positional sway shifts every sun spot.
    /// </summary>
    public class CanopySway : MonoBehaviour
    {
        public float amount = 0.35f;
        Vector3 origin;

        void Start()
        {
            origin = transform.position;
        }

        void Update()
        {
            float t = Time.time;
            transform.position = origin + new Vector3(Mathf.Sin(t * 0.31f) + 0.4f * Mathf.Sin(t * 1.3f), 0f,
                Mathf.Cos(t * 0.23f) + 0.3f * Mathf.Sin(t * 0.9f + 1f)) * amount;
        }
    }

    /// <summary>Gentle wind sway for grass billboards (leans the top, keeps the base planted).</summary>
    public class Sway : MonoBehaviour
    {
        public float phase;
        public float degrees = 4f;
        public float speed = 1.3f;
        Vector3 basePosition;
        float height;

        void Start()
        {
            height = transform.localScale.y;
            basePosition = transform.position - new Vector3(0f, height / 2f, 0f);
        }

        void Update()
        {
            float angle = Mathf.Sin(Time.time * speed + phase) * degrees;
            var rot = Quaternion.Euler(0f, 0f, angle);
            transform.rotation = rot;
            transform.position = basePosition + rot * new Vector3(0f, height / 2f, 0f);
        }
    }
}
