"""
Robot model: the crew's (and NPCs') sprites from one clean 64 px pixel-art sprite, reference/Robot.png
(51 x 62, 15 colours: a knight with a white helmet, dark hair, white plate, a crimson cape and a sword).

    python Tools/ChibiForge/robot_model.py [Names...] [--sheet out.png]

Native 64 px frames, no scaling, so every pixel is the artist's pixel.

Robot conversion (crew): the bare thighs and hands become dark mech joints and metal; the eyes glow in the
member's colour; the choker becomes a glowing collar light; a glowing seam and core go on the breastplate; and an
ear module with a light sits on the side of the head.
Recolours: the hair's 6 tones are remapped to the member's hair ramp (only inside the hair area, so the helmet and
armour greys stay). NPCs use the same sprite as humans (no robot parts) with their own hair and cape colours.

Animation by layers (native boxes on Robot.png): the cape (left, behind), the sword and sword hand (right; NPCs go
unarmed), two legs (below row 50, split at x 27) and the body.
    idle 2 / walk 9 (frame 0 stands) / slash 6 / spellcast 7 / hurt 6 (one row, ends lying down)
The art faces right (three-quarter view): right and down use it as drawn, left is the mirror, and up is a back
view (face and eyes painted over with hair).
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
OX = 4                            # the sprite's left edge in the frame (room for the sword swing)
CHARS = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')
SRC = os.path.join(HERE, 'reference', 'Robot.png')
UP, LEFT, DOWN, RIGHT = 0, 1, 2, 3
OUTLINE = (0x2B, 0x15, 0x26)


def hx(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


# The source palette, by role.
HAIR = [hx('#321F31'), hx('#36283B'), hx('#4A3E58'), hx('#574D67'), hx('#665D76'), hx('#886484')]   # dark -> light
SKIN, SKIN_SHADE = hx('#F1DED0'), hx('#C49B75')
CAPE = [hx('#701431'), hx('#952645')]
CHOKER = hx('#952645')
METAL = [hx('#767286'), hx('#99989A'), hx('#AFAFB9'), hx('#F6F5F8')]

# Areas on Robot.png (x0, y0, x1, y1, exclusive).
HELMET = (16, 0, 36, 14)          # the helmet keeps its greys
FACE = (22, 14, 39, 29)
EYES = [(26, 20, 31, 24), (33, 20, 38, 24)]
HAIR_AREA = (0, 2, 44, 32)


def ramp(*hs):
    return [hx(h) for h in hs]


SPECS = {
    # The crew: robots. hair = 6 tones (dark -> light), glow = eyes / lights, cape = 2 tones (dark, light).
    'MC': {'robot': True, 'hair': ramp('#1C2234', '#26304A', '#34446A', '#455A86', '#5A72A2', '#7C96C4'),
           'glow': hx('#BFE6FF'), 'cape': CAPE},
    'HealBot': {'robot': True, 'hair': ramp('#14261A', '#1C3624', '#2A5232', '#3A6E40', '#4E8A50', '#78B868'),
                'glow': hx('#8EF070'), 'cape': ramp('#1E4A24', '#2E6A34')},
    'Lucy': {'robot': True, 'hair': ramp('#24102E', '#341A44', '#4A265E', '#5E3478', '#76449A', '#A066C8'),
             'glow': hx('#D07AFF'), 'cape': ramp('#3A1458', '#5A2280')},
    # Ruin survivors: the same sprite as people.
    'NoodleVendor': {'robot': False, 'hair': ramp('#2A160E', '#3A2014', '#56321E', '#6E4428', '#8A5A36', '#A8744A'),
                     'glow': hx('#FFE0A0'), 'cape': ramp('#6A3A14', '#8E5420')},
    'Courier': {'robot': False, 'hair': ramp('#1E140E', '#2C1E14', '#40301E', '#56422A', '#6C5636', '#8A7048'),
                'glow': hx('#C8E0FF'), 'cape': ramp('#1E3A6A', '#2E5490')},
    'Drifter': {'robot': False, 'hair': ramp('#3A3A40', '#4C4C54', '#646470', '#7C7C88', '#9898A4', '#B8B8C4'),
                'glow': hx('#E0E0E0'), 'cape': ramp('#2E4A2A', '#43663C')},
}


def inside(box, x, y):
    return box[0] <= x < box[2] and box[1] <= y < box[3]


def load():
    w, h, px = read_png(SRC)
    return w, h, [[p[:3] if p[3] else None for p in row] for row in px]


def dress(px, w, h, spec):
    """Applies the member's hair colours, eyes and (for robots) the mechanical parts. Returns a new grid."""
    out = [row[:] for row in px]
    hair_map = {HAIR[i]: spec['hair'][i] for i in range(6)}
    glow = spec['glow']
    dim = tuple(int(c * 0.62) for c in glow)
    for y in range(h):
        for x in range(w):
            c = px[y][x]
            if c is None:
                continue
            in_eye = any(inside(b, x, y) for b in EYES)
            if in_eye and c in (HAIR[5], HAIR[4]):
                out[y][x] = glow if c == HAIR[5] else dim     # glowing eyes
                continue
            if c in CAPE and not inside(FACE, x, y):   # (the lips share the cape's light crimson)
                out[y][x] = spec['cape'][CAPE.index(c)]
                if not spec['robot'] or y < 30:
                    continue
            if inside(HAIR_AREA, x, y) and not inside(HELMET, x, y) and c in hair_map and not inside(FACE, x, y):
                out[y][x] = hair_map[c]
                continue
            if inside(FACE, x, y) and c in hair_map and y < 21:   # bangs over the forehead
                out[y][x] = hair_map[c]
                continue
            if spec['robot']:
                if c == CHOKER and 26 <= y <= 28 and 26 <= x <= 33:
                    out[y][x] = glow                            # collar light
                elif c in (SKIN, SKIN_SHADE) and y >= 30:
                    # thighs and hands: mech joints (dark) with metal highlights
                    out[y][x] = hx('#4A3E58') if c == SKIN else hx('#36283B')
                    if c == SKIN and (x + y) % 4 == 0:
                        out[y][x] = METAL[1]
    if spec['robot']:
        # glowing seam and core on the breastplate
        for y in range(31, 36):
            if out[y][29] in (METAL[3], METAL[2]):
                out[y][29] = glow
        for x, y in ((28, 33), (29, 33), (30, 33)):
            if out[y][x] is not None:
                out[y][x] = glow
        # ear module with a light, on the right side of the head
        for x, y in ((38, 20), (39, 20), (38, 21), (39, 21), (38, 22), (39, 22)):
            out[y][x] = METAL[2] if (x, y) != (39, 21) else glow
        out[19][38], out[19][39], out[23][38], out[23][39] = OUTLINE, OUTLINE, OUTLINE, OUTLINE
        out[20][40] = out[21][40] = out[22][40] = OUTLINE
        # a glowing line down each thigh joint
        for x, y in ((22, 46), (22, 47), (33, 46), (33, 47)):
            if out[y][x] is not None:
                out[y][x] = dim
    return out


def back_view(px, w, h, spec):
    """From behind: the face, eyes and ear module inside the face box become hair: a mid tone with lighter strand
    lines every third column and a darker band along the bottom edge."""
    out = [row[:] for row in px]
    hair = spec['hair']
    for y in range(FACE[1], FACE[3]):
        for x in range(FACE[0], FACE[2]):
            if out[y][x] is not None and out[y][x] != OUTLINE:
                c = hair[3] if x % 3 == 0 else hair[2]
                if y >= FACE[3] - 3:
                    c = hair[1]
                out[y][x] = c
    return out


def region(x, y):
    if y >= 50:
        return 'll' if x < 27 else 'lr'
    if x >= 37 and 36 <= y < 48:
        return 'sword'
    if x <= 18 and 32 <= y < 51:
        return 'cape'
    return 'body'


def compose_layers(px, w, h, offs, orb=None, sword=True):
    fr = [[None] * F for _ in range(F)]
    oy = GROUND - (h - 1)
    for layer in ('cape', 'll', 'lr', 'body') + (('sword',) if sword else ()):
        dx, dy = offs.get(layer, (0, 0))
        for y in range(h):
            for x in range(w):
                c = px[y][x]
                if c is not None and region(x, y) == layer:
                    X, Y = OX + x + dx, oy + y + dy
                    if 0 <= X < F and 0 <= Y < F:
                        fr[Y][X] = c
    if orb:
        cx, cy, r, colour = orb
        cx, cy = OX + cx, oy + cy
        for y in range(cy - r - 1, cy + r + 2):
            for x in range(cx - r - 1, cx + r + 2):
                if not (0 <= x < F and 0 <= y < F):
                    continue
                d = math.hypot(x - cx, y - cy)
                if d <= r:
                    fr[y][x] = (255, 255, 255) if d < r * 0.45 else colour
                elif d <= r + 1:
                    fr[y][x] = OUTLINE
    return fr


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
# Offsets for the art as drawn (facing right: forward is +x). Left-facing frames are mirrors.

def idle(i):
    return {'body': (0, i), 'sword': (0, i), 'cape': (-i, i)}


def walk(i, side):
    if i == 0:
        return {}
    t = (i - 1) / 8.0 * 2 * math.pi
    s = math.sin(t)
    lift_l, lift_r = max(0, round(2 * s)), max(0, round(-2 * s))
    stride = 2 if side else 0
    bob = 1 if abs(s) < 0.5 else 0
    return {'body': (0, bob), 'sword': (0, bob), 'cape': (-1 + round(-s), bob),
            'll': (round(stride * s), -lift_l), 'lr': (-round(stride * s), -lift_r)}


SLASH = [((-1, -3), 0), ((-2, -5), -1), ((2, -2), 1), ((3, 0), 2), ((2, 1), 1), ((1, 0), 0)]  # the sword stays in hand


def slash(i):
    (dx, dy), lean = SLASH[i]
    return {'body': (lean, 0), 'cape': (lean - 1, 0), 'sword': (dx + lean, dy)}


CAST = [(0, 0), (1, 1), (2, 2), (2, 3), (2, 3), (1, 2), (0, 0)]


def cast(i):
    dy, r = CAST[i]
    return {'sword': (-1, dy), 'cape': (-dy, -dy // 2)}, r


HURT = [{'body': (-1, 0), 'sword': (-1, 0), 'cape': (-2, 0)},
        {'body': (-2, 1), 'sword': (-2, 2), 'cape': (-3, 1)},
        {'body': (-1, 3), 'sword': (-1, 4), 'cape': (-2, 3), 'll': (0, 1), 'lr': (0, 1)},
        {'body': (0, 5), 'sword': (0, 6), 'cape': (-1, 5), 'll': (0, 2), 'lr': (0, 2)}]


def frames_for(name):
    w, h, base = load()
    spec = SPECS[name]
    px = dress(base, w, h, spec)
    back = back_view(px, w, h, spec)
    orb_at = (45, 30)
    armed = spec['robot']     # NPCs don't carry the sword

    def compose(src, w_, h_, offs, orb=None):
        return compose_layers(src, w_, h_, offs, orb, sword=armed)

    def row(anim, facing):
        src = back if facing == UP else px
        side = facing in (LEFT, RIGHT)
        if anim == 'idle':
            out = [compose(src, w, h, idle(i)) for i in range(2)]
        elif anim == 'walk':
            out = [compose(src, w, h, walk(i, side)) for i in range(9)]
        elif anim == 'slash':
            out = [compose(src, w, h, slash(i)) for i in range(6)]
        else:
            out = []
            for i in range(7):
                offs, r = cast(i)
                out.append(compose(src, w, h, offs, (orb_at[0], orb_at[1], r, spec['glow']) if r and facing != UP else None))
        return [mirror(f) for f in out] if facing == LEFT else out

    sheets = {anim: [row(anim, d) for d in (UP, LEFT, DOWN, RIGHT)] for anim in ('idle', 'walk', 'slash', 'spellcast')}
    hurt = [compose(px, w, h, HURT[i]) for i in range(4)]
    down = compose(px, w, h, {'body': (0, 2), 'sword': (0, 3)})
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
        f.write('# %s: sprite credits\n\nAnimated by `Tools/ChibiForge/robot_model.py` from `Tools/ChibiForge/reference/'
                'Robot.png` (the user\'s reference sprite; see that folder\'s README for its rights note), recoloured '
                'per character. Native 64 px frames.\n' % name)
    print('built', name, '(robot model)')


def contact(path, names, scale=4):
    cells = []
    for n in names:
        sh = frames_for(n)
        cells.append([sh['walk'][DOWN][0], sh['walk'][LEFT][2], sh['walk'][RIGHT][6], sh['walk'][UP][0],
                      sh['slash'][RIGHT][3], sh['spellcast'][RIGHT][3], sh['hurt'][0][5]])
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
