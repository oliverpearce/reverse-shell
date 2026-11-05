"""
A3's walk styles (Esc > Settings > Walk, GameSettings.WalkStyle): three walks to compare and refine.

    py -3.9 Tools/ChibiForge/a3_walks.py --preview out.png     (a strip of every style, left-facing, for checking)

a3_model.py imports this: CLIPS gets walk_s1..walk_s3 (and walk = WALK_DEFAULT), and frame() calls pose() for the
('rig', ...) entries below.

Every style walks the A2 study's body wearing the old model's hair (a3_oldhair), each step on the next of its drawn
hair-sway frames. Each leg is cut into a thigh (hip to KNEE) and a shin; the thigh turns about the hip and the
shin about the knee (inverse-mapped, nearest pixel, so the drawn texture stays whole). The body then drops until the
lowest foot is on the ground, plus each style's own bob, and the head (rows above NECK) moves on its own by a pixel.

  1 Stride     a sine walk: heel strike, crossing pass, knee lift; leans 1 px into the step.
  2 Reference  keyed from the CC0 "8-frame pixel art walk cycle template" (MostlyDecentGames): contact, down, pass, up;
               the body lowest on contact, the head rising a pixel on the pass and trailing a pixel on contact.
  3 Gandalf    inspired by the walk in GandalfHardcore's Character Asset Pack (its art is not used or kept): a long
               stride, a big knee kick on the pass (the shin swung back almost level), the body rising on the up beats.
The run (holding Shift) is the walk at 1.5x speed (CharacterSprite), for all three.
"""
import math

from PIL import Image

import a3_oldhair
import a3_portrait
import a3_study

# The A2 study's body (a3_portrait.cut(native()), 64 x 84): rows from HIP down are legs, the knees at KNEE.
HIP = 48                      # the thighs' top
KNEE = 58                     # the knees
NECK = 33                     # rows above this are the head (face, bangs; the long hair below moves with the body)
FOOT = a3_portrait.FOOT       # between her feet, bottom row
MARGIN = 10

_study = {}


def overworld_frame(atlas, upright, k):
    """(image, foot x, foot y): the study standing still, wearing the old model's hair (sway frame k of
    a3_oldhair.HAIR_FRAMES), cached. The overworld idle and every walk step are drawn from these."""
    k %= len(a3_oldhair.HAIR_FRAMES)
    key = ('old', k)
    if key not in _study:
        base, fx, fy = study_frame(0)
        sheet, _, _ = upright(atlas, a3_oldhair.HAIR_FRAMES[k])
        merged = a3_oldhair.wear(base, sheet)
        # Below the hip the legs stay exactly the study's (the rig cuts them by shape, so nothing may change them): the
        # merged frame only shows where the study had hair or nothing, and there only the old hair itself (no outline).
        m, b = merged.load(), base.load()
        hair = set(a3_portrait.STUDY_HAIR)
        for y in range(HIP, base.height):
            for x in range(base.width):
                if b[x, y][3] and b[x, y][:3] not in hair:
                    m[x, y] = b[x, y]
                elif not (m[x, y][3] and a3_oldhair.is_sheet_hair(m[x, y][:3])):
                    m[x, y] = (0, 0, 0, 0) if not b[x, y][3] else m[x, y]
        # a black blindfold over the eyes (rows 27-29 of the study's face; the old hair stays on top)
        for y, col in ((27, (44, 40, 48, 255)), (28, (22, 20, 26, 255)), (29, (22, 20, 26, 255))):
            for x in range(18, 35):
                if m[x, y][3] and not a3_oldhair.is_sheet_hair(m[x, y][:3]):
                    m[x, y] = col
        _study[key] = merged
    return _study[key], FOOT[0], FOOT[1]


def study_frame(t):
    """(image, foot x, foot y) of the study's idle frame t (a3_study), cached."""
    t %= a3_study.FRAMES
    if t not in _study:
        if 'base' not in _study:
            _study['base'] = a3_portrait.cut(a3_portrait.native())
        _study[t] = a3_study.frame(t, _study['base'])
    return _study[t], FOOT[0], FOOT[1]

# Keyed curves, 8 frames, for leg A (B is half a cycle later). thigh: + = knee forward (she faces left); bend: + = shin
# swung back; bob: px down; head: (dx, dy) px, + = back / down.
REFERENCE = dict(
    thigh=[24, 12, 0, -14, -22, -18, 4, 20],
    bend=[0, 18, 6, 0, 8, 40, 58, 22],
    bob=[1, 1, 0, 0, 1, 1, 0, 0],
    head=[(1, 0), (0, 0), (0, -1), (0, 0), (1, 0), (0, 0), (0, -1), (0, 0)],
)
GANDALF = dict(
    thigh=[22, 26, 10, -6, -20, -24, -8, 12],
    bend=[4, 10, 0, 0, 14, 42, 78, 56],   # the big knee kick, eased in so the foot never jumps
    bob=[0, 0, -1, -1, 0, 0, -1, -1],
    head=[(0, 0), (0, 0), (-1, 0), (0, 0), (0, 0), (0, 0), (-1, 0), (0, 0)],
)

STYLES = {
    1: dict(name='Stride', steps=8, thigh=24, knee=42, lean=1, fps=10),
    # the keyed walks play 12 frames (smooth in-betweens of their 8 keys) at 1.5x the fps: the same speed, smaller steps
    2: dict(name='Reference', steps=12, curve=REFERENCE, lean=0, fps=15),
    3: dict(name='Gandalf', steps=12, curve=GANDALF, lean=1, fps=13.5),
}
WALK_DEFAULT = 1


def clips():
    """Clip name -> frame entries for a3_model.CLIPS."""
    out = {}
    for n, s in STYLES.items():
        # each step wears the next of the old hair's drawn sway frames, so it keeps swaying while she walks
        # (the 6 frames once per walk loop, in order, so the sway never snaps back mid-loop)
        hf = len(a3_oldhair.HAIR_FRAMES)
        out['walk_s%d' % n] = [('rig', (k * hf) // s['steps'], n, k) for k in range(s['steps'])]
    out['walk'] = list(out['walk_s%d' % WALK_DEFAULT])
    return out


def fps(clip):
    n = WALK_DEFAULT if clip == 'walk' else int(clip[-1])
    return STYLES[n]['fps']


def pose(atlas, entry, upright):
    """(image, foot x, foot y) for a rig entry, before a3_model shrinks it."""
    _, k, style, step = entry
    im, fx, fy = overworld_frame(atlas, upright, k)
    return rig(im, fx, fy, STYLES[style], step)


# ----------------------------------------------------------------------------------------------------------- the rig

_HAIR = set(a3_portrait.STUDY_HAIR)


def _is_hair(c):
    """The study's hair or the old model's (a3_oldhair): it hangs with the body, the legs swing under it."""
    return tuple(c[:3]) in _HAIR or a3_oldhair.is_sheet_hair(c[:3])


def _leg_masks(p, w, h):
    """Pixels of each leg below HIP (not the hair hanging there): 'A' = the front leg (left of the split), 'B' = back."""
    a, b = set(), set()
    for y in range(HIP, h):
        xs = [x for x in range(w) if p[x, y][3] > 100 and not _is_hair(p[x, y][:3])]
        if not xs:
            continue
        # the split: the darkest outline column between the legs near the middle of the row, else the middle
        lo, hi = xs[0], xs[-1]
        mid = (lo + hi) / 2.0
        best, split = None, mid
        for x in range(lo + 2, hi - 1):
            r, g, bb, al = p[x, y]
            lum = 0.299 * r + 0.587 * g + 0.114 * bb if al > 100 else 0
            score = lum + 6 * abs(x - mid)
            if best is None or score < best:
                best, split = score, x
        for x in xs:
            (a if x <= split else b).add((x, y))
    return a, b


def _rot(x, y, cx, cy, deg):
    t = math.radians(deg)
    c, s = math.cos(t), math.sin(t)
    dx, dy = x - cx, y - cy
    return cx + dx * c - dy * s, cy + dx * s + dy * c


def _pose(st, step):
    """Per leg (thigh deg, knee bend deg), the bob (px down) and the head's (dx, dy) for this step."""
    n = st['steps']
    if 'curve' in st:
        cv = st['curve']
        keys = len(cv['thigh'])
        u = step * keys / float(n)              # where this frame falls among the keys
        ub = (u + keys / 2.0) % keys            # leg B: half a cycle later
        poses = {'A': (_smooth(cv['thigh'], u), _smooth(cv['bend'], u)),
                 'B': (_smooth(cv['thigh'], ub), _smooth(cv['bend'], ub))}
        k = int(round(u)) % keys                # whole-pixel bob and head move: the nearest key
        return poses, cv['bob'][k], cv['head'][k]
    phi = 2 * math.pi * step / n
    poses = {}
    for leg, ph in (('A', phi), ('B', phi + math.pi)):
        poses[leg] = (st['thigh'] * math.cos(ph), st['knee'] * max(0.0, math.sin(ph)))
    return poses, 0, (0, 0)


def _smooth(keys, u):
    """Catmull-Rom through the looping keys at position u (0..len): the in-betweens of a keyed curve."""
    n = len(keys)
    i = int(math.floor(u)) % n
    t = u - math.floor(u)
    p0, p1, p2, p3 = keys[(i - 1) % n], keys[i], keys[(i + 1) % n], keys[(i + 2) % n]
    return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t)


def rig(im, fx, fy, st, step):
    p = im.load()
    w, h = im.size
    legs = dict(zip('AB', _leg_masks(p, w, h)))
    poses, bob, (hx_, hy_) = _pose(st, step)

    big = Image.new('RGBA', (w + 2 * MARGIN, h + 2 * MARGIN), (0, 0, 0, 0))
    q = big.load()
    ox, oy = MARGIN, MARGIN
    lowest = {}

    def draw_leg(leg):
        mask = legs[leg]
        if not mask:
            return
        xs = [x for x, y in mask if y == HIP] or [x for x, y in mask]
        hx = (min(xs) + max(xs)) / 2.0
        kxs = [x for x, y in mask if y == KNEE] or xs
        kx = (min(kxs) + max(kxs)) / 2.0
        thigh, knee = poses[leg]
        th, kn = thigh, -knee   # the knee bends backward: the foot goes toward +x (behind her)
        kx2, ky2 = _rot(kx, KNEE, hx, HIP, th)
        y1 = max(y for x, y in mask)
        reach = (y1 - HIP) + 4
        for Y in range(int(HIP - 4), int(HIP + reach + 4)):
            for X in range(int(hx - reach - 2), int(hx + reach + 3)):
                sx, sy = _rot(X + 0.5, Y + 0.5, kx2, ky2, -(th + kn))
                sx, sy = sx - kx2 + kx, sy - ky2 + KNEE
                src = (int(math.floor(sx)), int(math.floor(sy)))
                if not (src in mask and src[1] >= KNEE):
                    tx, ty = _rot(X + 0.5, Y + 0.5, hx, HIP, -th)
                    src = (int(math.floor(tx)), int(math.floor(ty)))
                    if not (src in mask and src[1] < KNEE + 1):
                        continue
                q[X + ox, Y + oy] = p[src[0], src[1]]
                lowest[leg] = max(lowest.get(leg, 0), Y)

    # the hair hanging past the hip, behind the legs
    for y in range(HIP, h):
        for x in range(w):
            if p[x, y][3] > 100 and _is_hair(p[x, y][:3]):
                q[x + ox, y + oy] = p[x, y]
    draw_leg('B')
    draw_leg('A')
    # the body above the hip, over the legs' tops, leaning into the step; the head moves on its own by a pixel
    lean = st['lean']
    for y in range(HIP + 1):
        dx = -int(round(lean * (HIP - y) / float(HIP)))
        hdx, hdy = (hx_, hy_) if y < NECK else (0, 0)
        for x in range(w):
            if p[x, y][3] > 100:
                q[x + ox + dx + hdx, y + oy + hdy] = p[x, y]
    # drop so the lowest foot stands on the original foot row, then the style's own bob
    ground = max(lowest.values()) if lowest else fy
    drop = fy - ground + bob
    out = Image.new('RGBA', big.size, (0, 0, 0, 0))
    out.paste(big, (0, drop))
    # nothing below the ground row (a bob down must not push the feet through the floor)
    o = out.load()
    for y in range(fy + oy + 1, out.height):
        for x in range(out.width):
            o[x, y] = (0, 0, 0, 0)
    return out, fx + ox, fy + oy


if __name__ == '__main__':
    import os
    import sys
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import a3_model as m
    from PIL import ImageDraw
    atlas = Image.open(m.SHEET).convert('RGBA')
    out_path = sys.argv[sys.argv.index('--preview') + 1]
    cl = clips()
    names = ['walk_s%d' % n for n in STYLES]
    scale, cw = 6, 48
    longest = max(len(cl[n]) for n in names)
    sheet = Image.new('RGBA', (cw * scale * longest, cw * scale * len(names)), (60, 60, 70, 255))
    d = ImageDraw.Draw(sheet)
    for r, name in enumerate(names):
        for c, e in enumerate(cl[name]):
            cell = m.frame(atlas, e).crop((40, 68, 88, 116)).resize((cw * scale, cw * scale), Image.NEAREST)
            sheet.alpha_composite(cell, (c * cw * scale, r * cw * scale))
        d.text((4, r * cw * scale + 2), '%s %s' % (name, STYLES[int(name[-1])]['name']), fill=(255, 255, 0, 255))
    sheet.save(out_path)
    print('preview:', out_path)
