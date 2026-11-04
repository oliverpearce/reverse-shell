"""
PortraitForge: paints the character-card portraits procedurally (our own art, no license strings).

    python Tools/PortraitForge/portrait_forge.py                 # write every portrait
    python Tools/PortraitForge/portrait_forge.py --sheet out.png # also write a 4x contact sheet

For each character it writes four 96x144 PNGs into Assets/_Project/Art/Portraits/:

    <Name>_front.png           the full robot (card front)
    <Name>_internals.png       the same robot opened up: plates off, frame, wiring, servos and core (card back)
    <Name>_front_glow.png      white mask of the glowing accents (+ a soft halo), tinted in game by the element
    <Name>_internals_glow.png  the same for the internals

Accent pixels are painted light gray in the base art so the glow layer decides their color. That is how a
character's hue can change with the element it channels (A3) without repainting anything.

Ashe and Lucy are the same robot model (robot_base); Ashe is overgrown with plants, Lucy carries a virus
look (cable twin-tails, circuit traces, glitch rows). A3 is its own, taller model with a halo and a coat.
Every robot has two glowing eyes (no visor band) and glowing scale-pattern trims along its armour edges.
Deterministic: same script, same pixels. Uses pngtool.py from Tools/SpriteForge.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'SpriteForge'))
from pngtool import write_png  # noqa: E402

W, H = 96, 144
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Portraits')
CLEAR = (0, 0, 0, 0)
LIGHT = (-0.55, -0.65, 0.52)  # from the top left, towards the viewer


def hx(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def ramp(*hexes):
    return [hx(h) for h in hexes]


# Shading ramps, dark to light.
PLATE = ramp('#5C5F6B', '#8A8E99', '#B9BCC4', '#DCDDE2', '#F4F4F6')
PLATE_WARM = ramp('#5F5A52', '#8C857A', '#B8B0A2', '#DAD3C5', '#F1ECE2')
JOINT = ramp('#141318', '#22212A', '#33323D', '#474652', '#5C5B68')
FRAME = ramp('#1C1B22', '#2C2B35', '#3E3D4A', '#555462', '#6F6E7C')
COAT = ramp('#141319', '#1E1D26', '#2A2934', '#383746', '#4A4958')
TRIM = ramp('#8A8E99', '#C9CBD2', '#F4F4F6')
OUTLINE = hx('#0E0D12')
ACCENT = hx('#D9D9DE')        # glow pixels in the base art; the glow layer colors them
ACCENT_DIM = hx('#9C9CA6')
LEAF = ramp('#1F3D22', '#2F5A2C', '#46803A', '#68A548', '#98C860')
MOSS = ramp('#2B4427', '#46703A', '#6E9C46', '#A9C866')
BARK = ramp('#3A2A20', '#54402E', '#6E573F')
PETAL = ramp('#C2577E', '#E889A8', '#F7C6D6')
WIRE_COLORS = [hx('#C8483E'), hx('#3E78C8'), hx('#D8B040'), hx('#4AA86A')]
COPPER = ramp('#5A3418', '#8E5A2C', '#C08A4A', '#E8B878')

BAYER = [[0, 2], [3, 1]]


class Canvas:
    def __init__(self):
        self.px = [[CLEAR] * W for _ in range(H)]
        self.glow = [[0.0] * W for _ in range(H)]

    def inside(self, x, y):
        return 0 <= x < W and 0 <= y < H

    def set(self, x, y, c, glow=False):
        x, y = int(x), int(y)
        if self.inside(x, y):
            self.px[y][x] = c
            if glow:
                self.glow[y][x] = 1.0
            elif self.glow[y][x] >= 1.0:
                self.glow[y][x] = 0.0  # painted over

    def get(self, x, y):
        return self.px[y][x] if self.inside(x, y) else CLEAR

    def opaque(self, x, y):
        return self.inside(x, y) and self.px[y][x][3] > 0


def shade_pick(r, light, x, y):
    """Light in 0..1 -> a ramp color, with 2x2 ordered dithering between steps."""
    v = max(0.0, min(0.999, light)) * (len(r) - 1)
    i = int(v)
    frac = v - i
    if i + 1 < len(r) and frac * 4 > BAYER[y % 2][x % 2] + 0.5:
        i += 1
    return r[i]


def lambert(nx, ny, nz):
    d = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
    return 0.18 + 0.82 * max(0.0, d)


# ---------------------------------------------------------------------------------------------------- shapes

def ellipse(cv, cx, cy, rx, ry, r, glow=False, bias=0.0):
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
            nx, ny = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            d = nx * nx + ny * ny
            if d <= 1.0:
                nz = math.sqrt(max(0.0, 1 - d))
                cv.set(x, y, shade_pick(r, lambert(nx, ny, nz) + bias, x, y), glow)


def capsule(cv, x0, y0, x1, y1, r0, r1, r, glow=False, bias=0.0):
    """A limb: a tapered cylinder from (x0,y0) to (x1,y1), shaded across its width."""
    dx, dy = x1 - x0, y1 - y0
    length2 = dx * dx + dy * dy or 1
    length = math.sqrt(length2)
    px_, py_ = -dy / length, dx / length  # perpendicular
    lo_x, hi_x = int(min(x0, x1) - max(r0, r1)) - 1, int(max(x0, x1) + max(r0, r1)) + 2
    lo_y, hi_y = int(min(y0, y1) - max(r0, r1)) - 1, int(max(y0, y1) + max(r0, r1)) + 2
    for y in range(lo_y, hi_y):
        for x in range(lo_x, hi_x):
            fx, fy = x + 0.5 - x0, y + 0.5 - y0
            t = max(0.0, min(1.0, (fx * dx + fy * dy) / length2))
            rad = r0 + (r1 - r0) * t
            cx, cy = fx - dx * t, fy - dy * t
            dist = math.sqrt(cx * cx + cy * cy)
            if dist <= rad:
                side = (cx * px_ + cy * py_) / max(0.5, rad)
                nx, ny = px_ * side, py_ * side
                nz = math.sqrt(max(0.0, 1 - side * side))
                cv.set(x, y, shade_pick(r, lambert(nx, ny, nz) + bias, x, y), glow)


def polygon(cv, pts, r, glow=False, bias=0.0, bevel=True):
    """A flat plate: lit by its x position (left brighter), with a light top edge and darker bottom edge."""
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    x0, x1, y0, y1 = int(min(xs)), int(max(xs)) + 1, int(min(ys)), int(max(ys)) + 1
    inside = set()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if point_in(pts, x + 0.5, y + 0.5):
                inside.add((x, y))
    for (x, y) in inside:
        u = (x - x0) / max(1, x1 - x0)
        light = 0.82 - 0.45 * u + bias
        if bevel and (x, y - 1) not in inside:
            light += 0.3
        elif bevel and (x, y + 1) not in inside:
            light -= 0.25
        cv.set(x, y, shade_pick(r, light, x, y), glow)
    return inside


def point_in(pts, x, y):
    n, j, c = len(pts), len(pts) - 1, False
    for i in range(n):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / ((yj - yi) or 1e-9) + xi:
            c = not c
        j = i
    return c


def line(cv, x0, y0, x1, y1, c, glow=False, width=1):
    steps = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
    for i in range(steps + 1):
        t = i / max(1, steps)
        x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        for w in range(width):
            cv.set(round(x) + w, round(y), c, glow)


def curve(cv, pts, c, glow=False, width=1):
    """Catmull-Rom-ish polyline through the points (good enough for vines and cables)."""
    dense = []
    for i in range(len(pts) - 1):
        p0 = pts[i - 1] if i > 0 else pts[i]
        p1, p2 = pts[i], pts[i + 1]
        p3 = pts[i + 2] if i + 2 < len(pts) else p2
        for s in range(12):
            t = s / 12
            t2, t3 = t * t, t * t * t
            x = 0.5 * ((2 * p1[0]) + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3)
            y = 0.5 * ((2 * p1[1]) + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3)
            dense.append((x, y))
    dense.append(pts[-1])
    for i in range(len(dense) - 1):
        line(cv, dense[i][0], dense[i][1], dense[i + 1][0], dense[i + 1][1], c, glow, width)
    return dense


def outline(cv, color=OUTLINE):
    """1 px dark outline around every opaque shape."""
    add = []
    for y in range(H):
        for x in range(W):
            if cv.opaque(x, y):
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if cv.opaque(x + dx, y + dy):
                    add.append((x, y))
                    break
    for x, y in add:
        cv.set(x, y, color)


def halo(cv, radius=2, strength=0.38):
    """Spreads a soft halo around the glow pixels (only over the art, plus a little outside)."""
    src = [row[:] for row in cv.glow]
    for y in range(H):
        for x in range(W):
            if src[y][x] >= 1.0:
                continue
            best = 0.0
            for dy in range(-radius, radius + 1):
                for dx in range(-radius, radius + 1):
                    yy, xx = y + dy, x + dx
                    if 0 <= yy < H and 0 <= xx < W and src[yy][xx] >= 1.0:
                        d = math.sqrt(dx * dx + dy * dy)
                        if d <= radius:
                            best = max(best, strength * (1 - (d - 1) / radius))
            cv.glow[y][x] = best


def glow_pixels(cv):
    return [[(255, 255, 255, int(255 * min(1.0, cv.glow[y][x]))) for x in range(W)] for y in range(H)]


def rivet(cv, x, y):
    cv.set(x, y, PLATE[0])
    cv.set(x - 1, y - 1, PLATE[4])


def gear(cv, cx, cy, r, ramp_=None):
    ramp_ = ramp_ or COPPER
    ellipse(cv, cx, cy, r, r, ramp_)
    for k in range(8):
        a = k * math.pi / 4
        cv.set(round(cx + math.cos(a) * (r + 0.6)), round(cy + math.sin(a) * (r + 0.6)), ramp_[1])
    cv.set(cx, cy, JOINT[0])
    if r >= 3:
        cv.set(cx - 1, cy, JOINT[1])


# ---------------------------------------------------------------------------------------------------- the shared robot (Ashe / Lucy)

def robot_body(cv, plate=PLATE, internals=False):
    """The shared robot model: rounded helmet, visor, shoulder domes, chest plate, slim plated limbs.
    Returns anchor points other layers use."""
    a = {'head': (48, 21), 'core': (48, 50), 'l_sh': (30, 43), 'r_sh': (66, 43),
         'l_hand': (25, 95), 'r_hand': (71, 95), 'l_knee': (40, 106), 'r_knee': (56, 106)}
    body = FRAME if internals else plate

    # Legs (drawn first, behind the hips).
    for side in (-1, 1):
        hx_ = 48 + side * 8
        capsule(cv, hx_, 80, hx_ + side * 1, 104, 6, 5, JOINT if internals else body)
        ellipse(cv, hx_ + side * 1, 106, 4.5, 4, JOINT)  # knee
        capsule(cv, hx_ + side * 1, 108, hx_ + side * 2, 132, 5.5, 4.5, JOINT if internals else body)
        polygon(cv, [(hx_ + side * 2 - 7, 133), (hx_ + side * 2 + 6, 133), (hx_ + side * 2 + 7 + side, 140), (hx_ + side * 2 - 8 + side, 140)],
                JOINT if internals else body)

    # Hips.
    ellipse(cv, 48, 78, 12, 6, JOINT)

    # Arms.
    for side in (-1, 1):
        sx = 48 + side * 18
        capsule(cv, sx, 46, sx + side * 3, 68, 4.5, 4, JOINT if internals else body)
        ellipse(cv, sx + side * 3, 70, 3.5, 3.5, JOINT)  # elbow
        capsule(cv, sx + side * 3, 72, sx + side * 5, 90, 4.5, 5, JOINT if internals else body)
        ellipse(cv, sx + side * 5, 95, 4.5, 5, JOINT if internals else ramp(*['#33323D', '#47465A', '#5C5B6E', '#7A798A']))  # hand

    # Neck and torso.
    capsule(cv, 48, 32, 48, 38, 4, 4.5, JOINT)
    if internals:
        # Spine and ribs instead of the chest plate.
        polygon(cv, [(36, 38), (60, 38), (57, 52), (53, 73), (43, 73), (39, 52)], FRAME, bias=-0.2)
    else:
        polygon(cv, [(33, 38), (63, 38), (60, 54), (55, 72), (41, 72), (36, 54)], body)
        # Plate seams.
        line(cv, 48, 58, 48, 71, plate[1])
        line(cv, 38, 55, 58, 55, plate[1])
    # Waist band.
    polygon(cv, [(40, 70), (56, 70), (57, 75), (39, 75)], JOINT, bevel=False)

    # Shoulder domes.
    for side in (-1, 1):
        ellipse(cv, 48 + side * 18, 43, 8, 7, JOINT if internals else body)

    # Head: helmet with little ear fins (the eyes are drawn on top later).
    if not internals:
        for side in (-1, 1):
            polygon(cv, [(48 + side * 12, 15), (48 + side * 16, 11), (48 + side * 15, 24), (48 + side * 12, 26)], body)
    ellipse(cv, 48, 21, 12.5, 12, FRAME if internals else body)
    return a


# ---------------------------------------------------------------------------------------------------- eyes and scale trims

def eye(cv, x, y, slant=0):
    """One glowing eye: a dark almond socket with a 3x2 light (the glow colors it) and a white glint.
    slant tilts the outer end down a pixel (A3's sharper look); pass -1 / 1 for the left / right eye."""
    for dx in range(-1, 4):
        cv.set(x + dx, y, JOINT[0])
        cv.set(x + dx, y + 1, JOINT[0])
    for dx in range(0, 3):
        cv.set(x + dx, y - 1, JOINT[1])
        cv.set(x + dx, y + 2, JOINT[1])
    for dx in range(0, 3):
        cv.set(x + dx, y, ACCENT, True)
        cv.set(x + dx, y + 1, ACCENT, True)
    if slant:
        outer = x + 2 if slant > 0 else x
        cv.set(outer, y, JOINT[0])               # the outer top corner cut: a narrower, angled eye
        cv.set(outer + slant, y + 1, ACCENT, True)
    cv.set(x if slant >= 0 else x + 2, y, hx('#FFFFFF'), True)


def eyes(cv, y, gap, slant=False):
    """A pair of eyes centred on the face."""
    eye(cv, 48 - gap // 2 - 3, y, -1 if slant else 0)
    eye(cv, 48 + gap // 2 + 1, y, 1 if slant else 0)


def scales(cv, x0, x1, y, rows=2):
    """A band of overlapping scales on armour: rows of small arcs, offset every other row, painted only over
    existing pixels. The arc bottoms glow (element color); the tips are dim, so it reads as trimmed scale armour."""
    for r in range(rows):
        yy = y + r * 2
        start = x0 + (r % 2) * 2
        for x in range(start, x1 - 2, 4):
            for px_, py_, c, g in ((x, yy, ACCENT_DIM, False), (x + 3, yy, ACCENT_DIM, False),
                                   (x + 1, yy + 1, ACCENT, True), (x + 2, yy + 1, ACCENT, True)):
                if cv.opaque(px_, py_):
                    cv.set(px_, py_, c, g)


def robot_details(cv, a, plate=PLATE):
    # Eyes, chest core and scale trims along the armour edges (all glow: the element color).
    eyes(cv, 21, 8)
    ellipse(cv, 48, 48, 3.6, 3.6, [ACCENT_DIM, ACCENT, ACCENT, hx('#FFFFFF')], glow=True)
    robot_scales(cv)
    for x, y in ((38, 41), (58, 41), (44, 66), (52, 66)):
        rivet(cv, x, y)


def robot_scales(cv):
    """Scale trims on the shared robot: shoulder dome rims, the chest plate's lower edge, forearm cuffs and shins."""
    for side in (-1, 1):
        sx = 48 + side * 18
        scales(cv, sx - 7, sx + 8, 46, 2)
        scales(cv, sx + side * 4 - 5, sx + side * 4 + 6, 80, 2)
        scales(cv, 48 + side * 9 - 5, 48 + side * 9 + 6, 116, 3)
    scales(cv, 38, 59, 53, 2)


def robot_internals(cv, a, core_ramp, rng):
    """Opened-up view of the shared robot: frame, rib struts, wiring, gears, coolant line and the core."""
    # Rib struts.
    for i, y in enumerate((42, 47, 52, 57, 62)):
        w = 10 - i
        line(cv, 48 - w, y, 48 + w, y, FRAME[3])
    line(cv, 48, 38, 48, 72, FRAME[4])  # spine
    # Wires down the torso and arms.
    for i, c in enumerate(WIRE_COLORS):
        x = 41 + i * 4
        curve(cv, [(x, 39), (x + (2 if i % 2 else -2), 52), (x, 64), (48 + (i - 1.5) * 3, 76)], c)
    for side in (-1, 1):
        sx = 48 + side * 18
        curve(cv, [(sx, 46), (sx + side * 2, 58), (sx + side * 3, 70), (sx + side * 5, 88)], WIRE_COLORS[0 if side < 0 else 1])
        curve(cv, [(48 + side * 8, 82), (48 + side * 9, 96), (48 + side * 10, 120), (48 + side * 10, 130)], WIRE_COLORS[2])
        gear(cv, sx, 43, 4)
        gear(cv, sx + side * 3, 70, 3)
        gear(cv, 48 + side * 9, 106, 3)
        gear(cv, 48 + side * 8, 80, 3, COPPER)
    # Coolant line.
    curve(cv, [(37, 40), (34, 52), (38, 66), (46, 74)], hx('#5AC8D8'))
    # Head internals: a little processor above the eyes.
    polygon(cv, [(43, 11), (53, 11), (53, 18), (43, 18)], ramp('#1E3A2A', '#2E5A40', '#3E7A56'), bevel=False)
    for k in range(3):
        line(cv, 44, 12 + k * 2, 52, 12 + k * 2, hx('#7ACB9A'))
    eyes(cv, 22, 8)
    # Core housing + core.
    ellipse(cv, 48, 50, 7, 7, COPPER)
    ellipse(cv, 48, 50, 5, 5, core_ramp, glow=True)


# ---------------------------------------------------------------------------------------------------- Ashe: overgrown

def leaf(cv, x, y, angle, size, rng):
    for i in range(size):
        t = i / max(1, size - 1)
        w = int(round(math.sin(t * math.pi) * size * 0.35))
        cx, cy = x + math.cos(angle) * i, y + math.sin(angle) * i
        for k in range(-w, w + 1):
            px_, py_ = cx - math.sin(angle) * k, cy + math.cos(angle) * k
            shade = 3 if k < 0 else 2
            if i == 0 or abs(k) == w:
                shade -= 1
            cv.set(round(px_), round(py_), LEAF[max(0, min(4, shade + (1 if t < 0.4 else 0)))])
        cv.set(round(cx), round(cy), LEAF[1])  # midrib


def flower(cv, x, y, glow=False):
    for dx, dy in ((0, -2), (2, 0), (0, 2), (-2, 0), (-1, -1), (1, -1), (1, 1), (-1, 1)):
        cv.set(x + dx, y + dy, PETAL[2] if dy < 0 else PETAL[1])
    for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0)):
        cv.set(x + dx, y + dy, PETAL[1])
    cv.set(x, y, ACCENT if glow else hx('#F2D45A'), glow)


def moss(cv, cx, cy, r, rng, density=0.7):
    for y in range(cy - r, cy + r + 1):
        for x in range(cx - r, cx + r + 1):
            d = math.sqrt((x - cx) ** 2 + (y - cy) ** 2) / max(1, r)
            if d <= 1 and cv.opaque(x, y) and cv.get(x, y) in PLATE and rng.random() < density * (1.15 - d):
                cv.set(x, y, MOSS[min(3, int(rng.random() * 2 + (1 - d) * 2))])


def overgrowth(cv, rng, internals=False):
    # Vines spiralling round the limbs.
    vines = [
        [(30, 44), (26, 52), (31, 60), (27, 70), (30, 80), (26, 90)],
        [(66, 44), (70, 54), (65, 62), (70, 72), (67, 84), (72, 92)],
        [(41, 82), (37, 92), (42, 100), (37, 112), (41, 124), (38, 134)],
        [(56, 84), (60, 96), (55, 108), (59, 120), (56, 130)],
        [(40, 40), (46, 46), (52, 44), (57, 52), (52, 62), (44, 70)],
    ]
    for v in vines:
        pts = curve(cv, v, LEAF[1])
        for i in range(6, len(pts) - 2, 9):
            x, y = pts[i]
            leaf(cv, round(x), round(y), rng.uniform(0, 2 * math.pi), rng.choice((4, 5, 5, 6)), rng)
    # A sapling growing out of the right shoulder, leaves fanning out.
    curve(cv, [(68, 40), (71, 32), (70, 24), (74, 16)], BARK[1], width=2)
    curve(cv, [(71, 30), (77, 26), (81, 27)], BARK[1])
    for x, y, ang, s in ((74, 16, -1.9, 7), (75, 18, -0.6, 7), (72, 20, -2.6, 6), (81, 27, -0.3, 6), (79, 25, -1.4, 5)):
        leaf(cv, x, y, ang, s, rng)
    # Leaf crown on the helmet, with flowers (the flower hearts glow).
    for i, (x, y) in enumerate(((38, 12), (42, 9), (47, 8), (52, 8), (57, 10), (60, 13))):
        leaf(cv, x, y, -math.pi / 2 + (i - 2.5) * 0.45, 5, rng)
    flower(cv, 41, 10, glow=True)
    flower(cv, 55, 9, glow=True)
    flower(cv, 28, 58)
    flower(cv, 69, 66, glow=True)
    flower(cv, 39, 118)
    if not internals:
        for cx, cy, r in ((32, 41, 4), (64, 44, 3), (52, 62, 4), (40, 126, 3), (58, 98, 3), (46, 18, 3)):
            moss(cv, cx, cy, r, rng)
    # Hanging moss strands under the arms.
    for x in (24, 72):
        for k in range(rng.randint(3, 5)):
            cv.set(x + rng.randint(-1, 1), 98 + k, MOSS[1 + k % 2])


def roots_internals(cv, rng):
    """Inside Ashe, roots thread through the frame and wrap a glowing seed core."""
    for start, pts in (((48, 50), [(48, 50), (42, 58), (44, 68), (40, 78), (42, 90)]),
                       ((48, 50), [(48, 50), (55, 60), (52, 70), (57, 80), (55, 96)]),
                       ((48, 50), [(48, 50), (38, 46), (32, 52), (28, 64), (27, 80)]),
                       ((48, 50), [(48, 50), (58, 44), (64, 52), (68, 66), (70, 84)])):
        curve(cv, pts, BARK[2])
    for x, y in ((42, 60), (55, 66), (31, 58), (66, 58), (41, 84), (56, 90)):
        leaf(cv, x, y, rng.uniform(0, 6.28), 4, rng)


# ---------------------------------------------------------------------------------------------------- Lucy: virus

def cable_tails(cv, glow_tips=True):
    """Two cable bundles from the back of the helmet, like twin-tails, with glowing plug tips."""
    for side in (-1, 1):
        base_x = 48 + side * 12
        pts = [(base_x, 16), (base_x + side * 8, 22), (base_x + side * 10, 40), (base_x + side * 7, 60), (base_x + side * 11, 78)]
        for off, c in ((0, JOINT[2]), (1, JOINT[3]), (2, JOINT[1])):
            curve(cv, [(x + side * off, y) for x, y in pts], c)
        tip = pts[-1]
        polygon(cv, [(tip[0] - 2 + side, tip[1]), (tip[0] + 3 + side, tip[1]), (tip[0] + 3 + side, tip[1] + 5), (tip[0] - 2 + side, tip[1] + 5)], JOINT, bevel=False)
        if glow_tips:
            line(cv, tip[0] - 1 + side, tip[1] + 6, tip[0] + 2 + side, tip[1] + 6, ACCENT, glow=True)
    # Clamp rings where the cables leave the helmet.
    for side in (-1, 1):
        ellipse(cv, 48 + side * 12, 17, 3, 3, JOINT)
        cv.set(48 + side * 12, 17, ACCENT, True)


def circuit_traces(cv, rng):
    traces = [
        [(38, 42), (42, 42), (42, 50), (36, 50)],
        [(58, 42), (54, 42), (54, 47)],
        [(44, 60), (44, 66), (40, 66)],
        [(52, 60), (52, 64), (58, 64)],
        [(30, 74), (30, 80), (28, 82)],
        [(66, 74), (66, 82), (68, 84)],
        [(40, 112), (40, 120), (42, 122)],
        [(56, 112), (56, 118), (54, 120)],
    ]
    for t in traces:
        for i in range(len(t) - 1):
            line(cv, t[i][0], t[i][1], t[i + 1][0], t[i + 1][1], ACCENT, glow=True)
        cv.set(t[-1][0], t[-1][1], hx('#FFFFFF'), True)


def glitch(cv, rng, rows=5):
    """Shifts a few short rows sideways, a corrupted-signal look."""
    for _ in range(rows):
        y = rng.randint(8, H - 10)
        shift = rng.choice((-3, -2, 2, 3))
        x0 = rng.randint(20, 50)
        x1 = x0 + rng.randint(10, 26)
        row = cv.px[y][:]
        g = cv.glow[y][:]
        for x in range(x0, x1):
            sx = x - shift
            if 0 <= sx < W and 0 <= x < W and row[sx][3] > 0:
                cv.px[y][x] = row[sx]
                cv.glow[y][x] = g[sx]
        # A bright glitch pixel at the tear.
        if 0 <= x1 < W and cv.opaque(x1 - 1, y):
            cv.set(x1 - 1, y, ACCENT, True)


def virus_internals(cv, rng):
    """Inside Lucy, a data crystal core with packets of 'virus' spreading through the wiring."""
    polygon(cv, [(48, 43), (53, 50), (48, 57), (43, 50)], [ACCENT_DIM, ACCENT, ACCENT, hx('#FFFFFF')], glow=True)
    for _ in range(26):
        x, y = rng.randint(30, 66), rng.randint(36, 128)
        if cv.opaque(x, y):
            cv.set(x, y, ACCENT, True)
            if rng.random() < 0.4:
                cv.set(x + 1, y, ACCENT, True)


# ---------------------------------------------------------------------------------------------------- A3: the standout model

def mc_body(cv, internals=False):
    """Taller, slimmer frame: a smooth oval head with sharp eyes, a halo crest, a long coat with tails,
    pointed shoulder guards and long legs."""
    body = FRAME if internals else PLATE_WARM
    coat = FRAME if internals else COAT

    # Coat tails behind the legs (the coat is open at the front).
    if not internals:
        polygon(cv, [(34, 56), (62, 56), (72, 128), (62, 122), (56, 132), (48, 112), (40, 132), (34, 122), (24, 128)], coat)
        line(cv, 25, 127, 34, 60, TRIM[1])
        line(cv, 71, 127, 62, 60, TRIM[1])
    # Legs: long and slim.
    for side in (-1, 1):
        hx_ = 48 + side * 6
        capsule(cv, hx_, 78, hx_ + side * 1, 104, 5, 4, JOINT if internals else body)
        ellipse(cv, hx_ + side * 1, 106, 3.5, 3.5, JOINT)
        capsule(cv, hx_ + side * 1, 108, hx_ + side * 1, 134, 4.5, 3.5, JOINT if internals else body)
        polygon(cv, [(hx_ + side - 5, 134), (hx_ + side + 4, 134), (hx_ + side + 5 + side * 2, 141), (hx_ + side - 6 + side * 2, 141)], JOINT if internals else COAT)
    ellipse(cv, 48, 77, 10, 5, JOINT)
    # Arms, slimmer than the shared robot's.
    for side in (-1, 1):
        sx = 48 + side * 15
        capsule(cv, sx, 46, sx + side * 2, 68, 3.5, 3.5, JOINT if internals else coat)
        ellipse(cv, sx + side * 2, 70, 3, 3, JOINT)
        capsule(cv, sx + side * 2, 72, sx + side * 3, 92, 3.5, 4, JOINT if internals else body)
        ellipse(cv, sx + side * 3, 96, 3.5, 4.5, JOINT)
    # Neck, torso (fitted), coat collar.
    capsule(cv, 48, 30, 48, 38, 3.5, 4, JOINT)
    if internals:
        polygon(cv, [(38, 38), (58, 38), (55, 54), (52, 72), (44, 72), (41, 54)], FRAME, bias=-0.2)
    else:
        polygon(cv, [(36, 38), (60, 38), (57, 56), (53, 72), (43, 72), (39, 56)], body)
        polygon(cv, [(34, 36), (42, 36), (46, 52), (40, 58), (35, 46)], coat)  # left lapel
        polygon(cv, [(54, 36), (62, 36), (61, 46), (56, 58), (50, 52)], coat)  # right lapel
        line(cv, 42, 37, 46, 52, TRIM[2])
        line(cv, 54, 37, 50, 52, TRIM[2])
    polygon(cv, [(42, 70), (54, 70), (55, 74), (41, 74)], JOINT, bevel=False)
    # Pointed shoulder guards.
    for side in (-1, 1):
        sx = 48 + side * 15
        polygon(cv, [(sx - 7, 46), (sx - 5, 40), (sx + side * 2, 37), (sx + 7, 41), (sx + 8, 47)], JOINT if internals else body)
        if not internals:
            line(cv, sx - 5, 46, sx + 7, 46, TRIM[0])
    # Head: a smooth oval (the eyes are drawn on top later).
    ellipse(cv, 48, 20, 10.5, 11.5, FRAME if internals else body)


def mc_details(cv, internals=False):
    # Halo crest: a thin ring floating above the head, plus a fin.
    for k in range(64):
        a = k / 64 * 2 * math.pi
        x, y = 48 + math.cos(a) * 13, 6 + math.sin(a) * 3
        if y < 6 or abs(math.cos(a)) > 0.4:  # the back of the ring hides behind the head
            cv.set(round(x), round(y), ACCENT, True)
    polygon(cv, [(47, 4), (49, 4), (50, 10), (46, 10)], PLATE_WARM, bevel=False)
    # Sharp, slanted eyes.
    eyes(cv, 20, 6, slant=True)
    # Chest core: a diamond.
    polygon(cv, [(48, 42), (52, 47), (48, 52), (44, 47)], [ACCENT_DIM, ACCENT, ACCENT, hx('#FFFFFF')], glow=True)
    # Coat seams glow faintly along the hem.
    if not internals:
        line(cv, 26, 127, 34, 121, ACCENT, True)
        line(cv, 70, 127, 62, 121, ACCENT, True)
    # Scale trims: shoulder guards, chest, forearm cuffs, shins and (on the coat) the hem.
    for side in (-1, 1):
        sx = 48 + side * 15
        scales(cv, sx - 6, sx + 8, 42, 2)
        scales(cv, sx + side * 3 - 4, sx + side * 3 + 5, 84, 2)
        scales(cv, 48 + side * 7 - 4, 48 + side * 7 + 5, 118, 3)
    scales(cv, 40, 57, 54, 2)
    if not internals:
        scales(cv, 26, 71, 120, 2)


def mc_internals(cv, rng):
    """A3 opened up: a frame of struts and a multi-chamber element core. Each chamber is ready for one
    element (they show the six element tints faintly; the glow decides the main color)."""
    for i, y in enumerate((42, 47, 52, 57, 62)):
        w = 9 - i
        line(cv, 48 - w, y, 48 + w, y, FRAME[3])
    line(cv, 48, 36, 48, 72, FRAME[4])
    for i, c in enumerate(WIRE_COLORS):
        curve(cv, [(41 + i * 4, 38), (40 + i * 5, 56), (44 + i * 3, 72)], c)
    for side in (-1, 1):
        gear(cv, 48 + side * 15, 43, 4)
        gear(cv, 48 + side * 17, 70, 3)
        gear(cv, 48 + side * 7, 106, 3)
        curve(cv, [(48 + side * 15, 47), (48 + side * 17, 60), (48 + side * 18, 74), (48 + side * 18, 92)], WIRE_COLORS[1])
        curve(cv, [(48 + side * 6, 80), (48 + side * 7, 100), (48 + side * 7, 132)], WIRE_COLORS[2])
    # Six chambers around the core.
    tints = [hx('#FFDC80'), hx('#9EC6F2'), hx('#F2805A'), hx('#80EB72'), hx('#8CF2F2'), hx('#C873FF')]
    ellipse(cv, 48, 50, 10, 10, COPPER)
    for k, t in enumerate(tints):
        a = k / 6 * 2 * math.pi - math.pi / 2
        x, y = round(48 + math.cos(a) * 6.5), round(50 + math.sin(a) * 6.5)
        ellipse(cv, x, y, 2.2, 2.2, [shade_t(t, 0.5), shade_t(t, 0.75), t])
    ellipse(cv, 48, 50, 3.5, 3.5, [ACCENT_DIM, ACCENT, hx('#FFFFFF')], glow=True)
    # Head: processor (the eyes come from mc_details).
    polygon(cv, [(43, 10), (53, 10), (53, 16), (43, 16)], ramp('#1E2A3A', '#2E405A', '#3E567A'), bevel=False)
    for k in range(3):
        line(cv, 44, 11 + k * 2, 52, 11 + k * 2, hx('#8AB4E8'))


def shade_t(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c[:3]) + (255,)


# ---------------------------------------------------------------------------------------------------- build

def ashe(internals):
    rng = random.Random(11)
    cv = Canvas()
    a = robot_body(cv, PLATE, internals)
    if internals:
        robot_internals(cv, a, [hx('#2E5A2C'), ACCENT_DIM, ACCENT, hx('#FFFFFF')], rng)
        roots_internals(cv, rng)
        overgrowth(cv, rng, internals=True)
    else:
        robot_details(cv, a)
        overgrowth(cv, rng)
    return cv


def lucy(internals):
    rng = random.Random(23)
    cv = Canvas()
    cable_tails(cv)
    a = robot_body(cv, PLATE, internals)
    if internals:
        robot_internals(cv, a, [ACCENT_DIM, ACCENT, ACCENT, hx('#FFFFFF')], rng)
        virus_internals(cv, rng)
    else:
        robot_details(cv, a)
        circuit_traces(cv, rng)
    glitch(cv, rng, rows=4 if internals else 5)
    return cv


def a3(internals):
    rng = random.Random(5)
    cv = Canvas()
    mc_body(cv, internals)
    if internals:
        mc_internals(cv, rng)
    mc_details(cv, internals)
    return cv


CHARACTERS = {'A3': a3, 'Ashe': ashe, 'Lucy': lucy}


def finish(cv):
    outline(cv)
    halo(cv)
    return cv


def build(name):
    os.makedirs(OUT, exist_ok=True)
    results = {}
    for side in ('front', 'internals'):
        cv = finish(CHARACTERS[name](side == 'internals'))
        write_png(os.path.join(OUT, '%s_%s.png' % (name, side)), W, H, cv.px)
        write_png(os.path.join(OUT, '%s_%s_glow.png' % (name, side)), W, H, glow_pixels(cv))
        results[side] = cv
    return results


GLOW_PREVIEW = {'A3': (255, 255, 255), 'Ashe': (128, 235, 115), 'Lucy': (200, 115, 255)}


def sheet(path, built, scale=4):
    """Contact sheet: front and internals per character, glow tinted, on a dark card background."""
    names = list(built)
    cols = len(names) * 2
    pad = 8
    sw, sh = cols * (W + pad) + pad, H + pad * 2
    bg = (34, 33, 40, 255)
    canvas = [[bg] * sw for _ in range(sh)]
    for i, name in enumerate(names):
        for j, side in enumerate(('front', 'internals')):
            cv = built[name][side]
            ox, oy = pad + (i * 2 + j) * (W + pad), pad
            tint = GLOW_PREVIEW[name]
            for y in range(H):
                for x in range(W):
                    p = cv.px[y][x]
                    g = cv.glow[y][x]
                    base = canvas[oy + y][ox + x]
                    if p[3] > 0:
                        base = p
                    if g > 0:
                        if g >= 1.0:
                            base = (tint[0] * base[0] // 255 + 0, tint[1] * base[1] // 255, tint[2] * base[2] // 255, 255)
                        else:
                            base = tuple(int(base[k] + (tint[k] - base[k]) * g) for k in range(3)) + (255,)
                    canvas[oy + y][ox + x] = base
    big = [[canvas[y // scale][x // scale] for x in range(sw * scale)] for y in range(sh * scale)]
    write_png(path, sw * scale, sh * scale, big)


def main():
    args = sys.argv[1:]
    sheet_path = None
    if '--sheet' in args:
        sheet_path = args[args.index('--sheet') + 1]
    built = {name: build(name) for name in CHARACTERS}
    if sheet_path:
        sheet(sheet_path, built)
    print('Portraits written to', OUT)


if __name__ == '__main__':
    main()
