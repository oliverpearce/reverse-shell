using UnityEngine;

namespace Game.World
{
    /// <summary>Atmosphere props: neon lamps and signs, rain, Glimmer sparkles, pulsing floor markers.</summary>
    public static class WorldFx
    {
        public static void Lamp(Transform parent, Vector3 basePosition, Color color)
        {
            var root = new GameObject("Lamp").transform;
            root.SetParent(parent, false);
            root.position = basePosition;

            var post = WorldArt.Primitive(PrimitiveType.Cube, "Post", root, WorldArt.DarkMetal());
            post.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            post.transform.localScale = new Vector3(0.1f, 2.4f, 0.1f);

            var head = WorldArt.Primitive(PrimitiveType.Cube, "Tube", root, WorldArt.Emissive(color, 4f));
            head.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            head.transform.localScale = new Vector3(0.45f, 0.08f, 0.14f);

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 5.5f;
            light.intensity = 2.6f;
            light.shadows = LightShadows.None;
        }

        public static void Sign(Transform parent, SignSpot spot)
        {
            var sign = WorldArt.Primitive(PrimitiveType.Quad, "NeonSign", parent, WorldArt.Emissive(spot.Color, 3f));
            sign.transform.position = spot.Position;
            // A quad's visible side faces -forward, so look away from the wall normal.
            sign.transform.rotation = Quaternion.LookRotation(-spot.Normal);
            sign.transform.localScale = new Vector3(0.72f, 0.26f, 1f);
        }

        /// <summary>Rain volume that follows a target; particles fall in world space.</summary>
        public static ParticleSystem Rain(Transform parent)
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.75f;
            main.startSpeed = 0f;
            main.startSize = 0.035f;
            main.startColor = new Color(0.55f, 0.65f, 1f, 0.35f);
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 900f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(26f, 0.5f, 22f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-1.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(-17f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 1f;
            renderer.sharedMaterial = WorldArt.Additive();

            ps.Play();
            return ps;
        }

        /// <summary>Drifting pastel twinkles: the visible signature of Glimmer magic.</summary>
        public static ParticleSystem Glimmer(Transform parent, Vector3 position, float radius, float rate)
        {
            var go = new GameObject("Glimmer");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.95f), new Color(0.7f, 0.95f, 1f));
            main.gravityModifier = -0.02f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.5f, 0.3f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = WorldArt.Additive();

            ps.Play();
            return ps;
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
}
