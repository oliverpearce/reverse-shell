# HANDOFF: where we left off (2026-10-22, session 4)

> **Sprites (newest):** the crew and NPCs come from **`Tools/ChibiForge/girl_model.py`**, using the user's 6-frame front-view walk cycle (`reference/Girl_walk.png`, native 64 px).
> - **Walk:** the drawn cycle (7 frames, frame 0 stands).
> - **Other animations:** idle is a breathing dip; slash is a lunge with an arc in the member's colour; cast grows an orb at the hands; hurt ends lying down.
> - **Directions:** left and right reuse the front frames (no side views were drawn); up is a generated back view.
> - **Recolours (`SPECS`):** hair; glowing eyes for the crew; outfits for the NPCs.
> - **Superseded:** the robot model and the mech-arm base model are no longer used.
> **Sprites (previous):** the crew and NPCs come from **`Tools/ChibiForge/robot_model.py`**, which uses the user's clean 64 px sprite `reference/Robot.png`. It's a 51×62 knight with a white helmet, white plate, a cape and a sword, used at native resolution with no scaling.
> - **Crew (robots):** glowing eyes in the member's colour, a collar light, a chest seam and core, an ear module, and mech joints instead of bare skin. Hair, eyes and cape are recoloured per member (`SPECS`).
> - **NPCs:** the same sprite as humans, unarmed, with their own hair and cape colours.
> - **Animation:** the cape, sword, legs and body are animated as layers. The art faces right, so left is the mirror and up is the back view.
> - **Machine enemies:** the rig draws them back at native 64 px (`F = 64`, `RIG_K = 1`), so every sprite shares one pixel density.
> - **Superseded:** the mech-arm base model (`base_model.py`, `Base.png`) is no longer used. The notes below describe that earlier attempt.
> **Crew in-game sprites (newest):** the crew's overworld and battle sprites are animated from the user's **base model**, `Tools/ChibiForge/reference/Base.png`: a girl with cat ears, a crop top, shorts, thigh-highs and a mech backpack with two big mechanical arms.
> - **Source:** the art was extracted from an upscaled, compressed image by `Tools/ChibiForge/extract_reference.py` (flood-fill background removal, a 32-colour palette, majority-vote resampling to 112 px tall).
> - **Animation:** `Tools/ChibiForge/base_model.py` cuts it into layers (two mech arms, two legs, the body) and animates them: idle breathing; a walk with stepping legs, body bob and mech sway; a mech-claw swipe; a cast with the arms raised and an orb; and a defeat that ends lying down.
> - **Directions:** down and left use the art as drawn, right is mirrored, and up has the face painted over with hair.
> - **Frames:** **128 px at full detail** (`OUT` in `base_model.py`; 64 halves them). Darks are lifted with gamma 0.85. The rig draws the machines at 2× (`RIG_K`) into the same 128 px frames, so every sprite shares one pixel density. `AnimSet` reads the frame size from the sheet height.
> - **Overworld visibility:** character sprites use a **steady light** (`WorldArt.SpriteLit` is now the unlit `Sprites/Default`) tinted with `WorldArt.SpriteTint` (a warm haze). They don't darken in the dappled shade; their blob shadows stay.
> - **NPCs** (noodle vendor, courier, drifter) use the same base model without the mech gear (`strip_backpack`, no mech-arm layers), with their own hair, shirt and shorts colours (`PEOPLE` in `base_model.py`). Only the machine enemies still come from the rig.
> - **Recolours:** Heal Bot and Lucy hue-shift the hair, eyes and shorts (green / purple). The card art is still the knight portraits. The rig (`chibi_forge.py`) still draws the enemies and NPCs.
> **Crew look (newest):**
> - **Card art:** every crew card is the reference knight. MC is the original; Heal Bot and Lucy are regional recolours of it (`recolour_map`): hair above the shoulders, armour whites, trim (which also colours the eyes and the sword), accent and mane, with the face kept as is.
> - **In-game sprites:** the crew share one design, recoloured: **Octopath-like proportions** (about three heads tall, not chibi), **plate armour head to toe with trim** in each member's colour (cuffs, pauldron rims, belt, knees, boot tops), eyes in the trim colour, a cape and a greatsword. There is **no crown** (`headgear='ornament'`). The NPCs use the same proportions.
> **MC's look (latest):** MC is the chibi knight from the user's reference sprite (`Tools/ChibiForge/reference/MC.png`, 48×63, 16 colours: slate hair with a dark mane, a gold crest, white plate with gold and crimson, a golden greatsword). Its card art is that sprite at 2×, and the opened card shows its chest. The rig matches the reference's palette (`REF_*` ramps, outline #20021D) and its chunkier proportions. The other party portraits are drawn at the same pixel density (48×72 doubled). The reference's licence isn't recorded; see its README.
> **Crew-menu stats (latest):** Persona 5-style rows on a white slab in the top right: a big condensed label and value, "MAX" when full, and a long red bar with the Parts bonus in gold.
> **Art style (latest):** everything is **chibi pixel art** in the style of the user's reference: big heads, a bold plum outline, 4-tone ramps. The party are androids with faces, hair, armour and element-coloured glowing lines. See `Tools/ChibiForge`. Older notes below that describe LPC sprites, helmets or visors are out of date.

Read this first when resuming. See also [GAME_PLAN.md](GAME_PLAN.md) (roadmap) and [GAME_BIBLE.md](GAME_BIBLE.md) (vision).

## Project at a glance
- **Engine:** Unity **2019.4.14f1**, C# 7.3 only (no switch expressions or records). Project folder: `reverse-shell/`.
- **Game:** a non-commercial fan project (working title *NEON HEX*; a new title is pending, see the Bible). It has an HD-2D overworld (Octopath-style) and Reverse: 1999-style card-merge battles.
- **Look (new this session):** an **overgrown, sunlit city ruin** in the spirit of *NieR:Automata*, replacing the neon cyberpunk city.
- **Party (new in session 4):**
  - **MC** (the Prototype, the leader) is a standout robot model: a dark trench coat, a smooth plated head with **white glowing eyes** and a halo crest. It glows **white** for now. It is prepared to **channel several elements** later, with its hue following the element (`CharacterDefinition.channels`, `neutralGlow`, `glowColor`, `AfflatusInfo.GlowFor`).
  - **No blindfolds:** every robot has two glowing eyes in its element color and **scale-pattern armour trims**. In the portraits that's `eyes()` / `scales()` in PortraitForge. On the LPC sprites, a smooth `hair_buzzcut` plate replaces the visor helmets, with custom eye ramps `eye_moss` / `eye_virus` / `eye_white`, the `eyes:#hex` mask and the `trim:#hex` overlay; `'overlay'` can be a list.
  - **Heal Bot** (Earth, Medic) and **Lucy** (Virus, Hacker) are the **same robot model** with different accents. Heal Bot is overgrown like a dryad (vines, leaves, flowers, a sapling) with a green glow. Lucy has cable twin-tails, circuit traces and glitch rows, with a purple glow.
  - 2B, V1 and V2 were retired. Old saves migrate (`SaveGame.Migrate`: Char_2B to Char_MC, Char_V1 to Char_HealBot, Char_V2 to Char_Lucy).
  - **Kits:** MC has Edge Strike and Sync Field, pool card Cross Slash, Ult Prism Burst. Heal Bot has Sap Mend and Bramble Lash, pool card Overgrowth, Ult Verdant Bloom. Lucy has Virus Spike and Corrupt (new ATK Down status), pool card Worm, Ult Zero Day.
- **Elements:** the six afflatus values stay (the advantage wheel is unchanged), but Plant is shown as **Earth** and Intellect as **Virus** (`AfflatusInfo.Display`).
- **Enemies:** machine lifeforms, the **Rust Sentry** and the **Scrap Stalker**.
- **Packages added:** `com.unity.postprocessing` **3.0.3**, the newest version that works on 2019.4.14.

## How to run
1. Run **Tools → Combat → Generate Sample Content**. It rebuilds characters, cards, statuses, animations, the backdrop and `Scenes/Battle.unity`, and deletes the retired Miku/Static assets.
2. Run **Tools → Overworld → Generate Mossline Station**. It builds `Scenes/Overworld_Mossline.unity` and sets it as the Play start scene. **Tools → Overworld → Always Play From Overworld** turns that on or off.
3. Press Play. Walk with WASD/arrows, hold Shift to run, and press **F** on the red square to fight. The terminal next to the spawn is an **Access Point**: press **F** there to restore HP.
   - The red pylon in the plaza is a **Summon Beacon** (`R` on the map). After a yes/no confirmation it brings every beaten machine in the area back, for grinding (`SummonInteractable`, `OverworldBuilder.ResummonEncounters`).
   - **Esc** opens the **crew menu** (`CharacterMenu`).
     - **Front:** each member is a **character card** (Reverse: 1999 look): pixel portrait, element glow, serif name, element and class, a big Lv number and a slim HP bar. **←/→** swap cards, **L** makes the shown member the leader, and **Enter/F** (or a click) **flips** the card.
     - **Back** (Enter flips the card; it **opens on the left**): the robot's internals at 4x with the **item sockets (CHIP and CORE)** pinned on. Weapon and armor slots are shelved; `PartSlot` keeps the numbers 2 and 3, so saves stay compatible. Per-character spots are in `CharacterDefinition.partSockets`.
       - **Top right:** name, element and class, the **level inside a ring that fills with XP** (no XP bar), and HP with numbers. Then six **Persona-style stat bars**: label, big value and a thick bar whose Part bonus continues in gold. The bars are scaled so MC's level-cap stats fill **90%** (`Leveling.BarMax`, `CharacterDefinition.statBarReference`; a faint tick marks 90%). Below them are the CHIP and CORE slots.
       - **Bottom:** every ability card in one row as real card faces (`AbilityCard` at 1.5×, scroll to flip rank): Deck, Ult and Spare. Click a deck card to pick its slot, then a spare to swap it in.
       - **↑/↓** move the gold focus over the sockets and deck cards, and **Enter** opens a picker. **Esc** flips back, then closes.
       - Everything stays within x = ±820, because a 4:3 view only shows about 1740 of the 2400-wide layout (Shrink matching **crops** the width). Keep that in mind for any full-screen UI.
     - **Carousel:** one continuous scroll value glides with `SmoothDamp` (0.16 s, no overshoot). Cards scale, dim and drop smoothly with their distance from the centre, and fade out before wrapping round.
     - (A side-panel layout and a pixel status-screen layout were tried and dropped at the user's request.)
     - The game pauses while the menu is open.
   - Dialogue can ask questions: `DialogueBox.Ask(line, options, onChoice)` shows stacked label choices (Up/Down pick, F/Enter confirm, Esc = the last option).
   - `OverworldBuilder.useDefaultMap` (on by default) builds from `DefaultMap` in code, so map changes show up without regenerating the scene. Turn it off to use the map typed into the inspector.
- **Progress is saved to disk** (`save.json` in `Application.persistentDataPath`): each member's level, XP and HP, plus beaten encounters. It saves after every battle and at the Access Point, and loads when Play starts. **Tools → Overworld → Delete Save Data** starts fresh; you need it to fight the plaza machine again once it's beaten.
- The party HUD (top left) has one row per member: the color line, the name, a big HP bar and a small "Lv. N" beneath. The leader (MC) gets the largest row, with a soft gradient behind it (the crown icon was removed). A defeat sends the party back at full HP. Restarting from the pause menu restarts from the same levels and HP.
- **Level cap: 30** (`Leveling.MaxLevel`).
- **Levels and XP:** winning gives every member the enemies' XP (Rust Sentry 120 and Scrap Stalker 80, so 200 a fight). Lv 2 needs 100 XP, then 200, 300 and so on. Each level adds 8% base HP, ATK and DEF. The result screen shows "+XP", each member's level, "LEVEL UP!", and XP toward the next level. The battle nameplates show "Lv N".
- **UI theme (new in session 4): Reverse: 1999-style** (`InkStyle`). It replaces the Persona look; `PersonaStyle` and `PartyMenu` were deleted.
  - **Colors:** ink panels (#1B1A18), cream paper text (#ECE5D3) and muted **gold** (#C2A061) for numbers, ranks, AP and hairline rules. HP bars are green (party) / red (enemies), and the profile posters stay Psycho-Pass cyan.
  - **Shapes:** straight panels with 1–2 px gold hairlines, the **hero frame** (a double rule with corner ticks) for key pieces, and subtle paper grain. No slants, tilts, halftone or burst rays. **Type:** serif headings in **Cormorant Garamond** (`UIFactory.SerifFont`, OFL), letter-spaced caps labels (`InkStyle.Label`), and M PLUS for body text. Anton remains only for the dialogue speaker tag and the cards' kind word.
  - Restyled: the battle HUD (Turn plate, Go/Undo, queue, banner, result screen), the pause menu, the overworld HUD, prompt and area title, the button hover (a gold hairline) and the inspect-screen headers. The dialogue box, profile posters, reticles and cards are unchanged on purpose.
  - **Unit nameplates** are plain M PLUS with a soft shadow. Cards have no owner badge (it was removed).
- **Card text** joins effects that hit the same target into one sentence, naming the target once (`CardText.Sentences`, `CardEffect.Joinable/Clause`): "Deal 120% ATK damage and poison 1 enemy (x1, 2 turns)." Statuses can set a `verb` (Poison: "poison"); otherwise they read "give 1 enemy ATK Up (2 turns)". Different targets stay separate sentences.
- **No overworld blur:** depth of field is gone from the post-processing setup, and `PostFxFocus` switches it off in older profiles.
- **No overworld glow / blur (session 4):** the soft rim (eight offset copies) blurred overworld sprite edges, so it was removed from `CharacterSprite`. Film grain is off, and bloom only catches real light sources (threshold 1.6). `PostFxFocus` applies both at runtime to older profiles too. Battle sprites keep their rim (`UnitView`, through `AfflatusInfo.GlowFor`).
- In battle:
  - Battle sprites are bigger: 64 px frames at 4× (256 px).
  - **Hovering a card** rings everyone it would affect in orange (`BattleController.PreviewTargets`, tested): the targeted enemy, all enemies, the team for team buffs, the caster for self buffs, or the weakest ally for heals. Effects that pick their own targets count too. Random picks show all candidates, and the preview never touches the RNG.
  - Every card has its **owner's face badge** bottom-right, above the kind word (`CardView.OwnerBadge`), as a second cue besides the color.
  - Click a card to queue it (1 AP). Drag a card to move it and merge (1 AP).
  - Click an enemy (or press **Enter** on the ringed one) to target it. The target gets a red **Blue Archive-style lock-on**: four spinning arcs, inward ticks and a center diamond. The unit under the cursor gets a white segmented focus ring (three long arcs plus a counter-spinning dashed ring) that fits around it. Both snap in like a hit marker. The rings are smooth, anti-aliased 256 px textures drawn in code (`TargetReticle`, `Reticle`, `Spin`).
  - **Arrow keys** move the white ring: Left/Right step through every unit, Up/Down jump between the party and the enemies. **Enter** targets the ringed enemy. **Space** inspects the ringed unit (the same as holding it), and closes the inspect screen.
  - The reticles each sit on a soft dark shadow for contrast and carry a blurred glow that spins with their arcs.
  - **Cards** are Reverse 1999-style (`CardView.BuildFace`). The small hand card shows only the rank (top-left) and one huge ATTACK / BUFF / DEBUFF / HEAL word along the bottom; the middle is a clean solid color (art comes later). The hover preview and the zoom-in cards add the name header, owner and full text.
  - **Card rank** is **stacking stars** in the top-left (`CardStars`): rank I none, rank II one star, rank III two stars, Ritual (ult) cards three. Stars also show on the battle queue slots. The pause menu toggles **CARD RANK: STARS / PIPS** (`CardSettings.RankStars`, saved in PlayerPrefs). The hand and queue rebuild live. The earlier LoL-style borders were removed at the user's request.
  - The zoom-in screen's **ABILITIES** section shows each ability as the real card, at 1.6× (`AbilityCard`). Scroll over a card to flip its rank; the stars and text change.
  - **UI font:** all text is **M PLUS 1p**, a sleek Japanese gothic close to NieR:Automata's menus. Light is the default; `FontStyle.Bold` maps to the real Medium weight and BoldAndItalic to Bold (`UIFactory.CreateText`). Card text shows every number and percentage bigger, bold and gold (`UIFactory.EmphasizeNumbers` + `FitRich`, a rich-text-aware best fit).
  - **Profile posters** (`ProfilePoster`): Psycho-Pass-inspired holographic "virtual wanted posters".
    - Contents: dark teal glass with a grid, a TARGET PROFILE / PERSONNEL FILE header with an ID code, a cyan-tinted portrait behind scrolling scanlines and corner brackets (with an occasional glitch), the name in caps, a threat index with a cyan / amber / red gauge, a barcode, and a WANTED (enemy) or CLEARED (ally) stamp.
    - Where: the full poster is the zoom-in screen's portrait; a compact poster is the dialogue portrait.
  - Dragging a card onto a merge partner shows just a gold frame (the "MERGE -> II" tag was removed).
  - Battle HP bars show **no numbers**; exact HP is only in the inspect screen. Nameplates show the level small ("Lv 3").
  - **Hold** a unit (or press Space) to open its details screen, the only place for full info: next move, effects (buffs/debuffs), stats and abilities. The hover info menu (`UnitInfoPanel`) was removed at the user's request.
  - **Enemy intents** are **mini cards** above the head (`CardIcons.IntentCard`): the type icon on a cream (rank I), silver (II) or gold (III) card, or a dark arch card for an Ult.
  - **Card type icons:** every card shows its type icon in the **top-right corner**. The types are Attack (a katana), Debuff (a hexagon with down chevrons), Buff (a hexagon with up chevrons), **Counter** (a cog with a reflecting arrow; `CardKind.Counter` is new, with no mechanic yet), Heal (a seedling with a plus) and Ultimate (a radiant core star in a ring). The kind colors now match the icons, so Buff is blue.
  - **Icon atlas:** `Resources/UI/CardIcons.png` is painted by `python Tools/IconForge/card_icons.py` and read through `CardIcons`: glyphs (white and colored), intent cards in 3 ranks, and UI bits (empty diamond, Ult pips, star, card back). See `Resources/UI/README.md`.
  - **Queue:** the cards you've queued sit in the **middle, right above the hand** (`HudView`), so you can see what you're playing.
  - The battle nameplates show only the name, with no level; levels are in the crew menu.
  - The party is centred at x = -420 (`BattleView.PartyCenterX`), so the leftmost member fits a 4:3 view.
  - **Under each HP bar:** the **Ult points** as small diamonds (gold, orange when full) and, to the left of the bars, an **empty diamond** reserved for unit-type icons (enemy and ally types, later).
  - UIFactory buttons never keep keyboard focus, so Space and the arrows are free for game controls.
  - **Esc** or the top-right **II** opens the pause menu (Resume, Restart, Leave). **Leave Fight** always works. Launched from the overworld, it returns there as a retreat. Opened directly, it loads `BattleBootstrap.leaveScene` (`Overworld_Mossline`), or stops Play mode if that scene isn't in Build Settings.
  - Every `UIFactory` button has hover feedback (`ButtonHover`): it grows slightly, brightens and gets a gold border. This runs on unscaled time, so it works while paused.
- Turn on Unity's **Recompile After Finished Playing** setting, or scripts edited during Play wipe the battle's state. The game recovers by rebuilding itself.

## Dev tools (no Unity needed)
| Command | What it does |
|---|---|
| `bash Tools/DevCheck/compile_check.sh` | Compiles all 5 assemblies with Unity's Roslyn (C# 7.3) |
| `bash Tools/DevCheck/BackdropPreview/preview.sh` | Renders the battle backdrop painter to a PNG (uses the ruins atlas) |
| `python Tools/ChibiForge/chibi_forge.py [Names...] [--sheet out.png]` | **New (main art tool).** Draws **every character** as chibi pixel art from one rig (`SPECS`): the animated sheets in `Art/Characters/<Name>/` and the party's card portraits in `Art/Portraits/`. The party are androids (faces, hair, armour, glowing lines), the enemies are round machines and the NPCs are survivors. It replaces the LPC composer and PortraitForge for these characters. |
| `python Tools/IconForge/card_icons.py [--preview out.png]` | Paints the card-type icon atlas `Resources/UI/CardIcons.png` as **pixel art** matching the sprites (smooth shapes reduced to a 32 px grid per cell, plum outline). |
| `python Tools/PortraitForge/portrait_forge.py [--sheet out.png]` | **New.** Paints the character-card portraits (front, internals and glow masks, 96×144) into `Art/Portraits/`. Heal Bot and Lucy share `robot_base`; MC has its own model. |
| `python Tools/TileForge/ruins_atlas.py` | **New.** Repaints the overworld tile atlas `Art/Tiles/ruins.png` (our own art) |
| `python Tools/SpriteForge/lpc_compose.py [Names...]` | Rebuilds LPC character sprites from `RECIPES` (`MC`, `HealBot`, `Lucy`, `RustSentry`, `ScrapStalker`...). A layer with a `'mask'` isn't drawn; it marks the eye rows for a painted band (`'blindfold'`, or `'visor:#RRGGBB'` for a glowing visor). A recipe's `'overlay'` (`overgrowth`, `glitch`) paints per-frame decorations anchored to the body (`apply_overlay`). |
| `python Tools/SpriteForge/preview.py out.png MC HealBot Lucy` | Contact sheet of the composed characters |
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
- **Loadouts (session 4):** `PartDefinition` (slot + stat modifiers), `PartCatalog` (every Part + starter Parts, `Content/Parts/`), `Loadout` (deck cards + Parts). `BattleUnit.Incantations` / `Parts` / `ApplyLoadout`; `BattleController.ApplyLoadouts`; `BattleRequest.Loadouts`. `MemberProgress` stores `Cards` and `Parts` by asset name; `WorldSession` has `Inventory`, `DeckOf`, `SetDeckCard`, `Equip`, `Unequip`, `LoadoutsFor` and `GiveStarterParts`. Save version 2 adds `inventory` and `starterPartsGiven`.
- `Scripts/World` (**Game.World**): the overworld.
  - `Dialogue/`: the conversation system. `DialogueBox` is an UNBEATABLE-style tilted white label: hard black shadow, black Anton speaker tag with the speaker's accent color, a compact profile-poster portrait, bobbing next arrow, per-letter voice blips generated in code. `DynamicText` is Celeste-style per-character animation: letters pop in, and `{wave}`/`{shake}`/colors keep moving. `DialogueMarkup` parses `{wave} {shake} {red}/{gold}/{blue}/{green}/{cyan}/{gray}...{/c} {c=#hex} {slow} {fast} {p} {p=0.8}`, with beats after punctuation. `DialogueVoice` sets pitch, typing speed and accent per speaker.
  - Talk with **F**. **F / Space / Enter** finish the line, then advance. The player is frozen while a conversation is open. `OverworldUI.Talk(lines)` / `Say(...)` start one; `TalkInteractable` has `voice` and `portrait`. NPCs are characterized through voice and markup: the Trader is gruff and low, the Scout fast and excitable, the Wanderer slow and dreamy.
  - `OverworldBuilder` turns the text map into a 3D mesh, plus props, NPCs, encounters and access points, and keeps the party HUD current (`RefreshPartyHud`).
  - **Lighting:** there are **no point lights**. The only light is the sun, with a dappled "light through leaves" cookie (`WorldArt.DappleCookie`) that drifts slowly (`CanopySway`), over a dim green trilight ambient and haze. Tune it in `OverworldBuilder.ApplyAtmosphere`.
  - `WorldFx` builds the props and effects: `Tree`, `Bush`, `Rubble`, `Tuft` (swaying grass and flowers), `Vine`, `Motes`, `Drift` (falling leaves), `AccessPoint` and `FloorMarker`.
  - `WorldArt` has the palette (sun, machine red, sepia UI colors), the atlas cell constants and `CellMaterial` (one atlas cell on a primitive, opaque or cutout).
  - Other pieces: `PlayerController`, `CharacterSprite`, `OverworldCamera`, `PostFxFocus` and `OverworldUI` (sepia panels plus party rows with level and HP).
  - `WorldSession` holds the return point, defeated encounters and `Party` (progress keyed by CharacterDefinition asset name). `SaveGame` writes and reads it as JSON.
  - Map legend (`OverworldMap`): `H # B L` blocks, `.` pavement, `=` road, `P` grass plaza, `,` tall grass, `*` flowers, `T` tree, `b` bush, `r` rubble, `A` access point, `S n E X` spawn, NPC, enemy, stand square.
- `Resources/Fonts/`: M PLUS 1p Light/Medium/Bold (all UI text) and Anton (dialogue speaker tags), both **SIL OFL 1.1** from Google Fonts. Licenses and credits are in that folder.
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
Session 4 (crew menu, R1999 restyle, new party): run **Generate Sample Content**, then **Generate Mossline Station** (it assigns the `PartCatalog`), then check:
- The crew menu at 800×600: the cards slide, Enter flips, Parts equip and the stats show "+N", cards swap, and L sets the leader.
- The portraits import as point-filtered Sprites (`PixelArtImporter` handles `Art/Portraits/`), and the internals art has its paper-white rim (`UI/Default Font` silhouette material).
- The new `LoadoutTests` pass, along with the rest.
- Swapped cards show up in the hand, and Part stats show in the inspect screen.

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
- MC's element channeling: the data and the glow hook are in place (`channels`, `AfflatusInfo.GlowFor(member, channeled)`). Next: a battle mechanic that picks the element, then pass it to the battle glow, overworld glow and card portraits.
- More Parts as loot (the results screen), and Part effects beyond stats (passives).
- A second map (the Sunken Arcade or a Resistance camp) with warps and a data-driven dialogue box.
- Effects (buffs/debuffs) are still missing from the details screen ("can be somewhere else").
- Before making player builds, add `Sprites/Diffuse`, `Legacy Shaders/Particles/Additive`, `Legacy Shaders/Particles/Alpha Blended` and `Legacy Shaders/Transparent/Cutout/Diffuse` to Always Included Shaders.
