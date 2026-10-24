# Fonts

| File | Font | Author | License | Used for |
|---|---|---|---|---|
| `monogram-extended.ttf` | monogram (extended, v1.1.0) | datagoblin (https://datagoblin.itch.io/monogram) | CC0 (public domain; credit appreciated) | **All UI text** (`UIFactory.PixelFont`). A 16 px pixel font: imported as Hinted Raster, point-filtered at runtime, sizes scaled by `UIFactory.FontScale`. |

No longer used (kept for now; safe to delete together with their license files):

| File | Font | Authors | License |
|---|---|---|---|
| `MPLUS1p-Light.ttf`, `-Medium.ttf`, `-Bold.ttf` | M PLUS 1p | The M+ Project Authors | SIL OFL 1.1 (`OFL-MPLUS1p.txt`) |
| `Anton-Regular.ttf` | Anton | The Anton Project Authors (Vernon Adams) | SIL OFL 1.1 (`OFL-Anton.txt`) |
| `CormorantGaramond-SemiBold.ttf` | Cormorant Garamond | The Cormorant Project Authors (Christian Thalmann) | SIL OFL 1.1 (`OFL-CormorantGaramond.txt`) |

Fonts live in `Resources/` so code can load them with `Resources.Load<Font>("Fonts/...")` (see `UIFactory`).
