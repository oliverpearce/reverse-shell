"""
A3 model: the lead's sprites. Every clip is drawn from the A2 pixel-art study's body (a3_portrait / a3_study) wearing
the old model's hair (a3_oldhair: its six sway frames, saved in reference/A3_hair.png).

    py -3.9 Tools/ChibiForge/a3_model.py [--contact out.png]

The Last Cloudia sheet is no longer used. Its hair was saved once into reference/A3_hair.png (lined up on the study),
and the battle clips that were cut from it are now poses of the study body (a3_walks.hold: the walk rig's legs held
still, the body leaning, the whole figure lifting for the knee strike). See Assets/_Project/Art/Characters/A3/CREDITS.md.

Every frame is shrunk to 75% (shrink(): each output pixel takes the colour that covers most of its 4/3 x 4/3 source
area, so no new colours appear), then sturdy() thickens the legs and adds an unbroken outline.

Output: uncoloured base sheets in reference/A3_base/, in 128 px cells, feet at FEET_ROW from the bottom, centred.
Rows: up, left, down, right (SpriteAnimationSet). She is drawn facing left, so left and down are as drawn, while
right and up are mirrored. alt_recolour.py then dresses every ally (A3 included) from them, and a3_portrait.py makes
the menus' and cards' portrait.
Needs Pillow (the Windows "py -3.9" install has it).
"""
import math
import os
import sys

from PIL import Image, ImageDraw

import a3_study
import a3_walks

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
# The uncoloured base sheets. alt_recolour.py dresses every ally from them (A3 included) into Art/Characters/<name>/.
OUT = os.path.join(HERE, 'reference', 'A3_base')

CELL = 128
FEET_ROW = 14          # feet this many px up from the bottom of the cell (Anim_A3 feetPivot = 14 / 128)
UP, LEFT, DOWN, RIGHT = 0, 1, 2, 3

IDLE = [('oldhair', k) for k in range(len(a3_walks.a3_oldhair.HAIR_FRAMES))]
STAND = ('oldhair', 0)


def _pose(legs, lean=0, head=(0, 0), lift=0, k=0):
    """A fixed pose of the study body (a3_walks.hold): legs = (thigh A, bend A, thigh B, bend B) in degrees."""
    return ('hold', k, legs, lean, head, lift)


# clip: frame entries in order (see SpriteAnimationSet for how the game uses each clip)
CLIPS = {
    # the study standing still, its hair in the 6 drawn sway frames
    'idle': IDLE,
    'battle_idle': list(IDLE),
    # two more battle idles (CharacterDefinition.battleIdle): the same stance, each breathing in its own rhythm
    # (breathe_pose: the upper body sinks over still legs)
    'battle_idle2': [('breathe', k, d) for k, d in zip([5, 4, 3, 2, 1, 0], (0, 1, 1, 1, 0, 0))],
    'battle_idle3': [('breathe', k, d) for k, d in zip([3, 4, 5, 0, 1, 2], (1, 2, 1, 0, 0, 1))],
    # Settings > Sword used the sheet's sword. The study has none, so these are the plain idle and walk (the row is
    # hidden in the menu; the clips stay so the Anim assets keep their references).
    'idle_sword': list(IDLE),
    # the unarmed attack, 8 frames: the blow is frame 4 (CastSteps LungeStep.ImpactShare = 0.5)
    'slash': [STAND,
              _pose((18, 40, -6, 36), lean=1, k=1),             # crouch
              _pose((26, 62, -10, 52), lean=2, k=2),            # deeper, ready to spring
              _pose((38, 20, -34, 24), lean=4, k=3),            # the dash: a long stride, leaning in
              _pose((62, 88, -14, 6), lean=5, lift=2, k=4),     # the blow: front knee driven up, body forward
              _pose((54, 76, -12, 6), lean=4, lift=1, k=5),     # follow-through
              _pose((16, 18, -10, 14), lean=1, k=0),            # landing
              STAND],
    # casting: a braced stance leaning into the spell, held, then back
    'spellcast': [STAND,
                  _pose((10, 4, -12, 8), lean=1, k=1),
                  _pose((16, 6, -18, 12), lean=3, head=(-1, 0), k=2),
                  _pose((16, 6, -18, 12), lean=3, head=(-1, 0), k=3),
                  _pose((16, 6, -18, 12), lean=3, head=(-1, 0), k=4),
                  _pose((10, 4, -12, 8), lean=1, k=5),
                  STAND],
    # recoil, then down on her knees (the game holds the last frame when a unit falls)
    'hurt': [_pose((-6, 10, 8, 14), lean=-2, head=(1, 0), k=0),
             _pose((-10, 24, 10, 30), lean=-3, head=(1, 1), k=1),
             _pose((14, 60, -4, 70), lean=-1, head=(1, 1), k=2),
             _pose((30, 100, 10, 110), lean=1, head=(0, 1), k=3),
             _pose((34, 110, 14, 120), lean=2, head=(0, 1), k=3)],
}
# The walks (a3_walks.py): walk_s1..walk_s3 and walk = the default style.
CLIPS.update(a3_walks.clips())
CLIPS['walk_sword'] = list(CLIPS['walk'])


def frame(entry):
    """One 128 px cell: the frame shrunk to 75%, its feet on the cell's foot point."""
    kind = entry[0]
    if kind == 'rig':
        im, fx, fy = a3_walks.pose(entry)
    elif kind == 'hold':
        _, k, legs, lean, head, lift = entry
        im, fx, fy = a3_walks.hold(k, legs, lean, head, lift)
    elif kind == 'breathe':
        im, fx, fy = a3_walks.overworld_frame(entry[1])
        im = breathe_pose(im, entry[2])
    else:   # ('oldhair', k)
        im, fx, fy = a3_walks.overworld_frame(entry[1])
    im = shrink(im)
    fx, fy = int(round(fx * 0.75)), int(round(fy * 0.75))
    cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    cell.paste(im, (CELL // 2 - fx, CELL - FEET_ROW - fy), im)
    return cell


def breathe_pose(im, dy):
    """A standing frame breathing: everything above the waist (the top 55% of the figure) sinks dy source px, the
    rows it uncovers filled from the waist row, the legs untouched."""
    if dy <= 0:
        return im
    box = im.getbbox()
    if box is None:
        return im
    waist = box[1] + int((box[3] - box[1]) * 0.55)
    out = im.copy()
    top = im.crop((0, 0, im.width, waist))
    clear = Image.new('RGBA', (im.width, waist), (0, 0, 0, 0))
    out.paste(clear, (0, 0))
    out.paste(top, (0, dy))
    # the waist row stretches over the gap, so the body stays joined
    row = im.crop((0, waist - 1, im.width, waist))
    for y in range(waist - 1, waist + dy):
        out.paste(row, (0, y), row)
    return out


def _span(i, n):
    """Source pixels (index, overlap) under output pixel i at 3/4 scale."""
    a, b = i * 4 / 3, (i + 1) * 4 / 3
    out, x = [], int(a)
    while x < b and x < n:
        w = min(b, x + 1) - max(a, x)
        if w > 1e-6:
            out.append((x, w))
        x += 1
    return out


def shrink(im):
    """75% size. An output pixel is opaque if at least half its area is; its colour is the one covering most of it."""
    w, h = im.size
    p = im.load()
    W, H = (w * 3 + 3) // 4, (h * 3 + 3) // 4
    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    q = out.load()
    for oy in range(H):
        ys = _span(oy, h)
        for ox in range(W):
            total = solid = 0.0
            weight = {}
            for x, wx in _span(ox, w):
                for y, wy in ys:
                    a = wx * wy
                    total += a
                    c = p[x, y]
                    if c[3] > 100:
                        solid += a
                        weight[c[:3]] = weight.get(c[:3], 0) + a
            if total and solid / total >= 0.5:
                q[ox, oy] = max(weight, key=weight.get) + (255,)
    return out




def _runs(p, w, y):
    out, x = [], 0
    while x < w:
        if p[x, y][3] > 100:
            s = x
            while x < w and p[x, y][3] > 100:
                x += 1
            out.append((s, x))
        else:
            x += 1
    return out




MAX_TEXTURE = 2048   # the sheets' import limit (.meta maxTextureSize): Unity shrinks anything bigger and frames slice wrong


def check_size(sheet, name):
    if max(sheet.size) > MAX_TEXTURE:
        raise ValueError('%s is %dx%d: over the %d px import limit (fewer frames, or raise maxTextureSize in its .meta)'
                         % (name, sheet.width, sheet.height, MAX_TEXTURE))


# The overworld clips (walking the world, under buildings and trees): sturdier legs and an unbroken outline, so the
# figure reads at the overworld's small size and the behind-scenery outline (Silhouette.shader) is one solid line.
# Battle, the menus' portrait and the Soul cards keep the art as drawn.
LEG_SHARE = 0.42   # the bottom share of the figure that is legs (the study: hip at row 48 of 84)


def _outline_colour(p, w, h):
    """The figure's own outline: the most common colour among its edge pixels (opaque, next to an empty one)."""
    counts = {}
    for y in range(h):
        for x in range(w):
            c = p[x, y]
            if c[3] <= 100:
                continue
            if any(not (0 <= x + dx < w and 0 <= y + dy < h) or p[x + dx, y + dy][3] <= 100
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                counts[c[:3]] = counts.get(c[:3], 0) + 1
    return max(counts, key=counts.get) if counts else (26, 14, 19)


def sturdy(cell):
    """Thicker legs (each leg run grows 1 px on its outer side, in its own edge colour), then a 1 px outline all the way
    round the figure in its outline colour."""
    out = cell.copy()
    p = out.load()
    w, h = out.size
    bbox = out.getbbox()
    if bbox is None:
        return out
    x0, y0, x1, y1 = bbox
    centre = (x0 + x1) / 2.0
    legs_from = y1 - int(round((y1 - y0) * LEG_SHARE))
    src = cell.load()
    for y in range(legs_from, y1):
        runs = _runs(src, w, y)
        for s, e in runs:
            mid = (s + e) / 2.0
            grow_left = mid <= centre or len(runs) == 1
            grow_right = mid >= centre or len(runs) == 1
            if grow_left and s - 1 >= 0 and src[s - 1, y][3] <= 100:
                p[s - 1, y] = src[s, y]
            if grow_right and e < w and src[e, y][3] <= 100:
                p[e, y] = src[e - 1, y]
    ink = _outline_colour(p, w, h) + (255,)
    solid = [[p[x, y][3] > 100 for x in range(w)] for y in range(h)]
    for y in range(h):
        for x in range(w):
            if solid[y][x]:
                continue
            if any(0 <= x + dx < w and 0 <= y + dy < h and solid[y + dy][x + dx]
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                p[x, y] = ink
    return out

def build_clip(name):
    cells = [sturdy(frame(e)) for e in CLIPS[name]]   # every clip: sturdier legs and an unbroken outline
    sheet = Image.new('RGBA', (CELL * len(cells), CELL * 4), (0, 0, 0, 0))
    for c, cell in enumerate(cells):
        flipped = cell.transpose(Image.FLIP_LEFT_RIGHT)
        for r in (UP, LEFT, DOWN, RIGHT):
            sheet.paste(cell if r in (LEFT, DOWN) else flipped, (c * CELL, r * CELL))
    return sheet


def contact(path, scale=2):
    """Every clip's left-facing row, one clip per line, for checking alignment (a line marks the feet)."""
    longest = max(len(v) for v in CLIPS.values())
    w, h = CELL * scale, CELL * scale
    out = Image.new('RGBA', (w * longest, h * len(CLIPS)), (60, 60, 70, 255))
    d = ImageDraw.Draw(out)
    for r, name in enumerate(CLIPS):
        for c, e in enumerate(CLIPS[name]):
            cell = sturdy(frame(e)).resize((w, h), Image.NEAREST)
            out.alpha_composite(cell, (c * w, r * h))
            d.text((c * w + 3, r * h + 2), '%s %d' % (name, c), fill=(255, 255, 0, 255))
        y = r * h + (CELL - FEET_ROW) * scale
        d.line([0, y, out.width, y], fill=(255, 80, 80, 120))
    d.line([w // 2, 0, w // 2, out.height], fill=(80, 200, 255, 90))
    out.save(path)


def main(argv):
    if '--contact' in argv:
        contact(argv[argv.index('--contact') + 1])
        return
    os.makedirs(OUT, exist_ok=True)
    for name in CLIPS:
        sheet = build_clip(name)
        check_size(sheet, name)
        sheet.save(os.path.join(OUT, name + '.png'))
        print('%-12s %2d frames  %dx%d' % (name, len(CLIPS[name]), sheet.width, sheet.height))
    sys.path.insert(0, HERE)
    import alt_recolour   # every ally's sheets (A3 included) from these
    alt_recolour.main()
    import a3_portrait    # the menus' and cards' portrait (A3 only)
    a3_portrait.build()


if __name__ == '__main__':
    main(sys.argv[1:])
