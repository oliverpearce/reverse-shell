"""
The humanoid skeleton every person in the game shares (A3, the party, the Collector), and A3's parts.

Bones (parent -> child), drawn facing left in a 3/4 view; "near" parts are on the viewer's side (drawn in front):

    hips (root)
      torso
        head
          hair_front, hair_back
            hair_tail
        arm_near_up -> arm_near_fore -> held (an item in the near hand)
        arm_far_up  -> arm_far_fore
      sash
      leg_near_thigh -> leg_near_shin
      leg_far_thigh  -> leg_far_shin

Sizes are the knight's density: the figure is ~38 px tall with its outline. The root pivot (hips) is (0, 0); the soles
rest on y = GROUND.
A character is a Look: a palette plus its art functions. build(look) paints the parts and returns the Skeleton.
"""
from art import Sheet, ramp, hx
from rig import Part, Skeleton, GROUPS

GROUND = 18           # the soles' row, below the hips pivot
HEAD_PIVOT = (8, 14)  # head and bangs share one canvas layout, so they line up with no offsets

for g, names in (('leg_near', ('leg_near_thigh', 'leg_near_shin')), ('leg_far', ('leg_far_thigh', 'leg_far_shin')),
                 ('arm_near', ('arm_near_up', 'arm_near_fore')), ('arm_far', ('arm_far_up', 'arm_far_fore')),
                 ('head', ('head', 'hair_front', 'hair_back', 'hair_tail'))):
    for n in names:
        GROUPS[n] = g


class Look(object):
    def __init__(self, name, **kw):
        self.name = name
        self.__dict__.update(kw)


# --------------------------------------------------------------------------------------------- A3's look

A3 = Look(
    'A3',
    skin=ramp('#9a5e56', '#c88676', '#e8b09c', '#fad6c6'),
    hair=ramp('#4c4a5c', '#8e8ca0', '#c9c8d6', '#f2f1f6'),       # silver-white, lavender shade
    suit=ramp('#140f18', '#271e2c', '#3b2f42', '#54465c'),       # plum-black bodysuit
    metal=ramp('#262b34', '#434b57', '#6a7482', '#9aa6b4'),      # gunmetal bracers, boots, blade
    glow=ramp('#16707e', '#2fb2c2', '#7ae8f2', '#d8fcff'),       # android cyan (her own accent)
    eye=hx('#2fb2c2'),
)


# --------------------------------------------------------------------------------------------- parts

def _dim(s, k=4, d=5):
    """The far limbs sit in shadow."""
    for xy, c in list(s.px.items()):
        s.px[xy] = (c[0] * k // d, c[1] * k // d, c[2] * k // d, 255)


def head_art(L):
    s = Sheet(16, 16)
    s.fill(s.ellipse(7.5, 8.0, 4.6, 5.2), L.skin)                       # face, turned to the left
    s.fill(s.rect(7, 12, 8, 14), L.skin)                                # neck
    s.put(6, 13, L.skin[1]); s.put(7, 13, L.skin[1])
    s.put(4, 8, L.suit[0]); s.put(5, 8, L.suit[0])                      # lash line
    s.put(4, 9, L.eye); s.put(5, 9, L.glow[3])                          # the eye: cyan iris, glint
    s.put(4, 10, L.glow[0])
    s.put(5, 12, L.skin[1])                                             # mouth shadow
    s.put(3, 11, L.skin[2])
    return s.image()


def hair_front_art(L):
    s = Sheet(16, 18)
    s.fill(s.ellipse(8.0, 5.8, 6.0, 4.4), L.hair)                       # crown
    s.fill(s.poly([(7, 4), (13, 3), (13, 9), (11, 11), (10, 8), (9, 10), (8, 6)]), L.hair)   # fringe over the far eye
    s.fill(s.poly([(2, 5), (4, 5), (4, 11), (3, 15), (2, 13)]), L.hair)  # a thin lock framing the face
    for x, y in ((7, 2), (8, 2), (6, 3), (9, 3), (10, 4)):
        s.put(x, y, L.hair[3])                                          # shine band
    s.put(11, 10, L.hair[0]); s.put(10, 7, L.hair[1]); s.put(3, 12, L.hair[1]); s.put(5, 5, L.hair[1])
    return s.image()


def hair_back_art(L):
    """Shares the head canvas layout for its first rows (pivot = the neck), then falls behind her back."""
    s = Sheet(20, 25)
    s.fill(s.ellipse(8.5, 6.5, 6.4, 5.4), L.hair)
    s.fill(s.poly([(5, 7), (14, 6), (16, 14), (16, 24), (9, 24), (7, 16)]), L.hair)
    for x in (10, 13, 15):                                              # strands
        for y in range(10 + x % 3, 24):
            if (y + x) % 4:
                s.put(x, y, L.hair[1])
    for y in range(12, 24):
        s.put(8 if y < 17 else 9, y, L.hair[0])                         # the inner edge, in shadow
    _dim(s, 9, 10)                                                      # behind her: a touch darker
    return s.image()


def hair_tail_art(L):
    s = Sheet(10, 8)
    s.fill(s.poly([(1, 0), (8, 0), (9, 4), (8, 7), (6, 5), (4, 7), (2, 4)]), L.hair)
    for x in (3, 6):
        for y in range(0, 6):
            if (y + x) % 3:
                s.put(x, y, L.hair[1])
    _dim(s, 9, 10)
    return s.image()


def torso_art(L):
    s = Sheet(13, 11)
    s.fill(s.poly([(1, 1), (12, 1), (11, 5), (10, 10), (3, 10), (2, 5)]), L.suit)   # shoulders to waist
    s.fill(s.rect(1, 1, 2, 3), L.skin); s.fill(s.rect(11, 1, 12, 3), L.skin)        # bare shoulders
    s.fill(s.rect(5, 0, 8, 1), L.suit)                                               # high collar
    s.put(6, 1, L.glow[2]); s.put(7, 1, L.glow[2])
    s.put(5, 4, L.suit[3]); s.put(8, 4, L.suit[2])                                   # bust light
    s.fill(s.rect(6, 6, 7, 7), L.skin)                                               # a small cut-out at the waist
    for y in range(3, 9):
        s.put(10 if y < 6 else 9, y, L.glow[1])                                      # glowing side seam
    s.fill(s.rect(3, 9, 10, 10), L.metal)                                            # belt
    s.put(6, 9, L.glow[3]); s.put(7, 9, L.glow[2])                                   # buckle
    return s.image()


def hips_art(L):
    s = Sheet(11, 5)
    s.fill(s.poly([(1, 0), (10, 0), (10, 2), (7, 4), (4, 4), (1, 2)]), L.suit)
    return s.image()


def sash_art(L):
    s = Sheet(5, 12)
    s.fill(s.poly([(0, 0), (4, 0), (4, 10), (2, 11), (0, 9)]), L.suit)
    for y in range(1, 10):
        s.put(4, y, L.glow[1])
    s.put(3, 10, L.glow[1])
    return s.image()


def thigh_art(L, near=True):
    s = Sheet(6, 9)
    s.fill(s.taper(3, 0, 7, 4, 3), L.skin)                                           # bare thigh
    s.fill(s.taper(3, 3, 7, 4, 3), L.suit)                                           # thigh-high boot
    s.put(2, 3, L.metal[3]); s.put(3, 3, L.metal[2]); s.put(4, 3, L.metal[1])        # boot band
    if not near:
        _dim(s)
    return s.image()


def shin_art(L, near=True):
    s = Sheet(7, 10)
    s.fill(s.taper(4, 0, 6, 3, 2), L.suit)
    s.fill(s.poly([(1, 6), (6, 6), (6, 8), (0, 8), (0, 7)]), L.metal)                # boot, toe forward (left)
    s.put(4, 0, L.metal[3]); s.put(3, 1, L.metal[2])                                 # knee plate
    s.put(5, 8, L.metal[0]); s.put(2, 7, L.glow[1])
    if not near:
        _dim(s)
    return s.image()


def upper_arm_art(L, near=True):
    s = Sheet(5, 8)
    s.fill(s.taper(2, 0, 6, 3, 2), L.skin)
    if not near:
        _dim(s)
    return s.image()


def forearm_art(L, near=True):
    s = Sheet(5, 9)
    s.fill(s.taper(2, 0, 4, 3, 3), L.metal)                                          # bracer
    s.put(2, 1, L.glow[2]); s.put(2, 3, L.glow[1])
    s.fill(s.rect(1, 5, 3, 7), L.skin)                                               # hand
    s.put(3, 7, L.skin[1])
    if not near:
        _dim(s)
    return s.image()


def sword_art(L):
    """A3's greatsword, hanging blade-down from the grip (the pivot). Squared, notched tip; a cyan core."""
    s = Sheet(12, 44)
    s.fill(s.rect(5, 0, 6, 6), L.suit)                                               # grip
    s.put(5, 0, L.metal[2]); s.put(6, 0, L.metal[2])                                 # pommel
    s.fill(s.rect(1, 7, 10, 8), L.metal)                                             # guard
    s.put(1, 7, L.glow[2]); s.put(10, 7, L.glow[2])
    s.fill(s.poly([(2, 9), (10, 9), (10, 40), (8, 43), (6, 41), (4, 43), (2, 41)]), L.metal)
    for y in range(10, 41):
        s.put(2, y, L.metal[3])                                                      # the edge catches light
    for y in range(12, 37):
        s.put(6, y, L.glow[1] if y % 6 else L.glow[3])                              # core line
    for y in (16, 24, 32):
        s.put(9, y, L.metal[0]); s.put(9, y + 1, L.metal[0])                         # notches on the spine
    return s.image()


SWORD_GRIP = (5, 3)


# --------------------------------------------------------------------------------------------- skeleton

def build(L, held=None):
    """The humanoid skeleton painted with look L. held = (image, pivot) for an item in the near hand, or None."""
    parts = [
        Part('hips', hips_art(L), (5, 1), None, (0, 0), 40),
        Part('torso', torso_art(L), (6, 10), 'hips', (0, 0), 50),
        Part('head', head_art(L), HEAD_PIVOT, 'torso', (1, -8), 70),
        Part('hair_front', hair_front_art(L), HEAD_PIVOT, 'head', (0, 0), 75),
        Part('hair_back', hair_back_art(L), HEAD_PIVOT, 'head', (0, 0), 10),
        Part('hair_tail', hair_tail_art(L), (5, 0), 'hair_back', (6, 10), 5),
        Part('arm_far_up', upper_arm_art(L, False), (2, 1), 'torso', (-4, -8), 20),
        Part('arm_far_fore', forearm_art(L, False), (2, 0), 'arm_far_up', (0, 6), 21),
        Part('leg_far_thigh', thigh_art(L, False), (3, 0), 'hips', (-2, 2), 30),
        Part('leg_far_shin', shin_art(L, False), (4, 0), 'leg_far_thigh', (0, 6), 31),
        Part('leg_near_thigh', thigh_art(L), (3, 0), 'hips', (2, 2), 44),
        Part('leg_near_shin', shin_art(L), (4, 0), 'leg_near_thigh', (0, 6), 45),
        Part('sash', sash_art(L), (2, 0), 'hips', (-3, 1), 60),
        Part('arm_near_up', upper_arm_art(L), (2, 1), 'torso', (5, -8), 80),
        Part('arm_near_fore', forearm_art(L), (2, 0), 'arm_near_up', (0, 6), 81),
    ]
    if held is not None:
        img, pivot = held
        parts.append(Part('held', img, pivot, 'arm_near_fore', (0, 6), 85, seam=False))
    return Skeleton(parts, 'hips')
