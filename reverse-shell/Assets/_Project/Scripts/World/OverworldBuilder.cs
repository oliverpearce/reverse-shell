using System.Collections;
using System.Collections.Generic;
using Combat;
using Combat.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.World
{
    /// <summary>
    /// Builds an HD-2D overworld at runtime from a text map: 3D blocks textured with pixel art, a sunlit
    /// city ruin reclaimed by trees, grass and vines, with billboard pixel characters. Edit the map in the inspector to reshape the level.
    /// Handles interaction and the overworld -> battle -> overworld round trip.
    /// </summary>
    public class OverworldBuilder : MonoBehaviour
    {
        /// <summary>The Training Center map (the original Mossline Station test level). Areas live in
        /// <see cref="WorldAreas"/>; with useDefaultMap on (the default) the current area's map is built.</summary>
        public const string DefaultMap = WorldAreas.TrainingMap;

        [Header("Area")]
        [Tooltip("Shown on arrival; replaced at runtime by the current area's name (WorldAreas).")]
        public string areaName = "MOSSLINE STATION";
        public string areaSubtitle = "The City Ruins";
        [Tooltip("On: build the current area's map from code (WorldAreas), so map updates show up without regenerating the scene. " +
                 "Turn off to use the custom map below.")]
        public bool useDefaultMap = true;
        [TextArea(12, 30)] public string map = DefaultMap;

        [Header("Art")]
        [Tooltip("ruins.png (Tools/TileForge/ruins_atlas.py): 8x5 atlas of 64px cells.")]
        [UnityEngine.Serialization.FormerlySerializedAs("cityAtlas")]
        public Texture2D tileAtlas;
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
        [Tooltip("Scale for animated sprites (64 px frames): 2.5 makes the ~38 px knight-based bodies about 1.5 tiles tall.")]
        public float animatedCharacterScale = 2.5f;

        [Header("Battle")]
        public BattleSetup encounterSetup;
        public string battleScene = "Battle";
        [Tooltip("Every Part in the game (Content/Parts/PartCatalog.asset), for the crew menu and the save file.")]
        public PartCatalog partCatalog;

        [Header("Input")]
        public KeyCode interactKey = KeyCode.F;

        public OverworldUI UI { get; private set; }

        string LeaderName
        {
            get
            {
                var lead = Lead;
                return lead != null ? lead.DisplayName : "You";
            }
        }
        public OverworldMap Map { get; private set; }

        /// <summary>The formation: the active members in slot order (the first leads and is who you walk as), then the
        /// backup. The party HUD shows these.</summary>
        public List<CharacterDefinition> Party
        {
            get
            {
                if (encounterSetup == null) return new List<CharacterDefinition>();
                var list = WorldSession.FormationActive(encounterSetup.party);
                var backup = WorldSession.FormationBackup(encounterSetup.party);
                if (backup != null) list.Add(backup);
                return list;
            }
        }

        /// <summary>Everyone who has joined (the Units screen, the formation pick list).</summary>
        public List<CharacterDefinition> Roster
        {
            get { return encounterSetup != null ? WorldSession.Joined(encounterSetup.party) : new List<CharacterDefinition>(); }
        }

        CharacterDefinition Lead
        {
            get
            {
                var party = Party;
                return party.Count > 0 ? party[0] : null;
            }
        }

        /// <summary>Each 'E' cell's fight definition (from the area, in reading order).</summary>
        readonly Dictionary<Vector2Int, WorldAreas.EncounterDef> encounterDefs = new Dictionary<Vector2Int, WorldAreas.EncounterDef>();
        Vector2Int playerCell = new Vector2Int(-1, -1);

        CharacterMenu menu;
        FormationScreen formation;
        PauseCards pauseCards;
        ResonanceScreen resonance;
        ItemsScreen items;
        EnemyLevelPanel chestPanel;
        ScannerHud scannerHud;

        /// <summary>Seconds between scans (Q), so the pulse can't be spammed.</summary>
        const float ScanCooldown = 4f;
        TitleScreen title;
        WorldAreas.Area area;

        /// <summary>Overworld name of the encounter: the first enemy in its battle setup.</summary>
        string EncounterName
        {
            get
            {
                var first = encounterSetup != null && encounterSetup.enemies.Count > 0 ? encounterSetup.enemies[0] : null;
                return first != null ? first.DisplayName : "Machine";
            }
        }

        PlayerController player;
        Camera cam;
        readonly List<Interactable> interactables = new List<Interactable>();
        Interactable focused;
        bool transitioning;
        /// <summary>Set by fades and transitions; the player is also frozen while a conversation is open.</summary>
        bool cutscene;
        Transform drift;

        void Start()
        {
            WorldSession.Catalog = partCatalog;
#if UNITY_EDITOR
            if (WorldSession.Catalog == null) // scenes generated before Parts existed
                WorldSession.Catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<PartCatalog>("Assets/_Project/Content/Parts/PartCatalog.asset");
#endif
            WorldSession.EnsureLoaded(); // first area of the session reads save.json
            WorldSession.GiveStarterParts(); // a new game (or an older save) gets the starter Parts once
            if (StoryState.Has(Chapter1.ResonanceVolt)) WorldSession.BindSoulCard(); // saves from before the Resonant Core
            area = WorldAreas.Get(WorldSession.CurrentArea);
            areaName = area.Name;
            areaSubtitle = area.Subtitle;
            Map = new OverworldMap(useDefaultMap || string.IsNullOrEmpty(map) ? area.Map : map);
            cam = Camera.main;
            // Nothing tall just south of a walkable square, where it would hide the player (OverworldMap.ApplyCameraRule).
            var follow = cam != null ? cam.GetComponent<OverworldCamera>() : null;
            Map.ApplyCameraRule(follow != null ? follow.pitch : 38f);
            // The Training Center brings the whole crew in (a new game is just MC until then).
            if (area.Id == WorldAreas.Training && encounterSetup != null)
            {
                var joined = WorldSession.RecruitAll(encounterSetup.party);
                if (joined.Count > 0)
                {
                    SaveGame.Save();
                    string key = joined.Count == 1 ? "training.joins" : "training.joins.many";
                    WorldSession.PendingSpeaker = StoryText.Speaker(key);
                    WorldSession.PendingMessage = StoryText.Text(key, "who", string.Join(" and ", joined.ToArray()));
                }
            }
            ApplyAtmosphere();

            // Each 'E' gets the area's fight in reading order; beaten ones (and story fights whose flags say so) are gone.
            var fights = Map.FindAll('E');
            for (int i = 0; i < fights.Count; i++)
            {
                var def = i < area.Encounters.Count ? area.Encounters[i] : null;
                encounterDefs[fights[i]] = def;
                bool absent = def != null && !StoryState.Allows(def.Requires, def.Blocks);
                if (absent || WorldSession.DefeatedEncounters.Contains(EncounterId(fights[i]))) ClearEncounter(fights[i]);
            }

            BuildGeometry();
            BuildProps();
            SpawnPlayer();
            SpawnNpcs();
            SpawnEncounters();
            SpawnAccessPoints();
            SpawnSummonBeacons();
            SpawnWarps();
            foreach (var cell in Map.FindAll('C'))
                interactables.Add(WorldFx.Chest(transform, Map.CellCenter(cell.x, cell.y)).AddComponent<ChestInteractable>());
            SpawnLootChests();

            drift = WorldFx.Drift(transform).transform;
            UI = OverworldUI.Create(transform);
            menu = CharacterMenu.Create((RectTransform)UI.transform, this);
            formation = FormationScreen.Create((RectTransform)UI.transform, this);
            resonance = ResonanceScreen.Create((RectTransform)UI.transform, this);
            items = ItemsScreen.Create((RectTransform)UI.transform, this);
            pauseCards = PauseCards.Create((RectTransform)UI.transform, this, menu, formation, resonance, items);
            chestPanel = EnemyLevelPanel.Create((RectTransform)UI.transform);
            scannerHud = ScannerHud.Create((RectTransform)UI.transform);
            scannerHud.transform.SetSiblingIndex(0);   // under every menu
            // The first area of the session opens under the title screen (New Game / Continue / Load ...).
            if (!WorldSession.TitleDone) title = TitleScreen.Create((RectTransform)UI.transform, Party);
            RefreshPartyHud();
            ChapterOneDirector.OnAreaBuilt(this);
            StoryState.Changed += OnStoryChanged;
            RefreshObjective();
            StoryDebugPanel.Ensure();
            StartCoroutine(Arrive());
        }

        void OnDestroy()
        {
            StoryState.Changed -= OnStoryChanged;
        }

        void OnStoryChanged(string flag)
        {
            RefreshObjective();
        }

        /// <summary>The objective on the HUD (none in the Training Center).</summary>
        public void RefreshObjective()
        {
            if (UI != null) UI.SetObjective(area != null && area.Id == WorldAreas.Training ? "" : Chapter1.CurrentObjective());
        }

        // ------------------------------------------------------------------ Story hooks (ChapterOneDirector)

        public WorldAreas.Area Area { get { return area; } }
        public Camera Cam { get { return cam; } }
        public Vector3 PlayerPosition { get { return player != null ? player.transform.position : Vector3.zero; } }

        /// <summary>The player's sprite (the Esc menu sits it down on its mat).</summary>
        public CharacterSprite PlayerSprite { get { return player != null ? player.Sprite : null; } }

        /// <summary>True while anything (a conversation, a menu, a fade) has the player's attention.</summary>
        public bool Busy
        {
            get
            {
                return cutscene || transitioning || UI == null || UI.DialogueOpen || (menu != null && menu.IsOpen) || (title != null && title.IsOpen)
                       || (pauseCards != null && pauseCards.IsOpen) || (formation != null && formation.IsOpen) || (chestPanel != null && chestPanel.IsOpen)
                       || (resonance != null && resonance.IsOpen) || (items != null && items.IsOpen);
            }
        }

        /// <summary>Freezes / frees the player for a scripted moment.</summary>
        public void SetCutscene(bool on) { cutscene = on; if (on && UI != null) UI.HidePrompt(); }

        public void AddInteractable(Interactable it) { if (it != null) interactables.Add(it); }

        /// <summary>The first map fight matching <paramref name="match"/> still standing (null if none).</summary>
        public EncounterInteractable FindEncounter(System.Predicate<EncounterInteractable> match)
        {
            foreach (var it in interactables)
            {
                var e = it as EncounterInteractable;
                if (e != null && match(e)) return e;
            }
            return null;
        }

        public void RemoveInteractable(Interactable it) { interactables.Remove(it); }

        /// <summary>Opens a warp at a cell at runtime (the city's hidden route): walkable, a pad, travels on interact.</summary>
        public void OpenWarp(Vector2Int cell, string destination)
        {
            Map.Set(cell.x, cell.y, 'w');
            SpawnWarpPad(cell, destination);
        }

        /// <summary>A scripted fight with no map machine (the Collector): the pre-fight screen, then the battle.</summary>
        public void StartStoryFight(WorldAreas.EncounterDef def, string id)
        {
            var go = new GameObject("StoryFight_" + id);
            go.transform.SetParent(transform, false);
            go.transform.position = PlayerPosition;
            var encounter = go.AddComponent<EncounterInteractable>();
            encounter.def = def;
            encounter.id = SceneManager.GetActiveScene().name + "/" + area.Id + "/" + id;
            encounter.displayName = DisplayNameFor(def);
            encounter.setup = encounterSetup;
            StartEncounter(encounter);
        }

        static string DisplayNameFor(WorldAreas.EncounterDef def)
        {
            var catalog = EnemyCatalog.Get();
            var first = def != null && def.Enemies != null && def.Enemies.Length > 0 && catalog != null ? catalog.Find(def.Enemies[0]) : null;
            return first != null ? first.DisplayName : "Machine";
        }

        // ------------------------------------------------------------------ Build

        void ApplyAtmosphere()
        {
            if (area != null && area.Dim)
            {
                ApplyUnderground();
                return;
            }
            // A dim ruin under a leafy canopy: the only light is the sun, broken into dappled spots by the
            // leaves overhead (a cookie on the sun), over a low, cool green ambient and haze. No point lights.
            var haze = new Color(0.36f, 0.41f, 0.38f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.34f, 0.4f, 0.44f);
            RenderSettings.ambientEquatorColor = new Color(0.27f, 0.3f, 0.25f);
            RenderSettings.ambientGroundColor = new Color(0.13f, 0.14f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = haze;
            RenderSettings.fogDensity = 0.028f;

            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = haze;
                cam.allowHDR = true;
            }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.color = WorldArt.Sunlight;
            sun.intensity = 1.9f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            sun.transform.rotation = Quaternion.Euler(58f, -30f, 0f);
            sun.cookie = WorldArt.DappleCookie();
            sun.cookieSize = 9f;
            sun.gameObject.AddComponent<CanopySway>();
        }

        /// <summary>Underground (the tunnel, the chamber): a weak shaft of daylight through the cracks, a cold teal
        /// ambient and a dark haze. Still no point lights; the glow comes from the story props and the guardian.</summary>
        void ApplyUnderground()
        {
            var haze = new Color(0.07f, 0.1f, 0.12f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.22f, 0.26f);
            RenderSettings.ambientEquatorColor = new Color(0.1f, 0.14f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.06f, 0.07f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = haze;
            RenderSettings.fogDensity = 0.045f;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = haze;
                cam.allowHDR = true;
            }
            var shaft = new GameObject("Daylight").AddComponent<Light>();
            shaft.transform.SetParent(transform, false);
            shaft.type = LightType.Directional;
            shaft.color = new Color(0.75f, 0.85f, 0.95f);
            shaft.intensity = 0.7f;
            shaft.shadows = LightShadows.Soft;
            shaft.transform.rotation = Quaternion.Euler(64f, -20f, 0f);
            shaft.cookie = WorldArt.DappleCookie();
            shaft.cookieSize = 14f;
        }

        void BuildGeometry()
        {
            if (tileAtlas == null)
            {
                Debug.LogError("OverworldBuilder: assign the ruins tile atlas (Art/Tiles/ruins.png).", this);
                return;
            }
            var vines = new List<DecalSpot>();
            var go = new GameObject("WorldMesh");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = WorldMeshBuilder.Build(Map, tileAtlas, vines);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { WorldArt.WorldMaterial(tileAtlas, 0.08f), WorldArt.WorldMaterial(tileAtlas, 0.05f) };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            foreach (var vine in vines) WorldFx.Vine(transform, tileAtlas, vine);
        }

        void BuildProps()
        {
            if (tileAtlas == null) return;
            for (int row = 0; row < Map.Height; row++)
            {
                for (int x = 0; x < Map.Width; x++)
                {
                    char c = Map.At(x, row);
                    var pos = Map.CellCenter(x, row);
                    int hash = OverworldMap.Hash(x, row, 21);
                    switch (c)
                    {
                        case 'T': WorldFx.Tree(transform, tileAtlas, pos, hash); break;
                        case 'b': WorldFx.Bush(transform, tileAtlas, pos, hash); break;
                        case 'r': WorldFx.Rubble(transform, tileAtlas, pos, hash); break;
                        case ',':
                        case '*':
                            // A small clump per cell, offset so the rows don't look planted.
                            for (int i = 0; i < 3; i++)
                            {
                                var offset = new Vector3((OverworldMap.Hash(x, row, i) % 100) / 100f - 0.5f, 0f,
                                    (OverworldMap.Hash(x, row, i + 7) % 100) / 100f - 0.5f) * 0.8f;
                                WorldFx.Tuft(transform, tileAtlas, pos + offset, c == '*' && i == 0, 0.7f + (hash >> i) % 3 * 0.12f);
                            }
                            break;
                        case '.':
                        case '=':
                        case 'P':
                            // Stray weeds poking through the pavement.
                            if (hash % 6 == 0) WorldFx.Tuft(transform, tileAtlas, pos + new Vector3(0.25f, 0f, -0.2f), false, 0.5f);
                            break;
                    }
                }
            }

            // Dust hanging in the air near some of the trees.
            foreach (var cell in Map.FindAll('T'))
            {
                if (OverworldMap.Hash(cell.x, cell.y, 3) % 2 != 0) continue;
                var pos = Map.CellCenter(cell.x, cell.y) + new Vector3(1f, 0f, -0.5f);
                WorldFx.Motes(transform, pos + Vector3.up * 1.5f, 1f, 3f);
            }
        }

        void SpawnPlayer()
        {
            var spawns = Map.FindAll('S');
            var start = spawns.Count > 0 ? Map.CellCenter(spawns[0].x, spawns[0].y) : Map.CellCenter(1, Map.Height - 2);
            if (!string.IsNullOrEmpty(WorldSession.ArrivedFrom))
            {
                // Arriving through a warp: stand beside the warp here that leads back where you came from.
                foreach (var warp in area.Warps)
                {
                    if (warp.Value != WorldSession.ArrivedFrom) continue;
                    var cells = Map.FindAll(warp.Key);
                    if (cells.Count == 0 && warp.Key == 'V') cells = Map.FindAll('w');
                    if (cells.Count > 0) { start = BesideCell(cells[0]); break; }
                }
                WorldSession.ArrivedFrom = null;
            }
            else if (WorldSession.HasReturnPoint)
            {
                // Only a spot on this map (a stale one from another area would drop you outside the world).
                int rx, rrow;
                Map.WorldToCell(WorldSession.ReturnPosition, out rx, out rrow);
                if (rx >= 0 && rrow >= 0 && rx < Map.Width && rrow < Map.Height && !Map.IsSolidCell(rx, rrow))
                    start = WorldSession.ReturnPosition;
                else Debug.LogWarning("OverworldBuilder: the return spot isn't on this map; starting at the area's start.");
            }
            SpawnPlayerAt(start, WorldSession.HasReturnPoint ? WorldSession.ReturnFacing : CharacterSprite.Down);
        }

        /// <summary>The first walkable square next to <paramref name="cell"/> (south first), or the cell itself.</summary>
        Vector3 BesideCell(Vector2Int cell)
        {
            foreach (var d in new[] { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, -1) })
                if (!Map.IsSolidCell(cell.x + d.x, cell.y + d.y)) return Map.CellCenter(cell.x + d.x, cell.y + d.y);
            return Map.CellCenter(cell.x, cell.y);
        }

        /// <summary>The Training Center chest: set the practice fights' enemy level and boss mode.</summary>
        public void OpenTrainingChest()
        {
            UI.HidePrompt();
            chestPanel.Open();
        }

        /// <summary>Warp tiles (WorldAreas.Area.Warps): a glowing pad each; interact on it to travel. The city's hidden
        /// route ('V') stays a cracked wall until the scanner opens it (ChapterOneDirector).</summary>
        void SpawnWarps()
        {
            foreach (var warp in area.Warps)
            {
                foreach (var cell in Map.FindAll(warp.Key))
                {
                    if (warp.Key == 'V')
                    {
                        if (StoryState.Has(Chapter1.Route)) OpenWarp(cell, warp.Value);
                        continue;
                    }
                    SpawnWarpPad(cell, warp.Value);
                }
            }
        }

        void SpawnWarpPad(Vector2Int cell, string destination)
        {
            var pad = WorldFx.WarpPad(transform, Map.CellCenter(cell.x, cell.y));
            var warp = pad.AddComponent<WarpInteractable>();
            warp.destination = destination;
            interactables.Add(warp);
        }

        /// <summary>Warp: save, then reload the scene as the destination area (you arrive beside its warp).</summary>
        public void TravelTo(string destination)
        {
            if (!BeginTransition()) return;
            WorldSession.ArrivedFrom = area.Id;
            WorldSession.CurrentArea = destination;
            WorldSession.HasReturnPoint = false;
            Reload();
        }

        /// <summary>Settings → Enter Training Mode: remember this spot, then go to the Training Center's start.</summary>
        public void EnterTraining()
        {
            if (WorldSession.InTraining || player == null || !BeginTransition()) return;
            WorldSession.HasTrainingReturn = true;
            WorldSession.TrainingReturnArea = area.Id;
            WorldSession.TrainingReturnPosition = player.transform.position;
            WorldSession.TrainingReturnFacing = player.Sprite.Facing;
            WorldSession.CurrentArea = WorldAreas.Training;
            WorldSession.ArrivedFrom = null;
            WorldSession.HasReturnPoint = false;
            Reload();
        }

        /// <summary>Settings → Leave Training Mode: back to the spot you entered from (the city's start if unknown).</summary>
        public void LeaveTraining()
        {
            if (!WorldSession.InTraining || !BeginTransition()) return;
            WorldSession.CurrentArea = WorldSession.HasTrainingReturn ? WorldSession.TrainingReturnArea : WorldAreas.City;
            WorldSession.ArrivedFrom = null;
            WorldSession.HasReturnPoint = WorldSession.HasTrainingReturn;
            WorldSession.ReturnPosition = WorldSession.TrainingReturnPosition;
            WorldSession.ReturnFacing = WorldSession.TrainingReturnFacing;
            WorldSession.HasTrainingReturn = false;
            Reload();
        }

        bool BeginTransition()
        {
            if (transitioning) return false;
            transitioning = true;
            WorldSession.PlayerSpot = null;   // travelling: the new area places you
            cutscene = true;
            UI.HidePrompt();
            return true;
        }

        void Reload()
        {
            SaveGame.Save();
            UI.SetFade(new Color(0.45f, 0.85f, 1f, 0.6f));
            ScreenFade.LoadScene(SceneManager.GetActiveScene().name);
        }

        void SpawnPlayerAt(Vector3 start, int facing)
        {
            var lead = Lead;
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
            sprite.SetFacing(facing);

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

            // (sheet row, col), voice, and the script block with their name and lines (Resources/Story/World.txt).
            // Add more entries for more NPCs; they cycle if the map has extras.
            var cast = new[]
            {
                // Gruff, low and unhurried.
                Npc(1, 0, "training.trader", new DialogueVoice(0.72f, 0.85f, new Color(0.72f, 0.45f, 0.24f))),
                // Young, quick and a little starstruck.
                Npc(1, 1, "training.scout", new DialogueVoice(1.35f, 1.5f, new Color(0.3f, 0.65f, 1f))),
                // Dreamy and slow, with long pauses.
                Npc(2, 2, "training.wanderer", new DialogueVoice(1.05f, 0.6f, new Color(0.5f, 0.75f, 0.45f))),
            };

            // An area can bring its own people (WorldAreas.Area.Npcs); otherwise the old Mossline cast.
            if (area.Npcs.Count > 0)
            {
                cast = new NpcInfo[area.Npcs.Count];
                for (int i = 0; i < area.Npcs.Count; i++)
                    cast[i] = new NpcInfo(0, 0, area.Npcs[i].Name, new DialogueVoice(1.2f, 0.75f, new Color(1f, 0.6f, 0.4f)),
                        area.Npcs[i].LinesKey != null ? StoryText.Texts(area.Npcs[i].LinesKey) : area.Npcs[i].Lines);
            }

            for (int i = 0; i < cells.Count; i++)
            {
                var info = cast[i % cast.Length];
                var pos = Map.CellCenter(cells[i].x, cells[i].y);
                var look = animated ? npcAnimations[i % npcAnimations.Count] : null;
                if (i < area.Npcs.Count && area.Npcs[i].Machine && encounterAnimations != null) look = encounterAnimations;
                var npc = look != null
                    ? CharacterSprite.Create(transform, info.Name, look, animatedCharacterScale, pos)
                    : CharacterSprite.Create(transform, info.Name, WorldArt.SingleFrame(sheet, info.Row, info.Col), characterScale, pos, 0.04f);
                var talk = npc.gameObject.AddComponent<TalkInteractable>();
                talk.speaker = info.Name;
                talk.lines = info.Lines;
                talk.voice = info.Voice;
                talk.portrait = Portrait(look);
                interactables.Add(talk);
            }
        }

        void SpawnEncounters()
        {
            var frames = enemySheet != null ? WorldArt.SliceSheet(enemySheet, 3, 4) : PlaceholderFrames();
            foreach (var cell in Map.FindAll('E'))
            {
                var pos = Map.CellCenter(cell.x, cell.y);
                WorldAreas.EncounterDef def;
                encounterDefs.TryGetValue(cell, out def);
                string label = def != null ? DisplayNameFor(def) : EncounterName;
                var enemy = encounterAnimations != null
                    ? CharacterSprite.Create(transform, label, encounterAnimations, animatedCharacterScale, pos)
                    : CharacterSprite.Create(transform, label, frames, characterScale, pos, 0.03f);
                enemy.SetFacing(CharacterSprite.Down);

                WorldFx.Motes(enemy.transform, pos + Vector3.up * 0.9f, 0.5f, 6f, new Color(1f, 0.45f, 0.25f));

                var encounter = enemy.gameObject.AddComponent<EncounterInteractable>();
                encounter.def = def;
                encounter.id = EncounterId(cell);
                encounter.displayName = label;
                encounter.setup = encounterSetup;
                encounter.standPoint = StandPointFor(cell);
                interactables.Add(encounter);

                WorldFx.FloorMarker(transform, encounter.standPoint, WorldArt.MachineRed);
            }
        }

        void SpawnSummonBeacons()
        {
            foreach (var cell in Map.FindAll('R'))
            {
                var beacon = WorldFx.SummonBeacon(transform, Map.CellCenter(cell.x, cell.y));
                interactables.Add(beacon.AddComponent<SummonInteractable>());
            }
        }

        /// <summary>Encounter ids are per area. The Training Center keeps the old scene-only prefix so older saves still match.</summary>
        string EncounterPrefix
        {
            get
            {
                string scene = SceneManager.GetActiveScene().name;
                return (area == null || area.Id == WorldAreas.Training ? scene : scene + "/" + area.Id) + "/encounter_";
            }
        }

        /// <summary>How many encounters in this area are currently beaten.</summary>
        public int BeatenEncountersHere()
        {
            int n = 0;
            foreach (var id in WorldSession.DefeatedEncounters)
                if (id.StartsWith(EncounterPrefix)) n++;
            return n;
        }

        /// <summary>Summon beacon: un-beat every encounter in this area, save, and reload the area where you stand.</summary>
        public void ResummonEncounters()
        {
            WorldSession.DefeatedEncounters.RemoveWhere(id => id.StartsWith(EncounterPrefix));
            ReloadHere(LeaderName, StoryText.Text("beacon.sent"), new Color(1f, 0.3f, 0.2f, 0.5f));
        }

        /// <summary>Saves, then reloads this area with you standing where you are and a message once it is back.</summary>
        void ReloadHere(string speaker, string message, Color fade)
        {
            if (transitioning) return;
            transitioning = true;
            cutscene = true;
            UI.HidePrompt();
            WorldSession.ReturnTo(CurrentSpot());
            WorldSession.PendingSpeaker = speaker;
            WorldSession.PendingMessage = message;
            SaveGame.Save();
            UI.SetFade(fade);
            ScreenFade.LoadScene(SceneManager.GetActiveScene().name);
        }

        WorldSession.Spot CurrentSpot()
        {
            return new WorldSession.Spot { area = area.Id, position = player.transform.position, facing = player.Sprite.Facing };
        }

        // ------------------------------------------------------------------ Access Points and chests

        /// <summary>
        /// An Access Point: full HP, it becomes where a lost fight wakes you, every beaten patrol machine comes back
        /// (story fights don't), and the game saves. If machines return here, the area reloads around you.
        /// </summary>
        public void RestAtAccessPoint(Vector3 standPoint)
        {
            int beatenHere = BeatenEncountersHere();
            WorldSession.LastAccessPoint = new WorldSession.Spot { area = area.Id, position = standPoint, facing = player.Sprite.Facing };
            WorldSession.RestoreParty();
            WorldSession.RespawnPatrols();
            RefreshPartyHud();
            string speaker = StoryText.Speaker("access.rest");
            if (beatenHere > 0)
            {
                ReloadHere(speaker, StoryText.Text("access.rest") + "{p} " + StoryText.Text("access.respawn"), new Color(0.45f, 0.85f, 1f, 0.5f));
                return;
            }
            SaveGame.Save();
            UI.Say(speaker, StoryText.Text("access.rest"), DialogueVoice.System);
        }

        /// <summary>Loot chests ('c'): each holds the area's Part for it (WorldAreas.Area.Chests, reading order), once.</summary>
        void SpawnLootChests()
        {
            var cells = Map.FindAll('c');
            for (int i = 0; i < cells.Count; i++)
            {
                var go = WorldFx.Chest(transform, Map.CellCenter(cells[i].x, cells[i].y));
                var chest = go.AddComponent<LootChestInteractable>();
                chest.part = i < area.Chests.Count ? area.Chests[i] : null;
                chest.flag = "chest." + area.Id + "." + i;
                if (StoryState.Has(chest.flag)) chest.ShowOpen();
                interactables.Add(chest);
            }
        }

        public void OpenLootChest(LootChestInteractable chest)
        {
            if (StoryState.Has(chest.flag) || string.IsNullOrEmpty(chest.part))
            {
                UI.Say("", StoryText.Text("chest.empty"), DialogueVoice.Default);
                return;
            }
            StoryState.Set(chest.flag);
            chest.ShowOpen();
            string found = GiveItem(chest.part);
            SaveGame.Save();
            UI.Say("", found, DialogueVoice.Default);
        }

        /// <summary>Puts a Part (asset name) in the inventory and returns the "You got the ..." line for it.</summary>
        public static string GiveItem(string partName)
        {
            var part = WorldSession.Catalog != null ? WorldSession.Catalog.Find(partName) : null;
            if (part != null) WorldSession.AddPart(part);
            return StoryText.Text("item.found", "item", part != null ? part.DisplayName : partName, "desc", part != null ? part.description : "");
        }

        /// <summary>
        /// After the formation changes: if someone else now leads, walk as them, and redraw the party HUD.
        /// </summary>
        public void RefreshParty()
        {
            var lead = Lead;
            if (player != null && lead != null && player.name != LeaderName)
            {
                var position = player.transform.position;
                int facing = player.Sprite.Facing;
                Destroy(player.gameObject);
                SpawnPlayerAt(position, facing);
            }
            RefreshPartyHud();
        }

        /// <summary>Crew menu: make <paramref name="member"/> the leader and switch to playing as them.</summary>
        public void SetLeader(CharacterDefinition member)
        {
            if (encounterSetup == null || member == null) return;
            WorldSession.SetLeader(member, encounterSetup.party);
            SaveGame.Save();
            var position = player.transform.position;
            int facing = player.Sprite.Facing;
            Destroy(player.gameObject);
            SpawnPlayerAt(position, facing);
            RefreshPartyHud();
        }

        void SpawnAccessPoints()
        {
            foreach (var cell in Map.FindAll('A'))
            {
                var point = WorldFx.AccessPoint(transform, Map.CellCenter(cell.x, cell.y));
                var access = point.AddComponent<AccessPointInteractable>();
                // A purple square on its first free side (south first): stand there and press F to heal.
                access.standPoint = BesideCell(cell);
                WorldFx.FloorMarker(transform, access.standPoint, WorldArt.HealPurple);
                interactables.Add(access);
            }
        }

        /// <summary>Redraws the party HP readout from WorldSession.</summary>
        public void RefreshPartyHud()
        {
            if (UI == null || encounterSetup == null) return;
            var lines = new List<OverworldUI.PartyLine>();
            foreach (var member in Party)
            {
                if (member == null) continue;
                lines.Add(new OverworldUI.PartyLine(member.DisplayName, WorldSession.ProgressOf(member).Level,
                    WorldSession.HPOf(member), WorldSession.MaxHPOf(member), WorldSession.ColorOf(member)));
            }
            UI.SetParty(lines);
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
            return EncounterPrefix + cell.x + "_" + cell.y;
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
            if (drift != null) drift.position = player.transform.position + new Vector3(0f, 7f, 3f);
            // Where you are, for the save (Continue puts you back here).
            if (!transitioning)
            {
                if (WorldSession.PlayerSpot == null) WorldSession.PlayerSpot = new WorldSession.Spot();
                WorldSession.PlayerSpot.area = area.Id;
                WorldSession.PlayerSpot.position = player.transform.position;
                WorldSession.PlayerSpot.facing = player.Sprite.Facing;
            }

            bool menuOpen = (menu != null && menu.IsOpen) || (title != null && title.IsOpen) || (pauseCards != null && pauseCards.IsOpen)
                            || (formation != null && formation.IsOpen)
                            || (resonance != null && resonance.IsOpen)
                            || (items != null && items.IsOpen)
                            || (chestPanel != null && chestPanel.IsOpen);
            player.Frozen = cutscene || UI.DialogueOpen || menuOpen;
            scannerHud.SetVisible(StoryState.Has(Chapter1.Scanner) && !UI.DialogueOpen && !menuOpen && !cutscene);
            if (menuOpen)
            {
                focused = null;
                UI.HidePrompt();
                return;
            }
            // Esc: the card menu (Quit / Save / Resume / Party).
            if (Input.GetKeyDown(KeyCode.Escape) && menu != null && menu.LastToggleFrame != Time.frameCount
                && pauseCards != null && pauseCards.LastToggleFrame != Time.frameCount
                && chestPanel != null && chestPanel.LastToggleFrame != Time.frameCount
                && formation != null && formation.LastToggleFrame != Time.frameCount
                && resonance != null && resonance.LastToggleFrame != Time.frameCount
                && items != null && items.LastToggleFrame != Time.frameCount
                && !UI.Dialogue.gameObject.activeSelf && !cutscene && !transitioning)
            {
                UI.HidePrompt();
                pauseCards.Open();
                return;
            }
            if (UI.DialogueOpen)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) UI.Dialogue.Advance(); // F also advances
                focused = null;
                UI.HidePrompt();
                return;
            }

            // Story: stepping onto a new square (triggers) and the scanner (Q).
            int px, prow;
            Map.WorldToCell(player.transform.position, out px, out prow);
            var nowCell = new Vector2Int(px, prow);
            if (nowCell != playerCell)
            {
                bool first = playerCell.x < 0;
                playerCell = nowCell;
                if (!first && !Busy) ChapterOneDirector.OnEnterCell(this, nowCell, Map.At(px, prow));
            }
            if (Input.GetKeyDown(KeyCode.Q) && !Busy && StoryState.Has(Chapter1.Scanner))
            {
                if (!scannerHud.Ready) scannerHud.Deny();
                else
                {
                    scannerHud.StartCooldown(ScanCooldown);
                    ChapterOneDirector.Scan(this);
                }
            }

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

            // Prompts are simply "[F] Talk" for people and "[F] Interact" for everything else, for now
            // (each interactable still has its own Prompt text for later).
            if (focused != null)
            {
                var talk = focused as TalkInteractable;
                bool person = talk != null && string.IsNullOrEmpty(talk.prompt);
                string verb = person ? "Talk" : focused is AccessPointInteractable ? "Heal" : "Interact";
                UI.ShowPrompt(interactKey.ToString(), verb, focused.PromptAnchor, cam);
            }
            else UI.HidePrompt();
        }

        public void OnInteractPressed()
        {
            if (menu != null && menu.IsOpen) return; // the menu reads F itself
            if (UI.DialogueOpen)
            {
                UI.Dialogue.Advance();
                return;
            }
            if (transitioning || cutscene) return;
            if (focused != null) focused.Interact(this);
        }

        /// <summary>How the party leader sounds in dialogue: calm and even.</summary>
        public DialogueVoice LeaderVoice
        {
            get
            {
                var lead = Lead;
                return new DialogueVoice(0.9f, 0.95f, lead != null ? lead.color : DialogueVoice.Default.Accent);
            }
        }

        public Sprite LeaderPortrait
        {
            get
            {
                var lead = Lead;
                return Portrait(lead != null ? lead.animations : null);
            }
        }

        /// <summary>The front-facing idle frame of an animation set, used as a dialogue portrait.</summary>
        static Sprite Portrait(SpriteAnimationSet set)
        {
            if (set == null) return null;
            var frames = set.Frames("idle", Combat.Facing.Down, true) ?? set.Frames("walk", Combat.Facing.Down, true);
            return frames != null && frames.Length > 0 ? frames[0] : null;
        }

        public void StartEncounter(EncounterInteractable encounter)
        {
            if (transitioning) return;
            if (encounter.setup == null)
            {
                UI.Say("System", "No BattleSetup assigned to this encounter.", DialogueVoice.System);
                return;
            }
            // Every fight opens the pre-fight screen first: choose who to bring, then Start.
            UI.HidePrompt();
            formation.OpenPreFight(() => BuildSetup(encounter), () => StartCoroutine(BattleTransition(encounter)));
        }

        /// <summary>
        /// The fight an encounter starts, as the battle will run it: the formation's active members and backup, no more
        /// machines than active members, and (in the Training Center) the chest's level and boss mode.
        /// </summary>
        public BattleSetup BuildSetup(EncounterInteractable encounter)
        {
            var setup = Instantiate(encounter.setup);
            setup.name = encounter.setup.name;
            setup.party = WorldSession.FormationActive(encounter.setup.party);
            setup.backup = WorldSession.FormationBackup(encounter.setup.party);
            // A defined fight (story / area encounters): exactly its enemies, at its level.
            var def = encounter.def;
            var catalog = EnemyCatalog.Get();
            if (def != null && def.Enemies != null && catalog != null)
            {
                setup.enemies = new List<EnemyDefinition>();
                foreach (var name in def.Enemies)
                {
                    var e = catalog.Find(name);
                    if (e != null) setup.enemies.Add(e);
                    else Debug.LogWarning("Bestiary has no enemy '" + name + "' (open the project in Unity once to create Chapter One's content).");
                }
                setup.enemyLevel = def.Level;
                setup.boss = def.Boss;
                if (setup.enemies.Count > 0) return setup;
                setup.enemies = new List<EnemyDefinition>(encounter.setup.enemies);
            }
            // No more machines than active members: MC alone (a new game) faces one, the full crew faces them all.
            if (setup.party.Count > 0 && setup.enemies.Count > setup.party.Count)
                setup.enemies = setup.enemies.GetRange(0, setup.party.Count);
            // Training Center fights use the chest's level; a boss fight is one big machine.
            if (area != null && area.Id == WorldAreas.Training)
            {
                setup.enemyLevel = WorldSession.TrainingEnemyLevel;
                setup.boss = WorldSession.TrainingBoss;
                if (setup.boss && setup.enemies.Count > 1) setup.enemies = setup.enemies.GetRange(0, 1);
            }
            return setup;
        }

        IEnumerator BattleTransition(EncounterInteractable encounter)
        {
            transitioning = true;
            cutscene = true;
            UI.HidePrompt();

            WorldSession.HasReturnPoint = true;
            WorldSession.ReturnPosition = player.transform.position;
            WorldSession.ReturnFacing = player.Sprite.Facing;

            string returnScene = SceneManager.GetActiveScene().name;
            string id = encounter.id;
            string winFlag = encounter.def != null ? encounter.def.WinFlag : null;
            string winMessage = encounter.def == null ? null
                : encounter.def.WinKey != null ? StoryText.Text(encounter.def.WinKey) : encounter.def.WinMessage;
            string reward = encounter.def != null ? encounter.def.Reward : null;
            string leader = LeaderName;
            var setup = BuildSetup(encounter);
            // Levels, HP and loadouts for everyone who comes (the backup too).
            var everyone = new List<CharacterDefinition>(setup.party);
            if (setup.backup != null) everyone.Add(setup.backup);
            var party = WorldSession.PartyFor(everyone);
            BattleLauncher.Request(setup, party, WorldSession.LoadoutsFor(everyone), result =>
            {
                WorldSession.PendingSpeaker = leader;
                // Levels, XP and HP carry over; a defeat sends the party back to regroup at full HP.
                WorldSession.Apply(result.Party);
                if (result.Outcome == BattleOutcome.Defeat) WorldSession.RestoreParty();

                switch (result.Outcome)
                {
                    case BattleOutcome.Victory:
                        WorldSession.DefeatedEncounters.Add(id);
                        if (winFlag != null) StoryState.Set(winFlag);
                        // The results screen already shows XP and level ups: only story fights say anything after
                        // (plus the Part a story fight gives).
                        if (!string.IsNullOrEmpty(reward))
                        {
                            string got = GiveItem(reward);
                            winMessage = string.IsNullOrEmpty(winMessage) ? got : winMessage + "{p} " + got;
                        }
                        WorldSession.PendingMessage = winMessage;
                        break;
                    case BattleOutcome.Fled:
                        WorldSession.PendingMessage = StoryText.Text("battle.fled");
                        break;
                    default:
                        // A lost fight wakes you at the last Access Point you rested at (or the area's start).
                        if (WorldSession.LastAccessPoint != null) WorldSession.ReturnTo(WorldSession.LastAccessPoint);
                        else
                        {
                            WorldSession.HasReturnPoint = false;
                            WorldSession.PlayerSpot = null;
                        }
                        WorldSession.PendingMessage = StoryText.Text("battle.lost");
                        break;
                }
                SaveGame.Save();
                ScreenFade.LoadScene(returnScene);
            });

            // Red alert flash, then the ink fade into battle.
            UI.SetFade(new Color(1f, 0.3f, 0.2f, 0.7f));
            ScreenFade.LoadScene(battleScene);
            yield break;
        }

        IEnumerator Arrive()
        {
            var story = ChapterOneDirector.Arrival(this);
            if (story != null)
            {
                yield return story;
                yield break;
            }
            bool returning = WorldSession.HasReturnPoint;
            UI.SetFade(Color.black);
            cutscene = true;
            yield return UI.FadeTo(new Color(0f, 0f, 0f, 0f), 0.6f);
            cutscene = false;

            if (!string.IsNullOrEmpty(WorldSession.PendingMessage))
            {
                UI.Say(WorldSession.PendingSpeaker ?? "", WorldSession.PendingMessage, LeaderVoice, LeaderPortrait);
                WorldSession.PendingMessage = null;
            }
            if (!returning) yield return UI.ShowTitle(areaName, areaSubtitle);
        }

        /// <summary>An NPC whose name and lines are a script block (the first line's speaker is their name).</summary>
        static NpcInfo Npc(int row, int col, string key, DialogueVoice voice)
        {
            return new NpcInfo(row, col, StoryText.Speaker(key), voice, StoryText.Texts(key));
        }

        struct NpcInfo
        {
            public readonly int Row, Col;
            public readonly string Name;
            public readonly DialogueVoice Voice;
            public readonly string[] Lines;

            public NpcInfo(int row, int col, string name, DialogueVoice voice, string[] lines)
            {
                Voice = voice;
                Row = row;
                Col = col;
                Name = name;
                Lines = lines;
            }
        }
    }
}
