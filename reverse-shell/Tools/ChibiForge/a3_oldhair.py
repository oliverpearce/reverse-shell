"""
The overworld model: the A2 study's body (its walk, a3_walks) wearing the old sheet model's hair, which the user loves.

    py -3.9 Tools/ChibiForge/a3_oldhair.py --preview out.png

wear(study_frame, sheet_frame) lifts the study's own hair off (a3_portrait.STUDY_HAIR), patches the body pixels that
hair covered (from the nearest body pixel in the same row, only where the body is on both sides), then lays the sheet
frame's hair (its yellow-grey ramp, alt_recolour.is_hairish) on top, lined up by the crown and the face's middle, with a
1 px outline where it meets the background. The sheet idle frames (HAIR_FRAMES) each have their own drawn hair sway,
so the overworld cycles through them: standing still uses the first, each walk step takes the next one.
alt_recolour then dresses it like the old model (the yellow-grey hair ramp -> each ally's hair).
Needs Pillow (py -3.9).
"""
import colorsys

from PIL import Image

import a3_portrait

HAIR_FRAMES = [16, 95, 82, 64, 79, 63]   # the sheet's idle frames (arms folded, the hair swaying), in loop order
STUDY_CROWN = 18                          # the study's first hair row
SHEET_HEAD_ROWS = 13                      # the sheet's head (for lining the faces up)
STUDY_HEAD = (25, 34)                     # the study's face rows
OUTLINE = (17, 0, 0)                      # the sheet's outline colour


def is_sheet_hair(c):
    h, s, v = colorsys.rgb_to_hsv(c[0] / 255.0, c[1] / 255.0, c[2] / 255.0)
    return abs(h * 360 - 60) < 2 and s > 0 and v > 0.1


def _face_mid(p, w, rows, is_hair):
    xs = [x for y in range(*rows) for x in range(w) if p[x, y][3] > 100 and not is_hair(p[x, y][:3])
          and p[x, y][0] > 150 and p[x, y][0] > p[x, y][2] + 30]   # skin: warm and light
    return sum(xs) / float(len(xs)) if xs else w / 2.0


def wear(study, sheet):
    """The study frame (64 x 84, a3_portrait coordinates) with the sheet frame's hair."""
    out = study.copy()
    q = out.load()
    w, h = out.size
    study_hair = set(a3_portrait.STUDY_HAIR)
    p = sheet.load()
    sw, sh = sheet.size
    # line up: the sheet's crown on the study's, the sheet's face middle on the study's
    dy = STUDY_CROWN - min(y for y in range(sh) for x in range(sw) if p[x, y][3] > 100 and is_sheet_hair(p[x, y][:3]))
    dx = int(round(_face_mid(q, w, STUDY_HEAD, lambda c: c in study_hair)
                   - _face_mid(p, sw, (0, SHEET_HEAD_ROWS), is_sheet_hair)))
    # 1) lift the study's hair off; patch body pixels it covered (body on both sides in the row), else clear
    holes = [(x, y) for y in range(h) for x in range(w) if q[x, y][3] and q[x, y][:3] in study_hair]
    for x, y in holes:
        q[x, y] = (0, 0, 0, 0)
    for x, y in holes:
        left = next((q[i, y] for i in range(x - 1, -1, -1) if q[i, y][3] and (i, y) not in holes), None)
        right = next((q[i, y] for i in range(x + 1, w) if q[i, y][3] and (i, y) not in holes), None)
        if left and right:
            q[x, y] = left
    # 2) the sheet's hair, with an outline where it meets the background
    hair = {}
    for y in range(sh):
        for x in range(sw):
            c = p[x, y]
            if c[3] > 100 and is_sheet_hair(c[:3]):
                X, Y = x + dx, y + dy
                if 0 <= X < w and 0 <= Y < h:
                    hair[(X, Y)] = c
    for (X, Y), c in hair.items():
        q[X, Y] = c
    # 3) the sheet's own dark strand lines and outline (dark pixels touching its hair), only into empty spots, so they
    #    never cover the study's face
    for y in range(sh):
        for x in range(sw):
            c = p[x, y]
            if c[3] <= 100 or is_sheet_hair(c[:3]) or sum(c[:3]) > 200:
                continue
            if not any(0 <= x + ax < sw and 0 <= y + ay < sh and p[x + ax, y + ay][3] > 100 and is_sheet_hair(p[x + ax, y + ay][:3])
                       for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                continue
            X, Y = x + dx, y + dy
            if 0 <= X < w and 0 <= Y < h and q[X, Y][3] == 0:
                q[X, Y] = c
    # 3b) where the study's hair was lifted off and the old hair doesn't reach, but there's body or hair on both sides of
    #     the row: hair seen behind her (the old hair's darkest shade), not a see-through gap
    dark = min((c for c in hair.values()), key=lambda c: sum(c[:3])) if hair else OUTLINE + (255,)
    for x, y in holes:
        if q[x, y][3]:
            continue
        left = any(q[i, y][3] for i in range(max(0, x - 6), x))
        right = any(q[i, y][3] for i in range(x + 1, min(w, x + 7)))
        if left and right:
            q[x, y] = dark
    # 4) gaps left fully inside the hair (empty, but not reachable from the background): the darkest hair beside them
    outside, stack = set(), [(x, y) for x in range(w) for y in (0, h - 1)] + [(x, y) for y in range(h) for x in (0, w - 1)]
    while stack:
        x, y = stack.pop()
        if (x, y) in outside or not (0 <= x < w and 0 <= y < h) or q[x, y][3] != 0:
            continue
        outside.add((x, y))
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    for y in range(h):
        for x in range(w):
            if q[x, y][3] == 0 and (x, y) not in outside:
                near = [q[x + ax, y + ay] for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1))
                        if 0 <= x + ax < w and 0 <= y + ay < h and q[x + ax, y + ay][3]]
                if near:
                    q[x, y] = min(near, key=lambda c: sum(c[:3]))
    # 5) above the face, a lone study pixel showing through a gap in the bangs (3+ sides old hair) becomes hair
    for y in range(STUDY_HEAD[0]):
        for x in range(w):
            if (x, y) in hair or q[x, y][3] == 0:
                continue
            around = [hair[(x + ax, y + ay)] for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1)) if (x + ax, y + ay) in hair]
            if len(around) >= 3:
                q[x, y] = around[0]
                hair[(x, y)] = around[0]
    # 6) an outline where the hair still meets the background directly
    for (X, Y) in hair:
        for nx, ny in ((X + 1, Y), (X - 1, Y), (X, Y + 1), (X, Y - 1)):
            if 0 <= nx < w and 0 <= ny < h and q[nx, ny][3] == 0:
                q[nx, ny] = OUTLINE + (255,)
    return out


if __name__ == '__main__':
    import os
    import sys
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import a3_model as m
    atlas = Image.open(m.SHEET).convert('RGBA')
    base = a3_portrait.cut(a3_portrait.native())
    out_path = sys.argv[sys.argv.index('--preview') + 1]
    sc = 6
    sheet = Image.new('RGBA', (64 * sc * 8, 84 * sc), (60, 60, 70, 255))
    old, _, _ = m.upright(atlas, 16)
    sheet.alpha_composite(old.resize((old.width * sc, old.height * sc), Image.NEAREST), (0, (84 - 52 - 13) * sc))
    sheet.alpha_composite(base.resize((64 * sc, 84 * sc), Image.NEAREST), (64 * sc, 0))
    for i, fid in enumerate(HAIR_FRAMES):
        sf, _, _ = m.upright(atlas, fid)
        sheet.alpha_composite(wear(base, sf).resize((64 * sc, 84 * sc), Image.NEAREST), ((i + 2) * 64 * sc, 0))
    sheet.save(out_path)
    print('preview:', out_path)
