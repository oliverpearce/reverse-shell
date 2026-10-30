using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The office (the opening, and a later visit): desks (tile a) with the Retro PC (CC0, Resources/Props/RetroPC) and a
    /// chair. The PC's materials become toon ones with its own colour maps; its monitor glass is a phosphor screen with
    /// a faint idle glow, which the opening lights up (<see cref="OfficePC.SetGlow"/>). Every desk faces south, toward
    /// the camera, with its chair on the south side, and carries a working mess (books, papers, cards, a mug). The lab
    /// side of the room: benches of glowing test tubes and flasks (tile v) and blinking server racks (tile x).
    /// </summary>
    public static class OfficeKit
    {
        const string ModelPath = "Props/RetroPC/Retro Pc";
        const string TexturePath = "Props/RetroPC/";

        /// <summary>The desk top's height, and the PC set's (to the monitor's top) on it, in tiles.</summary>
        public const float DeskHeight = 0.62f;
        public const float PcHeight = 0.5f;

        /// <summary>The idle screens' glow: dim green phosphor.</summary>
        public static readonly Color IdleGlow = new Color(0.05f, 0.11f, 0.09f);

        static readonly Color PlasticTint = new Color(0.78f, 0.76f, 0.72f);

        // A material name (the model's, "Ucupaint Keyboard Creme" ...) to its colour map: the first keyword it contains.
        static readonly string[] MapKeys = { "Keyboard", "Mouse", "Front", "Monitor", "PC" };
        static readonly string[] MapFiles =
        {
            "Ucupaint Keyboard Creme_001 Color", "Ucupaint Mouse Creme Color", "Ucupaint PC Creme Front Color",
            "Ucupaint Monitor Cream Color", "Ucupaint PC Creme Color",
        };

        static GameObject prefab;
        static bool loaded;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly Dictionary<Color, Material> plains = new Dictionary<Color, Material>();

        /// <summary>A desk at <paramref name="basePosition"/> (the cell's centre on the floor): a light wood top on four
        /// legs, a modesty panel, the PC on top and a chair south of it, turned a little by <paramref name="seed"/>.</summary>
        public static OfficePC Desk(Transform parent, Vector3 basePosition, int seed)
        {
            var root = new GameObject("OfficeDesk").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var rng = new System.Random(seed);
            var wood = Plain(new Color(0.46f, 0.36f, 0.28f));
            var steel = Plain(new Color(0.36f, 0.38f, 0.4f));
            var fabric = Plain(new Color(0.18f, 0.2f, 0.24f));

            Part(root, wood, new Vector3(0f, DeskHeight - 0.03f, 0.08f), new Vector3(0.96f, 0.06f, 0.64f));
            for (int i = 0; i < 4; i++)
                Part(root, steel, new Vector3(i % 2 == 0 ? -0.43f : 0.43f, (DeskHeight - 0.06f) * 0.5f, 0.08f + (i < 2 ? -0.27f : 0.27f)),
                    new Vector3(0.05f, DeskHeight - 0.06f, 0.05f));
            Part(root, wood, new Vector3(0f, 0.36f, 0.37f), new Vector3(0.86f, 0.4f, 0.03f));

            var chair = new GameObject("Chair").transform;
            chair.SetParent(root, false);
            chair.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.12f, 0f, -0.46f);
            chair.localRotation = Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 24f, 0f);
            Part(chair, steel, new Vector3(0f, 0.17f, 0f), new Vector3(0.05f, 0.3f, 0.05f));
            Part(chair, steel, new Vector3(0f, 0.03f, 0f), new Vector3(0.36f, 0.04f, 0.36f));
            Part(chair, fabric, new Vector3(0f, 0.36f, 0f), new Vector3(0.4f, 0.07f, 0.38f));
            Part(chair, fabric, new Vector3(0f, 0.62f, -0.19f), new Vector3(0.38f, 0.42f, 0.06f));

            Clutter(root, rng);
            var pc = RetroPC(root, basePosition + new Vector3(0f, DeskHeight, 0.1f), Vector3.back);
            pc.Chair = chair;
            return pc;
        }

        // Book spines and covers, and the papers' off-whites.
        static readonly Color[] BookColours =
        {
            new Color(0.42f, 0.12f, 0.12f), new Color(0.14f, 0.2f, 0.36f), new Color(0.16f, 0.3f, 0.2f),
            new Color(0.55f, 0.42f, 0.18f), new Color(0.22f, 0.2f, 0.22f),
        };
        static readonly Color[] PaperColours = { new Color(0.86f, 0.85f, 0.8f), new Color(0.8f, 0.79f, 0.72f), new Color(0.88f, 0.84f, 0.66f) };
        /// <summary>The full-art Ult cards (Resources/Cards) a desk's face-up card is picked from.</summary>
        static readonly string[] FaceUpCards =
        {
            "Cards/Ult_Char_MC", "Cards/Ult_Char_Ashe", "Cards/Ult_Char_Lucy", "Cards/Ult_Char_Blue", "Cards/Ult_Card_MC_Thunderheart",
        };
        static readonly Dictionary<string, Material> cardMaterials = new Dictionary<string, Material>();

        /// <summary>One card standing on the desk at <paramref name="at"/> (its bottom edge), leaning back 25 degrees and
        /// turned by <paramref name="yaw"/>, its picture facing south (toward the chair and the camera).</summary>
        static void StandingCard(Transform root, Vector3 at, float yaw, string picture)
        {
            const float Width = 0.08f, Height = Width * 64f / 48f;   // the art's 48x64 shape
            var pivot = new GameObject("Card").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = at;
            pivot.localRotation = Quaternion.Euler(25f, yaw, 0f);
            var card = Part(pivot, Plain(new Color(0.1f, 0.08f, 0.1f)), new Vector3(0f, Height * 0.5f, 0f), new Vector3(Width, Height, 0.003f));
            var art = WorldArt.Primitive(PrimitiveType.Quad, "Art", card.transform, CardMaterial(picture));
            art.transform.localPosition = new Vector3(0f, 0f, -0.6f);   // just in front of the card's face
            art.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A card's picture (48x64 pixel art in Resources/Cards) as a toon material, or plain plum if missing.</summary>
        static Material CardMaterial(string path)
        {
            Material m;
            if (cardMaterials.TryGetValue(path, out m) && m != null) return m;
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null) return Plain(new Color(0.3f, 0.14f, 0.34f));
            m = Toon(texture);
            m.name = path;
            cardMaterials[path] = m;
            return m;
        }

        /// <summary>A working desk's mess, either side of the PC: a stack of books and a few standing upright, papers
        /// fanned across the corners, a little spread of cards and a mug. Varied by <paramref name="rng"/>.</summary>
        static void Clutter(Transform root, System.Random rng)
        {
            const float Top = DeskHeight;
            float R() { return (float)rng.NextDouble(); }

            // Left: a stack of books lying flat, each turned a little.
            float y = Top;
            int stack = 2 + rng.Next(3);
            for (int i = 0; i < stack; i++)
            {
                float h = 0.03f + R() * 0.025f;
                var book = Part(root, Plain(BookColours[rng.Next(BookColours.Length)]), new Vector3(-0.36f + R() * 0.03f, y + h * 0.5f, 0.24f),
                    new Vector3(0.17f + R() * 0.04f, h, 0.23f + R() * 0.03f));
                book.transform.localRotation = Quaternion.Euler(0f, (R() - 0.5f) * 30f, 0f);
                // Pages: a pale edge on the open side.
                Part(book.transform, Plain(PaperColours[0]), new Vector3(0.5f, 0f, 0f), new Vector3(0.04f, 0.8f, 0.92f));
                y += h;
            }

            // Right back: a few books standing in a row, one leaning.
            int upright = 2 + rng.Next(3);
            for (int i = 0; i < upright; i++)
            {
                float h = 0.18f + R() * 0.07f;
                var book = Part(root, Plain(BookColours[rng.Next(BookColours.Length)]), new Vector3(0.3f + i * 0.045f, Top + h * 0.5f, 0.3f),
                    new Vector3(0.04f, h, 0.15f + R() * 0.03f));
                if (i == upright - 1) book.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            }

            // Papers: loose sheets across the front corners and under the right side, overlapping.
            int sheets = 4 + rng.Next(4);
            for (int i = 0; i < sheets; i++)
            {
                bool left = i % 2 == 0;
                var at = new Vector3((left ? -0.33f : 0.32f) + (R() - 0.5f) * 0.12f, Top + 0.002f + i * 0.0015f, -0.1f + (R() - 0.5f) * 0.16f);
                var sheet = Part(root, Plain(PaperColours[rng.Next(PaperColours.Length)]), at, new Vector3(0.15f, 0.003f, 0.2f));
                sheet.transform.localRotation = Quaternion.Euler(0f, (R() - 0.5f) * 70f, 0f);
                sheet.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // A few lines of type on some.
                if (R() < 0.6f)
                    for (int l = 0; l < 4; l++)
                        Part(sheet.transform, Plain(new Color(0.3f, 0.3f, 0.32f)), new Vector3(0f, 0.6f, 0.3f - l * 0.14f), new Vector3(0.75f, 0.4f, 0.04f));
            }

            // Cards: the game's own Ult cards, each a different one, standing up and leaning back a little so they face
            // the chair: three against the book stack on the left, two against the upright books on the right, all
            // beside the monitor where the seated view sees them, none overlapping.
            var faces = new List<string>(FaceUpCards);
            for (int i = faces.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var swap = faces[i];
                faces[i] = faces[j];
                faces[j] = swap;
            }
            var spots = new[]
            {
                new Vector3(-0.435f, Top, 0.06f), new Vector3(-0.35f, Top, 0.06f), new Vector3(-0.265f, Top, 0.06f),
                new Vector3(0.31f, Top, 0.15f), new Vector3(0.41f, Top, 0.15f),
            };
            for (int i = 0; i < spots.Length; i++)
                StandingCard(root, spots[i], (R() - 0.5f) * 14f, faces[i % faces.Count]);

            // A mug, sometimes.
            if (rng.Next(3) != 0)
            {
                var mug = WorldArt.Primitive(PrimitiveType.Cylinder, "Mug", root, Plain(new Color(0.7f, 0.68f, 0.62f)));
                mug.transform.localPosition = new Vector3(0.42f, Top + 0.045f, -0.12f);   // in front, clear of the cards
                mug.transform.localScale = new Vector3(0.07f, 0.045f, 0.07f);
                var coffee = WorldArt.Primitive(PrimitiveType.Cylinder, "Coffee", mug.transform, Plain(new Color(0.16f, 0.1f, 0.06f)));
                coffee.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                coffee.transform.localScale = new Vector3(0.82f, 0.12f, 0.82f);
            }
        }

        /// <summary>A lab bench (tile v): a steel table with a rack of test tubes and a couple of flasks glowing with
        /// liquids, a microscope and a clipboard; the glow takes the theme's capsule colour and a few others.</summary>
        public static GameObject LabBench(Transform parent, Vector3 basePosition, int seed, Color liquid)
        {
            var root = new GameObject("LabBench").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var rng = new System.Random(seed);
            float R() { return (float)rng.NextDouble(); }
            var steel = Plain(new Color(0.32f, 0.34f, 0.36f));
            var dark = Plain(new Color(0.12f, 0.13f, 0.14f));
            var liquids = new[] { liquid, new Color(0.3f, 0.75f, 1f), new Color(0.9f, 0.3f, 0.75f), new Color(1f, 0.7f, 0.25f) };

            Part(root, steel, new Vector3(0f, DeskHeight - 0.03f, 0f), new Vector3(0.96f, 0.06f, 0.7f));
            Part(root, dark, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.04f, 0.62f));   // lower shelf
            for (int i = 0; i < 4; i++)
                Part(root, steel, new Vector3(i % 2 == 0 ? -0.44f : 0.44f, (DeskHeight - 0.06f) * 0.5f, i < 2 ? -0.31f : 0.31f),
                    new Vector3(0.04f, DeskHeight - 0.06f, 0.04f));
            // Boxes of samples on the shelf.
            for (int i = 0; i < 2 + rng.Next(2); i++)
                Part(root, Plain(new Color(0.24f, 0.26f, 0.24f)), new Vector3(-0.3f + i * 0.28f, 0.29f, (R() - 0.5f) * 0.2f), new Vector3(0.22f, 0.14f, 0.2f));

            // The rack and its tubes.
            float top = DeskHeight;
            Part(root, dark, new Vector3(-0.15f, top + 0.06f, 0.08f), new Vector3(0.44f, 0.02f, 0.1f));
            Part(root, dark, new Vector3(-0.15f, top + 0.01f, 0.08f), new Vector3(0.44f, 0.02f, 0.1f));
            int tubes = 5 + rng.Next(3);
            for (int i = 0; i < tubes; i++)
            {
                var colour = liquids[rng.Next(liquids.Length)];
                float fill = 0.05f + R() * 0.08f;
                float x = -0.33f + i * (0.38f / (tubes - 1));
                var glass = Part(root, Glass(), new Vector3(x, top + 0.09f, 0.08f), new Vector3(0.035f, 0.08f, 0.035f), PrimitiveType.Cylinder);
                glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Part(root, Glow(colour), new Vector3(x, top + 0.01f + fill, 0.08f), new Vector3(0.028f, fill, 0.028f), PrimitiveType.Cylinder);
            }

            // Flasks: a round body, a neck, glowing liquid.
            for (int i = 0; i < 2; i++)
            {
                var colour = liquids[rng.Next(liquids.Length)];
                var at = new Vector3(0.2f + i * 0.16f, top, -0.02f + i * 0.14f);
                float size = 0.11f + R() * 0.04f;
                Part(root, Glow(colour), at + Vector3.up * size * 0.45f, Vector3.one * size, PrimitiveType.Sphere);
                Part(root, Glass(), at + Vector3.up * size * 1.05f, new Vector3(0.035f, size * 0.35f, 0.035f), PrimitiveType.Cylinder);
            }

            // A microscope: a dark base, an arm and a tube.
            var scope = new GameObject("Microscope").transform;
            scope.SetParent(root, false);
            scope.localPosition = new Vector3(-0.28f, top, -0.18f);
            scope.localRotation = Quaternion.Euler(0f, 150f + R() * 40f, 0f);
            Part(scope, dark, new Vector3(0f, 0.015f, 0f), new Vector3(0.12f, 0.03f, 0.14f));
            Part(scope, dark, new Vector3(0f, 0.11f, 0.05f), new Vector3(0.03f, 0.2f, 0.03f));
            var eyepiece = Part(scope, steel, new Vector3(0f, 0.17f, 0f), new Vector3(0.035f, 0.12f, 0.035f), PrimitiveType.Cylinder);
            eyepiece.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            // A clipboard.
            var board = Part(root, Plain(new Color(0.4f, 0.3f, 0.2f)), new Vector3(0.04f, top + 0.005f, -0.22f), new Vector3(0.16f, 0.01f, 0.22f));
            board.transform.localRotation = Quaternion.Euler(0f, (R() - 0.5f) * 30f, 0f);
            Part(board.transform, Plain(PaperColours[0]), new Vector3(0f, 0.6f, -0.05f), new Vector3(0.88f, 0.4f, 0.82f));
            return root.gameObject;
        }

        /// <summary>A server rack (tile x): a tall dark cabinet whose front is rows of units with blinking green, amber
        /// and the odd red indicator light, and a vent strip.</summary>
        public static GameObject ServerRack(Transform parent, Vector3 basePosition, int seed)
        {
            var root = new GameObject("ServerRack").transform;
            root.SetParent(parent, false);
            root.position = basePosition;
            var rng = new System.Random(seed);
            var body = Plain(new Color(0.1f, 0.11f, 0.12f));
            var unit = Plain(new Color(0.2f, 0.21f, 0.23f));
            const float Height = 1.9f;
            Part(root, body, new Vector3(0f, Height * 0.5f, 0.05f), new Vector3(0.8f, Height, 0.7f));
            int units = 9;
            for (int u = 0; u < units; u++)
            {
                float y = 0.2f + u * 0.18f;
                Part(root, unit, new Vector3(0f, y, -0.31f), new Vector3(0.7f, 0.14f, 0.02f));
                int leds = 2 + rng.Next(4);
                for (int i = 0; i < leds; i++)
                {
                    int kind = rng.Next(10);
                    var colour = kind < 6 ? new Color(0.3f, 1f, 0.5f) : kind < 9 ? new Color(1f, 0.7f, 0.2f) : new Color(1f, 0.25f, 0.2f);
                    var led = Part(root, Led(colour, rng), new Vector3(0.28f - i * 0.05f, y + 0.03f, -0.325f), new Vector3(0.025f, 0.025f, 0.01f));
                    led.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                // A vent slot on some units.
                if (rng.Next(2) == 0) Part(root, body, new Vector3(-0.15f, y - 0.02f, -0.325f), new Vector3(0.3f, 0.02f, 0.01f));
            }
            return root.gameObject;
        }

        static readonly List<Material> leds = new List<Material>();

        /// <summary>One of a handful of shared LED materials per colour, each blinking at its own pace
        /// (<see cref="WorldEffects.Clock.Blink"/>), so a rack twinkles without a material per light.</summary>
        static Material Led(Color colour, System.Random rng)
        {
            leds.RemoveAll(m => m == null || !WorldEffects.Clock.IsBlinking(m));   // a new area's clock starts empty
            var same = leds.FindAll(m => m.color == colour);
            if (same.Count < 4)
            {
                var m = Toon(Texture2D.whiteTexture);
                m.color = colour;
                WorldEffects.Clock.Blink(m, colour * 1.4f, 0.6f + same.Count * 0.45f, same.Count == 0 ? 0.9f : 0.55f, same.Count * 0.23f);
                leds.Add(m);
                return m;
            }
            return same[rng.Next(same.Count)];
        }

        static Material Glow(Color colour)
        {
            Material m;
            var key = new Color(colour.r, colour.g, colour.b, 0.5f);   // keyed apart from the plain colours
            if (plains.TryGetValue(key, out m) && m != null) return m;
            m = Toon(Texture2D.whiteTexture);
            m.color = colour;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", colour * 0.8f);
            plains[key] = m;
            return m;
        }

        static Material glass;

        /// <summary>Lab glass: a pale see-through sheen, so the liquid shows inside.</summary>
        static Material Glass()
        {
            if (glass == null) glass = WorldArt.AdditiveTinted(Texture2D.whiteTexture, new Color(0.4f, 0.5f, 0.52f, 0.35f));
            return glass;
        }

        /// <summary>The Retro PC standing at <paramref name="basePosition"/>, its screen facing <paramref name="facing"/>
        /// (flat), <see cref="PcHeight"/> tall. Without the model, a cream box with a dark screen.</summary>
        public static OfficePC RetroPC(Transform parent, Vector3 basePosition, Vector3 facing)
        {
            var model = Model();
            GameObject go;
            if (model != null)
            {
                go = Object.Instantiate(model, parent, false);
                go.name = "RetroPC";
                go.transform.position = Vector3.zero;
            }
            else go = Fallback(parent);

            var pc = go.AddComponent<OfficePC>();
            pc.Screen = Plain(Color.white, true);
            pc.Screen.color = new Color(0.08f, 0.12f, 0.1f);
            int glassSlot = -1;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    string name = shared[i] != null ? shared[i].name : "";
                    if (name.Contains("Glass"))
                    {
                        shared[i] = pc.Screen;
                        pc.Monitor = r;
                        glassSlot = i;
                    }
                    else shared[i] = Textured(name);
                }
                r.sharedMaterials = shared;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            pc.GlassLocal = GlassBounds(pc.Monitor, glassSlot);
            pc.SetGlow(IdleGlow);

            // To size: the whole set PcHeight tall.
            var b = WorldBounds(go);
            go.transform.localScale *= PcHeight / Mathf.Max(0.001f, b.size.y);

            // Turned so the screen faces the way asked: the glass sits on the monitor's front.
            var front = pc.GlassCenter - pc.Monitor.bounds.center;
            front.y = 0f;
            facing.y = 0f;
            if (front.sqrMagnitude > 1e-8f && facing.sqrMagnitude > 1e-8f)
                go.transform.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(front, facing, Vector3.up), Vector3.up) * go.transform.rotation;
            pc.Facing = facing.sqrMagnitude > 1e-8f ? facing.normalized : Vector3.back;

            // Standing on the desk, centred on the spot.
            b = WorldBounds(go);
            go.transform.position += basePosition - new Vector3(b.center.x, b.min.y, b.center.z);
            return pc;
        }

        static GameObject Model()
        {
            if (!loaded)
            {
                loaded = true;
                prefab = Resources.Load<GameObject>(ModelPath);
                if (prefab == null) Debug.LogWarning("OfficeKit: no Retro PC model in Resources/" + ModelPath);
            }
            return prefab;
        }

        /// <summary>A stand-in when the model is missing: a cream monitor on a flat case, a keyboard in front, the screen
        /// facing -z (its own cube, so its "Glass" material is a whole renderer).</summary>
        static GameObject Fallback(Transform parent)
        {
            var root = new GameObject("RetroPC").transform;
            root.SetParent(parent, false);
            var cream = Plain(new Color(0.86f, 0.82f, 0.72f));
            Part(root, cream, new Vector3(0f, 0.05f, 0f), new Vector3(0.46f, 0.1f, 0.42f));
            Part(root, cream, new Vector3(0f, 0.3f, 0.02f), new Vector3(0.42f, 0.38f, 0.36f));
            var glass = Part(root, cream, new Vector3(0f, 0.31f, -0.16f), new Vector3(0.32f, 0.26f, 0.02f));
            glass.GetComponent<Renderer>().sharedMaterial = new Material(cream) { name = "Glass" };
            Part(root, cream, new Vector3(0f, 0.015f, -0.34f), new Vector3(0.4f, 0.03f, 0.14f));
            return root.gameObject;
        }

        /// <summary>The glass's own box, in the monitor's local space: its triangles' corners (the model is imported
        /// readable for this), else the whole mesh.</summary>
        static Bounds GlassBounds(Renderer monitor, int slot)
        {
            var filter = monitor != null ? monitor.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) return new Bounds(Vector3.zero, Vector3.one * 0.1f);
            if (!mesh.isReadable || slot < 0 || slot >= mesh.subMeshCount) return mesh.bounds;
            var vertices = mesh.vertices;
            var triangles = mesh.GetTriangles(slot);
            if (triangles.Length == 0) return mesh.bounds;
            var b = new Bounds(vertices[triangles[0]], Vector3.zero);
            foreach (int i in triangles) b.Encapsulate(vertices[i]);
            return b;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>The toon material for one of the model's materials: its colour map, or dark plastic (feet, cables).</summary>
        static Material Textured(string modelMaterial)
        {
            Material m;
            if (materials.TryGetValue(modelMaterial, out m) && m != null) return m;
            Texture2D map = null;
            for (int i = 0; i < MapKeys.Length && map == null; i++)
                if (modelMaterial.Contains(MapKeys[i])) map = Resources.Load<Texture2D>(TexturePath + MapFiles[i]);
            if (map != null)
            {
                m = Toon(map);
                m.color = PlasticTint;   // the cream maps are near white; toned down so they don't glare
                m.name = modelMaterial;
            }
            else m = Plain(new Color(0.22f, 0.21f, 0.2f));
            materials[modelMaterial] = m;
            return m;
        }

        static Material Plain(Color color, bool unique = false)
        {
            Material m;
            if (!unique && plains.TryGetValue(color, out m) && m != null) return m;
            m = Toon(Texture2D.whiteTexture);
            m.color = color;
            if (!unique) plains[color] = m;
            return m;
        }

        static Material Toon(Texture texture)
        {
            var m = new Material(WorldArt.FindShader("Reverse Shell/WorldToon", "Standard")) { mainTexture = texture };
            if (m.HasProperty("_VertexShade")) m.SetFloat("_VertexShade", 0f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.01f);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            return m;
        }

        static GameObject Part(Transform root, Material mat, Vector3 at, Vector3 size, PrimitiveType type = PrimitiveType.Cube)
        {
            var go = WorldArt.Primitive(type, type.ToString(), root, mat);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            return go;
        }
    }
}
