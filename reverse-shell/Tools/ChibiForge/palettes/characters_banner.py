"""
The README's characters banner: each party member's name, home area and Lospec colour palette (palettes.json).

    py -3.9 Tools/ChibiForge/palettes/characters_banner.py      -> .github/readme/characters.png (960 px wide)

Same look as the README's other headers (h-about.png): ink background, gold monogram text, a gold rule with an end dot.
Rerun after changing a palette. Needs Pillow (py -3.9).
"""
import json
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..', '..'))          # reverse-shell/
REPO = os.path.dirname(ROOT)
FONT = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Fonts', 'monogram-extended.ttf')
OUT = os.path.join(REPO, '.github', 'readme', 'characters.png')

INK = (27, 26, 24)
GOLD = (194, 160, 97)
GOLD_DIM = (90, 76, 50)
PAPER = (232, 225, 210)
PAPER_DIM = (150, 142, 128)

W = 960
HEAD = 64
ROW = 92
PAD = 26
SWATCH = 26
PAL_X = 470          # where the swatches start
ACCENT_X = 768       # the accent column (lined up on every row)
ORDER = ['A3', 'Erin', 'Lotus', 'Ashe', 'Zuko']   # in story order (by home area)
CHAPTER = {'A3': 1, 'Erin': 1, 'Lotus': 2, 'Ashe': 3, 'Zuko': 4}   # the chapter each is introduced in (bible §2)


def hx(s):
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


def main():
    pal = json.load(open(os.path.join(HERE, 'palettes.json'), encoding='utf-8'))
    pal = pal.get('characters', pal)
    big = ImageFont.truetype(FONT, 48)
    mid = ImageFont.truetype(FONT, 32)
    small = ImageFont.truetype(FONT, 16)
    h = HEAD + ROW * len(ORDER) + 12
    im = Image.new('RGB', (W, h), INK)
    d = ImageDraw.Draw(im)
    d.fontmode = '1'   # crisp pixel text

    # the header, like h-about.png
    d.text((PAD, 10), 'Characters', font=big, fill=GOLD)
    tw = d.textlength('Characters', font=big)
    y = HEAD // 2
    d.line([PAD + tw + 26, y, W - PAD - 4, y], fill=GOLD_DIM, width=1)
    d.rectangle([W - PAD - 4, y - 3, W - PAD + 2, y + 3], fill=GOLD)

    for i, name in enumerate(ORDER):
        p = pal[name]
        top = HEAD + i * ROW
        if i:
            d.line([PAD, top, W - PAD, top], fill=(44, 42, 38), width=1)
        accent = hx(p['accent'])
        # a bar in their accent colour, then name and area
        d.rectangle([PAD, top + 16, PAD + 5, top + ROW - 16], fill=accent)
        d.text((PAD + 22, top + 10), name, font=big, fill=PAPER)
        if name in CHAPTER:
            nx = PAD + 22 + d.textlength(name, font=big) + 18
            d.text((nx, top + 22), 'Chapter %d' % CHAPTER[name], font=mid, fill=GOLD)
        d.text((PAD + 24, top + 54), p['area'] + ' / ' + p['resonance'], font=mid, fill=PAPER_DIM)
        # the palette: hair ramp, a gap, outfit ramp, a gap, the accent (bigger), hex under the accent
        x = PAL_X
        cols = [('hair', p['hair']), ('outfit', p['outfit'])]
        for label, ramp in cols:
            x0 = x
            for c in ramp:
                d.rectangle([x, top + 22, x + SWATCH - 1, top + 22 + SWATCH - 1], fill=hx(c))
                x += SWATCH
            # a thin frame, so swatches as dark as the page still show
            d.rectangle([x0 - 1, top + 21, x, top + 22 + SWATCH], outline=(70, 66, 58))
            d.text((x0, top + 56), label, font=small, fill=PAPER_DIM)
            x += 16
        x = ACCENT_X
        d.rectangle([x, top + 16, x + 42 - 1, top + 16 + 42 - 1], fill=accent)
        d.rectangle([x - 1, top + 15, x + 42, top + 58], outline=(70, 66, 58))
        d.text((x + 52, top + 22), '#' + p['accent'].upper(), font=mid, fill=PAPER)
        d.text((x + 52, top + 54), ' + '.join(p.get('sources') or p.get('source') or []), font=small, fill=PAPER_DIM)
    # the bottom edge, like banner.png
    d.rectangle([0, h - 4, W, h], fill=GOLD_DIM)
    im.save(OUT)
    print('wrote', OUT)


if __name__ == '__main__':
    main()
