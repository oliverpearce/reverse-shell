"""
Soul Dummy: Erin's training dummy for the battle tutorial (our own art). A stitched burlap sack on a wooden post, with
mismatched button eyes, a sewn-on patch, straw poking out of the seams and a small Soul Card pinned to its chest that
glows. Only a Soul Card can finish it (EnemyDefinition.soulOnly).

    py -3.9 Tools/ChibiForge/soul_dummy.py            # writes the sheets
    py -3.9 Tools/ChibiForge/soul_dummy.py --preview  # also a 4x preview in Tools/ChibiForge/reference/soul_dummy_preview.png

Output, the same layout as chibi_forge (64 px frames; rows up, left, down, right; hurt has one row):
  Assets/_Project/Art/Characters/SoulDummy/idle|walk|slash|spellcast|hurt.png
Motion: the whole dummy leans about the foot of its post (rows shift by height), so it sways rather than walks.
"""
import math
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters', 'SoulDummy')
F = 64
UP, LEFT, DOWN, RIGHT = 0, 1, 2, 3

OUTLINE = (32, 2, 29, 255)
SACK = [(118, 78, 52, 255), (164, 116, 72, 255), (201, 155, 99, 255), (228, 196, 140, 255)]   # dark -> light
WOOD = [(70, 40, 30, 255), (110, 66, 42, 255), (146, 94, 58, 255)]
STRAW = [(186, 140, 46, 255), (232, 196, 88, 255)]
STITCH = (86, 52, 40, 255)
BUTTON_A = (58, 90, 140, 255)     # one blue button eye
BUTTON_B = (150, 58, 66, 255)     # one red button eye
PATCH = [(78, 112, 96, 255), (110, 150, 124, 255)]
CARD = [(240, 236, 220, 255), (255, 255, 255, 255)]
GLOW = [(150, 120, 230, 255), (196, 176, 255, 255)]
CLEAR = (0, 0, 0, 0)

# Where things sit in the 64 px cell (feet of the post at row 61).
FOOT = 61


class Canvas:
    def __init__(self):
        self.px = {}

    def put(self, x, y, c):
        if 0 <= x < F and 0 <= y < F:
            self.px[(x, y)] = c

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.put(x, y, c)

    def ellipse(self, cx, cy, rx, ry, shade):
        """A filled ellipse shaded from the top left (shade = 4-tone ramp, dark first)."""
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                dx, dy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
                d = dx * dx + dy * dy
                if d > 1.0:
                    continue
                light = -(dx * 0.6 + dy * 0.8)        # lit from the top left
                if d > 0.78:
                    i = 0 if light < 0.1 else 1
                elif light > 0.45:
                    i = 3
                elif light > -0.15:
                    i = 2
                else:
                    i = 1
                self.put(x, y, shade[i])


def draw_dummy(facing, glow):
    """The dummy standing straight, in one facing. glow (0..1) pulses the pinned card."""
    c = Canvas()
    back = facing == UP
    side = facing in (LEFT, RIGHT)

    # Post and its cross-foot.
    c.rect(30, 40, 33, FOOT, WOOD[1])
    c.rect(30, 40, 30, FOOT, WOOD[2])
    c.rect(33, 40, 33, FOOT, WOOD[0])
    c.rect(24, FOOT - 1, 39, FOOT, WOOD[1])
    c.rect(24, FOOT, 39, FOOT, WOOD[0])
    c.rect(26, FOOT - 2, 37, FOOT - 2, WOOD[2])

    # Crossbar for the arms (shorter seen from the side).
    reach = 7 if side else 14
    c.rect(32 - reach, 30, 31 + reach, 32, WOOD[1])
    c.rect(32 - reach, 30, 31 + reach, 30, WOOD[2])
    # Sack arms over the bar, tied off with straw at the ends.
    c.rect(32 - reach + 1, 29, 31 + reach - 1, 33, SACK[1])
    c.rect(32 - reach + 1, 29, 31 + reach - 1, 29, SACK[2])
    c.rect(32 - reach + 1, 33, 31 + reach - 1, 33, SACK[0])
    for end in (32 - reach, 31 + reach):
        sgn = -1 if end < 32 else 1
        c.put(end, 29, STRAW[1]); c.put(end, 31, STRAW[0]); c.put(end, 33, STRAW[1])
        c.put(end + sgn, 30, STRAW[0]); c.put(end + sgn, 32, STRAW[1])
    # Arm stitches.
    for x in range(32 - reach + 3, 31 + reach - 2, 3):
        if abs(x - 32) > 6:
            c.put(x, 31, STITCH)

    # Body: a stuffed sack.
    c.ellipse(32, 37.5, 9.5 if not side else 7.5, 8.5, SACK)
    # Belt of rope where the sack is tied to the post.
    c.rect(24 if not side else 26, 44, 39 if not side else 37, 44, STRAW[0])
    c.put(31, 45, STRAW[1]); c.put(33, 46, STRAW[0])

    # Head.
    c.ellipse(32, 20, 8, 7.5, SACK)
    # Straw tuft on top.
    for x, h in ((28, 2), (30, 3), (32, 4), (34, 2), (36, 3)):
        for k in range(h):
            c.put(x + (k % 2), 12 - k, STRAW[1] if k % 2 == 0 else STRAW[0])
    # The neck tie.
    c.rect(29, 27, 35, 28, STRAW[0])
    c.put(30, 27, STRAW[1]); c.put(34, 28, STRAW[1])

    if back:
        # The back of the head: a long seam; the back of the body: one big cross stitch.
        for y in range(14, 27, 2):
            c.put(32, y, STITCH)
        for k in range(-3, 4):
            c.put(32 + k, 37 + k, STITCH)
            c.put(32 + k, 37 - k, STITCH)
        return c

    off = {LEFT: -3, RIGHT: 3}.get(facing, 0)
    # Mismatched button eyes (one eye shows from the side).
    eyes = [(-3, BUTTON_A), (3, BUTTON_B)] if not side else [(off, BUTTON_A if facing == LEFT else BUTTON_B)]
    for dx, col in eyes:
        ex = 32 + dx
        c.rect(ex - 1, 19, ex, 20, col)
        c.put(ex - 1, 19, tuple(min(255, v + 60) for v in col[:3]) + (255,))
        c.put(ex, 20, OUTLINE)
    # A stitched mouth.
    mx = 32 + off
    for k in range(-3 if not side else -1, 4 if not side else 2):
        c.put(mx + k, 24, STITCH if k % 2 == 0 else SACK[1])
    # A patch sewn on the head's side.
    if facing != RIGHT:
        px0 = 25 if not side else 28
        c.rect(px0, 15, px0 + 2, 17, PATCH[0])
        c.put(px0, 15, PATCH[1])
        c.put(px0 + 3, 15, STITCH); c.put(px0 + 3, 17, STITCH)

    # The Soul Card pinned to its chest, softly glowing.
    cx = 32 + (off // 2)
    g = GLOW[1] if glow > 0.5 else GLOW[0]
    c.rect(cx - 3, 34, cx + 2, 41, g)                 # glow edge
    c.rect(cx - 2, 35, cx + 1, 40, CARD[0])
    c.rect(cx - 2, 35, cx - 2, 40, CARD[1])
    c.put(cx - 1, 37, GLOW[0]); c.put(cx, 37, GLOW[0]); c.put(cx - 1, 38, GLOW[1]); c.put(cx, 38, GLOW[0])
    c.put(cx - 1, 34, (200, 200, 210, 255))           # the pin
    # Seams on the body.
    for y in range(31, 45, 2):
        c.put(32 + (7 if not side else 5) * (1 if facing != LEFT else -1), y, STITCH)
    return c


def outline(img):
    """A dark outline around every filled pixel (4-neighbour), drawn into the empty pixels."""
    src = img.load()
    out = img.copy()
    dst = out.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            if src[x, y][3] != 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and src[nx, ny][3] != 0 and src[nx, ny] != OUTLINE:
                    dst[x, y] = OUTLINE
                    break
    return out


def render(facing, lean, glow=0.0, bob=0, squash=0.0, specks=()):
    """One frame: the dummy leaning by `lean` px at the top of its head (positive = to the right), sheared about the
    foot of the post; `bob` lifts it; `squash` (0..1) flattens the head and body a touch (a hit landing)."""
    base = draw_dummy(facing, glow)
    img = Image.new('RGBA', (F, F), CLEAR)
    p = img.load()
    top = 10
    for (x, y), col in base.px.items():
        t = max(0.0, (FOOT - y) / float(FOOT - top))      # 0 at the foot, 1 at the head
        sy = y
        if squash and y < 46:
            sy = int(round(46 - (46 - y) * (1.0 - 0.08 * squash)))
        nx = int(round(x + lean * t * t))
        ny = sy - bob if y < FOOT - 2 else sy            # the cross-foot stays planted
        if 0 <= nx < F and 0 <= ny < F:
            p[nx, ny] = col
    for x, y, col in specks:
        if 0 <= x < F and 0 <= y < F:
            p[x, y] = col
    return outline(img)


def mirror_lean(facing, lean):
    """Lean toward where it faces (left row leans left, right row leans right)."""
    return -lean if facing == LEFT else lean


def sheet(frames_by_row):
    rows = len(frames_by_row)
    cols = len(frames_by_row[0])
    out = Image.new('RGBA', (cols * F, rows * F), CLEAR)
    for r, frames in enumerate(frames_by_row):
        for i, fr in enumerate(frames):
            out.alpha_composite(fr, (i * F, r * F))
    return out


def ease(t):
    return 0.5 - 0.5 * math.cos(math.pi * t)


def clip_idle(facing, n=8):
    # A slow sway, the card pulsing.
    return [render(facing, 1.6 * math.sin(2 * math.pi * i / n), glow=0.5 + 0.5 * math.sin(2 * math.pi * i / n))
            for i in range(n)]


def clip_walk(facing, n=13):
    # It hops on its post: frame 0 is standing.
    frames = [render(facing, 0)]
    for i in range(n - 1):
        t = i / float(n - 1)
        hop = int(round(3 * abs(math.sin(2 * math.pi * t))))
        frames.append(render(facing, mirror_lean(facing, 1.5) * math.sin(2 * math.pi * t), bob=hop, glow=t))
    return frames


def clip_poke(facing, n=10):
    # Wind back, then a stiff lean-in "poke" toward its target, then settle.
    keys = [0, -2, -3, -1, 4, 6, 6, 4, 2, 0]
    return [render(facing, mirror_lean(facing, keys[i % len(keys)]), glow=1.0 if 4 <= i <= 6 else 0.3)
            for i in range(n)]


def clip_cast(facing, n=12):
    # The card flares: a shiver in place.
    return [render(facing, (1 if i % 2 else -1) * (1.0 if 3 <= i <= 8 else 0.4), glow=1.0 if i % 2 else 0.2)
            for i in range(n)]


def clip_hurt(n=9):
    # Knocked back, wobbling to a stop, straw flying off the seams.
    amp = [-7, 5, -4, 3, -2, 1.5, -1, 0.5, 0]
    frames = []
    for i in range(n):
        specks = []
        if i < 5:
            for k in range(4):
                a = k * 1.7 + i * 0.4
                r = 6 + i * 3
                specks.append((int(32 + math.cos(a) * r), int(26 - math.sin(a) * r * 0.6 - i), STRAW[k % 2]))
        frames.append(render(DOWN, amp[i], squash=1.0 if i < 2 else 0.0, specks=specks, glow=0.2))
    return frames


def main():
    os.makedirs(OUT, exist_ok=True)
    rows = (UP, LEFT, DOWN, RIGHT)
    clips = {
        'idle': sheet([clip_idle(f) for f in rows]),
        'walk': sheet([clip_walk(f) for f in rows]),
        'slash': sheet([clip_poke(f) for f in rows]),
        'spellcast': sheet([clip_cast(f) for f in rows]),
        'hurt': sheet([clip_hurt()]),
    }
    for name, img in clips.items():
        if img.size[0] > 2048 or img.size[1] > 2048:
            raise SystemExit('%s is %dx%d: sheets must fit 2048 px' % (name, img.size[0], img.size[1]))
        img.save(os.path.join(OUT, name + '.png'))
        print('wrote', name, img.size)
    if '--preview' in sys.argv:
        prev = clips['idle'].resize((clips['idle'].size[0] * 4, clips['idle'].size[1] * 4), Image.NEAREST)
        bg = Image.new('RGBA', prev.size, (58, 60, 72, 255))
        bg.alpha_composite(prev)
        path = os.path.join(HERE, 'reference', 'soul_dummy_preview.png')
        bg.save(path)
        print('preview', path)


if __name__ == '__main__':
    main()
