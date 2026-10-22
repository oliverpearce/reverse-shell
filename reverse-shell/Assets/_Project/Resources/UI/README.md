# UI atlas

`CardIcons.png` is our own art, painted in code by `Tools/IconForge/card_icons.py` (no third-party assets). Re-run the script
to rebuild it, and add `--preview out.png` for a version on a dark background.

It is a 6 × 6 grid of 128 px cells, read at runtime through `Combat.View.CardIcons` (keep the two layouts in sync):

| Row | Contents (columns: Attack, Debuff, Buff, Counter, Heal, Ultimate) |
|---|---|
| 0 | White glyphs, for tinting |
| 1 | Glyphs in their own colors |
| 2–4 | Mini intent cards: rank I cream, II silver, III gold (the Ultimate card is a dark arch card in all three) |
| 5 | Empty unit-type diamond, Ult pip empty, Ult pip full, star, filled diamond, card back |

`PixelArtImporter` imports everything in `Resources/UI/` trilinear, mipmapped and uncompressed, so it scales cleanly.
