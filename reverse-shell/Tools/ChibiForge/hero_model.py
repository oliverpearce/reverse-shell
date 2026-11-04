"""
Hero model: every human-shaped sprite (the crew and the NPCs) from one clean pixel-art sprite, reference/Hero.png
(29 x 47, 16 colours, front view: a girl with long navy hair and a ponytail, a white crop top, a gold belt, grey
cargo pants, magenta gloves and boots).

    python Tools/ChibiForge/hero_model.py [Names...] [--sheet out.png]

Native 64 px frames, no scaling, so every pixel is the artist's pixel.

Animation by layers (native boxes on Hero.png): the ponytail (behind), two legs (from row 37, split at x 16), two
arms (x <= 10 and x >= 23, rows 24-37) and the body. A layer that moves leaves no hole at its seam: the source
pixels on the seam side fill any gap it opens.
    idle 2 / walk 9 (frame 0 stands) / slash 6 / spellcast 7 / hurt 6 (one row, ends lying down)
Directions: the art is a front view turned slightly to the viewer's right. Down and right use it as drawn, left is
the mirror, and up is a back view (face painted over with hair).
Recolours (SPECS): hair (4 tones, only in the hair area), glowing eyes for the crew, the magenta accent (gloves,
boots, collar), and for NPCs the top and pants.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'SpriteForge'))
from pngtool import read_png, write_png  # noqa: E402

F = 64
GROUND = 61                       # the feet's lowest row (feet pivot 0.04)
CHARS = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')
SRC = os.path.join(HERE, 'reference', 'Hero.png')
UP, LEFT, DOWN, RIGHT = 0, 1, 2, 3
OUTLINE = (0x14, 0x15, 0x2B)


def hx(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def ramp(*hs):
    return [hx(h) for h in hs]


# The source palette, by role.
HAIR = ramp('#221E38', '#2D2B4F', '#333157', '#636083')     # dark -> light
ACCENT = ramp('#6E2C5A', '#B54E75')                         # gloves, boots, collar (dark, light)
TOP = ramp('#B2B1BE', '#DEDEE9')
PANTS = ramp('#3A394C', '#545465', '#5B596C')
SKIN = {hx('#F4D9D5'), hx('#CF987C'), hx('#9F7D5D')}
EYE = (15, 13)                    # the one visible iris pixel
FACE = (9, 9, 22, 18)             # x0, y0, x1, y1 (exclusive)

SPECS = {
    # The crew: android girls; glowing eyes in their colour, own hair and accent.
    'A3': {'hair': HAIR, 'eye': hx('#BFE6FF'), 'accent': ACCENT, 'top': None, 'pants': None},
    'Ashe': {'hair': ramp('#14261A', '#1E3A26', '#2A5232', '#5E9A58'), 'eye': hx('#8EF070'),
                'accent': ramp('#2E5A2A', '#5E9A44'), 'top': None, 'pants': None},
    'Lucy': {'hair': ramp('#24102E', '#341A44', '#4A265E', '#8A58AE'), 'eye': hx('#D07AFF'),
             'accent': ramp('#4A1E6A', '#8A44B4'), 'top': None, 'pants': None},
    # Ruin survivors: plain eyes, own hair, accent and outfit.
    'NoodleVendor': {'hair': ramp('#1A120E', '#2A1C14', '#3C2A1E', '#72563A'), 'eye': None,
                     'accent': ramp('#5A2A1E', '#8E4A2E'), 'top': ramp('#B89A78', '#E0CBA8'),
                     'pants': ramp('#3A2A22', '#4E3A2E', '#5A4636')},
    'Courier': {'hair': ramp('#2A1A10', '#3C2616', '#56381E', '#8E6436'), 'eye': None,
                'accent': ramp('#1E3A6A', '#3A64A0'), 'top': ramp('#8AA4C0', '#BCD0E4'),
                'pants': ramp('#22283A', '#30384E', '#3A4458')},
    'Drifter': {'hair': ramp('#3A3A40', '#50505A', '#686874', '#A4A4AE'), 'eye': None,
                'accent': ramp('#2A3A26', '#4A6640'), 'top': ramp('#8A8A7A', '#B4B4A2'),
                'pants': ramp('#2C3226', '#3C4434', '#46503C')},
}


def load():
    w, h, px = read_png(SRC)
    return w, h, [[p[:3] if p[3] else None for p in row] for row in px]


def hair_area(x, y):
    return y <= 18 or (x <= 9 and y <= 24)


def dress(px, w, h, spec):
    """Applies the member's hair, eyes, accent and (NPCs) outfit colours. Returns a new grid."""
    out = [row[:] for row in px]
    maps = [({HAIR[i]: spec['hair'][i] for i in range(4)}, hair_area),
            ({ACCENT[i]: spec['accent'][i] for i in range(2)}, lambda x, y: y >= 19)]
    if spec['top']:
        maps.append(({TOP[i]: spec['top'][i] for i in range(2)}, lambda x, y: y >= 19))
    if spec['pants']:
        maps.append(({PANTS[i]: spec['pants'][i] for i in range(3)}, lambda x, y: y >= 24))
    for y in range(h):
        for x in range(w):
            c = px[y][x]
            if c is None:
                continue
            for m, where in maps:
                if c in m and where(x, y):
                    out[y][x] = m[c]
                    break
    if spec['hair'] != HAIR:
        # the ponytail's dark mass is drawn in the outline colour: inner pixels take the second-darkest hair tone
        for y in range(15, 24):
            for x in range(1, 10):
                if px[y][x] == OUTLINE and all(px[y + dy][x + dx] is not None
                                               for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    out[y][x] = spec['hair'][1]
    if spec['eye']:
        out[EYE[1]][EYE[0]] = spec['eye']
    return out


def back_view(px, spec):
    """From behind: the face (skin, eye and lash pixels in the face box) becomes hair: a mid tone with lighter
    strand lines every third column and a darker band along the bottom edge."""
    out = [row[:] for row in px]
    hair = spec['hair']
    for y in range(FACE[1], FACE[3]):
        for x in range(FACE[0], FACE[2]):
            if out[y][x] is None:
                continue
            c = hair[3] if x % 3 == 0 else hair[2]
            if y >= FACE[3] - 2:
                c = hair[1]
            out[y][x] = c
    return out


def region(x, y):
    if 24 <= y <= 37 and x <= 10 and not (y <= 24 and x >= 9):
        return 'al'
    if 24 <= y <= 37 and x >= 23:
        return 'ar'
    if y >= 37:
        return 'll' if x <= 16 else 'lr'
    if x <= 5 and 14 <= y <= 23:
        return 'tail'
    return 'body'


# A moving layer fills the gap it opens on its seam side with its own source pixels.
SEAM = {'al': lambda x, y: y < 30, 'ar': lambda x, y: y < 30, 'tail': lambda x, y: x >= 3}
ORDER = ('tail', 'll', 'lr', 'body', 'al', 'ar')


def compose(px, w, h, offs, ox=None, fx=None):
    """A 64 px frame from layer offsets {layer: (dx, dy)}; fx(frame, ox, oy) draws effects on top."""
    fr = [[None] * F for _ in range(F)]
    ox = (F - w) // 2 if ox is None else ox
    oy = GROUND - (h - 1)

    def put(X, Y, c):
        if 0 <= X < F and 0 <= Y < F:
            fr[Y][X] = c

    for layer in ORDER:
        dx, dy = offs.get(layer, (0, 0))
        cells = [(x, y, px[y][x]) for y in range(h) for x in range(w) if px[y][x] is not None and region(x, y) == layer]
        seam = SEAM.get(layer)
        if seam and (dx or dy):
            for x, y, c in cells:
                if seam(x, y):
                    put(ox + x, oy + y, c)
        for x, y, c in cells:
            put(ox + x + dx, oy + y + dy, c)
    if fx:
        fx(fr, ox, oy)
    return fr


def arc(fr, cx, cy, r, colour):
    """A pale slash crescent to the right of (cx, cy)."""
    for a in range(-60, 61, 6):
        t = math.radians(a)
        for rr in (r, r - 1):
            x, y = cx + int(round(math.cos(t) * rr)), cy + int(round(math.sin(t) * rr))
            if 0 <= x < F and 0 <= y < F:
                fr[y][x] = colour if rr == r else (255, 255, 255)


def orb(fr, cx, cy, r, colour):
    for y in range(cy - r - 1, cy + r + 2):
        for x in range(cx - r - 1, cx + r + 2):
            if not (0 <= x < F and 0 <= y < F):
                continue
            d = math.hypot(x - cx, y - cy)
            if d <= r:
                fr[y][x] = (255, 255, 255) if d < r * 0.45 else colour
            elif d <= r + 1:
                fr[y][x] = OUTLINE


def mirror(fr):
    return [row[::-1] for row in fr]


def lying(fr):
    rot = [[None] * F for _ in range(F)]
    for y in range(F):
        for x in range(F):
            rot[x][F - 1 - y] = fr[y][x]
    lowest = max((y for y in range(F) for x in range(F) if rot[y][x] is not None), default=GROUND)
    out = [[None] * F for _ in range(F)]
    shift = GROUND - lowest
    for y in range(F):
        for x in range(F):
            if rot[y][x] is not None and 0 <= y + shift < F:
                out[y + shift][x] = rot[y][x]
    return out


# ---------------------------------------------------------------------------------------------------- poses

def idle(i):
    return {'body': (0, i), 'al': (0, i), 'ar': (0, i), 'tail': (0, i)}


def walk(i, side):
    """Frame 0 stands; 1-8 are one stride: legs lift in turn, arms swing against them, the body bobs on the
    passing frames and the ponytail trails. Side views add a stride (legs slide forward and back)."""
    if i == 0:
        return {}
    s = math.sin((i - 1) / 8.0 * 2 * math.pi)
    lift_l, lift_r = max(0, int(round(2 * s))), max(0, int(round(-2 * s)))
    stride = int(round(s)) if side else 0
    bob = 1 if abs(s) < 0.5 else 0
    swing = int(round(s))
    return {'body': (0, bob), 'tail': (-1 if side else int(round(-s)), bob),
            'al': (0, bob - swing), 'ar': (0, bob + swing),
            'll': (stride, -lift_l), 'lr': (-stride, -lift_r)}


SLASH = [((0, 0), (0, 0)), ((-1, 0), (0, -1)), ((1, 0), (1, -2)), ((2, 0), (1, -2)), ((1, 0), (1, -1)), ((0, 0), (0, 0))]


def slash(i):
    """A lunging strike with the right-hand arm (forward is +x)."""
    (lean, _), arm = SLASH[i]
    return {'body': (lean, 0), 'tail': (lean - 1, 0), 'al': (lean, 0), 'ar': (lean + arm[0], arm[1])}


CAST = [0, 1, 2, 3, 3, 2, 0]      # orb radius; the arms rise with it


def cast(i):
    r = CAST[i]
    up = -min(r, 2)
    return {'al': (1 if r else 0, up), 'ar': (-1 if r else 0, up), 'tail': (-(r > 0), 0)}, r


HURT = [{'body': (-1, 0), 'al': (-1, 0), 'ar': (-1, 0), 'tail': (-2, 0)},
        {'body': (-2, 1), 'al': (-2, 2), 'ar': (-2, 2), 'tail': (-3, 1)},
        {'body': (-1, 3), 'al': (-1, 4), 'ar': (-1, 4), 'tail': (-2, 3), 'll': (0, 1), 'lr': (0, 1)},
        {'body': (0, 5), 'al': (0, 6), 'ar': (0, 6), 'tail': (-1, 5), 'll': (0, 2), 'lr': (0, 2)}]


def frames_for(name):
    w, h, base = load()
    spec = SPECS[name]
    px = dress(base, w, h, spec)
    back = back_view(px, spec)
    glow = spec['eye'] or (230, 230, 240)

    def row(anim, facing):
        src = back if facing == UP else px
        side = facing in (LEFT, RIGHT)
        if anim == 'idle':
            out = [compose(src, w, h, idle(i)) for i in range(2)]
        elif anim == 'walk':
            out = [compose(src, w, h, walk(i, side)) for i in range(9)]
        elif anim == 'slash':
            out = []
            for i in range(6):
                offs = slash(i)
                fx = None
                if i in (2, 3) and facing != UP:
                    dx = offs['ar'][0]
                    fx = (lambda d: lambda fr, ox, oy: arc(fr, ox + w - 3 + d, oy + 30, 8, glow))(dx)
                out.append(compose(src, w, h, offs, fx=fx))
        else:
            out = []
            for i in range(7):
                offs, r = cast(i)
                fx = None
                if r and facing != UP:
                    fx = (lambda rr: lambda fr, ox, oy: orb(fr, ox + w // 2 + 1, oy + 30, rr, glow))(r)
                out.append(compose(src, w, h, offs, fx=fx))
        return [mirror(f) for f in out] if facing == LEFT else out

    sheets = {anim: [row(anim, d) for d in (UP, LEFT, DOWN, RIGHT)] for anim in ('idle', 'walk', 'slash', 'spellcast')}
    hurt = [compose(px, w, h, HURT[i]) for i in range(4)]
    down = compose(px, w, h, {})
    sheets['hurt'] = [hurt + [lying(down), lying(down)]]
    return sheets


def to_rgba(fr):
    return [[(c[0], c[1], c[2], 255) if c is not None else (0, 0, 0, 0) for c in row] for row in fr]


def build(name):
    out_dir = os.path.join(CHARS, name)
    os.makedirs(out_dir, exist_ok=True)
    for anim, rows in frames_for(name).items():
        cols = len(rows[0])
        img = [[(0, 0, 0, 0)] * (F * cols) for _ in range(F * len(rows))]
        for r, frames in enumerate(rows):
            for c, fr in enumerate(frames):
                rgba = to_rgba(fr)
                for y in range(F):
                    img[r * F + y][c * F:(c + 1) * F] = rgba[y]
        write_png(os.path.join(out_dir, anim + '.png'), F * cols, F * len(rows), img)
    with open(os.path.join(out_dir, 'CREDITS.md'), 'w', encoding='utf-8') as f:
        f.write('# %s: sprite credits\n\nAnimated by `Tools/ChibiForge/hero_model.py` from `Tools/ChibiForge/reference/'
                'Hero.png` (the user\'s reference sprite; see that folder\'s README for its rights note), recoloured '
                'per character. Native 64 px frames.\n' % name)
    print('built', name, '(hero model)')


def contact(path, names, scale=4):
    cells = []
    for n in names:
        sh = frames_for(n)
        cells.append([sh['walk'][DOWN][0], sh['walk'][DOWN][3], sh['walk'][DOWN][7], sh['walk'][LEFT][3],
                      sh['walk'][UP][3], sh['slash'][RIGHT][3], sh['spellcast'][DOWN][3], sh['hurt'][0][5]])
    W, H = len(cells[0]) * F, len(cells) * F
    bg = (52, 50, 60, 255)
    img = [[bg] * W for _ in range(H)]
    for r, row in enumerate(cells):
        for c, fr in enumerate(row):
            for y in range(F):
                for x in range(F):
                    if fr[y][x] is not None:
                        img[r * F + y][c * F + x] = fr[y][x] + (255,)
    write_png(path, W * scale, H * scale, [[img[y // scale][x // scale] for x in range(W * scale)] for y in range(H * scale)])


if __name__ == '__main__':
    args = sys.argv[1:]
    sheet = None
    if '--sheet' in args:
        i = args.index('--sheet')
        sheet = args[i + 1]
        del args[i:i + 2]
    names = args or list(SPECS)
    if sheet:
        contact(sheet, names)
    else:
        for n in names:
            build(n)
