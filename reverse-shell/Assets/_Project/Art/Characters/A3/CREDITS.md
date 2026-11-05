# A3: sprite credits

These sprites are A2 from *NieR:Automata*, cut from her unit sprite sheet in **Last Cloudia** (the NieR:Automata
collab). The art belongs to **Aidis Inc. / Square Enix**. It is not licensed for this project: it is used in a
non-commercial fan game only, and the rights holders' names are kept here.

`Tools/ChibiForge/a3_model.py` cuts the frames from `Tools/ChibiForge/reference/A3_sheet.png`. It turns rotated
frames upright, lines up the feet, shrinks them to 75% (to the crew's height) and lays them out in 128 px cells with
4 facing rows. In the overworld (idle and walks) her legs are the A2 study's (`a3_hybrid.py`, below) under the sheet's
head and torso. The sheet has no walk cycle, so `Tools/ChibiForge/a3_walks.py` rigs three (Settings > Walk):

- **Reference** is keyed from the *8-frame pixel art walk cycle template* by MostlyDecentGames
  (https://mostlydecent.itch.io/8-frame-walk, CC0). Only its timing and poses were measured; none of its pixels are used.
- **Gandalf** is inspired by the walk in the *GandalfHardcore Character Asset Pack* (https://gandalfhardcore.itch.io).
  Its licence forbids redistributing the art, so only the timing was studied by eye; none of its pixels are used or
  kept in this repository.

Party colours come from Lospec palettes: see `Tools/ChibiForge/palettes/README.md`.

`portrait.png` (the Esc menu's party page, Soul cards and Ult pictures) is an A2 pixel-art study
(`Tools/ChibiForge/reference/A3_study.gif`) with its sword removed by `Tools/ChibiForge/a3_portrait.py`. Artist: TODO
(add the study's creator here before publishing).
