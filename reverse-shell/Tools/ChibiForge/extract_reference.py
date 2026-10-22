"""
Turns an upscaled / compressed piece of pixel art into clean native pixel art (for Tools/ChibiForge/reference/).

    python Tools/ChibiForge/extract_reference.py in.png out.png --height 112 [--colors 32] [--bg auto]

1. Background (the corner colour, flood-filled from the edges) and the soft ground shadow become transparent.
2. The remaining colours are reduced to a small palette (k-means), so compression noise disappears.
3. The figure is resampled to the target height: each output pixel takes the most common palette colour of its
   block (not an average), which keeps edges and outlines hard.
Pure Python (pngtool), deterministic.
"""
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(HERE), 'SpriteForge'))
from pngtool import read_png, write_png  # noqa: E402


def dist(a, b):
    return (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2


def kmeans(samples, k, iters=12, seed=3):
    rng = random.Random(seed)
    centers = rng.sample(samples, k)
    for _ in range(iters):
        groups = [[] for _ in range(k)]
        for s in samples:
            groups[min(range(k), key=lambda i: dist(s, centers[i]))].append(s)
        for i, g in enumerate(groups):
            if g:
                centers[i] = tuple(sum(c[j] for c in g) / len(g) for j in range(3))
    return [tuple(int(round(v)) for v in c) for c in centers]


def extract(src, dst, height, colors=32):
    w, h, px = read_png(src)
    bg = px[2][2][:3]
    # 1. mask: background and the pale bluish ground shadow out
    def is_bg(p):
        return dist(p, bg) < 26 ** 2
    # flood fill from the border: only background connected to the edge is removed, so white clothing inside
    # the outlines stays
    opaque = [[True] * w for _ in range(h)]
    stack = [(x, y) for x in range(w) for y in (0, h - 1)] + [(x, y) for y in range(h) for x in (0, w - 1)]
    while stack:
        x, y = stack.pop()
        if not (0 <= x < w and 0 <= y < h) or not opaque[y][x] or not is_bg(px[y][x]):
            continue
        opaque[y][x] = False
        stack.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    # background trapped inside the silhouette (e.g. between a leg and a big arm): large background-coloured
    # patches in the lower half go too. White clothing up top and small highlights stay.
    fig_ys = [y for y in range(h) for x in range(0, w, 3) if opaque[y][x]]
    mid = (min(fig_ys) + max(fig_ys)) / 2.0
    seen = [[False] * w for _ in range(h)]
    for sy in range(h):
        for sx in range(w):
            if seen[sy][sx] or not opaque[sy][sx] or not is_bg(px[sy][sx]):
                continue
            comp, stack = [], [(sx, sy)]
            seen[sy][sx] = True
            while stack:
                x, y = stack.pop()
                comp.append((x, y))
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and opaque[ny][nx] and is_bg(px[ny][nx]):
                        seen[ny][nx] = True
                        stack.append((nx, ny))
            cy = sum(c[1] for c in comp) / len(comp)
            if len(comp) > 400 and cy > mid:
                for x, y in comp:
                    opaque[y][x] = False
    # the ground shadow: a pale, low-saturation blob under the feet
    ys = [y for y in range(h) for x in range(0, w, 3) if opaque[y][x]]
    bottom = max(ys)
    for y in range(int(bottom - 0.06 * h), h):
        for x in range(w):
            p = px[y][x]
            if opaque[y][x] and sum(p[:3]) > 3 * 150 and max(p[:3]) - min(p[:3]) < 60:
                opaque[y][x] = False
    xs = [x for y in range(h) for x in range(w) if opaque[y][x]]
    ys = [y for y in range(h) for x in range(w) if opaque[y][x]]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    # 2. palette
    rng = random.Random(1)
    samples = [px[y][x][:3] for y in range(y0, y1 + 1) for x in range(x0, x1 + 1) if opaque[y][x]]
    samples = rng.sample(samples, min(4000, len(samples)))
    pal = kmeans(samples, colors)
    # 3. resample by majority vote per block
    scale = (y1 - y0 + 1) / float(height)
    W = int(round((x1 - x0 + 1) / scale))
    out = [[(0, 0, 0, 0)] * W for _ in range(height)]
    cache = {}
    for oy in range(height):
        for ox in range(W):
            sx0, sx1 = x0 + ox * scale, x0 + (ox + 1) * scale
            sy0, sy1 = y0 + oy * scale, y0 + (oy + 1) * scale
            votes, total, solid = {}, 0, 0
            # sample the inner 60% of the block, away from blurry edges
            for sy in range(int(sy0 + 0.2 * scale), int(sy1 - 0.2 * scale) + 1):
                for sx in range(int(sx0 + 0.2 * scale), int(sx1 - 0.2 * scale) + 1):
                    if not (0 <= sx < w and 0 <= sy < h):
                        continue
                    total += 1
                    if not opaque[sy][sx]:
                        continue
                    solid += 1
                    c = px[sy][sx][:3]
                    i = cache.get(c)
                    if i is None:
                        i = min(range(len(pal)), key=lambda k: dist(c, pal[k]))
                        cache[c] = i
                    votes[i] = votes.get(i, 0) + 1
            if total and solid * 2 >= total:
                i = max(votes, key=votes.get)
                out[oy][ox] = pal[i] + (255,)
    write_png(dst, W, height, out)
    print('wrote', dst, W, 'x', height, 'from scale', round(scale, 2))


if __name__ == '__main__':
    a = sys.argv[1:]
    height = int(a[a.index('--height') + 1]) if '--height' in a else 112
    colors = int(a[a.index('--colors') + 1]) if '--colors' in a else 32
    extract(a[0], a[1], height, colors)
