"""
Base model: animates the crew from one reference sprite (reference/Base.png, a 74 x 112 girl with a mech backpack
and two big mechanical arms) by cutting it into layers and moving them per frame.

    python Tools/ChibiForge/base_model.py [MC Ashe Lucy] [--sheet out.png]

Layers (native pixel boxes on Base.png): the left mech arm (x < 26, below the shoulders), the right mech arm (x > 46),
the two legs (split at x 36, below the shorts) and the body (head, torso, gloves, backpack, shorts).
Animations, in the same sheet layout as every other character (rows up, left, down, right; hurt one row):
    idle 2      breathing: the body dips while the mech arms float
    walk 9      frame 0 stands; legs step (lifted foot, stride in side views), body bob, mech arms sway
    slash 6     the leading mech arm winds up, swipes forward and recovers (the claws are the weapon)
    spellcast 7 both mech arms rise while an orb grows in front of the chest
    hurt 6      recoil, sink, then lie on the ground
Directions: down and left use the art as drawn (a three-quarter view facing left), right is the mirror, and up is
a back view (the face and eyes painted over with hair).
Frames are 128 px at the art's full detail (the rig draws the machines at 2x into the same 128 px frames, so every
sprite shares one pixel density), with the darks lifted a little so they read in the dim overworld. Setting OUT to
64 halves them instead (2 x 2 majority, ties to the darker colour).
The crew are recolours (hair, eyes and shorts hue-shifted); the NPCs use the same body without the mech arms, with
their own hair, shirt and shorts colours, so every human sprite shares one style.
"""
import colorsys
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'SpriteForge'))
from pngtool import read_png, write_png  # noqa: E402

F = 128
GROUND = 122                     # the feet's lowest row in every frame (feet pivot 6 / 128 = 0.047)
CHARS = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')
BASE = os.path.join(HERE, 'reference', 'Base.png')
UP, LEFT, DOWN, RIGHT = 0, 1, 2, 3

HEAD_BOX = (24, 0, 48, 23)       # x0, y0, x1, y1 (exclusive) on Base.png
HAIR_BOXES = [(24, 0, 46, 14), (24, 14, 40, 23)]   # the head without the backpack's top (x >= 40 below y 14)
SHORTS_BOX = (24, 45, 46, 61)
EYE_BLUE = (0x62, 0x7B, 0x9D)

# Recolours: target hue (degrees) for hair, eyes and shorts, and the spell / glow colour.
CREW = {
    'MC': {'hue': None, 'glow': (240, 246, 255)},
    'Ashe': {'hue': 115, 'glow': (142, 240, 112), 'sat': 1.5},
    'Lucy': {'hue': 285, 'glow': (208, 122, 255), 'sat': 1.5},
}
# Ruin survivors: the same body without the mech backpack. hair_sat / hair_lift tune the near-black hair,
# top tints the white crop top.
PEOPLE = {
    'NoodleVendor': {'hue': 25, 'sat': 1.8, 'hair_lift': 2.2, 'shorts_hue': 0, 'top': (1.0, 0.9, 0.78),
                     'glow': (255, 220, 160), 'mech': False},
    'Courier': {'hue': 30, 'sat': 1.4, 'hair_lift': 1.6, 'shorts_hue': 215, 'top': (0.72, 0.84, 1.0),
                'glow': (200, 230, 255), 'mech': False},
    'Drifter': {'hue': 220, 'sat': 0.0, 'hair_lift': 2.0, 'shorts_hue': 95, 'top': (0.74, 0.82, 0.66),
                'glow': (220, 220, 220), 'mech': False},
}
ALL = dict(CREW, **PEOPLE)
OUT = 128                        # final frame size: full detail (64 halves it, for a lower pixel density)
TOP_BOX = (24, 20, 46, 46)
BACKPACK_BOX = (39, 10, 53, 38)   # the mech backpack behind the right shoulder (removed for NPCs)


def strip_backpack(px, w, h):
    """NPCs have no mech gear: the backpack's blue-grey plating becomes transparent (skin, hair and the black
    glove in front of it stay)."""
    out = [row[:] for row in px]
    for y in range(BACKPACK_BOX[1], BACKPACK_BOX[3]):
        for x in range(BACKPACK_BOX[0], min(w, BACKPACK_BOX[2])):
            p = px[y][x]
            if not p[3]:
                continue
            hh, ss, vv = colorsys.rgb_to_hsv(p[0] / 255.0, p[1] / 255.0, p[2] / 255.0)
            if 0.5 <= hh <= 0.72 and 0.08 <= ss <= 0.45 and vv >= 0.3:
                out[y][x] = (0, 0, 0, 0)
    return out


def load():
    w, h, px = read_png(BASE)
    # the ground shadow streak under the feet
    for y in range(104, h):
        for x in list(range(15, 29)) + list(range(47, 55)):
            px[y][x] = (0, 0, 0, 0)
    return w, h, px


def region(x, y):
    if x < 26 and y >= 38:
        return 'ml'
    if x > 46 and y >= 38:
        return 'mr'
    if y >= 62 and 26 <= x < 36:
        return 'll'
    if y >= 62 and 36 <= x <= 46:
        return 'lr'
    return 'body'


def recolour(px, w, h, spec):
    """Hue-shifts the bluish hair, eyes and shorts to the member's colour (mech arms and skin are untouched)."""
    if spec['hue'] is None and 'top' not in spec:
        return px
    out = [row[:] for row in px]
    boost = spec.get('sat', 1.0)
    top = spec.get('top')
    for y in range(h):
        for x in range(w):
            p = px[y][x]
            if not p[3]:
                continue
            in_head = any(b[0] <= x < b[2] and b[1] <= y < b[3] for b in HAIR_BOXES)
            in_shorts = SHORTS_BOX[0] <= x < SHORTS_BOX[2] and SHORTS_BOX[1] <= y < SHORTS_BOX[3]
            in_top = TOP_BOX[0] <= x < TOP_BOX[2] and TOP_BOX[1] <= y < TOP_BOX[3]
            r, g, b = p[0] / 255.0, p[1] / 255.0, p[2] / 255.0
            hh, ss, vv = colorsys.rgb_to_hsv(r, g, b)
            if top and in_top and ss < 0.1 and vv > 0.72:     # the white crop top
                out[y][x] = (int(p[0] * top[0]), int(p[1] * top[1]), int(p[2] * top[2]), p[3])
                continue
            if spec['hue'] is None or not (in_head or in_shorts):
                continue
            if ss < 0.12 or not (0.5 <= hh <= 0.75) or vv < 0.08:
                continue
            hue = spec.get('shorts_hue', spec['hue']) if in_shorts and not in_head else spec['hue']
            lift = spec.get('hair_lift', 1.25) if in_head else 1.0   # near-black hair needs light to show its colour
            nr, ng, nb = colorsys.hsv_to_rgb(hue / 360.0, min(1.0, ss * boost), min(1.0, vv * lift))
            out[y][x] = (int(nr * 255), int(ng * 255), int(nb * 255), p[3])
    return out


def back_view(px, w, h):
    """Seen from behind: the face and eyes in the head box become hair (dark and mid hair tones alternate)."""
    out = [row[:] for row in px]
    hair_dark, hair_mid = None, None
    for y in range(HEAD_BOX[1], HEAD_BOX[3]):
        for x in range(HEAD_BOX[0], HEAD_BOX[2]):
            p = px[y][x]
            if p[3] and sum(p[:3]) < 180:
                hair_dark = hair_dark or p
            elif p[3] and sum(p[:3]) < 330:
                hair_mid = hair_mid or p
    hair_dark = hair_dark or (40, 46, 69, 255)
    hair_mid = hair_mid or (61, 74, 101, 255)
    for y in range(6, HEAD_BOX[3]):
        for x in range(HEAD_BOX[0], HEAD_BOX[2]):
            p = px[y][x]
            if p[3] and sum(p[:3]) > 330:   # skin and eye whites
                out[y][x] = hair_mid if (x + y) % 3 else hair_dark
            elif p[3] and p[:3] == EYE_BLUE:
                out[y][x] = hair_mid
    return out


class Frame:
    def __init__(self):
        self.px = [[(0, 0, 0, 0)] * F for _ in range(F)]

    def put(self, x, y, c):
        if 0 <= x < F and 0 <= y < F and c[3]:
            self.px[y][x] = c


def compose(px, w, h, offs, orb=None, mech=True):
    """Draws the layers with their offsets (dx, dy); back to front: mech arms, legs, body."""
    fr = Frame()
    ox, oy = F // 2 - w // 2, GROUND - (h - 1)
    for layer in (('ml', 'mr') if mech else ()) + ('ll', 'lr', 'body'):
        dx, dy = offs.get(layer, (0, 0))
        for y in range(h):
            for x in range(w):
                if region(x, y) == layer:
                    fr.put(ox + x + dx, oy + y + dy, px[y][x])
    if orb:
        cx, cy, r, colour = orb
        cx, cy = ox + cx, oy + cy
        for y in range(cy - r - 1, cy + r + 2):
            for x in range(cx - r - 1, cx + r + 2):
                d = math.hypot(x - cx, y - cy)
                if d <= r:
                    fr.put(x, y, (255, 255, 255, 255) if d < r * 0.45 else colour + (255,))
                elif d <= r + 1:
                    fr.put(x, y, (32, 2, 29, 255))
    return fr


def halve(fr):
    """The final frame: kept at 128 px, or (OUT = 64) halved, each 2 x 2 block keeping its most common colour
    (ties: the darkest), empty if mostly empty. Darks are lifted slightly (gamma 0.85) either way, so the sprite
    reads in the dim overworld."""
    out = [[(0, 0, 0, 0)] * OUT for _ in range(OUT)]
    for y in range(OUT):
        for x in range(OUT):
            if F // OUT == 1:
                c = fr.px[y][x]
                if c[3]:
                    out[y][x] = tuple(int(255 * (v / 255.0) ** 0.85) for v in c[:3]) + (255,)
                continue
            block = [fr.px[2 * y + dy][2 * x + dx] for dy in (0, 1) for dx in (0, 1)]
            solid = [c for c in block if c[3]]
            if len(solid) < 2:
                continue
            counts = {}
            for c in solid:
                counts[c] = counts.get(c, 0) + 1
            best = max(counts.items(), key=lambda kv: (kv[1], -sum(kv[0][:3])))[0]
            out[y][x] = tuple(int(255 * (v / 255.0) ** 0.85) for v in best[:3]) + (255,)
    return out


def mirror(fr):
    out = Frame()
    for y in range(F):
        out.px[y] = fr.px[y][::-1]
    return out


def lying(fr):
    """Rotated 90 degrees clockwise and dropped so the body rests on the ground line."""
    rot = Frame()
    for y in range(F):
        for x in range(F):
            rot.px[x][F - 1 - y] = fr.px[y][x]
    lowest = max((y for y in range(F) for x in range(F) if rot.px[y][x][3]), default=GROUND)
    out = Frame()
    shift = GROUND - lowest
    for y in range(F):
        for x in range(F):
            if rot.px[y][x][3] and 0 <= y + shift < F:
                out.px[y + shift][x] = rot.px[y][x]
    return out


# ---------------------------------------------------------------------------------------------------- poses
# Offsets are for a left-facing (or front) frame; "forward" is -x. Right-facing frames are mirrors.

def idle(i):
    return {'body': (0, i), 'll': (0, 0), 'lr': (0, 0), 'ml': (0, -i), 'mr': (0, -i)}


def walk(i, side):
    if i == 0:
        return {}
    t = (i - 1) / 8.0 * 2 * math.pi
    s, c = math.sin(t), math.cos(t)
    lift_l, lift_r = max(0, round(3 * s)), max(0, round(-3 * s))
    stride = 4 if side else 1   # side views stride; front / back views step with a small sway
    bob = 1 if abs(s) < 0.5 else 0
    return {'body': (0, bob),
            'll': (-round(stride * s), -lift_l + bob // 2),
            'lr': (round(stride * s), -lift_r + bob // 2),
            'ml': (round(1 * c), round(1.5 * s) + bob),
            'mr': (-round(1 * c), -round(1.5 * s) + bob)}


SLASH = [((1, -3), 0), ((2, -6), 1), ((-4, -2), -1), ((-10, 1), -2), ((-6, 1), -1), ((-2, 0), 0)]


def slash(i, lead):
    (dx, dy), lean = SLASH[i]
    offs = {'body': (lean, 0)}
    offs[lead] = (dx, dy)
    return offs


CAST = [(-1, 0), (-3, 1), (-5, 2), (-6, 3), (-6, 3), (-4, 2), (-1, 0)]


def cast(i):
    dy, r = CAST[i]
    return {'ml': (1, dy), 'mr': (-1, dy), 'body': (0, 0)}, r


HURT = [{'body': (1, 0)}, {'body': (2, 1), 'ml': (1, 2), 'mr': (1, 2)},
        {'body': (1, 3), 'ml': (0, 4), 'mr': (0, 4), 'll': (0, 1), 'lr': (0, 1)},
        {'body': (0, 6), 'ml': (0, 6), 'mr': (0, 6), 'll': (0, 2), 'lr': (0, 2)}]


# ---------------------------------------------------------------------------------------------------- sheets

def frames_for(name):
    w, h, base = load()
    spec = ALL[name]
    mech = spec.get('mech', True)
    if not mech:
        base = strip_backpack(base, w, h)
    px = recolour(base, w, h, spec)
    back = back_view(px, w, h)
    glow = spec['glow']
    chest = (w // 2 - 8, 38)   # in front of the chest (three-quarter view, slightly left)

    def row_frames(anim, facing):
        src = back if facing == UP else px
        side = facing in (LEFT, RIGHT)
        out = []
        if anim == 'idle':
            out = [compose(src, w, h, idle(i), mech=mech) for i in range(2)]
        elif anim == 'walk':
            out = [compose(src, w, h, walk(i, side), mech=mech) for i in range(9)]
        elif anim == 'slash':
            lead = 'mr' if facing == UP else 'ml'
            out = [compose(src, w, h, slash(i, lead), mech=mech) for i in range(6)]
        elif anim == 'spellcast':
            for i in range(7):
                offs, r = cast(i)
                out.append(compose(src, w, h, offs, (chest[0], chest[1], r, glow) if r and facing != UP else None, mech=mech))
        if facing == RIGHT:
            out = [mirror(f) for f in out]
        return out

    sheets = {}
    for anim, count in (('idle', 2), ('walk', 9), ('slash', 6), ('spellcast', 7)):
        sheets[anim] = [row_frames(anim, d) for d in (UP, LEFT, DOWN, RIGHT)]
    hurt = [compose(px, w, h, HURT[i], mech=mech) for i in range(4)]
    down = compose(px, w, h, {'body': (0, 2), 'ml': (0, 3), 'mr': (0, 3)}, mech=mech)
    hurt += [lying(down), lying(down)]
    sheets['hurt'] = [hurt]
    return sheets


def build(name):
    out_dir = os.path.join(CHARS, name)
    os.makedirs(out_dir, exist_ok=True)
    sheets = frames_for(name)
    for anim, rows in sheets.items():
        cols = len(rows[0])
        img = [[(0, 0, 0, 0)] * (OUT * cols) for _ in range(OUT * len(rows))]
        for r, frames in enumerate(rows):
            for c, fr in enumerate(frames):
                small = halve(fr)
                for y in range(OUT):
                    img[r * OUT + y][c * OUT:(c + 1) * OUT] = small[y]
        write_png(os.path.join(out_dir, anim + '.png'), OUT * cols, OUT * len(rows), img)
    with open(os.path.join(out_dir, 'CREDITS.md'), 'w', encoding='utf-8') as f:
        f.write('# %s: sprite credits\n\nAnimated by `Tools/ChibiForge/base_model.py` from the base model '
                '`Tools/ChibiForge/reference/Base.png` (the user\'s reference art; see that folder\'s README for '
                'its rights note), recoloured per character. 128 px frames.\n' % name)
    print('built', name, '(base model)')
    return sheets


def contact(path, names, scale=3):
    rows = []
    for n in names:
        sh = frames_for(n)
        rows.append([sh['walk'][DOWN][0], sh['walk'][LEFT][2], sh['walk'][RIGHT][6], sh['walk'][UP][0],
                     sh['slash'][LEFT][3], sh['spellcast'][RIGHT][3], sh['hurt'][0][5]])
    W, H = len(rows[0]) * OUT, len(rows) * OUT
    bg = (52, 50, 60, 255)
    img = [[bg] * W for _ in range(H)]
    for r, fr_row in enumerate(rows):
        for c, fr in enumerate(fr_row):
            small = halve(fr)
            for y in range(OUT):
                for x in range(OUT):
                    p = small[y][x]
                    if p[3]:
                        img[r * OUT + y][c * OUT + x] = p
    big = [[img[y // scale][x // scale] for x in range(W * scale)] for y in range(H * scale)]
    write_png(path, W * scale, H * scale, big)


if __name__ == '__main__':
    args = sys.argv[1:]
    sheet = None
    if '--sheet' in args:
        i = args.index('--sheet')
        sheet = args[i + 1]
        del args[i:i + 2]
    names = args or list(ALL)
    if sheet:
        contact(sheet, names)
    else:
        for n in names:
            build(n)
