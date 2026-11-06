# Third-party assets and credits

Reverse Shell's code and the art its own tools draw (`reverse-shell/Tools/*`) are MIT licensed (see `LICENSE.md`).
Everything below belongs to its authors and keeps its own licence.

**"Not in this repo"** means the files are git-ignored and listed in `.github/PRIVATE_ASSETS.txt`. The playable
demo on itch.io includes them. A fresh clone opens and compiles, but those sprites, icons and effects are missing
(Reverse Shell → Bug Sweep lists what's absent).

| Asset | Used for | Author | Licence | In this repo? |
|---|---|---|---|---|
| A2 pixel-art study (artist unknown) and the hair from A2's *Last Cloudia* unit sheet (NieR:Automata collab) | The party's sprites (A3, and Erin, Ashe, Lotus and Zuko as recolours): the study's body, reworked (thicker legs, posed battle clips), wearing the Last Cloudia hair. Soul cards and Ult art use the study | (c) Aidis Inc. / Square Enix | Not licensed. Used in a free fan game only, not affiliated | **No** |
| A2 pixel-art study | A3's menu portrait and overworld legs | Artist not yet identified | Unknown | **No** |
| Pixel RPG Prototype Characters | The machines and NPCs (Settings → Machine Look: Snoblin, the default) | [Snoblin](https://snoblin.itch.io/pixel-rpg-free-npc) | Free and commercial use, may edit; no redistribution | **No** |
| Tech Dungeon Roguelite (DEMO) | The machines and NPCs (Machine Look: Tech Dungeon), the gunner recoloured | [Trevor Pupkin](https://trevor-pupkin.itch.io/tech-dungeon-roguelite) | Free and commercial use, may modify; no redistribution (the author counts a public repo as redistribution) | **No** |
| Super Pixel Effects Gigapack (free) | Battle effects | Will Tice / [unTied Games](https://untiedgames.itch.io/super-pixel-effects-gigapack) | Free to use in games with credit; no redistribution of the files | **No** |
| Raven Fantasy Icons (free) | Card and menu icons | [Clockwork Raven Studios](https://clockworkraven.itch.io/raven-fantasy-icons) | Personal use (free games); no redistribution | **No** |
| Free Card Packs | Card pack art | argametina | Free use, no resale or redistribution | **No** |
| Stylized Nature MegaKit | Forest models | [Quaternius](https://quaternius.com) | CC0 | Yes |
| monogram (extended) | Every font in the game | [datagoblin](https://datagoblin.itch.io/monogram) | CC0 | Yes |
| M PLUS 1p, Anton, Cormorant Garamond | Old fonts, no longer used | Their project authors | SIL OFL 1.1 (licence files alongside) | Yes |
| Retro PC model | The office PC | Not recorded yet | Believed CC0 | **No** (until the source is confirmed) |
| Lospec palettes | Party colours | AAP-64 by Adigun A. Polack, Endesga 64 by ENDESGA; Rosy 42, Cornsole2, Taffy 16, Fantasy 24 (authors on [lospec.com](https://lospec.com/palette-list)) | Free palettes | Yes (the colours) |
| 8-frame walk template | Walk timing only (no pixels used) | [MostlyDecentGames](https://mostlydecent.itch.io/8-frame-walk) | CC0 | No pixels used |
| Cyber City | Tiles in the earliest builds | [warlloyd](https://opengameart.org/content/cyber-city) | CC0 | Yes |

Original art drawn by the project's scripts: the ruins tiles (`TileForge`), the card frame, swirl, pips and card
back (`CardForge`), the NPC chibis and portraits (`ChibiForge`), the Soul Dummy and every map.

## Replaced for the public release (2026-11)
- **Fantasy Knight** by aamatniekss (the old enemy and NPC base): its licence says the assets can't be used in AI
  creations, and this project uses AI-assisted tools. The machines and NPCs now use Snoblin's and Trevor Pupkin's packs (`Tools/ChibiForge/npc_packs.py`).
- **Major Arcana Tarot Cards** by Stars in a Box (the old card back): its terms rule out AI projects. The card back
  is now drawn by `Tools/CardForge/card_back.py`.

Reverse Shell is a free, non-commercial fan game inspired by *Reverse: 1999*. It is not affiliated with Bluepoch,
Square Enix or Aidis.
