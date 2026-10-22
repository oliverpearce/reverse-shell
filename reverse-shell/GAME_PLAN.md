# GAME PLAN (working title *NEON HEX*, renaming pending)

This is the technical roadmap. For the creative vision, see [GAME_BIBLE.md](GAME_BIBLE.md).
**Engine:** Unity 2019.4.14f1 (C# 7.3) · **Art:** HD-2D (16 px pixel art drawn at 4× onto 64 px cells, in a 3D world) · **Render:** built-in pipeline, forward, plus Post Processing 3.0.3
**Look:** an overgrown, sunlit city ruin in the spirit of *NieR:Automata* (changed from neon cyberpunk on 2026-10-22).
**Art:** tiles are our own (`Tools/TileForge/ruins_atlas.py`, written to `Art/Tiles/ruins.png`). Characters are LPC (CC-BY-SA/GPL, credited). CyberCity (CC0) remains only as a fallback sprite source.

---

## Where we are

### ✅ Phase 0: Combat core (done)
- A card-merge battle system: 7-card hand, adjacent merges up to rank III, 3 AP per round, Moxie that spawns a Ritual card, enemy intents, statuses, Afflatus advantage.
- Data-driven content: Cards, Characters (with an `archetype`/class), Enemies, Statuses and Cast Sequences are all ScriptableObjects. New effects and animation steps are single subclasses.
- Logic (`BattleController`) is separate from the view (`BattleView`), which replays `BattleEvent`s.
- Slay the Spire-style hover preview, play-slot frames, pastel Ritual cards and big damage numbers.
- An EditMode test suite covers merges, Moxie, AP, damage and statuses.
- The main party is MC (Prototype, the leader; a standout robot that will channel several elements), Heal Bot (Earth medic) and Lucy (Virus hacker). Heal Bot and Lucy are one robot model with different accents. The enemies are machine lifeforms (Rust Sentry, Scrap Stalker). Enemies and NPCs are animated 32×48 LPC sprites (`SpriteAnimationSet`: idle, walk, slash, spellcast, hurt) built by `Tools/SpriteForge/lpc_compose.py`. Hovering a unit in battle opens a menu of its buffs, debuffs and intent. Holding a unit opens a full character sheet with stats, effects and abilities. The battle has a painted Mossline Station ruin backdrop with drifting leaves (`BattleBackdropDefinition`) and a pause menu (Esc or the top-right button) with Resume, Restart and Leave. The intro fight is tuned to last about 6–8 rounds.

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
| `BattleRequest` / `BattleResult` API | ✅ The request carries each member's `MemberProgress` (level, XP, HP), and the result returns the outcome, XP and the updated progress with level-ups. Still to do: a "can flee" flag and loot. |
| Level scaling | ✅ `CharacterDefinition.growthPerLevel` (default +8% per level for HP, ATK and DEF). `BattleUnit.SetLevel`, with the curve in `Leveling`. |
| Results screen | ✅ "+XP", a row per member with level, LEVEL UP! and XP to next, then Continue (or Fight Again in test mode). Loot comes later. |
| Save file | ✅ Moved up from Phase 5: `SaveGame` stores party progress and beaten encounters as JSON, saving after battles and at Access Points. |
| Flee button | Has a chance-based escape and is disabled in boss fights. |
| Terminology pass | Display names from the Bible §6 (Resonance, Charge…). Keep the enum values and just add display labels. |
| Second card set per character | Put the card system through its paces with more effects: Shield, Cleanse, Draw, Rank-up-a-card. |
| Balance sim (optional) | An editor tool that auto-plays N battles headless and reports the win rate and average rounds. |

### Phase 2: Overworld (HD-2D, Octopath-style)
**Direction change:** the overworld is now **HD-2D** like *Octopath Traveler II* rather than flat Pokémon tiles. Pixel-art billboard characters stand in a 3D block world built from a text map and textured with pixel art. Sunlight with soft shadows, haze, gentle bloom, depth of field and a warm grade complete the look.

**✅ 2a, prototype (done):** `Tools > Overworld > Generate Mossline Station` (was Rainline Station before the retheme)
| Done | Notes |
|---|---|
| Text-map level | `OverworldBuilder.map` (editable in the inspector). `WorldMeshBuilder` turns it into one mesh textured from the ruins atlas. |
| Free movement | 8-way movement with Shift to run, collision against the map grid, and 4-direction walk animation (`PlayerController`, `CharacterSprite`). |
| HD-2D camera | Narrow FOV with a pitched follow camera (`OverworldCamera`), and depth-of-field focus that tracks the player (`PostFxFocus`). |
| Atmosphere | ✅ **Retheme:** warm sun with soft shadows, trilight ambient, haze fog, trees, bushes, rubble, swaying grass and flowers, vines on walls, sun shafts with dust motes, drifting leaves, and a Post Processing 3.0.3 profile (soft bloom, DOF, warm desaturated grade). |
| Interaction | `Interactable` base class, with `TalkInteractable` (NPC lines), `EncounterInteractable` (stand on the red square and press **F**) and `AccessPointInteractable` (restores the party's HP). |
| Party HP | ✅ HP carries between fights through `WorldSession.PartyHP`, and the overworld shows it top left. A defeat sends the party back at full HP. |
| Battle round trip | `BattleLauncher` hands off to the Battle scene and returns with the result. `WorldSession` remembers position and defeated encounters. |

**Still to do**
| Task | Notes |
|---|---|
| Dialogue upgrade | A `DialogueDefinition` ScriptableObject (lines, speaker, portrait) with a typewriter effect, replacing the hard-coded NPC lines. |
| Warps | Door cells that load another scene at a spawn point, with a fade or glitch transition. |
| Map tooling | Several layers (decor, overhead), slopes and stairs, and props (crates, vending machines, fences). Possibly move from text maps to a ScriptableObject map or Tilemap authoring. |
| Collision polish | Per-prop collision radii and NPC wandering. |
| Shader include list | Add `Sprites/Diffuse`, `Legacy Shaders/Particles/Additive`, `Legacy Shaders/Particles/Alpha Blended` and `Legacy Shaders/Transparent/Cutout/Diffuse` to *Always Included Shaders* before making player builds. |

### Phase 3: Encounters and scene flow
| Task | Notes |
|---|---|
| `EncounterTable` ScriptableObject | Weighted enemy groups with a level range, plus a per-step encounter chance for the zone. |
| Visible enemies | They patrol, and touching one starts the fight. Each one is flagged defeated in WorldState. |
| Battle transition | Save the player's location, play the transition, load the Battle scene, run the fight, apply the result, then reload the district at the saved spot. |
| Defeat handling | Respawn at the last Access Point with the crew restored and some Parts lost. (Today the party just regroups at full HP where it stood.) |
| Story battles | A `BattleRequest` triggered from dialogue, with custom enemies and no fleeing. |

### Phase 4: Party and progression
| Task | Notes |
|---|---|
| Recruiting | Story flags add members to the PartyRoster. |
| Formation | 3 active members out of up to 4, swapped in the pause menu. |
| XP and level-ups | XP curve, stat growth, and a level-up popup on the results screen. |
| Card upgrades | Spend Parts to raise a card's base numbers or unlock rank bonuses. |
| Parts | ✅ Four internal slots per member (WPN / ARM / CHIP / CORE) with stat modifiers, a saved inventory and starter Parts. Next: Parts as loot, and passive effects (a new `PassiveEffect` hooked into battle events). |
| Deck swap | ✅ Each member has a card pool; the crew menu swaps cards into the 2 deck slots. |

### Phase 5: Menus and save
| Task | Notes |
|---|---|
| Pause menu | ✅ Crew: the flip-card `CharacterMenu` (stats, deck, Parts, leader). Still to do: Save and Options. |
| Save/Load | JSON in `Application.persistentDataPath`. Assets are referenced by stable IDs through a `ContentDatabase` ScriptableObject that lists every character, card and item. |
| Title screen | New game, continue, options. |

### Phase 6: Art and juice pass
| Task | Notes |
|---|---|
| Overworld art | A TileForge atlas per district (same 8×5 layout), plus more props. |
| Battle art | Optional sepia NieR-style pass on the battle UI to match the overworld. |
| Lighting | Time of day per district (dawn haze, noon, dusk). Avoid the experimental 2D Renderer in URP for 2019.4. |
| Atmosphere | Birds, butterflies, water in the Sunken Arcade. |
| Pixel fonts | Swap Arial for an OFL or CC0 pixel font, checking the license. |
| Audio | Music per district (piano, strings, wordless vocals), card cast sounds, Ritual choir stingers. |

### Phase 7: First playable
- District 1 (Mossline Station), from the tutorial through to the boss: about 20–30 minutes of play.
- 3 recruitable crew members, about 8 enemy types and 1 boss.
- A full loop working end to end: explore → encounter → battle → level up → save → boss.

### Later
Districts 2–6, more crew classes (Hacker, Scout, Archivist), side quests, a Resistance camp with shops, controller support, and a build pipeline.

---

## Immediate next steps
1. ~~Overworld → battle → overworld loop~~ ✅. It now works through `BattleLauncher` and `WorldSession`.
2. ~~Carry party HP between fights~~ ✅, Access Points ✅, levels, XP and results screen ✅, save file ✅. Next: Flee and loot (Phase 1), then defeat respawning at the last Access Point.
3. Add a warp to a second small map (the Sunken Arcade or a Resistance camp), plus a data-driven dialogue box.
4. Add random-encounter zones (Phase 3) in the tall grass (`,`), reusing `BattleLauncher`.

## Conventions
- All content is a ScriptableObject in `Assets/_Project/Content/...`, and code goes in `Assets/_Project/Scripts/<Assembly>/...`.
- Logic emits events and views animate them. Keep game rules testable without scenes.
- C# 7.3 only: no switch expressions, records or `init`.
- Each phase ends with its EditMode tests passing and a manual play-through checklist.
