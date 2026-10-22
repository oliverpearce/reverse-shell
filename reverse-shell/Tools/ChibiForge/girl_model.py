"""
Girl model: every human-shaped sprite (the crew and the NPCs) from the user's 6-frame walk cycle,
reference/Girl_walk.png (six front-view frames, about 20 x 61 px each, 17 colours, side by side in 48 px cells:
frame 0 stands, frames 1-5 step).

    python Tools/ChibiForge/girl_model.py [Names...] [--sheet out.png]

Native 64 px frames, no scaling. Each frame is centred on the hair top and stands on the ground row, so the cycle
doesn't jitter.
    walk 7      frame 0 stands, then the drawn stepping frames 1-5 and back to the stance
    idle 2      the stance, then the upper body dipping a pixel (breathing)
    slash 6     a lunge with a pale slash arc in front
    spellcast 7 an orb grows at the hands
    hurt 6      recoil, sink, then lie on the ground (one row)
Directions: the art is front-facing only. Down, left and right use it (left / right lean a pixel toward the walk
direction); up is a back view (face and eyes painted over with hair).
Recolours: hair (5 tones), eyes (the crew's glow in their element colour), and for NPCs the outfit's near-blacks.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'SpriteForge'))
from pngtool import read_png, write_png  # noqa: E402

F = 64
GROUND = 61
CELL = 48
CHARS = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')
SRC = os.path.join(HERE, 'reference', 'Girl_walk.png')
UP, LEFT, DOWN, RIGHT = 0, 1, 2, 3
OUTLINE = (0x0A, 0x07, 0x06)


def hx(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def ramp(*hs):
    return [hx(h) for h in hs]


HAIR = [hx('#2F1612'), hx('#411F19'), hx('#533833'), hx('#703D35'), hx('#8A503F')]   # dark -> light
SKIN = {hx('#FCDCC8'), hx('#C39799'), hx('#D5BEB4')}
IRIS = hx('#C39799')
OUTFIT = [hx('#231F23'), hx('#342F33')]
TRIM = hx('#877473')

SPECS = {
    # The crew: android girls; glowing eyes in their colour, own hair.
    'MC': {'hair': ramp('#1C2234', '#26304A', '#34446A', '#4A5E8C', '#6A82B0'), 'eye': hx('#BFE6FF'), 'outfit': None},
    'HealBot': {'hair': ramp('#14261A', '#1C3624', '#2A5232', '#3E7444', '#5E9A58'), 'eye': hx('#8EF070'), 'outfit': None},
    'Lucy': {'hair': ramp('#24102E', '#341A44', '#4A265E', '#653A82', '#8A58AE'), 'eye': hx('#D07AFF'), 'outfit': None},
    # Ruin survivors: own hair, plain eyes, outfits in their own colours.
    'NoodleVendor': {'hair': ramp('#1A120E', '#2A1C14', '#3C2A1E', '#56402C', '#72563A'), 'eye': None,
                     'outfit': ramp('#5A2A1E', '#7A3C2A')},
    'Courier': {'hair': ramp('#2A1A10', '#3C2616', '#56381E', '#704C28', '#8E6436'), 'eye': None,
                'outfit': ramp('#1E2E4A', '#2C426A')},
    'Drifter': {'hair': ramp('#3A3A40', '#50505A', '#686874', '#84848E', '#A4A4AE'), 'eye': None,
                'outfit': ramp('#2A3A26', '#3C5236')},
}


def load_frames():
    """The six frames as grids of RGB / None, each trimmed to its own box."""
    w, h, px = read_png(SRC)
    frames = []
    for n in range(w // CELL):
        cells = [(x, y) for y in range(h) for x in range(n * CELL, (n + 1) * CELL) if px[y][x][3]]
        x0, x1 = min(c[0] for c in cells), max(c[0] for c in cells)
        y0, y1 = min(c[1] for c in cells), max(c[1] for c in cells)
        grid = [[px[y][x][:3] if px[y][x][3] else None for x in range(x0, x1 + 1)] for y in range(y0, y1 + 1)]
        frames.append(grid)
    return frames


def hair_top_centre(grid):
    xs = [x for y in range(min(8, len(grid))) for x, c in enumerate(grid[y]) if c is not None]
    return sum(xs) / float(len(xs))


def dress(grid, spec):
    """Recolours one frame: hair (inside the hair area), eyes and outfit."""
    h, w = len(grid), len(grid[0])
    out = [row[:] for row in grid]
    hair_map = {HAIR[i]: spec['hair'][i] for i in range(5)}
    cx = hair_top_centre(grid)
    for y in range(h):
        for x in range(w):
            c = grid[y][x]
            if c is None:
                continue
            # hair: the whole head (rows 0-14), then only the locks either side of the torso (rows 15-35)
            torso = y >= 15 and abs(x - cx) <= 6.5
            if c in hair_map and y <= 35 and not torso:
                out[y][x] = hair_map[c]
            # eyes: the iris pixels just under the dark lash line (rows 6-10 of the head)
            elif spec['eye'] and c == IRIS and 6 <= y <= 10 and y > 0 and grid[y - 1][x] == OUTLINE:
                out[y][x] = spec['eye']
            elif spec['outfit'] and c in OUTFIT and y >= 15:
                out[y][x] = spec['outfit'][OUTFIT.index(c)]
    return out


def back_view(grid, spec):
    """From behind: face and eyes (the skin and dark features inside the face oval) become hair."""
    out = [row[:] for row in grid]
    cx = hair_top_centre(grid)
    hair = spec['hair']
    for y in range(6, 16):
        for x in range(len(grid[0])):
            c = grid[y][x]
            if c is None or abs(x - cx) > 5:
                continue
            if c in SKIN or c == OUTLINE or c in (hx('#CD7577'), hx('#AF6655')) or c in hair:
                out[y][x] = hair[3] if (x % 3 == 0) else hair[2]
    return out


def place(grid, dx=0, dy=0, upper_dy=0, split=38):
    """A 64 px frame: the grid centred on its hair top, feet on the ground row; rows above `split` can shift
    separately (upper-body dips)."""
    fr = [[None] * F for _ in range(F)]
    h = len(grid)
    cx = hair_top_centre(grid)
    ox = int(round(F / 2.0 - cx)) + dx
    oy = GROUND - (h - 1) + dy
    for y in range(h):
        for x, c in enumerate(grid[y]):
            if c is None:
                continue
            Y = oy + y + (upper_dy if y < split else 0)
            X = ox + x
            if 0 <= X < F and 0 <= Y < F:
                fr[Y][X] = c
    return fr


def arc(fr, cx, cy, r, colour, facing_left):
    """A pale slash crescent in front of the body."""
    for a in range(-60, 61, 6):
        t = math.radians(a)
        for rr in (r, r - 1):
            x = cx + (-1 if facing_left else 1) * int(round(math.cos(t) * rr))
            y = cy + int(round(math.sin(t) * rr))
            if 0 <= x < F and 0 <= y < F:
                fr[y][x] = colour if rr == r else (255, 255, 255)
    return fr


def orb(fr, cx, cy, r, colour):
    for y in range(cy - r - 1, cy + r + 2):
        for x in range(cx - r - 1, cx + r + 2):
            if not (0 <= x < F and 0 <= y < F):
                continue
            d = math.hypot(x - cx, y - cy)
            if d <= r:
                fr[y][x] = (255, 255, 255) if d < r * 0.45 else colour
            elif d <= r + 1:
                fr[y][x] = OUTLINE
    return fr


def lying(fr):
    rot = [[None] * F for _ in range(F)]
    for y in range(F):
        for x in range(F):
            rot[x][F - 1 - y] = fr[y][x]
    lowest = max((y for y in range(F) for x in range(F) if rot[y][x] is not None), default=GROUND)
    out = [[None] * F for _ in range(F)]
    for y in range(F):
        for x in range(F):
            if rot[y][x] is not None and 0 <= y + GROUND - lowest < F:
                out[y + GROUND - lowest][x] = rot[y][x]
    return out


def frames_for(name):
    spec = SPECS[name]
    raw = load_frames()
    front = [dress(g, spec) for g in raw]
    back = [back_view(g, spec) for g in front]
    glow = spec['eye'] or (230, 230, 240)
    walk_order = [0, 1, 2, 3, 4, 5, 0]

    def row(anim, facing):
        src = back if facing == UP else front
        lean = -1 if facing == LEFT else 1 if facing == RIGHT else 0
        left = facing == LEFT
        if anim == 'walk':
            return [place(src[i], dx=lean if k else 0) for k, i in enumerate(walk_order)]
        if anim == 'idle':
            return [place(src[0]), place(src[0], upper_dy=1)]
        if anim == 'slash':
            seq = [(0, 0, False), (-1, 3, False), (2 * (-1 if left else 1), 4, True), (3 * (-1 if left else 1), 4, True),
                   (1 * (-1 if left else 1), 3, False), (0, 0, False)]
            out = []
            for dx, i, swing in seq:
                fr = place(src[i], dx=dx)
                if swing and facing != UP:
                    arc(fr, F // 2 + (-6 if left else 6) + dx, 34, 9, glow, left)
                out.append(fr)
            return out
        out = []
        for i, r in enumerate([0, 1, 2, 3, 3, 2, 0]):
            fr = place(src[0], upper_dy=1 if r else 0)
            if r and facing != UP:
                orb(fr, F // 2, 30, r, glow)
            out.append(fr)
        return out

    sheets = {a: [row(a, d) for d in (UP, LEFT, DOWN, RIGHT)] for a in ('idle', 'walk', 'slash', 'spellcast')}
    hurt = [place(front[0], dx=-1), place(front[0], dx=-2, upper_dy=1), place(front[3], dx=-1, dy=0, upper_dy=3),
            place(front[3], upper_dy=5)]
    down = place(front[0])
    sheets['hurt'] = [hurt + [lying(down), lying(down)]]
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
        f.write('# %s: sprite credits\n\nBuilt by `Tools/ChibiForge/girl_model.py` from `Tools/ChibiForge/reference/'
                'Girl_walk.png` (the user\'s reference walk cycle; see that folder\'s README for its rights note), '
                'recoloured per character. Native 64 px frames.\n' % name)
    print('built', name, '(girl model)')


def contact(path, names, scale=4):
    cells = []
    for n in names:
        sh = frames_for(n)
        cells.append([sh['walk'][DOWN][0], sh['walk'][DOWN][2], sh['walk'][DOWN][4], sh['walk'][UP][0],
                      sh['slash'][RIGHT][3], sh['spellcast'][DOWN][3], sh['hurt'][0][5]])
    W, H = len(cells[0]) * F, len(cells) * F
    bg = (52, 50, 60, 255)
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
    names = args or list(SPECS)
    if sheet:
        contact(sheet, names)
    else:
        for n in names:
            build(n)
