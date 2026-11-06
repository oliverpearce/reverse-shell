"""
ChibiForge: draws every character as chibi pixel art from one parametric rig (our own art, no third-party assets).

    python Tools/ChibiForge/chibi_forge.py                  # every character: sprites + card portraits
    python Tools/ChibiForge/chibi_forge.py A3 Lucy          # only these
    python Tools/ChibiForge/chibi_forge.py --sheet out.png  # also a contact sheet of every character

Style: big-headed chibi proportions, a bold dark plum outline around and between parts, 4-tone ramps lit from the
top left, no dithering, limited warm palette. The party are androids (faces, hair, armour plates and glowing
circuit lines in their element colour); the enemies are round machine lifeforms; the NPCs are ruin survivors.

Output (same layout the game already uses, so nothing else changes):
  Assets/_Project/Art/Characters/<Name>/idle|walk|slash|spellcast|hurt.png
      64 px frames; rows up, left, down, right (hurt has one row); 8 / 13 / 10 / 12 / 9 frames
      (walk frame 0 is the standing pose). Poses are eased curves sampled per frame, so motion is smooth.
  Assets/_Project/Art/Portraits/<Name>_front|internals[_glow].png   (party only)
      96 x 144 card art: the rig in a hero pose at the reference's pixel density (48 x 72, doubled); *_glow are white masks of the glowing
      pixels, tinted in game by the element colour.

How a character is built: Spec (palettes + parts) -> Pose (per frame: bob, arm / leg / weapon positions, effects)
-> draw(spec, pose, facing, k) paints parts back to front into a Canvas, each part shaded by its own ramp, with
dark separation lines where it overlaps earlier parts, then a final outline pass.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'SpriteForge'))
from pngtool import write_png  # noqa: E402

CHARS = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')
PORTRAITS = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Portraits')
F = 64          # native 64 px frames
RIG_K = 1
OUTLINE = (32, 2, 29, 255)          # the reference's outline: #20021D
CLEAR = (0, 0, 0, 0)
UP, LEFT, DOWN, RIGHT = 0, 1, 2, 3


def hx(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


def ramp(*hexes):
    return [hx(h) for h in hexes]


# ---------------------------------------------------------------------------------------------------- canvas

class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = [[CLEAR] * w for _ in range(h)]
        self.glow = [[False] * w for _ in range(h)]

    def ok(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h

    def opaque(self, x, y):
        return self.ok(x, y) and self.px[y][x][3] > 0

    def set(self, x, y, c, glow=False):
        if self.ok(x, y):
            self.px[y][x] = c
            self.glow[y][x] = glow


class Part:
    """A set of pixels painted as one shaded piece."""

    def __init__(self):
        self.pts = set()

    def add(self, x, y):
        self.pts.add((int(x), int(y)))

    def ellipse(self, cx, cy, rx, ry):
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                if ((x + 0.5 - cx) / max(0.5, rx)) ** 2 + ((y + 0.5 - cy) / max(0.5, ry)) ** 2 <= 1.0:
                    self.add(x, y)
        return self

    def rect(self, x0, y0, x1, y1):
        for y in range(int(round(y0)), int(round(y1))):
            for x in range(int(round(x0)), int(round(x1))):
                self.add(x, y)
        return self

    def poly(self, pts):
        xs, ys = [p[0] for p in pts], [p[1] for p in pts]
        for y in range(int(min(ys)) - 1, int(max(ys)) + 2):
            for x in range(int(min(xs)) - 1, int(max(xs)) + 2):
                if inside(pts, x + 0.5, y + 0.5):
                    self.add(x, y)
        return self

    def limb(self, a, b, r0, r1=None):
        """A thick segment from a to b (radius r0 at a, r1 at b)."""
        r1 = r0 if r1 is None else r1
        ax, ay = a
        bx, by = b
        dx, dy = bx - ax, by - ay
        L2 = dx * dx + dy * dy or 1.0
        R = max(r0, r1) + 1
        for y in range(int(min(ay, by) - R), int(max(ay, by) + R) + 1):
            for x in range(int(min(ax, bx) - R), int(max(ax, bx) + R) + 1):
                px_, py_ = x + 0.5 - ax, y + 0.5 - ay
                t = max(0.0, min(1.0, (px_ * dx + py_ * dy) / L2))
                r = r0 + (r1 - r0) * t
                if (px_ - dx * t) ** 2 + (py_ - dy * t) ** 2 <= r * r:
                    self.add(x, y)
        return self

    def merge(self, other):
        self.pts |= other.pts
        return self

    def shifted(self, dx, dy):
        p = Part()
        p.pts = {(x + dx, y + dy) for x, y in self.pts}
        return p


def inside(pts, x, y):
    c, j = False, len(pts) - 1
    for i in range(len(pts)):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / ((yj - yi) or 1e-9) + xi:
            c = not c
        j = i
    return c


def paint(cv, part, r, glow=False, flat=False, sep=True):
    """Shades a part with its 4-tone ramp (dark..light): light where the top / left edge meets the outside, dark
    on the bottom / right, and the darkest tone as a separation line where it overlaps something drawn earlier."""
    pts = part.pts
    edits = []
    for (x, y) in pts:
        if not cv.ok(x, y):
            continue
        c = r[2] if len(r) > 2 else r[-1]
        if not flat:
            if (x, y - 1) not in pts or (x - 1, y) not in pts:
                c = r[3] if len(r) > 3 else r[-1]
            if (x, y + 1) not in pts or (x + 1, y) not in pts:
                c = r[1] if len(r) > 1 else r[0]
        if sep:
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if (nx, ny) not in pts and cv.opaque(nx, ny) and not cv.glow[ny][nx]:
                    c = r[0]
                    break
        edits.append((x, y, c))
    for x, y, c in edits:
        cv.set(x, y, c, glow)


def trim_edge(cv, part, color, top=False):
    """Recolours a part's bottom (or top) rim pixels: the orange-style trim lines along armour plates."""
    dy = -1 if top else 1
    for (x, y) in part.pts:
        if (x, y + dy) not in part.pts and cv.ok(x, y):
            cv.set(x, y, color)


def dot(cv, x, y, c, glow=False):
    cv.set(int(x), int(y), c, glow)


def line(cv, x0, y0, x1, y1, c, glow=False):
    steps = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
    for i in range(steps + 1):
        t = i / max(1, steps)
        cv.set(int(round(x0 + (x1 - x0) * t)), int(round(y0 + (y1 - y0) * t)), c, glow)


def outline(cv):
    add = []
    for y in range(cv.h):
        for x in range(cv.w):
            if cv.px[y][x][3]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if cv.opaque(x + dx, y + dy):
                    add.append((x, y))
                    break
    for x, y in add:
        cv.set(x, y, OUTLINE)


def mirror(cv):
    out = Canvas(cv.w, cv.h)
    for y in range(cv.h):
        out.px[y] = cv.px[y][::-1]
        out.glow[y] = cv.glow[y][::-1]
    return out


def rotate_cw(cv):
    out = Canvas(cv.h, cv.w)
    for y in range(cv.h):
        for x in range(cv.w):
            out.px[x][cv.h - 1 - y] = cv.px[y][x]
            out.glow[x][cv.h - 1 - y] = cv.glow[y][x]
    return out


# ---------------------------------------------------------------------------------------------------- palettes

# Colours sampled from the reference sprite (Tools/ChibiForge/reference/A3.png): 16 colours in all.
REF_SKIN = ramp('#B35420', '#D2884A', '#E6BB9A', '#F4D8BE')
REF_SLATE = ramp('#434954', '#4F6072', '#617787', '#748492')
REF_DARK = ramp('#20021D', '#391C2F', '#472D38', '#5E4250')
REF_WHITE = ramp('#988E8E', '#B1ABA7', '#DDD6D2', '#F4F0EE')
REF_GOLD = ramp('#894B32', '#B35420', '#D2884A', '#E2BA4B')
REF_CRIMSON = ramp('#391C2F', '#921037', '#B0223F', '#C8445A')
SKIN_PALE = ramp('#9C6A64', '#D6A08C', '#F2CDB6', '#FBE6D6')
SKIN_WARM = ramp('#7A4A3A', '#B87A5A', '#E0A880', '#F4CDA8')
SKIN_TAN = ramp('#5E3A2E', '#94603E', '#C48A5E', '#E2B286')
BLACK_ARMOR = ramp('#15121C', '#2C2836', '#454052', '#676176')
WHITE_ARMOR = ramp('#5E5A66', '#9A97A4', '#D3D1DA', '#F4F3F7')
GOLD = ramp('#7A3A16', '#B8601E', '#E09A36', '#F6CE5A')
CRIMSON = ramp('#4A0E1E', '#7E1A2C', '#A82A38', '#D04A50')
STEEL = ramp('#3C4656', '#5E6E84', '#8E9EB4', '#D6DEEA')
RUST = ramp('#3A1A10', '#6E3418', '#A85A26', '#D88A40')
MOSSY = ramp('#24281A', '#3E4A2A', '#62723E', '#93A25A')
CLOTH_BROWN = ramp('#2E1E18', '#563A2A', '#86603E', '#B48A5A')
CLOTH_BLUE = ramp('#1A2238', '#2E3E62', '#4E6A92', '#7E9CC0')
CLOTH_GREEN = ramp('#1C2A20', '#2E4A34', '#4A6E4A', '#76986A')
CLOTH_RED = ramp('#3A1418', '#6A2028', '#9A3436', '#C45A4E')
CLOTH_GRAY = ramp('#24222A', '#3E3C46', '#5E5C68', '#8A8894')
LEAF = ramp('#1E3A1E', '#2E5E2A', '#4E8E3A', '#86C25A')
PETAL = ramp('#8E2E52', '#C8507A', '#EE8AAE', '#FCC6DA')
WHITE = (250, 248, 252, 255)
ORANGE_HI = ramp('#A8401A', '#E8641E', '#FF9A3A', '#FFE2B0')   # A3's glow: deep orange to white-hot


class Spec:
    """Everything that makes one character: palettes, hair style, armour / outfit, headgear, weapon, glow."""

    def __init__(self, **kw):
        self.kind = 'android'          # android | human | machine
        self.skin = SKIN_PALE
        self.hair = None
        self.hair_style = 'long'       # long | bob | twin | short | none
        self.armor = BLACK_ARMOR
        self.trim = GOLD
        self.under = BLACK_ARMOR       # bodysuit / pants
        self.glow = hx('#FFFFFF')
        self.eye = hx('#FFFFFF')
        self.headgear = None           # crown | earpiece | ornament | leaves | phones | band | cap | hood | None
        self.weapon = None             # katana | staff | dagger | piston | blade | None
        self.blade = STEEL
        self.cape = None               # ramp for coat tails / cape
        self.extra = None              # vines | cables | apron | bag
        self.machine = None            # (body ramp, eye colour) for machines
        self.back_hair = None          # a darker ramp for the long hair behind the body (None = the hair ramp)
        self.accent = CRIMSON          # small accent details (hair ornament, sword grip, armour gems)
        self.lines = True              # glowing circuit lines on the armour (androids)
        self.joint = None              # androids: dark leggings / sleeves ramp, with glowing knee and elbow pads and
                                       # glowing cores on the gloves (None = plate all the way down)
        self.hi = None                 # the glow ramp (dark..white-hot) for pads, cores, strike arcs and wing tips
        self.wings = None              # feathered wings behind the back (a 4-tone ramp) or None
        self.size = 1.0
        self.__dict__.update(kw)


SPECS = {
    # The crew all share A3's design: white plate with trim, dark leggings and sleeves with glowing knee and elbow
    # pads, big shoulder plates, gloves, hips and chest, dark gloves with glowing cores, an earpiece like a hair
    # ornament, the card's eyes (warm lash over a crimson-to-glow iris), unarmed (strikes leave a glowing arc).
    # Each member is a recolour of it; A3 glows orange.
    'A3': Spec(skin=REF_SKIN, hair=REF_SLATE, hair_style='long', armor=REF_WHITE,
               trim=ramp('#894B32', '#D2641E', '#FF8A2A', '#FFC060'), under=REF_SLATE, accent=REF_CRIMSON,
               glow=hx('#FF8A2A'), eye=hx('#FF9A3A'), headgear='earpiece', lines=False,
               joint=ramp('#14101A', '#221C2A', '#302838', '#443A4E'), hi=ORANGE_HI),
    'Ashe': Spec(skin=REF_SKIN, hair=ramp('#2E5232', '#3C6E36', '#5E9A48', '#8EC468'),
                    hair_style='long', armor=REF_WHITE,
                    trim=ramp('#1E4A1A', '#3E8A2E', '#6EC04A', '#B4EC7A'), under=REF_SLATE,
                    accent=ramp('#5A1E3A', '#B8406A', '#E07A9A', '#F4B4C8'), glow=hx('#8EF070'), eye=hx('#6EC04A'),
                    headgear='earpiece', lines=False,
                    joint=ramp('#0E140E', '#1A221A', '#263026', '#384438'),
                    hi=ramp('#2E6A1E', '#5EB03A', '#8EF070', '#E4FFD0')),
    'Lucy': Spec(skin=REF_SKIN, hair=ramp('#3E2052', '#5A2E78', '#7E48A4', '#A874CC'),
                 hair_style='long',
                 armor=ramp('#2A2834', '#3E3A4C', '#5E5A70', '#827E96'),
                 trim=ramp('#3A1458', '#7A3AB0', '#B070E8', '#E2B8FF'), under=REF_SLATE,
                 accent=ramp('#2A0E3A', '#5A1E7A', '#7A2E9E', '#A24EC4'), glow=hx('#D07AFF'), eye=hx('#B070E8'),
                 headgear='earpiece', lines=False,
                 joint=ramp('#120C18', '#1E1626', '#2C2236', '#3E3248'),
                 hi=ramp('#5A1E8A', '#9A48D8', '#D07AFF', '#F4DCFF')),
    # Machine lifeforms: round heads, barrel bodies, glowing red eyes.
    'RustSentry': Spec(kind='machine', machine=(RUST, hx('#FF5A3A')), trim=STEEL, weapon='piston', size=1.12),
    'ScrapStalker': Spec(kind='machine', machine=(MOSSY, hx('#FF8A3A')), trim=RUST, weapon='blade', blade=STEEL),
    # Ruin survivors.
    'NoodleVendor': Spec(kind='human', skin=SKIN_WARM, hair=ramp('#1E1410', '#3A2618', '#5A3E26', '#7E5A3A'),
                         hair_style='short', armor=CLOTH_RED, under=CLOTH_BROWN, trim=hx('#E8E2D2') and ramp('#8E887A', '#BEB6A6', '#E8E2D2', '#FFFFFF'),
                         headgear='band', extra='apron', eye=hx('#3A2618')),
    'Courier': Spec(kind='human', skin=SKIN_TAN, hair=ramp('#2A1A10', '#4A2E1A', '#704628', '#9A6838'), hair_style='short',
                    armor=CLOTH_BLUE, under=CLOTH_GRAY, trim=GOLD, headgear='cap', extra='bag', eye=hx('#2A1A10')),
    'Drifter': Spec(kind='human', skin=SKIN_PALE, hair=ramp('#4A4A52', '#7A7A84', '#A8A8B2', '#D4D4DC'), hair_style='short',
                    armor=CLOTH_GREEN, under=CLOTH_BROWN, trim=CLOTH_BROWN, headgear='hood', eye=hx('#4A5A6A')),
}

PARTY = ['A3', 'Ashe']
# Removed from the game (2026-11-03); their specs stay for reference, but a full run no longer rebuilds them.
RETIRED = {'Lucy', 'Blue', 'Ashe'}   # Ashe: A3's model now (alt_recolour.py), not the knight


# ---------------------------------------------------------------------------------------------------- poses

class Pose:
    def __init__(self, **kw):
        self.bob = 0             # whole body vertical offset
        self.lean = 0            # torso / head horizontal offset (facing direction is +)
        self.near_hand = None    # (dx, dy) of the near hand from its rest spot
        self.far_hand = None
        self.near_foot = (0, 0)  # (dx, dy) offsets of the feet
        self.far_foot = (0, 0)
        self.weapon_angle = None # degrees: 0 = forward, 90 = down, -90 = up; None = resting (raised over the shoulder)
        self.orb = 0             # spell orb radius at the near hand (0 = none)
        self.crouch = 0          # legs shortened by this much
        self.hair_sway = 0
        self.wing = 0            # wing tip lift (rig units): + raised / spread, - drooped
        self.trail = None        # near-hand offsets of the last frames: an unarmed strike leaves a glowing arc
        self.__dict__.update(kw)


def lerp_keys(keys, times, u):
    """Values at u (0..1) between keyframes (tuples of numbers, or None) placed at `times`, eased so each
    segment starts and ends gently (smoothstep)."""
    for j in range(len(times) - 1):
        if u <= times[j + 1] or j == len(times) - 2:
            t = (u - times[j]) / float(times[j + 1] - times[j])
            t = max(0.0, min(1.0, t))
            t = t * t * (3 - 2 * t)
            a, b = keys[j], keys[j + 1]
            return tuple(x + (y - x) * t for x, y in zip(a, b))
    return keys[-1]


def r(v):
    return int(round(v))


WALK_N = 12                       # frames in one stride (the walk sheet adds the standing frame 0)


def walk_pose(i):
    """Walk frame i (1..12; 0 is standing). Side views stride; front / back views step in place. The feet follow
    sine curves sampled finely, the body rises a pixel as the legs pass, and the hair trails the arm swing."""
    if i == 0:
        return Pose()
    t = (i - 1) / float(WALK_N) * 2 * math.pi
    s, c = math.sin(t), math.cos(t)
    return Pose(bob=-1 if math.cos(2 * t) > 0.3 else 0,
                near_foot=(r(3 * s), -max(0, r(2 * c))),
                far_foot=(r(-3 * s), -max(0, r(-2 * c))),
                near_hand=(r(-2 * s), 0), far_hand=(r(2 * s), 0), hair_sway=r(math.sin(t - 0.6)),
                wing=r(0.8 * math.cos(2 * t)))


IDLE_N = 8


def idle_pose(i):
    """A slow breath: the body sinks a pixel and rises again, the hair lagging a little behind."""
    t = i / float(IDLE_N) * 2 * math.pi
    return Pose(bob=r(0.5 - 0.5 * math.cos(t)), hair_sway=r(0.5 - 0.5 * math.cos(t - 1.0)),
                near_hand=(0, r(0.5 - 0.5 * math.cos(t - 0.5))), far_hand=(0, r(0.5 - 0.5 * math.cos(t - 0.5))),
                wing=r(1.5 * math.sin(t)))


# wind up (blade up and back), swing through, follow through, recover: (angle, hand x, hand y, lean)
SLASH_KEYS = [(-40, -2, -7, 0), (-80, -3, -10, -1), (-10, 3, -5, 1), (45, 5, 0, 2), (80, 4, 3, 1), (100, 1, 1, 0)]
SLASH_TIMES = [0.0, 0.3, 0.45, 0.55, 0.72, 1.0]     # a slow wind-up, a fast swing, an eased recovery
SLASH_N = 10


def slash_hand(i):
    _, hx_, hy, _ = lerp_keys(SLASH_KEYS, SLASH_TIMES, max(0, i) / float(SLASH_N - 1))
    return r(hx_), r(hy)


def slash_pose(i):
    a, hx_, hy, lean = lerp_keys(SLASH_KEYS, SLASH_TIMES, i / float(SLASH_N - 1))
    u = i / float(SLASH_N - 1)
    # unarmed: the hand leaves a glowing arc through the fast part of the swing
    trail = [slash_hand(j) for j in (i - 2, i - 1, i)] if 0.35 <= u <= 0.75 else None
    return Pose(weapon_angle=r(a), near_hand=(r(hx_), r(hy)), lean=r(lean), far_hand=(1, 0), trail=trail,
                wing=r(1 - 2 * lean))


# orb radius, hand x, hand y
CAST_KEYS = [(0, 2, -2), (2, 6, -5), (4, 7, -5), (4, 7, -5), (0, 2, -1)]
CAST_TIMES = [0.0, 0.3, 0.5, 0.75, 1.0]
CAST_N = 12


def cast_pose(i):
    rad, hx_, hy = lerp_keys(CAST_KEYS, CAST_TIMES, i / float(CAST_N - 1))
    rad = r(rad)
    return Pose(orb=rad, near_hand=(r(hx_), r(hy)), far_hand=(r(hx_) - 1, r(hy) + 1), weapon_angle=130,
                lean=1 if rad else 0, wing=r(rad * 0.6))


# lean, crouch: recoil back, then sink to the knees
HURT_KEYS = [(0, 0), (-2, 0), (-1, 2), (0, 8)]
HURT_TIMES = [0.0, 0.25, 0.5, 1.0]
HURT_STAND = 6                    # standing / kneeling frames before the fall (then: falling, bounce, lying)
HURT_N = HURT_STAND + 3


def hurt_pose(i):
    lean, crouch = lerp_keys(HURT_KEYS, HURT_TIMES, i / float(HURT_STAND - 1))
    return Pose(lean=r(lean), crouch=r(crouch), near_hand=(-2, 2), far_hand=(2, 2), weapon_angle=110,
                wing=-r(crouch * 0.4))


# ---------------------------------------------------------------------------------------------------- the rig

def draw(spec, pose, facing, k=1, hero=False, internals=False, w=F, h=F, ox=None, ground=None):
    """Paints one frame. facing: UP / LEFT / DOWN (RIGHT is LEFT mirrored by the caller). k scales the rig
    (2 = portrait detail). Returns a Canvas (not outlined yet)."""
    cv = Canvas(w, h)
    S = k * spec.size
    cx = (w / 2.0) if ox is None else ox
    gy = (h - (6 if h >= 128 else 4)) if ground is None else ground  # feet bottom (128 px frames: row 122)

    def P(x, y):
        """Rig coordinates (x around 0, y up from the ground in body units) -> canvas."""
        return cx + x * S, gy - y * S

    if spec.kind == 'machine':
        draw_machine(cv, spec, pose, facing, P, S)
        return cv

    side = facing in (LEFT, RIGHT)
    front = facing == DOWN
    fwd = -1  # left-facing: forward is -x
    bob = pose.bob
    crouch = pose.crouch
    hip_y = 19 - crouch
    body_dx = (pose.lean * fwd) if side else 0

    # ---- positions (rig units: y up from the ground)
    head_c = (body_dx * 1.0, 41 - bob - crouch)
    torso_top = 33 - bob - crouch
    sh_y = 32 - bob - crouch
    if side:
        sh_near = (body_dx - 0.5, sh_y)
        sh_far = (body_dx + 1.5, sh_y)
    else:
        sw = 6.6 if spec.joint else 5.0   # the crew's broad breastplate pushes the shoulders out
        sh_near = (-sw, sh_y)
        sh_far = (sw, sh_y)
    rest = (0, -11)
    nh = pose.near_hand or (0, 0)
    fh = pose.far_hand or (0, 0)

    def near_hand_at(o):
        if side:
            return sh_near[0] + fwd * o[0] + fwd * 0.5, sh_near[1] - 14 - o[1]
        return sh_near[0] - 0.8 + o[0] * (1 if not front else -1) * 0.5, sh_near[1] - 14 - o[1]

    hand_near = near_hand_at(nh)
    if side:
        hand_far = (sh_far[0] + fwd * fh[0] + 1, sh_far[1] - 14 - fh[1])
    else:
        hand_far = (sh_far[0] + 0.8 + fh[0] * 0.5, sh_far[1] - 14 - fh[1])
    if hero:
        hand_near = (sh_near[0] - 4, sh_near[1] + 2)   # sword hand raised beside the head
        hand_far = (sh_far[0] + 2, sh_far[1] - 9)

    # ---- back layers: wings / long hair / cape / twin tails behind the body
    if spec.wings and facing != UP:
        wings(cv, spec, pose, facing, P, sh_y, body_dx)
    if spec.cape and facing != UP:
        cape = Part()
        if side:
            cape.poly([P(body_dx + 2, sh_y), P(body_dx + 6, sh_y - 2), P(body_dx + 9 + pose.hair_sway, 3), P(body_dx + 2, 4)])
        else:
            cape.poly([P(-6, sh_y), P(6, sh_y), P(8, 3), P(3, 5), P(0, 3), P(-3, 5), P(-8, 3)])
        paint(cv, cape, spec.cape)
    if spec.hair and spec.hair_style in ('long', 'twin') and facing != UP:
        back_hair(cv, spec, pose, facing, P, head_c)
    if spec.extra == 'cables' and facing != UP:
        cables(cv, spec, pose, facing, P, head_c)

    # ---- far arm
    if facing != UP:
        arm(cv, spec, sh_far, hand_far, P, far=True, internals=internals)

    # ---- legs
    legs(cv, spec, pose, facing, P, hip_y, body_dx, internals)

    # ---- torso
    torso(cv, spec, pose, facing, P, torso_top, hip_y, body_dx, internals)

    # ---- weapon behind the near arm when pointing back, in front otherwise
    if spec.weapon and not hero:
        weapon(cv, spec, pose, facing, P, hand_near, S)
    if hero and spec.weapon:
        weapon(cv, spec, Pose(weapon_angle=-55), DOWN, P, hand_near, S, hero=True)

    # ---- near arm
    if facing == UP:
        arm(cv, spec, sh_near, hand_near, P, far=False, internals=internals)
        arm(cv, spec, sh_far, hand_far, P, far=False, internals=internals)
    else:
        arm(cv, spec, sh_near, hand_near, P, far=False, internals=internals)

    # ---- from behind, the wings sit over the back and arms (the hair falls over their roots)
    if spec.wings and facing == UP:
        wings(cv, spec, pose, facing, P, sh_y, body_dx)

    # ---- head
    head(cv, spec, pose, facing, P, head_c, internals)
    if facing == UP and spec.hair and spec.hair_style in ('long', 'twin'):
        back_hair(cv, spec, pose, facing, P, head_c, over=True)
    if facing == UP and spec.extra == 'cables':
        cables(cv, spec, pose, facing, P, head_c)

    # ---- spell orb
    if pose.orb:
        hx_, hy_ = P(*hand_near)
        if side:
            hx_ += fwd * 3 * S
        orb = Part().ellipse(hx_, hy_ - 1 * S, (1 + pose.orb * 0.8) * S, (1 + pose.orb * 0.8) * S)
        paint(cv, orb, [spec.glow, spec.glow, spec.glow, WHITE], glow=True, sep=False)
        dot(cv, hx_, hy_ - 1 * S, WHITE, True)
    # ---- unarmed strike: a glowing arc along the hand's recent path
    if pose.trail and not spec.weapon and facing != UP:
        hi = spec.hi or [spec.glow] * 3 + [WHITE]
        pts = [P(*near_hand_at(o)) for o in pose.trail]
        off = (-2 if side else 0) * S
        for j in range(len(pts) - 1):
            (x0, y0), (x1, y1) = pts[j], pts[j + 1]
            line(cv, x0 + off, y0, x1 + off, y1, hi[1] if j == 0 else hi[2], True)
            line(cv, x0 + off, y0 - 1, x1 + off, y1 - 1, hi[2] if j == 0 else hi[3], True)
    return cv


def wings(cv, spec, pose, facing, P, sh_y, body_dx):
    """Feathered wings from the shoulder blades: the wing ramp with darker feather lines and glowing tips. Front /
    back views spread both; side views show the near wing swept back (+x when facing left) over a darker far one."""
    w = pose.wing
    root_y = sh_y - 2
    hi = spec.hi or [spec.glow] * 3 + [WHITE]

    def one(shape, ramp_, feathers, tips):
        part = Part().poly([P(x, y) for x, y in shape])
        paint(cv, part, ramp_)
        for (ax, ay), (bx, by) in feathers:
            a, b = P(ax, ay), P(bx, by)
            line(cv, a[0], a[1], b[0], b[1], ramp_[1])
        if tips:
            # glowing tips along the outer feather edge
            for x, y in shape[2:5]:
                dot(cv, *P(x, y), hi[2], True)

    if facing in (LEFT, RIGHT):
        b = body_dx + 1.5
        for far, dx in ((True, 2.5), (False, 0)):
            B = b + dx
            shape = [(B, root_y + 2), (B + 5, root_y + 7 + w), (B + 11, root_y + 11 + w),
                     (B + 13, root_y + 5 + w * 0.6), (B + 11, root_y - 3 + w * 0.3),
                     (B + 7, root_y - 7), (B + 2, root_y - 4)]
            feathers = [((B + 3, root_y), (B + 11, root_y + 4 + w * 0.6)),
                        ((B + 3, root_y - 2), (B + 9, root_y - 3 + w * 0.3))]
            one(shape, dim(spec.wings) if far else spec.wings, feathers, not far)
        return
    for sgn in (-1, 1):
        X = lambda x: body_dx + sgn * x
        shape = [(X(3), root_y + 2), (X(8), root_y + 8 + w), (X(14), root_y + 11 + w),
                 (X(16), root_y + 5 + w * 0.6), (X(14), root_y - 2 + w * 0.3), (X(10), root_y - 7), (X(5), root_y - 4)]
        feathers = [((X(5), root_y + 1), (X(14), root_y + 5 + w * 0.6)), ((X(5), root_y - 1), (X(12), root_y - 3 + w * 0.3))]
        one(shape, spec.wings, feathers, True)


def back_hair(cv, spec, pose, facing, P, head_c, over=False):
    hcx, hcy = head_c
    Q = lambda dx, dy: P(head_c[0] + dx * HEAD, head_c[1] + dy * HEAD)
    sway = pose.hair_sway
    hair = Part()
    if spec.hair_style == 'long':
        if facing in (LEFT, RIGHT):
            hair.poly([Q(0 - 2, 0 + 8), Q(0 + 8, 0 + 6), Q(0 + 10 + sway, 0 - 18), Q(0 + 6 + sway, 0 - 22), Q(0 + 1, 0 - 10)])
        else:
            hair.poly([Q(0 - 9, 0 + 4), Q(0 + 9, 0 + 4), Q(0 + 10 + sway, 0 - 20), Q(0 + 4, 0 - 22),
                       Q(0, 0 - 19), Q(0 - 4, 0 - 22), Q(0 - 10 + sway, 0 - 20)])
    else:  # twin tails
        for sgn in ((-1, 1) if facing not in (LEFT, RIGHT) else (1,)):
            bx = hcx + sgn * 8 if facing not in (LEFT, RIGHT) else hcx + 6
            hair.poly([P(bx - 2, hcy + 5), P(bx + 2, hcy + 5), P(bx + sgn * 4 + sway, hcy - 8), P(bx + sgn * 2 + sway, hcy - 17), P(bx - sgn * 1, hcy - 9)])
    if over:
        # from behind the hair is one piece: the crown (the head's hair ellipse) flows into the long hair in the
        # same ramp, with darker strand lines and a light shine across the crown
        s = P(1, 0)[0] - P(0, 0)[0]
        cx, cy = Q(0, 0)
        hair.merge(Part().ellipse(cx, cy, 8.4 * s * HEAD + 0.6 * s, 8.0 * s * HEAD + 0.4 * s))
        paint(cv, hair, spec.hair)
        for dx in (-5, -2, 2, 5):
            a, b = Q(dx * 0.6, 4), Q(dx + sway * 0.5, -19)
            line(cv, a[0], a[1], b[0], b[1], spec.hair[1])
        sx, sy = Q(-4, 6)
        for d in range(int(6 * s)):
            dot(cv, sx + d, sy + (d % 3 == 0), spec.hair[3])
        return
    paint(cv, hair, spec.back_hair or spec.hair)


def cables(cv, spec, pose, facing, P, head_c):
    """Lucy's cable twin-tails end in glowing plugs."""
    hcx, hcy = head_c
    for sgn in ((-1, 1) if facing not in (LEFT, RIGHT) else (1,)):
        bx = hcx + sgn * 8 if facing not in (LEFT, RIGHT) else hcx + 7
        tip = P(bx + sgn * 3 + pose.hair_sway, hcy - 18)
        c = P(bx, hcy + 1)
        line(cv, c[0], c[1], tip[0], tip[1], spec.armor[1])
        line(cv, c[0] + 1, c[1], tip[0] + 1, tip[1], spec.armor[2])
        dot(cv, tip[0], tip[1] + 1, spec.glow, True)
        dot(cv, tip[0] + 1, tip[1] + 1, spec.glow, True)


def dim(r_):
    return [r_[0], r_[0], r_[1], r_[2]]


def glow_pad(cv, spec, c, s, far, rx=1.3, ry=1.1):
    """A glowing pad (knee / elbow): the hi ramp, with a white-hot centre on the near side."""
    pad = Part().ellipse(c[0], c[1], rx * s, ry * s)
    paint(cv, pad, dim(spec.hi) if far else spec.hi, glow=not far, sep=False)
    if not far:
        dot(cv, c[0], c[1], spec.hi[3], True)


def arm(cv, spec, sh, hand, P, far, internals):
    elbow = ((sh[0] + hand[0]) / 2 + (0.6 if hand[1] < sh[1] - 4 else -0.4), (sh[1] + hand[1]) / 2)
    sleeve = spec.joint or spec.armor
    r = sleeve if not far else dim(sleeve)
    s = P(1, 0)[0] - P(0, 0)[0]
    thin = 0.68 if spec.joint else 1.0            # the dark undersuit is slimmer than plate
    up = Part().limb(P(*sh), P(*elbow), 1.6 * s * thin, 1.4 * s * thin)
    paint(cv, up, r)
    fore = Part().limb(P(*elbow), P(*hand), 1.6 * s * thin, 1.9 * s * thin)
    paint(cv, fore, r)
    if spec.kind == 'android':
        # trim: a cuff at the wrist and the bottom rim of the pauldron
        a, b = P(*elbow), P(*hand)
        cuff = Part().limb(((a[0] + b[0] * 3) / 4, (a[1] + b[1] * 3) / 4), ((a[0] + b[0] * 5) / 6, (a[1] + b[1] * 5) / 6), 1.9 * s)
        paint(cv, cuff, spec.trim)
    glove = 1.9 if spec.joint else 1.4
    hand_part = Part().ellipse(P(*hand)[0], P(*hand)[1], glove * s, glove * s)
    if spec.joint:
        # a dark glove with a glowing core, and a glowing elbow pad
        paint(cv, hand_part, spec.joint if not far else dim(spec.joint))
        dot(cv, P(*hand)[0], P(*hand)[1], (dim(spec.hi) if far else spec.hi)[2], not far)
        glow_pad(cv, spec, P(*elbow), s, far, 0.9, 0.9)
    else:
        paint(cv, hand_part, spec.skin if spec.kind == 'human' else spec.under)
    big = 1.35 if spec.joint else 1.0              # the crew's big pauldrons
    pad = Part().ellipse(P(*sh)[0], P(*sh)[1] + 0.3 * s, 2.5 * s * big, 2.0 * s * big)
    paint(cv, pad, spec.armor)
    if spec.kind == 'android':
        trim_edge(cv, pad, spec.trim[2])
    if spec.kind == 'android' and not far and spec.lines:
        a, b = P(*elbow), P(*hand)
        line(cv, (a[0] * 2 + b[0]) / 3, (a[1] * 2 + b[1]) / 3, (a[0] + b[0] * 2) / 3, (a[1] + b[1] * 2) / 3, spec.glow, True)
    if spec.extra == 'vines':
        a = P(*elbow)
        dot(cv, a[0], a[1], LEAF[2])
        dot(cv, a[0] + 1, a[1] - 1, LEAF[3])


def legs(cv, spec, pose, facing, P, hip_y, body_dx, internals):
    s = P(1, 0)[0] - P(0, 0)[0]
    side = facing in (LEFT, RIGHT)
    for which in ('far', 'near'):
        foot = pose.far_foot if which == 'far' else pose.near_foot
        if side:
            fx = body_dx + (1.0 if which == 'far' else -0.5) + (-1) * foot[0]
        else:
            fx = (2.3 if which == 'far' else -2.3) + foot[0] * 0.3
        hip = (body_dx + (0.8 if which == 'far' else -0.6) if side else (2.1 if which == 'far' else -2.1), hip_y)
        knee = ((hip[0] + fx) / 2, (hip_y + 0) / 2 + 1 - foot[1] * 0.5)
        ankle = (fx, 2.5 - foot[1])
        dark = which == 'far' and side
        legwear = spec.joint or (spec.armor if spec.kind == 'android' else spec.under)   # leggings or plate
        thin = 0.7 if spec.joint else 1.0         # slim leggings under the plate
        thigh = Part().limb(P(*hip), P(*knee), 1.9 * s * thin, 1.7 * s * thin)
        paint(cv, thigh, legwear if not dark else [legwear[0]] * 2 + legwear[1:3])
        shin = Part().limb(P(*knee), P(*ankle), 1.8 * s * thin, 2.1 * s * thin)
        shin_r = spec.joint or spec.armor
        paint(cv, shin, shin_r if not dark else [shin_r[0]] * 2 + shin_r[1:3])
        if spec.joint:
            glow_pad(cv, spec, P(*knee), s, dark, 1.3, 1.1)
        elif spec.kind == 'android':
            kp = Part().ellipse(*P(*knee), 1.6 * s, 1.3 * s)
            paint(cv, kp, spec.trim if not dark else [spec.trim[0]] * 2 + spec.trim[1:3])
        boot = Part()
        bx, by = P(*ankle)
        if side:
            boot.rect(bx - 2.5 * s - 1 * s, by - 0.5 * s, bx + 2.0 * s, by + 2.5 * s)
        else:
            boot.rect(bx - 2.2 * s, by - 0.5 * s, bx + 2.2 * s, by + 2.5 * s)
        paint(cv, boot, spec.armor if spec.kind == 'android' else spec.under)
        if spec.kind == 'android':
            trim_edge(cv, boot, spec.trim[2], top=True)
        if spec.kind == 'android' and not dark and spec.lines:
            kx, ky = P(*knee)
            dot(cv, kx, ky, spec.glow, True)
        if spec.extra == 'vines' and which == 'near':
            kx, ky = P(*knee)
            for d in range(3):
                dot(cv, kx - 1 + d, ky + 2 + (d % 2), LEAF[1 + d % 3])


def torso(cv, spec, pose, facing, P, top, hip_y, body_dx, internals):
    s = P(1, 0)[0] - P(0, 0)[0]
    side = facing in (LEFT, RIGHT)
    w_top, w_hip = (3.8, 3.2) if side else (5.4, 4.2)
    if spec.joint:   # the crew: a broad breastplate over a narrow waist, and wide hip plates
        w_top, w_hip = (4.4, 3.0) if side else (6.2, 3.8)
    t = Part().poly([P(body_dx - w_top, top), P(body_dx + w_top, top), P(body_dx + w_hip, hip_y + 1), P(body_dx - w_hip, hip_y + 1)])
    if spec.joint:
        t.merge(Part().ellipse(*P(body_dx + (-0.5 if side else 0), top - 4), (w_top - 0.2) * s, 4.2 * s))
    paint(cv, t, spec.armor if spec.kind != 'human' else spec.armor)
    # hips / skirt plate
    hw, hdown = (w_hip + 1, 2.5) if not spec.joint else (w_hip + 2.2, 3.5)
    hips = Part().poly([P(body_dx - hw, hip_y + 2.5), P(body_dx + hw, hip_y + 2.5), P(body_dx + hw + 0.6, hip_y - hdown), P(body_dx - hw - 0.6, hip_y - hdown)])
    paint(cv, hips, spec.armor if spec.kind == 'android' else spec.under)
    if spec.kind == 'android':
        trim_edge(cv, hips, spec.trim[2], top=True)   # the belt line
        trim_edge(cv, hips, spec.trim[1])             # and the plate's lower rim
    if spec.extra == 'apron' and facing != UP:
        apron = Part().rect(P(body_dx - 3.5, top - 4)[0], P(0, top - 4)[1], P(body_dx + 3.5, 0)[0], P(0, hip_y - 6)[1])
        paint(cv, apron, spec.trim)
    if spec.extra == 'bag':
        bag = Part().ellipse(*P(body_dx + (3 if not side else 3.5), hip_y + 2), 3 * s, 2.6 * s)
        paint(cv, bag, CLOTH_BROWN)
    if spec.kind == 'android':
        cx, cy = P(body_dx + (-1 if side else 0), top - 4)
        if internals:
            core = Part().ellipse(cx, cy, 2.6 * s, 2.6 * s)
            paint(cv, core, [spec.glow, spec.glow, spec.glow, WHITE], glow=True, sep=False)
        else:
            core = Part().ellipse(cx, cy, 1.3 * s, 1.3 * s)
            paint(cv, core, [spec.glow] * 3 + [WHITE], glow=True, sep=False)
        # glowing circuit lines: down the sternum and out to the hips
        x0, y0 = P(body_dx + (-1 if side else 0), top - 6)
        x1, y1 = P(body_dx + (-1 if side else 0), hip_y + 1)
        if not spec.lines:
            pass
        elif facing != UP:
            line(cv, x0, y0, x1, y1, spec.glow, True)
            if not side:
                line(cv, x1, y1, x1 - 2 * s, y1 + 1.5 * s, spec.glow, True)
                line(cv, x1, y1, x1 + 2 * s, y1 + 1.5 * s, spec.glow, True)
        elif spec.lines:
            line(cv, x0, y0 - 1 * s, x1, y1, spec.glow, True)
        if not spec.lines and facing != UP:
            # gold trim down the breastplate and a crimson gem, like the reference
            line(cv, x0, y0 + 1 * s, x1, y1 - 1 * s, spec.trim[3])
            gem = Part().ellipse(x0, y0 + 1.5 * s, 1.1 * s, 1.1 * s)
            paint(cv, gem, spec.accent, sep=False)
        if internals:
            internals_overlay(cv, spec, P, top, hip_y, body_dx, s)


def internals_overlay(cv, spec, P, top, hip_y, body_dx, s):
    """The opened-up view: plates off the chest, showing ribs, wires and gears around the glowing core."""
    wires = [hx('#C8483E'), hx('#3E78C8'), hx('#D8B040'), hx('#4AA86A')]
    x0, _ = P(body_dx - 4, 0)
    x1, _ = P(body_dx + 4, 0)
    _, ytop = P(0, top - 1)
    _, ybot = P(0, hip_y + 2)
    for y in range(int(ytop), int(ybot)):
        for x in range(int(x0), int(x1)):
            if cv.opaque(x, y) and not cv.glow[y][x]:
                cv.set(x, y, BLACK_ARMOR[0] if (x + y) % 5 else BLACK_ARMOR[1])
    for i, c in enumerate(wires):
        xa = x0 + (i + 1) * (x1 - x0) / 5
        line(cv, xa, ytop + 1, xa + (1 if i % 2 else -1) * s, ybot - 1, c)
    for ry in range(int(ytop) + 2, int(ybot) - 2, max(2, int(3 * s))):
        line(cv, x0 + 1, ry, x1 - 2, ry, BLACK_ARMOR[2])
    cx, cy = P(body_dx, top - 4)
    core = Part().ellipse(cx, cy, 3.2 * s, 3.2 * s)
    paint(cv, core, [hx('#8E5A2C'), hx('#C08A4A'), hx('#C08A4A'), hx('#E8B878')], sep=False)
    inner = Part().ellipse(cx, cy, 2.2 * s, 2.2 * s)
    paint(cv, inner, [spec.glow, spec.glow, spec.glow, WHITE], glow=True, sep=False)
    for gx, gy in ((x0 + 1, ytop + 2), (x1 - 2, ytop + 2), (x0 + 1, ybot - 3), (x1 - 2, ybot - 3)):
        g = Part().ellipse(gx, gy, 1.4 * s, 1.4 * s)
        paint(cv, g, [hx('#5A3418'), hx('#8E5A2C'), hx('#C08A4A'), hx('#E8B878')], sep=False)


HEAD = 0.8   # head size relative to the original rig: an Octopath-style body about three heads tall


def card_eye(cv, spec, exx, eyy, s, outer, narrow=False):
    """One eye drawn like the card art (reference/A3.png): a warm orange-brown lash line that darkens at the outer
    corner, over an iris that is dark crimson on top and glows below. outer = +1 when the outer corner is to the
    right; narrow draws the side-view eye (two columns)."""
    glow = spec.kind == 'android'
    top = spec.hi[0] if spec.hi else spec.accent[1]
    cols = range(0, int(1.2 * s) + 1) if narrow else range(int(-1 * s), int(1.2 * s) + 1)
    cols = list(cols)
    y0 = int(eyy - 1.5 * s)
    for dx in cols:
        outer_col = dx == (cols[-1] if outer > 0 else cols[0])
        dot(cv, exx + dx, y0, OUTLINE if outer_col else spec.skin[0])
        dot(cv, exx + dx, y0 + 1, OUTLINE if outer_col and not narrow else top, glow and not outer_col)
        for dy in range(2, int(1.2 * s) + 2):
            dot(cv, exx + dx, y0 + dy, spec.eye, glow)
    # a lash flick past the outer corner, and a pale glint in the iris
    dot(cv, exx + (cols[-1] + 1 if outer > 0 else cols[0] - 1), y0 - 1 if s < 2 else y0, spec.skin[0])
    if s >= 2:
        dot(cv, exx + cols[1], y0 + 2, WHITE, glow)


def head(cv, spec, pose, facing, P, head_c, internals):
    s = P(1, 0)[0] - P(0, 0)[0]
    Q = lambda dx, dy: P(head_c[0] + dx * HEAD, head_c[1] + dy * HEAD)
    side = facing in (LEFT, RIGHT)
    hcx, hcy = head_c
    cx, cy = Q(0, 0)
    rx, ry = 8.4 * s * HEAD, 8.0 * s * HEAD
    # face / skull
    face = Part().ellipse(cx, cy + 0.6 * s, rx - 0.6 * s, ry - 0.4 * s)
    paint(cv, face, spec.skin)
    if facing == DOWN:
        for ex in (-3.3, 3.3):
            exx, eyy = Q(0 + ex, 0 - 1.6)
            card_eye(cv, spec, exx, eyy, s, outer=1 if ex > 0 else -1)
        mx, my = Q(0, 0 - 5.2)
        dot(cv, mx, my, spec.skin[0])
        if s >= 2:
            dot(cv, mx + 1, my, spec.skin[0])
            # blush
            for bx in (-4.8, 4.8):
                bxx, byy = Q(0 + bx, 0 - 3.8)
                dot(cv, bxx, byy, PETAL[2])
    elif side:
        exx, eyy = Q(0 - 4.6, 0 - 1.6)
        card_eye(cv, spec, exx + 0.5 * s, eyy, s, outer=1, narrow=True)
        mx, my = Q(0 - 6.2, 0 - 5.0)
        dot(cv, mx, my, spec.skin[0])
    # hair
    if spec.hair and spec.hair_style != 'none':
        hair = Part()
        if facing == UP:
            hair.ellipse(cx, cy, rx + 0.6 * s, ry + 0.4 * s)
        elif side:
            hair.poly([Q(0 - 7.5, 0 + 2.5), Q(0 - 3, 0 + 8.6), Q(0 + 6, 0 + 8), Q(0 + 9, 0 + 1),
                       Q(0 + 8, 0 - 7), Q(0 + 2, 0 - 6), Q(0 + 1, 0 - 1), Q(0 - 3, 0 + 1.5), Q(0 - 6, 0 - 0.5)])
        else:
            hair.poly([Q(0 - 9, 0 + 1), Q(0 - 6, 0 + 7.6), Q(0, 0 + 9), Q(0 + 6, 0 + 7.6), Q(0 + 9, 0 + 1),
                       Q(0 + 9, 0 - 7), Q(0 + 6.6, 0 - 4), Q(0 + 5.5, 0 + 1.5), Q(0 + 3, 0 - 0.2), Q(0 + 1, 0 + 1.6),
                       Q(0 - 1.5, 0 - 0.2), Q(0 - 3.5, 0 + 1.6), Q(0 - 5.5, 0 + 0.4), Q(0 - 6.6, 0 - 4), Q(0 - 9, 0 - 7)])
        if spec.hair_style == 'short' and facing != UP:
            hair = Part().poly([Q(0 - 8.8, 0 - 1), Q(0 - 6, 0 + 7.6), Q(0, 0 + 9), Q(0 + 6, 0 + 7.6), Q(0 + 8.8, 0 - 1),
                                Q(0 + 6, 0 + 2), Q(0 + 2, 0 + 1), Q(0 - 2, 0 + 2.2), Q(0 - 6, 0 + 1.2)])
            if side:
                hair = Part().poly([Q(0 - 6, 0 + 3), Q(0 - 2, 0 + 8.6), Q(0 + 6, 0 + 8), Q(0 + 8.6, 0 + 1),
                                    Q(0 + 7, 0 - 5), Q(0 + 2, 0 - 2), Q(0 - 2, 0 + 2)])
        paint(cv, hair, spec.hair)
        # a shine band across the top
        if facing != UP:
            sx, sy = Q(0 - 4, 0 + 5.5)
            for d in range(int(5 * s)):
                dot(cv, sx + d, sy + (d % 3 == 0), spec.hair[3])
    headgear(cv, spec, pose, facing, P, head_c, s)


def headgear(cv, spec, pose, facing, P, head_c, s):
    hcx, hcy = head_c
    Q = lambda dx, dy: P(head_c[0] + dx * HEAD, head_c[1] + dy * HEAD)
    side = facing in (LEFT, RIGHT)
    g = spec.headgear
    if g == 'crown':
        # an angular headpiece with a glowing gem and swept horns, like the reference
        crown = Part().poly([Q(0 - 6, 0 + 6), Q(0 - 3, 0 + 9.5), Q(0, 0 + 7.6), Q(0 + 3, 0 + 9.5), Q(0 + 6, 0 + 6), Q(0, 0 + 5.2)])
        if side:
            crown = Part().poly([Q(0 - 5, 0 + 6.4), Q(0 - 1, 0 + 9.4), Q(0 + 4, 0 + 7.4), Q(0 + 1, 0 + 5.4)])
        paint(cv, crown, BLACK_ARMOR)
        for sgn in ((-1, 1) if not side else (1,)):
            horn = Part().poly([Q(0 + sgn * 6.5, 0 + 4), Q(0 + sgn * 11, 0 + 11), Q(0 + sgn * 8.5, 0 + 3)])
            if side:
                horn = Part().poly([Q(0 + 5, 0 + 4), Q(0 + 11, 0 + 10), Q(0 + 7.5, 0 + 2)])
            paint(cv, horn, BLACK_ARMOR)
            ex, ey = Q(0 + sgn * 8.2, 0 + 0.5) if not side else Q(0 + 6.5, 0 + 0.5)
            ear = Part().ellipse(ex, ey, 1.6 * s, 1.8 * s)
            paint(cv, ear, BLACK_ARMOR)
            dot(cv, ex, ey, spec.glow, True)
        if facing != UP:
            gx, gy = Q(0 + (-1 if side else 0), 0 + 7.3)
            dot(cv, gx, gy, spec.glow, True)
            dot(cv, gx, gy + 1, spec.glow, True)
    elif g == 'earpiece':
        # front: on the viewer's right; back: the same ear, now on the viewer's left; side: the visible ear
        if side:
            ex_, sgn = 5.6, 1
        else:
            sgn = 1 if facing == DOWN else -1
            ex_ = 8.3 * sgn
        ecx, ecy = Q(ex_, 0.4)
        pod = Part().ellipse(ecx, ecy, 1.9 * s, 2.3 * s)
        paint(cv, pod, spec.accent)
        trim_edge(cv, pod, spec.trim[3], top=True)
        hi = spec.hi or [spec.glow] * 3 + [WHITE]
        dot(cv, ecx, ecy, hi[2], True)
        if s >= 2:
            dot(cv, ecx, ecy - 1, hi[3], True)
        fin = Part().poly([Q(ex_ - sgn * 0.6, 2.2), Q(ex_ + sgn * 2.6, 8.4), Q(ex_ + sgn * 1.6, 2.0)])
        paint(cv, fin, spec.trim)
    elif g == 'ornament':
        # a crimson hair ornament on one side, like the reference (no crown)
        if facing != UP:
            ox_ = -7.6 if not side else 5.5
            orn = Part().ellipse(*Q(ox_, 3), 1.7 * s, 1.5 * s)
            paint(cv, orn, spec.accent)
            gx_, gy_ = Q(ox_, 3.4)
            dot(cv, gx_, gy_, spec.trim[3])
    elif g == 'leaves':
        for i, (lx, ly) in enumerate(((-6, 7), (-3, 9), (0, 9.6), (3, 9), (6, 7))):
            if side and lx < -2:
                continue
            leaf = Part().ellipse(*Q(0 + lx, 0 + ly), 1.8 * s, 1.2 * s)
            paint(cv, leaf, LEAF)
        fx, fy = Q(0 + (4 if not side else 2), 0 + 7.5)
        fl = Part().ellipse(fx, fy, 1.6 * s, 1.6 * s)
        paint(cv, fl, PETAL)
        dot(cv, fx, fy, hx('#F6D45A'), True)
    elif g == 'phones':
        for sgn in ((-1, 1) if not side else (1,)):
            ex, ey = Q(0 + sgn * 8.6, 0) if not side else Q(0 + 6, 0)
            ph = Part().ellipse(ex, ey, 2.0 * s, 2.6 * s)
            paint(cv, ph, BLACK_ARMOR)
            dot(cv, ex, ey, spec.glow, True)
            dot(cv, ex, ey - 1, spec.glow, True)
        band = Part()
        if not side:
            band.poly([Q(0 - 8, 0 + 2), Q(0 - 4, 0 + 9), Q(0 + 4, 0 + 9), Q(0 + 8, 0 + 2), Q(0 + 7, 0 + 2), Q(0 + 3.5, 0 + 8), Q(0 - 3.5, 0 + 8), Q(0 - 7, 0 + 2)])
            paint(cv, band, BLACK_ARMOR)
    elif g == 'band' and facing != UP:
        b = Part().poly([Q(0 - 8.6, 0 + 2.6), Q(0 + 8.6, 0 + 2.6), Q(0 + 8.4, 0 + 1), Q(0 - 8.4, 0 + 1)])
        paint(cv, b, hx('#E8E2D2') and ramp('#8E887A', '#BEB6A6', '#E8E2D2', '#FFFFFF'))
    elif g == 'cap':
        cap = Part().ellipse(*Q(0, 0 + 5), 8.6 * s, 4.2 * s)
        paint(cv, cap, CLOTH_BLUE)
        if facing != UP:
            brim = Part().rect(P(hcx - (9 if not side else 11), 0)[0], P(0, hcy + 3.2)[1], P(hcx + (9 if not side else 2), 0)[0], P(0, hcy + 1.8)[1])
            paint(cv, brim, CLOTH_BLUE)
    elif g == 'hood':
        hood = Part()
        if facing == UP:
            hood.ellipse(*Q(0, 0), 9.6 * s, 9.2 * s)
        elif side:
            hood.poly([Q(0 - 6, 0 + 8), Q(0 + 9, 0 + 6), Q(0 + 10, 0 - 8), Q(0 + 3, 0 - 6), Q(0 + 2, 0 + 3), Q(0 - 6, 0 + 5)])
        else:
            hood.poly([Q(0 - 9.8, 0 - 6), Q(0 - 9, 0 + 6), Q(0, 0 + 10), Q(0 + 9, 0 + 6), Q(0 + 9.8, 0 - 6),
                       Q(0 + 7, 0 - 4), Q(0 + 7.6, 0 + 4), Q(0, 0 + 7.6), Q(0 - 7.6, 0 + 4), Q(0 - 7, 0 - 4)])
        paint(cv, hood, CLOTH_GREEN)


def weapon(cv, spec, pose, facing, P, hand, S, hero=False):
    w = spec.weapon
    hxp, hyp = P(*hand)
    side = facing in (LEFT, RIGHT)
    rest = 100 if spec.kind == 'machine' else -115   # androids rest their weapon up over the shoulder
    ang = math.radians(pose.weapon_angle if pose.weapon_angle is not None else rest)
    fwd = -1 if side or hero else 1
    if facing == UP:
        fwd = 1
    dx, dy = math.cos(ang) * fwd, math.sin(ang)
    if hero:
        dx, dy = -0.55, -0.83   # raised up and back over the shoulder, like the reference
    L = {'katana': 17, 'greatsword': 18, 'staff': 20, 'dagger': 9, 'blade': 12, 'piston': 7}.get(w, 10) * S
    tip = (hxp + dx * L, hyp + dy * L)
    if w == 'katana':
        blade = Part().limb((hxp + dx * 2 * S, hyp + dy * 2 * S), tip, 1.4 * S, 0.7 * S)
        paint(cv, blade, spec.blade)
        line(cv, hxp + dx * 3 * S, hyp + dy * 3 * S, tip[0] - dx * 1.5 * S, tip[1] - dy * 1.5 * S, spec.glow, True)
        guard = Part().ellipse(hxp + dx * 1.6 * S, hyp + dy * 1.6 * S, 1.7 * S, 1.7 * S)
        paint(cv, guard, GOLD)
        grip = Part().limb((hxp, hyp), (hxp - dx * 3 * S, hyp - dy * 3 * S), 0.9 * S)
        paint(cv, grip, CRIMSON)
    elif w == 'greatsword':
        # a broad golden blade: light edge, dark spine, a crimson-and-gold guard
        blade = Part().limb((hxp + dx * 2.5 * S, hyp + dy * 2.5 * S), tip, 2.6 * S, 1.3 * S)
        paint(cv, blade, spec.blade)
        line(cv, hxp + dx * 3.5 * S - dy * 1.2 * S, hyp + dy * 3.5 * S + dx * 1.2 * S,
             tip[0] - dx * 2 * S, tip[1] - dy * 2 * S, spec.blade[3])
        guard = Part().limb((hxp + dx * 2 * S - dy * 3 * S, hyp + dy * 2 * S + dx * 3 * S),
                            (hxp + dx * 2 * S + dy * 3 * S, hyp + dy * 2 * S - dx * 3 * S), 1.1 * S)
        paint(cv, guard, spec.accent)
        grip = Part().limb((hxp, hyp), (hxp - dx * 3 * S, hyp - dy * 3 * S), 1.0 * S)
        paint(cv, grip, spec.accent)
    elif w == 'staff':
        shaft = Part().limb((hxp - dx * 6 * S, hyp - dy * 6 * S), tip, 0.9 * S)
        paint(cv, shaft, spec.blade)
        for i, (ox, oy) in enumerate(((-1.5, -1), (1.5, -1), (0, -2.4))):
            leaf = Part().ellipse(tip[0] + ox * S, tip[1] + oy * S, 1.6 * S, 1.1 * S)
            paint(cv, leaf, LEAF)
        dot(cv, tip[0], tip[1] - 1 * S, spec.glow, True)
    elif w == 'dagger':
        blade = Part().limb((hxp + dx * 1.5 * S, hyp + dy * 1.5 * S), tip, 1.2 * S, 0.5 * S)
        paint(cv, blade, spec.blade, glow=True)
        grip = Part().limb((hxp, hyp), (hxp - dx * 2 * S, hyp - dy * 2 * S), 0.9 * S)
        paint(cv, grip, BLACK_ARMOR)
    elif w == 'blade':
        blade = Part().poly([(hxp, hyp - 1 * S), (tip[0], tip[1]), (hxp + dx * 4 * S, hyp + dy * 4 * S + 2 * S)])
        paint(cv, blade, spec.blade)
    elif w == 'piston':
        fist = Part().ellipse(tip[0], tip[1], 2.6 * S, 2.6 * S)
        rod = Part().limb((hxp, hyp), tip, 1.0 * S)
        paint(cv, rod, STEEL)
        paint(cv, fist, spec.trim)


def draw_machine(cv, spec, pose, facing, P, S):
    """NieR-style machine lifeform: a round head with glowing eyes on a barrel body, pipe arms, stubby legs."""
    body_r, eye = spec.machine
    side = facing in (LEFT, RIGHT)
    bob = pose.bob
    crouch = pose.crouch
    lean = pose.lean * (-1 if side else 0)
    s = S
    # legs
    for which, off in (('far', 2.4), ('near', -2.4)):
        foot = pose.far_foot if which == 'far' else pose.near_foot
        fx = (off * (0.4 if side else 1)) + (-foot[0] if side else foot[0] * 0.3)
        leg = Part().limb(P(off * (0.4 if side else 1), 9 - crouch), P(fx, 2 - foot[1]), 1.8 * s, 2.0 * s)
        paint(cv, leg, STEEL if which == 'near' else [STEEL[0], STEEL[0], STEEL[1], STEEL[2]])
        foot_p = Part().rect(P(fx - 2.6, 0)[0], P(0, 3 - foot[1])[1], P(fx + 2.6, 0)[0], P(0, 0 - foot[1])[1])
        paint(cv, foot_p, body_r)
    # body barrel
    by = 15 - bob - crouch
    body = Part().ellipse(*P(lean, by), 7.6 * s, 6.6 * s)
    paint(cv, body, body_r)
    for i in range(-2, 3):
        x, y = P(lean + i * 2.4, by + 1)
        dot(cv, x, y, body_r[0])
    # arms
    for sgn in ((-1, 1) if not side else (1, -1)):
        sh = P(lean + sgn * 7, by + 2)
        nh = pose.near_hand if sgn < 0 else pose.far_hand
        nh = nh or (0, 0)
        hand = P(lean + sgn * 8.5 + (nh[0] * (-1 if side else 1)), by - 6 - nh[1])
        armp = Part().limb(sh, hand, 1.2 * s)
        paint(cv, armp, STEEL)
        if sgn < 0 and spec.weapon:
            weapon(cv, spec, pose, facing, P, (lean + sgn * 8.5 + (nh[0] * (-1 if side else 1)), by - 6 - nh[1]), S)
        else:
            claw = Part().ellipse(hand[0], hand[1], 1.6 * s, 1.6 * s)
            paint(cv, claw, body_r)
    # head
    hy = 27 - bob - crouch
    headp = Part().ellipse(*P(lean, hy), 8.2 * s, 7.4 * s)
    paint(cv, headp, body_r)
    antenna = Part().limb(P(lean + (1 if side else 0), hy + 7), P(lean + (2 if side else 0), hy + 11), 0.6 * s)
    paint(cv, antenna, STEEL)
    tipx, tipy = P(lean + (2 if side else 0), hy + 11.5)
    dot(cv, tipx, tipy, eye, True)
    if facing == DOWN:
        for ex in (-3.2, 3.2):
            e = Part().ellipse(*P(lean + ex, hy - 0.4), 1.8 * s, 1.8 * s)
            paint(cv, e, [OUTLINE, OUTLINE, OUTLINE, OUTLINE], sep=False)
            ec = Part().ellipse(*P(lean + ex, hy - 0.4), 1.0 * s, 1.0 * s)
            paint(cv, ec, [eye] * 3 + [WHITE], glow=True, sep=False)
    elif side:
        e = Part().ellipse(*P(lean - 4.6, hy - 0.4), 1.8 * s, 1.8 * s)
        paint(cv, e, [OUTLINE] * 4, sep=False)
        ec = Part().ellipse(*P(lean - 4.6, hy - 0.4), 1.0 * s, 1.0 * s)
        paint(cv, ec, [eye] * 3 + [WHITE], glow=True, sep=False)
    # a rusty seam across the head
    x0, y0 = P(lean - 7, hy - 3)
    x1, _ = P(lean + 7, hy - 3)
    for x in range(int(x0), int(x1)):
        if cv.opaque(x, int(y0)):
            dot(cv, x, y0, body_r[1])


# ---------------------------------------------------------------------------------------------------- sheets

# Card portraits (see below). The crew menu pins the item sockets to the head (CHIP)
# and the chest core (CORE): CharacterMenu.SocketSpots must match portrait_anchor() below.
# Portraits are drawn at the reference sprite's pixel density: a 48 x 72 canvas at 1.25x rig detail, then doubled
# into the 96 x 144 card, so every card has the same chunky pixels as A3's reference art.
PORTRAIT_K, PORTRAIT_X, PORTRAIT_GROUND = 1.25, 25, 69


def portrait_anchor(x, y):
    """A rig point (body units, y up from the ground) in card-portrait pixels (after doubling)."""
    return 2 * (PORTRAIT_X + x * PORTRAIT_K), 2 * (PORTRAIT_GROUND - y * PORTRAIT_K)


def doubled(small):
    big = Canvas(small.w * 2, small.h * 2)
    for y in range(big.h):
        for x in range(big.w):
            big.px[y][x] = small.px[y // 2][x // 2]
            big.glow[y][x] = small.glow[y // 2][x // 2]
    return big


ANIMS = [('idle', IDLE_N, idle_pose), ('walk', WALK_N + 1, walk_pose), ('slash', SLASH_N, slash_pose),
         ('spellcast', CAST_N, cast_pose), ('hurt', HURT_N, hurt_pose)]


def frame(spec, pose, facing):
    if facing == RIGHT:
        cv = draw(spec, pose, LEFT, k=RIG_K)
        outline(cv)
        return mirror(cv)
    cv = draw(spec, pose, facing, k=RIG_K)
    outline(cv)
    return cv


def hurt_frame(spec, i):
    """The defeat row (front view): recoil, kneel, then fall flat: one frame tipping over (a little above the
    ground), one bouncing a pixel, then lying still."""
    if i < HURT_STAND:
        return frame(spec, hurt_pose(i), DOWN)
    cv = draw(spec, Pose(), LEFT, k=RIG_K)
    outline(cv)
    rot = rotate_cw(cv)
    # lie on the ground: move the rotated body down so its lowest pixel touches the feet line
    lowest = max((y for y in range(F) for x in range(F) if rot.px[y][x][3]), default=F - 1)
    out = Canvas(F, F)
    shift = (F - 6) - lowest - {HURT_STAND: 4, HURT_STAND + 1: 1}.get(i, 0)
    for y in range(F):
        for x in range(F):
            ny = y + shift
            if 0 <= ny < F and rot.px[y][x][3]:
                out.px[ny][x] = rot.px[y][x]
    return out


def build_sheets(name):
    if name == 'A3':
        # A3: the A2 study body wearing the old hair (a3_model.py, reference/A3_hair.png), not drawn here. Needs Pillow.
        try:
            import a3_model
        except ImportError as e:
            print('A3 skipped (%s): run  py -3.9 Tools/ChibiForge/a3_model.py' % e)
            return None
        a3_model.main([])
        return None
    import npc_packs
    if name in npc_packs.CAST:
        npc_packs.main()   # the machines and NPCs: Snoblin's and Tech Dungeon's packs (Settings > Machine Look)
        return None
    if name in PARTY:
        import sample_model
        if sample_model.available(name):
            sample_model.build(name)   # A3: the user's own idle / walk animation (reference/sample/)
            return None
        import card_model
        if card_model.available(name):
            card_model.build(name)   # the crew walk as their card art (reference/A3.png), animated by layers
            return None
    spec = SPECS[name]
    out_dir = os.path.join(CHARS, name)
    os.makedirs(out_dir, exist_ok=True)
    first_frames = {}
    for anim, count, pose_fn in ANIMS:
        rows = 1 if anim == 'hurt' else 4
        sheet = [[CLEAR] * (F * count) for _ in range(F * rows)]
        for r in range(rows):
            for i in range(count):
                cv = hurt_frame(spec, i) if anim == 'hurt' else frame(spec, pose_fn(i), r)
                for y in range(F):
                    sheet[r * F + y][i * F:(i + 1) * F] = cv.px[y]
                if anim == 'idle' and r == DOWN and i == 0:
                    first_frames[name] = cv
        write_png(os.path.join(out_dir, anim + '.png'), F * count, F * rows, sheet)
    with open(os.path.join(out_dir, 'CREDITS.md'), 'w', encoding='utf-8') as f:
        f.write('# %s: sprite credits\n\nOriginal pixel art drawn in code by `Tools/ChibiForge/chibi_forge.py` '
                '(no third-party assets, no license strings). Re-run it to rebuild these sheets.\n' % name)
    print('built', name)
    return first_frames.get(name)


REFERENCE = os.path.join(HERE, 'reference')


def recolour_map(spec):
    """How A3's reference colours become another crew member's: hair, mane, armour, trim (the trim also colours
    the eyes and the sword) and accent. Outline, inner shadows and skin stay."""
    m = {}
    for i, c in enumerate(REF_SLATE):
        m[c[:3]] = spec.hair[i][:3]
    for i, c in enumerate(REF_WHITE[:3]):
        m[c[:3]] = spec.armor[i][:3]
    for i, c in enumerate(REF_GOLD):
        m[c[:3]] = spec.trim[i][:3]
    m[hx('#472D38')[:3]] = (spec.back_hair or spec.hair)[2][:3]
    m[REF_CRIMSON[1][:3]] = spec.accent[1][:3]
    return m


def reference_portrait(name):
    """Card art from the user's reference sprite (2x into the 96 x 144 card, feet at the bottom): the character's
    own reference if there is one, otherwise A3's recoloured to the character's palette. None when missing."""
    ref = reference_pixels(name)
    if ref is None:
        return None
    w, h, px = ref
    cv = Canvas(96, 144)
    ox, oy = (96 - w * 2) // 2, 144 - 4 - h * 2
    for y in range(h):
        for x in range(w):
            p = px[y][x]
            if p[3]:
                for dy in range(2):
                    for dx in range(2):
                        cv.set(ox + x * 2 + dx, oy + y * 2 + dy, p)
    return cv, (ox, oy)


def reference_pixels(name):
    """The character's reference sprite at native size as (w, h, RGBA rows): their own file, or A3's recoloured to
    their palette. None when missing. Used by the card art and by card_model.py (the overworld sprites)."""
    path = os.path.join(REFERENCE, name + '.png')
    mapping = None
    if not os.path.exists(path):
        path = os.path.join(REFERENCE, 'A3.png')
        if not os.path.exists(path) or name not in PARTY:
            return None
        mapping = recolour_map(SPECS[name])
    from pngtool import read_png
    w, h, px = read_png(path)
    if mapping:
        # The reference reuses colours across areas, so the recolour is regional:
        #   slate is hair above the shoulders (native y < 30) but gauntlets / greaves below (kept);
        #   inside the face box the off-whites are skin highlights and the light orange is skin shading (kept);
        #   everything else follows the palette map (armour whites, trim / eyes / sword, accent, mane).
        slate = {c[:3] for c in REF_SLATE}
        keep_in_face = {c[:3] for c in REF_WHITE[:3]} | {hx('#D2884A')[:3]}
        face = (18, 17, 40, 34)
        hair_line = 30
        out = []
        for y in range(h):
            row = []
            for x in range(w):
                c = px[y][x]
                rgb = c[:3]
                if c[3] and rgb in mapping:
                    in_face = face[0] <= x < face[2] and face[1] <= y < face[3]
                    if rgb in slate and y >= hair_line:
                        pass
                    elif in_face and rgb in keep_in_face:
                        pass
                    else:
                        c = mapping[rgb] + (c[3],)
                row.append(c)
            out.append(row)
        px = out
    return w, h, px


def open_chest(cv, spec, box, armour=None):
    """Internals on a finished portrait: inside the chest box, armour pixels (the reference's whites and slates, or
    the given set of RGB colours) become a dark frame with ribs and wires around a glowing core in a copper housing."""
    x0, y0, x1, y1 = box
    if armour is None:
        armour = {c[:3] for c in REF_WHITE + REF_SLATE + spec.armor + spec.hair}
    wires = [hx('#C8483E'), hx('#3E78C8'), hx('#D8B040'), hx('#4AA86A')]
    for y in range(y0, y1):
        for x in range(x0, x1):
            if cv.px[y][x][3] and cv.px[y][x][:3] in armour and cv.px[y][x][:3] not in {c[:3] for c in REF_SKIN}:
                cv.set(x, y, REF_DARK[1] if (y - y0) % 4 else REF_DARK[2])
    for i, c in enumerate(wires):
        xa = x0 + 2 + i * (x1 - x0 - 4) // 4
        for y in range(y0 + 1, y1 - 1):
            if cv.px[y][xa][:3] in (REF_DARK[1][:3], REF_DARK[2][:3]):
                cv.set(xa, y, c)
    cx, cy = (x0 + x1) // 2, (y0 + y1) // 2
    paint(cv, Part().ellipse(cx, cy, 5, 5), [hx('#8E5A2C'), hx('#C08A4A'), hx('#C08A4A'), hx('#E8B878')], sep=False)
    paint(cv, Part().ellipse(cx, cy, 3.2, 3.2), [spec.glow, spec.glow, spec.glow, WHITE], glow=True, sep=False)


def build_portraits(name):
    """Card art: the reskinned knight (A3), A3's reference sprite (recoloured for the others), or the rig in a hero
    pose; front and opened up (internals), plus glow masks."""
    spec = SPECS[name]
    os.makedirs(PORTRAITS, exist_ok=True)
    results = {}
    knight = False   # the Fantasy Knight card art is gone (knight_model.py retired: its licence rules out AI creations)
    ref = None if knight else reference_portrait(name)
    for side_name, internals in (('front', False), ('internals', True)):
        if knight:
            # A3's card: the reskinned knight (the same art as the sprites)
            cv, box = knight_model.portrait(name)
            if internals:
                plate = set(knight_model.SKINS[name]['map'][c] for c in knight_model.STEEL)
                open_chest(cv, spec, box, armour=plate | set(knight_model.SKINS[name]['map'][c] for c in knight_model.GOLD[1:]))
        elif ref is not None:
            cv, (ox, oy) = reference_portrait(name)
            if internals:
                # the reference's breastplate: native pixels about (17..35, 33..47)
                open_chest(cv, spec, (ox + 34, oy + 66, ox + 70, oy + 94))
        else:
            small = draw(spec, Pose(hair_sway=1), DOWN, k=PORTRAIT_K, hero=True, internals=internals, w=48, h=72, ox=PORTRAIT_X, ground=PORTRAIT_GROUND)
            outline(small)
            cv = doubled(small)
        write_png(os.path.join(PORTRAITS, '%s_%s.png' % (name, side_name)), 96, 144, cv.px)
        glow = [[(255, 255, 255, 255 if cv.glow[y][x] else 0) for x in range(96)] for y in range(144)]
        # a soft one-pixel halo around the glow
        halo = [row[:] for row in glow]
        for y in range(144):
            for x in range(96):
                if glow[y][x][3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    if 0 <= x + dx < 96 and 0 <= y + dy < 144 and glow[y + dy][x + dx][3]:
                        halo[y][x] = (255, 255, 255, 90)
                        break
        write_png(os.path.join(PORTRAITS, '%s_%s_glow.png' % (name, side_name)), 96, 144, halo)
        results[side_name] = cv
    print('portraits', name)
    return results


def contact_sheet(path, names, scale=4):
    """Every character: walk frame 0 in all 4 directions, a slash and a cast frame, the fallen frame; then the
    party's card portraits."""
    rows = []
    for name in names:
        spec = SPECS[name]
        cells = [frame(spec, Pose(), d) for d in (DOWN, LEFT, RIGHT, UP)]
        cells += [frame(spec, walk_pose(2), LEFT), frame(spec, slash_pose(2), RIGHT), frame(spec, cast_pose(3), RIGHT), hurt_frame(spec, 5)]
        rows.append(cells)
    W = len(rows[0]) * F
    H = len(rows) * F
    bg = (52, 50, 60, 255)
    img = [[bg] * W for _ in range(H)]
    for r, cells in enumerate(rows):
        for c, cv in enumerate(cells):
            for y in range(F):
                for x in range(F):
                    p = cv.px[y][x]
                    if p[3]:
                        img[r * F + y][c * F + x] = p
    big = [[img[y // scale][x // scale] for x in range(W * scale)] for y in range(H * scale)]
    write_png(path, W * scale, H * scale, big)


def portrait_sheet(path, names, scale=3):
    W, H = 96 * 2 * len(names), 144
    bg = (40, 38, 46, 255)
    img = [[bg] * W for _ in range(H)]
    for n, name in enumerate(names):
        res = build_portraits(name)
        for j, key in enumerate(('front', 'internals')):
            cv = res[key]
            for y in range(144):
                for x in range(96):
                    if cv.px[y][x][3]:
                        img[y][(n * 2 + j) * 96 + x] = cv.px[y][x]
    big = [[img[y // scale][x // scale] for x in range(W * scale)] for y in range(H * scale)]
    write_png(path, W * scale, H * scale, big)


def main():
    args = sys.argv[1:]
    sheet = None
    if '--sheet' in args:
        i = args.index('--sheet')
        sheet = args[i + 1]
        del args[i:i + 2]
    names = args or [n for n in SPECS if n not in RETIRED]
    for n in names:
        build_sheets(n)
        if n in PARTY:
            build_portraits(n)
    if sheet:
        contact_sheet(sheet, names)
        party = [n for n in names if n in PARTY]
        if party:
            portrait_sheet(sheet.replace('.png', '_portraits.png'), party)


if __name__ == '__main__':
    main()
