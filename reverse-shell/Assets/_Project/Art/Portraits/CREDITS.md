# Character card portraits

Original pixel art drawn in code by `Tools/ChibiForge/chibi_forge.py`, the same rig as the sprites at 2.4× detail
in a hero pose (no third-party assets). The older `Tools/PortraitForge` is retired.

| File | Used for |
|---|---|
| `<Name>_front.png` | Card front in the crew menu (96×144, shown at 4×) and the portrait on the opened card |
| `<Name>_internals.png` | The opened card: the chest plates are off, showing ribs, wires, gears and the glowing core |
| `<Name>_front_glow.png`, `<Name>_internals_glow.png` | White masks of the glowing pixels (plus a soft halo), tinted at runtime with `AfflatusInfo.GlowFor` |

The crew menu pins the CHIP socket on the head and the CORE socket on the chest (`CharacterMenu.SocketSpots`,
which matches `chibi_forge.portrait_anchor`).
