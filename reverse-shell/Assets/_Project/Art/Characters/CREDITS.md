# Character sprites

Every folder here (2B, V1, V2, RustSentry, ScrapStalker, NoodleVendor, Courier, Drifter) is composed by
`Tools/SpriteForge/lpc_compose.py` from layers of the **Universal LPC Spritesheet Character Generator**
(https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator):

- 64×64 frames, with a body of roughly **32×48 px** (Octopath-style proportions).
- Animations: `idle`, `walk`, `slash`, `spellcast`, `hurt`. Each sheet has 4 rows (up, left, down, right); `hurt` has 1 row.
- The license is **CC-BY-SA 3.0 / GPL 3.0**, with some layers also OGA-BY. **Credit is required.** Each folder has its own
  `CREDITS.md` listing every author, license and source for the layers used. Ship those credits with the game.
  Because CC-BY-SA is share-alike, edited versions of these sprites must stay under the same license.

To change a look, edit `RECIPES` in `lpc_compose.py`, then run `python Tools/SpriteForge/lpc_compose.py`. Then run
**Tools → Combat → Generate Sample Content** again in Unity.

## Character rights: non-commercial fan project
| Character | Rights holder | Notes |
|---|---|---|
| 2B (NieR:Automata) | © SQUARE ENIX | Fan work for **non-commercial** use only. Check Square Enix's fan-content guidelines before sharing publicly. |

V1, V2, the machines and the NPCs are original generic designs. The pixel art comes from LPC (open license); 2B's
**character design** belongs to the rights holder above. Retired recipes (Miku, Teto, Rei, StaticThug, StaticEnforcer)
are still in `lpc_compose.py` but their sprites are no longer in the project.
