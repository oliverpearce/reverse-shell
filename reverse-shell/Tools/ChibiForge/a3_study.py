"""
The study model's idle loop: the A2 study (a3_portrait.cut(native())) breathing, blinking and with its hair in a light
wind. The Esc menu's party page (the "portrait" clip, full size) and the overworld idle (shrunk to 75%) both use it,
and the walks (a3_walks) rig its frames, so the hair keeps moving while she walks.

    py -3.9 Tools/ChibiForge/a3_study.py --preview out.png      (every frame, zoomed, for checking)

Every move is a whole pixel, no resampling:
  hair   loose hair (STUDY_HAIR colours below HAIR_FROM) drifts sideways with the wind, more toward the tips
         (strands lag the ones above them), between 0 and +AMP px: blown back, then settling.
  shrug  once a loop the head, shoulders and folded arms (rows above WAIST) rise 1 px for SHRUG frames.
  blink  once a loop the eyes close for BLINK frames: the near eye's lash drops onto the iris, the far eye's top goes.
Needs Pillow (py -3.9).
"""
import math

from PIL import Image

import a3_portrait

FRAMES = 16          # 2 s at 8 fps; 16 x 128 px = 2048, the textures' import limit (wider sheets get shrunk and slice wrong)
FPS = 8
HAIR_FROM = 30       # hair rows above this (the crown, the bangs) stay still
WAIST = 42           # rows above this rise in the shrug
SHRUG = (5, 6)       # frames the shoulders are up
BLINK = (12, 13)     # frames the eyes are shut
NEAR_EYE = [(26, 28), (27, 28), (28, 28), (27, 29), (28, 29)]   # grey edge, iris (dark, light) and its highlight
NEAR_LASH_Y = 27
FAR_EYE = [(22, 28)]                                           # the far eye's top (its bottom stays as the closed line
LID = (209, 133, 126)      # skin shade (the closed lid)
LID_LIGHT = (247, 195, 167)
LASH = (0, 0, 4)

STUDY_IRIS = [(74, 103, 133), (121, 151, 164)]   # dark, light: a palette's eye ramp replaces these (a3_portrait.dress)


def _amp(y):
    """How far the hair at row y may drift: nothing at the crown, 1 px down the strands, 2 px for the long tail."""
    if y < HAIR_FROM:
        return 0.0
    if y < 40:
        return (y - HAIR_FROM) / 10.0
    return 1.0 + min(1.0, (y - 40) / 14.0)


def _wind(y, t):
    """Whole-pixel drift of hair at row y in frame t: blown toward +x (behind her), then settling, lagging downward."""
    phase = 2 * math.pi * t / FRAMES - y * 0.16
    gust = 0.5 + 0.5 * math.sin(phase) + 0.18 * math.sin(2 * phase + 1.3)
    return int(round(_amp(y) * max(0.0, min(1.0, gust))))


def frame(t, base=None):
    """Idle frame t (0..FRAMES-1) of the study, same size as the study."""
    im = (base or a3_portrait.cut(a3_portrait.native())).copy()
    w, h = im.size
    src = im.load()
    hair = set(a3_portrait.STUDY_HAIR)
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    q = out.load()

    def edge(x, y):
        return any(not (0 <= x + dx < w and 0 <= y + dy < h) or src[x + dx, y + dy][3] == 0
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))

    # 1) everything except the loose hair on its open edges (hair over her body stays put underneath)
    moving = []
    for y in range(h):
        for x in range(w):
            c = src[x, y]
            if c[3] == 0:
                continue
            if c[:3] in hair and _wind(y, t) != 0 and _amp(y) > 0:
                moving.append((x, y, c))
                if edge(x, y):
                    continue
            q[x, y] = c
    # 2) the loose hair, drifted
    for x, y, c in moving:
        nx = x + _wind(y, t)
        if 0 <= nx < w:
            q[nx, y] = c
    # 3) the shrug: rows above the waist up a pixel (the waist row stretches to fill)
    if t in SHRUG:
        lifted = out.copy()
        lp = lifted.load()
        for y in range(1, WAIST + 1):
            for x in range(w):
                lp[x, y - 1] = q[x, y]
        out = lifted
        q = out.load()
    # 4) the blink
    if t in BLINK:
        dy = -1 if t in SHRUG else 0
        for (x, y) in NEAR_EYE:
            q[x, y + dy] = LID_LIGHT + (255,)
        for x in (26, 27, 28):
            q[x, 28 + dy] = LASH + (255,)
        for x in (27, 28):
            q[x, NEAR_LASH_Y + dy] = LID + (255,)
        for (x, y) in FAR_EYE:
            q[x, y + dy] = LID + (255,)
    return out


def frames():
    base = a3_portrait.cut(a3_portrait.native())
    return [frame(t, base) for t in range(FRAMES)]


if __name__ == '__main__':
    import os
    import sys
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    out_path = sys.argv[sys.argv.index('--preview') + 1]
    fs = frames()
    sc = 6
    sheet = Image.new('RGBA', (48 * sc * 8, 60 * sc * 2), (60, 60, 70, 255))
    for i, f in enumerate(fs):
        cell = f.crop((8, 14, 56, 74)).resize((48 * sc, 60 * sc), Image.NEAREST)
        sheet.alpha_composite(cell, ((i % 8) * 48 * sc, (i // 8) * 60 * sc))
    sheet.save(out_path)
    print('preview:', out_path)
