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

`Girl_walk.png` is the **current** sprite source for the crew and NPCs: the user's 6-frame front-view walk cycle,
extracted pixel-for-pixel from a 6×-upscaled image (17 colours, about 20 × 61 px per frame, in 48 px cells).
`girl_model.py` recolours it (hair, glowing eyes for the crew, outfits for NPCs) and builds every animation at
native 64 px. `Robot.png` and `Base.png` are no longer used in game. The same rights note applies.
