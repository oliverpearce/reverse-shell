# A3: sprite credits

These sprites are A2 from *NieR:Automata*, cut from her unit sprite sheet in **Last Cloudia** (the NieR:Automata
collab). The art belongs to **Aidis Inc. / Square Enix**. It is not licensed for this project: it is used in a
non-commercial fan game only, and the rights holders' names are kept here.

`Tools/ChibiForge/a3_model.py` cuts the frames from `Tools/ChibiForge/reference/A3_sheet.png`. It turns rotated
frames upright, lines up the feet, shrinks them to 75% (to the crew's height) and lays them out in 128 px cells with
4 facing rows. The walk is made by re-posing the legs of her dash frames, because the sheet has no walk cycle; the sword walk adds
the blade from one of her idle frames.
