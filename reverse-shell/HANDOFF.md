# HANDOFF: where we left off (2026-10-22, session 3)

Read this first when resuming. See also [GAME_PLAN.md](GAME_PLAN.md) (roadmap) and [GAME_BIBLE.md](GAME_BIBLE.md) (vision).

## Project at a glance
- **Engine:** Unity **2019.4.14f1**, C# 7.3 only (no switch expressions or records). Project folder: `reverse-shell/`.
- **Game:** a non-commercial fan project (working title *NEON HEX*; a new title is pending, see the Bible). It has an HD-2D overworld (Octopath-style) and Reverse: 1999-style card-merge battles.
- **Look (new this session):** an **overgrown, sunlit city ruin** in the spirit of *NieR:Automata*, replacing the neon cyberpunk city.
- **Party:** **2B** (YoRHa Battler, the leader, with a painted **blindfold**), **V1** (Assault Unit, white-plated robot; the old Teto kit) and **V2** (Support Unit, charcoal-plated robot; the old Rei kit). Teto and Rei were removed at the user's request.
- **Enemies:** machine lifeforms, the **Rust Sentry** and the **Scrap Stalker**.
- **Packages added:** `com.unity.postprocessing` **3.0.3**, the newest version that works on 2019.4.14.

## How to run
1. Run **Tools → Combat → Generate Sample Content**. It rebuilds characters, cards, statuses, animations, the backdrop and `Scenes/Battle.unity`, and deletes the retired Miku/Static assets.
2. Run **Tools → Overworld → Generate Mossline Station**. It builds `Scenes/Overworld_Mossline.unity` and sets it as the Play start scene. **Tools → Overworld → Always Play From Overworld** turns that on or off.
3. Press Play. Walk with WASD/arrows, hold Shift to run, and press **F** on the red square to fight. The terminal next to the spawn is an **Access Point**: press **F** there to restore HP.
- **Progress is saved to disk** (`save.json` in `Application.persistentDataPath`): each member's level, XP and HP, plus beaten encounters. It saves after every battle and at the Access Point, and loads when Play starts. **Tools → Overworld → Delete Save Data** starts fresh; you need it to fight the plaza machine again once it's beaten.
- The party HUD (top left) has one row per member: the color line, the name, a big HP bar and a small "Lv. N" beneath. The leader (2B) gets the largest row, with a soft gradient behind it (the crown icon was removed). A defeat sends the party back at full HP. Restarting from the pause menu restarts from the same levels and HP.
- **Levels and XP:** winning gives every member the enemies' XP (Rust Sentry 120 and Scrap Stalker 80, so 200 a fight). Lv 2 needs 100 XP, then 200, 300 and so on. Each level adds 8% base HP, ATK and DEF. The result screen shows "+XP", each member's level, "LEVEL UP!", and XP toward the next level. The battle nameplates show "Lv N".
- In battle:
  - Click a card to queue it (1 AP). Drag a card to move it and merge (1 AP).
  - Click an enemy (or press **Enter** on the ringed one) to target it. The target gets a red **Blue Archive-style lock-on**: four spinning arcs, inward ticks and a center diamond. The unit under the cursor gets a white segmented focus ring (three long arcs plus a counter-spinning dashed ring) that fits around it. Both snap in like a hit marker. The rings are smooth, anti-aliased 256 px textures drawn in code (`TargetReticle`, `Reticle`, `Spin`).
  - **Arrow keys** move the white ring: Left/Right step through every unit, Up/Down jump between the party and the enemies. **Enter** targets the ringed enemy. **Space** inspects the ringed unit (the same as holding it), and closes the inspect screen.
  - The reticles each sit on a soft dark shadow for contrast and carry a blurred glow that spins with their arcs.
  - **Cards** are Reverse 1999-style (`CardView.BuildFace`). The small hand card shows only the rank (top-left) and one huge ATTACK / BUFF / DEBUFF / HEAL word along the bottom; the middle is a clean solid color (art comes later). The hover preview and the zoom-in cards add the name header, owner and full text.
  - **Card rank** is **stacking stars** in the top-left (`CardStars`): rank I none, rank II one star, rank III two stars, Ritual (ult) cards three. Stars also show on the battle queue slots. The pause menu toggles **CARD RANK: STARS / PIPS** (`CardSettings.RankStars`, saved in PlayerPrefs). The hand and queue rebuild live. The earlier LoL-style borders were removed at the user's request.
  - The zoom-in screen's **ABILITIES** section shows each ability as the real card, at 1.6× (`AbilityCard`). Scroll over a card to flip its rank; the stars and text change.
  - Battle HP bars show **no numbers**; exact HP is only in the inspect screen. Nameplates show the level small ("Lv 3").
  - **Hold** a unit (or press Space) to open its details screen, the only place for full info: next move, effects (buffs/debuffs), stats and abilities. The hover info menu (`UnitInfoPanel`) was removed at the user's request.
  - **Enemy intents** are Slay the Spire-style icons over the enemy's head (`IntentIcons`): sword = attack, gold up-arrow = buff, purple down-arrow = debuff, green plus = heal.
  - UIFactory buttons never keep keyboard focus, so Space and the arrows are free for game controls.
  - **Esc** or the top-right **II** opens the pause menu (Resume, Restart, Leave). **Leave Fight** always works. Launched from the overworld, it returns there as a retreat. Opened directly, it loads `BattleBootstrap.leaveScene` (`Overworld_Mossline`), or stops Play mode if that scene isn't in Build Settings.
  - Every `UIFactory` button has hover feedback (`ButtonHover`): it grows slightly, brightens and gets a gold border. This runs on unscaled time, so it works while paused.
- Turn on Unity's **Recompile After Finished Playing** setting, or scripts edited during Play wipe the battle's state. The game recovers by rebuilding itself.

## Dev tools (no Unity needed)
| Command | What it does |
|---|---|
| `bash Tools/DevCheck/compile_check.sh` | Compiles all 5 assemblies with Unity's Roslyn (C# 7.3) |
| `bash Tools/DevCheck/BackdropPreview/preview.sh` | Renders the battle backdrop painter to a PNG (uses the ruins atlas) |
| `python Tools/TileForge/ruins_atlas.py` | **New.** Repaints the overworld tile atlas `Art/Tiles/ruins.png` (our own art) |
| `python Tools/SpriteForge/lpc_compose.py [Names...]` | Rebuilds LPC character sprites from `RECIPES` (`2B`, `V1`, `V2`, `RustSentry`, `ScrapStalker`...). A layer with `'mask': 'blindfold'` isn't drawn; it marks the eye rows for a painted blindfold band (`apply_masks`). |
| `python Tools/SpriteForge/preview.py out.png 2B Teto Rei` | Contact sheet of the composed characters |
The scripts are written for Git Bash on Windows. The Python scripts are pure Python (no PIL), and they read and write PNGs through `pngtool.py`. `lpc_compose.py` now understands LPC's `metal` material (armor), which is how the machines are recolored.

## Code map (`Assets/_Project/`)
- `Scripts/Core, Data, Effects, Battle` (**Combat.Runtime**): battle rules, with no UI.
  - `BattleController` emits `BattleEvent`s. `ApplyParty` / `PartySnapshot` carry level and HP in and out, and `XpReward` sums the enemies' `xpReward`.
  - `Core/Leveling.cs`: `MemberProgress` (level, XP, HP where -1 means full) and the rules: `XpToNext`, `StatScale`, `MaxHP` and `AddXp`. `BattleUnit.SetLevel` scales HP, ATK and DEF by `CharacterDefinition.growthPerLevel`.
  - Cards and characters are ScriptableObjects. `CardEffect` subclasses provide `Text()` templates, and `CardText.AllRanks` / `ForRank` build the descriptions.
  - `BattleLauncher.Request(setup, party, onComplete)` passes a `BattleRequest` into a fight. The fight returns a `BattleResult` with the outcome, `XpGained`, each member's progress and `LevelsGained`. `BattleResult.Build` makes it, and `BattleBootstrap.Result` holds it for the result screen.
- `Scripts/View` (also **Combat.Runtime**): the uGUI battle presentation.
  - `BattleView` builds the screen and replays events. Other pieces: `HandView`/`CardView`, `UnitView`, `UnitInfoPanel`, `UnitInspectView` + `AbilityCard`, `PauseMenu`, `HudView`.
  - `BattleBackdrop` + `BackdropPainterCore` paint the backdrop: sky with clouds, broken far towers, mossy facades from the ruins atlas, vines, trees, overgrown road and sun rays, with drifting-leaf overlay layers.
  - The global UI size is `UIFactory.UIScale` (currently 0.8). Canvases use **Shrink** matching.
- `Scripts/World` (**Game.World**): the overworld.
  - `OverworldBuilder` turns the text map into a 3D mesh, plus props, NPCs, encounters and access points, and keeps the party HUD current (`RefreshPartyHud`).
  - **Lighting:** there are **no point lights**. The only light is the sun, with a dappled "light through leaves" cookie (`WorldArt.DappleCookie`) that drifts slowly (`CanopySway`), over a dim green trilight ambient and haze. Tune it in `OverworldBuilder.ApplyAtmosphere`.
  - `WorldFx` builds the props and effects: `Tree`, `Bush`, `Rubble`, `Tuft` (swaying grass and flowers), `Vine`, `Motes`, `Drift` (falling leaves), `AccessPoint` and `FloorMarker`.
  - `WorldArt` has the palette (sun, machine red, sepia UI colors), the atlas cell constants and `CellMaterial` (one atlas cell on a primitive, opaque or cutout).
  - Other pieces: `PlayerController`, `CharacterSprite`, `OverworldCamera`, `PostFxFocus` and `OverworldUI` (sepia panels plus party rows with level and HP).
  - `WorldSession` holds the return point, defeated encounters and `Party` (progress keyed by CharacterDefinition asset name). `SaveGame` writes and reads it as JSON.
  - Map legend (`OverworldMap`): `H # B L` blocks, `.` pavement, `=` road, `P` grass plaza, `,` tall grass, `*` flowers, `T` tree, `b` bush, `r` rubble, `A` access point, `S n E X` spawn, NPC, enemy, stand square.
- `Editor/` (**Combat.Editor**) and `Editor/World/` (**Game.Editor**): generators, the `SubclassSelectorDrawer` type picker, and `PixelArtImporter` (point filtering; `ThirdParty/` and `Tiles/` textures are Read/Write).
- `Tests/EditMode` (**Combat.Tests**): hand/merge, rules, controller (party carry-over and XP results), progression (XP curve, level stats, save JSON round trip), map and card-text tests.
- `Art/`:
  - `Tiles/ruins.png`: our own procedural atlas (see `Tiles/CREDITS.md`).
  - `Characters/<Name>/`: LPC sprites (CC-BY-SA/GPL). **Credits are required**; see each folder's `CREDITS.md`.
  - `ThirdParty/CyberCity` (CC0): no longer used for tiles. It's kept only as a fallback sprite sheet source.

## Terms (renamed during development)
| Shown in game | Was / code name |
|---|---|
| Turn | Round (`State.Round`) |
| AP | action points |
| Ult (the charge meter) | Moxie (`Unit.Moxie`, `moxieCap`) |
| Ritual (the ultimate card) | Ritual (`isRitual`) |
| Physical / Special damage and DEF | Reality / Mental (enum values unchanged) |
| `BattleBackdropDefinition.foliage` / `driftingLeaves` | were `neon` / `rain` (`FormerlySerializedAs` kept for `rain`) |
| `OverworldBuilder.tileAtlas` | was `cityAtlas` (`FormerlySerializedAs` kept) |

## User preferences learned
- **Readability is the top priority.** Keep UI simple and number-first: big auto-fitting numbers, minimal text.
- The user likes **scroll-to-change-rank** on abilities, with a hover highlight (gold border) and no hint text.
- Use existing online assets (CC0 or credited open licenses) before drawing anything, and tell the user which assets are used. The ruins tiles were generated procedurally because no CC0 atlas matched the existing layout.
- The look is now NieR-style overgrown ruins (2026-10-22). Pastel Ritual cards and Octopath-style 32×48 sprites stay.
- Keep combat code separate from world code. Build content as data, not code.

## Not yet verified in Unity
Everything above compiles with `compile_check.sh`, but this session couldn't open the Unity editor. The first time you play, check:
- The EditMode tests pass (Window → General → Test Runner). New this session: `ApplyParty_...`, `BattleResult_Victory_...` and the whole `ProgressionTests` class.
- Win a fight, stop Play, press Play again: HP and level should come back, and the plaza machine should stay beaten.
- The cutout grass and vines render (they use `Legacy Shaders/Transparent/Cutout/Diffuse`).
- The lighting and fog balance look right. Tune them in `OverworldBuilder.ApplyAtmosphere` and the post-FX profile in `OverworldGenerator`.

## Open items / ideas for next session
- Phase 1 continues: loot (Parts) on the results screen, a Flee button (chance-based, disabled for bosses), and a second card set per character.
- Defeat currently regroups at full HP where the party stood. It should respawn at the last Access Point (save the Access Point's position too).
- More fights are needed for levels to matter: random encounters in the tall grass would supply them.
- The save doesn't store the player's position yet; a loaded game starts at the spawn.
- Pick a new title (ideas are in the Bible).
- An optional sepia NieR-style pass on the battle UI, to match the overworld.
- 2B's sword: LPC weapons use 128 px frames, which don't fit the 64 px layout. A Pod companion sprite would be a nice touch.
- A second map (the Sunken Arcade or a Resistance camp) with warps and a data-driven dialogue box.
- Effects (buffs/debuffs) are still missing from the details screen ("can be somewhere else").
- Before making player builds, add `Sprites/Diffuse`, `Legacy Shaders/Particles/Additive`, `Legacy Shaders/Particles/Alpha Blended` and `Legacy Shaders/Transparent/Cutout/Diffuse` to Always Included Shaders.
