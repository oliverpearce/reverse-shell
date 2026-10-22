"""
Builds a contact sheet of composed characters for quick visual checks.

    python Tools/SpriteForge/preview.py out.png Miku Teto Rei

Each row is one character: idle (down, right), then walk, slash and spellcast frames facing right.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pngtool import read_png, write_png  # noqa: E402

ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
CHARS = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')
F = 64
UP, LEFT, DOWN, RIGHT = 0, 1, 2, 3
BG = (34, 28, 46, 255)


def frames(name, anim, row, picks):
    w, h, px = read_png(os.path.join(CHARS, name, anim + '.png'))
    row = min(row, h // F - 1)
    out = []
    for c in picks:
        if c * F >= w:
            continue
        out.append([px[row * F + y][c * F:(c + 1) * F] for y in range(F)])
    return out


def main(dst, names, scale=3):
    rows = []
    for n in names:
        cells = frames(n, 'idle', DOWN, [0]) + frames(n, 'idle', RIGHT, [0]) + frames(n, 'walk', RIGHT, [1, 3, 5, 7]) \
            + frames(n, 'slash', RIGHT, [1, 3, 5]) + frames(n, 'spellcast', RIGHT, [2, 5])
        rows.append(cells)
    cols = max(len(r) for r in rows)
    W, H = cols * F * scale, len(rows) * F * scale
    out = [[BG] * W for _ in range(H)]
    for ri, cells in enumerate(rows):
        for ci, cell in enumerate(cells):
            for y in range(F):
                for x in range(F):
                    p = cell[y][x]
                    if p[3] < 128:
                        continue
                    for yy in range(scale):
                        orow = out[(ri * F + y) * scale + yy]
                        for xx in range(scale):
                            orow[(ci * F + x) * scale + xx] = p[:3] + (255,)
    write_png(dst, W, H, out)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2:])
