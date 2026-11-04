"""
RigForge drawing helpers: parts are painted in code on small canvases.

A Sheet is one part's canvas. Shapes are filled with a 4-tone ramp (dark, mid-dark, mid-light, light) and shaded
automatically, lit from the top left like the rest of the game's sprites: pixels on a shape's upper / left edge take the
light tone, its lower / right edge the dark-mid tone, the rest the mid tone. Details (eyes, trim, glow lines) are put on
top pixel by pixel. No outlines here: rig.finish() outlines the posed figure.
"""
from PIL import Image


def hx(s):
    s = s.lstrip('#')
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), 255)


def ramp(*hexes):
    return [hx(h) for h in hexes]


class Sheet(object):
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = {}

    # --- masks (sets of (x, y)) -------------------------------------------------------------------------------
    def rect(self, x0, y0, x1, y1):
        return {(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)}

    def ellipse(self, cx, cy, rx, ry):
        out = set()
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1.0:
                    out.add((x, y))
        return out

    def poly(self, pts):
        """Filled polygon (pixel centres inside)."""
        out = set()
        ys = [p[1] for p in pts]
        for y in range(int(min(ys)), int(max(ys)) + 1):
            yc = y + 0.5
            xs = []
            for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]):
                if (y0 <= yc < y1) or (y1 <= yc < y0):
                    xs.append(x0 + (yc - y0) * (x1 - x0) / float(y1 - y0))
            xs.sort()
            for a, b in zip(xs[0::2], xs[1::2]):
                for x in range(int(round(a)), int(round(b))):
                    out.add((x, y))
        return out

    def taper(self, x, y0, y1, w0, w1):
        """A vertical limb from y0 to y1, centred on x, w0 wide at the top and w1 at the bottom."""
        out = set()
        for y in range(y0, y1 + 1):
            t = (y - y0) / float(max(1, y1 - y0))
            w = w0 + (w1 - w0) * t
            a = x - w / 2.0
            for xx in range(int(round(a)), int(round(a + w))):
                out.add((xx, y))
        return out

    # --- painting ------------------------------------------------------------------------------------------
    def fill(self, mask, r, light=True):
        """Fills a mask with ramp r, auto-shaded (top-left light)."""
        mask = {(x, y) for (x, y) in mask if 0 <= x < self.w and 0 <= y < self.h}
        for (x, y) in mask:
            up, left = (x, y - 1) not in mask, (x - 1, y) not in mask
            down, right = (x, y + 1) not in mask, (x + 1, y) not in mask
            if not light:
                tone = 1
            elif (up or left) and not (down or right):
                tone = 3
            elif (down or right) and not (up or left):
                tone = 1
            else:
                tone = 2
            self.px[(x, y)] = r[tone]
        return mask

    def flat(self, mask, c):
        for (x, y) in mask:
            if 0 <= x < self.w and 0 <= y < self.h:
                self.px[(x, y)] = c

    def put(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[(x, y)] = c

    def line(self, x0, y0, x1, y1, c):
        n = max(abs(x1 - x0), abs(y1 - y0), 1)
        for i in range(n + 1):
            self.put(int(round(x0 + (x1 - x0) * i / float(n))), int(round(y0 + (y1 - y0) * i / float(n))), c)

    def image(self):
        im = Image.new('RGBA', (self.w, self.h), (0, 0, 0, 0))
        p = im.load()
        for (x, y), c in self.px.items():
            p[x, y] = c
        return im
