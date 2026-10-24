"""
Chapter One's maps (bible §9): the City Ruins, the buried tunnel and the electric dragon's chamber, in the
OverworldMap legend. Prints C# string constants for WorldAreas.cs and checks every map:

    python Tools/MapForge/chapter1_maps.py            # checks + C# to stdout
    python Tools/MapForge/chapter1_maps.py --preview  # also the maps as text

Story legend (besides the usual tiles):  K procedure pod   1-3 memory fragments   4-7 records   O scanner
c loot chest (one Part each, WorldAreas Area.Chests in reading order)
Z story trigger (walkable)   V hidden route (a cracked wall until the scanner opens it)   W / J warps   U pedestal.
The checks: rectangular, one spawn, every warp / fighting square / trigger reachable from the spawn (with V open),
every solid story prop next to a reachable square, and V next to a reachable square while closed.
"""
import random
import sys
from collections import deque

SOLID = set('H#BLTbrnEARGCcKOU1234567V')


def grid(w, h, fill='.'):
    return [[fill for _ in range(w)] for _ in range(h)]


def rect(g, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= y < len(g) and 0 <= x < len(g[0]):
                g[y][x] = c


def border(g, c='H'):
    h, w = len(g), len(g[0])
    for x in range(w):
        g[0][x] = c
        g[h - 1][x] = c
    for y in range(h):
        g[y][0] = c
        g[y][w - 1] = c


# ---------------------------------------------------------------------------------------------- City Ruins

def city():
    W, H = 64, 42
    rng = random.Random(1999)
    g = grid(W, H)
    h_roads = [(9, 10), (20, 22), (32, 33)]
    v_roads = [(13, 14), (30, 32), (48, 49)]
    for y0, y1 in h_roads:
        rect(g, 1, y0, W - 2, y1, '=')
    for x0, x1 in v_roads:
        rect(g, x0, 1, x1, H - 2, '=')
    ys = [1] + [y for r in h_roads for y in (r[0] - 1, r[1] + 1)] + [H - 2]
    xs = [1] + [x for r in v_roads for x in (r[0] - 1, r[1] + 1)] + [W - 2]
    blocks = {}
    for by in range(0, len(ys), 2):
        for bx in range(0, len(xs), 2):
            blocks[(bx // 2, by // 2)] = (xs[bx], ys[by], xs[bx + 1], ys[by + 1])

    PARK, PLAZA, COURT, CLINIC = (2, 2), (1, 1), (0, 2), (3, 0)
    for key, (x0, y0, x1, y1) in blocks.items():
        if key == PARK:
            rect(g, x0, y0, x1, y1, ',')
            for _ in range(16):
                g[rng.randint(y0 + 1, y1 - 1)][rng.randint(x0 + 1, x1 - 1)] = 'T'
            for _ in range(14):
                g[rng.randint(y0, y1)][rng.randint(x0, x1)] = '*'
            for _ in range(6):
                g[rng.randint(y0, y1)][rng.randint(x0, x1)] = 'b'
        elif key == PLAZA:
            rect(g, x0, y0, x1, y1, 'P')
            for x in range(x0, x1 + 1, 3):
                g[y0][x] = 'T'
                g[y1][x] = 'T'
        elif key == COURT:
            # The walled courtyard where the protagonist wakes: quiet, flowers, one gate north and one south.
            rect(g, x0, y0, x1, y1, 'B')
            rect(g, x0 + 1, y0 + 1, x1 - 1, y1 - 1, 'P')
            for x in range(x0 + 2, x1 - 1, 3):
                g[y0 + 1][x] = '*'
            g[y1][(x0 + x1) // 2] = 'P'
            g[y0][(x0 + x1) // 2] = 'P'
        elif key == CLINIC:
            # A collapsed clinic: walls round a room with one gate south. The scanner is inside.
            rect(g, x0, y0, x1, y1, '.')
            rect(g, x0 + 1, y0 + 1, x1 - 1, y1, 'B')
            rect(g, x0 + 2, y0 + 2, x1 - 2, y1 - 1, '.')
        else:
            rect(g, x0 + 1, y0 + 1, x1 - 1, y1 - 1, rng.choice('H#H#B'))
            for _ in range(3):
                cx, cy = rng.randint(x0 + 1, x1 - 1), rng.randint(y0 + 1, y1 - 1)
                rect(g, cx - 1, cy - 1, cx + 1, cy + 1, rng.choice(['L', ',', 'B']))
            for _ in range(4):
                g[rng.randint(y0, y1)][rng.choice([x0, x1])] = rng.choice(['r', 'b', 'T', ','])

    for _ in range(90):
        x, y = rng.randint(1, W - 2), rng.randint(1, H - 2)
        if g[y][x] in '.=':
            g[y][x] = rng.choice([',', ',', '*', 'r', 'b', ',', 'T'])
    border(g)

    # --- The courtyard: wake beside the pod.
    kx0, ky0, kx1, ky1 = blocks[COURT]
    cx, cy = (kx0 + kx1) // 2, (ky0 + ky1) // 2
    g[cy][cx] = 'K'
    g[cy + 1][cx] = 'S'
    g[cy - 1][cx - 3] = '1'                         # first memory, right beside the pod
    g[ky1 - 1][kx0 + 2] = '4'                       # an evacuation notice on the courtyard wall
    # --- The plaza: an Access Point, a Reverse Shell poster, a memory.
    px0, py0, px1, py1 = blocks[PLAZA]
    pcx, pcy = (px0 + px1) // 2, (py0 + py1) // 2
    g[pcy][pcx] = 'A'
    g[py0 + 2][px0 + 2] = '5'
    g[pcy + 2][pcx + 4] = '2'
    # --- The park: a gardening machine (a small kindness), a memorial plaque.
    qx0, qy0, qx1, qy1 = blocks[PARK]
    rect(g, qx0 + 5, qy0 + 3, qx0 + 9, qy0 + 6, '*')
    g[qy0 + 4][qx0 + 7] = 'n'
    g[qy0 + 1][qx0 + 1] = '6'
    # --- The clinic: the scanner, its terminal, the last memory, and the gate the Collector waits at.
    lx0, ly0, lx1, ly1 = blocks[CLINIC]
    gx = (lx0 + lx1) // 2
    g[ly0 + 3][gx] = 'O'
    g[ly0 + 2][lx0 + 2] = '7'
    g[ly0 + 2][lx1 - 2] = '3'
    g[ly1][gx] = 'Z'                                # the gate: leaving with the scanner meets the Collector
    g[ly1 + 1][gx] = '='
    # --- The hidden route: a cracked wall on the NORTH side of the south street (a building face looking at the
    # camera, so it stands in full view), and a faint trace on the street leading up to it.
    vx, vy = 26, 30
    g[vy][vx] = 'V'
    g[vy + 1][vx] = '.'                             # the pavement in front of it
    for x in range(vx - 4, vx + 3):
        if g[32][x] in '=.,*rb':
            g[32][x] = ':'
    # --- Scrap Hound patrols (one fighting square each, south of the machine).
    for (ex, ey) in [(40, 21), (8, 21), (44, 32)]:
        g[ey][ex] = 'E'
        g[ey + 1][ex] = 'X'
        for dx, dy in ((0, 2), (1, 1), (-1, 1)):
            if g[ey + dy][ex + dx] not in 'HEXV:':
                g[ey + dy][ex + dx] = '='
    return g


# ---------------------------------------------------------------------------------------------- Buried tunnel

def tunnel():
    W, H = 44, 16
    g = grid(W, H, 'H')
    # A winding passage west to east: down from the city, along an old line, up to the chamber door.
    rect(g, 2, 2, 6, 6, '.')            # landing under the city (the way back up: W)
    rect(g, 4, 6, 6, 12, '.')
    rect(g, 4, 10, 24, 12, '=')         # the old line, rails grown over
    rect(g, 22, 4, 24, 12, '.')
    rect(g, 22, 4, 38, 6, '=')
    rect(g, 36, 4, 38, 12, '.')
    rect(g, 36, 10, 41, 12, '.')
    g[3][3] = 'W'                       # back to the city
    g[4][4] = 'S'
    g[11][41] = 'J'                     # on to the chamber
    g[10][39] = 'A'                     # an Access Point before the guardian
    g[2][5] = '4'                       # a maintenance log
    for (x, y) in [(5, 8), (10, 11), (16, 11), (23, 8), (30, 5)]:
        g[y][x] = ':'                   # the trace, leading on
    for (ex, ey) in [(14, 10), (31, 4)]:  # Wire Crawlers on the line
        g[ey][ex] = 'E'
        g[ey + 1][ex] = 'X'
    for (x, y) in [(8, 12), (20, 10), (27, 6), (37, 7)]:
        g[y][x] = 'r'
    return g


# ---------------------------------------------------------------------------------------------- Dragon's chamber

def chamber():
    W, H = 26, 18
    g = grid(W, H, 'H')
    rect(g, 3, 3, 22, 14, '.')
    rect(g, 11, 14, 14, 16, '.')        # the way in from the tunnel (south)
    g[16][12] = 'W'
    g[15][13] = 'S'
    rect(g, 4, 4, 21, 4, ',')           # moss along the back wall
    g[4][12] = 'U'                      # the pedestal with the blank Soul Card
    g[8][12] = 'E'                      # the guardian, in front of it
    g[9][12] = 'X'
    for x in range(3, 23):
        g[12][x] = 'Z'                  # crossing this line (wall to wall) wakes the guardian
    for (x, y) in [(4, 13), (21, 13), (4, 5), (21, 5), (7, 9), (18, 9)]:
        g[y][x] = 'r'
    return g


# ---------------------------------------------------------------------------------------------- Checks

def find(g, c):
    return [(x, y) for y, row in enumerate(g) for x, v in enumerate(row) if v == c]


def reach(g, start, open_v=True):
    solid = set(SOLID)
    if open_v:
        solid.discard('V')
    seen = {start}
    q = deque([start])
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= ny < len(g) and 0 <= nx < len(g[0]) and (nx, ny) not in seen and g[ny][nx] not in solid:
                seen.add((nx, ny))
                q.append((nx, ny))
    return seen


def check(name, g):
    w = len(g[0])
    assert all(len(r) == w for r in g), name + ': not rectangular'
    spawns = find(g, 'S')
    assert len(spawns) == 1, name + ': needs one spawn'
    closed = reach(g, spawns[0], open_v=False)
    opened = reach(g, spawns[0], open_v=True)
    for c in 'WJXZ':
        for cell in find(g, c):
            assert cell in opened, '%s: %s at %s unreachable' % (name, c, cell)
    for c in 'KO1234567UnAVc':
        for (x, y) in find(g, c):
            near = [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
            pool = closed if c == 'V' else opened
            assert any(p in pool for p in near), '%s: %s at %s has no reachable side' % (name, c, (x, y))
    return True


def csharp(const, g):
    bs = chr(92)
    lines = ['            "' + ''.join(r) + bs + 'n"' for r in g]
    return '        public const string ' + const + ' =\n' + ' +\n'.join(lines) + ';\n'


if __name__ == '__main__':
    maps = [('CityMap', city()), ('TunnelMap', tunnel()), ('ChamberMap', chamber())]
    for const, g in maps:
        check(const, g)
        if '--preview' in sys.argv:
            print('// ' + const)
            print('\n'.join(''.join(r) for r in g))
    for const, g in maps:
        print(csharp(const, g))
