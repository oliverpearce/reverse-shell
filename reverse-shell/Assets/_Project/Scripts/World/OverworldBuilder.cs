using System.Collections;
using System.Collections.Generic;
using Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.World
{
    /// <summary>
    /// Builds an HD-2D overworld at runtime from a text map: 3D blocks textured with pixel art, lit by
    /// neon lamps, with billboard pixel characters. Edit the map in the inspector to reshape the level.
    /// Handles interaction and the overworld -> battle -> overworld round trip.
    /// </summary>
    public class OverworldBuilder : MonoBehaviour
    {
        public const string DefaultMap =
            "HHHHHH####HHHHH#BBBBBBBBBBBB\n" +
            "HHHHHH####HHHHH#BPPPPPPPPPPB\n" +
            "HHHHHH####HHHHH#BPPPPPEPPPPB\n" +
            "HHHHHH####HHHHH#BPPPPPXPPPPB\n" +
            "HHHHHH####HHHHH#BPPPPPPPPPPB\n" +
            "HHHHHH####HHHHH#BLLLLP.PLLLB\n" +
            "B.m......c..n.m.....g.....mB\n" +
            "B==========================B\n" +
            "B==========================B\n" +
            "B.c....n..m.......c.....m..B\n" +
            "B.S...................n....B\n" +
            "LLLLLLLLLLLLLLLLLLLLLLLLLLLL\n";

        [Header("Area")]
        public string areaName = "RAINLINE STATION";
        public string areaSubtitle = "Lowmarrow Undercity";
        [TextArea(12, 30)] public string map = DefaultMap;

        [Header("Art (CyberCity, CC0)")]
        [Tooltip("city.png: 8x5 atlas of 64px cells.")]
        public Texture2D cityAtlas;
        [Tooltip("Player walk sheet (3x4). Defaults to the party leader's sprite sheet from the encounter setup.")]
        public Texture2D playerSheet;
        [Tooltip("Yakuza1.png: 3x4 walk sheet, used for the encounter enemy.")]
        public Texture2D enemySheet;
        [Tooltip("streetPeople.png: 3x4 grid of individual NPCs.")]
        public Texture2D npcSheet;
        public float characterScale = 1.5f;

        [Header("Animated sprites (LPC, 32x48 bodies in 64px frames)")]
        [Tooltip("NPCs in map order (cycles if there are more 'n' cells). Overrides npcSheet.")]
        public List<SpriteAnimationSet> npcAnimations = new List<SpriteAnimationSet>();
        [Tooltip("Overworld look of encounter enemies. Overrides enemySheet.")]
        public SpriteAnimationSet encounterAnimations;
        [Tooltip("Scale for animated sprites: 2 makes a 48px-tall body about 1.5 tiles tall.")]
        public float animatedCharacterScale = 2f;

        [Header("Battle")]
        public BattleSetup encounterSetup;
        public string battleScene = "Battle";

        [Header("Input")]
        public KeyCode interactKey = KeyCode.F;

        public OverworldUI UI { get; private set; }

        string LeaderName
        {
            get
            {
                var lead = encounterSetup != null && encounterSetup.party.Count > 0 ? encounterSetup.party[0] : null;
                return lead != null ? lead.DisplayName : "You";
            }
        }
        public OverworldMap Map { get; private set; }

        PlayerController player;
        Camera cam;
        readonly List<Interactable> interactables = new List<Interactable>();
        Interactable focused;
        bool transitioning;
        Transform rain;

        void Start()
        {
            Map = new OverworldMap(map);
            cam = Camera.main;
            ApplyAtmosphere();

            foreach (var cell in Map.FindAll('E'))
                if (WorldSession.DefeatedEncounters.Contains(EncounterId(cell))) ClearEncounter(cell);

            BuildGeometry();
            BuildProps();
            SpawnPlayer();
            SpawnNpcs();
            SpawnEncounters();

            rain = WorldFx.Rain(transform).transform;
            UI = OverworldUI.Create(transform);
            StartCoroutine(Arrive());
        }

        // ------------------------------------------------------------------ Build

        void ApplyAtmosphere()
        {
            var night = new Color(0.05f, 0.035f, 0.09f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.17f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = night;
            RenderSettings.fogDensity = 0.03f;
            // Every neon lamp is a real point light; let them all light per-pixel.
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 16);

            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = night;
                cam.allowHDR = true;
            }

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(transform, false);
            moon.type = LightType.Directional;
            moon.color = new Color(0.45f, 0.5f, 0.85f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.7f;
            moon.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
        }

        void BuildGeometry()
        {
            if (cityAtlas == null)
            {
                Debug.LogError("OverworldBuilder: assign the CyberCity atlas (city.png).", this);
                return;
            }
            var signs = new List<SignSpot>();
            var go = new GameObject("WorldMesh");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = WorldMeshBuilder.Build(Map, cityAtlas, signs);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { WorldArt.WorldMaterial(cityAtlas, 0.55f), WorldArt.WorldMaterial(cityAtlas, 0.12f) };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            foreach (var sign in signs) WorldFx.Sign(transform, sign);
        }

        void BuildProps()
        {
            AddLamps('m', WorldArt.NeonMagenta);
            AddLamps('c', WorldArt.NeonCyan);
            AddLamps('g', WorldArt.NeonGreen);
            AddLamps('o', WorldArt.NeonOrange);
        }

        void AddLamps(char c, Color color)
        {
            foreach (var cell in Map.FindAll(c))
            {
                var pos = Map.CellCenter(cell.x, cell.y);
                WorldFx.Lamp(transform, pos, color);
                if (OverworldMap.Hash(cell.x, cell.y, 11) % 2 == 0)
                    WorldFx.Glimmer(transform, pos + Vector3.up * 1.6f, 0.9f, 3f);
            }
        }

        void SpawnPlayer()
        {
            var spawns = Map.FindAll('S');
            var start = spawns.Count > 0 ? Map.CellCenter(spawns[0].x, spawns[0].y) : Map.CellCenter(1, Map.Height - 2);
            if (WorldSession.HasReturnPoint) start = WorldSession.ReturnPosition;

            var lead = encounterSetup != null && encounterSetup.party.Count > 0 ? encounterSetup.party[0] : null;
            CharacterSprite sprite;
            if (playerSheet == null && lead != null && lead.animations != null)
            {
                sprite = CharacterSprite.Create(transform, LeaderName, lead.animations, animatedCharacterScale, start);
            }
            else
            {
                var sheet = playerSheet != null ? playerSheet : (lead != null ? lead.spriteSheet : null);
                var frames = sheet != null ? WorldArt.SliceSheet(sheet, 3, 4) : null;
                sprite = CharacterSprite.Create(transform, LeaderName, frames ?? PlaceholderFrames(), characterScale, start);
            }
            sprite.SetFacing(WorldSession.HasReturnPoint ? WorldSession.ReturnFacing : CharacterSprite.Down);

            player = sprite.gameObject.AddComponent<PlayerController>();
            player.interactKey = interactKey;
            player.Init(this, Map, sprite);

            if (cam != null)
            {
                var follow = cam.GetComponent<OverworldCamera>();
                if (follow == null) follow = cam.gameObject.AddComponent<OverworldCamera>();
                follow.target = player.transform;
                follow.Snap();
                var focus = cam.GetComponent<PostFxFocus>();
                if (focus != null) focus.target = player.transform;
            }
        }

        void SpawnNpcs()
        {
            var cells = Map.FindAll('n');
            bool animated = npcAnimations.Exists(a => a != null);
            if ((npcSheet == null && !animated) || cells.Count == 0) return;
            var sheet = npcSheet != null ? WorldArt.SliceSheet(npcSheet, 3, 4) : null;

            // (sheet row, col), name, lines. Add more entries for more NPCs; they cycle if the map has extras.
            var cast = new[]
            {
                new NpcInfo(1, 0, "Noodle Vendor", new[]
                {
                    "Rain's thick tonight. Glimmer's been pooling in the gutters again - see it sparkle?",
                    "Careful by the plaza up north. A Static-touched thug's been shaking people down.",
                    "If you're gonna fight him, stand on the glowing square. Hexers always do."
                }),
                new NpcInfo(1, 1, "Courier", new[]
                {
                    "You're " + LeaderName + ", right? The singing Hexer? Your voice crackled through every speaker on Neon Row.",
                    "Teto and Rei are waiting for you. A drill-haired brawler and an android medic - some crew you've got."
                }),
                new NpcInfo(2, 2, "Drifter", new[] { "The Spires never see the rain. Must be nice." }),
            };

            for (int i = 0; i < cells.Count; i++)
            {
                var info = cast[i % cast.Length];
                var pos = Map.CellCenter(cells[i].x, cells[i].y);
                var look = animated ? npcAnimations[i % npcAnimations.Count] : null;
                var npc = look != null
                    ? CharacterSprite.Create(transform, info.Name, look, animatedCharacterScale, pos)
                    : CharacterSprite.Create(transform, info.Name, WorldArt.SingleFrame(sheet, info.Row, info.Col), characterScale, pos, 0.04f);
                var talk = npc.gameObject.AddComponent<TalkInteractable>();
                talk.speaker = info.Name;
                talk.lines = info.Lines;
                interactables.Add(talk);
            }
        }

        void SpawnEncounters()
        {
            var frames = enemySheet != null ? WorldArt.SliceSheet(enemySheet, 3, 4) : PlaceholderFrames();
            foreach (var cell in Map.FindAll('E'))
            {
                var pos = Map.CellCenter(cell.x, cell.y);
                var enemy = encounterAnimations != null
                    ? CharacterSprite.Create(transform, "Static Thug", encounterAnimations, animatedCharacterScale, pos)
                    : CharacterSprite.Create(transform, "Static Thug", frames, characterScale, pos, 0.03f);
                enemy.SetFacing(CharacterSprite.Down);

                var light = new GameObject("ThreatLight").AddComponent<Light>();
                light.transform.SetParent(enemy.transform, false);
                light.transform.localPosition = new Vector3(0f, 1.2f, -0.6f);
                light.type = LightType.Point;
                light.color = WorldArt.NeonMagenta;
                light.range = 3.5f;
                light.intensity = 2f;
                WorldFx.Glimmer(enemy.transform, pos + Vector3.up * 0.9f, 0.6f, 10f);

                var encounter = enemy.gameObject.AddComponent<EncounterInteractable>();
                encounter.id = EncounterId(cell);
                encounter.setup = encounterSetup;
                encounter.standPoint = StandPointFor(cell);
                interactables.Add(encounter);

                WorldFx.FloorMarker(transform, encounter.standPoint, WorldArt.NeonMagenta);
            }
        }

        /// <summary>The 'X' square next to an encounter, or the square south of it.</summary>
        Vector3 StandPointFor(Vector2Int enemyCell)
        {
            foreach (var d in new[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) })
            {
                var c = enemyCell + d;
                if (Map.At(c.x, c.y) == 'X') return Map.CellCenter(c.x, c.y);
            }
            return Map.CellCenter(enemyCell.x, enemyCell.y + 1);
        }

        void ClearEncounter(Vector2Int cell)
        {
            Map.Set(cell.x, cell.y, 'P');
            foreach (var d in new[] { new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0) })
                if (Map.At(cell.x + d.x, cell.y + d.y) == 'X') Map.Set(cell.x + d.x, cell.y + d.y, 'P');
        }

        string EncounterId(Vector2Int cell)
        {
            return SceneManager.GetActiveScene().name + "/encounter_" + cell.x + "_" + cell.y;
        }

        static Sprite[,] PlaceholderFrames()
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.magenta);
            tex.Apply();
            return new Sprite[1, 1] { { Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0f), 1f) } };
        }

        // ------------------------------------------------------------------ Runtime

        void Update()
        {
            // Scripts recompiled during Play mode wipe the in-memory map; reload the area instead of breaking.
            if (Map == null)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }
            if (player == null || UI == null) return;
            if (rain != null) rain.position = player.transform.position + new Vector3(0f, 9f, 3f);

            focused = null;
            if (!transitioning)
            {
                float best = float.MaxValue;
                foreach (var it in interactables)
                {
                    if (it == null) continue;
                    float score = it.Score(player.transform.position);
                    if (score >= 0f && score < best)
                    {
                        best = score;
                        focused = it;
                    }
                }
            }

            if (focused != null) UI.ShowPrompt(interactKey.ToString(), focused.Prompt, focused.PromptAnchor, cam);
            else UI.HidePrompt();
        }

        public void OnInteractPressed()
        {
            if (transitioning) return;
            if (focused != null) focused.Interact(this);
            else if (UI.MessageVisible) UI.HideMessage();
        }

        public void StartEncounter(EncounterInteractable encounter)
        {
            if (transitioning) return;
            if (encounter.setup == null)
            {
                UI.ShowMessage("System", "No BattleSetup assigned to this encounter.");
                return;
            }
            StartCoroutine(BattleTransition(encounter));
        }

        IEnumerator BattleTransition(EncounterInteractable encounter)
        {
            transitioning = true;
            player.Frozen = true;
            UI.HidePrompt();
            UI.HideMessage();

            WorldSession.HasReturnPoint = true;
            WorldSession.ReturnPosition = player.transform.position;
            WorldSession.ReturnFacing = player.Sprite.Facing;

            string returnScene = SceneManager.GetActiveScene().name;
            string id = encounter.id;
            string enemyName = encounter.displayName;
            string leader = LeaderName;
            BattleLauncher.Request(encounter.setup, outcome =>
            {
                WorldSession.PendingSpeaker = leader;
                switch (outcome)
                {
                    case BattleOutcome.Victory:
                        WorldSession.DefeatedEncounters.Add(id);
                        WorldSession.PendingMessage = "The " + enemyName + " dissolves into static. The plaza goes quiet - just rain and Glimmer.";
                        break;
                    case BattleOutcome.Fled:
                        WorldSession.PendingMessage = "We slipped away. He's still out there... let's come back ready.";
                        break;
                    default:
                        WorldSession.PendingMessage = "...We need to regroup. That thing hits harder than it looks.";
                        break;
                }
                SceneManager.LoadScene(returnScene);
            });

            // Neon flash, then cut to black.
            UI.SetFade(new Color(1f, 0.2f, 0.6f, 0.8f));
            yield return UI.FadeTo(Color.black, 0.45f);
            SceneManager.LoadScene(battleScene);
        }

        IEnumerator Arrive()
        {
            bool returning = WorldSession.HasReturnPoint;
            UI.SetFade(Color.black);
            player.Frozen = true;
            yield return UI.FadeTo(new Color(0f, 0f, 0f, 0f), 0.6f);
            player.Frozen = false;

            if (!string.IsNullOrEmpty(WorldSession.PendingMessage))
            {
                UI.ShowMessage(WorldSession.PendingSpeaker ?? "", WorldSession.PendingMessage);
                WorldSession.PendingMessage = null;
            }
            if (!returning) yield return UI.ShowTitle(areaName, areaSubtitle);
        }

        struct NpcInfo
        {
            public readonly int Row, Col;
            public readonly string Name;
            public readonly string[] Lines;

            public NpcInfo(int row, int col, string name, string[] lines)
            {
                Row = row;
                Col = col;
                Name = name;
                Lines = lines;
            }
        }
    }
}
