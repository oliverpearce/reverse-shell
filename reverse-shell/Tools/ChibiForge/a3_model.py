"""
A3 model: the lead's sprites, cut from a packed sprite sheet (reference/A3_sheet.png, 2048 x 1155, transparent).

    py -3.9 Tools/ChibiForge/a3_model.py [--contact out.png]

Source: "Last Cloudia" unit sheet for A2 (NieR:Automata collab), (c) Aidis / Square Enix. See
Assets/_Project/Art/Characters/A3/CREDITS.md.

The sheet is a texture-packer atlas with no frame data: frames are trimmed, some are stored rotated 90 degrees
(those are turned back clockwise), and some entries are loose parts (a sword, legs, a head) that the original game
puts together at runtime. Only whole-body frames are used. FRAMES lists each one by its box in the atlas, plus its
foot point (the middle between the feet, in the upright frame). The foot points were read by eye for the key poses
and carried to the in-between frames by pixel matching; nudge one here if a frame jitters.

Every frame is shrunk to 75% (shrink(): each output pixel takes the colour that covers most of its 4/3 x 4/3 source
area, so no new colours appear), which puts her at the robots' height (~36 px) with the knight's pixel size.

Output: Assets/_Project/Art/Characters/A3/<clip>.png, in 128 px cells (the sword swings need the room), feet at
FEET_ROW from the bottom, centred. Anim_A3's referenceSize = 64 keeps it at the crew's pixel density in game.
Rows: up, left, down, right (SpriteAnimationSet). A2 is drawn facing left, so left and down are as drawn, while right
and up are mirrored. Every clip has all 4 rows (a one-row sheet would face left even for the party).

No sword in battle: the attack (slash) is unarmed, built from her sword-less poses (crouch, dash, a raised strike with a
knee), and spellcast is an arm thrust. The sword only appears with Settings > Sword in the overworld (idle_sword,
walk_sword). ERASE takes out the hilt left in a few frames.
The sheet has no walk cycle, so the walk is a stroll of her idle frames (stroll_pose()): the legs below the hip split
down the middle, each sheared about the hip in whole pixels so the feet swing forward and back in turn, the swinging
foot lifts, and the body bobs 1 px. She is the same sprite and height standing and walking, and her hair keeps swaying.
walk_sword is the same stroll of idle_sword (sword resting): the overworld uses them when Settings > Sword is on.
Output: uncoloured base sheets in reference/A3_base/; alt_recolour.py then dresses every ally (A3 included) from them.
Needs Pillow (the Windows "py -3.9" install has it).
"""
import math
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
SHEET = os.path.join(HERE, 'reference', 'A3_sheet.png')
# The uncoloured base sheets. alt_recolour.py dresses every ally from them (A3 included) into Art/Characters/<name>/.
OUT = os.path.join(HERE, 'reference', 'A3_base')

CELL = 128
FEET_ROW = 14          # feet this many px up from the bottom of the cell (Anim_A3 feetPivot = 14 / 128)
UP, LEFT, DOWN, RIGHT = 0, 1, 2, 3

# id: (atlas box x0, y0, x1, y1), stored rotated?, foot x, foot y (in the upright frame)
FRAMES = {
    3: ((143, 3, 191, 39), True, 13, 47),
    6: ((92, 41, 140, 113), True, 17, 47),
    7: ((142, 41, 190, 113), True, 17, 47),
    8: ((192, 41, 240, 113), True, 17, 47),
    15: ((89, 169, 161, 217), False, 17, 47),
    16: ((163, 169, 195, 221), False, 11, 51),
    17: ((22, 219, 81, 285), True, 54, 46),
    19: ((145, 223, 192, 275), True, 22, 46),
    23: ((22, 287, 84, 358), False, 42, 70),
    27: ((138, 311, 190, 358), False, 22, 46),
    31: ((87, 345, 136, 413), True, 15, 48),
    33: ((18, 360, 85, 413), False, 56, 43),
    34: ((138, 360, 190, 407), False, 22, 46),
    36: ((144, 409, 196, 456), False, 22, 46),
    45: ((144, 458, 196, 505), False, 22, 46),
    47: ((224, 460, 265, 503), False, 13, 42),
    48: ((21, 488, 89, 540), False, 15, 51),
    57: ((103, 548, 151, 620), True, 17, 47),
    58: ((153, 557, 201, 629), True, 17, 47),
    59: ((203, 557, 251, 629), True, 17, 47),
    63: ((349, 577, 380, 629), False, 13, 51),
    64: ((431, 577, 460, 629), False, 12, 51),
    65: ((382, 579, 429, 629), True, 20, 46),
    70: ((149, 631, 217, 680), False, 56, 39),
    71: ((219, 631, 270, 685), True, 46, 41),
    74: ((369, 631, 416, 663), True, 13, 47),
    75: ((418, 631, 457, 672), False, 13, 40),
    77: ((459, 633, 502, 672), True, 13, 42),
    80: ((357, 665, 406, 697), True, 10, 47),
    83: ((442, 674, 484, 714), True, 13, 42),
    84: ((96, 680, 141, 737), True, 22, 43),
    86: ((143, 682, 211, 716), False, 40, 32),
    90: ((442, 716, 492, 763), False, 20, 46),
    92: ((275, 717, 315, 758), True, 14, 39),
    94: ((153, 718, 221, 767), False, 53, 41),
    96: ((357, 733, 407, 780), False, 20, 46),
    99: ((442, 765, 492, 812), False, 20, 46),
    105: ((607, 801, 644, 850), False, 14, 48),
    109: ((949, 805, 1001, 852), False, 22, 46),
    121: ((279, 835, 340, 912), False, 39, 76),
    127: ((949, 854, 1021, 901), False, 17, 47),
    134: ((646, 900, 722, 929), False, 40, 28),
    148: ((976, 950, 1022, 1023), True, 18, 47),
    # sword-less: standing with arms folded, and a crouch
    79: ((327, 663, 355, 715), False, 13, 51),
    82: ((409, 674, 440, 726), False, 11, 51),
    87: ((281, 687, 325, 715), True, 16, 43),
    95: ((409, 728, 440, 780), False, 10, 51),
    # the unarmed strike: arm raised with a knee up, and the knee's follow-through (standing on one foot)
    125: ((607, 852, 644, 905), False, 13, 52),
    66: ((462, 580, 485, 631), False, 12, 50),
}

# clip: frame ids in order (see SpriteAnimationSet for how the game uses each clip)
# Hilt pixels left in the sword-less frames: (x0, y0, x1, y1) boxes in the upright frame; only dark pixels go, so hair
# crossing a box stays.
ERASE = {
    3: [(0, 0, 8, 8)],      # the dash frames (the unarmed attack's lunge): the pommel above her head
    74: [(0, 0, 8, 8)],
    80: [(0, 0, 7, 6)],
    105: [(0, 0, 8, 8)],
    87: [(24, 15, 29, 25)],
}


# A stroll entry is ('stroll', standing frame, step 0..STROLL_STEPS-1): see stroll_pose(). The walk is a stroll of the
# idle frames, so she is the same sprite (same height, same body) standing and walking.
STROLL_STEPS = 8
STROLL_AMP = 3      # how far each foot swings forward / back, source px
STROLL_LIFT = 2     # how high the swinging foot rises, source px


def _stroll(frames):
    return [('stroll', frames[k % len(frames)], k) for k in range(STROLL_STEPS)]

CLIPS = {
    # overworld and menus: no sword
    'idle': [16, 95, 82, 64, 79, 63],                       # arms folded, hair swaying (ordered for the smoothest loop)
    'walk': _stroll([16, 95, 82, 64, 79, 63, 16, 95]),       # the idle body (hair still swaying), legs stepping
    'walk_sword': _stroll([6, 7, 8, 57, 58, 59, 127, 148]),  # sword resting, legs stepping (Settings > Sword)
    'idle_sword': [6, 7, 8, 57, 58, 59, 127, 148],          # sword resting on the ground (Settings > Sword)
    'sit': [16, 87],                                        # drops into a crouch (held)
    'read': [16, 63, 64],                                   # arms folded (held)
    # battle: no sword (battle uses idle for its stance)
    'slash': [16, 87, 3, 74, 125, 66, 105, 16],             # unarmed: crouch, dash in, overhead strike + knee, recover
    'spellcast': [16, 47, 92, 83, 92, 47, 16],              # arm thrust out, held, back
    'hurt': [75, 77, 83, 92, 47],                           # recoil
}


def upright(atlas, fid):
    box, rotated, fx, fy = FRAMES[fid]
    im = atlas.crop(box)
    if rotated:
        im = im.transpose(Image.ROTATE_270)
    if fid in ERASE:
        p = im.load()
        for x0, y0, x1, y1 in ERASE[fid]:
            for y in range(y0, min(y1, im.height)):
                for x in range(x0, min(x1, im.width)):
                    r, g, b, a = p[x, y]
                    if a and 0.299 * r + 0.587 * g + 0.114 * b < 120:
                        p[x, y] = (0, 0, 0, 0)
    return im, fx, fy






def stroll_pose(im, step):
    """A standing frame taking a step: the legs below the hip split down the middle of each row (or at the gap between
    them), each sheared about the hip so its foot swings STROLL_AMP forward and back, opposite to the other one; the
    foot moving forward rises STROLL_LIFT at mid-swing, and the body rises 1 px as the feet pass. Whole-pixel moves only."""
    p = im.load()
    w, h = im.size
    spans = {}
    for y in range(h):
        xs = [x for x in range(w) if p[x, y][3] > 100]
        if xs:
            spans[y] = _runs(p, w, y)
    hip = h - 1   # up from the feet while the row stays leg-narrow
    while hip - 1 in spans and spans[hip - 1][-1][1] - spans[hip - 1][0][0] <= w // 2:
        hip -= 1
    theta = 2 * math.pi * step / STROLL_STEPS
    off = {'A': STROLL_AMP * math.sin(theta), 'B': -STROLL_AMP * math.sin(theta)}
    lift = {'A': STROLL_LIFT * max(0.0, -math.cos(theta)), 'B': STROLL_LIFT * max(0.0, math.cos(theta))}
    bob = int(round(abs(math.cos(theta))))
    out = Image.new('RGBA', (w + 2 * MARGIN, h + MARGIN), (0, 0, 0, 0))
    q = out.load()
    for y in range(hip):
        for x in range(w):
            if p[x, y][3] > 100:
                q[x + MARGIN, y + MARGIN - bob] = p[x, y]
    L = float(h - 1 - hip) or 1.0
    for leg in ('B', 'A'):   # A (the left leg) drawn last, over B
        for y in range(hip, h):
            r = spans.get(y)
            if not r:
                continue
            if len(r) >= 2:
                gaps = [(r[i + 1][0] - r[i][1], (r[i][1] + r[i + 1][0]) / 2.0) for i in range(len(r) - 1)]
                divide = max(gaps)[1]
            else:
                divide = (r[0][0] + r[-1][1]) / 2.0
            t = (y - hip) / L
            dx = int(round(t * off[leg]))
            # rows rise with the hip (bob) at the top and with the lifted foot at the bottom
            dy = -int(round(bob * (1 - t) + lift[leg] * t * t))
            for x in range(w):
                if p[x, y][3] > 100 and (x < divide) == (leg == 'A'):
                    xd, yd = x + dx + MARGIN, y + dy + MARGIN
                    if 0 <= yd < out.height and 0 <= xd < out.width:
                        q[xd, yd] = p[x, y]
    # fill one-pixel holes the shear opened inside a leg (a row moved further than its neighbours)
    for y in range(1, out.height - 1):
        for x in range(1, out.width - 1):
            if q[x, y][3] == 0 and q[x, y - 1][3] and q[x, y + 1][3]:
                q[x, y] = q[x, y - 1]
    return out


def frame(atlas, entry):
    """One 128 px cell: the frame (or a re-posed walk frame) shrunk to 75%, its feet on the cell's foot point."""
    if isinstance(entry, tuple) and entry[0] == 'stroll':
        _, fid, step = entry
        im, fx, fy = upright(atlas, fid)
        im, fx, fy = stroll_pose(im, step), fx + MARGIN, fy + MARGIN
    else:
        im, fx, fy = upright(atlas, entry)
    im = shrink(im)
    fx, fy = int(round(fx * 0.75)), int(round(fy * 0.75))
    cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    # paste() clips anything that sticks out of the cell (nothing does at CELL 128)
    cell.paste(im, (CELL // 2 - fx, CELL - FEET_ROW - fy), im)
    return cell


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


MARGIN = 8   # room stroll_pose() adds around a frame for the moving legs


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




def build_clip(atlas, name):
    cells = [frame(atlas, i) for i in CLIPS[name]]
    sheet = Image.new('RGBA', (CELL * len(cells), CELL * 4), (0, 0, 0, 0))
    for c, cell in enumerate(cells):
        flipped = cell.transpose(Image.FLIP_LEFT_RIGHT)
        for r in (UP, LEFT, DOWN, RIGHT):
            sheet.paste(cell if r in (LEFT, DOWN) else flipped, (c * CELL, r * CELL))
    return sheet


def contact(atlas, path, scale=2):
    """Every clip's left-facing row, one clip per line, for checking alignment (a line marks the feet)."""
    longest = max(len(v) for v in CLIPS.values())
    w, h = CELL * scale, CELL * scale
    out = Image.new('RGBA', (w * longest, h * len(CLIPS)), (60, 60, 70, 255))
    d = ImageDraw.Draw(out)
    for r, name in enumerate(CLIPS):
        for c, fid in enumerate(CLIPS[name]):
            cell = frame(atlas, fid).resize((w, h), Image.NEAREST)
            out.alpha_composite(cell, (c * w, r * h))
            label = fid[0] if isinstance(fid, tuple) else fid
            d.text((c * w + 3, r * h + 2), '%s %s' % (name, label), fill=(255, 255, 0, 255))
        y = r * h + (CELL - FEET_ROW) * scale
        d.line([0, y, out.width, y], fill=(255, 80, 80, 120))
    d.line([w // 2, 0, w // 2, out.height], fill=(80, 200, 255, 90))
    out.save(path)


def main(argv):
    atlas = Image.open(SHEET).convert('RGBA')
    if '--contact' in argv:
        contact(atlas, argv[argv.index('--contact') + 1])
        return
    os.makedirs(OUT, exist_ok=True)
    for name in CLIPS:
        sheet = build_clip(atlas, name)
        sheet.save(os.path.join(OUT, name + '.png'))
        print('%-10s %2d frames  %dx%d' % (name, len(CLIPS[name]), sheet.width, sheet.height))
    sys.path.insert(0, HERE)
    import alt_recolour   # every ally's sheets (A3 included) from these
    alt_recolour.main()


if __name__ == '__main__':
    main(sys.argv[1:])
