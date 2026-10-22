# GAME BIBLE (working title: *NEON HEX*, renaming pending)

> *Sunlight through a broken roof. Wind in the grass that grew over the road. Somewhere in the green, a machine is still keeping watch.*

This is the creative source of truth. When a design question comes up ("should this card do X?", "what does this district look like?"), the answer should fit what's written here. If it doesn't, change the bible on purpose rather than drifting.

**Direction change (2026-10-22):** the game moved from a rainy neon cyberpunk city to an **overgrown, sunlit city ruin** in the spirit of *NieR:Automata*. **2B** replaced Miku as the lead. The combat system, party structure and HD-2D presentation are unchanged.

The old title *NEON HEX* no longer fits. Title ideas: *Overgrowth*, *Verdant Static*, *After the Last City*, *Moss & Machine*, *Quiet Steel*. The code still uses `NEONHEX_POSTFX` as a define name, which doesn't matter to players.

---

## 1. Elevator pitch
An HD-2D RPG set in a city that humanity left centuries ago and nature quietly took back. Androids explore the sunlit ruins, recruit a small crew and fight the machine lifeforms that still wander the streets. Battles are tactical card fights in which merging cards powers up attacks and builds toward a **Ritual**, a once-a-fight overload program.

**Genre:** exploration RPG with card-merge tactical combat.
**Influences:** *NieR:Automata* (mood, setting, androids vs machines), Octopath Traveler II (HD-2D diorama look), Reverse: 1999 (merge-card combat), Slay the Spire (card readability).

## 2. Design pillars
1. **Every card is a little technique.** Each card has a name, a cast animation and a clear effect. The modular card system is the heart of the game.
2. **Merge to rise.** Combat is about arranging your hand: setting up merges, spending AP well and timing your Ritual. Planning beats grinding.
3. **Beauty in the ruins.** The world is quiet, green and sunlit. Danger is the exception, so the calm stays meaningful.
4. **A small world, dense with secrets.** Hand-built districts with rooftops, collapsed tunnels and side characters, rather than a huge empty map.
5. **Found family.** The crew choose each other. Recruiting someone should feel like gaining a friend, not a stat stick.

## 3. Setting

### The city ruins
A great city, long empty. Towers lean on each other, roads have cracked open into meadows, and trees grow out of office floors. Morning haze hangs over everything. The only sounds are birds, wind and the creak of machines.

### Who's here
- **Androids** were built to retake the world. They're sent down from an orbital base, the **Bunker**, and save their progress at **Access Points** (field terminals).
- **Machine lifeforms** are old war machines that have outlived their makers. Some still fight, some have grown strange, and a few have grown curious.
- **The Resistance** is a camp of androids on the ground who trade, scout and patch each other up.

### Factions (draft)
| Faction | Vibe | Role |
|---|---|---|
| **The Bunker** | White, sterile, orbital | Command; sends orders and the protagonist |
| **The Resistance** | Patched tents, solar lamps, scrap furniture | The crew's home camp: shops, rest, quests |
| **Machine packs** | Rusted plating, glowing eyes | The common enemies of the city |
| **The Hollow Choir** | Machines in robes, ringing bells | A machine cult; mid-game antagonists |

### Districts (one level each)
1. **Mossline Station**: the starting area. A collapsed transit plaza overrun by grass. Tutorial. *(built)*
2. **The Sunken Arcade**: a flooded shopping mall with fish swimming between the escalators.
3. **Rustbelt Yards**: a factory district where machines still build machines.
4. **The Canopy**: high-rises swallowed by a forest growing on their roofs.
5. **The Hollow Cathedral**: the Hollow Choir's bell tower.
6. **The Spire**: the final climb toward whatever is still broadcasting from the top.

## 4. Characters

Each character is a **class (archetype)** with two cards and one Ritual.

| Name | Class | Resonance* | Role | Hook |
|---|---|---|---|---|
| **2B** *(NieR:Automata)* | YoRHa Battler | Star | Single-target damage plus team buffs | A combat android from the Bunker. Calm, precise, and party leader. Her Pod fights alongside her. |
| **V1** | Assault Unit | Mineral | Area damage and armor breaking | A white-plated combat robot. Few words, heavy hits. Cards: *Spin Saw*, *Armor Breaker*, Ritual *Overdrive*. |
| **V2** | Support Unit | Intellect | Healing (repair) and poison (viruses) | A charcoal-plated support robot that patches the crew and infects enemy code. Cards: *Patch Protocol*, *Virus Spike*, Ritual *System Restore*. |
| *(future)* | Hacker | Spirit | Debuffs and stuns | Breaks into machines from the inside. |
| *(future)* | Scout | Beast | Speed, AP gain, Ult feeding | A rooftop runner with a salvaged machine dog. |
| *(future)* | Archivist | Intellect | Card manipulation and rank-ups | Keeps what is left of the human records. |

\*The Resonance names are placeholders; see §6.

**2B's kit:** *Virtuous Contract* (a heavy single-target sword strike), *Pod Support* (raises the team's card damage) and the Ritual *Pod Laser* (two huge hits plus team ATK Up).

> **Fan-project note.** 2B (NieR:Automata) © SQUARE ENIX. That's fine for a personal, non-commercial fan game with
> credit given (see `Art/Characters/CREDITS.md`). A public or commercial release would need an original lead or
> permission. V1 and V2 are original generic robots. Earlier casts (Vesper, then Miku, plus Teto and Rei) used the same kits.

**Protagonist:** the player is **2B**. She walks the overworld, and V1 and V2 join her in battle.

### Enemy families (machine lifeforms)
- **Sentries**: heavy, slow, armored (Rust Sentry). Mineral.
- **Stalkers**: quick scrap hunters that work in packs (Scrap Stalker). Beast.
- **Flyers**: drones and missile platforms. Intellect.
- **Choir machines**: bell-ringing cultists that buff each other. Spirit.
- **Overgrown machines**: half-plant, half-machine things rooted in place. Plant.

## 5. Visual identity

### Mood
**Quiet, hazy and sunlit.** Desaturated greens and warm concrete under a soft blue-white sky, with long shafts of light and drifting leaves. Melancholy, but peaceful. Machines bring the only strong, warm accent: rust and glowing red-orange eyes.

### Palette (starting point)
| Use | Color | Hex |
|---|---|---|
| Sky | Washed blue | `#85A8CC` |
| Haze / horizon | Pale cream | `#E0E0CC` |
| Concrete | Sun-bleached gray-beige | `#A59C88` |
| Foliage (shadow to light) | Moss, leaf, fresh, sunlit | `#24452A` `#356A34` `#55913F` `#8DBF57` |
| Rust | Machine plating | `#94622F` |
| Threat | Machine eye red | `#FF4729` |
| UI | Sepia paper with dark ink | `#D9D1B5` / `#454033` |

**Rule:** the world is soft and low-contrast. Strong saturated color is reserved for **threats** (machine red) and **power** (Ritual cards, heals). Rituals keep their pastel look. They're the one bright, clean thing on screen.

### Pixel art spec: HD-2D (Octopath Traveler II style)
- **World:** 1 Unity unit is 1 tile. Blocks and floors use 16×16 pixel art stored at 4× (64 px cells) with point filtering. The atlas is **our own**: `Art/Tiles/ruins.png`, painted by `Tools/TileForge/ruins_atlas.py` (mossy concrete, ivy-covered windows, grassy rooftops, overgrown road, leaves, bark, grass and vine sprites).
- **Props:** chunky diorama trees (bark trunk plus leaf blocks), bushes, rubble, swaying grass and flower billboards, vines spilling down walls, and Access Point terminals.
- **Characters:** Octopath-style **32×48 px** LPC sprites composed by `Tools/SpriteForge/lpc_compose.py`, with idle, walk, slash, spellcast and hurt in 4 directions. Machines are LPC plate armor recolored as rusted plating.
- **Camera:** a narrow FOV (around 28°), pitched about 38° and following from the south.
- **Lighting:** a dim scene lit only by the sun, broken into **dappled spots** as if filtering through leaves (a slowly drifting light cookie). Low green trilight ambient and haze. No lamps or point lights.
- **Post-processing:** gentle bloom, a warm, slightly desaturated color grade, a light vignette and a little grain. No depth-of-field blur (the user wants crisp sprites).
- **Atmosphere:** drifting leaves and pollen, dust motes, swaying grass.

### UI
- Readability always wins. Big numbers, few words.
- The overworld UI uses **sepia paper panels with dark ink**, a nod to NieR's menus. The battle UI keeps its current look for now; a sepia pass is a later option.
- Cards are a dark card body with an owner-colored frame and the effect spelled out in full.
- Damage numbers are big and colored by meaning: gold is advantage, orange is a crit, purple is poison and green is a heal.

## 6. Terminology (originality pass)
The combat borrows Reverse: 1999's terms. Before any release, swap them for originals. Only the display names change.

| Current (in code) | Proposed | Notes |
|---|---|---|
| Afflatus | **Resonance** | |
| Star / Mineral / Beast / Plant | **Sun / Steel / Wild / Bloom** | Same cycle |
| Spirit / Intellect | **Signal / Logic** | Each is strong against the other |
| Moxie | **Ult** | ✅ Shown as "Ult" in game |
| Incantation | **Skill** | |
| Ritual | **Ritual** *(keep)* or **Overload** | |
| Reality / Mental damage | **Physical / Special** | ✅ Done in code |

## 7. Core loops
- **Moment to moment (battle):** read the enemy intents, arrange the hand, set up merges, spend AP, time the Ritual, then resolve.
- **Session (overworld):** explore a district, talk to people, trigger encounters, heal at **Access Points**, then push to the district boss. HP carries between fights.
- **Long term:** recruit crew, level up, unlock card upgrades and new cards, open new districts.

## 8. Encounters and the overworld
- **Tall grass zones** (`,` on the map) are the natural place for random encounters later.
- **Visible machines** patrol, and touching one starts a fight.
- **Story fights:** scripted battles with special rules.
- **Access Points:** field terminals that restore the party (✅ built) and will become the save points.

## 9. Progression (draft)
- **Levels:** stats grow with level along per-character curves.
- **Card upgrades:** spend salvaged **Parts** to add effects to a card's ranks.
- **Chips:** one equippable passive per character, such as "+1 Ult when this character merges".
- **Crew:** up to 4 recruited, with 3 fighting at a time.

## 10. Audio direction
Soft, melancholy and choral: piano, strings and wordless vocals for exploration, with drums and distorted synths layered in for battle. The ambience is wind, birds and creaking metal. Machines get clanks and servo whines; Rituals get a choir swell.

## 11. Tone guide
- **Do:** quiet melancholy, small kindnesses, curiosity about the machines, beauty in decay.
- **Don't:** edgy for its own sake, grimdark despair, or real-world brands.
- **The writing test:** would this line work said quietly in a sunlit, empty street?

## 12. Open questions
- [ ] Final title (see the ideas at the top)
- [x] Protagonist: 2B leads the party.
- [ ] Should the battle UI get the sepia NieR treatment too?
- [ ] Is crew death permanent in story mode? (Recommendation: no. Downed members recover at Access Points.)
- [ ] How many districts for a first playable? (Recommendation: 1–2.)
