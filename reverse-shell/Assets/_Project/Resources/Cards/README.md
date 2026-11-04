# Cards (pixel art)

Painted by `python Tools/CardForge/card_forge.py [--preview out.png]` (our own art; the Ult cards use the user's knight
reference sprite, recoloured per member by `Tools/ChibiForge`). Loaded at runtime by `CardArt` (Scripts/View/CardArt.cs).

- `CardSwirl.png`: the background in greys, tinted with the card owner's colour in game.
- `CardFrame.png`: rim, pip strip and name plate (never tinted).
- `CardPips.png`: a lit and an unlit rank pip.
- `CardArt.png` + `CardArt.txt`: one 32 px centre icon per card asset name. Add a card: draw an `icon_*` function in
  card_forge.py, add it to `ICONS`, rerun.
- `Ult_<Char asset>.png`: full-art Ult cards.
