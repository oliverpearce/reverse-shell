using System.Collections.Generic;
using Combat.View;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Track C's signature effects. Two sorts:
    ///   Air        particles that follow the player, picked by the theme (<see cref="WorldTheme.effects"/>): falling
    ///              leaves, fireflies, snow, blizzard, embers, ash, poison spores, smog, petals, prism glints, cave dust.
    ///   Tiles      lava ~ and ooze z (one churning pool mesh each, glow spilling onto the ground, flickering
    ///              lights, Minecraft-style pops / bubbles), ice
    ///              crystals i (glowing clusters that sparkle), light shafts l (soft beams of light, walkable) and prism
    ///              shards y (floating crystals throwing rainbow glints), lab capsules u, dormant robots h, hanging
    ///              chains g and brazier fires t.
    ///              Any area can use them.
    /// Glow comes from emission (bloom picks it up with Graphics: Pretty); only pools add a few lights. Graphics: Fast
    /// halves every particle rate. One <see cref="Clock"/> on the area animates the shared materials.
    /// </summary>
    public static class WorldEffects
    {
        /// <summary>The kinds of air effect a theme can list.</summary>
        public enum Air { Leaves, Fireflies, Snow, Embers, Ash, Spores, Smog, Petals, Glints, Dust, Blizzard }

        static float Rate(float rate) { return GameSettings.FastGraphics ? rate * 0.5f : rate; }

        // ------------------------------------------------------------------ Air

        /// <summary>The theme's air effects under one root that the builder keeps on the player.</summary>
        public static Transform AirAround(Transform parent, WorldTheme theme)
        {
            var root = new GameObject("WorldAir").transform;
            root.SetParent(parent, false);
            if (theme == null) return root;
            foreach (var fx in theme.effects)
                if (fx != null && fx.density > 0f) MakeAir(root, fx.kind, fx.density, fx.color);
            return root;
        }

        static void MakeAir(Transform root, Air kind, float density, Color color)
        {
            switch (kind)
            {
                case Air.Leaves:
                    Emitter(root, "Leaves", new Vector3(0f, 7f, 3f), new Vector3(24f, 0.5f, 20f), 28f * density, 5f, 8f, 0.06f, 0.14f,
                        color, new Color(0.95f, 0.92f, 0.7f, 0.8f), new Vector3(0.3f, -1.2f, -0.2f), new Vector3(0.8f, -0.6f, 0.2f), 0.6f, false, false);
                    break;
                case Air.Fireflies:
                    Emitter(root, "Fireflies", new Vector3(0f, 0.9f, 2f), new Vector3(18f, 1.2f, 14f), 6f * density, 3f, 6f, 0.08f, 0.14f,
                        color, color * 0.7f, new Vector3(-0.15f, -0.05f, -0.15f), new Vector3(0.15f, 0.12f, 0.15f), 0.5f, true, true);
                    break;
                case Air.Snow:
                    Emitter(root, "Snow", new Vector3(0f, 8f, 3f), new Vector3(26f, 0.5f, 22f), 90f * density, 5f, 7f, 0.05f, 0.12f,
                        color, new Color(0.85f, 0.92f, 1f, 0.8f), new Vector3(0.2f, -1.6f, -0.1f), new Vector3(0.6f, -1f, 0.1f), 0.35f, false, false);
                    break;
                case Air.Embers:
                    Emitter(root, "Embers", new Vector3(0f, 0f, 3f), new Vector3(22f, 0.3f, 18f), 22f * density, 2.5f, 4.5f, 0.05f, 0.11f,
                        color, new Color(1f, 0.85f, 0.35f, 1f), new Vector3(-0.2f, 0.6f, -0.2f), new Vector3(0.3f, 1.5f, 0.2f), 0.9f, true, true);
                    break;
                case Air.Ash:
                    Emitter(root, "Ash", new Vector3(0f, 7f, 3f), new Vector3(24f, 0.5f, 20f), 30f * density, 6f, 9f, 0.05f, 0.1f,
                        color, color * 0.6f, new Vector3(0.1f, -0.7f, -0.1f), new Vector3(0.5f, -0.35f, 0.1f), 0.5f, false, false);
                    break;
                case Air.Spores:
                    Emitter(root, "Spores", new Vector3(0f, 1.2f, 3f), new Vector3(22f, 2.4f, 18f), 26f * density, 4f, 7f, 0.07f, 0.16f,
                        color, new Color(0.85f, 1f, 0.35f, 0.9f), new Vector3(-0.15f, 0.05f, -0.15f), new Vector3(0.2f, 0.3f, 0.15f), 0.7f, true, true);
                    break;
                case Air.Smog:
                    var smog = Emitter(root, "Smog", new Vector3(0f, 0.6f, 3f), new Vector3(24f, 0.8f, 20f), 5f * density, 7f, 11f, 1.8f, 3.2f,
                        color, color * 0.8f, new Vector3(0.1f, 0.02f, -0.1f), new Vector3(0.35f, 0.08f, 0.1f), 0.2f, false, true);
                    smog.GetComponent<ParticleSystemRenderer>().sharedMaterial = SoftPuff();
                    break;
                case Air.Petals:
                    Emitter(root, "Petals", new Vector3(0f, 7f, 3f), new Vector3(24f, 0.5f, 20f), 18f * density, 6f, 9f, 0.08f, 0.15f,
                        color, new Color(1f, 0.92f, 0.95f, 0.9f), new Vector3(0.4f, -0.9f, -0.2f), new Vector3(0.9f, -0.5f, 0.2f), 0.8f, false, false);
                    break;
                case Air.Glints:
                    Emitter(root, "Glints", new Vector3(0f, 1.5f, 3f), new Vector3(20f, 3f, 16f), 10f * density, 0.6f, 1.2f, 0.08f, 0.16f,
                        color, new Color(0.7f, 0.9f, 1f, 1f), Vector3.zero, new Vector3(0f, 0.1f, 0f), 0f, true, true, true);
                    break;
                case Air.Blizzard:
                    // Snow driven sideways by the wind (the mountain pass): fast, low and long streaks.
                    Emitter(root, "Blizzard", new Vector3(-12f, 2.5f, 3f), new Vector3(1f, 5f, 22f), 120f * density, 3f, 4.5f, 0.04f, 0.09f,
                        color, new Color(0.85f, 0.92f, 1f, 0.6f), new Vector3(5f, -0.8f, -0.4f), new Vector3(8f, -0.2f, 0.4f), 0.8f, false, false);
                    break;
                case Air.Dust:
                    Emitter(root, "Dust", new Vector3(0f, 1.5f, 2f), new Vector3(18f, 3f, 14f), 10f * density, 3f, 6f, 0.04f, 0.09f,
                        color, color * 0.6f, new Vector3(-0.05f, -0.02f, -0.05f), new Vector3(0.05f, 0.05f, 0.05f), 0.3f, true, true);
                    break;
            }
        }

        /// <summary>A looping box emitter (world-space particles) above, around or below the root.</summary>
        static ParticleSystem Emitter(Transform root, string name, Vector3 at, Vector3 box, float rate, float lifeMin, float lifeMax,
            float sizeMin, float sizeMax, Color a, Color b, Vector3 velMin, Vector3 velMax, float noiseStrength, bool additive, bool fade,
            bool rainbow = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.maxParticles = Mathf.CeilToInt(Rate(rate) * lifeMax * 1.3f) + 4;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = Rate(rate);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(velMin.x, velMax.x);
            velocity.y = new ParticleSystem.MinMaxCurve(velMin.y, velMax.y);
            velocity.z = new ParticleSystem.MinMaxCurve(velMin.z, velMax.z);
            if (noiseStrength > 0f)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = noiseStrength;
                noise.frequency = 0.4f;
            }
            if (fade)
            {
                // Grow in and fade out (fireflies and glints twinkle, smog breathes).
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(0.7f, 0.85f), new Keyframe(1f, 0f)));
            }
            if (rainbow)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.5f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0.33f),
                    new GradientColorKey(new Color(0.5f, 1f, 0.7f), 0.66f), new GradientColorKey(new Color(0.55f, 0.7f, 1f), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
            }
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = additive ? WorldArt.Additive() : WorldArt.AlphaBlended();
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ Tiles

        /// <summary>One glowing pool over the cells (lava, or ooze with <paramref name="ooze"/>): a flat seamless mesh with
        /// world-space UVs and the churning Lava shader; a soft glow spilling onto the ground a couple of tiles around it;
        /// a few warm lights that flicker on the walls and props nearby; and pops that leap out and fall back like
        /// Minecraft lava (bubbles for ooze), with a wisp of smoke.</summary>
        public static GameObject Pool(Transform parent, List<Vector3> cellCenters, bool ooze)
        {
            if (cellCenters.Count == 0) return null;
            var go = new GameObject(ooze ? "OozePool" : "LavaPool", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            const float y = 0.04f;
            foreach (var c in cellCenters)
            {
                int i = verts.Count;
                verts.Add(new Vector3(c.x - 0.5f, y, c.z - 0.5f)); verts.Add(new Vector3(c.x - 0.5f, y, c.z + 0.5f));
                verts.Add(new Vector3(c.x + 0.5f, y, c.z + 0.5f)); verts.Add(new Vector3(c.x + 0.5f, y, c.z - 0.5f));
                foreach (var v in verts.GetRange(i, 4)) uvs.Add(new Vector2(v.x, v.z) * 0.18f);
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            var mesh = new Mesh { name = go.name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = PoolMaterial(ooze);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            var glow = ooze ? new Color(0.45f, 1f, 0.2f) : new Color(1f, 0.42f, 0.1f);
            // The glow on the ground: a soft round halo under every tile, so it spills onto the tiles around the pool
            // (the pool itself covers its own tiles) and builds up near big pools.
            var halo = HaloMaterial(glow);
            foreach (var c in cellCenters)
            {
                var quad = WorldArt.Primitive(PrimitiveType.Quad, "Glow", go.transform, halo);
                quad.transform.position = new Vector3(c.x, 0.025f, c.z);
                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localScale = new Vector3(3.6f, 3.6f, 1f);
                var qr = quad.GetComponent<Renderer>();
                qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                qr.receiveShadows = false;
            }
            // Light on the walls and props nearby: one per 4x4 block of tiles that has pool in it (at most 8).
            var blocks = new Dictionary<Vector2Int, List<Vector3>>();
            foreach (var c in cellCenters)
            {
                var key = new Vector2Int(Mathf.FloorToInt(c.x / 4f), Mathf.FloorToInt(c.z / 4f));
                List<Vector3> list;
                if (!blocks.TryGetValue(key, out list)) blocks[key] = list = new List<Vector3>();
                list.Add(c);
            }
            int lights = 0;
            foreach (var block in blocks.Values)
            {
                if (lights++ >= 8) break;
                var mid = Vector3.zero;
                foreach (var c in block) mid += c;
                mid /= block.Count;
                var light = new GameObject("PoolLight").AddComponent<Light>();
                light.transform.SetParent(go.transform, false);
                light.transform.position = mid + Vector3.up * 1.1f;
                light.type = LightType.Point;
                light.color = glow;
                light.range = 3.5f + Mathf.Min(block.Count, 8) * 0.35f;
                light.intensity = ooze ? 1.1f : 1.8f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.Auto;
                Clock.Flicker(light);
            }
            // Pops (or bubbles), spread over the pool: one emitter per three tiles.
            for (int k = 0; k < cellCenters.Count; k += 3)
                Pops(go.transform, cellCenters[k], ooze);
            return go;
        }

        /// <summary>Square pixel blobs that leap up out of the pool in an arc and fall back, cooling as they go, with a
        /// little smoke rising from where they burst (Minecraft's lava pops); ooze bubbles hop lower and pop green.</summary>
        static void Pops(Transform parent, Vector3 at, bool ooze)
        {
            var go = new GameObject(ooze ? "Bubbles" : "LavaPops");
            go.transform.SetParent(parent, false);
            go.transform.position = at + Vector3.up * 0.06f;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);   // the cone points up
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startSpeed = ooze ? new ParticleSystem.MinMaxCurve(1.2f, 2f) : new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
            main.gravityModifier = ooze ? 0.6f : 0.9f;
            main.startColor = ooze
                ? new ParticleSystem.MinMaxGradient(new Color(0.7f, 1f, 0.3f), new Color(0.45f, 0.9f, 0.2f))
                : new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.2f), new Color(1f, 0.45f, 0.08f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            var emission = ps.emission;
            emission.rateOverTime = Rate(ooze ? 2f : 2.6f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.9f;
            // They darken as they cool (orange to deep red) and shrink at the end.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(ooze ? new Color(0.3f, 0.6f, 0.15f) : new Color(0.6f, 0.12f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 0.3f)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = PixelMaterial();
            // A wisp of smoke from each pop.
            var smoke = Emitter(go.transform.parent, "PopSmoke", at + Vector3.up * 0.15f, new Vector3(1.4f, 0.1f, 1.4f), ooze ? 1.5f : 2f,
                1.2f, 2f, 0.12f, 0.22f, ooze ? new Color(0.5f, 0.6f, 0.35f, 0.35f) : new Color(0.35f, 0.3f, 0.28f, 0.4f),
                new Color(0.25f, 0.22f, 0.2f, 0.2f), new Vector3(-0.05f, 0.4f, -0.05f), new Vector3(0.05f, 0.8f, 0.05f), 0.2f, false, true);
            smoke.transform.position = at + Vector3.up * 0.15f;
            ps.Play();
        }

        /// <summary>An ice-crystal cluster: a few glowing pale-blue spikes, and sparkles that twinkle over them.</summary>
        public static GameObject Crystal(Transform parent, Vector3 basePosition, int seed, Color color)
        {
            var root = new GameObject("IceCrystal").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var rng = new System.Random(seed);
            var mat = Glow(color, 1.1f);
            int spikes = 3 + rng.Next(0, 3);
            for (int i = 0; i < spikes; i++)
            {
                var spike = new GameObject("Spike", typeof(MeshFilter), typeof(MeshRenderer));
                spike.transform.SetParent(root, false);
                float h = 0.6f + (float)rng.NextDouble() * (i == 0 ? 1.1f : 0.6f);
                spike.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.5f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.5f);
                spike.transform.localRotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 40f, rng.Next(0, 360), ((float)rng.NextDouble() - 0.5f) * 40f);
                spike.transform.localScale = new Vector3(0.18f + (float)rng.NextDouble() * 0.1f, h, 0.18f + (float)rng.NextDouble() * 0.1f);
                spike.GetComponent<MeshFilter>().sharedMesh = SpikeMesh();
                spike.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            Emitter(root, "Sparkle", new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 1.2f, 0.9f), 3f, 0.4f, 0.8f, 0.1f, 0.2f,
                Color.white, new Color(0.7f, 0.95f, 1f), Vector3.zero, new Vector3(0f, 0.05f, 0f), 0f, true, true);
            return root.gameObject;
        }

        /// <summary>A floating prism shard that turns and bobs, throwing rainbow glints (Sanctuary).</summary>
        public static GameObject PrismShard(Transform parent, Vector3 basePosition, int seed)
        {
            var root = new GameObject("PrismShard").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var shard = new GameObject("Shard", typeof(MeshFilter), typeof(MeshRenderer));
            shard.transform.SetParent(root, false);
            shard.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            shard.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
            shard.GetComponent<MeshFilter>().sharedMesh = SpikeMesh(true);
            shard.GetComponent<MeshRenderer>().sharedMaterial = Glow(new Color(0.95f, 0.95f, 1f), 1.4f);
            Clock.Spin(shard.transform, seed);
            Emitter(root, "Glints", new Vector3(0f, 1.3f, 0f), new Vector3(1.4f, 1.4f, 1.4f), 5f, 0.5f, 1f, 0.1f, 0.18f,
                Color.white, Color.white, Vector3.zero, new Vector3(0f, 0.1f, 0f), 0f, true, true, true);
            return root.gameObject;
        }

        /// <summary>A soft beam of light slanting down onto the cell (walkable), gently breathing.</summary>
        public static GameObject LightShaft(Transform parent, Vector3 basePosition, Vector2 sunAngles, Color color)
        {
            var root = new GameObject("LightShaft").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            root.rotation = Quaternion.Euler(0f, sunAngles.y, 0f) * Quaternion.Euler(-(90f - sunAngles.x) * 0.6f, 0f, 0f);
            var mat = ShaftMaterial(color);
            for (int i = 0; i < 2; i++)
            {
                var quad = WorldArt.Primitive(PrimitiveType.Quad, "Beam", root, mat);
                quad.transform.localPosition = new Vector3(0f, 3f, 0f);
                quad.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                quad.transform.localScale = new Vector3(1.1f, 6f, 1f);
                quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Emitter(root, "Motes", new Vector3(0f, 1.5f, 0f), new Vector3(0.8f, 3f, 0.8f), 3f, 2f, 4f, 0.05f, 0.1f,
                new Color(1f, 0.97f, 0.85f), color, new Vector3(0f, -0.05f, 0f), new Vector3(0f, 0.05f, 0f), 0.2f, true, true);
            return root.gameObject;
        }

        /// <summary>A specimen capsule (the lab): a metal base and cap with pipes, a glass tube of glowing liquid with
        /// bubbles rising through it, and a dark shape floating inside. With <paramref name="light"/> it also lights the
        /// floor around it in the liquid's colour.</summary>
        public static GameObject Capsule(Transform parent, Vector3 basePosition, int seed, Color liquid, bool light)
        {
            var root = new GameObject("Capsule").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var metal = Metal(new Color(0.24f, 0.26f, 0.28f));
            var trim = Metal(new Color(0.42f, 0.4f, 0.32f));
            Part(root, PrimitiveType.Cylinder, metal, new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.12f, 0.9f));
            Part(root, PrimitiveType.Cylinder, trim, new Vector3(0f, 0.27f, 0f), new Vector3(0.78f, 0.03f, 0.78f));
            Part(root, PrimitiveType.Cylinder, metal, new Vector3(0f, 2.25f, 0f), new Vector3(0.86f, 0.1f, 0.86f));
            Part(root, PrimitiveType.Cylinder, trim, new Vector3(0f, 2.4f, 0f), new Vector3(0.5f, 0.06f, 0.5f));
            Part(root, PrimitiveType.Cylinder, metal, new Vector3(0.22f, 2.75f, 0f), new Vector3(0.09f, 0.35f, 0.09f));   // pipes
            Part(root, PrimitiveType.Cylinder, metal, new Vector3(-0.18f, 2.65f, 0.1f), new Vector3(0.07f, 0.25f, 0.07f));
            var fluid = Part(root, PrimitiveType.Cylinder, Glow(liquid, 0.9f), new Vector3(0f, 1.25f, 0f), new Vector3(0.64f, 0.9f, 0.64f));
            fluid.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var tube = Part(root, PrimitiveType.Cylinder, GlassMaterial(), new Vector3(0f, 1.25f, 0f), new Vector3(0.74f, 0.98f, 0.74f));
            tube.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var rng = new System.Random(seed);
            if (rng.Next(4) != 0)
            {
                // A dark shape curled up in the liquid, drifting slowly.
                var body = Part(root, PrimitiveType.Capsule, Metal(new Color(0.06f, 0.08f, 0.07f)), new Vector3(0f, 1.25f, 0f), new Vector3(0.3f, 0.42f, 0.3f));
                body.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 20f - 10f, rng.Next(0, 360), (float)rng.NextDouble() * 20f - 10f);
                body.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Clock.Bob(body.transform, seed);
            }
            Emitter(root, "Bubbles", new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.1f, 0.45f), 4f, 1.2f, 1.8f, 0.04f, 0.08f,
                new Color(1f, 1f, 1f, 0.9f), liquid, new Vector3(0f, 0.7f, 0f), new Vector3(0f, 1f, 0f), 0.05f, true, false);
            if (light)
            {
                var l = new GameObject("CapsuleLight").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.localPosition = new Vector3(0f, 1.2f, -0.6f);
                l.type = LightType.Point;
                l.color = liquid;
                l.range = 3.2f;
                l.intensity = 1.2f;
                l.shadows = LightShadows.None;
                Clock.Flicker(l);
            }
            return root.gameObject;
        }

        /// <summary>A dormant lab robot, slumped where it stopped: boxy body and head, one dim eye, arms hanging and legs
        /// out in front. Built from simple shapes in the toon light, so it sits in the pixel art like the props.</summary>
        public static GameObject Robot(Transform parent, Vector3 basePosition, int seed)
        {
            var root = new GameObject("DormantRobot").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var rng = new System.Random(seed);
            root.rotation = Quaternion.Euler(0f, rng.Next(0, 360), 0f);
            var shell = Metal(new Color(0.55f, 0.56f, 0.52f));
            var dark = Metal(new Color(0.18f, 0.19f, 0.2f));
            Part(root, PrimitiveType.Cube, dark, new Vector3(0f, 0.18f, 0f), new Vector3(0.7f, 0.36f, 0.55f));
            var body = Part(root, PrimitiveType.Cube, shell, new Vector3(0f, 0.68f, -0.06f), new Vector3(0.62f, 0.66f, 0.46f));
            body.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            var head = Part(root, PrimitiveType.Cube, shell, new Vector3(0.04f, 1.12f, 0.08f), new Vector3(0.4f, 0.32f, 0.36f));
            head.transform.localRotation = Quaternion.Euler(32f, 0f, (float)rng.NextDouble() * 24f - 12f);
            var eyeColour = rng.Next(2) == 0 ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.45f, 0.2f);
            var eye = Part(head.transform, PrimitiveType.Cube, Glow(eyeColour, 0.6f), new Vector3(0f, 0f, 0.52f), new Vector3(0.5f, 0.18f, 0.08f));
            eye.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var armL = Part(root, PrimitiveType.Cube, dark, new Vector3(-0.42f, 0.5f, 0.05f), new Vector3(0.14f, 0.6f, 0.14f));
            armL.transform.localRotation = Quaternion.Euler(10f, 0f, 8f);
            var armR = Part(root, PrimitiveType.Cube, dark, new Vector3(0.42f, 0.5f, 0.05f), new Vector3(0.14f, 0.6f, 0.14f));
            armR.transform.localRotation = Quaternion.Euler(-6f, 0f, -10f);
            Part(root, PrimitiveType.Cube, dark, new Vector3(-0.18f, 0.12f, 0.42f), new Vector3(0.16f, 0.16f, 0.5f));
            Part(root, PrimitiveType.Cube, dark, new Vector3(0.18f, 0.12f, 0.42f), new Vector3(0.16f, 0.16f, 0.5f));
            return root.gameObject;
        }

        /// <summary>A metal gantry with chains hanging from its beam (the lab, the foundry), swaying slightly, each link
        /// a small bar turned a quarter from the last.</summary>
        public static GameObject Chains(Transform parent, Vector3 basePosition, int seed)
        {
            var root = new GameObject("Chains").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var rng = new System.Random(seed);
            root.rotation = Quaternion.Euler(0f, rng.Next(2) * 90f, 0f);
            var iron = Metal(new Color(0.2f, 0.2f, 0.22f));
            var rust = Metal(new Color(0.36f, 0.24f, 0.18f));
            Part(root, PrimitiveType.Cube, iron, new Vector3(-0.42f, 1.5f, 0f), new Vector3(0.1f, 3f, 0.1f));
            Part(root, PrimitiveType.Cube, iron, new Vector3(0.42f, 1.5f, 0f), new Vector3(0.1f, 3f, 0.1f));
            Part(root, PrimitiveType.Cube, rust, new Vector3(0f, 2.95f, 0f), new Vector3(1f, 0.12f, 0.14f));
            for (int c = 0; c < 3; c++)
            {
                // Each chain hangs from a pivot under the beam, so it swings from the top.
                var pivot = new GameObject("Chain").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(-0.25f + c * 0.25f, 2.88f, 0f);
                int links = 5 + rng.Next(0, 6);
                for (int i = 0; i < links; i++)
                {
                    var link = Part(pivot, PrimitiveType.Cube, i % 2 == 0 ? iron : rust, new Vector3(0f, -0.1f - i * 0.17f, 0f), new Vector3(0.05f, 0.16f, 0.1f));
                    link.transform.localRotation = Quaternion.Euler(0f, i % 2 == 0 ? 0f : 90f, 0f);
                }
                if (rng.Next(3) == 0)   // a hook on the end
                    Part(pivot, PrimitiveType.Cube, rust, new Vector3(0f, -0.2f - links * 0.17f, 0.04f), new Vector3(0.05f, 0.08f, 0.16f));
                Clock.Swing(pivot, seed + c * 17);
            }
            return root.gameObject;
        }

        /// <summary>A brazier fire: an iron bowl on legs, flames licking up and flickering, embers, and a warm light on
        /// everything around it (with <paramref name="light"/>).</summary>
        public static GameObject Fire(Transform parent, Vector3 basePosition, int seed, bool light)
        {
            var root = new GameObject("Fire").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var iron = Metal(new Color(0.16f, 0.14f, 0.14f));
            Part(root, PrimitiveType.Cylinder, iron, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 0.08f, 0.6f));
            Part(root, PrimitiveType.Cylinder, iron, new Vector3(0f, 0.28f, 0f), new Vector3(0.12f, 0.28f, 0.12f));
            Part(root, PrimitiveType.Cylinder, iron, new Vector3(0f, 0.03f, 0f), new Vector3(0.4f, 0.03f, 0.4f));
            var coals = Part(root, PrimitiveType.Cylinder, Glow(new Color(1f, 0.45f, 0.1f), 1f), new Vector3(0f, 0.62f, 0f), new Vector3(0.5f, 0.02f, 0.5f));
            coals.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The flames: big soft blobs rising and shrinking, yellow at the root to deep red at the tips.
            var flames = Emitter(root, "Flames", new Vector3(0f, 0.68f, 0f), new Vector3(0.36f, 0.05f, 0.36f), 26f, 0.45f, 0.8f, 0.22f, 0.38f,
                new Color(1f, 0.85f, 0.35f), new Color(1f, 0.55f, 0.15f), new Vector3(-0.05f, 0.9f, -0.05f), new Vector3(0.05f, 1.5f, 0.05f), 0.4f, true, false);
            var col = flames.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f), new GradientColorKey(new Color(0.6f, 0.08f, 0.02f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var size = flames.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f)));
            flames.GetComponent<ParticleSystemRenderer>().sharedMaterial = FlameMaterial();
            Emitter(root, "Embers", new Vector3(0f, 0.9f, 0f), new Vector3(0.3f, 0.1f, 0.3f), 5f, 1f, 2f, 0.04f, 0.07f,
                new Color(1f, 0.7f, 0.25f), new Color(1f, 0.45f, 0.1f), new Vector3(-0.2f, 0.8f, -0.2f), new Vector3(0.2f, 1.6f, 0.2f), 0.6f, true, true);
            if (light)
            {
                var l = new GameObject("FireLight").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                l.type = LightType.Point;
                l.color = new Color(1f, 0.55f, 0.2f);
                l.range = 4f;
                l.intensity = 1.6f;
                l.shadows = LightShadows.None;
                Clock.Flicker(l);
            }
            return root.gameObject;
        }

        static Material flameMat;

        /// <summary>Soft round additive blobs for flames (the puff texture, glowing).</summary>
        static Material FlameMaterial()
        {
            if (flameMat == null) flameMat = WorldArt.AdditiveTinted(PuffTexture(), new Color(1f, 1f, 1f, 1f));
            return flameMat;
        }

        /// <summary>A primitive part under <paramref name="root"/> (no collider), placed and sized locally.</summary>
        static GameObject Part(Transform root, PrimitiveType type, Material mat, Vector3 at, Vector3 size)
        {
            var go = WorldArt.Primitive(type, type.ToString(), root, mat);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            return go;
        }

        static readonly Dictionary<Color, Material> metals = new Dictionary<Color, Material>();
        static Material glassMat;

        /// <summary>Plain toon-shaded metal in one colour (lab machines).</summary>
        static Material Metal(Color color)
        {
            Material m;
            if (metals.TryGetValue(color, out m) && m != null) return m;
            m = Toon(Texture2D.whiteTexture, Color.black);
            m.color = color;
            metals[color] = m;
            return m;
        }

        /// <summary>The capsules' glass: a faint additive sheen, so the liquid shows through.</summary>
        static Material GlassMaterial()
        {
            if (glassMat == null) glassMat = WorldArt.AdditiveTinted(PuffTexture(), new Color(0.35f, 0.45f, 0.42f, 0.5f));
            return glassMat;
        }

        // ------------------------------------------------------------------ Materials, meshes and textures

        static Material lava, ooze, puff;
        static readonly Dictionary<Color, Material> glows = new Dictionary<Color, Material>();
        static readonly Dictionary<Color, Material> shafts = new Dictionary<Color, Material>();
        static Mesh spike, gem;

        static Material Toon(Texture tex, Color emission)
        {
            var m = new Material(WorldArt.FindShader("Reverse Shell/WorldToon", "Standard")) { mainTexture = tex };
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
            if (m.HasProperty("_VertexShade")) m.SetFloat("_VertexShade", 0f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.01f);
            return m;
        }

        /// <summary>The churning pool material (the Lava shader over a grey noise), lava or ooze colours.</summary>
        static Material PoolMaterial(bool isOoze)
        {
            var m = isOoze ? ooze : lava;
            if (m != null) return m;
            m = new Material(WorldArt.FindShader("Reverse Shell/Lava", "Unlit/Texture")) { mainTexture = NoiseTexture(), name = isOoze ? "Ooze" : "Lava" };
            if (isOoze)
            {
                m.SetColor("_C0", new Color(0.1f, 0.22f, 0.05f));
                m.SetColor("_C1", new Color(0.2f, 0.42f, 0.08f));
                m.SetColor("_C2", new Color(0.4f, 0.7f, 0.12f));
                m.SetColor("_C3", new Color(0.72f, 0.95f, 0.35f));
                m.SetVector("_Speed", new Vector4(0.025f, -0.015f, -0.02f, 0.02f));
                m.SetFloat("_Glow", 1.25f);
                ooze = m;
            }
            else lava = m;
            return m;
        }

        /// <summary>The soft glow a pool throws on the ground around it (additive, one per colour).</summary>
        static Material HaloMaterial(Color color)
        {
            Material m;
            if (halos.TryGetValue(color, out m) && m != null) return m;
            m = WorldArt.AdditiveTinted(HaloTexture(), color * 0.55f);
            halos[color] = m;
            return m;
        }

        /// <summary>Crisp square pixels for the lava pops (additive, so they glow).</summary>
        static Material PixelMaterial()
        {
            if (pixel == null)
            {
                pixel = new Material(WorldArt.FindShader("Legacy Shaders/Particles/Additive", "Particles/Standard Unlit"));
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Pixel" };
                tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                tex.Apply();
                pixel.mainTexture = tex;
            }
            return pixel;
        }

        static readonly Dictionary<Color, Material> halos = new Dictionary<Color, Material>();
        static Material pixel;

        /// <summary>A glowing solid (crystals, prism shards): a toon material that lights itself.</summary>
        static Material Glow(Color color, float strength)
        {
            Material m;
            if (glows.TryGetValue(color, out m) && m != null) return m;
            m = Toon(Texture2D.whiteTexture, color * strength * 0.6f);
            m.color = color;
            glows[color] = m;
            return m;
        }

        static Material ShaftMaterial(Color color)
        {
            Material m;
            if (shafts.TryGetValue(color, out m) && m != null) return m;
            m = WorldArt.AdditiveTinted(BeamTexture(), color * 0.35f);
            Clock.Breathe(m, color * 0.35f);
            shafts[color] = m;
            return m;
        }

        static Material SoftPuff()
        {
            if (puff == null) puff = new Material(WorldArt.FindShader("Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit")) { mainTexture = PuffTexture() };
            return puff;
        }

        /// <summary>Tileable grey value noise (two octaves) that the Lava shader drifts and bands.</summary>
        static Texture2D NoiseTexture()
        {
            if (noise != null) return noise;
            const int size = 64;
            noise = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat, name = "PoolNoise" };
            var rng = new System.Random(3);
            var grid = new float[8, 8];
            var fine = new float[16, 16];
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) grid[x, y] = (float)rng.NextDouble();
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) fine[x, y] = (float)rng.NextDouble();
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Smooth(grid, 8, x / (float)size * 8f, y / (float)size * 8f) * 0.65f + Smooth(fine, 16, x / (float)size * 16f, y / (float)size * 16f) * 0.35f;
                    noise.SetPixel(x, y, new Color(n, n, n, 1f));
                }
            noise.Apply();
            return noise;
        }

        static Texture2D noise, halo;

        /// <summary>A round glow, bright in the middle and gone at the edge (smooth falloff).</summary>
        static Texture2D HaloTexture()
        {
            if (halo != null) return halo;
            const int size = 32;
            halo = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Halo" };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f));
                    float v = (1f - d) * (1f - d);
                    halo.SetPixel(x, y, new Color(v, v, v, v));
                }
            halo.Apply();
            return halo;
        }

        /// <summary>Wrapping bilinear value noise.</summary>
        static float Smooth(float[,] g, int n, float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = g[(x0 % n + n) % n, (y0 % n + n) % n], b = g[(x0 + 1) % n, (y0 % n + n) % n];
            float c = g[(x0 % n + n) % n, (y0 + 1) % n], d = g[(x0 + 1) % n, (y0 + 1) % n];
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static Texture2D beam, puffTex;

        /// <summary>Bright at the top, fading to nothing at the bottom and the sides.</summary>
        static Texture2D BeamTexture()
        {
            if (beam != null) return beam;
            beam = new Texture2D(16, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Beam" };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 16; x++)
                {
                    float side = 1f - Mathf.Abs((x + 0.5f) / 16f * 2f - 1f);
                    float v = Mathf.Pow(y / 31f, 1.4f) * side * side;
                    beam.SetPixel(x, y, new Color(v, v, v, v));
                }
            beam.Apply();
            return beam;
        }

        /// <summary>A soft round puff (smog).</summary>
        static Texture2D PuffTexture()
        {
            if (puffTex != null) return puffTex;
            const int size = 32;
            puffTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Puff" };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    puffTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * 0.35f));
                }
            puffTex.Apply();
            return puffTex;
        }

        /// <summary>A six-sided spike, 1 tall from its base (crystals), or a double-pointed gem (prism shards); flat-shaded.</summary>
        static Mesh SpikeMesh(bool doublePoint = false)
        {
            if (doublePoint ? gem != null : spike != null) return doublePoint ? gem : spike;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const int sides = 6;
            float mid = doublePoint ? 0f : 0.65f, top = doublePoint ? 0.5f : 1f, bottom = doublePoint ? -0.5f : 0f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, mid, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, mid, Mathf.Sin(a1) * 0.5f);
                var b0 = doublePoint ? p0 : new Vector3(p0.x, bottom, p0.z);
                var b1 = doublePoint ? p1 : new Vector3(p1.x, bottom, p1.z);
                // The tip.
                Tri(verts, tris, p0, new Vector3(0f, top, 0f), p1);
                if (doublePoint) Tri(verts, tris, p1, new Vector3(0f, bottom, 0f), p0);
                else
                {
                    Tri(verts, tris, b0, p0, p1);
                    Tri(verts, tris, b0, p1, b1);
                }
            }
            var mesh = new Mesh { name = doublePoint ? "Gem" : "Spike" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (doublePoint) gem = mesh; else spike = mesh;
            return mesh;
        }

        static void Tri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        // ------------------------------------------------------------------ Clock

        /// <summary>Animates the shared materials (the pools flow and pulse, the light shafts breathe) and spins the
        /// prism shards. Made once per area (it lives on the builder's object, so it goes with the scene).</summary>
        public class Clock : MonoBehaviour
        {
            class Flowing { public Material Material; public Vector2 Speed; public Color Glow; }
            class Breathing { public Material Material; public Color Tint; }
            class Spinning { public Transform Transform; public float Phase; public Vector3 Home; public bool Turn = true; }
            class Flickering { public Light Light; public float Base; public float Phase; }

            static readonly List<Flowing> flowing = new List<Flowing>();
            static readonly List<Breathing> breathing = new List<Breathing>();
            static readonly List<Spinning> spinning = new List<Spinning>();
            static readonly List<Flickering> flickering = new List<Flickering>();
            static Clock instance;

            /// <summary>Makes sure the area has a clock (call once from the builder).</summary>
            public static void Ensure(GameObject host)
            {
                if (instance == null) instance = host.AddComponent<Clock>();
            }

            public static void Flow(Material m, Vector2 speed, Color glow) { flowing.Add(new Flowing { Material = m, Speed = speed, Glow = glow }); }
            public static void Breathe(Material m, Color tint) { breathing.Add(new Breathing { Material = m, Tint = tint }); }
            public static void Flicker(Light l) { flickering.Add(new Flickering { Light = l, Base = l.intensity, Phase = flickering.Count * 1.7f }); }
            public static void Spin(Transform t, int seed) { spinning.Add(new Spinning { Transform = t, Phase = seed % 100 * 0.1f, Home = t.localPosition }); }
            /// <summary>A gentle swing from the top (a hanging chain).</summary>
            public static void Swing(Transform t, int seed) { swinging.Add(new Spinning { Transform = t, Phase = seed % 100 * 0.1f, Home = t.localPosition, Turn = false }); }
            static readonly List<Spinning> swinging = new List<Spinning>();
            /// <summary>An indicator light blinking on and off (a server rack's LEDs): on for <paramref name="duty"/> of
            /// every <paramref name="period"/> seconds, from <paramref name="phase"/> (0-1).</summary>
            public static void Blink(Material m, Color glow, float period, float duty, float phase) { blinking.Add(new Blinking { Material = m, Glow = glow, Period = period, Duty = duty, Phase = phase }); }
            public static bool IsBlinking(Material m) { return blinking.Exists(b => b.Material == m); }
            class Blinking { public Material Material; public Color Glow; public float Period, Duty, Phase; }
            static readonly List<Blinking> blinking = new List<Blinking>();
            /// <summary>A slow float up and down, without turning (a specimen in its capsule).</summary>
            public static void Bob(Transform t, int seed) { spinning.Add(new Spinning { Transform = t, Phase = seed % 100 * 0.1f, Home = t.localPosition, Turn = false }); }

            void Update()
            {
                float t = Time.time;
                foreach (var f in flowing)
                {
                    if (f.Material == null) continue;
                    f.Material.mainTextureOffset = f.Speed * t;
                    f.Material.SetColor("_EmissionColor", f.Glow * (0.85f + 0.15f * Mathf.Sin(t * 1.3f)));
                }
                foreach (var b in breathing)
                    if (b.Material != null) b.Material.SetColor("_TintColor", b.Tint * (0.75f + 0.25f * Mathf.Sin(t * 0.8f)));
                blinking.RemoveAll(b => b.Material == null);
                foreach (var b in blinking)
                {
                    bool on = Mathf.Repeat(t / b.Period + b.Phase, 1f) < b.Duty;
                    b.Material.SetColor("_EmissionColor", on ? b.Glow : b.Glow * 0.08f);
                }
                flickering.RemoveAll(f => f.Light == null);
                foreach (var f in flickering)
                    f.Light.intensity = f.Base * (0.85f + 0.1f * Mathf.Sin(t * 2.3f + f.Phase) + 0.05f * Mathf.Sin(t * 7.1f + f.Phase * 2f));
                swinging.RemoveAll(s => s.Transform == null);
                foreach (var s in swinging)
                    s.Transform.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.1f + s.Phase) * 4f, 0f, Mathf.Sin(t * 0.8f + s.Phase * 1.3f) * 6f);
                spinning.RemoveAll(s => s.Transform == null);
                foreach (var s in spinning)
                {
                    if (s.Turn) s.Transform.localRotation = Quaternion.Euler(0f, (t * 40f + s.Phase * 30f) % 360f, 0f);
                    s.Transform.localPosition = s.Home + Vector3.up * Mathf.Sin(t * (s.Turn ? 1.4f : 0.6f) + s.Phase) * (s.Turn ? 0.12f : 0.06f);
                }
            }

            void OnDestroy()
            {
                if (instance != this) return;
                instance = null;
                spinning.Clear();
                swinging.Clear();
                flickering.Clear();
                blinking.Clear();
            }
        }
    }
}
