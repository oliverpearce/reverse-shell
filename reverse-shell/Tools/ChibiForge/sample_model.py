"""
Sample model: sprites from the user's own animation (reference/sample/, exported from Aseprite). It was MC's art;
MC now uses knight_model.py (chibi_forge checks that first), so this only runs when the knight sheets are missing:
    idle/sprite sheets/idle.png   10 frames of 46 x 55 in a row: a front three-quarter idle loop, facing the viewer's left
    walk/sprite sheets/walk.png   24 frames of 45 x 58 in a 4 x 6 grid: a side-view walk loop, facing left
    walk/sprite sheets/from idle.png  2 transition frames (not used: the game has no idle-to-walk transition)

    python Tools/ChibiForge/sample_model.py [--sheet out.png]

Each frame keeps the artist's pixels and its place in its cell, dropped into a 64 px frame with its bottom row on the
ground row (feet pivot 0.04) and centred horizontally.
    idle 10       the drawn loop (every facing; right is the mirror)
    walk 25       frame 0 stands (idle frame 0), then the 24 drawn frames. Left as drawn, right mirrored. There is no
                  front or back walk yet: down uses the left walk, up the right one.
    slash 10      built from idle frame 0: wind back, lunge, recover, with a glow arc in MC's orange
    spellcast 12  idle frame 0 with an orb growing at the hands
    hurt 9        recoil, sink, then lie on the ground
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'SpriteForge'))
sys.path.insert(0, HERE)
from pngtool import read_png, write_png  # noqa: E402
import chibi_forge as cf  # noqa: E402

F = 64
GROUND = 61
UP, LEFT, DOWN, RIGHT = 0, 1, 2, 3
SAMPLE = os.path.join(HERE, 'reference', 'sample')
IDLE = os.path.join(SAMPLE, 'idle', 'sprite sheets', 'idle.png')
WALK = os.path.join(SAMPLE, 'walk', 'sprite sheets', 'walk.png')
IDLE_CELL, IDLE_N = (46, 55), 10
WALK_CELL, WALK_COLS, WALK_N = (45, 58), 4, 24
NAMES = ['MC']                    # who uses this art
OUTLINE = (0x20, 0x02, 0x1D)


def available(name):
    return name in NAMES and os.path.exists(IDLE) and os.path.exists(WALK)


def cells(path, size, count, cols):
    w, h, px = read_png(path)
    cw, ch = size
    out = []
    for i in range(count):
        c, r = i % cols, i // cols
        out.append([[px[r * ch + y][c * cw + x] if px[r * ch + y][c * cw + x][3] else None for x in range(cw)]
                    for y in range(ch)])
    return out


def place(cell, dx=0, dy=0, fx=None):
    """A 64 px frame: the cell centred, its bottom row on the ground row, shifted by (dx, dy)."""
    ch, cw = len(cell), len(cell[0])
    fr = [[None] * F for _ in range(F)]
    ox, oy = (F - cw) // 2 + dx, GROUND - (ch - 1) + dy
    for y in range(ch):
        for x in range(cw):
            c = cell[y][x]
            if c is not None and 0 <= ox + x < F and 0 <= oy + y < F:
                fr[oy + y][ox + x] = c
    if fx:
        fx(fr, ox, oy)
    return fr


def mirror(fr):
    return [row[::-1] for row in fr]


def arc(fr, cx, cy, r, hi):
    """A slash crescent to the left of (cx, cy) (the art faces left), white-hot inside."""
    for a in range(110, 251, 5):
        t = math.radians(a)
        for rr, c in ((r, hi[1]), (r - 1, hi[2]), (r - 2, hi[3])):
            x, y = cx + int(round(math.cos(t) * rr)), cy + int(round(math.sin(t) * rr))
            if 0 <= x < F and 0 <= y < F and (rr == r or 125 < a < 235):
                fr[y][x] = c + (255,)


def orb(fr, cx, cy, r, hi):
    for y in range(cy - r - 1, cy + r + 2):
        for x in range(cx - r - 1, cx + r + 2):
            if not (0 <= x < F and 0 <= y < F):
                continue
            d = math.hypot(x - cx, y - cy)
            if d <= r:
                c = hi[3] if d < r * 0.45 else hi[2] if d < r * 0.8 else hi[1]
                fr[y][x] = c + (255,)
            elif d <= r + 1:
                fr[y][x] = OUTLINE + (255,)


def lying(fr, lift=0):
    rot = [[None] * F for _ in range(F)]
    for y in range(F):
        for x in range(F):
            rot[x][F - 1 - y] = fr[y][x]          # clockwise, like the other models
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


SLASH_LEAN = [0, 1, 2, 1, -2, -4, -4, -3, -1, 0]    # wind back (+x), lunge forward (-x: she faces left), recover


def frames_for(name):
    idle = cells(IDLE, IDLE_CELL, IDLE_N, IDLE_N)
    walk = cells(WALK, WALK_CELL, WALK_N, WALK_COLS)
    spec = cf.SPECS[name]
    hi = [c[:3] for c in (spec.hi or [spec.glow] * 3 + [cf.WHITE])]
    stand = idle[0]

    def facing_rows(frames):
        """[up, left, down, right] from left-facing frames: right (and up) mirrored."""
        left = frames
        right = [mirror(f) for f in frames]
        return [right, left, left, right]

    sheets = {}
    sheets['idle'] = facing_rows([place(c) for c in idle])
    sheets['walk'] = facing_rows([place(stand)] + [place(c) for c in walk])
    slash = []
    for i, lean in enumerate(SLASH_LEAN):
        fx = None
        if 4 <= i <= 6:
            fx = (lambda k: lambda fr, ox, oy: arc(fr, ox + 6 - (k - 4), oy + 32, 10 + (k - 4), hi))(i)
        slash.append(place(stand, dx=lean, fx=fx))
    sheets['slash'] = facing_rows(slash)
    radii = [0, 1, 2, 3, 4, 4, 4, 4, 3, 2, 1, 0]
    cast = []
    for r in radii:
        fx = (lambda rr: lambda fr, ox, oy: orb(fr, ox + 12, oy + 34, rr, hi))(r) if r else None
        cast.append(place(stand, dy=1 if r >= 3 else 0, fx=fx))
    sheets['spellcast'] = facing_rows(cast)
    hurt = []
    for i in range(6):
        t = ease(i / 5.0)
        hurt.append(place(stand, dx=int(round(3 * math.sin(t * math.pi))), dy=int(round(3 * t))))
    flat = place(stand)
    sheets['hurt'] = [hurt + [lying(flat, 4), lying(flat, 1), lying(flat)]]
    return sheets


def to_rgba(fr):
    return [[c if c is not None else (0, 0, 0, 0) for c in row] for row in fr]


def build(name):
    out_dir = os.path.join(cf.CHARS, name)
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
        f.write('# %s: sprite credits\n\nIdle and walk drawn by the user (Aseprite sources in '
                '`Tools/ChibiForge/reference/sample/`); `Tools/ChibiForge/sample_model.py` places them in 64 px '
                'frames and builds the attack, cast and hurt clips from the idle pose.\n' % name)
    print('built', name, '(sample model)')


def contact(path, scale=4):
    sh = frames_for('MC')
    cells_ = [sh['idle'][DOWN][0], sh['idle'][DOWN][5], sh['walk'][LEFT][1], sh['walk'][LEFT][7],
              sh['walk'][RIGHT][13], sh['slash'][LEFT][5], sh['spellcast'][LEFT][5], sh['hurt'][0][3], sh['hurt'][0][8]]
    W, H = len(cells_) * F, F
    bg = (60, 72, 50, 255)
    img = [[bg] * W for _ in range(H)]
    for c, fr in enumerate(cells_):
        for y in range(F):
            for x in range(F):
                if fr[y][x] is not None:
                    img[y][c * F + x] = fr[y][x]
    write_png(path, W * scale, H * scale, [[img[y // scale][x // scale] for x in range(W * scale)] for y in range(H * scale)])


if __name__ == '__main__':
    args = sys.argv[1:]
    if '--sheet' in args:
        contact(args[args.index('--sheet') + 1])
    else:
        for n in NAMES:
            build(n)
