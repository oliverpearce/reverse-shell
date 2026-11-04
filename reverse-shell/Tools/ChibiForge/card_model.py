"""
Card model: the crew's overworld and battle sprites are their card art (reference/A3.png, 48 x 63, recoloured per
member by chibi_forge.reference_pixels), animated by layers at native 64 px.

    python Tools/ChibiForge/card_model.py [A3 Ashe Lucy] [--sheet out.png]

The sword is taken out (SWORD = False): its blade pixels in the top-left are cleared, leaving the raised gauntlet.
Layers on the art: the far leg (x >= 30, from row 45), the near leg (x <= 29, from row 46) and the body. A moved
leg fills the gap it opens at the hip with its own source pixels.
    idle 8      the upper body sinks a pixel and rises (a breath)
    walk 13     frame 0 stands; 1-12 swing the legs side to side in turn, lifting the stepping foot, with a bob
    slash 10    a lunge forward with an arc in the member's glow colour
    spellcast 12  an orb grows in front of the chest
    hurt 9      recoil, sink, then lie on the ground
Directions: the art is a three-quarter view facing the viewer's right. Down and right use it as drawn, left is the
mirror, and up is a repainted back view (back_view): the whole head is hair, a long hair cascade runs down the
back to the waist, and the front details (gold trim, gems, skin) on the armour become plain back plates.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'SpriteForge'))
sys.path.insert(0, HERE)
from pngtool import write_png  # noqa: E402
import chibi_forge as cf  # noqa: E402

F = 64
GROUND = 62                       # the feet's lowest row (the art is 63 tall)
CHARS = cf.CHARS
UP, LEFT, DOWN, RIGHT = 0, 1, 2, 3
OUTLINE = (0x20, 0x02, 0x1D)
SWORD = False                     # True keeps the card's greatsword

GOLD = {cf.hx(h)[:3] for h in ('#B35420', '#E2BA4B', '#D2884A', '#894B32')}
FACE = (21, 17, 40, 31)           # x0, y0, x1, y1 (exclusive): the face inside the hair
SPLIT = 29                        # legs: near (x <= SPLIT) and far
MANE = {cf.hx(h)[:3] for h in ('#391C2F', '#472D38')}   # the card's dark back hair


def available(name):
    return name in cf.PARTY and cf.reference_pixels(name) is not None


def load(name):
    w, h, px = cf.reference_pixels(name)
    grid = [[p[:3] if p[3] else None for p in row] for row in px]
    if not SWORD:
        # the blade: gold / orange pixels left of the hand (and the hilt beside it); the gold crest on the head
        # (x >= 21) stays
        gold = {c for c in GOLD}
        if name != 'A3':
            gold = {tuple(cf.SPECS[name].trim[i][:3]) for i in range(4)}
        for y in range(0, 29):
            for x in range(0, 22):
                c = grid[y][x]
                if c in gold and (x <= 17 or 17 <= y <= 27) and not (x >= 21 and y <= 3):
                    grid[y][x] = None
        # stray outline / shadow pixels the blade left floating
        for y in range(0, 29):
            for x in range(0, 18):
                if grid[y][x] is not None and sum(1 for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
                                                  if 0 <= x + dx < w and 0 <= y + dy < h
                                                  and grid[y + dy][x + dx] is not None) <= 1:
                    grid[y][x] = None
    return w, h, grid


def back_view(grid, name):
    """The view from behind, repainted on the art's silhouette:
    - the head (rows up to 30) is all hair: a mid tone with light strands that wave a little, darker separations,
      a dark rim along the silhouette and a shine across the crown; the gold crest and the raised gauntlet beside
      the head stay as drawn, and the dark mane behind the left shoulder takes the hair colour;
    - a long cascade of the same hair runs down the back to the waist, ending in uneven tips;
    - elsewhere the front details (gold / orange trim, the crimson gems, skin) become back plates: the armour ramp
      picked by brightness. Outlines and the slate greaves stay."""
    spec = cf.SPECS[name]
    hair = [c[:3] for c in spec.hair]
    armor = [c[:3] for c in spec.armor]
    h, w = len(grid), len(grid[0])
    front_bits = {c[:3] for c in (spec.trim if name != 'A3' else cf.REF_GOLD)}
    front_bits |= {cf.REF_CRIMSON[1][:3], spec.accent[1][:3]} | {c[:3] for c in cf.REF_SKIN}
    cx = 31

    def opaque(x, y):
        return 0 <= x < w and 0 <= y < h and grid[y][x] is not None

    def in_hair(x, y):
        if y <= 30:
            return True
        half = 7 - (y - 31) * 0.2
        tip = 42 + (x * 7) % 4
        return y <= tip and abs(x - cx) <= half

    def keep(x, y):
        return (y <= 3 and grid[y][x] in GOLD) or (16 <= x <= 24 and 11 <= y <= 27)

    def hair_at(x, y):
        rim = any(not opaque(x + dx, y + dy) or grid[y + dy][x + dx] == OUTLINE or not in_hair(x + dx, y + dy)
                  for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        if rim:
            return hair[1]
        wave = int(round(math.sin(y / 5.0) * 1.2))
        k = (x + wave) % 5
        if 7 <= y <= 8 and cx - 7 <= x <= cx + 5 and (x + y) % 3:
            return hair[3]                       # the crown's shine
        if k == 0:
            return hair[3] if y < 26 else hair[2]
        if k == 3:
            return hair[1]
        return hair[2]

    out = [row[:] for row in grid]
    for y in range(h):
        for x in range(w):
            c = grid[y][x]
            if c is None or keep(x, y):
                continue
            edge = any(not opaque(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if c == OUTLINE and (edge or (y > 30 and not in_hair(x, y))):
                continue                         # silhouette and armour outlines stay
            if in_hair(x, y):
                out[y][x] = hair_at(x, y)
            elif x <= 17 and 25 <= y <= 41 and c in MANE:
                out[y][x] = hair[1] if (x + y) % 4 else hair[2]   # the long hair falling behind the left shoulder
            elif c in front_bits:
                lum = 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]
                out[y][x] = armor[min(3, max(0, int((lum - 60) / 45)))]
    return out


def region(x, y):
    if y >= 46 and x <= SPLIT:
        return 'near'
    if y >= 45 and x > SPLIT:
        return 'far'
    return 'body'


ORDER = ('far', 'near', 'body')


def compose(grid, w, h, offs, fx=None, upper_dy=0):
    """A 64 px frame from layer offsets {layer: (dx, dy)}; upper_dy shifts the rows above the waist (breathing);
    fx(frame, ox, oy) draws effects on top."""
    fr = [[None] * F for _ in range(F)]
    ox = (F - w) // 2
    oy = GROUND - (h - 1)

    def put(X, Y, c):
        if 0 <= X < F and 0 <= Y < F:
            fr[Y][X] = c

    for layer in ORDER:
        dx, dy = offs.get(layer, (0, 0))
        cells = [(x, y, grid[y][x]) for y in range(h) for x in range(w) if grid[y][x] is not None and region(x, y) == layer]
        if layer != 'body' and (dx or dy):
            top = min(y for _, y, _ in cells)
            for x, y, c in cells:       # the hip seam keeps its source pixels, so no gap opens
                if y < top + 4:
                    put(ox + x, oy + y, c)
        for x, y, c in cells:
            extra = upper_dy if (layer == 'body' and y < 40) else 0
            put(ox + x + dx, oy + y + dy + extra, c)
    if fx:
        fx(fr, ox, oy)
    return fr


def arc(fr, cx, cy, r, ramp_):
    """A slash crescent to the right of (cx, cy) in the glow ramp, white-hot inside."""
    for a in range(-70, 71, 5):
        t = math.radians(a)
        for rr, c in ((r, ramp_[1]), (r - 1, ramp_[2]), (r - 2, ramp_[3])):
            x, y = cx + int(round(math.cos(t) * rr)), cy + int(round(math.sin(t) * rr))
            if 0 <= x < F and 0 <= y < F and (rr == r or abs(a) < 55):
                fr[y][x] = c


def orb(fr, cx, cy, r, ramp_):
    for y in range(cy - r - 1, cy + r + 2):
        for x in range(cx - r - 1, cx + r + 2):
            if not (0 <= x < F and 0 <= y < F):
                continue
            d = math.hypot(x - cx, y - cy)
            if d <= r:
                fr[y][x] = ramp_[3] if d < r * 0.45 else ramp_[2] if d < r * 0.8 else ramp_[1]
            elif d <= r + 1:
                fr[y][x] = OUTLINE


def mirror(fr):
    return [row[::-1] for row in fr]


def lying(fr, lift=0):
    rot = [[None] * F for _ in range(F)]
    for y in range(F):
        for x in range(F):
            rot[x][F - 1 - y] = fr[y][x]
    lowest = max((y for y in range(F) for x in range(F) if rot[y][x] is not None), default=GROUND)
    out = [[None] * F for _ in range(F)]
    shift = GROUND - lowest - lift
    for y in range(F):
        for x in range(F):
            if rot[y][x] is not None and 0 <= y + shift < F:
                out[y + shift][x] = rot[y][x]
    return out


def ease(t):
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------------------------------------------- poses

def walk(i):
    """Frame 0 stands; 1-12 are one stride: the legs swing side to side against each other, the stepping foot
    lifts a pixel, and the body bobs as the legs pass."""
    if i == 0:
        return {}, 0
    t = (i - 1) / 12.0 * 2 * math.pi
    s = math.sin(t)
    swing = int(round(2 * s))
    lift_n, lift_f = (-1 if s > 0.5 else 0), (-1 if s < -0.5 else 0)
    bob = 1 if abs(s) < 0.5 else 0
    return {'near': (swing, lift_n), 'far': (-swing, lift_f), 'body': (0, bob)}, 0


def idle(i):
    return {}, int(round(0.5 - 0.5 * math.cos(i / 8.0 * 2 * math.pi)))


SLASH_LEAN = [0, -1, -1, 0, 2, 3, 3, 2, 1, 0]   # wind back, lunge, recover (forward is +x)


def frames_for(name):
    w, h, grid = load(name)
    back = back_view(grid, name)
    spec = cf.SPECS[name]
    hi = [c[:3] for c in (spec.hi or [spec.glow] * 3 + [cf.WHITE])]

    def row(anim, facing):
        src = back if facing == UP else grid
        out = []
        if anim == 'idle':
            for i in range(8):
                offs, up = idle(i)
                out.append(compose(src, w, h, offs, upper_dy=up))
        elif anim == 'walk':
            for i in range(13):
                offs, _ = walk(i)
                out.append(compose(src, w, h, offs))
        elif anim == 'slash':
            for i, lean in enumerate(SLASH_LEAN):
                fx = None
                if 4 <= i <= 6 and facing != UP:
                    fx = (lambda k: lambda fr, ox, oy: arc(fr, ox + w - 6 + k, oy + 30, 11 - (6 - k), hi))(i)
                out.append(compose(src, w, h, {'body': (lean, 0), 'near': (lean // 2, 0)}, fx=fx))
        else:
            radii = [0, 1, 2, 3, 4, 4, 4, 4, 3, 2, 1, 0]
            for i, r in enumerate(radii):
                fx = None
                if r and facing != UP:
                    fx = (lambda rr: lambda fr, ox, oy: orb(fr, ox + w - 4, oy + 34, rr, hi))(r)
                out.append(compose(src, w, h, {}, fx=fx, upper_dy=1 if r >= 3 else 0))
        return [mirror(f) for f in out] if facing == LEFT else out

    sheets = {a: [row(a, d) for d in (UP, LEFT, DOWN, RIGHT)] for a in ('idle', 'walk', 'slash', 'spellcast')}
    hurt = []
    for i in range(6):
        t = ease(i / 5.0)
        lean = -int(round(2 * math.sin(t * math.pi)))
        sink = int(round(4 * t))
        hurt.append(compose(grid, w, h, {'body': (lean, sink), 'near': (0, sink // 2), 'far': (0, sink // 2)}))
    flat = compose(grid, w, h, {})
    sheets['hurt'] = [hurt + [lying(flat, 4), lying(flat, 1), lying(flat)]]
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
        f.write('# %s: sprite credits\n\nAnimated by `Tools/ChibiForge/card_model.py` from the card art '
                '`Tools/ChibiForge/reference/A3.png` (the user\'s reference sprite; see that folder\'s README for its '
                'rights note), recoloured per character. Native 64 px frames.\n' % name)
    print('built', name, '(card model)')


def contact(path, names, scale=4):
    cells = []
    for n in names:
        sh = frames_for(n)
        cells.append([sh['idle'][DOWN][0], sh['walk'][DOWN][2], sh['walk'][DOWN][5], sh['walk'][DOWN][8],
                      sh['walk'][LEFT][4], sh['walk'][UP][3], sh['slash'][RIGHT][5], sh['spellcast'][DOWN][5],
                      sh['hurt'][0][8]])
    W, H = len(cells[0]) * F, len(cells) * F
    bg = (60, 72, 50, 255)
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
    names = args or list(cf.PARTY)
    if sheet:
        contact(sheet, names)
    else:
        for n in names:
            build(n)
