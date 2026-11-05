"""
A3's portrait: the A2 pixel-art study (reference/A3_study.gif) without its sword, for the menus and cards.

    py -3.9 Tools/ChibiForge/a3_portrait.py      (a3_model.py runs it too)

The study is 64 x 84 px art scaled up 3.83x (not a whole number) onto white with a grey ground shadow. This samples it
back to its own pixels (each cell takes its most common colour), clears the white and the shadow, and removes the
sword by its colours (SWORD: the blade's greens and greys and the brown cord tied round it; none of them are used on
her). Allies (alt_recolour.LOOKS) get the same art with the hair in their Lospec palette (palettes/palettes.json).
Output: Art/Characters/<name>/portrait.png, one 128 px cell per facing row (as drawn = left and down, mirrored = up
and right), feet on the same row as her other clips, so Anim_A3's "portrait" clip lines up like the rest.

Used by: the Esc menu's party page (CharacterMenu), Soul cards (CardArt.SoulSprite, cropped by Anim_A3.portraitCrop)
and the Ult pictures (card_forge.sprite_art). The overworld and battle keep the sheet sprite.
Needs Pillow (py -3.9).
"""
import os
from collections import Counter

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
STUDY = os.path.join(HERE, 'reference', 'A3_study.gif')

NATIVE = (64, 84)
GRID_X0, GRID_Y0 = 0.86, 0.0      # where the first pixel column / row of the upscale starts (measured)
FOOT = (27, 71)                   # between her feet, bottom row (native px)
CELL, FEET_ROW = 128, 14          # as a3_model

SWORD = {(38, 41, 43), (76, 78, 76), (58, 61, 63), (103, 109, 102), (9, 9, 2), (138, 144, 135), (30, 41, 36),
         (56, 58, 58), (193, 188, 177), (247, 246, 246), (79, 97, 90), (3, 0, 4), (40, 39, 39), (109, 79, 54),
         (97, 68, 43), (73, 75, 73), (56, 59, 60), (37, 39, 41), (230, 226, 225), (222, 191, 165), (219, 208, 204),
         (9, 8, 1), (57, 56, 56), (50, 42, 39), (186, 152, 120), (80, 70, 65), (119, 89, 62), (28, 17, 8), (81, 50, 19)}
SHADOW_FROM_ROW = 66              # the ground shadow: pure greys from this row down


def native():
    src = Image.open(STUDY)
    src.seek(0)
    src = src.convert('RGBA')
    W, H = src.size
    p = src.load()
    nw, nh = NATIVE
    sx, sy = W / float(nw), H / float(nh)
    out = Image.new('RGBA', NATIVE, (0, 0, 0, 0))
    q = out.load()
    for j in range(nh):
        for i in range(nw):
            x0, x1 = int(round(GRID_X0 + i * sx)), int(round(GRID_X0 + (i + 1) * sx))
            y0, y1 = int(round(GRID_Y0 + j * sy)), int(round(GRID_Y0 + (j + 1) * sy))
            votes = Counter(p[x, y] for x in range(x0, min(x1, W)) for y in range(y0, min(y1, H)))
            q[i, j] = votes.most_common(1)[0][0]
    return out


def cut(im):
    p = im.load()
    for y in range(im.height):
        for x in range(im.width):
            c = p[x, y][:3]
            if c == (255, 255, 255) or c in SWORD or (y >= SHADOW_FROM_ROW and c[0] == c[1] == c[2]):
                p[x, y] = (0, 0, 0, 0)
    return im


# The study's hair: its outline and five warm greys (dark to light). Allies get these mapped onto their Lospec hair ramp.
STUDY_HAIR = [(136, 127, 125), (161, 158, 153), (181, 178, 174), (206, 201, 193), (232, 225, 213), (255, 255, 247)]
CHARS = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')


STUDY_IRIS = [(74, 103, 133), (121, 151, 164)]   # the eyes, dark and light (the white highlight stays)


def _ramp_at(ramp, i, n):
    return ramp[int(round(i * (len(ramp) - 1) / float(max(1, n - 1))))]


def dress(fig, look):
    """The study (or any frame drawn from it) in a character's palette: the hair greys onto their hair ramp and the
    irises onto their eye ramp (look = an alt_recolour.LOOKS entry; A3's has neither, so she keeps A2's colours)."""
    hair = look.get('hair_ramp') if look.get('hair') is not None else None
    eyes = look.get('eyes_ramp')
    if not hair and not eyes:
        return fig
    out = fig.copy()
    p = out.load()
    for y in range(out.height):
        for x in range(out.width):
            c = p[x, y]
            if not c[3]:
                continue
            if hair and c[:3] in STUDY_HAIR:
                p[x, y] = _ramp_at(hair, STUDY_HAIR.index(c[:3]), len(STUDY_HAIR)) + (c[3],)
            elif eyes and c[:3] in STUDY_IRIS:
                p[x, y] = _ramp_at(eyes, STUDY_IRIS.index(c[:3]), len(STUDY_IRIS)) + (c[3],)
    return out


def build():
    """Every ally's portrait clip: the study's idle loop (a3_study: hair in the wind, a shrug, a blink), dressed in their
    palette (A3 as drawn), one 128 px cell per frame, with their Anim entries."""
    import alt_recolour
    import a3_study
    frames = a3_study.frames()
    for folder, look in alt_recolour.LOOKS.items():
        sheet = Image.new('RGBA', (CELL * len(frames), CELL * 4), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            art = dress(f, look)
            cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
            cell.paste(art, (CELL // 2 - FOOT[0], CELL - FEET_ROW - FOOT[1]), art)
            flipped = cell.transpose(Image.FLIP_LEFT_RIGHT)
            for row in range(4):   # up, left, down, right
                sheet.paste(cell if row in (1, 2) else flipped, (i * CELL, row * CELL))
        out = os.path.join(CHARS, folder, 'portrait.png')
        import a3_model
        a3_model.check_size(sheet, out)
        sheet.save(out)
        alt_recolour.write_meta(os.path.join(CHARS, 'A3', 'idle.png.meta'), out)
        register(folder, alt_recolour.meta_guid(out), a3_study.FPS)
        print('portrait', out, len(frames), 'frames')


# The Soul card's window on the portrait (SpriteAnimationSet.portraitCrop, fractions of the cell from the bottom left):
# 42 x 48 px from just above her hair down to the ankles, the card's art box at one card pixel per sprite pixel.
CROP = dict(x=46 / 128.0, y=20 / 128.0, width=42 / 128.0, height=48 / 128.0)


def register(folder, guid, fps=8):
    """Puts the portrait clip (this folder's sheet) and its Soul card crop into Anim_<folder>."""
    import re
    path = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Animations', 'Anim_%s.asset' % folder)
    anim = open(path, encoding='utf-8').read()
    crop = ('  portraitCrop:\n    serializedVersion: 2\n    x: %s\n    y: %s\n    width: %s\n    height: %s\n'
            % (CROP['x'], CROP['y'], CROP['width'], CROP['height']))
    anim = re.sub(r'  portraitCrop:\n(    .*\n){5}', '', anim)
    anim = anim.replace('  clips:\n', crop + '  clips:\n', 1)
    anim = re.sub(r'  - name: portrait\n(    .*\n)+', '', anim)
    anim = anim.rstrip('\n') + '\n' + ('  - name: portrait\n    sheet: {fileID: 2800000, guid: %s, type: 3}\n'
                                       '    fps: %d\n    loop: 1\n    firstFrame: 0\n    frameCount: 0\n') % (guid, fps)
    open(path, 'w', encoding='utf-8', newline='\n').write(anim)


if __name__ == '__main__':
    import sys
    sys.path.insert(0, HERE)
    build()
