"""
Knight model: sprites from "Fantasy Knight - Free Pixelart Animated Character" by aamatniekss
(https://aamatniekss.itch.io/fantasy-knight-free-pixelart-animated-character), reskinned per character: MC as a white
robot with an orange glow, and the machine enemies (Rust Sentry: rusted plates, red glow; Scrap Stalker: mossy plates,
amber glow). Enemies face left in game, which the game does by mirroring (their left row). The pack's Colour2 / Outline
sheets (120 x 80 frames in a row, facing right) are in reference/FreeKnight/. Also MC's character-card portrait
(portrait(): the idle pose at 3x in the 96 x 144 card).

    python Tools/ChibiForge/knight_model.py [--sheet out.png] [--only clip,clip]

Reskin (a fixed colour map per character, SKINS): the steel armour becomes plating (white for MC); the gold /
leather (gloves, belt, boots) becomes dark mech joints with glowing highlights; the red scarf becomes a glowing collar;
and the visor slit (outline pixels inside the head, found per frame) glows.
Frames keep the artist's pixels. The 120 x 80 frame is cropped to 64 x 64 around the body (same window for every
clip, so nothing jitters): feet on the ground row, body centred. Wide sword swings are clipped at the frame edge.
    idle 10       _Idle
    walk 11       frame 0 stands (idle frame 0), then _Run (10)
    slash 10      _AttackComboNoMovement
    spellcast 12  idle frame 0 with an orange orb growing in front of the hands (the pack has no cast)
    hurt 11       _Hit, then _DeathNoMovement (ends lying down)
    sit 5         kneeling on one knee (the Esc menu, beside the mat): the pack has no kneel, so kneel() lowers the
                  idle pose's upper body and draws new legs in the character's plating (front knee up, back knee down)
Directions: right as drawn, left mirrored; there are no front / back views, so down uses right and up uses left.
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
SRC = os.path.join(HERE, 'reference', 'FreeKnight', 'Colour2', 'Outline', '120x80_PNGSheets')
FW, FH = 120, 80
OUTLINE = (0x1A, 0x0E, 0x13)


def hx(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


# The source palette (Colour2): steel light / mid / dark, gold light / mid / dark (leather), scarf light / dark.
STEEL = [hx('#CAD5D7'), hx('#88919D'), hx('#4B495E')]
GOLD = [hx('#C39942'), hx('#886B2E'), hx('#503C1F')]
SCARF = [hx('#934325'), hx('#77291C')]


def skin(plate, joint_hi, joint, collar, visor, hi):
    """A reskin: plate (light, mid, dark), joint highlight, joints (mid, dark), collar (light, dark), the visor colour
    and the glow ramp (dark..white-hot) for effects."""
    m = {STEEL[i]: hx(plate[i]) for i in range(3)}
    m.update({GOLD[0]: hx(joint_hi), GOLD[1]: hx(joint[0]), GOLD[2]: hx(joint[1]),
              SCARF[0]: hx(collar[0]), SCARF[1]: hx(collar[1])})
    return {'map': m, 'visor': hx(visor), 'hi': [hx(h) for h in hi]}


SKINS = {
    # white robot plating, dark joints with orange highlights, an orange collar and visor
    'MC': skin(('#F4F0EE', '#C4C0CA', '#5E5A70'), '#FF9A3A', ('#302838', '#1E1826'), ('#E8641E', '#A8401A'),
               '#FF8A2A', ('#A8401A', '#E8641E', '#FF9A3A', '#FFE2B0')),
    # Heal Bot: pale sage plating, dark joints with green lights, a green collar and visor
    'HealBot': skin(('#E6F2DE', '#A6C29E', '#4E6A50'), '#8EF070', ('#22302A', '#141C18'), ('#5EB03A', '#2E6A1E'),
                    '#8EF070', ('#2E6A1E', '#5EB03A', '#8EF070', '#E4FFD0')),
    # Lucy: violet-grey plating, dark joints with purple lights, a purple collar and visor
    'Lucy': skin(('#C8C0DC', '#867CA6', '#3E3452'), '#D07AFF', ('#24182E', '#140C1C'), ('#9A48D8', '#5A1E8A'),
                 '#D07AFF', ('#5A1E8A', '#9A48D8', '#D07AFF', '#F4DCFF')),
    # Blue (the backup, Sandbox): blue plating, dark joints with cyan lights, a deep blue collar, cyan visor
    'Blue': skin(('#A8C8F0', '#5E86C4', '#2E4478'), '#7FE6FF', ('#1C2438', '#101624'), ('#3A6AD8', '#1E3A8A'),
                 '#7FE6FF', ('#1E3A8A', '#3A6AD8', '#7FE6FF', '#E0FBFF')),
    # Ruin survivors (NPCs): civilian colours, no glow (the visor stays dark).
    'NoodleVendor': skin(('#C45A4E', '#9A3436', '#6A2028'), '#B48A5A', ('#563A2A', '#2E1E18'), ('#E8E2D2', '#BEB6A6'),
                         '#1A0E13', ('#563A2A', '#86603E', '#B48A5A', '#E8E2D2')),
    'Courier': skin(('#7E9CC0', '#4E6A92', '#2E3E62'), '#E09A36', ('#3E3C46', '#24222A'), ('#E09A36', '#B8601E'),
                    '#1A0E13', ('#B8601E', '#E09A36', '#F6CE5A', '#FFF0C0')),
    'Drifter': skin(('#76986A', '#4A6E4A', '#2E4A34'), '#B48A5A', ('#563A2A', '#2E1E18'), ('#86603E', '#563A2A'),
                    '#1A0E13', ('#563A2A', '#86603E', '#B48A5A', '#E8E2D2')),
    # rusted iron plates, dark steel joints with red warning lights, a red visor
    'RustSentry': skin(('#D88A40', '#A85A26', '#6E3418'), '#FF5A3A', ('#3C4656', '#232A36'), ('#E8402A', '#9A2418'),
                       '#FF3A2A', ('#9A2418', '#E8402A', '#FF6A4A', '#FFD0C0')),
    # moss-grown plates, rust joints with amber lights, an amber visor
    'ScrapStalker': skin(('#93A25A', '#62723E', '#3E4A2A'), '#FFB03A', ('#6E3418', '#3A1A10'), ('#E88A2A', '#9A5418'),
                         '#FFA02A', ('#9A5418', '#E88A2A', '#FFB84A', '#FFF0C0')),
}
NAMES = list(SKINS)


def available(name):
    return name in NAMES and os.path.exists(os.path.join(SRC, '_Idle.png'))


def strip(anim):
    """The frames of one sheet as grids of RGB / None (120 x 80 each)."""
    w, h, px = read_png(os.path.join(SRC, anim + '.png'))
    return [[[px[y][n * FW + x][:3] if px[y][n * FW + x][3] else None for x in range(FW)] for y in range(FH)]
            for n in range(w // FW)]


def swoosh(fr):
    """The sword's slash trail: steel-coloured pixels that reach the transparent background without an outline
    between (the body's plating is always enclosed by the outline). Flood-filled through steel colours; only big
    patches count (stray unoutlined pixels on a running foot stay plating)."""
    steel = set(STEEL)
    seeds = [(x, y) for y in range(FH) for x in range(FW) if fr[y][x] in steel and any(
        0 <= x + dx < FW and 0 <= y + dy < FH and fr[y + dy][x + dx] is None for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    seen, trail = set(), set()
    for seed in seeds:
        if seed in seen:
            continue
        patch, stack = set(), [seed]
        while stack:
            x, y = stack.pop()
            if (x, y) in seen:
                continue
            seen.add((x, y))
            patch.add((x, y))
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < FW and 0 <= ny < FH and (nx, ny) not in seen and fr[ny][nx] in steel:
                    stack.append((nx, ny))
        if len(patch) >= 80:
            trail |= patch
    return trail


def reskin(fr, name, attack=False):
    sk = SKINS[name]
    trail = swoosh(fr) if attack else set()    # an attack's slash trail keeps its own pale colours
    out = [[(c if (x, y) in trail else sk['map'].get(c, c)) if c is not None else None for x, c in enumerate(row)]
           for y, row in enumerate(fr)]
    # the visor: outline pixels inside the head (opaque above, below and to the left), in its top 7 rows
    ys = [y for y in range(FH) for x in range(FW) if fr[y][x] is not None and (x, y) not in trail]
    if not ys:
        return out
    top = min(ys)
    for y in range(top + 1, min(FH - 1, top + 7)):
        for x in range(1, FW - 1):
            if (fr[y][x] == OUTLINE and fr[y - 1][x] is not None and fr[y + 1][x] is not None
                    and fr[y][x - 1] is not None and fr[y][x - 1] != OUTLINE):
                out[y][x] = sk['visor']
    return out


def window(idle0):
    """The crop offset (source x, y of the 64 px frame's corner) from the standing pose: body centred, feet on the
    ground row."""
    xs = [x for y in range(FH) for x in range(FW) if idle0[y][x] is not None]
    ys = [y for y in range(FH) for x in range(FW) if idle0[y][x] is not None]
    cx = (min(xs) + max(xs)) // 2
    return cx - F // 2, max(ys) - GROUND


def crop(fr, ox, oy, fx=None):
    out = [[None] * F for _ in range(F)]
    for y in range(F):
        sy = oy + y
        if 0 <= sy < FH:
            for x in range(F):
                sx = ox + x
                if 0 <= sx < FW:
                    out[y][x] = fr[sy][sx]
    if fx:
        fx(out)
    return out


def mirror(fr):
    return [row[::-1] for row in fr]


def orb(fr, cx, cy, r, HI):
    for y in range(cy - r - 1, cy + r + 2):
        for x in range(cx - r - 1, cx + r + 2):
            if not (0 <= x < F and 0 <= y < F):
                continue
            d = math.hypot(x - cx, y - cy)
            if d <= r:
                fr[y][x] = HI[3] if d < r * 0.45 else HI[2] if d < r * 0.8 else HI[1]
            elif d <= r + 1:
                fr[y][x] = OUTLINE


def frames_for(name):
    idle = [reskin(f, name) for f in strip('_Idle')]
    hi = SKINS[name]['hi']
    ox, oy = window(idle[0])

    def clip(frames, fx=None):
        return [crop(f, ox, oy, fx) for f in frames]

    def rows(frames):
        """[up, left, down, right] from right-facing frames."""
        left = [mirror(f) for f in frames]
        return [left, left, frames, frames]

    sheets = {
        'idle': rows(clip(idle)),
        'walk': rows(clip([idle[0]] + [reskin(f, name) for f in strip('_Run')])),
        'slash': rows(clip([reskin(f, name, attack=True) for f in strip('_AttackComboNoMovement')])),
    }
    cast = []
    for r in [0, 1, 2, 3, 4, 4, 4, 4, 3, 2, 1, 0]:
        cast.append(crop(idle[0], ox, oy, (lambda rr: lambda fr: orb(fr, F // 2 + 9, 40, rr, hi))(r) if r else None))
    sheets['spellcast'] = rows(cast)
    sheets['hurt'] = [clip([reskin(f, name) for f in strip('_Hit') + strip('_DeathNoMovement')])]
    stand = crop(idle[0], ox, oy)
    sheets['sit'] = rows([stand, kneel(stand, name, 0.5), kneel(stand, name, 1.0), kneel(stand, name, 1.0), kneel(stand, name, 1.0)])
    return sheets


# The kneel (64 px frame, facing right). The idle pose's legs start at HIP; the upper body drops KNEEL_DROP px at full
# depth. Leg paths are (x, y) points in the frame, interpolated from standing (t = 0) to kneeling (t = 1).
HIP = 50
KNEEL_DROP = 6
BACK_LEG = [((29, 50), (29, 56)), ((28, 56), (28, 60)), ((27, 61), (22, 60))]    # hip, knee (down), toe (behind)
FRONT_LEG = [((37, 50), (36, 56)), ((38, 56), (41, 53)), ((38, 61), (42, 60)), ((40, 61), (45, 61))]   # hip, knee (up), heel, toe


def _stroke(layer, pts, r, c):
    for i in range(len(pts) - 1):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        n = max(1, int(math.hypot(x1 - x0, y1 - y0) * 3))
        for k in range(n + 1):
            t = k / n
            cx, cy = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            for y in range(int(cy - r - 1), int(cy + r + 2)):
                for x in range(int(cx - r - 1), int(cx + r + 2)):
                    if 0 <= x < F and 0 <= y < F and (x - cx) ** 2 + (y - cy) ** 2 <= r * r:
                        layer[y][x] = c


def _leg(layer, path, t, light, mid, dark, joint):
    """One leg: a dark underside, the plate on top, a lit top edge on the thigh and a joint at the knee."""
    pts = [(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t) for a, b in path]
    _stroke(layer, pts, 2.1, dark)
    _stroke(layer, [(x, y - 0.6) for x, y in pts], 1.5, mid)
    _stroke(layer, [(x, y - 1.2) for x, y in pts[:2]], 0.7, light)
    kx, ky = pts[1]
    layer[int(round(ky))][int(round(kx))] = joint


def kneel(stand, name, t):
    """The standing frame lowered into a one-knee kneel (t: 0 standing .. 1 kneeling). New legs go under the upper
    body, then everything is outlined."""
    sk = SKINS[name]['map']
    light, mid, dark = sk[STEEL[0]], sk[STEEL[1]], sk[STEEL[2]]
    joint = sk[GOLD[1]]
    drop = int(round(KNEEL_DROP * t))
    out = [[None] * F for _ in range(F)]
    _leg(out, BACK_LEG, t, mid, dark, sk[GOLD[2]], joint)           # the far leg, a shade darker
    _leg(out, FRONT_LEG, t, light, mid, dark, joint)
    # Outline the legs (where they meet empty space).
    edge = [(x, y) for y in range(F) for x in range(F) if out[y][x] is None and any(
        0 <= x + dx < F and 0 <= y + dy < F and out[y + dy][x + dx] is not None for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    for x, y in edge:
        out[y][x] = OUTLINE
    # The upper body (everything above the hips), lowered, over the legs.
    for y in range(HIP):
        for x in range(F):
            c = stand[y][x]
            if c is not None and 0 <= y + drop < F:
                out[y + drop][x] = c
    return out


PORTRAIT_SCALE = 3


def portrait(name):
    """The character card's art: the reskinned idle pose at 3x, centred in the 96 x 144 card with its feet 4 px above
    the bottom. Glowing pixels (the glow ramp, collar, visor and joint highlights) are marked for the card's glow mask.
    Returns (Canvas, chest box in canvas pixels) for chibi_forge.open_chest."""
    fr = reskin(strip('_Idle')[0], name)
    pts = [(x, y) for y in range(FH) for x in range(FW) if fr[y][x] is not None]
    x0, x1 = min(p[0] for p in pts), max(p[0] for p in pts)
    y0, y1 = min(p[1] for p in pts), max(p[1] for p in pts)
    k = PORTRAIT_SCALE
    ox = (96 - (x1 - x0 + 1) * k) // 2
    oy = 144 - 4 - (y1 - y0 + 1) * k
    sk = SKINS[name]
    glowing = set(sk['hi']) | {sk['visor'], sk['map'][GOLD[0]], sk['map'][SCARF[0]], sk['map'][SCARF[1]]}
    cv = cf.Canvas(96, 144)
    for x, y in pts:
        c = fr[y][x]
        for dy in range(k):
            for dx in range(k):
                cv.set(ox + (x - x0) * k + dx, oy + (y - y0) * k + dy, c + (255,), c in glowing)
    cx = (x0 + x1) // 2 + 1                         # the chest: below the collar, a little forward of centre
    box = (ox + (cx - 4 - x0) * k, oy + 10 * k, ox + (cx + 4 - x0) * k, oy + 17 * k)
    return cv, box


def to_rgba(fr):
    return [[(c[0], c[1], c[2], 255) if c is not None else (0, 0, 0, 0) for c in row] for row in fr]


def build(name, only=None):
    out_dir = os.path.join(cf.CHARS, name)
    os.makedirs(out_dir, exist_ok=True)
    for anim, rows_ in frames_for(name).items():
        if only and anim not in only:
            continue
        cols = len(rows_[0])
        img = [[(0, 0, 0, 0)] * (F * cols) for _ in range(F * len(rows_))]
        for r, frames in enumerate(rows_):
            for c, fr in enumerate(frames):
                rgba = to_rgba(fr)
                for y in range(F):
                    img[r * F + y][c * F:(c + 1) * F] = rgba[y]
        write_png(os.path.join(out_dir, anim + '.png'), F * cols, F * len(rows_), img)
    with open(os.path.join(out_dir, 'CREDITS.md'), 'w', encoding='utf-8') as f:
        f.write('# %s: sprite credits\n\n"Fantasy Knight - Free Pixelart Animated Character" by aamatniekss '
                '(https://aamatniekss.itch.io/fantasy-knight-free-pixelart-animated-character), reskinned as a robot '
                'by `Tools/ChibiForge/knight_model.py`. Free and commercial use and modification are allowed; credit '
                'appreciated; the art may not be redistributed or resold on its own.\n' % name)
    print('built', name, '(knight model)')


def contact(path, scale=4):
    grid = []
    for n in NAMES:
        sh = frames_for(n)
        grid.append([sh['idle'][DOWN][0], sh['idle'][DOWN][5], sh['walk'][RIGHT][2], sh['walk'][RIGHT][6],
                     sh['walk'][LEFT][4], sh['slash'][RIGHT][2], sh['slash'][RIGHT][6], sh['spellcast'][RIGHT][5],
                     sh['hurt'][0][0], sh['hurt'][0][10]])
    W, H = len(grid[0]) * F, len(grid) * F
    bg = (60, 72, 50, 255)
    img = [[bg] * W for _ in range(H)]
    for r, cells in enumerate(grid):
        for c, fr in enumerate(cells):
            for y in range(F):
                for x in range(F):
                    if fr[y][x] is not None:
                        img[r * F + y][c * F + x] = fr[y][x] + (255,)
    write_png(path, W * scale, H * scale, [[img[y // scale][x // scale] for x in range(W * scale)] for y in range(H * scale)])


if __name__ == '__main__':
    args = sys.argv[1:]
    if '--sheet' in args:
        contact(args[args.index('--sheet') + 1])
    else:
        only = args[args.index('--only') + 1].split(',') if '--only' in args else None
        for n in NAMES:
            build(n, only)
