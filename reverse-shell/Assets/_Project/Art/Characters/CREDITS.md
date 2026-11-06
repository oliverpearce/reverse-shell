# Character sprites

**A3 is not original art.** Her body is an A2 pixel-art study (artist unknown), reworked; her hair is from the Last
Cloudia unit sheet for A2 (NieR:Automata collab), (c) Aidis / Square Enix. Built by `Tools/ChibiForge/a3_model.py`.
See `A3/CREDITS.md`.

Every other folder here (Ashe, RustSentry, ScrapStalker, NoodleVendor, Courier, Drifter; Erin, Lotus, Zuko and Alt_Ashe are recolours of A3 by Tools/ChibiForge/alt_recolour.py) is **original pixel
art**, drawn in code by `Tools/ChibiForge/chibi_forge.py` (no third-party assets, no license strings):

- Chibi proportions in 64×64 frames, a bold plum outline, 4-tone ramps and a limited warm palette.
- The party are androids: faces and hair, armour plates and glowing lines in their element colour. The enemies are
  round machine lifeforms, and the NPCs are ruin survivors.
- Animations: `idle` (2), `walk` (9; frame 0 is the standing pose), `slash` (6), `spellcast` (7) and `hurt` (6).
  Each has 4 rows (up, left, down, right); `hurt` has one row, ending lying on the ground.

To change a look, edit `SPECS` in `chibi_forge.py`, run `python Tools/ChibiForge/chibi_forge.py`, then run
**Tools → Combat → Generate Sample Content** again in Unity.

The old LPC composer (`Tools/SpriteForge/lpc_compose.py`) is no longer used for these characters. If it is run
again, its output must keep the LPC credits it writes (CC-BY-SA / GPL).
