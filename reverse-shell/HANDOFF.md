# HANDOFF: where we left off (2026-10-22, session 4)

# Chapter One demo (start here)

**Web demo, Track A (newest, 2026-10-24):**
- **Training Mode is now Sandbox Mode everywhere:** the UI, story keys (`sandbox.*` in World.txt), code
  (`WorldAreas.Sandbox`, `WorldSession.InSandbox` / `StartSandbox` / `Sandbox*` fields, `EnterSandbox` / `LeaveSandbox`)
  and docs. The area is called SANDBOX (subtitle Mossline Station). Old saves still load: `WorldAreas.Migrate` maps
  the old id `training` to `sandbox` (`SaveGame.MigrateAreas`). The chest level and return point in older saves are
  dropped, because their JSON fields were renamed. Sandbox Mode stays on the title for the demo.
- **End card:** a gold line, "That's it for now, but please explore Sandbox Mode! (Esc, then Settings)"
  (`[endcard]` line 4; the hint moved to line 5).
- **Esc or P:** `UIKeys.Back` (Esc, or P unless a text field has focus) replaces every `GetKeyDown(Escape)`. In
  browser fullscreen Esc only leaves fullscreen, so P opens the menu. The key hints say "Esc / P".
- **Web saves:** `WebSync.Flush()` (with `Plugins/WebGL/WebSync.jslib`, `FS.syncfs`) runs after every save or
  delete, so a page reload keeps the save. It does nothing outside WebGL.
- **Reverse Shell / Web Build Setup** (`Editor/World/WebBuildSetup.cs`) adds every `Shader.Find` shader to Always
  Included Shaders (WebGL builds also do this themselves, through `IPreprocessBuildWithReport`). It also sets the
  WebGL player settings: 960x540, 256 MB, Gzip, explicitly thrown exceptions, Run In Background off.
- **Title screen:**
  - Quit is hidden in web builds, and the Fullscreen help explains the browser rules.
  - **Credits** page (`CreditsText`, `MadeBy`).
  - **How to Play** page (`ControlsText`). It shows once before the first game on a computer (`ControlsFirst`,
    PlayerPrefs `seen.controls`) and is always under Settings.
  - Version label bottom-right (`TitleScreen.Version`, "Demo v0.1"; raise it for each build you hand out).
  - Rows are 84 apart from `MenuTop` 110, and the settings help panel sits under the last row.

**Esc menu as physical cards (newest):** `PauseCards` now plays out a scene.
- **Zoom:** like the battle's hold-to-inspect, the view zooms in quickly (1.5x, OutCubic, 0.28 s;
  `OverworldCamera.zoom` / `aimOffset` / `Direct`) while the party HUD and objective fade (`OverworldUI.SetHudAlpha`).
  The player ends up on the left (`PlayerViewX` = 0.2; `Frame()` works out the aim for the screen shape), turns
  right and kneels on one knee (the `sit` clip).
- **Mats:** where the mat goes depends on the ground (`BlockedRight` samples the footprint).
  - **Open to the right:** the player is framed on the left, facing right. The ground mat rolls out north to south on
    their right over 0.5 s (`PauseMat.Create(..., northToSouth)`, `WorldRoll`, `MatGap`). Then the big UI mat rolls
    across the screen, always left to right, from where the ground mat lands on screen to the right edge.
  - **A block to the right:** the player is framed top-centre (`AimSouth`), facing down. The ground mat rolls out west
    to east just south of them, then the UI mat rolls across the full width left to right.
  - The UI cloth texture is as wide as needed to keep pixels square.
- **Cards:** dealt onto the mat with a small bounce, overlapping slightly (`Gap` -36), each with a stable sprawl.
  "PAUSED" is top-left.
- **Input:** Settings is hovered by default (no Resume card). A/D, the arrows or the mouse move the hover; the hovered card straightens
  and rises. Esc during the opening skips to the end.
- **Close:** Esc / Resume plays it backwards: cards lift off, the UI mat rolls back up to the left, then the
  ground mat, then the player stands (unkneels), then zoom out. Party / Units / Resonance keep the view zoomed and the kneel, and roll the mats out again on the
  way back (`Open(card)`). The Sandbox warps reset at once (`HideNow(true)`).
- **Kneel:** the Fantasy Knight pack has no kneel, so `knight_model.kneel()` draws one: the idle pose's upper body
  lowered 6 px over new legs in the character's plating (front knee up, back knee down; `FRONT_LEG` / `BACK_LEG`
  paths). It's written as `sit.png` (`--only sit`), so the existing `sit` clips (`EnsureSitClips`) pick it up.

**Planning + demo (newest):** every planning document lives in `reverse-shell/Planning/` (git-ignored):
`ROADMAP.md` (Track A: itch.io web demo; Track B: creator tools, stages 0-7, with statuses), `ITCH_DEMO_CHECKLIST.md`
and the old `GAME_PLAN.md` (moved there). The title screen's subtitle is "Demo" (`TitleScreen.Subtitle`).

**Card icons:** every non-Ult card's centre icon is now the user's pick from Raven Fantasy Icons
(`RAVEN_PICKS` in `Tools/CardForge/card_forge.py`; the files are copied into `Art/ThirdParty/RavenFantasyIcons/32x32`,
listed in its LICENSE-NOTE), with the cards' plum outline added. Cards without a drawing of their own (Bite, Coil Up,
Grapple Hook, Poison Strike, Spark, Static Coil, Suppressing Shot, Tail Lash, Thunder Breath) are added to the atlas
from the picks. Renames: **Piston Slam → Golden Hammer**. **Pounce → Poison Strike**, which now also poisons
(1 stack, 2 turns).
- **Card asset names now match what the game shows** (the `.meta` files were renamed with them, so references
  hold): `Card_Enemy_GoldenHammer`, `Card_Enemy_PoisonStrike`, `Card_Enemy_Reinforce`, `Card_MC_PrismBurst`,
  `Card_Blue_AegisDawn`, `Card_HealBot_VerdantBloom`, `Card_Lucy_ZeroDay`, `Card_Enemy_Reclaim`,
  `Card_Enemy_StormHeart`, `Card_Enemy_Meltdown`, `Card_Enemy_ToxicSwarm`. The generator and CardForge use the new
  names, and the variables follow them (`goldenHammer`, `poisonStrike`, `reinforce`).
- **No duplicate icons:** Bulwark is fb2151 (blue tower shield) and Reinforce is fb1921 (breastplate). No Raven icon
  is used twice across cards, card types and statuses (fb665 stays the Counter type icon only).

**Battle + Esc polish:**
- **Go button:** an ordinary ink button with corner brackets (not gold). `GoButtonFx`: hovering slides gold chevrons
  out; pressing (click, or Enter when no enemy is ringed, through `HudView.PressGo`) punches and flashes it.
- **Horizon:** `BattleBackdropDefinition.streetLine` is 0.52 (was 0.42), so more sky and buildings show. The Stage is
  centred on the street line (its pivot stays at the screen centre for the inspect zoom), so units always stand on the
  street (`StageY` = 12 above it).
- **Status icons:** 76 px (were 52). The corner shows the total effect over all stacks (`StatusChip.Amount`: two
  stacks of +15% ATK show "+30%"), and the hover box says it in words (`TotalEffect`: "ATK +30%", "Lose 6% max HP
  each turn"). Card chips show the amount too.
- **Esc menu:** the big mat always spans the whole screen, and the cards are bigger (`CardScale` 1.3, fitted; mat 560
  tall). The Resonance card glitters (`View/CardSparkles`: stars and glints, reusable on any card).
- **Icon picks:** done; see Card icons above.

**Items:**
- **Esc cards:** Quit, Save, Settings, Party, Units, **Items**, Resonance (`OpenNext` indices 3-6). The Items icon
  (`Menu_Items`, a drawstring pouch) is drawn by CardForge in cell 29.
- **`ItemsScreen`:** every Part you own (spare or fitted) on a teal velvet mat (`PauseMat.ClothStyle.Velvet`) that
  rolls out. Each item's icon floats and bobs (the cushions and empty spots were dropped at the user's request).
  Hover or arrows select one: a soft gold glow fades in behind it, it rises, grows 15% and gets corner brackets, and
  the panel under the mat shows its name, CHIP/CORE, how many, who has it fitted, and the description.
- **Units page:** item descriptions are hidden until that slot is selected (`itemDescs`, toggled in `SetFocus`). They
  then show as an ink tooltip just above the item slots.

**Readable text:**
- **Minimum size:** `UIFactory.MinFontSize` (18 in layout units, about 29 px of monogram) is enforced by `CreateText`,
  `BestFit`, `FitRich` and `Size()`. Anything smaller is raised to it. The only exception is the pixel cards' own
  name plates (`BestFitCardPlate`), which are part of the card art.
- **Units item picker** (`CharacterMenu.OpenPicker`): a wider box, one row per option with the name at 34 and its
  description underneath at 26 in gold, wrapping instead of shrinking (`PickerOption`).

**Party screen + controls:**
- **Backup rule:** a backup only while all three fighting slots are filled (`WorldSession.BackupAllowed`). `Assign`
  refuses a backup that would leave a gap, and `EnsureFormation` moves the backup into any empty fighting slot.
- **Backup slot:** greyed out while closed, with "Replaces a fallen party member in combat" under it. The
  "Choose up to 3..." subtitle is gone.
- **Single corners:** empty slots have one set of corner brackets, and `HeroFrame` is a single, bolder set too.
- **Readability:** "Members" sits above the mat in paper text. Member names show only while a card is held (0.3 s;
  that release isn't a click) or highlighted with the keyboard, as an ink tag above the card.
- **Resonance chart:** `View/ResonanceChartView` (built from `AfflatusChart.Beats`, so it always matches the rules)
  sits bottom-right on the Party screen (full size) and Prepare for Battle (0.8x): the wheel in one line and
  Light <> Void. Physical is left out.
- **Last fighter:** `WorldSession.CanClear` hides Remove for the only active member (and `ClearSlot` refuses).
- **Back to the Esc menu:** returning from Party / Units / Resonance shows the mats already out and the cards already
  dealt (no replay). The current leader is put straight into the kneel (`PlayPose("sit", instant: true)`), even if the
  leader changed, and the ground mat stays out while those screens are open.
- **Controls setting:** title, battle and Esc Settings offer Controls: Both / Mouse / Keyboard (`GameSettings.Controls`,
  saved in PlayerPrefs). Menus read keys through `UIKeys.Pressed`, which is silent in Mouse mode. `ControlInputModule`
  ignores the mouse and hides the cursor in Keyboard mode. Walking, F, Q and Esc always work.

**Play + Esc camera:**
- **Play from the title:** `Editor/World/PlayFromTitle.cs` sets `EditorSceneManager.playModeStartScene` to
  `Overworld_Mossline`, so Play always boots to the title screen whatever scene is open. Toggle it with
  Reverse Shell > Play From Title.
- **Esc near walls:** `PauseCards.ViewBlocked` samples the line from the player to where the zoomed camera would go,
  against the map's heights (`OverworldMap.TopAt`). If terrain is in the way, the camera stays put (`steady`: no zoom,
  no shift); you still kneel, the HUD fades, and the mats roll out as usual.
- **Next (planned, staged):** Level Designer, chapter and area assets, story events without code, Card / Enemy /
  Part editors, art import, all UI text in files, Afflatus renamed to Resonance, plain-heading comments. See
  `~/.claude/plans/can-you-add-a-prancy-sprout.md`.

**Title + menus:**
- **Title background:** `FallingCards` rains only Soul Cards: every `Resources/Cards/Ult_*.png`, each tinted toward a
  random colour (`Tints`) and keeping its shine. Four pixel sparkle effects play on a brighter layer above the dimmed
  cards: Twinkle (four-point stars blooming on a card, riding with it), Trail (glints left behind a falling card),
  Burst (an eight-ray pop at a card corner) and Glitter (diamonds drifting down the screen, flickering).
- **Brackets:** arms are at least 3 canvas units thick (`CornerBrackets.MinThickness`), so the vertical arms don't
  disappear at 4:3.
- **Selection is a hover now:** title rows, `RowMenu` pages (Save / Settings / Quit) and every `InkStyle.Button`
  (`ButtonHover`), including the combat pause menu, show the selected or hovered item as a lighter grey plate
  (`InkStyle.HoverGray`) that grows a little. There's no gold plate any more, and the combat menu's Resume / Back
  aren't gold.

**UI round:**
- **Corner brackets:** `InkStyle.Frame` now draws only corner brackets (`CornerBrackets`; arms 22 px, longer for
  thick frames, at most a third of a side), so every panel, card and selection frame has brackets instead of lines.
  Button hover borders (`ButtonHover`) and the battle queue chips (`HudView`) use it too.
- **Esc cards:** Quit, Save, Settings (hovered by default), Party, Units, Resonance. There is no Resume card (Esc
  resumes). Quit asks "Are you sure?" (`ConfirmQuit`).
- **Party / Prepare for Battle:** Space no longer starts a fight (only the Start button does). The Members strip lies
  on a grey mat with a pearl trim (`PauseMat.ClothTexture(w, pearl: true)`, `RollTexture(true)`) that rolls out from
  a scroll in the bottom-left. Click the scroll or the mat (or press Down / Enter) to unroll it; click again to roll it
  up. It stays open or closed across rebuilds.

**Prepare for Battle: enemy cards.** In `FormationScreen`'s pre-fight mode, hovering or clicking an enemy row shows
a panel beside the list with that enemy's skill cards (at their rank) and its Ult, at the fight's level
(`ShowEnemyCards`). It stays while the pointer is over the row or the panel. Clicking a card opens `CardDetailPopup`.

**Party drag and drop:** `FormationScreen` cards can be dragged. A strip member dropped onto a slot goes there; a
slot dropped onto another slot swaps the two; a slot dropped anywhere else is emptied (never the last active member).
The hovered slot gets a gold frame. **Scanner HUD:** the cooldown is just the clock wipe now (no numbers).

**Sandbox Mode from the title (kept for the demo, so players can go straight there):** the title's main menu has a **Sandbox Mode** row, and its
Settings page has **Enter Sandbox Mode**. Both call `WorldSession.StartSandbox()`. That loads the latest save (or
starts a new game in slot 1 when there's none), then starts in the Sandbox. Leave Sandbox Mode returns to
where that save left off (for a new game, the city's start, where the opening plays).

**Newest (resonance card + scanner HUD):**
- **Resonance is its own Esc card** (`PauseCards`, between Units and Settings), shown only after Chapter One
  (`ch1.complete`) or in Sandbox Mode (`WorldSession.ResonanceMenuUnlocked`). Hidden cards drop out of the row, and a
  row too wide for 4:3 shrinks (`MaxRowWidth`). It opens `ResonanceScreen`, which shows only the resonances you have
  (Physical, plus Electric once absorbed or always in Sandbox Mode, which lends every kit: `AvailableResonances`). The
  row centres and shrinks as more unlock. Locked ones aren't shown (the user's choice, 2026-10-24; `Ui_Lock` is still in
  the atlas). Below the row are the shown resonance's cards and Ult; click or hold one to open `CardDetailPopup`. The
  active one has a gold frame. The old resonance buttons on the Units page are gone.
- **Resonance Ult art:** `CardArt.Ult(card, owner)` tries `Ult_<card asset>` before the owner's card, so Electric's
  Thunderheart has its own full art (`Ult_Card_MC_Thunderheart.png`: yellow swirl and lightning, from `ULT_CARDS` in
  CardForge). It's used in the hand, the queue, the menus and the formation portrait (`WorldSession.UltimateOf`).
- **Fix:** Sandbox Mode from the title used to drop you at the save's city coordinates inside the Sandbox
  (an empty world). `StartSandbox` now clears the return point, and `SpawnPlayer` ignores any return spot that isn't on
  the current map.
- **Resonance colour:** **Electric is yellow** now (`AfflatusInfo.Color(Volt)`; Light moved to pale white to stay
  apart). The active resonance tints the protagonist's card swirls, queue chips, cast flashes, battle glow, party-HUD
  line and Units-page cards (`BattleUnit.Color`, `WorldSession.ColorOf`, `AfflatusInfo.ResonanceColor`; Physical keeps
  the character's own colour). The sprites themselves aren't recoloured. The Electric Dragon's own colour is still blue.
- **Scanner HUD:** `ScannerHud` sits bottom-left once `ch1.scanner` is set. It shows the binoculars (`Ui_Binoculars`)
  with a Q key badge. A scan starts a 4 s cooldown (`OverworldBuilder.ScanCooldown`), shown as a clock wipe with the
  seconds left and a flash when ready. Q pressed too early shakes it. It's hidden during dialogue and menus.
- New icons `Menu_Resonance`, `Ui_Lock` and `Ui_Binoculars` are drawn by `Tools/CardForge` (appended to `CardArt.png`,
  cells 26-28). Tests: `StoryTests` (Sandbox Mode lends Electric + menu unlock, resonance colour).

**Canon:** `GAME_BIBLE.md` v0.3 is the source of truth; its §13 lists what is only a draft (Collector, record and memory
texts, the ending mystery, the Electric kit and enemy numbers) and the code-name mapping (Electric = `Volt`, Physical =
`Afflatus.None`). The protagonist is player-named (asset `Char_MC`) and alone. Heal Bot, Lucy, Blue and the Sandbox
are Sandbox-only: Esc → **Settings → Enter Sandbox Mode** takes you to the Sandbox (the crew joins
there), and the same row, now **Leave Sandbox Mode**, puts you back exactly where you were (saved, so it survives a
Continue). Esc → **Save** asks which slot to save to (saving to another slot makes it this game's slot; overwriting a
different occupied slot asks first). Both pages are a `RowMenu` over the Esc cards.

**First launch:** open the project in Unity once. `Editor/BlueContentBootstrap.cs` then runs
`SampleContentGenerator.EnsureChapterOne()` (and `EnsureBlue()`), which creates the chapter's assets: the protagonist's
resonance kits, `Status_Stunned`, the Scrap Hound / Wire Crawler / Collector / Electric Dragon enemies (all on the red
Rust Sentry sprite), their cards, and `Resources/Bestiary.asset`. The Console logs it. It runs again whenever
`SampleContentGenerator.ChapterOneVersion` is higher than the bestiary's `contentVersion` (v2 = the harder enemies), so
bump that constant after changing the chapter's numbers. Then press Play in `Scenes/Overworld_Mossline.unity`.

**Story text:** every word the game says lives in `Assets/_Project/Resources/Story/` (`StoryText` reads every .txt
there): `Chapter1.txt` is the chapter's script, `World.txt` everything else (objectives, Access Points, chests and
rewards, battle outcomes, the Summon Beacon, the Sandbox people). Text can be pasted in from the bible or any
document without touching code. Some blocks take fill-ins such as `{item}` (listed above them in the file). `[key]` starts a block, `Speaker | text` is one
box spoken by Speaker (Scanner, Collector and Memory get their own voices, anyone else the narrator's), a bare line is
narration, `#` is a comment, `{name}` is the protagonist's name. Lines too long for the dialogue box are split into
several boxes automatically (`World/Dialogue/DialoguePager`, which never breaks inside a tag and carries colours and
effects over), so a whole paragraph can sit on one line. Keep the keys; `StoryTests` checks they all exist, and a
missing one shows `[missing: key]` in game.

**How to play:** Title → New Game → pick a slot → the opening recording (Enter/Space/click advances, Esc skips) → name
the protagonist (type, Enter) → wake in the city's walled courtyard. WASD/arrows move, **F** interacts, **Q** scans
(once you have the scanner; 4 s cooldown), **Esc** opens the pause cards (Quit, Save, Resume, Party, Units, Resonance,
Settings), **F8** opens the story debug panel (editor / development builds).

**The chapter, beat by beat** (flags in `World/Story/Chapter1.cs`; the objective shows top-right):
1. Opening (`PrologueSequence`): the hopeful public-information recording ending on EVERLASTING, the transfer stalling at
   63% and failing, then name entry. Flag `ch1.named`.
2. Awakening beside the procedure pod (`ch1.awake`). Three memory fragments (`ch1.memory.1-3`, optional) and records
   (courtyard notice, plaza poster, park plaque, clinic terminal) are spread across the city; a small machine tends
   flowers in the park.
3. The optical scanner in the ruined clinic (top-right block) (`ch1.scanner`). **Q** sends a pulse: traces show as cyan
   marks; it never reveals elemental weaknesses.
4. Leaving the clinic through its only gate (a `Z` trigger) meets the **Collector** (`ch1.collector.met`): dialogue
   with a choice, then a fight. Winning sets `ch1.collector.beaten` and they retreat over the rooftops.
5. On the south street, the trace marks lead to a cracked wall in the building face on its north side (facing the camera); scanning it (after the Collector) opens the hidden
   route (`ch1.route`), a warp into **The Buried Line** (tunnel, dim; `ch1.tunnel`): a maintenance log, two optional
   Wire Crawler fights, an Access Point.
6. **The Humming Hall** (chamber): crossing the line in the middle wakes the guardian (`ch1.dragon.seen`), then the
   **Electric Dragon** boss fight (Lv 7, boss) (`ch1.dragon.beaten`).
7. The pedestal's blank Soul Card binds to the Core: **Electric resonance** unlocked (`ch1.resonance.volt`), switchable
   for free in Esc → Resonance (Electric swaps the innate cards for Arc Edge / Static Field and the Ult for
   Thunderheart). Then the ending (a second signature matching your own, moving) and the "End of Chapter One" card
   (`ch1.complete`). Free roam continues.

**Losing:** a defeat wakes you at full HP at the last Access Point you rested at (`WorldSession.LastAccessPoint`,
saved; the area's start if you never rested). Story fights (Collector, dragon) start again when you step back onto
their trigger (or, for the dragon, interact with it). Nothing is ever lost.

**Access Points** heal, save, become your wake-up point, and bring back every beaten patrol machine
(`WorldSession.RespawnPatrols`; story fights stay gone because their flags block them). If machines return in this
area, it reloads around you.

**Parts in Chapter One:** no spare Parts at the start. The only Part found is the **Ult Chip** (`Part_UltChip`, start
every battle with 2 Ult points via `StatType.StartingMoxie`), the Collector's story reward (`EncounterDef.Reward`). The
dragon's reward is the Soul Card on the pedestal (Electric resonance), not a Part. There are no other Cores: the
protagonist's own Core binds with the Soul Card, so it can't be removed or swapped (`WorldSession.IsBound`). Loot chests
still work (`c` on the map, contents in `WorldAreas.Area.Chests`, opened once via a `chest.<area>.<n>` flag) but none
are placed.

**Saving:** the save keeps your exact position (`WorldSession.PlayerSpot`), so Continue puts you where you saved. Save
slots (title and Esc → Save) show your chosen name, level, area and time (`SaveGame.SlotLabel`).

**Code map (Chapter One):** `World/Story/` (`StoryState` flags + name, `Chapter1` flags + objective,
`ChapterOneDirector` all beats, `StoryText` the script file, `PrologueSequence`, `StoryProps` placeholder props, `StoryDebugPanel` F8).
`WorldAreas` (areas, warps, per-encounter enemies, NPCs, dim lighting); maps are generated and checked by
`Tools/MapForge/chapter1_maps.py` (paste its output into `WorldAreas`). Resonance: `ResonanceKit`,
`CharacterDefinition.resonances`, `WorldSession.ActiveResonance/UnlockedResonances`, `BattleUnit.Afflatus/Ultimate`.
Enemy lookup at runtime: `EnemyCatalog` (`Resources/Bestiary.asset`). Map legend additions are documented on
`OverworldMap`.

**Results screen:** only members who fought get XP (a backup who never stepped in gets none and isn't listed). Each
member who levels gets a stat panel on the right, stacked (up to three fit above the button), one line per stat:
`LABEL  old → new  +N ▲`. Ordinary wins say nothing afterwards (the results screen covers XP and levels); only story
fights (Collector, dragon) have a line after.

**Starting kit:** a new game has no spare Parts; the protagonist starts with `Part_FragmentedCore` (no bonuses) fitted
in the Core slot (`PartCatalog.starterCore`, fitted by `WorldSession.GiveStarterParts`). Older saves keep what they have.

**Tests:** `StoryTests` (objective, save round trip, name override, resonance switching, Physical neutral, Stunned,
script parsing + every key present, dialogue paging) and
`OverworldMapTests` (every area's warps pair up, fight definitions match the map, every Chapter One beat reachable).
They compile; run them in the Test Runner.

**Known issues / not verified in Unity:** nothing here has been play-tested in the editor yet (no Unity run was
possible); the props are primitive placeholders; there is no music or sound; enemy numbers are first drafts; scanning
"materials" only shows as text; an old save from before Chapter One plays the opening again on Continue; in-combat
resonance switching is not built (the bible leaves its cost open).

---

> **Icons (newest):** the game's icons are the user's picks from Clockwork Raven's **Raven Fantasy Icons**. It's the free version (personal use only, no redistribution, so keep the pack folder out of public repos). The 32x32 files sit in `Assets/_Project/Art/ThirdParty/RavenFantasyIcons/`, and `LICENSE-NOTE.md` there lists every icon used.
> - **Build:** `Tools/IconForge/card_icons.py` composites them (4x, hard pixels) into `Resources/UI/CardIcons.png`, now an **8x8** atlas.
>   - Rows 0–1: the card types, as white masks and in their own colours.
>   - Rows 2–4: intent cards, with the icon at 2x on the cream / silver / gold card faces.
>   - Row 5: UI bits. The Ult pips are fb2186 (the empty pip is greyed) and the star is fb984; the empty diamond, the filled diamond and the card back are still drawn in code.
>   - Row 6: the element orbs.
>   - Row 7: CHIP, CORE and the status icons.
> - **Code:** `CardIcons.Element(afflatus)` (the orb beside each unit's bars, replacing the empty diamond), `CardIcons.Part(slot)` (party-menu sockets and part rows) and `CardIcons.Status(status)` (a status's own `icon`, else the atlas icon by asset name; status chips now show it beside the label). `CardStars` uses the atlas star.
> - **Colours:** icons keep their own colours (no tinting).
> - **Incantation Might Up** has no icon yet (the user is deciding).
>
> **Goal:** a polished **Chapter 1 demo** with the user's own art and music; everything visual now is a placeholder. The decisions so far are recorded in memory (chapter1-polish-plan).
>
> **Compile check without the editor:** a scratch script compiled every assembly (Combat.Runtime, Game.World, both editor assemblies and Combat.Tests) with Unity's bundled Roslyn `csc` at C# 7.3, using the references from the `.csproj` files. Everything below compiles; the tests were compiled, not run.
>
> **Card text + icon-only cards (newest):**
> - **Card text:** one short sentence per effect, worded the same at every rank, with no commas (`CardText.Sentences` no longer joins clauses; `Joinable` / `Clause` / `TargetKey` were removed).
>   - Status sentences no longer write turns or stacks: "Give the weakest ally Regeneration." or, with a verb, "Poison 1 enemy."
>   - The detailed face (the card popup) shows **status chips** above the kind band instead: the status icon with its turns, plus "2x3" when stacks > 1 (`CardView.StatusChips`).
>   - `CardTextTests` were updated, plus a no-commas test and a Sap Mend wording test. They compile but weren't run (the editor was open).
> - **Icon-only cards:** `CardView.BuildFace(..., iconOnly)` and `AbilityCard.Create(..., iconOnly)` show only the rank stars, a big type icon and a thin kind-colour strip, and they ignore the wheel. Used in the party menu (click to open) and the battle inspect sheet (hold to open, through `AbilityCard.OnLongPress`).
> - **Popup:** the wheel stops at rank I and III rather than wrapping. `BattleView.HandleTargetKeys` ignores keys while a card is open.
> - **Status icons:** a pick sheet for every status was made for the user (`pick_statuses.png`, scratch). The current picks are 673 / 641 / 642 / 645 / 968, and Incantation Might Up is still unpicked.
>
> **Card popup + element glow (previous):**
> - **`CardDetailPopup` (shared):** the card lifts from where it is, glides to the centre growing to 2.5x, then flips (scale X 0 -> 1) to its detailed face with the full text. "Scroll to see upgrades" appears when it has ranks (`AbilityCard` flips the rank on the wheel). A click anywhere or Esc shrinks it back.
>   - **Party menu:** clicking a card opens it, and Enter on a focused deck card does the same.
>   - **Battle:** holding a hand card for 0.45 s opens it (`CardView` long-press; releasing doesn't queue it). The hover text preview is gone; hovering lifts the card to 1.06x. `BattleView.InputEnabled` is false while it's open, and Esc closes the card before the pause menu.
> - **`ElementBadge`:** the element orb with a thick dark outline on a soft, slowly pulsing glow in the element colour, with no box. Used in the battle inspect header and the party menu header.
> - **Battle:**
>   - Inspect cards are 1.35x.
>   - Unit names (shown on hover or focus) sit above the head; for enemies they sit above the intent card.
> - **Party menu:**
>   - The sprite is 12x and the cards 1.45x.
>   - Spare cards are removed, and with them deck swapping (`OpenCardPicker` / `SwapIn` deleted; `WorldSession.SetDeckCard` still exists).
> - **Dialogue box:** one default look for everyone, with no portraits (the profile poster is no longer used).
>   - A dark ink panel with a gold hairline and a thin accent bar in the speaker's colour.
>   - The name in gold serif with an accent underline, and paper-white text.
>   - A gold "next" diamond, and ink choice rows that turn gold when selected.
>   - It rises and fades in, with no tilt.
>
> **Polish pass (previous):**
> - **Battle inspect:** the unit keeps its side of the screen and the sheet fills the other side. The sheet has a tab with the element orb, the name, the level, HP as one line, the stats as two columns of label / value strips, and the cards in a hairline bracket, with "Click anywhere to return to battle" below. The zoom offset is clamped to `(Zoom - 1) * screen / 2`, so the backdrop never shows its edges.
> - **Party menu (`CharacterMenu`, rewritten):** one detail page per member.
>   - Tabs along the top (the first is marked Leader); the member's idle animation at 9x on the left (`UISpriteAnimator.UnscaledTime`, because the menu pauses the game).
>   - On the right: the element tab, name, level with an XP hairline, HP, stat strips (Part bonuses as a gold +N), Items (CHIP / CORE strips, click to change) and every card in a bracket (Deck / Ult / Spare).
>   - Left/Right switch member, Up/Down focus, Enter opens a picker, L sets the leader, Esc closes.
>   - The opened-robot internals, the carousel, the flip and the Persona stat slab are gone. `CharacterDefinition.portraitInternals` and `partSockets` are no longer read.
> - **Battle HUD:**
>   - No battle log (`HudView.Log` is a no-op).
>   - The turn counter is a small "TURN" label over a serif number, with no plate.
>   - The queue is a slim row of slots with no panel or labels (still clickable to take every card back).
>   - Nameplates are just the HP bar and Ult pips; the name shows on hover or focus. The element / class line and the element orb are gone.
>   - Status effects are small icons with turns left (the short label when there's no icon).
>   - Hand cards now show the card name (serif) between the rank / icon and the kind word.
> - **Formation, Blue, targeting, enemy levels (2026-10-23):** `WorldSession.Formation` (saved): slots 0-2 active (slot 0 leads/walks), slot 3 backup; `FormationActive/Backup`, `Assign` (swaps), `ClearSlot` (never leaves no active), recruits fill empty slots. `FormationScreen` edits it with Ult-art slot cards + a member strip: from the Esc cards (**Party**, X/Esc back) and as the **pre-fight** screen before every encounter (enemies with level/BOSS on the left, Start/Space). `OverworldBuilder.BuildSetup` makes the fight (active + backup, enemy count, sandbox level) for both preview and battle. Esc cards are now Quit/Save/Resume/Party/**Units** (Units = the old character detail menu, retitled). **Blue**: Light Guardian backup (Shield Bash, Bulwark, Ult Aegis Dawn; new Status_DefUp), sprites from knight_model `Blue`, assets made by `SampleContentGenerator.EnsureBlue` (auto-run once by `BlueContentBootstrap`), added to Battle_Sample's roster; joins in the Sandbox. **Targeting**: queueing a single-enemy card locks `CardInstance.Target` to the enemy under the cursor. **Enemy levels**: enemy stats are their Lv 5 values scaled by `BattleUnit.EnemyScale` (1-99); boss = 3x HP, 1.25x ATK, 9x sprite, BOSS tag; XP scales too. `BattleSetup.enemyLevel/boss/backup`. The Sandbox chest (`C`, `EnemyLevelPanel`) sets presets Enemy Lv 5 / Boss Lv 20 / Custom Lv 30 and ±1/±10 up to 99 (saved).
> - **AP / hand / party rules (2026-10-23):** `BattleState`: `Party` = up to 3 active (`MaxActive`), `Backup` = 1 reserve (`MaxBackup`), `Roster` = everyone (loadouts, levels, results use it). Each turn AP = living active units (+ bonus), hand cap = AP + 4 (`HandBonus`); `CombatConfig.handSize`/`actionPointsPerRound` are no longer used. A fallen member's cards leave the hand/queue and the backup takes their slot (`UnitEnteredEvent`, `BattleView.PlayUnitEntered`); defeat only when no one is active and no backup is left. Moves cost 1 AP and count in `MovesThisRound` (undo restores it); the queue shows each as a locked "CARD MOVED" slot. Merges after a play/move/draw are free and chain (`ResolveMerges`). Overworld: Access Points have a purple stand square (`HealPurple`) and prompt "[F] Heal"; the party menu has an X (top-right) and, opened from the Esc cards, returns to them on close.
> - **Results screen (2026-10-23):** `ResultScreen.Play` (started by `HudView.ShowResult`) animates: RESULTS/DEFEAT header between gold rules, +XP, member rows sliding in with slanted XP bars that fill from `BattleResult.Before` (new: pre-XP progress) to the new XP, wrapping per level with a LEVEL UP! tag, the alert effect and an Lv punch; then per levelled member a gold diamond LEVEL UP / N burst and a stat panel (HP, ATK, PHYS DEF, SPEC DEF counting up with gold "+N ▲", base stats without Parts); the Continue / Fight Again button appears last.
> - **Party & pause (2026-10-23):** `WorldSession.Members` (saved as `members`; empty = everyone, for old saves): a new game is just MC (Lv 1); entering the Sandbox recruits the rest of the roster (`RecruitAll`, with a "joins your party!" message) and they stay. `OverworldBuilder.Party`/battles use `WorldSession.Joined`; encounters field at most as many machines as party members. Overworld **Esc** opens `PauseCards`: pixel cards Quit / Save / **Resume** (default) / Party (icons from CardForge `MENU_ICONS`); Party opens the CharacterMenu, Save flashes SAVED!, Quit saves and returns to the title. Overworld text is bigger (party HUD, prompt, area subtitle, dialogue), and prompts are just "[F] Talk" (NPCs) or "[F] Interact".
> - **Camera rule (2026-10-23):** `OverworldMap.ApplyCameraRule(pitch)` runs on every area at build: a block k rows south of a walkable square in its column is lowered to at most `0.9 + k*tan(pitch)` (H 5 / # 3 / B 2 / L 1), and trees within ~2 rows become bushes, so nothing hides the player (Octopath-style: tall architecture behind, low things in front). Solidity is unchanged. City start: the Sandbox warp is immediately left of the spawn; a paved path leads east to a rune stone (`G`, `WorldFx.RuneStone`, a TalkInteractable with a prompt override) that says "YOUR NAME IS MC". CardSettings.Changed (never raised) was removed.
> - **Areas (2026-10-23):** one overworld scene builds the area named by `WorldSession.CurrentArea` (saved as `area`), defined in `WorldAreas`: **city** = The City Ruins, a 64x42 open NieR-style ruined city (main map; new games start here; generated layout, reachability checked) and **sandbox** = Sandbox (the old Mossline Station map, practice fights + summon beacon). `W` on a map is a glowing cyan warp pad (`WorldFx.WarpPad`, `WarpInteractable`): interact to travel; `OverworldBuilder.TravelTo` saves and reloads as the other area, arriving beside its warp. Encounter ids are per area (sandbox keeps the old scene-only prefix for old saves). **Settings** are one shared list (`GameSettings.Options`: label, toggle, help) shown by the title screen and the battle pause menu, each row with a "?" (`InkStyle.HelpBadge`) whose help shows in `InkStyle.HelpPanel`. (Superseded: the old Sandbox Mode toggle that skipped the title is gone; see Esc → Settings above.) Battle HUD: the turn counter is back top-left ("TURN 3", one line). Overworld party HUD: the level sits on the name line, big and gold.
> - **Polish (2026-10-23, later):** Ult cards (party and enemy) are a 2x bust of the battle sprite (CardForge `sprite_art`), so the crew's cards match their shared model. `UltShine` (replaces UltGlow): light bands sweep across Ult cards every 2.6 s plus twinkling pixel sparkles, masked to the card art; also on every channelled card. Ult pips are 26 px on a dark strip, solid dark when empty, pulsing orange/gold when ready (inspect: 34 px). End-of-turn ticks: a pause, an "End of Turn" banner, then each tick on its own beat. IM Up's icon is Raven fb877 (violet rising arrow, user's pick; atlas RowExtra 7).
> - **Statuses (2026-10-23):** 52 px icons under units and 64 px by the HP bar on the inspect screen, both `StatusChip.Create`; hover shows `StatusTooltip` (name, description, turns/stacks, big text). **Beats:** `BattleView.PlayStatuses` lands newly gained statuses `StatusDelay` after the hit (buff/debuff FX + the name floating up); end-of-turn ticks start after `TickLead` and play one by one (`TickHold`), labelled with the status (e.g. Poison).
> - **Fanned hand (2026-10-23):** Slay the Spire-style. `HandView` squeezes the spacing (`SpacingFor`, up to 0.82 card widths) so any hand fits between the left edge and the Go button, tilts cards outward (`SlotAngle`, 4 deg per card, max 14) on a gentle arc (`ArcDrop`). Cards ease to their pose in `CardView.LateUpdate` (`SetPose` replaced `AnimateTo`). Hover: the card straightens, grows to 1.25x and comes to the front, risen just enough that its bottom edge stays put (no hover flicker); neighbours lean aside (`HoverPush`).
> - **Battle pass (2026-10-23, later):** hand cards are now 5x (240x320, spacing 248, hand at y 178); the hold popup is 10x with live numbers (DAMAGE / HEAL from the owner's current ATK and bonuses, before DEF; `CardView.LiveNumbers`). The info screen (`UnitInspectView`) holds cards the same polled way (`AbilityCard`), shows an ULT pip row under HP and the Ult card for party and enemies, cards at 4x across the whole sheet. **Enemy Ults:** `EnemyDefinition.ritual`; enemies with one gain Ult points per cast and use it instead of a skill when full (`EnemyAI.ChooseIntent(..., moxieCap)`, `EnemyTurn`). Rust Sentry: *Meltdown* (140% ATK, all), Scrap Stalker: *Toxic Swarm* (90% ATK all + Poison). Their Ult art is a 2x bust from the idle sprite (CardForge `ULT_ENEMIES`). The SampleContentGenerator doesn't know these yet. **HUD:** the Turn counter and AP pips are gone (round banner and empty slots say it); the 3 queue slots sit above the hand and show the card icons. **Pacing:** each cast first channels its card (`BattleView.ChannelCard`: slot -> above the caster, glow and bob, flare), then the cast animation, then a pause; constants `ChannelFly/Hold/Release`, `CastPause`, plus longer damage/heal waits.
> - **Pixel cards (2026-10-23):** cards are 48x64 px pixel art (hand 4x, popup 10x, inspect 4x, party menu 5x), modelled on the user's "Soul" reference: owner-tinted swirl, pale rim with plum outline, rank pips across the top, small type icon top-right, a centre icon per card, the name on a bottom plate. Ult cards are full art (MC's reference recoloured per member) with a pale border and no text. Pieces are painted by `Tools/CardForge/card_forge.py` into `Resources/Cards/` and assembled by `CardView.BuildFace` via `CardArt`. The hold popup shows the card left of centre with an ink text panel beside it (`CardView.DetailPanel`). Hand cards: the wheel previews other ranks in place; holding opens the popup (now polled per frame, with the drag threshold raised to the hold slop so a shaky hold isn't a drag).
> - **Battle effects (2026-10-23):** pixel animations from the free **Super Pixel Effects Gigapack** (Will Tice / unTied Games; credit required, raw files must not be publicly redistributed) live in `Resources/Fx/` as sheet + frame list. `FxLibrary` plays them at 15 fps and 6x on the battle FX layer; `BattleFx` holds the user's picks: a hit effect per attacker element, plus crit, heal, buff/debuff landing, poison tick, unit destroyed, Ult wind-up on the caster and the big Ult impact, and a looping alert beside LEVEL UP! on the results. To swap one, copy its spritesheet into `Resources/Fx/` (txt lines `frameNNNN x y w h`) and change the name in `BattleFx`.
> - **Fonts (2026-10-23):** every font getter in `UIFactory` now returns the pixel font **monogram-extended** (CC0), so the UI matches the pixel art. It has one weight, so Bold and Italic render plain. All sizes go through `UIFactory.Px` (scale `FontScale` = 1.6, because monogram's capitals are 7 px of a 16 px em); rich-text sizes use `UIFactory.Size(n)` instead of a literal `<size=n>`. The importer sets Hinted Raster and the glyph texture is point-filtered at runtime.
> - **Transitions:** `Combat.View.ScreenFade` is a persistent ink fade (DontDestroyOnLoad, unscaled). `ScreenFade.LoadScene` fades out, loads, and fades in after the new scene builds. The overworld uses it into and out of battle and for area reloads, and the battle uses it for Leave Fight.
> - **Title screen + save slots:**
>   - `TitleScreen` opens over the first area of a session and pauses the world. It has Continue (latest slot), New Game, Load, Settings (fullscreen) and Quit. New Game and Load list 3 slots with the leader, level and save time; overwriting asks first.
>   - Saves are now `save_1.json` .. `save_3.json` (`SaveGame.Slot`, `Peek`, `LatestSlot`, `DeleteAll`). The old `save.json` moves to slot 1 automatically.
>   - `WorldSession.StartSlot` / `TitleDone`. Tools > Overworld > Delete Save Data wipes every slot.
>   - The game's name is the working title `TitleScreen.GameTitle` ("REVERSE SHELL").
> - **Overworld:** the controls hint bar was removed.
> - **NPCs:** knight reskins in civilian colours with no glow (vendor in red, courier in blue and gold, drifter in drab green).
> - **Still open from the plan:** none of the user's art or music has gone in yet. The user declined an art pipeline, a music system and chapter data for now.
>
> **Inspect screen (previous), Reverse: 1999-style:** holding a unit (or Space on the ringed one) zooms the battlefield in, and `UnitInspectView` was rewritten for it.
> - **Zoom:** the backdrop and `Stage` scale to 1.5x (the 6x sprites become exactly 9x) and slide the unit to the left (`FocusPoint` -470, -40). The hand and HUD fade out through their CanvasGroups.
> - **Right-hand column:** the name (big serif over a gold hairline), HP (numbers and a bar), STATS (the six stat boxes, with +/-% badges when changed) and CARDS (the unit's card faces at 1x; scroll over one to flip its rank).
> - **Removed on purpose:** effects, Ult pips, the profile poster and icons, and the enemy "next move" box.
> - **Closing:** Esc, Space or clicking the battlefield closes it, and everything eases back.
>
> **Fixes:** the queue panel now receives clicks (`UIFactory.CreateImage` makes images click-through by default, so `queueBg.raycastTarget` is now true). Overworld sprites use `animatedCharacterScale` **2.5** (it was 3), in code and in the scene.
>
> **Latest pass (all knights, bigger sprites, queue click):**
> - **Party:** Heal Bot (sage plating, green lights) and Lucy (violet-grey plating, purple lights) are knight reskins too (`knight_model.SKINS`), so the whole party and both machines share one look. Their party-menu cards come from `knight_model.portrait` as well. Only the NPCs are still on the rig.
> - **Rank display:** card ranks are always stars. `CardSettings.RankStars` is fixed to true, and the "Card Rank: Stars / Pips" button is gone from the pause menu.
> - **Select reticle:** the white focus ring (and the orange card-target ring, which uses it) no longer has the dark shadow ring behind it. The red lock-on keeps its shadow.
> - **Bigger sprites:**
>   - Battle sprites are 64 px frames at **6x** (`UnitView.SpriteSize` 384), and the rim-glow offsets were scaled to match.
>   - The enemy intent cards are 96 px (`IntentSize`, at `IntentY` 64).
>   - Overworld animated sprites use `animatedCharacterScale` **3** (it was 2), changed both in code and in `Overworld_Mossline.unity`.
> - **Undo button removed:** clicking the **queue panel** (`HudView.queueButton`, which is interactable only when there's something to undo) calls `BattleView.OnReturnAll`. That unwinds this turn's whole undo stack, so every queued card goes back to the hand and all AP returns. Moves and merges made in the hand are undone too.
>
> **Enemies (newest): the Free Knight too.** The machine enemies use the same knight, reskinned per character (`knight_model.SKINS`). Enemies face left in battle, which the game handles by mirroring.
> - **Rust Sentry:** rusted plates, dark steel joints with red lights, a red visor and collar.
> - **Scrap Stalker:** mossy plates, rust joints with amber lights, an amber visor and collar.
> - **Build:** `chibi_forge.build_sheets` routes every name in `SKINS` (MC, RustSentry, ScrapStalker) to the knight. NPCs, Heal Bot and Lucy are unchanged.
> - **Slash trail:** the sword's trail shares the steel colours, so on attack frames the big un-outlined steel patches (`swoosh`, 80 px or more) keep their pale colours.
> - **MC's party card:** `knight_model.portrait` puts the reskinned idle pose at 3x in the 96×144 card, with glow pixels marked for the card's glow mask. The internals card opens its chest (`open_chest` now takes an `armour` colour set). The ability cards are unchanged.
>
> **MC's sprites (newest): the Free Knight, reskinned as a robot.** The source is "Fantasy Knight - Free Pixelart Animated Character" by aamatniekss (Colour2 / Outline sheets in `Tools/ChibiForge/reference/FreeKnight/`), built by **`Tools/ChibiForge/knight_model.py`**. `chibi_forge.build_sheets` checks the knight first, then the sample, then the card model.
> - **Reskin (`RESKIN`):** the steel becomes white plating; gold and leather become dark joints with orange highlights; the red scarf becomes an orange collar; and the visor slit (interior outline pixels in the head, found per frame) glows orange.
> - **Frames:** 120×80 frames are cropped to 64×64 with one window for every clip (body centred, feet on row 61). Wide sword swings are clipped at the edge.
> - **Clips:** idle = `_Idle` (10). Walk = idle 0 + `_Run` (10). Slash = `_AttackComboNoMovement` (10). Spellcast = idle 0 + an orange orb (12). Hurt = `_Hit` + `_DeathNoMovement` (11).
> - **Directions:** right as drawn, left mirrored; down uses right and up uses left (no front / back views).
> - **Size:** the knight is about 38 px tall, so about 0.6 units, a bit smaller than the machines (0.73).
> - **Licence:** free and commercial use and modification are allowed, with credit appreciated (credited in MC's `CREDITS.md`). No redistribution on its own. Its page says "can't be used in AI creations"; the user reviewed that and chose to proceed.
>
> **MC's sprites (previous): the user's own animation.** `Tools/ChibiForge/reference/sample/` holds the Aseprite sources and the exported sheets, and **`Tools/ChibiForge/sample_model.py`** builds MC's sheets from them. `chibi_forge.build_sheets` routes MC there before the card model.
> - **Idle:** 10 frames of 46×55, a front three-quarter view facing left.
> - **Walk:** 24 frames of 45×58 (a 4×6 grid), a side view facing left, played as one loop after a standing frame 0 (idle frame 0).
> - **Directions:** left as drawn, right mirrored. There's no front or back walk yet, so down uses the left walk and up the right one.
> - **Not used:** `from idle.png` (2 transition frames), since the game has no idle-to-walk transition.
> - **Slash, spellcast and hurt** are built from idle frame 0 (a lunge with an orange arc, an orb, a fall) until the user draws them.
> - **Walk speed:** `SampleContentGenerator.AnimSet` now sets the walk fps so one cycle takes 0.8 s whatever the frame count (rig 12 frames at 15 fps, MC 24 at 30).
> - Heal Bot and Lucy still use the card model, and the card portraits are still the card art.
>
> **Crew sprites (Heal Bot and Lucy):** the crew's overworld and battle sprites are their **card art**, animated: **`Tools/ChibiForge/card_model.py`** uses `reference/MC.png` (recoloured for Heal Bot and Lucy through `chibi_forge.reference_pixels`). `chibi_forge.build_sheets` hands the party to it; NPCs and machines stay on the rig.
> - **Sword:** removed (`SWORD = False` clears the blade, leaving the raised gauntlet).
> - **Walk:** the legs (near x ≤ 29 from row 46, far from row 45) swing side to side against each other, the stepping foot lifts a pixel, and the body bobs. 13 frames, with frame 0 standing.
> - **Back view (walking up):** repainted on the art's silhouette (`back_view`). The head is all hair, with wavy light strands, a dark rim and a crown shine. A long hair cascade runs down the back to the waist with uneven tips, and the mane behind the left shoulder takes the hair colour. The front details (gold trim, gems, skin) become back plates in the armour ramp. The crest and the raised gauntlet stay as drawn.
> - **Size:** native 64 px frames. Shrinking to the machines' size with 84 px frames was tried and **reverted**: the animation scrolled the character off screen in the UI. Unity does *not* resize these textures (`nPOTScale: 0`). The likely cause is that `SpriteAnimationSet.frameSize` is stored in each `Anim_*.asset` and only updated by **Generate Sample Content**, so 64 px rects were being cut from 84 px sheets. To shrink the crew, scale the art down in 64 px frames instead, or fix the import settings first.
> - **Party menu:** the element · class line under the names (card front and back) was removed; it will be replaced later.
> - **Directions:** left is the mirror of the art.
> - **Other clips:** idle is a breath, slash a lunge with a glow arc, cast an orb, and hurt a fall.
>
> **Battle UI (newest):**
> - **Reticles** (`TargetReticle`): several concentric rings of **uneven** arcs (`Segments` with (start, length) pairs, plus `Dashes` for random-length dashes), each spinning at its own speed and direction.
>   - Focus: outer +70°/s, middle −44, hairline +118, dashed inner −23.
>   - Lock: main −110, outer +57, inner +163, still ticks.
> - **Queue slots** are plain empty frames (no "+").
> - **AP:** the help text under the AP pips ("Click card: queue…") was removed; the AP label and pips stay.
>
> **Sprites (rig, a style test; the crew now use the card model above):** every character (crew, NPCs and machines) is drawn by the one parametric rig in **`Tools/ChibiForge/chibi_forge.py`** again, so everything shares one style. This is a placeholder until the user draws the real sprites.
> - **Smoother animation:** poses are eased curves (`lerp_keys`, smoothstep between keyframes) sampled per frame. Idle has 8 frames (a breath with the hair lagging), walk 13 (frame 0 stands, then a 12-frame stride), slash 10 (a slow wind-up, a fast swing, an eased recovery), spellcast 12 and hurt 9 (recoil and kneel, then tip over, bounce a pixel and lie still).
> - **Clip rates** in `SampleContentGenerator.AnimSet` were raised to keep the old timings: idle 6, walk 15, slash 22, spellcast 20, hurt 15 fps. Re-run **Generate Sample Content** after rebuilding the sheets.
> - **Crew outfit (rig `SPECS`):** no sword (strikes leave a glowing arc, `Pose.trail`). Big white plates: the shoulders sit out at ±6.6 with 1.35× pauldrons, and there's a broad breastplate, wide hip plates and big gloves (1.9). The dark sleeves and leggings (`Spec.joint`) are slim (0.68 / 0.7×) and carry glowing knee and elbow pads; the gloves have a glowing core (`Spec.hi` ramp). An **earpiece** (`headgear='earpiece'`) is a crimson pod with a gold rim, a glowing light and a gold fin, like the card's ornament. **Eyes** are drawn like the card (`card_eye`): a warm lash with a dark outer corner over an iris that's dark on top and glowing below. Wings were tried and removed (`wings()` remains but is unused). No capes. MC glows **orange** (`ORANGE_HI`, eyes and trim; `mc.glowColor` is orange in the generator too).
> - **Hair:** the crew have no separate `back_hair`, so the hair is one colour from every side. From behind, the crown and the long hair are one shape with strand lines (`back_hair(over=True)`).
> - **Menu text:** the Esc menu title is **"Party"** (was "Crew"), and the overworld hint says "Esc: party".
> - **Unused:** `hero_model.py` and `reference/Hero.png` (the user's single front-view girl) are kept but no longer called; `build_sheets` no longer hands the crew to any reference model.
> **Sprites (previous):** the crew and NPCs came from **`Tools/ChibiForge/hero_model.py`**, using the user's single front-view sprite `reference/Hero.png` (29×47, 16 colours, native 64 px frames). It was chosen because it's easier to animate.
> - **Layers:** ponytail (behind), two legs (from row 37, split at x 16), two arms (x ≤ 10 / x ≥ 23, rows 24–37) and the body. A moved layer fills the gap at its seam with its own source pixels.
> - **Animations:** idle is a breathing dip. Walk has 9 frames (frame 0 stands): legs lift in turn, arms swing, the body bobs and the ponytail trails, and the side views add a 1 px stride. Slash is a lunge with the right arm and an arc in the member's colour. Cast raises the arms with an orb at the chest. Hurt ends lying down.
> - **Directions:** down and right use the art as drawn, left is the mirror, and up is a back view (face painted with hair).
> - **Recolours (`SPECS`):** 4-tone hair (the ponytail's outline-coloured mass takes hair tone 1 on recolours), glowing eyes for the crew, the magenta accent (gloves, boots, collar), and the top and pants for NPCs. MC keeps the original colours.
> - **Size:** the character is 47 px tall in a 64 px frame, so it's smaller in the world than the 61 px girl. That keeps one pixel density with the machines.
> - **Superseded:** `girl_model.py` (Girl_walk.png) is no longer used. `chibi_forge.py` `build_sheets` now calls `hero_model`.
> **Sprites (older):** the crew and NPCs came from **`Tools/ChibiForge/girl_model.py`**, using the user's 6-frame front-view walk cycle (`reference/Girl_walk.png`, native 64 px).
> - **Walk:** the drawn cycle (7 frames, frame 0 stands).
> - **Other animations:** idle is a breathing dip; slash is a lunge with an arc in the member's colour; cast grows an orb at the hands; hurt ends lying down.
> - **Directions:** left and right reuse the front frames (no side views were drawn); up is a generated back view.
> - **Recolours (`SPECS`):** hair; glowing eyes for the crew; outfits for the NPCs.
> - **Superseded:** the robot model and the mech-arm base model are no longer used.
> **Sprites (older):** the crew and NPCs came from **`Tools/ChibiForge/robot_model.py`**, which uses the user's clean 64 px sprite `reference/Robot.png`. It's a 51×62 knight with a white helmet, white plate, a cape and a sword, used at native resolution with no scaling.
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

Read this first when resuming. See also `Planning/ROADMAP.md` and `Planning/GAME_PLAN.md` (roadmaps; the Planning folder is git-ignored) and [GAME_BIBLE.md](GAME_BIBLE.md) (vision).

## Project at a glance
- **Engine:** Unity **2019.4.14f1**, C# 7.3 only (no switch expressions or records). Project folder: `reverse-shell/`.
- **Game:** a non-commercial fan project (working title *Reverse Shell*). It has an HD-2D overworld (Octopath-style) and Reverse: 1999-style card-merge battles.
- **Look (new this session):** an **overgrown, sunlit city ruin** in the spirit of *NieR:Automata*, replacing the neon cyberpunk city.
- **Party (new in session 4):**
  - **MC** (the Prototype, the leader) is a standout robot model: a dark trench coat, a smooth plated head with **white glowing eyes** and a halo crest. It glows **white** for now. It is prepared to **channel several elements** later, with its hue following the element (`CharacterDefinition.channels`, `neutralGlow`, `glowColor`, `AfflatusInfo.GlowFor`).
  - **No blindfolds:** every robot has two glowing eyes in its element color and **scale-pattern armour trims**. In the portraits that's `eyes()` / `scales()` in PortraitForge. On the LPC sprites, a smooth `hair_buzzcut` plate replaces the visor helmets, with custom eye ramps `eye_moss` / `eye_virus` / `eye_white`, the `eyes:#hex` mask and the `trim:#hex` overlay; `'overlay'` can be a list.
  - **Heal Bot** (Earth, Medic) and **Lucy** (Void, Hacker) are the **same robot model** with different accents. Heal Bot is overgrown like a dryad (vines, leaves, flowers, a sapling) with a green glow. Lucy has cable twin-tails, circuit traces and glitch rows, with a purple glow.
  - 2B, V1 and V2 were retired. Old saves migrate (`SaveGame.Migrate`: Char_2B to Char_MC, Char_V1 to Char_HealBot, Char_V2 to Char_Lucy).
  - **Kits:** MC has Edge Strike and Sync Field, pool card Cross Slash, Ult Prism Burst. Heal Bot has Sap Mend and Bramble Lash, pool card Overgrowth, Ult Verdant Bloom. Lucy has Virus Spike and Corrupt (new ATK Down status), pool card Worm, Ult Zero Day.
- **Elements (2026-10-23):** picked to match the free Super Pixel Effects Gigapack animations: **Volt, Fire, Toxin, Earth, Light, Void** (the `Afflatus` enum, renamed in place from Star, Mineral, Beast, Plant, Spirit, Intellect, so saved data and balance are unchanged). Wheel: Volt > Fire > Toxin > Earth > Volt; Light and Void beat each other. MC is Volt (multi-element later), Heal Bot Earth, Lucy Void, Rust Sentry Fire, Scrap Stalker Toxin. The element orb icons (`ELEMENT_ICONS` in `Tools/IconForge/card_icons.py`) are still the old picks.
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
  - **Shapes:** straight panels with 1–2 px gold hairlines, the **hero frame** (a double rule with corner ticks) for key pieces, and subtle paper grain. No slants, tilts, halftone or burst rays. **Type:** all text is the pixel font **monogram** (see Fonts above); `SerifFont` and the other getters are kept as names for call-site intent. Letter-spaced caps labels (`InkStyle.Label`).
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
  - **UI font:** all text is **monogram** (pixel, one weight). Card text shows every number and percentage bigger and gold (`UIFactory.EmphasizeNumbers` + `FitRich`, a rich-text-aware best fit).
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
- `Resources/Fonts/`: **monogram-extended** by datagoblin (CC0), all UI text. The old OFL fonts (M PLUS 1p, Anton, Cormorant Garamond) are still there but unused. Credits are in that folder.
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
- More fights are needed for levels to matter: random encounters in the tall grass would supply them.
- Pick a new title (ideas are in the Bible).
- An optional sepia NieR-style pass on the battle UI, to match the overworld.
- MC's element channeling: the data and the glow hook are in place (`channels`, `AfflatusInfo.GlowFor(member, channeled)`). Next: a battle mechanic that picks the element, then pass it to the battle glow, overworld glow and card portraits.
- More Parts as loot (the results screen), and Part effects beyond stats (passives).
- A second map (the Sunken Arcade or a Resistance camp) with warps and a data-driven dialogue box.
- Effects (buffs/debuffs) are still missing from the details screen ("can be somewhere else").
- Before making player builds, add `Sprites/Diffuse`, `Legacy Shaders/Particles/Additive`, `Legacy Shaders/Particles/Alpha Blended` and `Legacy Shaders/Transparent/Cutout/Diffuse` to Always Included Shaders.
