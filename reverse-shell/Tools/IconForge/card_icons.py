"""
IconForge / card icons: paints the card-type icon atlas (our own art, drawn from simple shapes; no third-party assets).

    python Tools/IconForge/card_icons.py [--preview out.png]

Writes Assets/_Project/Resources/UI/CardIcons.png: a 6 x 6 grid of 128 px cells. Shapes are drawn smooth
(4x supersampling), then turned into chunky pixel art (a 32 x 32 grid per cell, plum outline, edge lighting) to
match the chibi sprites (pixelate).
The layout is mirrored by Combat.View.CardIcons (keep the two in sync):

    columns (types):  0 Attack   1 Debuff   2 Buff   3 Counter   4 Heal   5 Ultimate
    row 0  glyph masks, white: tint them in code (any color)
    row 1  glyphs in their own color
    row 2  mini intent cards, rank I   (cream)
    row 3  mini intent cards, rank II  (silver)
    row 4  mini intent cards, rank III (gold)      (the Ultimate column is the same dark arch card in rows 2-4)
    row 5  UI bits: 0 empty diamond frame, 1 Ult pip (empty), 2 Ult pip (full), 3 star, 4 filled diamond, 5 card back

Flat symbols on small printed cards, with white / silver / gold faces for the ranks. The symbols are our own:
a katana (Attack), hexagons with chevrons down / up (Debuff / Buff), a cog with a reflecting arrow
(Counter), a seedling with a plus (Heal) and a radiant core star (Ultimate).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'SpriteForge'))
from pngtool import write_png  # noqa: E402

CELL, COLS, ROWS, SS = 128, 6, 6, 4
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'UI', 'CardIcons.png')
N = CELL * SS


def hx(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


TYPE_COLORS = [hx('#B4302B'), hx('#6A4A9E'), hx('#3C6DB5'), hx('#B08528'), hx('#4C9868'), hx('#3FB2A6')]
INK = hx('#1B1A18')


# ---------------------------------------------------------------------------------------------------- masks

class Mask:
    """A supersampled coverage mask (1 bit per subsample). Shapes are given in cell units (0..128, y down)
    through an optional transform, filled with scanlines, and can add (1) or cut (0)."""

    def __init__(self, k=1.0, cx=64.0, cy=64.0):
        self.bits = [bytearray(N) for _ in range(N)]
        self.k, self.cx, self.cy = k, cx, cy

    def tf(self, x, y):
        return ((self.cx + (x - 64.0) * self.k) * SS, (self.cy + (y - 64.0) * self.k) * SS)

    def poly(self, pts, value=1):
        pts = [self.tf(x, y) for x, y in pts]
        ys = [p[1] for p in pts]
        y0, y1 = max(0, int(min(ys))), min(N - 1, int(max(ys)) + 1)
        n = len(pts)
        for y in range(y0, y1 + 1):
            yc = y + 0.5
            xs = []
            for i in range(n):
                ax, ay = pts[i]
                bx, by = pts[(i + 1) % n]
                if (ay <= yc < by) or (by <= yc < ay):
                    xs.append(ax + (yc - ay) * (bx - ax) / (by - ay))
            xs.sort()
            row = self.bits[y]
            for j in range(0, len(xs) - 1, 2):
                a, b = max(0, int(math.ceil(xs[j] - 0.5))), min(N - 1, int(math.floor(xs[j + 1] - 0.5)))
                if b >= a:
                    row[a:b + 1] = bytes([value]) * (b - a + 1)

    def circle(self, x, y, r, value=1, steps=48):
        self.poly([(x + math.cos(t) * r, y + math.sin(t) * r) for t in (i * 2 * math.pi / steps for i in range(steps))], value)

    def rect(self, x0, y0, x1, y1, value=1):
        self.poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], value)

    def rounded(self, x0, y0, x1, y1, r, value=1):
        pts = []
        for cx, cy, a0 in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
            for i in range(9):
                t = math.radians(a0 + i * 90 / 8)
                pts.append((cx + math.cos(t) * r, cy + math.sin(t) * r))
        self.poly(pts, value)

    def band(self, x0, y0, x1, y1, w, value=1):
        """A straight stroke of width w."""
        dx, dy = x1 - x0, y1 - y0
        L = math.hypot(dx, dy) or 1
        nx, ny = -dy / L * w / 2, dx / L * w / 2
        self.poly([(x0 + nx, y0 + ny), (x1 + nx, y1 + ny), (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)], value)

    def coverage(self):
        out = [[0.0] * CELL for _ in range(CELL)]
        inv = 1.0 / (SS * SS)
        for y in range(CELL):
            rows = self.bits[y * SS:(y + 1) * SS]
            o = out[y]
            for x in range(CELL):
                s = 0
                a = x * SS
                for r in rows:
                    s += sum(r[a:a + SS])
                o[x] = s * inv
        return out


# ---------------------------------------------------------------------------------------------------- glyphs

def ring(m, x, y, r_out, r_in, value=1, steps=64):
    m.circle(x, y, r_out, value, steps)
    m.circle(x, y, r_in, 1 - value, steps)


def hexagon(m, r, value=1, cx=64, cy=64):
    m.poly([(cx + math.cos(math.radians(90 + 60 * i)) * r, cy - math.sin(math.radians(90 + 60 * i)) * r) for i in range(6)], value)


def chevron(m, cy, up, w=26, h=16, t=9, value=1):
    """A thick chevron centred at (64, cy), pointing up or down."""
    d = -1 if up else 1
    m.poly([(64 - w, cy - d * h / 2), (64, cy + d * h / 2), (64 + w, cy - d * h / 2),
            (64 + w, cy - d * h / 2 - d * t), (64, cy + d * h / 2 - d * t), (64 - w, cy - d * h / 2 - d * t)], value)


def glyph_attack(m):
    """A katana, drawn diagonally and bold enough to read small: a gently curved blade with a cut edge line, a
    round guard (tsuba) and a wrapped grip with diamond gaps."""
    def spine(t):
        bow = 6 * math.sin(t * math.pi)
        return 44 + 70 * t - bow * 0.7, 84 - 72 * t - bow * 0.7
    back, edge = [], []
    for i in range(21):
        t = i / 20
        x, y = spine(t)
        w = 11 * (1 - t) + 2.0
        back.append((x - w * 0.7, y - w * 0.7))
        edge.append((x + w * 0.7, y + w * 0.7))
    m.poly(back + [(118, 8)] + edge[::-1])
    for i in range(12):   # the edge line (hamon), cut into the blade
        a0, a1 = spine(0.1 + i / 18), spine(0.1 + (i + 1) / 18)
        m.band(a0[0] + 3, a0[1] + 3, a1[0] + 3, a1[1] + 3, 2.4, 0)
    m.circle(41, 87, 16)        # tsuba
    m.circle(41, 87, 5.5, 0)
    m.band(36, 92, 10, 118, 16)  # grip
    for k in range(3):
        cx, cy = 29 - k * 8, 99 + k * 8
        m.poly([(cx, cy - 4.5), (cx + 4.5, cy), (cx, cy + 4.5), (cx - 4.5, cy)], 0)


def glyph_debuff(m):
    """A hexagon outline with two chevrons pointing down."""
    hexagon(m, 58)
    hexagon(m, 48, 0)
    chevron(m, 52, False)
    chevron(m, 78, False)


def glyph_buff(m):
    """A hexagon outline with two chevrons pointing up (the mirror of Debuff)."""
    hexagon(m, 58)
    hexagon(m, 48, 0)
    chevron(m, 52, True)
    chevron(m, 78, True)


def glyph_counter(m):
    """A cog with a reflecting arrow through its hub: hits come back."""
    for i in range(10):
        a = math.radians(i * 36)
        x, y = 64 + math.cos(a) * 50, 64 + math.sin(a) * 50
        m.band(64 + math.cos(a) * 36, 64 + math.sin(a) * 36, x, y, 15)
    m.circle(64, 64, 44)
    m.circle(64, 64, 32, 0)
    # A bent arrow: in from the left, turning back out to the upper left.
    m.band(40, 72, 68, 72, 9)
    m.band(68, 72, 68, 50, 9)
    m.band(68, 50, 52, 50, 9)
    m.poly([(36, 50), (54, 38), (54, 62)])


def glyph_heal(m):
    """A seedling: a curved stem with two leaves, and a small plus beside it."""
    for i in range(10):
        t0, t1 = i / 10, (i + 1) / 10
        m.band(56 + 6 * math.sin(t0 * 3), 118 - 64 * t0, 56 + 6 * math.sin(t1 * 3), 118 - 64 * t1, 7)
    def leaf(x0, y0, x1, y1, bulge):
        dx, dy = x1 - x0, y1 - y0
        L = math.hypot(dx, dy)
        nx, ny = -dy / L, dx / L
        pts = []
        for i in range(17):
            t = i / 16
            b = math.sin(t * math.pi) * bulge
            pts.append((x0 + dx * t + nx * b, y0 + dy * t + ny * b))
        for i in range(17):
            t = 1 - i / 16
            b = math.sin(t * math.pi) * bulge * 0.35
            pts.append((x0 + dx * t - nx * b, y0 + dy * t - ny * b))
        m.poly(pts)
    leaf(60, 62, 18, 34, 16)
    leaf(60, 56, 106, 20, -18)
    m.rect(90, 70, 100, 104)
    m.rect(78, 82, 112, 92)


def glyph_ultimate(m):
    """A radiant core: an eight-point star inside a ring."""
    ring(m, 64, 64, 60, 51)
    pts = []
    for k in range(16):
        r = 46 if k % 2 == 0 else 15
        if k % 4 == 2:
            r = 30
        a = -math.pi / 2 + k * math.pi / 8
        pts.append((64 + math.cos(a) * r, 64 + math.sin(a) * r))
    m.poly(pts)
    m.circle(64, 64, 8, 0)


GLYPHS = [glyph_attack, glyph_debuff, glyph_buff, glyph_counter, glyph_heal, glyph_ultimate]


# ---------------------------------------------------------------------------------------------------- compositing

def blank():
    return [[(0.0, 0.0, 0.0, 0.0)] * CELL for _ in range(CELL)]


def comp(cell, cov, color):
    """Paints color (an (r,g,b) or a function y -> (r,g,b)) over the cell with the given coverage."""
    for y in range(CELL):
        c = color(y) if callable(color) else color
        row, crow = cell[y], cov[y]
        for x in range(CELL):
            a = crow[x]
            if a <= 0.0:
                continue
            r, g, b, da = row[x]
            oa = a + da * (1 - a)
            if oa <= 0:
                continue
            row[x] = ((c[0] * a + r * da * (1 - a)) / oa, (c[1] * a + g * da * (1 - a)) / oa, (c[2] * a + b * da * (1 - a)) / oa, oa)


def glyph_cov(t, k=1.0, cx=64.0, cy=64.0):
    m = Mask(k, cx, cy)
    GLYPHS[t](m)
    return m.coverage()


def lerp(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


RANK_FACES = [
    (lambda y: lerp(hx('#F4EEDF'), hx('#E2D8C2'), y / CELL), hx('#3A342C')),   # I: cream
    (lambda y: lerp(hx('#E6EAF0'), hx('#9EA8B8'), y / CELL), hx('#4A5262')),   # II: silver
    (lambda y: lerp(hx('#F6E2A0'), hx('#C4923A'), y / CELL), hx('#6E5020')),   # III: gold
]


def mini_card(t, rank):
    """A small intent card: a rounded card face with a hairline inner frame and the colored glyph."""
    cell = blank()
    x0, y0, x1, y1 = 20, 6, 108, 122
    if t == 5:  # Ultimate: a dark card with an arched top and a teal rim
        rim, face = Mask(), Mask()
        rim.rounded(x0, y0 + 26, x1, y1, 8)
        rim.circle(64, y0 + 44, 44)
        rim.rect(x0, y0 + 44, x1, y1 - 8)
        face.rounded(x0 + 3, y0 + 29, x1 - 3, y1 - 3, 6)
        face.circle(64, y0 + 44, 41)
        face.rect(x0 + 3, y0 + 44, x1 - 3, y1 - 11)
        comp(cell, rim.coverage(), TYPE_COLORS[5])
        comp(cell, face.coverage(), lambda y: lerp(hx('#22303A'), hx('#0F1418'), y / CELL))
        comp(cell, glyph_cov(5, 0.66, 64, 74), TYPE_COLORS[5])
        return cell
    face_color, border = RANK_FACES[rank]
    outer, face, frame = Mask(), Mask(), Mask()
    outer.rounded(x0, y0, x1, y1, 7)
    face.rounded(x0 + 3, y0 + 3, x1 - 3, y1 - 3, 5)
    frame.rounded(x0 + 8, y0 + 8, x1 - 8, y1 - 8, 3)
    frame.rounded(x0 + 9.5, y0 + 9.5, x1 - 9.5, y1 - 9.5, 2, 0)
    comp(cell, outer.coverage(), border)
    comp(cell, face.coverage(), face_color)
    comp(cell, frame.coverage(), lerp(border, (1, 1, 1), 0.35))
    comp(cell, glyph_cov(t, 0.72, 64, 60), TYPE_COLORS[t])
    # A thin baseline under the glyph, like a printed card.
    base = Mask()
    base.rect(44, 104, 84, 105.5)
    comp(cell, base.coverage(), lerp(border, (1, 1, 1), 0.2))
    return cell


def misc(i):
    cell = blank()
    m = Mask()
    white = (1.0, 1.0, 1.0)
    if i == 0:      # empty diamond frame (a slot for unit-type icons)
        m.poly([(64, 6), (122, 64), (64, 122), (6, 64)])
        m.poly([(64, 16), (112, 64), (64, 112), (16, 64)], 0)
        m.poly([(64, 26), (102, 64), (64, 102), (26, 64)])
        m.poly([(64, 29), (99, 64), (64, 99), (29, 64)], 0)
    elif i == 1:    # Ult pip, empty: a diamond outline
        m.poly([(64, 14), (114, 64), (64, 114), (14, 64)])
        m.poly([(64, 30), (98, 64), (64, 98), (30, 64)], 0)
    elif i == 2:    # Ult pip, full
        m.poly([(64, 14), (114, 64), (64, 114), (14, 64)])
    elif i == 3:    # star
        pts = []
        for k in range(10):
            r = 56 if k % 2 == 0 else 23
            t = -math.pi / 2 + k * math.pi / 5
            pts.append((64 + math.cos(t) * r, 66 + math.sin(t) * r))
        m.poly(pts)
    elif i == 4:    # filled diamond
        m.poly([(64, 6), (122, 64), (64, 122), (6, 64)])
    else:           # card back: an ink card with a gold hairline and a small diamond
        m.rounded(20, 6, 108, 122, 7)
        comp(cell, m.coverage(), INK)
        f = Mask()
        f.rounded(28, 14, 100, 114, 4)
        f.rounded(30, 16, 98, 112, 3, 0)
        f.poly([(64, 46), (82, 64), (64, 82), (46, 64)])
        f.poly([(64, 52), (76, 64), (64, 76), (52, 64)], 0)
        comp(cell, f.coverage(), hx('#C2A061'))
        return cell
    comp(cell, m.coverage(), white)
    return cell


PIXEL = 4                      # every art pixel is 4 x 4 in the 128 px cell (a 32 x 32 pixel-art grid)
PLUM = (34 / 255.0, 14 / 255.0, 30 / 255.0)


def pixelate(cell):
    """Turns a smooth cell into pixel art in the chibi sprites' style: averages 4 x 4 blocks onto a 32 x 32 grid
    (hard alpha), lights top / left edges and darkens bottom / right ones, adds a dark plum outline, then scales
    back up with hard pixels."""
    n = CELL // PIXEL
    small = [[None] * n for _ in range(n)]
    for by in range(n):
        for bx in range(n):
            r = g = b = a = 0.0
            for y in range(by * PIXEL, (by + 1) * PIXEL):
                for x in range(bx * PIXEL, (bx + 1) * PIXEL):
                    pr, pg, pb, pa = cell[y][x]
                    r += pr * pa
                    g += pg * pa
                    b += pb * pa
                    a += pa
            if a / (PIXEL * PIXEL) >= 0.45:
                small[by][bx] = (r / a, g / a, b / a)
    shaded = [[None] * n for _ in range(n)]
    for y in range(n):
        for x in range(n):
            c = small[y][x]
            if c is None:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    if 0 <= x + dx < n and 0 <= y + dy < n and small[y + dy][x + dx] is not None:
                        shaded[y][x] = PLUM
                        break
                continue
            up = y > 0 and small[y - 1][x] is not None
            left = x > 0 and small[y][x - 1] is not None
            down = y < n - 1 and small[y + 1][x] is not None
            right = x < n - 1 and small[y][x + 1] is not None
            if not up or not left:
                c = lerp(c, (1.0, 1.0, 1.0), 0.22)
            elif not down or not right:
                c = lerp(c, (0.0, 0.0, 0.0), 0.22)
            shaded[y][x] = c
    out = blank()
    for y in range(CELL):
        for x in range(CELL):
            c = shaded[y // PIXEL][x // PIXEL]
            if c is not None:
                out[y][x] = (c[0], c[1], c[2], 1.0)
    return out


def build():
    atlas = [[(0, 0, 0, 0)] * (CELL * COLS) for _ in range(CELL * ROWS)]

    def put(col, row, cell):
        for y in range(CELL):
            arow = atlas[row * CELL + y]
            for x in range(CELL):
                r, g, b, a = cell[y][x]
                arow[col * CELL + x] = (int(r * 255 + 0.5), int(g * 255 + 0.5), int(b * 255 + 0.5), int(a * 255 + 0.5))

    for t in range(6):
        cov = glyph_cov(t, 0.92)
        white, colored = blank(), blank()
        comp(white, cov, (1.0, 1.0, 1.0))
        comp(colored, cov, TYPE_COLORS[t])
        put(t, 0, pixelate(white))
        put(t, 1, pixelate(colored))
        for rank in range(3):
            put(t, 2 + rank, pixelate(mini_card(t, rank)))
        put(t, 5, pixelate(misc(t)))
        print('  type', t, 'done')
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    write_png(OUT, CELL * COLS, CELL * ROWS, atlas)
    print('wrote', os.path.relpath(OUT, ROOT))
    return atlas


def preview(atlas, path):
    """The atlas on a dark background (transparent areas show as ink), at 1x."""
    bg = (34, 33, 31)
    out = []
    for row in atlas:
        line = []
        for r, g, b, a in row:
            k = a / 255.0
            line.append((int(r * k + bg[0] * (1 - k)), int(g * k + bg[1] * (1 - k)), int(b * k + bg[2] * (1 - k)), 255))
        out.append(line)
    write_png(path, CELL * COLS, CELL * ROWS, out)


if __name__ == '__main__':
    atlas = build()
    if '--preview' in sys.argv:
        preview(atlas, sys.argv[sys.argv.index('--preview') + 1])
