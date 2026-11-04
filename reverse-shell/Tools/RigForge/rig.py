"""
RigForge core: cut-out (skeletal) pixel-art animation, baked to sprite sheets.

A character is a tree of Parts (like a Godot node tree: Root -> Torso -> Head / UpperArm -> Forearm ...). Each part is a
small RGBA bitmap with a pivot (its joint), a parent, an offset from the parent's pivot, and a draw order (z). A pose
gives each part a local rotation and nudge; pose() walks the tree root first, so turning the upper arm carries the
forearm, hand and anything held (forward kinematics).

Pixel-art rules:
- Rotation is RotSprite-style (rotsprite()): the part is scaled x8 with Scale2x three times, turned with
  nearest-neighbour, and brought back down by majority vote, so it keeps its own palette and edges. Angles within
  ROT_SNAP of a right angle are turned exactly (no resampling).
- Every part lands on whole pixels (positions are rounded once, at the end), so nothing drifts between pixels.
- Parts are drawn without outlines. After the pose is put together, finish() draws the plum outline around the figure and
  a dark seam where a part overlaps one behind it, so the joints read like hand-drawn sprites.

Needs Pillow (py -3.9 on this machine).
"""
import math

from PIL import Image

OUTLINE = (0x20, 0x02, 0x1D, 255)   # the sprites' plum outline (chibi_forge.OUTLINE)
ROT_SNAP = 4.0                       # degrees


class Part(object):
    """A bitmap with a pivot. offset = where its pivot sits relative to the parent's pivot, in the parent's frame."""

    def __init__(self, name, image, pivot, parent=None, offset=(0, 0), z=0, seam=True):
        self.name, self.image, self.pivot = name, image, pivot
        self.parent, self.offset, self.z, self.seam = parent, offset, z, seam


class Skeleton(object):
    def __init__(self, parts, root):
        self.parts = {p.name: p for p in parts}
        self.root = root
        self.order = []          # parents before children
        seen = set()

        def visit(n):
            if n in seen:
                return
            p = self.parts[n]
            if p.parent:
                visit(p.parent)
            seen.add(n)
            self.order.append(n)

        for n in self.parts:
            visit(n)

    def swap(self, name, image, pivot=None):
        """A copy of this skeleton with one part's art replaced (outfits, held items, other characters)."""
        parts = []
        for p in self.parts.values():
            if p.name == name:
                p = Part(p.name, image, pivot or p.pivot, p.parent, p.offset, p.z, p.seam)
            parts.append(p)
        return Skeleton(parts, self.root)


# ----------------------------------------------------------------------------------------------- rotation

def _scale2x(im):
    w, h = im.size
    p = im.load()
    out = Image.new('RGBA', (w * 2, h * 2), (0, 0, 0, 0))
    q = out.load()
    for y in range(h):
        for x in range(w):
            E = p[x, y]
            B = p[x, y - 1] if y > 0 else E
            D = p[x - 1, y] if x > 0 else E
            F = p[x + 1, y] if x < w - 1 else E
            H = p[x, y + 1] if y < h - 1 else E
            if B != H and D != F:
                e0 = D if D == B else E
                e1 = F if B == F else E
                e2 = D if D == H else E
                e3 = F if H == F else E
            else:
                e0 = e1 = e2 = e3 = E
            q[2 * x, 2 * y], q[2 * x + 1, 2 * y], q[2 * x, 2 * y + 1], q[2 * x + 1, 2 * y + 1] = e0, e1, e2, e3
    return out


def _down8(im):
    """Back to 1x: each 8x8 block takes its most common opaque colour, if at least half the block is opaque."""
    w, h = im.size[0] // 8, im.size[1] // 8
    p = im.load()
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    q = out.load()
    for by in range(h):
        for bx in range(w):
            count, solid = {}, 0
            for y in range(by * 8 + 1, by * 8 + 7):      # the middle 6x6 votes (the edges are interpolation noise)
                for x in range(bx * 8 + 1, bx * 8 + 7):
                    c = p[x, y]
                    if c[3] > 127:
                        solid += 1
                        count[c] = count.get(c, 0) + 1
            if solid >= 18:
                q[bx, by] = max(count, key=count.get)
    return out


_rot_cache = {}


def rotsprite(image, pivot, angle):
    """Turns image by angle degrees (counter-clockwise on screen) about pivot. Returns (image, new pivot)."""
    a = angle % 360.0
    for right in (0.0, 90.0, 180.0, 270.0, 360.0):
        if abs(a - right) <= ROT_SNAP:
            a = right % 360.0
            break
    if a == 0.0:
        return image, pivot
    key = (id(image), pivot, round(a, 1))
    hit = _rot_cache.get(key)
    if hit:
        return hit
    w, h = image.size
    px, py = pivot
    # pad so the pivot is the centre of a square big enough for any turn
    r = int(math.ceil(max(math.hypot(px - cx, py - cy) for cx in (0, w) for cy in (0, h)))) + 1
    side = 2 * r + 1
    pad = Image.new('RGBA', (side, side), (0, 0, 0, 0))
    pad.paste(image, (r - px, r - py))
    if a in (90.0, 180.0, 270.0):
        turned = pad.rotate(a, resample=Image.NEAREST)
    else:
        big = _scale2x(_scale2x(_scale2x(pad)))
        big = big.rotate(a, resample=Image.NEAREST, center=(r * 8 + 4, r * 8 + 4))
        turned = _down8(big)
    _rot_cache[key] = (turned, (r, r))
    return turned, (r, r)


# ----------------------------------------------------------------------------------------------- posing

def _turn(v, deg):
    """Rotates a vector by deg (counter-clockwise on screen, y down)."""
    t = math.radians(deg)
    c, s = math.cos(t), math.sin(t)
    return (v[0] * c + v[1] * s, -v[0] * s + v[1] * c)


def pose(skel, keys, hide=()):
    """keys: {part: (angle, dx, dy[, z])}. Returns [(z, name, image, (x, y) top-left)] in world pixels (root pivot = 0,0)."""
    world = {}   # name -> (angle, (x, y))
    placed = []
    for n in skel.order:
        p = skel.parts[n]
        k = keys.get(n, ())
        ang = k[0] if len(k) > 0 else 0.0
        dx = k[1] if len(k) > 1 else 0.0
        dy = k[2] if len(k) > 2 else 0.0
        z = k[3] if len(k) > 3 else p.z
        if p.parent:
            pa, (ppx, ppy) = world[p.parent]
            ox, oy = _turn((p.offset[0] + dx, p.offset[1] + dy), pa)
            pos, a = (ppx + ox, ppy + oy), pa + ang
        else:
            pos, a = (p.offset[0] + dx, p.offset[1] + dy), ang
        world[n] = (a, pos)
        if n in hide or p.image is None:
            continue
        img, (qx, qy) = rotsprite(p.image, p.pivot, a)
        placed.append((z, n, img, (int(round(pos[0] - qx)), int(round(pos[1] - qy)))))
    placed.sort(key=lambda t: t[0])
    return placed, world


def compose(placed, size, origin, seams=True):
    """Draws posed parts into a size canvas with the root pivot at origin, then finishes it (outline and seams)."""
    W, H = size
    canvas = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    owner = [[None] * W for _ in range(H)]
    rank = {}
    cp = canvas.load()
    for z, n, img, (x, y) in placed:
        rank[n] = z
        ip = img.load()
        for j in range(img.size[1]):
            for i in range(img.size[0]):
                c = ip[i, j]
                if c[3] > 127:
                    X, Y = x + i + origin[0], y + j + origin[1]
                    if 0 <= X < W and 0 <= Y < H:
                        cp[X, Y] = c
                        owner[Y][X] = n
    return finish(canvas, owner, rank, seams)


def finish(canvas, owner, rank, seams=True):
    W, H = canvas.size
    p = canvas.load()
    out = canvas.copy()
    q = out.load()
    if seams:
        # a part's edge pixel next to a part drawn behind it becomes a dark seam
        for y in range(H):
            for x in range(W):
                n = owner[y][x]
                if n is None:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    X, Y = x + dx, y + dy
                    if 0 <= X < W and 0 <= Y < H:
                        m = owner[Y][X]
                        if m is not None and m != n and rank.get(m, 0) < rank.get(n, 0) and not _same_group(n, m):
                            c = p[x, y]
                            q[x, y] = (c[0] * 45 // 100 + 8, c[1] * 40 // 100, c[2] * 45 // 100 + 8, 255)
                            break
    # the plum outline around the whole figure
    final = out.copy()
    f = final.load()
    for y in range(H):
        for x in range(W):
            if q[x, y][3] == 0:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    X, Y = x + dx, y + dy
                    if 0 <= X < W and 0 <= Y < H and q[X, Y][3]:
                        f[x, y] = OUTLINE
                        break
    return final


GROUPS = {}   # part name -> group; parts in one group (e.g. thigh + shin of one leg) don't seam against each other


def _same_group(a, b):
    ga, gb = GROUPS.get(a), GROUPS.get(b)
    return ga is not None and ga == gb


# ----------------------------------------------------------------------------------------------- keyframes

def ease(t):
    return t * t * (3 - 2 * t)


def sample(keys, t, loop=True):
    """keys: [(time 0..1, {part: (angle, dx, dy[, z])}), ...] sorted. Returns the pose at t (eased between keys)."""
    if loop:
        t = t % 1.0
        keys = keys + [(1.0, keys[0][1])]
    if t <= keys[0][0]:
        return dict(keys[0][1])
    for (t0, a), (t1, b) in zip(keys, keys[1:]):
        if t0 <= t <= t1:
            u = ease((t - t0) / (t1 - t0)) if t1 > t0 else 1.0
            out = {}
            for n in set(a) | set(b):
                va, vb = a.get(n, (0, 0, 0)), b.get(n, (0, 0, 0))
                m = max(len(va), len(vb))
                va = tuple(va) + (0,) * (m - len(va))
                vb = tuple(vb) + (0,) * (m - len(vb))
                v = [va[i] + (vb[i] - va[i]) * u for i in range(min(m, 3))]
                if m > 3:   # z is not blended: it switches at the halfway point
                    v.append(va[3] if u < 0.5 else vb[3])
                out[n] = tuple(v)
            return out
    return dict(keys[-1][1])


def merge(*poses):
    """Adds poses part by part (a base clip plus a layered sway, say)."""
    out = {}
    for p in poses:
        for n, v in p.items():
            o = out.get(n, (0, 0, 0))
            m = max(len(o), len(v))
            o = tuple(o) + (0,) * (m - len(o))
            v2 = tuple(v) + (0,) * (m - len(v))
            s = [o[i] + v2[i] for i in range(min(m, 3))]
            if m > 3:
                s.append(v2[3] if len(v) > 3 else o[3])
            out[n] = tuple(s)
    return out
