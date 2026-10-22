# GAME PLAN: *NEON HEX*

This is the technical roadmap. For the creative vision, see [GAME_BIBLE.md](GAME_BIBLE.md).
**Engine:** Unity 2019.4.14f1 (C# 7.3) · **Art:** HD-2D (16 px pixel art drawn at 4× onto 64 px cells, in a 3D world) · **Render:** built-in pipeline, forward, plus Post Processing 3.0.3
**Third-party art:** CyberCity by warlloyd (CC0). See `Assets/_Project/Art/ThirdParty/CyberCity/CREDITS.md`.

---

## Where we are

### ✅ Phase 0: Combat core (done)
- A card-merge battle system: 7-card hand, adjacent merges up to rank III, 3 AP per round, Moxie that spawns a Ritual card, enemy intents, statuses, Afflatus advantage.
- Data-driven content: Cards, Characters (with an `archetype`/class), Enemies, Statuses and Cast Sequences are all ScriptableObjects. New effects and animation steps are single subclasses.
- Logic (`BattleController`) is separate from the view (`BattleView`), which replays `BattleEvent`s.
- Slay the Spire-style hover preview, play-slot frames, pastel Ritual cards and big damage numbers.
- An EditMode test suite covers merges, Moxie, AP, damage and statuses.
- The main party is Miku (Cantor), Teto (Bruiser) and Rei (Android Medic). Enemies and NPCs are animated 32×48 LPC sprites (`SpriteAnimationSet`: idle, walk, slash, spellcast, hurt) built by `Tools/SpriteForge/lpc_compose.py`. Hovering a unit in battle opens a menu of its buffs, debuffs and intent. Holding a unit opens a full character sheet with stats, effects and abilities. The battle has a painted Rainline Station backdrop (`BattleBackdropDefinition`) and a pause menu (Esc or the top-right button) with Resume, Restart and Leave. The intro fight is tuned to last about 6–8 rounds.

---

## Architecture target

```
Boot scene ──► GameSession (DontDestroyOnLoad)
                 ├─ PartyRoster   (members: definition, level, xp, current HP)
                 ├─ Inventory     (items, shards)
                 ├─ WorldState    (story flags, defeated trainers, opened chests)
                 └─ PlayerLocation(scene, cell, facing)

District scene (Tilemaps) ──step──► EncounterZone ──► BattleRequest ──► Battle scene
        ▲                                                                   │
        └────────────── BattleResult (victory, HP left, xp, loot) ◄─────────┘
```

**Assemblies**
- `Combat.Runtime` exists already. It has no world knowledge and must stay that way.
- `Game.Core` is new: GameSession, party, inventory, save/load, scene flow. It references Combat.
- `Game.World` is new: grid movement, tilemaps, encounters, NPCs, dialogue. It references Core.
- `Game.UI` is new: menus, dialogue box, transitions.

**Key rule:** Combat never references the overworld. The overworld talks to combat only through a `BattleRequest` it sends in and a `BattleResult` it gets back.

---

## Roadmap

### Phase 1: Combat ready for the RPG
| Task | Notes |
|---|---|
| `BattleRequest` / `BattleResult` API | The party arrives with persisted HP, level and an optional "can flee" flag. The result returns victory, per-member HP, XP and loot. |
| Level scaling | `CharacterDefinition.growth` gives stats per level, and BattleUnit takes level and starting HP. |
| Results screen | Shows XP gained and loot, with a "Continue" button that returns to the overworld. The current "Fight again" stays for test mode. |
| Flee button | Has a chance-based escape and is disabled in boss fights. |
| Terminology pass | Display names from the Bible §6 (Resonance, Charge…). Keep the enum values and just add display labels. |
| Second card set per character | Put the card system through its paces with more effects: Shield, Cleanse, Draw, Rank-up-a-card. |
| Balance sim (optional) | An editor tool that auto-plays N battles headless and reports the win rate and average rounds. |

### Phase 2: Overworld (HD-2D, Octopath-style)
**Direction change:** the overworld is now **HD-2D** like *Octopath Traveler II* rather than flat Pokémon tiles. Pixel-art billboard characters stand in a 3D block world built from a text map and textured with pixel art. Neon point lights, fog, bloom, depth of field and grain complete the look.

**✅ 2a, prototype (done):** `Tools > Overworld > Generate Rainline Station`
| Done | Notes |
|---|---|
| Text-map level | `OverworldBuilder.map` (editable in the inspector). `WorldMeshBuilder` turns it into one mesh textured from the CyberCity atlas. |
| Free movement | 8-way movement with Shift to run, collision against the map grid, and 4-direction walk animation (`PlayerController`, `CharacterSprite`). |
| HD-2D camera | Narrow FOV with a pitched follow camera (`OverworldCamera`), and depth-of-field focus that tracks the player (`PostFxFocus`). |
| Atmosphere | Neon lamps (real point lights), emissive signs, rain, Glimmer sparkles, fog, and a Post Processing 3.0.3 profile. |
| Interaction | `Interactable` base class, with `TalkInteractable` (NPC lines) and `EncounterInteractable` (stand on the glowing square and press **F**). |
| Battle round trip | `BattleLauncher` hands off to the Battle scene and returns with the result. `WorldSession` remembers position and defeated encounters. |

**Still to do**
| Task | Notes |
|---|---|
| Dialogue upgrade | A `DialogueDefinition` ScriptableObject (lines, speaker, portrait) with a typewriter effect, replacing the hard-coded NPC lines. |
| Warps | Door cells that load another scene at a spawn point, with a fade or glitch transition. |
| Map tooling | Several layers (decor, overhead), slopes and stairs, and props (crates, vending machines, fences). Possibly move from text maps to a ScriptableObject map or Tilemap authoring. |
| Collision polish | Per-prop collision radii and NPC wandering. |
| Shader include list | Add `Sprites/Diffuse` and `Legacy Shaders/Particles/Additive` to *Always Included Shaders* before making player builds. |

### Phase 3: Encounters and scene flow
| Task | Notes |
|---|---|
| `EncounterTable` ScriptableObject | Weighted enemy groups with a level range, plus a per-step encounter chance for the zone. |
| Visible enemies | They patrol, and touching one starts the fight. Each one is flagged defeated in WorldState. |
| Battle transition | Save the player's location, play the transition, load the Battle scene, run the fight, apply the result, then reload the district at the saved spot. |
| Defeat handling | Respawn at the last Glimmer Shrine with the crew restored and some Shards lost. |
| Story battles | A `BattleRequest` triggered from dialogue, with custom enemies and no fleeing. |

### Phase 4: Party and progression
| Task | Notes |
|---|---|
| Recruiting | Story flags add members to the PartyRoster. |
| Formation | 3 active members out of up to 4, swapped in the pause menu. |
| XP and level-ups | XP curve, stat growth, and a level-up popup on the results screen. |
| Card upgrades | Spend Shards to raise a card's base numbers or unlock rank bonuses. |
| Mods | One equippable passive per member, made as a new `PassiveEffect` hooked into battle events. |

### Phase 5: Menus and save
| Task | Notes |
|---|---|
| Pause menu | Crew (stats and cards), Items, Save, Options. |
| Save/Load | JSON in `Application.persistentDataPath`. Assets are referenced by stable IDs through a `ContentDatabase` ScriptableObject that lists every character, card and item. |
| Title screen | New game, continue, options. |

### Phase 6: Art and juice pass
| Task | Notes |
|---|---|
| Overworld art | 16×16 tilesets for each district, plus character walk cycles (16×24 or 16×32). |
| Battle art | Replace the uGUI placeholder portraits with 64×64 pixel sprites and idle animations. Reskin cards as holo-tarot. |
| Neon lighting | Additive glow sprites and flickering-sign animations, plus **Post Processing v2** (`com.unity.postprocessing`) bloom. Avoid the experimental 2D Renderer in URP for 2019.4. |
| Atmosphere | Rain overlay, Glimmer sparkle particles, puddle reflections. |
| Pixel fonts | Swap Arial for an OFL or CC0 pixel font, checking the license. |
| Audio | Music per district, card cast sounds, Ritual stingers. |

### Phase 7: First playable
- District 1 (Rainline Station), from the tutorial through to the boss: about 20–30 minutes of play.
- 3 recruitable crew members, about 8 enemy types and 1 boss.
- A full loop working end to end: explore → encounter → battle → level up → save → boss.

### Later
Districts 2–6, more crew classes (Glitchwitch, Courier, Archivist), side quests, shops in the Drowned Market, controller support, and a build pipeline.

---

## Immediate next steps
1. ~~Overworld → battle → overworld loop~~ ✅. It now works through `BattleLauncher` and `WorldSession`.
2. Carry party HP between fights and add levels (Phase 1). `WorldSession` grows into `GameSession`.
3. Add a warp to a second small map (Neon Row), plus a data-driven dialogue box.
4. Add random-encounter zones (Phase 3), reusing `BattleLauncher`.

## Conventions
- All content is a ScriptableObject in `Assets/_Project/Content/...`, and code goes in `Assets/_Project/Scripts/<Assembly>/...`.
- Logic emits events and views animate them. Keep game rules testable without scenes.
- C# 7.3 only: no switch expressions, records or `init`.
- Each phase ends with its EditMode tests passing and a manual play-through checklist.
