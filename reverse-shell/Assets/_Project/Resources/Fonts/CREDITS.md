# Fonts

Both fonts are from Google Fonts (https://github.com/google/fonts) under the **SIL Open Font License 1.1**.
The full license texts are next to the font files (`OFL-MPLUS1p.txt`, `OFL-Anton.txt`). They must ship with the fonts.

| File | Font | Authors | Used for |
|---|---|---|---|
| `MPLUS1p-Light.ttf`, `-Medium.ttf`, `-Bold.ttf` | M PLUS 1p | The M+ Project Authors | All UI text: Light by default, Medium for bold text, Bold for emphasized numbers. Includes Japanese glyphs. |
| `Anton-Regular.ttf` | Anton | The Anton Project Authors (Vernon Adams) | Dialogue speaker tags |

They live in `Resources/` so code can load them with `Resources.Load<Font>("Fonts/...")` (see `UIFactory`).
