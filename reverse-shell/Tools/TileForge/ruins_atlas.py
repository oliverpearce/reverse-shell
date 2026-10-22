"""
TileForge / ruins atlas: paints the overgrown-ruins tile atlas procedurally (our own art, no license strings).

    python Tools/TileForge/ruins_atlas.py [out.png]

Writes Assets/_Project/Art/Tiles/ruins.png: 8x5 cells of 16x16 art pixels drawn at 4x (64 px cells), in the same
layout WorldMeshBuilder and BackdropPainterCore expect (it is a drop-in for the old CyberCity city.png):

    row 0: (0) leaf canopy  (1) bark  (2) grass tuft*  (3) flower tuft*  (4) hanging vines*  (5) dirt  (6) moss  (7) cracked slab
    row 1: (1) cracked sidewalk          (4-7) mossy brick, 4 tints
    row 2: (0-3) concrete facade + empty window, 4 tints   (4-7) weathered concrete slab, 4 tints
    row 3: (0-3) ivy-covered facade, 4 tints               (4-7) grassy rooftop, 4 tints
    row 4: (0) overgrown road  (2) grass plaza with stepping stones   (4-7) ruined storefront, 4 tints
    * transparent sprites (cutout)

Deterministic: same script, same pixels. Uses pngtool.py from Tools/SpriteForge.
"""
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'SpriteForge'))
from pngtool import write_png  # noqa: E402

ART, SCALE, COLS, ROWS = 16, 4, 8, 5
CLEAR = (0, 0, 0, 0)


def hx(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c[:3]) + (c[3],)


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (255,)


# Concrete tints: neutral gray, sun-bleached beige, cool blue-gray, rust-stained.
TINTS = [hx('#8C8B85'), hx('#A59C88'), hx('#7E8A90'), hx('#977F74')]
MOSS = [hx('#2B4427'), hx('#46703A'), hx('#6E9C46'), hx('#A9C866')]
GRASS = [hx('#2F5426'), hx('#467A30'), hx('#6A9E3E'), hx('#9CC458')]
LEAF = [hx('#24452A'), hx('#356A34'), hx('#55913F'), hx('#8DBF57'), hx('#C3DE7A')]
BARK = [hx('#3A2A20'), hx('#54402E'), hx('#6E573F')]
DIRT = [hx('#4A3D2E'), hx('#5E4E3A'), hx('#75644B')]
RUST = [hx('#5A2E1C'), hx('#86482A'), hx('#B06A38')]
ASPHALT = [hx('#3A3B3E'), hx('#46474B'), hx('#525358')]
WINDOW = hx('#161B1E')


class Cell:
    def __init__(self, fill=CLEAR):
        self.px = [[fill] * ART for _ in range(ART)]

    def set(self, x, y, c):
        if 0 <= x < ART and 0 <= y < ART:
            self.px[y][x] = c

    def get(self, x, y):
        return self.px[y][x]


def noise_fill(cell, ramp, rng, weights):
    for y in range(ART):
        for x in range(ART):
            cell.set(x, y, rng.choices(ramp, weights)[0])


def moss_patch(cell, rng, cx, cy, r, density=0.75):
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            d = ((x - cx) ** 2 + (y - cy) ** 2) ** 0.5 / max(1, r)
            if d <= 1 and rng.random() < density * (1.15 - d):
                cell.set(x, y, MOSS[min(3, int(rng.random() * 2 + (1 - d) * 2))])


def grass_blades(cell, rng, count, base_y=ART - 1, height=(3, 7), ramp=GRASS):
    for _ in range(count):
        x = rng.randrange(ART)
        h = rng.randint(*height)
        lean = rng.choice((-1, 0, 0, 1))
        for k in range(h):
            xx = x + (lean if k > h // 2 else 0)
            cell.set(xx, base_y - k, ramp[min(len(ramp) - 1, 1 + k * (len(ramp) - 1) // h)])


# ------------------------------------------------------------------ cells

def leaves(rng):
    c = Cell(LEAF[0])
    noise_fill(c, LEAF[:3], rng, [3, 4, 2])
    for _ in range(9):  # sunlit leaf clusters
        cx, cy = rng.randrange(ART), rng.randrange(ART)
        for dx, dy in ((0, 0), (1, 0), (0, 1), (-1, 0), (1, 1)):
            c.set(cx + dx, cy + dy, LEAF[3])
        c.set(cx, cy, LEAF[4])
        c.set(cx + 1, cy + 2, LEAF[0])
    return c


def bark(rng):
    c = Cell(BARK[1])
    for x in range(ART):
        col = BARK[0] if x % 4 == 0 else (BARK[2] if x % 4 == 2 else BARK[1])
        for y in range(ART):
            c.set(x, y, col if rng.random() < 0.85 else BARK[rng.randrange(3)])
    moss_patch(c, rng, 3, 13, 3, 0.5)
    return c


def grass_tuft(rng, flowers=False):
    c = Cell()
    grass_blades(c, rng, 14, height=(4, 12))
    if flowers:
        for _ in range(5):
            x, y = rng.randrange(2, 14), rng.randrange(4, 10)
            col = rng.choice((hx('#F2F0DC'), hx('#EAD56C'), hx('#B9D4F0')))
            for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, -1), (0, 1)):
                c.set(x + dx, y + dy, col)
            c.set(x, y, hx('#E8B040'))
    return c


def vines(rng):
    c = Cell()
    for x in range(1, ART, 3):
        x += rng.choice((-1, 0, 0, 1))
        length = rng.randint(6, ART)
        for y in range(length):
            c.set(x, y, MOSS[0] if y % 3 else MOSS[1])
            if rng.random() < 0.45:
                side = rng.choice((-1, 1))
                c.set(x + side, y, MOSS[2] if rng.random() < 0.6 else MOSS[3])
    for x in range(ART):  # dense top edge where the vines spill over
        for y in range(rng.randint(1, 3)):
            c.set(x, y, MOSS[rng.randint(1, 3)])
    return c


def dirt(rng):
    c = Cell()
    noise_fill(c, DIRT, rng, [2, 5, 2])
    for _ in range(4):
        x, y = rng.randrange(ART), rng.randrange(ART)
        c.set(x, y, shade(TINTS[0], 0.8))
        c.set(x + 1, y, shade(TINTS[0], 1.0))
    grass_blades(c, rng, 3, base_y=rng.randrange(8, 16), height=(2, 3))
    return c


def moss(rng):
    c = Cell()
    noise_fill(c, MOSS, rng, [2, 5, 4, 1])
    return c


def slab(rng, base, cracks=True, moss_amount=2):
    c = Cell()
    for y in range(ART):
        for x in range(ART):
            k = 0.92 + rng.random() * 0.1
            c.set(x, y, shade(base, k))
    if cracks:
        x, y = rng.randrange(3, 13), 0
        while y < ART:
            c.set(x, y, shade(base, 0.55))
            y += 1
            x += rng.choice((-1, 0, 1))
    for _ in range(moss_amount):
        moss_patch(c, rng, rng.randrange(ART), rng.randrange(ART), rng.randint(1, 3), 0.6)
    return c


def sidewalk(rng):
    c = slab(rng, TINTS[0], cracks=True, moss_amount=0)
    for i in range(ART):  # slab seams, overgrown
        for x, y in ((i, 7), (i, 15), (7, i), (15, i)):
            c.set(x, y, MOSS[1] if rng.random() < 0.6 else shade(TINTS[0], 0.6))
    grass_blades(c, rng, 3, base_y=7, height=(1, 3))
    grass_blades(c, rng, 3, base_y=15, height=(1, 3))
    return c


def brick(rng, tint):
    c = Cell()
    mortar = shade(tint, 0.62)
    for y in range(ART):
        for x in range(ART):
            row = y // 4
            off = 4 if row % 2 else 0
            edge = y % 4 == 3 or (x + off) % 8 == 7
            if edge:
                c.set(x, y, mortar)
            else:
                k = 0.85 + ((x + off) // 8 * 37 + row * 11) % 5 * 0.04
                warm = mix(tint, RUST[2], 0.25)
                c.set(x, y, shade(warm, k + rng.random() * 0.05))
    for _ in range(2):
        moss_patch(c, rng, rng.randrange(ART), rng.randrange(8, ART), rng.randint(2, 3), 0.7)
    return c


def facade(rng, tint, ivy):
    c = slab(rng, tint, cracks=False, moss_amount=0)
    # Empty window: dark interior, a lighter sill and a cracked frame.
    for y in range(3, 12):
        for x in range(3, 13):
            c.set(x, y, WINDOW if rng.random() < 0.93 else shade(WINDOW, 1.8))
    for x in range(2, 14):
        c.set(x, 12, shade(tint, 1.15))
        c.set(x, 2, shade(tint, 0.7))
    for y in range(2, 13):
        c.set(2, y, shade(tint, 0.75))
        c.set(13, y, shade(tint, 0.75))
    for _ in range(3):  # broken glass shards in the corners
        c.set(rng.choice((3, 12)), rng.randrange(3, 6), hx('#7FA0A8'))
    # Rain streaks below the sill.
    for x in range(3, 13, 3):
        for y in range(13, ART):
            c.set(x, y, shade(tint, 0.82))
    if ivy:
        for x in range(ART):
            length = rng.randint(4, ART) if rng.random() < 0.85 else 2
            for y in range(length):
                if rng.random() < 0.85:
                    c.set(x, y, MOSS[min(3, rng.randint(0, 2) + (1 if y < 3 else 0))])
    else:
        vine_x = rng.randrange(1, 15)
        for y in range(rng.randint(5, 14)):
            c.set(vine_x, y, MOSS[1])
            if rng.random() < 0.5:
                c.set(vine_x + rng.choice((-1, 1)), y, MOSS[2])
    return c


def rooftop(rng, tint):
    c = Cell()
    noise_fill(c, GRASS, rng, [2, 5, 4, 1])
    for _ in range(2):
        moss_patch(c, rng, rng.randrange(ART), rng.randrange(ART), 2, 0.8)
    for i in range(ART):  # crumbling concrete parapet
        if rng.random() < 0.7:
            c.set(i, 0, shade(tint, 0.95))
        if rng.random() < 0.7:
            c.set(0, i, shade(tint, 0.95))
    return c


def road(rng):
    c = Cell()
    noise_fill(c, ASPHALT, rng, [2, 5, 2])
    for x in range(1, ART, 4):  # faded lane dashes
        for xx in (x, x + 1):
            if rng.random() < 0.7:
                c.set(xx, 8, hx('#A8A184'))
    x, y = rng.randrange(ART), 0
    while y < ART:  # long crack full of grass
        c.set(x, y, GRASS[1])
        if rng.random() < 0.5:
            c.set(x + 1, y, GRASS[2])
        y += 1
        x = (x + rng.choice((-1, 0, 1))) % ART
    moss_patch(c, rng, rng.randrange(ART), rng.randrange(ART), 2, 0.6)
    return c


def plaza(rng):
    c = Cell()
    noise_fill(c, GRASS, rng, [1, 5, 4, 1])
    for sx, sy in ((2, 2), (9, 4), (4, 10), (11, 11)):  # stepping stones
        for y in range(sy, sy + 4):
            for x in range(sx, sx + 4):
                corner = (x in (sx, sx + 3)) and (y in (sy, sy + 3))
                if not corner:
                    c.set(x, y, shade(TINTS[1], 0.9 + rng.random() * 0.12))
        c.set(sx + 1, sy + 3, shade(TINTS[1], 0.65))
        c.set(sx + 2, sy + 3, shade(TINTS[1], 0.65))
    return c


def storefront(rng, tint):
    c = slab(rng, tint, cracks=False, moss_amount=0)
    for y in range(4, ART):  # rusted roll shutter, half open onto darkness
        for x in range(2, 14):
            if y < 11:
                c.set(x, y, RUST[1] if y % 2 else RUST[2] if rng.random() < 0.6 else RUST[0])
            else:
                c.set(x, y, WINDOW)
    for x in range(2, 14):
        c.set(x, 3, shade(tint, 0.7))
    for x in range(ART):  # rubble and weeds at the base
        if rng.random() < 0.5:
            c.set(x, ART - 1, shade(TINTS[0], 0.8))
    grass_blades(c, rng, 6, height=(2, 5))
    return c


def build():
    rng = random.Random(1999)
    cells = {}
    cells[(0, 0)] = leaves(rng)
    cells[(1, 0)] = bark(rng)
    cells[(2, 0)] = grass_tuft(rng)
    cells[(3, 0)] = grass_tuft(rng, flowers=True)
    cells[(4, 0)] = vines(rng)
    cells[(5, 0)] = dirt(rng)
    cells[(6, 0)] = moss(rng)
    cells[(7, 0)] = slab(rng, TINTS[1], moss_amount=3)
    cells[(0, 1)] = slab(rng, TINTS[0])
    cells[(1, 1)] = sidewalk(rng)
    cells[(2, 1)] = dirt(rng)
    cells[(3, 1)] = moss(rng)
    for i, t in enumerate(TINTS):
        cells[(4 + i, 1)] = brick(rng, t)
        cells[(i, 2)] = facade(rng, t, ivy=False)
        cells[(4 + i, 2)] = slab(rng, t)
        cells[(i, 3)] = facade(rng, t, ivy=True)
        cells[(4 + i, 3)] = rooftop(rng, t)
        cells[(4 + i, 4)] = storefront(rng, t)
    cells[(0, 4)] = road(rng)
    cells[(1, 4)] = road(rng)
    cells[(2, 4)] = plaza(rng)
    cells[(3, 4)] = plaza(rng)

    W, H = COLS * ART * SCALE, ROWS * ART * SCALE
    out = [[CLEAR] * W for _ in range(H)]
    for (col, row), cell in cells.items():
        for y in range(ART):
            for x in range(ART):
                p = cell.get(x, y)
                for sy in range(SCALE):
                    line = out[(row * ART + y) * SCALE + sy]
                    for sx in range(SCALE):
                        line[(col * ART + x) * SCALE + sx] = p
    return W, H, out


if __name__ == '__main__':
    path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Tiles', 'ruins.png')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    w, h, px = build()
    write_png(path, w, h, px)
    print('wrote', os.path.relpath(path, ROOT))
