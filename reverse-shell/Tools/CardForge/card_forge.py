"""
CardForge: paints the pixel-art card pieces (48 x 64 px cards, shown at 4x in the hand), in the style of the user's
"Soul" reference card: a pale rim with a plum outline, a wavy swirl background, rank pips along the top and a name
plate at the bottom.

    python Tools/CardForge/card_forge.py [--preview out.png]

Writes into Assets/_Project/Resources/Cards/ (imported point-filtered by PixelArtImporter):
    CardSwirl.png   48x64  the swirl background in greys; the game tints it with the card owner's colour
    CardFrame.png   48x64  rim, pip strip and name plate, drawn over the swirl (never tinted)
    CardPips.png    10x5   a lit and an unlit rank pip (5x5 diamonds)
    CardTypes.png   42x7   the six card-type icons (Attack, Debuff, Buff, Counter, Heal, Ultimate), 7x7 each, drawn
                           at the card's own pixel size so they sit in the pip strip as crisply as the pips
    CardArt.png     32 px cells, one centre icon per card; CardArt.txt lists "cell asset_name" (read by CardArt.cs)
    Ult_<Char>.png  48x64  full-art Ult cards: a 2x bust of the character's battle sprite over their own tinted swirl

Layout (card pixels, y from the top) is mirrored in CardArt.cs: interior 4..43 x 4..59, pip strip 4..12 with pips at
x 6/12/18, y 6 and the type icon at 35..41 x 5..11,
art window 8..39 x 13..44, name plate 6..41 x 51..59.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'SpriteForge'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'ChibiForge'))
from pngtool import write_png  # noqa: E402

OUT = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Cards')
W, H = 48, 64
CELL = 32
ICON_CELL = 34   # an icon's 32 px drawing plus 1 px all round, so its plum outline is always whole
OUTLINE = (0x20, 0x02, 0x1D, 255)     # the sprites' plum outline
CLEAR = (0, 0, 0, 0)


def hx(h, a=255):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (a,)


# ---------------------------------------------------------------------------------------------- canvas helpers

class Canvas:
    def __init__(self, w, h, fill=CLEAR):
        self.w, self.h = w, h
        self.px = [[fill] * w for _ in range(h)]

    def put(self, x, y, c):
        x, y = int(x), int(y)
        if 0 <= x < self.w and 0 <= y < self.h and c is not None:
            if len(c) == 4 and c[3] < 255 and self.px[y][x][3] > 0:
                a = c[3] / 255.0
                b = self.px[y][x]
                c = tuple(int(b[i] * (1 - a) + c[i] * a) for i in range(3)) + (255,)
            self.px[y][x] = c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.px[y][x]
        return CLEAR

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def poly(self, pts, c):
        """Scanline-fills a polygon given in pixel-centre coordinates."""
        ys = [p[1] for p in pts]
        for y in range(int(math.floor(min(ys))), int(math.ceil(max(ys))) + 1):
            yc = y + 0.5
            xs = []
            for i in range(len(pts)):
                (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % len(pts)]
                if (y0 <= yc < y1) or (y1 <= yc < y0):
                    xs.append(x0 + (yc - y0) * (x1 - x0) / (y1 - y0))
            xs.sort()
            for i in range(0, len(xs) - 1, 2):
                for x in range(int(math.ceil(xs[i] - 0.5)), int(math.floor(xs[i + 1] - 0.5)) + 1):
                    self.put(x, y, c)

    def disc(self, cx, cy, r, c):
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                    self.put(x, y, c)

    def stroke(self, pts, r, c):
        """A round-capped thick line through the points (dense discs)."""
        for i in range(len(pts) - 1):
            (x0, y0), (x1, y1) = pts[i], pts[i + 1]
            n = max(1, int(math.hypot(x1 - x0, y1 - y0) * 3))
            for k in range(n + 1):
                t = k / n
                self.disc(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, r, c)

    def line(self, x0, y0, x1, y1, c):
        """A 1 px Bresenham line."""
        x0, y0, x1, y1 = int(x0), int(y0), int(x1), int(y1)
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            self.put(x0, y0, c)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy
                x0 += sx
            if e2 <= dx:
                err += dx
                y0 += sy

    def outline(self, c=OUTLINE):
        """A 1 px outline around everything opaque (4-neighbours)."""
        edge = []
        for y in range(self.h):
            for x in range(self.w):
                if self.px[y][x][3] == 0 and any(self.get(x + dx, y + dy)[3] > 0 and self.get(x + dx, y + dy) != c
                                                 for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    edge.append((x, y))
        for x, y in edge:
            self.px[y][x] = c

    def paste(self, other, ox, oy):
        for y in range(other.h):
            for x in range(other.w):
                p = other.px[y][x]
                if p[3] > 0:
                    self.put(ox + x, oy + y, p)

    def rows(self):
        return self.px


def curve(fn, n=24):
    return [fn(i / n) for i in range(n + 1)]


# ---------------------------------------------------------------------------------------------- card pieces

def swirl_value(x, y):
    """Grey level (0..1) of the wavy band pattern at a pixel: diagonal bands bent by two sine waves."""
    t = y * 0.85 + x * 0.35 + 5.0 * math.sin(x / 6.5 + y / 13.0) + 2.2 * math.sin(x / 3.1 - y / 7.0)
    band = t / 4.6
    i = int(math.floor(band)) % 6
    f = band - math.floor(band)
    tones = [0.62, 0.74, 0.86, 0.97, 0.80, 0.68]
    v = tones[i]
    if f < 0.16 and i in (2, 3):
        v = 1.0                                             # thin bright crest lines
    elif f > 0.86 and (x + y) % 2 == 0:
        v = tones[(i + 1) % 6]                              # checker dither into the next band
    return v


def card_mask(x, y):
    """Inside the card's outline (the four corner pixels are cut)."""
    corner = (x in (0, W - 1)) and (y in (0, H - 1))
    return not corner


def make_swirl():
    c = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            if card_mask(x, y):
                g = int(255 * swirl_value(x, y))
                c.px[y][x] = (g, g, g, 255)
    return c


RIM_LIGHT = hx('#F4F2FA')
RIM_MID = hx('#C9C6D8')
RIM_DARK = hx('#8E8AA6')
PLATE = hx('#2A2238')
PLATE_RIM = hx('#B9B5CC')
STRIP = (0x14, 0x0E, 0x1C, 120)


def make_frame():
    c = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            if not card_mask(x, y):
                continue
            d = min(x, y, W - 1 - x, H - 1 - y)
            if d == 0:
                c.px[y][x] = OUTLINE
            elif d in (1, 2):
                # light from the top-left: the bottom and right sides of the rim are a shade darker
                shade = (x >= W - 3 or y >= H - 3)
                c.px[y][x] = RIM_MID if shade else (RIM_LIGHT if d == 1 else RIM_MID)
            elif d == 3:
                c.px[y][x] = OUTLINE
    # rounded inner corners
    for (x, y) in ((1, 1), (W - 2, 1), (1, H - 2), (W - 2, H - 2)):
        c.px[y][x] = OUTLINE
    # pip strip: a dark translucent band under the top rim
    for y in range(4, 13):
        for x in range(4, W - 4):
            c.put(x, y, STRIP)
    for x in range(4, W - 4):
        c.put(x, 13, (0x14, 0x0E, 0x1C, 60))
    # name plate
    x0, x1, y0, y1 = 6, W - 7, 51, 59
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            edge = x in (x0, x1) or y in (y0, y1)
            corner = (x in (x0, x1)) and (y in (y0, y1))
            if corner:
                continue
            c.px[y][x] = PLATE_RIM if edge else PLATE
    for x in range(x0 + 1, x1):
        c.px[y1 + 1][x] = OUTLINE                         # a hard shadow line under the plate
    return c


PIP_ON = [hx('#FFF6C8'), hx('#FFD45A'), hx('#C88A1E')]
PIP_OFF = [hx('#5A5470'), hx('#3A3450'), hx('#241E34')]


def make_pips():
    c = Canvas(10, 5)
    for k, pal in enumerate((PIP_ON, PIP_OFF)):
        ox = k * 5
        for y in range(5):
            for x in range(5):
                d = abs(x - 2) + abs(y - 2)
                if d > 2:
                    continue
                if d == 2:
                    col = OUTLINE
                elif x < 2 or y < 2 or (x == 2 and y == 2):
                    col = pal[0] if (x + y) <= 3 else pal[1]
                else:
                    col = pal[2]
                c.px[y][ox + x] = col
    return c


# The card-type icons, 7x7 at card pixel size (a light / b main / c dark, outlined in plum): the same order as
# CardIcons.Type. Colours follow UnitView.KindColor (Ultimate in gold).
TYPE_SHAPES = [
    ('Attack', (0.84, 0.3, 0.27), [      # a sword, point up-right
        '.......',
        '....aa.',
        '...abb.',
        '.b.bb..',
        '..bb...',
        '.c.b...',
        '.......']),
    ('Debuff', (0.62, 0.46, 0.9), [      # a chunky block arrow down, its own 1 px outline (o) filling the cell
        '.ooooo.',
        '.ocbao.',
        'oocbaoo',
        'ocbbbao',
        '.ocbao.',
        '..oco..',
        '...o...']),
    ('Buff', (0.4, 0.62, 0.95), [        # a chunky block arrow up (the user's pick, "small block")
        '...o...',
        '..oao..',
        '.oabco.',
        'oabbbco',
        'ooabcoo',
        '.oabco.',
        '.ooooo.']),
    ('Counter', (0.76, 0.58, 0.2), [     # a shield
        '.......',
        '.aaabb.',
        '.abbbc.',
        '.abbbc.',
        '..bbc..',
        '...c...',
        '.......']),
    ('Heal', (0.35, 0.72, 0.48), [       # a plus
        '.......',
        '...a...',
        '...a...',
        '.aabbc.',
        '...b...',
        '...c...',
        '.......']),
    ('Ultimate', (1.0, 0.8, 0.3), [      # a star
        '.......',
        '.......',
        '...a...',
        '.aabbc.',
        '..abc..',
        '..b.c..',
        '.......']),
]
TYPE_SIZE = 7


def make_types():
    c = Canvas(TYPE_SIZE * len(TYPE_SHAPES), TYPE_SIZE)
    for k, (name, rgb, rows) in enumerate(TYPE_SHAPES):
        cell = Canvas(TYPE_SIZE, TYPE_SIZE)
        base = tuple(int(v * 255) for v in rgb)
        pal = {
            'a': tuple(min(255, int(v + (255 - v) * 0.55)) for v in base) + (255,),
            'b': base + (255,),
            'c': tuple(int(v * 0.6) for v in base) + (255,),
            'o': OUTLINE,
        }
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch in pal:
                    cell.px[y][x] = pal[ch]
        if name not in ('Debuff', 'Buff'):   # the arrows draw their own outline (o) to the cell's edge
            cell.outline()
        c.paste(cell, k * TYPE_SIZE, 0)
    return c


# ---------------------------------------------------------------------------------------------- icons (32 x 32)

STEEL = [hx('#FFFFFF'), hx('#D6DEEA'), hx('#9AA6BA'), hx('#5E6880')]
GOLD = [hx('#FFF1A8'), hx('#F2C94C'), hx('#C9822A'), hx('#7A4318')]
GREEN = [hx('#E2FF9A'), hx('#9ADC50'), hx('#4E9E3A'), hx('#255A2E')]
WOOD = [hx('#C88A52'), hx('#9A5E32'), hx('#663A20')]
VIOLET = [hx('#F2D6FF'), hx('#C08AF2'), hx('#8A4ED8'), hx('#4E2490')]
PINK = [hx('#FFD0E6'), hx('#F27AB6'), hx('#B8407E')]
CYAN = [hx('#E0FDFF'), hx('#7FE6FF'), hx('#2EA8D8'), hx('#1A5C8A')]
RUST = [hx('#F2B47A'), hx('#C8742E'), hx('#8A4420'), hx('#4E2414')]
RED = [hx('#FFB09A'), hx('#F0503A'), hx('#9A2030')]
AMBER = [hx('#FFF0B0'), hx('#FFC44A'), hx('#D8801E'), hx('#8A4A12')]


def blade(c, x0, y0, x1, y1, width, pal, guard=None, grip=None):
    """A straight sword from the hilt (x0, y0) to the tip (x1, y1): lit upper edge, dark lower edge, a fuller."""
    dx, dy = x1 - x0, y1 - y0
    L = math.hypot(dx, dy)
    ux, uy = dx / L, dy / L
    nx, ny = -uy, ux
    hw = width / 2.0
    tipback = 4.0
    base_a = (x0 + nx * hw, y0 + ny * hw)
    base_b = (x0 - nx * hw, y0 - ny * hw)
    sh_a = (x1 - ux * tipback + nx * hw, y1 - uy * tipback + ny * hw)
    sh_b = (x1 - ux * tipback - nx * hw, y1 - uy * tipback - ny * hw)
    c.poly([base_a, sh_a, (x1, y1), sh_b, base_b], pal[2])
    # lit half (the side facing up-left)
    lit = 1 if (nx + ny) < 0 else -1
    c.poly([(x0, y0), (x1, y1), (x1 - ux * tipback + lit * nx * hw, y1 - uy * tipback + lit * ny * hw),
            (x0 + lit * nx * hw, y0 + lit * ny * hw)], pal[1])
    c.line(x0 + ux * 2, y0 + uy * 2, x1 - ux * 2, y1 - uy * 2, pal[0])
    if guard is not None:
        gx, gy = x0, y0
        c.stroke([(gx + nx * 5, gy + ny * 5), (gx - nx * 5, gy - ny * 5)], 1.3, guard[1])
        c.line(gx + nx * 4, gy + ny * 4, gx - nx * 4, gy - ny * 4, guard[0])
    if grip is not None:
        c.stroke([(x0 - ux * 1.5, y0 - uy * 1.5), (x0 - ux * 6, y0 - uy * 6)], 1.2, grip[1])
        c.disc(x0 - ux * 7.5, y0 - uy * 7.5, 1.8, (guard or grip)[1])
        c.put(x0 - ux * 8 - 0.5, y0 - uy * 8 - 0.5, (guard or grip)[0])


def icon_edge_strike():
    c = Canvas(CELL, CELL)
    # a slash arc behind the sword
    for i, r in enumerate((13.5, 12.5)):
        pts = curve(lambda t, r=r: (16 + r * math.cos(math.radians(200 + 120 * t)), 18 + r * math.sin(math.radians(200 + 120 * t))))
        for (x, y) in pts:
            c.put(x, y, CYAN[1] if i == 0 else CYAN[0])
    blade(c, 9, 23, 26, 5, 4.2, STEEL, guard=GOLD, grip=WOOD)
    c.outline()
    return c


def icon_cross_slash():
    c = Canvas(CELL, CELL)
    blade(c, 9, 24, 25, 6, 3.2, STEEL, guard=GOLD, grip=WOOD)
    blade(c, 23, 24, 7, 6, 3.2, STEEL, guard=GOLD, grip=WOOD)
    c.outline()
    # two slash streaks crossing in front
    for k in range(-1, 2):
        c.line(4, 9 + k, 27, 26 + k, CYAN[0] if k == 0 else CYAN[1])
        c.line(27, 9 + k, 4, 26 + k, CYAN[0] if k == 0 else CYAN[1])
    return c


def hexagon(cx, cy, r):
    return [(cx + r * math.cos(math.radians(60 * i - 90)), cy + r * math.sin(math.radians(60 * i - 90))) for i in range(6)]


def icon_sync_field():
    c = Canvas(CELL, CELL)
    c.poly(hexagon(16, 16, 13.5), CYAN[3])
    c.poly(hexagon(16, 16, 12), CYAN[2])
    c.poly(hexagon(16, 16, 9), CYAN[1])
    c.poly(hexagon(16, 16, 7.5), CYAN[3])
    c.poly(hexagon(16, 16, 5), CYAN[0])
    c.disc(16, 16, 2.2, (255, 255, 255, 255))
    # lit top-left facets
    for (x, y) in hexagon(16, 16, 12)[4:] + hexagon(16, 16, 12)[:1]:
        pass
    c.line(7, 9, 15, 4, CYAN[0])
    c.line(5, 12, 5, 19, CYAN[1])
    # orbit sparks
    for (x, y) in ((3, 4), (28, 6), (27, 27), (4, 27)):
        c.put(x, y, CYAN[0])
        c.put(x + 1, y, CYAN[1])
        c.put(x, y + 1, CYAN[1])
    c.outline()
    return c


def icon_bramble_lash():
    c = Canvas(CELL, CELL)
    path = curve(lambda t: (5 + 22 * t, 26 - 20 * t + 7 * math.sin(t * math.pi * 1.6)), 40)
    c.stroke(path, 1.8, GREEN[2])
    c.stroke([(x - 0.6, y - 0.6) for (x, y) in path], 0.8, GREEN[1])
    # thorns on alternate sides
    for i in range(4, len(path) - 2, 6):
        (x0, y0), (x1, y1) = path[i - 1], path[i + 1]
        nx, ny = -(y1 - y0), (x1 - x0)
        n = math.hypot(nx, ny) or 1
        s = 1 if (i // 6) % 2 == 0 else -1
        tx, ty = path[i][0] + s * nx / n * 4.5, path[i][1] + s * ny / n * 4.5
        c.line(path[i][0], path[i][1], tx, ty, WOOD[1])
        c.put(tx, ty, GOLD[0])
    # handle wrapped in bark, a leaf at the tip
    c.stroke([(3, 28), (7, 24)], 2.0, WOOD[1])
    c.line(3, 27, 6, 24, WOOD[0])
    c.poly([(27, 6), (30, 2), (31, 7), (28, 9)], GREEN[1])
    c.put(29, 5, GREEN[0])
    c.outline()
    return c


def leaf(c, x, y, ang, length, pal):
    ux, uy = math.cos(ang), math.sin(ang)
    nx, ny = -uy, ux
    tip = (x + ux * length, y + uy * length)
    mid = (x + ux * length * 0.5, y + uy * length * 0.5)
    w = length * 0.38
    c.poly([(x, y), (mid[0] + nx * w, mid[1] + ny * w), tip, (mid[0] - nx * w, mid[1] - ny * w)], pal[2])
    c.poly([(x, y), (mid[0] + nx * w, mid[1] + ny * w), tip], pal[1])
    c.line(x, y, tip[0] - ux, tip[1] - uy, pal[0])


def icon_overgrowth():
    c = Canvas(CELL, CELL)
    # soil mound
    c.poly([(4, 30), (8, 25), (24, 25), (28, 30)], WOOD[2])
    c.poly([(7, 27), (9, 25), (23, 25), (25, 27)], WOOD[1])
    # stems
    c.stroke(curve(lambda t: (16 + 2 * math.sin(t * 3), 26 - 21 * t)), 1.0, GREEN[2])
    c.stroke(curve(lambda t: (16 - 9 * t, 24 - 10 * t * t)), 0.8, GREEN[2])
    c.stroke(curve(lambda t: (16 + 9 * t, 22 - 9 * t * t)), 0.8, GREEN[2])
    leaf(c, 16, 12, math.radians(-150), 9, GREEN)
    leaf(c, 17, 9, math.radians(-30), 9, GREEN)
    leaf(c, 8, 15, math.radians(-120), 7, GREEN)
    leaf(c, 24, 14, math.radians(-60), 7, GREEN)
    # flowers
    for (x, y, pal) in ((16, 4, PINK), (6, 13, GOLD), (26, 12, PINK)):
        c.disc(x, y, 2.4, pal[1])
        c.put(x, y, pal[0])
        c.put(x - 1, y - 1, pal[0])
    c.outline()
    return c


def icon_sap_mend():
    c = Canvas(CELL, CELL)
    # a big sap droplet
    drop = [(16, 3)] + [(16 + 10 * math.sin(a), 19 - 10 * math.cos(a)) for a in [math.radians(d) for d in range(40, 330, 10)]]
    c.poly(drop, AMBER[2])
    c.poly([(16, 6), (10, 16), (9, 21), (13, 26), (16, 27), (16, 6)], AMBER[1])
    c.disc(12, 20, 2.0, AMBER[0])
    c.put(13, 15, AMBER[0])
    c.put(14, 12, AMBER[0])
    # a plus inside (it heals)
    c.rect(18, 17, 19, 24, (255, 255, 255, 255))
    c.rect(15, 20, 22, 21, (255, 255, 255, 255))
    # a leaf on the side
    leaf(c, 24, 9, math.radians(-40), 8, GREEN)
    c.outline()
    return c


def icon_virus_spike():
    c = Canvas(CELL, CELL)
    for k in range(10):
        a = math.radians(k * 36 + 10)
        x1, y1 = 16 + 12.5 * math.cos(a), 16 + 12.5 * math.sin(a)
        c.stroke([(16 + 8 * math.cos(a), 16 + 8 * math.sin(a)), (x1, y1)], 0.7, VIOLET[3])
        c.disc(x1, y1, 1.8, PINK[1])
        c.put(x1 - 0.5, y1 - 0.5, PINK[0])
    c.disc(16, 16, 9, VIOLET[2])
    c.disc(15, 15, 7.2, VIOLET[1])
    c.disc(13, 13, 3.0, VIOLET[0])
    for (x, y) in ((19, 18), (14, 20), (20, 13)):
        c.disc(x, y, 1.3, VIOLET[3])
    c.outline()
    return c


def icon_corrupt():
    c = Canvas(CELL, CELL)
    # a microchip
    for i in range(4):
        for side in (0, 1):
            x = 6 + side * 18
            y = 9 + i * 4
            c.rect(x - 3 if side == 0 else x + 2, y, x if side == 0 else x + 5, y + 1, STEEL[2])
            c.rect(9 + i * 4, 3 if side == 0 else 26, 10 + i * 4, 6 if side == 0 else 29, STEEL[2])
    c.rect(6, 6, 25, 25, hx('#2E2A40'))
    c.rect(7, 7, 24, 24, hx('#454060'))
    c.rect(7, 7, 24, 8, hx('#5E5880'))
    # violet glitch traces and a crack
    c.rect(11, 11, 20, 20, VIOLET[3])
    c.rect(12, 12, 19, 19, VIOLET[2])
    c.line(10, 6, 14, 13, hx('#120C1C'))
    c.line(14, 13, 12, 18, hx('#120C1C'))
    c.line(12, 18, 17, 25, hx('#120C1C'))
    for (x, y, w) in ((3, 15, 6), (22, 12, 7), (17, 21, 9), (5, 22, 4)):
        c.rect(x, y, x + w, y, VIOLET[0] if w > 5 else PINK[1])
    c.outline()
    return c


def icon_worm():
    c = Canvas(CELL, CELL)
    path = curve(lambda t: (5 + 22 * t, 22 - 12 * math.sin(t * math.pi) + 3 * math.sin(t * math.pi * 3)), 7)
    for i, (x, y) in enumerate(path):
        r = 3.6 if i < len(path) - 1 else 4.4
        c.disc(x, y, r, VIOLET[2] if i % 2 == 0 else PINK[2])
        c.disc(x - 0.8, y - 1.0, r * 0.55, VIOLET[1] if i % 2 == 0 else PINK[1])
    hx_, hy = path[-1]
    c.line(hx_ - 1, hy - 4, hx_ - 2, hy - 7, PINK[2])           # antennae
    c.line(hx_ + 2, hy - 4, hx_ + 3, hy - 7, PINK[2])
    c.put(hx_ - 2, hy - 8, CYAN[0])
    c.put(hx_ + 3, hy - 8, CYAN[0])
    c.put(hx_ + 1, hy - 1, (255, 255, 255, 255))
    c.put(hx_ + 2, hy - 1, OUTLINE)
    c.put(hx_ + 1, hy + 2, OUTLINE)
    c.put(hx_ + 2, hy + 2, OUTLINE)
    # binary crumbs it leaves behind
    for (x, y) in ((3, 28), (7, 29), (11, 28)):
        c.rect(x, y, x, y + 1, CYAN[1])
    c.outline()
    return c


def icon_corroded_blade():
    c = Canvas(CELL, CELL)
    blade(c, 8, 25, 26, 6, 5.0, RUST, guard=STEEL, grip=WOOD)
    # pits and a broken notch
    for (x, y) in ((15, 15), (19, 12), (13, 18), (22, 9)):
        c.put(x, y, RUST[3])
        c.put(x + 1, y, RUST[2])
    c.px[8][24] = CLEAR
    c.px[9][23] = CLEAR
    c.outline()
    # dripping corrosion
    c.rect(18, 17, 18, 20, GREEN[1])
    c.put(18, 21, GREEN[0])
    return c


def missile(c, x, y, ang, length, body):
    ux, uy = math.cos(ang), math.sin(ang)
    nx, ny = -uy, ux
    tail = (x - ux * length, y - uy * length)
    c.stroke([(x, y), tail], 1.6, body[1])
    c.line(x - ux, y - uy, tail[0], tail[1], body[0])
    c.disc(x, y, 1.6, RED[1])
    c.poly([(tail[0] + nx * 3, tail[1] + ny * 3), (tail[0] + ux * 2, tail[1] + uy * 2), (tail[0] - nx * 3, tail[1] - ny * 3)], RED[2])
    # flame
    c.stroke([(tail[0] - ux * 1.5, tail[1] - uy * 1.5), (tail[0] - ux * 5, tail[1] - uy * 5)], 1.2, AMBER[1])
    c.put(tail[0] - ux * 2, tail[1] - uy * 2, AMBER[0])


def icon_missile_volley():
    c = Canvas(CELL, CELL)
    ang = math.radians(-45)
    for (x, y) in ((17, 6), (23, 12), (29, 18)):     # side by side, flying up-right in formation
        missile(c, x, y, ang, 10, STEEL)
    c.outline()
    return c


def icon_network_link():
    c = Canvas(CELL, CELL)
    nodes = [(16, 7), (6, 24), (26, 24)]
    for i in range(3):
        a, b = nodes[i], nodes[(i + 1) % 3]
        c.stroke([a, b], 0.8, CYAN[2])
    for (x, y) in nodes:
        c.disc(x, y, 4.2, CYAN[3])
        c.disc(x, y, 3.2, CYAN[1])
        c.disc(x - 1, y - 1, 1.3, CYAN[0])
    # a pulse travelling along a link
    c.disc(11, 15.5, 1.5, (255, 255, 255, 255))
    c.disc(16, 24, 1.2, hx('#FFF6A0'))
    c.outline()
    return c


def icon_golden_hammer():
    c = Canvas(CELL, CELL)
    # cylinder at the top, rod, a heavy plate slamming down
    c.rect(9, 2, 22, 10, STEEL[3])
    c.rect(10, 3, 21, 9, STEEL[2])
    c.rect(10, 3, 13, 9, STEEL[1])
    c.rect(14, 11, 17, 18, STEEL[2])
    c.rect(14, 11, 15, 18, STEEL[0])
    c.rect(5, 19, 26, 24, RUST[2])
    c.rect(6, 20, 25, 22, RUST[1])
    c.rect(6, 20, 25, 20, RUST[0])
    for x in (8, 23):
        c.put(x, 22, RUST[3])
    c.outline()
    # impact lines under the plate
    for (x0, x1) in ((4, 1), (10, 9), (16, 16), (22, 23), (28, 31)):
        c.line(x0, 27, x1, 30, hx('#FFF6A0'))
    return c


def icon_reinforce():
    c = Canvas(CELL, CELL)
    shield = [(16, 3), (27, 7), (26, 18), (16, 29), (6, 18), (5, 7)]
    c.poly(shield, STEEL[3])
    c.poly([(16, 5), (25, 8), (24, 18), (16, 27), (8, 18), (7, 8)], STEEL[2])
    c.poly([(16, 5), (16, 27), (8, 18), (7, 8)], STEEL[1])
    # a riveted cross band
    c.rect(15, 5, 16, 26, RUST[1])
    c.rect(8, 13, 24, 14, RUST[1])
    for (x, y) in ((10, 9), (22, 9), (11, 20), (21, 20)):
        c.put(x, y, STEEL[0])
    c.put(12, 7, (255, 255, 255, 255))
    c.outline()
    return c


def icon_menu_resume():
    c = Canvas(CELL, CELL)
    c.poly([(9, 5), (27, 16), (9, 27)], GOLD[2])
    c.poly([(10, 7), (25, 16), (10, 16)], GOLD[0])
    c.poly([(10, 16), (25, 16), (10, 25)], GOLD[1])
    c.outline()
    return c


def icon_menu_save():
    c = Canvas(CELL, CELL)
    c.rect(5, 5, 26, 26, CYAN[3])                 # disk body
    c.rect(6, 6, 25, 25, CYAN[2])
    c.rect(6, 6, 25, 7, CYAN[1])
    c.rect(10, 6, 21, 12, STEEL[1])               # metal shutter
    c.rect(17, 7, 19, 11, STEEL[3])
    c.rect(9, 16, 22, 24, (255, 255, 255, 255))   # label
    for y in (18, 20, 22):
        c.rect(11, y, 20, y, STEEL[2])
    c.put(24, 24, CYAN[1])
    c.outline()
    return c


def icon_menu_quit():
    c = Canvas(CELL, CELL)
    c.rect(7, 4, 22, 28, hx('#3A2A20'))           # dark doorway
    c.rect(8, 5, 21, 27, hx('#1A1218'))
    c.poly([(14, 5), (24, 2), (24, 30), (14, 27)], WOOD[1])   # the door swung open
    c.poly([(15, 6), (23, 3.5), (23, 9), (15, 10)], WOOD[0])
    c.rect(20, 15, 21, 17, GOLD[1])               # handle
    c.poly([(3, 16), (9, 11), (9, 14), (13, 14), (13, 18), (9, 18), (9, 21)], RED[1])  # exit arrow
    c.outline()
    return c


def icon_menu_party():
    c = Canvas(CELL, CELL)
    for (x, y, pal) in ((8, 15, GREEN), (24, 15, VIOLET), (16, 12, STEEL)):   # Ashe, Lucy, MC in front
        c.disc(x, y + 8, 6.5, pal[2])             # shoulders
        c.disc(x, y, 5.2, pal[2])                 # helmet
        c.disc(x - 1, y - 1, 3.4, pal[1])
        c.put(x - 2, y - 2, pal[0])
        c.rect(x - 2, y + 1, x + 2, y + 1, CYAN[0] if pal is STEEL else pal[0])  # visor glow
    c.outline()
    return c


def kite_shield(c, cx, top, w, h, pal, trim):
    """A kite shield: lit left half, darker right half, a trim band and a boss in the middle."""
    pts = [(cx, top), (cx + w / 2, top + 3), (cx + w / 2 - 1, top + h * 0.55), (cx, top + h), (cx - w / 2 + 1, top + h * 0.55), (cx - w / 2, top + 3)]
    c.poly(pts, pal[3])
    inner = [(cx, top + 2), (cx + w / 2 - 2, top + 4), (cx + w / 2 - 3, top + h * 0.53), (cx, top + h - 3), (cx - w / 2 + 3, top + h * 0.53), (cx - w / 2 + 2, top + 4)]
    c.poly(inner, pal[2])
    c.poly([inner[0], inner[3], inner[4], inner[5]], pal[1])
    c.rect(int(cx) - 1, top + 3, int(cx), top + h - 4, trim[1])
    c.disc(cx, top + h * 0.42, 2.6, trim[0])


def icon_shield_bash():
    c = Canvas(CELL, CELL)
    kite_shield(c, 13, 4, 18, 24, CYAN, GOLD)
    c.outline()
    for (x0, y0, x1, y1) in ((24, 8, 30, 5), (25, 14, 31, 14), (24, 20, 30, 23)):   # impact lines off the rim
        c.line(x0, y0, x1, y1, hx('#FFF6A0'))
    return c


def icon_bulwark():
    c = Canvas(CELL, CELL)
    kite_shield(c, 16, 6, 22, 24, CYAN, STEEL)
    c.poly([(16, 1), (23, 8), (19, 8), (19, 13), (13, 13), (13, 8), (9, 8)], GREEN[1])     # rising arrow behind it
    c.poly([(16, 2), (21, 7), (16, 7)], GREEN[0])
    c.outline()
    return c


def icon_def_up():
    c = Canvas(CELL, CELL)
    kite_shield(c, 13, 4, 20, 24, CYAN, STEEL)
    c.poly([(25, 14), (31, 22), (27, 22), (27, 29), (23, 29), (23, 22), (19, 22)], GREEN[1])
    c.poly([(25, 15), (29, 20), (25, 20)], GREEN[0])
    c.outline()
    return c


def bolt(c, pts, pal):
    """A thick lightning bolt along a zig-zag polyline, with a bright core."""
    c.stroke(pts, 1.6, pal[2])
    c.stroke(pts, 0.7, pal[0])


def icon_arc_edge():
    c = Canvas(CELL, CELL)
    blade(c, 8, 24, 25, 6, 3.6, STEEL, guard=GOLD, grip=WOOD)
    c.outline()
    # an arc of lightning jumping off the tip to a second point
    bolt(c, [(25, 6), (28, 10), (25, 13), (29, 18), (26, 22), (30, 27)], AMBER)
    return c


def icon_static_field():
    c = Canvas(CELL, CELL)
    for r, col in ((13, CYAN[2]), (9, CYAN[1])):                 # a crackling dome
        for k in range(0, 181, 6):
            a = math.radians(180 + k)
            c.put(16 + r * math.cos(a), 24 + r * math.sin(a), col)
    bolt(c, [(8, 22), (11, 15), (14, 19), (17, 10), (20, 17), (24, 13)], AMBER)
    c.rect(4, 25, 27, 26, STEEL[3])                                # the ground line
    c.outline()
    return c


def icon_stunned():
    c = Canvas(CELL, CELL)
    for i, (x, y) in enumerate(((9, 9), (23, 8), (16, 4))):        # dizzy stars
        pal = AMBER if i != 2 else GOLD
        c.poly([(x, y - 4), (x + 1.3, y - 1.2), (x + 4, y), (x + 1.3, y + 1.2), (x, y + 4), (x - 1.3, y + 1.2), (x - 4, y), (x - 1.3, y - 1.2)], pal[1])
        c.put(x, y, pal[0])
    bolt(c, [(10, 30), (14, 21), (18, 25), (22, 15)], AMBER)
    c.outline()
    return c


# Chapter One: the protagonist's Electric cards and the Stunned status.
CH1_ICONS = [
    ('Card_MC_ArcEdge', icon_arc_edge),
    ('Card_MC_StaticField', icon_static_field),
    ('Status_Stunned', icon_stunned),
]

# Blue's cards and the DEF Up status icon (CardIcons.Status picks it up by name).
BLUE_ICONS = [
    ('Card_Blue_ShieldBash', icon_shield_bash),
    ('Card_Blue_Bulwark', icon_bulwark),
    ('Status_DefUp', icon_def_up),
]

def icon_menu_units():
    c = Canvas(CELL, CELL)
    c.rect(4, 7, 27, 25, STEEL[3])                 # an ID card
    c.rect(5, 8, 26, 24, STEEL[1])
    c.rect(5, 8, 26, 10, CYAN[2])                  # its header band
    c.rect(7, 13, 14, 22, CYAN[3])                 # portrait window
    c.disc(10.5, 16, 2.4, STEEL[0])                # head
    c.rect(8, 19, 13, 22, STEEL[0])                # shoulders
    for y in (14, 17, 20):                         # text lines
        c.rect(16, y, 24, y, STEEL[2])
    c.outline()
    return c


def icon_menu_settings():
    import math
    c = Canvas(CELL, CELL)
    cx, cy = 15.5, 15.5
    for k in range(8):                             # eight teeth round the rim
        a = k * math.pi / 4
        x, y = cx + math.cos(a) * 10.5, cy + math.sin(a) * 10.5
        c.disc(x, y, 3.0, STEEL[3])
    c.disc(cx, cy, 10.0, STEEL[3])                 # the wheel
    c.disc(cx, cy, 8.8, STEEL[2])
    c.disc(cx - 1.5, cy - 1.5, 6.0, STEEL[1])      # lit from the top left
    c.disc(cx, cy, 4.2, STEEL[3])                  # the axle hole
    c.disc(cx, cy, 3.0, hx('#1A1218'))
    c.put(12, 11, STEEL[0])
    c.outline()
    return c


# The overworld Esc menu's cards (Game.World.PauseCards): drawn here so they match the battle cards.
MENU_ICONS = [
    ('Menu_Units', icon_menu_units),
    ('Menu_Resume', icon_menu_resume),
    ('Menu_Save', icon_menu_save),
    ('Menu_Quit', icon_menu_quit),
    ('Menu_Party', icon_menu_party),
    ('Menu_Settings', icon_menu_settings),
]

def icon_menu_resonance():
    c = Canvas(CELL, CELL)
    cx, cy = 15.5, 15.5
    # the Core: a faceted crystal, Physical steel on the left and Electric amber on the right
    c.poly([(cx, 4), (cx + 9, cy), (cx, 27), (cx - 9, cy)], STEEL[3])
    c.poly([(cx, 6), (cx - 7, cy), (cx, 25)], STEEL[1])
    c.poly([(cx, 6), (cx + 7, cy), (cx, 25)], AMBER[1])
    c.poly([(cx, 6), (cx - 7, cy), (cx - 2, cy - 1)], STEEL[0])
    c.poly([(cx, 6), (cx + 7, cy), (cx + 2, cy - 1)], AMBER[0])
    c.outline()
    # an orbit of element sparks around it
    for k, col in enumerate((AMBER[1], RED[1], GREEN[1], VIOLET[1], CYAN[1], STEEL[0])):
        a = math.radians(-90 + k * 60)
        x, y = 16 + math.cos(a) * 13.5, 16 + math.sin(a) * 13.5
        c.disc(x, y, 1.6, col)
    return c


def icon_ui_lock():
    c = Canvas(CELL, CELL)
    for k in range(0, 181, 4):                    # the shackle
        a = math.radians(180 + k)
        for r in (6.5, 7.5, 8.5):
            c.put(15.5 + r * math.cos(a), 14 + r * math.sin(a), STEEL[2])
    c.rect(7, 13, 8, 15, STEEL[2])
    c.rect(23, 13, 24, 15, STEEL[2])
    c.rect(5, 15, 26, 28, GOLD[3])                 # the body
    c.rect(6, 16, 25, 27, GOLD[2])
    c.rect(6, 16, 25, 18, GOLD[1])
    c.rect(6, 16, 7, 27, GOLD[1])
    c.disc(15.5, 21, 2.2, hx('#1A1218'))           # keyhole
    c.rect(15, 22, 16, 25, hx('#1A1218'))
    c.put(7, 17, GOLD[0])
    c.outline()
    return c


def icon_ui_binoculars():
    c = Canvas(CELL, CELL)
    for x in (9, 22):                              # two barrels, wide lenses at the bottom
        c.rect(x - 3, 6, x + 3, 15, STEEL[3])
        c.rect(x - 2, 7, x + 2, 15, STEEL[2])
        c.disc(x, 21, 6.5, STEEL[3])
        c.disc(x, 21, 5.5, hx('#3A3450'))
        c.disc(x, 21, 4.0, CYAN[2])               # the lens, with the scanner's cyan glint
        c.disc(x - 1, 20, 2.2, CYAN[1])
        c.put(x - 2, 19, CYAN[0])
        c.rect(x - 3, 6, x + 3, 7, STEEL[1])      # eyepiece rims
    c.rect(12, 9, 19, 12, STEEL[2])                # the bridge and focus wheel
    c.rect(14, 8, 17, 13, GOLD[2])
    c.rect(14, 9, 17, 9, GOLD[1])
    c.outline()
    return c


def icon_menu_items():
    c = Canvas(CELL, CELL)
    # a drawstring pouch: teal velvet body, gathered neck, gold cord, a chip peeking out
    c.disc(16, 20, 9.5, CYAN[3])
    c.disc(16, 20, 8.5, CYAN[2])
    c.disc(13.5, 18, 5.0, CYAN[1])
    c.put(12, 16, CYAN[0])
    c.poly([(11, 11), (21, 11), (19, 13), (13, 13)], CYAN[2])        # the gathered neck
    c.rect(11, 12, 21, 13, GOLD[2])                                 # the cord
    c.rect(12, 12, 20, 12, GOLD[1])
    c.disc(22, 14, 1.4, GOLD[1])                                    # its knot
    c.poly([(13, 5), (20, 4), (21, 10), (14, 11)], STEEL[2])        # a chip peeking out
    c.poly([(14, 6), (19, 5), (20, 9), (15, 10)], STEEL[1])
    c.rect(16, 7, 17, 8, CYAN[0])
    c.outline()
    return c


# UI pieces outside the cards: the Esc menu's Resonance and Items cards, the lock on locked resonances and the
# scanner's HUD icon.
UI_ICONS = [
    ('Menu_Resonance', icon_menu_resonance),
    ('Ui_Lock', icon_ui_lock),
    ('Ui_Binoculars', icon_ui_binoculars),
    ('Menu_Items', icon_menu_items),
]

# The user's picks from Raven Fantasy Icons (Clockwork Raven, free version; the 32 px files live in
# Art/ThirdParty/RavenFantasyIcons/32x32/fb<n>.png): a card listed here uses that icon as-is (32 px fills the art window)
# instead of the drawing above. Cards with no drawing of their own are added to the atlas from here too.
RAVEN_DIR = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'ThirdParty', 'RavenFantasyIcons', '32x32')
RAVEN_PICKS = {
    'Card_MC_EdgeStrike': 724, 'Card_MC_CrossSlash': 155, 'Card_MC_SyncField': 1047, 'Card_MC_ArcEdge': 731,
    'Card_MC_StaticField': 1039,
    'Card_Ashe_BrambleLash': 1067, 'Card_Ashe_Overgrowth': 1110, 'Card_Ashe_SapMend': 1073,
    'Card_Lucy_VirusSpike': 741, 'Card_Lucy_Corrupt': 1117, 'Card_Lucy_Worm': 342,
    'Card_Blue_ShieldBash': 1811, 'Card_Blue_Bulwark': 2151,
    'Card_Enemy_CorrodedBlade': 722, 'Card_Enemy_MissileVolley': 1017, 'Card_Enemy_NetworkLink': 905,
    'Card_Enemy_GoldenHammer': 1044,
    'Card_Enemy_Reinforce': 1921, 'Card_Enemy_Bite': 799, 'Card_Enemy_CoilUp': 824,
    'Card_Enemy_GrappleHook': 393,
    'Card_Enemy_PoisonStrike': 835,
    'Card_Enemy_Spark': 1025, 'Card_Enemy_StaticCoil': 1027, 'Card_Enemy_SuppressingShot': 1034,
    'Card_Enemy_TailLash': 1057, 'Card_Enemy_ThunderBreath': 832,
    # The Esc menu cards and the scanner HUD (picked 2026-10-24; Units, Resonance and the lock keep their drawings).
    'Menu_Quit': 3, 'Menu_Save': 10, 'Menu_Settings': 2, 'Menu_Party': 14, 'Menu_Items': 1,
    'Ui_Binoculars': 297,
}


def raven_icon(n):
    """A picked Raven icon as a 32 x 32 Canvas in its own colours (<see framed> adds the outline)."""
    from pngtool import read_png
    w, h, px = read_png(os.path.join(RAVEN_DIR, 'fb%d.png' % n))
    c = Canvas(CELL, CELL)
    for y in range(min(h, CELL)):
        for x in range(min(w, CELL)):
            if px[y][x][3] > 0:
                c.px[y][x] = tuple(px[y][x])
    return c


def framed(icon):
    """An icon in its 34 px cell: the 32 px drawing 1 px in from every side and the cards' 1 px plum outline all the
    way round it, so every icon has the same whole border (pale icons still read on pale swirls)."""
    c = Canvas(ICON_CELL, ICON_CELL)
    c.paste(icon, 1, 1)
    c.outline()
    return c


def raven_only_icons(existing):
    """Picked cards that have no drawing of their own, as (name, None) atlas entries."""
    names = set(n for n, _ in existing)
    return [(name, None) for name in sorted(RAVEN_PICKS) if name not in names]


ICONS = [
    ('Card_MC_EdgeStrike', icon_edge_strike),
    ('Card_MC_CrossSlash', icon_cross_slash),
    ('Card_MC_SyncField', icon_sync_field),
    ('Card_Ashe_BrambleLash', icon_bramble_lash),
    ('Card_Ashe_Overgrowth', icon_overgrowth),
    ('Card_Ashe_SapMend', icon_sap_mend),
    ('Card_Lucy_VirusSpike', icon_virus_spike),
    ('Card_Lucy_Corrupt', icon_corrupt),
    ('Card_Lucy_Worm', icon_worm),
    ('Card_Enemy_CorrodedBlade', icon_corroded_blade),
    ('Card_Enemy_MissileVolley', icon_missile_volley),
    ('Card_Enemy_NetworkLink', icon_network_link),
    ('Card_Enemy_GoldenHammer', icon_golden_hammer),
    ('Card_Enemy_Reinforce', icon_reinforce),
]

# Enemies with an Ult: their full-art card uses the front-facing idle frame of their battle sprite (Art/Characters),
# shown as a 2x bust (head and shoulders) filling the card. (asset name, sprite folder, Enemy_*.asset colour)
ULT_ENEMIES = [
    ('Enemy_RustSentry', 'RustSentry', (0.66, 0.46, 0.3)),
    # Chapter One: every enemy uses the red Rust Sentry sprite for now (placeholder art).
    ('Enemy_Collector', 'RustSentry', (0.62, 0.66, 0.72)),
    ('Enemy_ElectricDragon', 'RustSentry', (0.45, 0.75, 1.0)),
    ('Enemy_ScrapStalker', 'ScrapStalker', (0.5, 0.58, 0.32)),
]
CHARS = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')


def sprite_art(folder):
    """The idle sheet's first front-facing frame (row 2 of 64 px frames), cropped to its pixels, as (w, h, rows)."""
    from pngtool import read_png
    path = os.path.join(CHARS, folder, 'idle.png')
    if not os.path.exists(path):
        return None
    w, h, px = read_png(path)
    f = 64
    y0 = 2 * f if h >= 3 * f else 0
    cells = [(x, y) for y in range(y0, y0 + f) for x in range(f) if px[y][x][3] > 0]
    if not cells:
        return None
    x1, x2 = min(c[0] for c in cells), max(c[0] for c in cells)
    y1, y2 = min(c[1] for c in cells), max(c[1] for c in cells)
    # A 2x bust: the top of the figure, as much as fits inside the rim, centred on the figure.
    k = 2
    cw, ch = min(x2 - x1 + 1, (W - 6) // k), min(y2 - y1 + 1, (H - 4) // k)
    cx = (x1 + x2 + 1) // 2 - cw // 2
    bw, bh = cw * k, ch * k
    rows = [[tuple(px[y1 + yy // k][cx + xx // k]) for xx in range(bw)] for yy in range(bh)]
    return bw, bh, rows

# Party members' card colours (Char_*.asset "color"), used to bake the Ult cards' swirl.
ULT_OWNERS = [
    ('Char_MC', 'MC', (0.92, 0.92, 0.95)),
    ('Char_Ashe', 'Ashe', (0.5, 0.86, 0.45)),
    ('Char_Lucy', 'Lucy', (0.72, 0.45, 0.95)),
    ('Char_Blue', 'Blue', (0.45, 0.65, 1.0)),
]


# Resonance Ults with their own full art (CardArt.Ult tries "Ult_<card asset>" before the owner's card):
# (card asset, sprite folder, swirl colour = AfflatusInfo.Color of the element, extra decoration)
ULT_CARDS = [
    ('Card_MC_Thunderheart', 'MC', (1.0, 0.84, 0.18), 'bolts'),   # Electric: yellow, lightning down both sides
]


def add_bolts(card):
    """Lightning crackling down both edges of an Ult card, inside the rim."""
    bolt(card, [(7, 6), (10, 13), (6, 19), (10, 27), (6, 34), (9, 41)], AMBER)
    bolt(card, [(40, 22), (37, 29), (41, 36), (37, 44), (41, 51), (38, 58)], AMBER)
    for (x, y) in ((12, 9), (35, 26), (11, 38), (36, 54)):
        card.put(x, y, AMBER[0])


def tint(grey, color):
    """The same multiply the game applies to CardSwirl (Image.color)."""
    return tuple(int(grey[i] * color[i]) for i in range(3)) + (grey[3],)


def make_ult(char_name, color, ref=None):
    if ref is None:
        import chibi_forge as cf
        ref = cf.reference_pixels(char_name)
    card = Canvas(W, H)
    swirl = make_swirl()
    for y in range(H):
        for x in range(W):
            if swirl.px[y][x][3]:
                card.px[y][x] = tint(swirl.px[y][x], color)
    if ref is not None:
        rw, rh, rows = ref
        art = Canvas(rw, rh)
        for y in range(rh):
            for x in range(rw):
                p = rows[y][x]
                art.px[y][x] = tuple(p) if len(p) == 4 else tuple(p) + (255,)
        # full art: the figure fills the card, feet at the bottom edge (the frame crops a little)
        card.paste(art, (W - rw) // 2, H - rh - 1)
    # the pale border and plum outline, like the jester card
    for y in range(H):
        for x in range(W):
            if not card_mask(x, y):
                card.px[y][x] = CLEAR
                continue
            d = min(x, y, W - 1 - x, H - 1 - y)
            if d == 0:
                card.px[y][x] = OUTLINE
            elif d == 1:
                card.px[y][x] = RIM_LIGHT
            elif d == 2:
                card.px[y][x] = RIM_MID if (x >= W - 3 or y >= H - 3) else RIM_LIGHT
            elif d == 3:
                card.px[y][x] = OUTLINE
    for (x, y) in ((1, 1), (W - 2, 1), (1, H - 2), (W - 2, H - 2)):
        card.px[y][x] = OUTLINE
    return card


def save(canvas, name):
    path = os.path.join(OUT, name)
    write_png(path, canvas.w, canvas.h, canvas.rows())
    return path


def build_normal(swirl, frame, pips, icon, color, rank, max_rank):
    """A finished normal card as the game draws it (for the preview only)."""
    card = Canvas(W, H)
    for y in range(H):
        for x in range(W):
            if swirl.px[y][x][3]:
                card.px[y][x] = tint(swirl.px[y][x], color)
    card.paste(frame, 0, 0)
    for i in range(max_rank):
        on = i < rank
        for y in range(5):
            for x in range(5):
                p = pips.px[y][x + (0 if on else 5)]
                if p[3]:
                    card.put(6 + i * 6 + x, 6 + y, p)
    card.paste(icon, 7, 14)
    return card


def build_atlas():
    """CardArt.png (one 34 px centre icon cell per card, see framed) and CardArt.txt (cell -> card asset name)."""
    cols = 4
    entries = ICONS + MENU_ICONS + BLUE_ICONS + CH1_ICONS + UI_ICONS
    entries = entries + raven_only_icons(entries)
    rows_n = (len(entries) + cols - 1) // cols
    atlas = Canvas(cols * ICON_CELL, rows_n * ICON_CELL)
    icons = {}
    lines = []
    for i, (name, fn) in enumerate(entries):
        icon = framed(raven_icon(RAVEN_PICKS[name]) if name in RAVEN_PICKS else fn())
        icons[name] = icon
        atlas.paste(icon, (i % cols) * ICON_CELL, (i // cols) * ICON_CELL)
        lines.append('%d %s' % (i, name))
    save(atlas, 'CardArt.png')
    with open(os.path.join(OUT, 'CardArt.txt'), 'w', newline='\n') as f:
        f.write('# cell (left to right, top to bottom, %d px, %d per row) and card asset name; written by Tools/CardForge\n' % (ICON_CELL, cols))
        f.write('\n'.join(lines) + '\n')
    return entries, icons


def main():
    os.makedirs(OUT, exist_ok=True)
    if '--types-only' in sys.argv:   # just CardTypes.png (the six 7x7 type icons)
        save(make_types(), 'CardTypes.png')
        return
    swirl, frame, pips = make_swirl(), make_frame(), make_pips()
    save(swirl, 'CardSwirl.png')
    save(frame, 'CardFrame.png')
    save(pips, 'CardPips.png')
    save(make_types(), 'CardTypes.png')

    entries, icons = build_atlas()

    ults = []
    for asset, char, color in ULT_OWNERS:
        # The crew share one in-game model in different colours, so their Ult cards use that battle sprite (the same
        # 2x bust as the enemies) rather than the old card reference art.
        u = make_ult(char, color, sprite_art(char))
        save(u, 'Ult_%s.png' % asset)
        ults.append(u)
    for asset, char, color, extra in ULT_CARDS:
        u = make_ult(char, color, sprite_art(char))
        if extra == 'bolts':
            add_bolts(u)
        save(u, 'Ult_%s.png' % asset)
        ults.append(u)
    for asset, folder, color in ULT_ENEMIES:
        art = sprite_art(folder)
        if art is None:
            continue
        u = make_ult(folder, color, art)
        save(u, 'Ult_%s.png' % asset)
        ults.append(u)

    if '--preview' in sys.argv:
        out = sys.argv[sys.argv.index('--preview') + 1]
        owners = {'MC': (0.92, 0.92, 0.95), 'Ashe': (0.5, 0.86, 0.45), 'Lucy': (0.72, 0.45, 0.95),
                  'Enemy': (0.66, 0.46, 0.3)}
        cards = []
        owners['Menu'] = (0.6, 0.55, 0.75)
        owners['Blue'] = (0.45, 0.65, 1.0)
        owners['Status'] = (0.45, 0.65, 1.0)
        owners['MC'] = (0.92, 0.92, 0.95)
        for name, _ in entries:
            owner = name.split('_')[1]
            cards.append(build_normal(swirl, frame, pips, icons[name], owners.get(owner, owners['Enemy']), 2, 3))
        cards += ults
        per_row = 6
        scale = 3
        gap = 4
        rows_p = (len(cards) + per_row - 1) // per_row
        sheet = Canvas(per_row * (W + gap) + gap, rows_p * (H + gap) + gap, (0x26, 0x24, 0x1F, 255))
        for i, card in enumerate(cards):
            sheet.paste(card, gap + (i % per_row) * (W + gap), gap + (i // per_row) * (H + gap))
        big = [[sheet.px[y // scale][x // scale] for x in range(sheet.w * scale)] for y in range(sheet.h * scale)]
        write_png(out, sheet.w * scale, sheet.h * scale, big)
        print('preview:', out)
    print('wrote', OUT)


if __name__ == '__main__':
    main()
