"""
Design sheet for a rig character: its parts laid out, and a few assembled poses at 1x and 4x next to a reference sprite
for scale.

    py -3.9 Tools/RigForge/design.py out.png [reference.png]
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import rig  # noqa: E402
import humanoid as hu  # noqa: E402

CELL = 128
FEET = CELL - 14

POSES = {
    'rest': {},
    'arms folded': {
        'arm_near_up': (-25, 0, 0), 'arm_near_fore': (-70, 0, 0),
        'arm_far_up': (15, 0, 0), 'arm_far_fore': (75, 0, 0, 76),
    },
    'battle stance': {
        'hips': (0, 0, 1), 'torso': (-6, 0, 0),
        'leg_far_thigh': (-28, 0, 0), 'leg_far_shin': (14, 0, 0),
        'leg_near_thigh': (20, 0, 0), 'leg_near_shin': (-6, 0, 0),
        'arm_near_up': (-15, 0, 0), 'arm_near_fore': (-35, 0, 0), 'held': (105, 0, 0),
        'arm_far_up': (-30, 0, 0), 'arm_far_fore': (-50, 0, 0),
        'hair_back': (-6, 0, 0), 'hair_tail': (-10, 0, 0),
    },
}


def render(skel, keys, hide=()):
    placed, _ = rig.pose(skel, keys, hide)
    return rig.compose(placed, (CELL, CELL), (CELL // 2, FEET - hu.GROUND))


def main(out, ref=None):
    L = hu.A3
    sword = (hu.sword_art(L), hu.SWORD_GRIP)
    skel = hu.build(L, sword)
    poses = [('rest', render(skel, POSES['rest'], hide=('held',))),
             ('arms folded', render(skel, POSES['arms folded'], hide=('held',))),
             ('battle stance', render(skel, POSES['battle stance']))]
    z = 4
    parts = [(n, skel.parts[n].image) for n in skel.order]
    pw = sum(p.size[0] * z + 12 for _, p in parts)
    W = max(pw, len(poses) * (CELL * z // 2 + 20) + 400)
    H = 60 + max(p.size[1] for _, p in parts) * z + 40 + CELL * z // 2 + 120
    sheet = Image.new('RGBA', (W, H), (70, 84, 66, 255))
    d = ImageDraw.Draw(sheet)
    d.text((8, 6), 'A3 - parts (4x)', fill=(255, 240, 160, 255))
    x = 8
    for n, im in parts:
        sheet.alpha_composite(im.resize((im.size[0] * z, im.size[1] * z), Image.NEAREST), (x, 24))
        d.text((x, 24 + im.size[1] * z + 2), n.replace('_', ' '), fill=(230, 230, 230, 255))
        x += im.size[0] * z + 12
    y0 = 60 + max(p.size[1] for _, p in parts) * z + 30
    x = 8
    crop = (CELL // 4, CELL * 7 // 16, CELL * 3 // 4, CELL - 8)   # around the figure
    for name, im in poses:
        c = im.crop(crop)
        big = c.resize((c.size[0] * z, c.size[1] * z), Image.NEAREST)
        sheet.alpha_composite(big, (x, y0))
        d.text((x, y0 - 14), name + ' (4x)', fill=(255, 240, 160, 255))
        sheet.alpha_composite(c, (x + 4, y0 + big.size[1] + 6))
        x += big.size[0] + 20
    if ref:
        r = Image.open(ref).convert('RGBA')
        d.text((x, y0 - 14), 'scale: knight / old A3 (1x and 4x)', fill=(255, 240, 160, 255))
        sheet.alpha_composite(r, (x, y0 + 0))
        sheet.alpha_composite(r.resize((r.size[0] * 4, r.size[1] * 4), Image.NEAREST), (x, y0 + r.size[1] + 8))
    sheet.save(out)
    for name, im in poses:
        bb = im.getbbox()
        print(name, 'bbox', bb, 'height', bb[3] - bb[1], 'feet row', bb[3] - 1, 'target', FEET - 1)


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
