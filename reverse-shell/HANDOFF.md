# HANDOFF: where we left off (2026-10-22)

Read this first when resuming. See also [GAME_PLAN.md](GAME_PLAN.md) (roadmap) and [GAME_BIBLE.md](GAME_BIBLE.md) (vision).

## Project at a glance
- **Engine:** Unity **2019.4.14f1**, C# 7.3 only (no switch expressions or records). Project folder: `reverse-shell/`.
- **Game:** *NEON HEX*, a non-commercial fan project. It has an HD-2D overworld (Octopath-style) and Reverse: 1999-style card-merge battles.
- **Party:** Miku (Cantor), Teto (Bruiser) and Rei (Android Medic, **orange hair**).
- **Packages added:** `com.unity.postprocessing` **3.0.3**, the newest version that works on 2019.4.14.

## How to run
1. Run **Tools → Combat → Generate Sample Content**. It rebuilds characters, cards, statuses, animations, the backdrop and `Scenes/Battle.unity`.
2. Run **Tools → Overworld → Generate Rainline Station**. It builds `Scenes/Overworld_Rainline.unity` and sets it as the Play start scene. **Tools → Overworld → Always Play From Overworld** turns that on or off.
3. Press Play. Walk with WASD/arrows, hold Shift to run, and press **F** on the glowing square to fight.
- In battle:
  - Click a card to queue it (1 AP). Drag a card to move it and merge (1 AP).
  - Click an enemy to target it. **Hover** a unit for its buffs and debuffs. **Hold** a unit to open its details screen.
  - **Esc** or the top-right **II** opens the pause menu (Resume, Restart, Leave).
- Turn on Unity's **Recompile After Finished Playing** setting, or scripts edited during Play wipe the battle's state. The game now recovers by rebuilding itself.

## Dev tools (no Unity needed)
| Command | What it does |
|---|---|
| `bash Tools/DevCheck/compile_check.sh` | Compiles all 5 assemblies with Unity's Roslyn (C# 7.3) |
| `bash Tools/DevCheck/BackdropPreview/preview.sh` | Renders the battle backdrop painter to a PNG |
| `python Tools/SpriteForge/lpc_compose.py [Names...]` | Rebuilds LPC character sprites from `RECIPES` |
| `python Tools/SpriteForge/preview.py out.png Miku Teto Rei` | Contact sheet of the composed characters |
The scripts are written for Git Bash on Windows. The Python scripts are pure Python (no PIL), and they read and write PNGs through `pngtool.py`.

## Code map (`Assets/_Project/`)
- `Scripts/Core, Data, Effects, Battle` (**Combat.Runtime**): battle rules, with no UI.
  - `BattleController` emits `BattleEvent`s.
  - Cards and characters are ScriptableObjects. `CardEffect` subclasses provide `Text()` templates, and `CardText.AllRanks` / `ForRank` build the descriptions.
  - `BattleLauncher` passes a `BattleRequest` from the world into a fight and returns a `BattleOutcome` (Victory, Defeat or Fled).
- `Scripts/View` (also **Combat.Runtime**): the uGUI battle presentation.
  - `BattleView` builds the screen and replays events. Other pieces: `HandView`/`CardView`, `UnitView` (sprite, idle/slash/cast/hurt, hover, long-press), `UnitInfoPanel` (hover menu), `UnitInspectView` + `AbilityCard` (details screen, scroll-to-change-rank), `PauseMenu`, `BattleBackdrop` + `BackdropPainterCore` (procedural pixel backdrop), `HudView`.
  - The global UI size is `UIFactory.UIScale` (currently 0.8). Canvases use **Shrink** matching.
- `Scripts/World` (**Game.World**): the overworld.
  - `OverworldBuilder` turns a text map into a 3D mesh, plus lamps, signs, NPCs and the encounter.
  - Other pieces: `PlayerController`, upright `CharacterSprite` billboards, `OverworldCamera`, `PostFxFocus`, and `WorldSession` (return point, defeated encounters).
- `Editor/` (**Combat.Editor**) and `Editor/World/` (**Game.Editor**): generators, the `SubclassSelectorDrawer` type picker, and `PixelArtImporter` (point filtering; ThirdParty textures are Read/Write).
- `Tests/EditMode` (**Combat.Tests**): hand/merge, rules, controller, map and card-text tests.
- `Art/`:
  - CyberCity (CC0, tiles and some sprites).
  - `Characters/<Name>/` LPC sprites (CC-BY-SA/GPL). **Credits are required**; see each folder's `CREDITS.md`.

## Terms (renamed during development)
| Shown in game | Was / code name |
|---|---|
| Turn | Round (`State.Round`) |
| AP | action points |
| Ult (the charge meter) | Moxie (`Unit.Moxie`, `moxieCap`) |
| Ritual (the ultimate card) | Ritual (`isRitual`) |
| Physical / Special damage and DEF | Reality / Mental (enum values unchanged) |

## User preferences learned
- **Readability is the top priority.** Keep UI simple and number-first: big auto-fitting numbers, minimal text.
- The user likes **scroll-to-change-rank** on abilities, with a hover highlight (gold border) and no hint text.
- Use existing online assets (CC0 or credited open licenses) before drawing anything. Tell the user which assets are being used.
- Pastel Ritual cards, a neon-grunge cyberpunk look, and Octopath-style 32×48 sprites.
- Keep combat code separate from world code. Build content as data, not code.

## Open items / ideas for next session
- Effects (buffs/debuffs) were removed from the details screen. The user said they "can be somewhere else". They're still on the status chips and the hover menu, but this could get a dedicated place.
- Phase 1 from GAME_PLAN: carry HP between fights, levels and XP, a results screen with XP and loot.
- Overworld: warps and a second map (Neon Row), a data-driven dialogue box, random encounter zones.
- The overworld pixel density (16-px tiles at 4×) doesn't match the 32×48 LPC characters; this could be revisited.
- Before making player builds, add `Sprites/Diffuse` and `Legacy Shaders/Particles/Additive` to Always Included Shaders.
- Code changes are compile-checked with `Tools/DevCheck`. Play-testing happens in the Unity editor.
