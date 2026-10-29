"""The card back: Stars in a Box's Major Arcana back (46 x 81) refit to the game's 48 x 64 card, with a gem in the middle.

    py Tools/CardForge/card_back.py

Reads Art/ThirdParty/MajorArcana/cardBack_1x.png (credit: Stars in a Box, stars-in-a-box.itch.io; see the LICENSE-NOTE
there) and writes Resources/Cards/CardBack.png (the back, a gold ring cleared in its centre) and CardBackGem.png (a pale
gem the game tints in the pack's resonance colour; CardView.BuildBack). The height comes from dropping the middle rows
(the orb), the width from repeating the two centre columns, so the gold border and corners stay whole.
"""
import math
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
SRC = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'ThirdParty', 'MajorArcana', 'cardBack_1x.png')
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Cards')
W, H = 48, 64
KEEP_TOP, KEEP_BOTTOM_FROM = 32, 49   # rows 0-31 and 49-80 (the orb at 32-48 goes)
GOLD = (255, 208, 128, 255)
INK = (10, 10, 10, 255)
CX, CY = 23.5, 31.5                   # the centre (between the middle pixels)
RING = 6.2


def main():
    src = Image.open(SRC).convert('RGBA')
    sw, sh = src.size
    rows = list(range(KEEP_TOP)) + list(range(KEEP_BOTTOM_FROM, sh))
    assert len(rows) == H, len(rows)
    cols = list(range(24)) + [22, 23] + list(range(24, sw))
    assert len(cols) == W, len(cols)
    back = Image.new('RGBA', (W, H))
    for y, sy in enumerate(rows):
        for x, sx in enumerate(cols):
            back.putpixel((x, y), src.getpixel((sx, sy)))

    # The middle: clear the pattern inside a gold ring, where the gem goes.
    gem = Image.new('RGBA', (W, H))
    for y in range(H):
        for x in range(W):
            d = math.hypot(x - CX, y - CY)
            if d < RING - 0.5:
                back.putpixel((x, y), INK)
            elif d < RING + 0.5:
                back.putpixel((x, y), GOLD)
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
