# Reference art

`MC.png` is the user's reference sprite: a 48×63, 16-colour chibi knight. It was extracted at native resolution from an
upscaled image the user supplied (18× pixels, pink border and white background removed).

- **How it's used:** `chibi_forge.py` uses it directly as MC's card portrait (scaled 2× into the 96×144 card), and
  makes the opened-up "internals" card from it. The animated sprites are drawn by the rig to match its palette and proportions.
- **Rights:** the original artist and licence are not recorded here. Before sharing the game publicly, confirm you may
  use this art, or replace this file (the forge falls back to the rig portrait when it's missing).

`Base.png` is the crew's in-game base model: a girl with a mech backpack and two mechanical arms. It was extracted from
an image the user supplied with `extract_reference.py` (74 × 112, 32 colours) and is animated by `base_model.py`. The
same rights note applies: confirm it's OK to use before sharing the game publicly.

`Robot.png` is the **current** sprite source for the crew and NPCs: a clean 51 × 62, 15-colour knight extracted
pixel-for-pixel from the user's 18×-upscaled image. `robot_model.py` turns it into robots (glowing eyes, collar and
chest lights, an ear module, mech joints), recolours the hair, eyes and cape per character, and animates it by
layers at native 64 px. `Base.png` (the mech-arm girl) is no longer used in game. The same rights note applies.

`Girl_walk.png` was the previous sprite source for the crew and NPCs: the user's 6-frame front-view walk cycle,
extracted pixel-for-pixel from a 6×-upscaled image (17 colours, about 20 × 61 px per frame, in 48 px cells).
`girl_model.py` recolours it (hair, glowing eyes for the crew, outfits for NPCs) and builds every animation at
native 64 px. It is no longer used in game. The same rights note applies.

`Hero.png` is the **current** sprite source for the crew and NPCs: a clean 29 × 47, 16-colour front-view girl (long
navy hair with a ponytail, white crop top, gold belt, grey cargo pants, magenta gloves and boots), extracted
pixel-for-pixel from the user's 18×-upscaled image (pink border and white background removed). `hero_model.py`
recolours it (hair, glowing eyes for the crew, accent, NPC outfits) and animates it by layers at native 64 px.
`Girl_walk.png`, `Robot.png` and `Base.png` are no longer used in game. The same rights note applies.

`sample/` is the user's own animation of MC: Aseprite sources (`idle.aseprite`, `walk.aseprite`) and their exported sheets. `sample_model.py` turns them into MC's in-game sprites. This is the user's own art, so no third-party rights note applies.

`FreeKnight/` holds "Fantasy Knight - Free Pixelart Animated Character" by aamatniekss (https://aamatniekss.itch.io/fantasy-knight-free-pixelart-animated-character): the 120x80 PNG sheets of both colourways and the promo images. `knight_model.py` reskins the Colour2 / Outline sheets as MC's robot. **Licence:** free and commercial use and modification are allowed, and credit is appreciated (it is credited in `Art/Characters/MC/CREDITS.md`). The art may not be redistributed or resold on its own; the author has said a public repo is fine with credit and a link. The page also says the assets "can't be used in AI creations". The user reviewed that clause and chose to go ahead with the code-driven reskin.
