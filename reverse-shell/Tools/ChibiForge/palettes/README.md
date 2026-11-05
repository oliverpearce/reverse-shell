# Character palettes

Every party member's colours come from a [Lospec](https://lospec.com/palette-list) palette, chosen in the palette
editor (`design/palette_editor.bat`, `Tools/PaletteEditor/`). `palettes.json` has two parts: `characters` (each one's
Lospec sources, hair / eyes / outfit ramps dark → light, accent, card swirl and glow, home area) and `areas` (each
area's world palette and dither; the game reads them from `Resources/Palettes/WorldPalettes.json`). The `.hex` files are
the same colours, one per line, so they load straight into Aseprite or Lospec's tools.

`Tools/ChibiForge/alt_recolour.py` snaps every recoloured hair, outfit and trim pixel to these colours, and
`a3_portrait.py` (card and menu art) and `card_forge.py` (card colours) read them too. Change a colour here, then
rebuild (`py -3.9 Tools/ChibiForge/a3_model.py`, then `python Tools/CardForge/card_forge.py`).

| Character | Area | Lospec palette(s) | Accent |
|---|---|---|---|
| A3 | Ruined City | [AAP-64](https://lospec.com/palette-list/aap-64) by Adigun A. Polack | `#DAE0EA` silver |
| Erin | Ruined City | [Rosy 42](https://lospec.com/palette-list/rosy-42) | `#FF8933` ember |
| Lotus | Corrupted Conservatory | [Endesga 64](https://lospec.com/palette-list/endesga-64) by ENDESGA | `#99E65F` green |
| Ashe | Frozen Region | [Cornsole2](https://lospec.com/palette-list/cornsole2) (cornflower blue, exactly `#6495ED`) + [Taffy 16](https://lospec.com/palette-list/taffy-16) (the navies and ice) | `#6495ED` cornflower |
| Zuko | Cindercore | [Fantasy 24](https://lospec.com/palette-list/fantasy-24) | `#EF3A0C` vermillion |

Ashe's cornflower is the exact web colour cornflower blue (`#6495ED`, RGB 100, 149, 237). It is the only Lospec
palette colour found that matches it exactly; the closest in the larger palettes (Taffy 16's `#448DE7`) is visibly
different, so Cornsole2 supplies it.
