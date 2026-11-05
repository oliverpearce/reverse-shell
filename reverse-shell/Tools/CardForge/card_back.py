"""The card back: original pixel art drawn here (no third-party art), 48 x 64, with a gem in the middle.

    py Tools/CardForge/card_back.py

Writes Resources/Cards/CardBack.png (the back) and CardBackGem.png (a pale gem the game tints in the pack's resonance
colour; CardView.BuildBack). The look matches the game's ink-and-gold UI (InkStyle):
  - a deep ink field with a faint diamond lattice;
  - a gold border with a dark rim, and a thin inner rule with diamond knots at the corners and the middle of each side;
  - a ring of short gold rays round the centre, where the gold ring holds the gem.
It used to refit Stars in a Box's Major Arcana back; that was replaced (2026-11-04) because its terms rule out AI
projects. Everything here is drawn from code, so it's free to share with the repo.
"""
import math
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Cards')
W, H = 48, 64
CX, CY = 23.5, 31.5                   # the centre (between the middle pixels)
RING = 6.2

INK = (10, 10, 10, 255)
FIELD = (27, 24, 30, 255)             # ink with a little plum
FIELD_DARK = (19, 17, 22, 255)
LATTICE = (40, 35, 44, 255)
RIM = (6, 5, 7, 255)
GOLD = (255, 208, 128, 255)
GOLD_MID = (194, 160, 97, 255)
GOLD_DARK = (120, 92, 48, 255)


def put(img, x, y, c):
    if 0 <= x < W and 0 <= y < H:
        img.putpixel((x, y), c)


def diamond(img, cx, cy, r, c, core=None):
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            if abs(x - cx) + abs(y - cy) <= r:
                put(img, x, y, c)
    if core is not None:
        put(img, cx, cy, core)


def main():
    back = Image.new('RGBA', (W, H), FIELD)

    # The field: darker towards the edges, with a faint diamond lattice (lines every 6 px both ways).
    for y in range(H):
        for x in range(W):
            edge = min(x, y, W - 1 - x, H - 1 - y)
            c = FIELD_DARK if edge < 6 else FIELD
            if (x + y) % 6 == 0 or (x - y) % 6 == 0:
                c = LATTICE
            back.putpixel((x, y), c)

    # Border: a dark rim, a gold line with a darker inner edge.
    for x in range(W):
        for y in range(H):
            edge = min(x, y, W - 1 - x, H - 1 - y)
            if edge == 0:
                back.putpixel((x, y), RIM)
            elif edge == 1:
                back.putpixel((x, y), GOLD)
            elif edge == 2:
                back.putpixel((x, y), GOLD_DARK)
            elif edge == 3:
                back.putpixel((x, y), RIM)
    # The rounded-off outer corners.
    for cx, cy in [(0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1)]:
        back.putpixel((cx, cy), (0, 0, 0, 0))

    # The inner rule, 6 px in, with knots at the corners and the middle of each side.
    inset = 6
    for x in range(inset, W - inset):
        put(back, x, inset, GOLD_MID)
        put(back, x, H - 1 - inset, GOLD_MID)
    for y in range(inset, H - inset):
        put(back, inset, y, GOLD_MID)
        put(back, W - 1 - inset, y, GOLD_MID)
    knots = [(inset, inset), (W - 1 - inset, inset), (inset, H - 1 - inset), (W - 1 - inset, H - 1 - inset),
             (W // 2, inset), (W // 2 - 1, inset), (W // 2, H - 1 - inset), (W // 2 - 1, H - 1 - inset),
             (inset, H // 2), (inset, H // 2 - 1), (W - 1 - inset, H // 2), (W - 1 - inset, H // 2 - 1)]
    for kx, ky in knots[:4]:
        diamond(back, kx, ky, 2, GOLD, RIM)
    for kx, ky in knots[4:]:
        diamond(back, kx, ky, 1, GOLD)

    # A ring of rays round the gem: 16 short spokes, alternately long and short.
    for i in range(16):
        a = i / 16.0 * math.tau
        long_ray = i % 2 == 0
        for r10 in range(int((RING + 2.0) * 10), int((RING + (7.0 if long_ray else 4.5)) * 10), 5):
            r = r10 / 10.0
            x = int(round(CX + math.cos(a) * r))
            y = int(round(CY + math.sin(a) * r * 1.15))
            put(back, x, y, GOLD_MID if long_ray else GOLD_DARK)
    # Small diamonds above and below, on the long axis.
    diamond(back, W // 2, 14, 2, GOLD_MID, GOLD)
    diamond(back, W // 2 - 1, 14, 2, GOLD_MID, GOLD)
    diamond(back, W // 2, H - 15, 2, GOLD_MID, GOLD)
    diamond(back, W // 2 - 1, H - 15, 2, GOLD_MID, GOLD)

    # The middle: clear inside a gold ring, where the gem goes.
    gem = Image.new('RGBA', (W, H))
    for y in range(H):
        for x in range(W):
            d = math.hypot(x - CX, y - CY)
            if d < RING - 0.5:
                back.putpixel((x, y), INK)
            elif d < RING + 0.5:
                back.putpixel((x, y), GOLD)
            elif d < RING + 1.2:
                back.putpixel((x, y), RIM)
            # A diamond gem, lit from the top left: pale greys so the tint shows (white = full colour).
            dx, dy = abs(x - CX), abs(y - CY)
            if dx + dy <= 4.0:
                edge = dx + dy > 3.0
                lit = (x - CX) + (y - CY) < 0
                v = 120 if edge else (255 if lit else 185)
                if dx < 0.6 and y < CY - 1.5: v = 255   # a highlight down the top facet
                gem.putpixel((x, y), (v, v, v, 255))
    os.makedirs(OUT, exist_ok=True)
    back.save(os.path.join(OUT, 'CardBack.png'))
    gem.save(os.path.join(OUT, 'CardBackGem.png'))
    print('wrote CardBack.png and CardBackGem.png (%dx%d)' % (W, H))


if __name__ == '__main__':
    main()
