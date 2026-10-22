# Fonts

All fonts are under the **SIL Open Font License 1.1**: M PLUS 1p and Anton from Google Fonts (https://github.com/google/fonts),
Cormorant Garamond from its upstream project (https://github.com/CatharsisFonts/Cormorant; also on Google Fonts).
The full license texts are next to the font files (`OFL-MPLUS1p.txt`, `OFL-Anton.txt`, `OFL-CormorantGaramond.txt`). They must ship with the fonts.

| File | Font | Authors | Used for |
|---|---|---|---|
| `MPLUS1p-Light.ttf`, `-Medium.ttf`, `-Bold.ttf` | M PLUS 1p | The M+ Project Authors | All UI text: Light by default, Medium for bold text, Bold for emphasized numbers. Includes Japanese glyphs. |
| `Anton-Regular.ttf` | Anton | The Anton Project Authors (Vernon Adams) | Dialogue speaker tags |
| `CormorantGaramond-SemiBold.ttf` | Cormorant Garamond | The Cormorant Project Authors (Christian Thalmann) | Serif headings in the Reverse: 1999-style UI (`UIFactory.SerifFont`) |

They live in `Resources/` so code can load them with `Resources.Load<Font>("Fonts/...")` (see `UIFactory`).
