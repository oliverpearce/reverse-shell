using System.Collections;
using System.Collections.Generic;
using Combat;
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
        public const string DefaultMap =
            "HHHHHH####HHHHH#BBBBBBBBBBBB\n" +
            "HHHHHH####HHHHH#BPPP,,PPPPPB\n" +
            "HHHHHH####HHHHH#BPT,PPEPP,TB\n" +
            "HHHHHH####HHHHH#B,,PPPXPPP,B\n" +
            "HHHHHH####HHHHH#BPR*PPPPPbPB\n" +
            "HHHHHH####HHHHH#BLLLLP.PLLLB\n" +
            "B.T,,...b..n.r,,....*..,.,TB\n" +
            "B=====,,==========,,=======B\n" +
            "B=======,===========r======B\n" +
            "B.b..,.n..T,,......b...,T..B\n" +
            "B.S.A,,......*....n..,,..*.B\n" +
            "LLLLLLLLLLLLLLLLLLLLLLLLLLLL\n";

        [Header("Area")]
        public string areaName = "MOSSLINE STATION";
        public string areaSubtitle = "The City Ruins";
        [Tooltip("On: always use the built-in DefaultMap from code, so map updates show up without regenerating the scene. " +
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
                var lead = Lead;
                return lead != null ? lead.DisplayName : "You";
            }
        }
        public OverworldMap Map { get; private set; }

        /// <summary>The party in the player's chosen order (WorldSession.PartyOrder). The first is the leader you play as.</summary>
        public List<CharacterDefinition> Party
        {
            get { return encounterSetup != null ? WorldSession.Ordered(encounterSetup.party) : new List<CharacterDefinition>(); }
        }

        CharacterDefinition Lead
        {
            get
            {
                var party = Party;
                return party.Count > 0 ? party[0] : null;
            }
        }

        PartyMenu menu;

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
            WorldSession.EnsureLoaded(); // first area of the session reads save.json
            Map = new OverworldMap(useDefaultMap || string.IsNullOrEmpty(map) ? DefaultMap : map);
            cam = Camera.main;
            ApplyAtmosphere();

            foreach (var cell in Map.FindAll('E'))
                if (WorldSession.DefeatedEncounters.Contains(EncounterId(cell))) ClearEncounter(cell);

            BuildGeometry();
            BuildProps();
            SpawnPlayer();
            SpawnNpcs();
            SpawnEncounters();
            SpawnAccessPoints();
            SpawnSummonBeacons();

            drift = WorldFx.Drift(transform).transform;
            UI = OverworldUI.Create(transform);
            menu = PartyMenu.Create((RectTransform)UI.transform, this);
            RefreshPartyHud();
            StartCoroutine(Arrive());
        }

        // ------------------------------------------------------------------ Build

        void ApplyAtmosphere()
        {
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
            if (WorldSession.HasReturnPoint) start = WorldSession.ReturnPosition;
            SpawnPlayerAt(start, WorldSession.HasReturnPoint ? WorldSession.ReturnFacing : CharacterSprite.Down);
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
            var glow = Color.Lerp(lead != null ? lead.color : Color.white, Color.white, 0.4f);
            glow.a = 0.28f;
            sprite.SetGlow(glow);

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

            // (sheet row, col), name, voice, lines. Lines use DialogueMarkup ({wave}, {shake}, {red}, {p}, {slow}...).
            // Add more entries for more NPCs; they cycle if the map has extras.
            var cast = new[]
            {
                // Gruff, low and unhurried.
                new NpcInfo(1, 0, "Resistance Trader", new DialogueVoice(0.72f, 0.85f, new Color(0.72f, 0.45f, 0.24f)), new[]
                {
                    "Quiet morning.{p} Just birds, wind{p=0.4}... and the {shake}creak{/shake} of machines somewhere in the green.",
                    "One of 'em planted itself in the old station plaza up north. {gray}Rusted{/c} thing, but it {shake}still swings hard{/shake}.",
                    "Want to fight it? Stand on the {red}red square{/c}.{p} Better you than me."
                }),
                // Young, quick and a little starstruck.
                new NpcInfo(1, 1, "Resistance Scout", new DialogueVoice(1.35f, 1.5f, new Color(0.3f, 0.65f, 1f)), new[]
                {
                    "{wave}Whoa{/wave}{p} you're " + LeaderName + "?! {fast}The android from the orbital Bunker? The one who came down on the last flight?!{/fast}",
                    "Sorry, sorry.{p} I've just never met one of you up close.",
                    "Your support units are waiting by the road. {blue}V1{/c} hits hard, {green}V2{/c} keeps everyone running.{p} {wave}Good company down here!{/wave}"
                }),
                // Dreamy and slow, with long pauses.
                new NpcInfo(2, 2, "Wanderer", new DialogueVoice(1.05f, 0.6f, new Color(0.5f, 0.75f, 0.45f)), new[]
                {
                    "{slow}The trees took this city slowly...{/slow}",
                    "{wave}Kind of beautiful{/wave}, if you stop and look.",
                    "{gray}{slow}...Do machines dream, do you think?{/slow}{/c}"
                }),
            };

            for (int i = 0; i < cells.Count; i++)
            {
                var info = cast[i % cast.Length];
                var pos = Map.CellCenter(cells[i].x, cells[i].y);
                var look = animated ? npcAnimations[i % npcAnimations.Count] : null;
                var npc = look != null
                    ? CharacterSprite.Create(transform, info.Name, look, animatedCharacterScale, pos)
                    : CharacterSprite.Create(transform, info.Name, WorldArt.SingleFrame(sheet, info.Row, info.Col), characterScale, pos, 0.04f);
                npc.SetGlow(new Color(1f, 0.95f, 0.85f, 0.18f));
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
                var enemy = encounterAnimations != null
                    ? CharacterSprite.Create(transform, EncounterName, encounterAnimations, animatedCharacterScale, pos)
                    : CharacterSprite.Create(transform, EncounterName, frames, characterScale, pos, 0.03f);
                enemy.SetFacing(CharacterSprite.Down);
                enemy.SetGlow(new Color(1f, 0.35f, 0.25f, 0.3f));

                WorldFx.Motes(enemy.transform, pos + Vector3.up * 0.9f, 0.5f, 6f, new Color(1f, 0.45f, 0.25f));

                var encounter = enemy.gameObject.AddComponent<EncounterInteractable>();
                encounter.id = EncounterId(cell);
                encounter.displayName = EncounterName;
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

        string EncounterPrefix { get { return SceneManager.GetActiveScene().name + "/encounter_"; } }

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
            SaveGame.Save();
            WorldSession.HasReturnPoint = true;
            WorldSession.ReturnPosition = player.transform.position;
            WorldSession.ReturnFacing = player.Sprite.Facing;
            WorldSession.PendingSpeaker = LeaderName;
            WorldSession.PendingMessage = "{red}Signal sent.{/c}{p} Something is {shake}stirring{/shake} in the plaza again.";
            StartCoroutine(ReloadArea());
        }

        IEnumerator ReloadArea()
        {
            cutscene = true;
            UI.SetFade(new Color(1f, 0.3f, 0.2f, 0.5f));
            yield return UI.FadeTo(Color.black, 0.4f);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Party menu: make <paramref name="member"/> the leader and switch to playing as them.</summary>
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
                interactables.Add(point.AddComponent<AccessPointInteractable>());
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
                    WorldSession.HPOf(member), WorldSession.MaxHPOf(member), member.color));
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
            if (drift != null) drift.position = player.transform.position + new Vector3(0f, 7f, 3f);

            bool menuOpen = menu != null && menu.IsOpen;
            player.Frozen = cutscene || UI.DialogueOpen || menuOpen;
            if (menuOpen)
            {
                focused = null;
                UI.HidePrompt();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) && menu != null && menu.LastToggleFrame != Time.frameCount
                && !UI.Dialogue.gameObject.activeSelf && !cutscene && !transitioning)
            {
                UI.HidePrompt();
                menu.Open();
                return;
            }
            if (UI.DialogueOpen)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) UI.Dialogue.Advance(); // F also advances
                focused = null;
                UI.HidePrompt();
                return;
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

            if (focused != null) UI.ShowPrompt(interactKey.ToString(), focused.Prompt, focused.PromptAnchor, cam);
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
            StartCoroutine(BattleTransition(encounter));
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
            string enemyName = encounter.displayName;
            string leader = LeaderName;
            var setup = Instantiate(encounter.setup);
            setup.name = encounter.setup.name;
            setup.party = WorldSession.Ordered(encounter.setup.party);
            var party = WorldSession.PartyFor(setup.party);
            BattleLauncher.Request(setup, party, result =>
            {
                WorldSession.PendingSpeaker = leader;
                // Levels, XP and HP carry over; a defeat sends the party back to regroup at full HP.
                WorldSession.Apply(result.Party);
                if (result.Outcome == BattleOutcome.Defeat) WorldSession.RestoreParty();

                switch (result.Outcome)
                {
                    case BattleOutcome.Victory:
                        WorldSession.DefeatedEncounters.Add(id);
                        WorldSession.PendingMessage = "The " + enemyName + " grinds to a halt.{p} The plaza goes quiet{p=0.4}... just wind in the leaves." +
                            LevelUpLine(result);
                        break;
                    case BattleOutcome.Fled:
                        WorldSession.PendingMessage = "Withdrawing.{p} It's still out there... {gray}we come back ready.{/c}";
                        break;
                    default:
                        WorldSession.PendingMessage = "{shake}...{/shake}We pulled back and patched up.{p} That machine hits {shake}harder{/shake} than it looks.";
                        break;
                }
                SaveGame.Save();
                SceneManager.LoadScene(returnScene);
            });

            // Red alert flash, then cut to black.
            UI.SetFade(new Color(1f, 0.3f, 0.2f, 0.7f));
            yield return UI.FadeTo(Color.black, 0.45f);
            SceneManager.LoadScene(battleScene);
        }

        /// <summary>" 2B and V2 reached a new level." style suffix for the victory message, or "".</summary>
        static string LevelUpLine(BattleResult result)
        {
            var names = new List<string>();
            foreach (var entry in result.LevelsGained) names.Add(entry.Key.DisplayName);
            if (names.Count == 0) return "";
            string who = names.Count == 1 ? names[0] : string.Join(", ", names.GetRange(0, names.Count - 1).ToArray()) + " and " + names[names.Count - 1];
            return "{p} {gold}" + who + (names.Count == 1 ? " reached a new level!" : " reached new levels!") + "{/c}";
        }

        IEnumerator Arrive()
        {
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
