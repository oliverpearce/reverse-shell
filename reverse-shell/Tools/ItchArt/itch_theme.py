"""
The itch page theme images (itch/theme/), built from the current overgrown forest screenshot so they show the
current sprites.

    py -3.9 Tools/ItchArt/itch_theme.py            -> previews in itch/art_preview/theme/ (nothing replaced)
    py -3.9 Tools/ItchArt/itch_theme.py --write    -> also writes itch/theme/ (the old ones go to itch/art_old/theme/)
    add --run run10 to pick the screenshot run (default run10)

Outputs:
  - theme-embed-bg-960x540.png: the forest around A3 (HUD cropped out), lightly darkened, with the title, a gold rule,
    "CHAPTER ONE DEMO" and the click-to-focus hint.
  - theme-background-2400x1350.png: the same forest, blurred and dimmed towards Ink for the page background.
  - theme-background-tile-1080x840.png: a heavily blurred, mirrored (seamless) tile of it.
Needs Pillow (py -3.9).
"""
import os
import shutil
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import itch_art as ia  # noqa: E402  (shared paths, colours, font, helpers)

THEME = os.path.join(ia.REPO, 'itch', 'theme')
PREVIEW = os.path.join(ia.PREVIEW, 'theme')
OLD = os.path.join(ia.OLD, 'theme')
SOURCE = '02-overgrown-forest'
# 16:9 window of the 1920x1080 shot: below the party HUD (top left), clear of the Q item (bottom left), A3 centred.
CROP = (250, 170, 1690, 980)


def forest():
    return ia.shot(SOURCE).crop(CROP)


def tint(img, amount, colour=ia.INK):
    """Mix img towards colour (0 = unchanged, 1 = solid colour)."""
    return Image.blend(img, Image.new('RGB', img.size, colour), amount)


def vignette(size, strength=200):
    w, h = size
    mask = Image.new('L', (w, h), 0)
    ImageDraw.Draw(mask).ellipse((-w * 0.15, -h * 0.25, w * 1.15, h * 1.25), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(min(w, h) // 6))
    return mask.point(lambda v: 255 - int((255 - v) * strength / 255))


def embed():
    w, h = 960, 540
    img = forest().resize((w, h), Image.LANCZOS)
    img = tint(img, 0.30)
    img = Image.composite(img, Image.new('RGB', (w, h), ia.INK), vignette((w, h), 170))
    # a dim band behind the title so it reads over the trees
    band = Image.new('L', (w, h), 0)
    ImageDraw.Draw(band).rectangle((0, 30, w, 150), fill=130)
    band = band.filter(ImageFilter.GaussianBlur(24))
    img = Image.composite(Image.new('RGB', (w, h), ia.INK), img, band)
    hint_band = Image.new('L', (w, h), 0)
    ImageDraw.Draw(hint_band).rectangle((180, 428, w - 180, 470), fill=150)
    hint_band = hint_band.filter(ImageFilter.GaussianBlur(14))
    img = Image.composite(Image.new('RGB', (w, h), ia.INK), img, hint_band)

    d = ImageDraw.Draw(img)
    cx = w // 2
    ia.shadow_text(d, (cx, 92), ia.TITLE, ia.font(64), ia.PAPER, anchor='ms', offset=3)
    d.line((cx - 200, 106, cx + 200, 106), fill=ia.GOLD, width=2)
    ia.shadow_text(d, (cx, 132), 'CHAPTER ONE DEMO', ia.font(32), ia.GOLD, anchor='ms', offset=2)
    ia.shadow_text(d, (cx, 458), 'click the game once so it hears your keys', ia.font(32), ia.PAPER, anchor='ms',
                   offset=2)
    return img


def background():
    w, h = 2400, 1350
    img = forest().resize((w, h), Image.LANCZOS).filter(ImageFilter.GaussianBlur(14))
    img = tint(img, 0.72)
    return Image.composite(img, Image.new('RGB', (w, h), ia.INK), vignette((w, h), 230))


def tile():
    w, h = 1080, 840
    half = forest().resize((w // 2, h // 2), Image.LANCZOS)
    t = Image.new('RGB', (w, h))
    t.paste(half, (0, 0))
    t.paste(half.transpose(Image.FLIP_LEFT_RIGHT), (w // 2, 0))
    bottom = t.crop((0, 0, w, h // 2)).transpose(Image.FLIP_TOP_BOTTOM)
    t.paste(bottom, (0, h // 2))
    # blur on a wrapped copy so the edges stay seamless
    big = Image.new('RGB', (w * 3, h * 3))
    for i in range(3):
        for j in range(3):
            big.paste(t, (i * w, j * h))
    big = big.filter(ImageFilter.GaussianBlur(40)).crop((w, h, w * 2, h * 2))
    return tint(big, 0.70)


def main():
    write = '--write' in sys.argv
    made = {'theme-embed-bg-960x540.png': embed(),
            'theme-background-2400x1350.png': background(),
            'theme-background-tile-1080x840.png': tile()}
    os.makedirs(PREVIEW, exist_ok=True)
    for name, img in made.items():
        img.save(os.path.join(PREVIEW, name))
        print('preview', os.path.join(PREVIEW, name))
    if write:
        os.makedirs(OLD, exist_ok=True)
        for name, img in made.items():
            dst = os.path.join(THEME, name)
            if os.path.exists(dst):
                shutil.copy2(dst, os.path.join(OLD, name))
            img.save(dst)
            print('wrote', dst)


if __name__ == '__main__':
    main()
