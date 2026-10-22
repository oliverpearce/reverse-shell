# GAME BIBLE: *NEON HEX* (working title)

> *Rain on chrome. Static in the air. Somewhere under the neon, someone is singing a spell.*

This is the creative source of truth. When a design question comes up ("should this card do X?", "what does this district look like?"), the answer should fit what's written here. If it doesn't, change the bible on purpose rather than drifting.

Other title ideas: *Static Saints*, *Glitch & Glimmer*, *Hexline*, *Lowlight Requiem*.

---

## 1. Elevator pitch
A top-down pixel-art RPG set in a rain-soaked cyberpunk megacity where magic has leaked into the network. You explore the city tile by tile, like a classic monster-collector. You recruit a crew of misfit spellcasters and fight in tactical card battles where merging cards powers up spells and builds toward dazzling, pastel-bright **Rituals**.

**Genre:** top-down exploration RPG with card-merge tactical combat.
**Influences:** Pokémon (overworld, encounters, crew collecting), Reverse: 1999 (merge-card combat, stylish casts), Slay the Spire (card readability, card craft), Cyberpunk / Blade Runner (mood), Hyper Light Drifter (neon pixel art).

## 2. Design pillars
1. **Every card is a little spell.** Cards have personality: a name, a cast animation and a clear effect. Making cards should be fun for the developer too, and the modular card system is the heart of the game.
2. **Merge to rise.** The core of combat is arranging your hand: setting up merges, spending actions wisely and timing your Ritual. Fights reward planning, not grinding.
3. **Neon in the dark.** The look is dark grunge lit by neon and spell-sparkle. Light means hope, and light means magic.
4. **A small city, dense with secrets.** Hand-built districts with alleys, rooftops, hidden shops and side characters, rather than a huge empty map.
5. **Found family.** The crew are outcasts who choose each other. Recruiting someone should feel like gaining a friend, not a stat stick.

## 3. Setting

### The city: **Lowmarrow**
A vertical megacity built in layers. The rich live above the cloud line in the **Spires**. Everyone else lives below in the **Undercity**, where the rain never stops and the only light comes from signs, screens and spells.

### The magic: **Glimmer**
Years ago, a corporate experiment tried to run code on something older than electricity. What leaked out was **Glimmer**: sparkling, living static that hums in wires, pools in puddles and answers to song, rhythm and will.
- Glimmer shows up as **sparkles, glitter, prismatic noise and soft pastel light** against the grime. It is the only beautiful thing in the Undercity.
- People who can shape it are **Hexers**. Corporations hunt them, and gangs want them on their payroll.
- Raw Glimmer gone wrong becomes **Static**. Static infects machines, animals and people, and creates the corrupted enemies you fight.

### Factions (draft)
| Faction | Vibe | Role |
|---|---|---|
| **Halcyon Dynamics** | Sterile white chrome, holo-halos | The corp that caused the leak and now sells "containment" |
| **The Wiremothers** | Punk witches in cable-knit shawls | An underground Hexer network that becomes the crew's allies |
| **Chrome Saints** | Cyber-church, stained-glass LEDs | A cult that worships Static as divine |
| **The Drowned Market** | Flooded bazaar, lanterns on boats | Neutral traders, shops and an information hub |

### Districts (one level each)
1. **Rainline Station**: the starting area. Abandoned transit hub, flickering signs. Tutorial.
2. **Neon Row**: a strip of bars, arcades and noodle stalls. The first recruit.
3. **The Drowned Market**: a canal maze of boats and lantern light.
4. **Rustbelt Yards**: scrap towers, junk golems and Rook's home turf.
5. **The Chapel of Static**: the Chrome Saints' cathedral, in a glitch-art style.
6. **Halcyon Spire**: the final ascent from grime to blinding white chrome.

## 4. Characters

Each character is a **class (archetype)** with their own two incantations and one Ritual.

| Name | Class | Resonance* | Role | Hook |
|---|---|---|---|---|
| **Miku** *(Hatsune Miku)* | Cantor | Star | Single-target damage plus team buffs | Her songs bend Glimmer through every speaker in the Undercity. Party leader. |
| **Teto** *(Kasane Teto)* | Bruiser | Mineral | Area damage and armor breaking | Drill-haired brawler who swings a baguette like a crowbar. |
| **Rei** *(Adachi Rei)* | Android Medic | Intellect | Healing (repair) and poison (viruses) | An android who patches the crew and corrupts enemy code. |
| *(future)* | Glitchwitch | Spirit | Debuffs and stuns | Talks to Static and might be partly made of it. |
| *(future)* | Courier | Beast | Speed, AP gain, Moxie feeding | Rooftop runner with a cyber-hound. |
| *(future)* | Archivist | Intellect | Card manipulation and rank-ups | A Halcyon defector who brings the stolen files. |

\*The Resonance names are placeholders; see §6.

> **Fan-project note.** The main cast are existing virtual singers: Hatsune Miku © Crypton Future Media, INC.;
> Kasane Teto © TWINDRILL; Adachi Rei © missile39. That's fine for a personal or non-commercial fan game, with
> credit given (see `Art/Characters/CREDITS.md`). If the game ever goes commercial, either get permission
> (Piapro for Miku) or swap in original characters. Vesper, Rook and Moss were the originals; their kits are the same.

**Protagonist:** the player is **Miku**, the party leader. She walks the overworld, and Teto and Rei join her in battle.

### Enemy families
- **Static-touched animals**: Ashen Hounds, glitch-pigeons, rat swarms (Beast)
- **Junk constructs**: Shard Sentinels, scrap golems (Mineral)
- **Halcyon security**: drones and containment agents (Intellect)
- **Chrome Saints**: zealots and choir-bots (Spirit)
- **Overgrowth**: neon fungus and vine-wraiths (Plant)

## 5. Visual identity

### Mood
Dark, wet, grimy surfaces under **saturated neon** and **soft magical sparkle**. Think of a puddle reflecting a pink sign with glitter drifting through it.

### Palette (starting point)
| Use | Color | Hex |
|---|---|---|
| Night base | Bruised black-violet | `#0E0B14` |
| Grime | Wet asphalt | `#2A2733` |
| Neon A | Hot magenta | `#FF2E88` |
| Neon B | Electric cyan | `#2EF2FF` |
| Neon C | Acid green | `#B6FF3B` |
| Warm light | Sodium orange | `#FF9A3C` |
| Glimmer | Sparkle white / pale gold | `#FFF6D6` |
| Ritual pastels | Lavender, mint, peach, baby blue | per character |

**Rule:** grime is low-saturation and dark, and light sources are high-saturation. Pastel is reserved for pure magic: Rituals, Glimmer and healing. That's why Ritual cards are pastel. They should feel like the one clean, bright thing in the room.

### Pixel art spec: HD-2D (Octopath Traveler II style)
The overworld uses **pixel-art sprites in a 3D diorama**:
- **World:** 1 Unity unit is 1 tile. Blocks and floors are textured with 16×16 pixel art, stored at 4× as 64 px cells with point filtering. The current source is the CC0 **CyberCity** atlas by warlloyd.
- **Characters:** Octopath-style **32×48 px** pixel sprites (64 px LPC frames) composed by `Tools/SpriteForge/lpc_compose.py` from the CC-BY-SA Universal LPC generator. Each has idle, walk, slash, spellcast and hurt animations in 4 directions. In the overworld they're billboards with blob shadows. In battle the party faces right and enemies face left, idling, slashing or casting, and collapsing when defeated.
- **Camera:** a narrow FOV (around 28°), pitched about 38° and following the player from the south.
- **Lighting:** real point lights from neon lamps light both the world and the sprites (`Sprites/Diffuse`). Signs are emissive, and fog adds depth.
- **Post-processing:** bloom (neon glow), depth of field focused on the player (the tilt-shift diorama blur), vignette, film grain and a little chromatic aberration.
- **Battle sprites:** 64×64 px for characters and 64–128 px for bosses, with a 2–4 frame idle loop.
- **Outlines:** 1 px dark (not black). Use a darker shade of the base color.
- **Atmosphere:** rain particles, Glimmer sparkle particles (pastel twinkles), flickering signs, and wet glossy ground.

### UI
- Chunky pixel font for headings and a clean, readable pixel font for card text. Readability always wins. (Pick fonts with an open license, such as OFL or CC0, and check before shipping.)
- Cards look like **tarot cards printed on cheap holo-foil**: a dark card body, a neon-colored frame for the owner, and the effect text spelled out in full.
- Damage numbers are big, chunky and colored by meaning. Gold means advantage, orange means crit, purple means poison and green means a heal.

## 6. Terminology (originality pass)
The combat currently borrows Reverse: 1999's terms. Before release, swap them for originals. The code only needs display-name changes, not a logic rewrite.

| Current (in code) | Proposed | Notes |
|---|---|---|
| Afflatus | **Resonance** | The kind of Glimmer a being is tuned to |
| Star / Mineral / Beast / Plant | **Neon / Chrome / Feral / Bloom** | Same cycle: Feral > Bloom > Neon > Chrome > Feral |
| Spirit / Intellect | **Hex / Code** | Each is strong against the other |
| Moxie | **Charge** | Builds toward the Ritual |
| Incantation | **Spell** | |
| Ritual | **Ritual** *(keep)* or **Overture** | |
| Reality / Mental damage | **Physical / Special** | ✅ Done in code (`DamageType`, Physical/Special DEF). |

## 7. Core loops
- **Moment to moment (battle):** read the enemy intents, arrange the hand, set up merges, spend AP, time the Ritual, then resolve.
- **Session (overworld):** explore a district, talk to people, find items, trigger encounters, heal at safe spots, then push to the district boss.
- **Long term:** recruit crew, level them up, unlock card upgrades and new cards, open new districts and uncover what Halcyon did.

## 8. Encounters and the overworld
- **Static zones:** glitching floor tiles (the "tall grass") that can trigger random fights as you walk through them.
- **Visible enemies:** some foes patrol the map and start a fight on touch, with a chance to sneak around them.
- **Story fights:** scripted battles with special rules, such as a boss that changes intent mid-fight.
- **Safe spots:** **Glimmer Shrines**, neon-lit altars where you heal the crew and save.

## 9. Progression (draft)
- **Levels:** stats grow with level along per-character curves.
- **Card upgrades:** spend **Shards** (found loot) to add effects to a card's ranks. This is the deep card-craft layer.
- **Mods:** one equippable item per character, such as "+1 Charge when this character merges".
- **Crew:** up to 4 recruited, with 3 fighting at a time. The formation swaps between fights.

## 10. Audio direction
Lo-fi synthwave and trip-hop with rain ambience. Spell casts get **chime and sparkle** layers, and Rituals get a short choir swell. Enemies made of Static use bitcrushed, glitchy sounds.

## 11. Tone guide
- **Do:** melancholy but warm, wry humor, small kindnesses in a cruel city, beauty in grime.
- **Don't:** edgy for its own sake, grimdark despair, or real-world brands.
- **The writing test:** would this line work whispered under a flickering sign in the rain?

## 12. Open questions
- [ ] Final title
- [x] Protagonist: Miku leads the party.
- [ ] Is crew death permanent in story mode? (Recommendation: no. Fainted members recover at shrines.)
- [ ] How many districts for a first playable? (Recommendation: 1–2.)
- [ ] Monetization or scope: a hobby project, a jam game or a commercial release?
