"""
The store page's banner, cover and the README's social preview, built from the shipped sprites and the screenshot
run's PNGs (itch/screenshots/new, made by the development player's -shots run, Game.World.ShotTour).

    py -3.9 Tools/ItchArt/itch_art.py            -> previews in itch/art_preview/ (nothing replaced)
    py -3.9 Tools/ItchArt/itch_art.py --write    -> also writes the real files (the old ones go to itch/art_old/)
    add --run run10 to pick the screenshot run (default run10)

Outputs:
  - banner 960x240: an ink panel on the left with the title, a gold rule and the tagline; on the right a darkened crop of
    the overgrown forest with A3, Erin, Ashe, Lotus and Zuko standing in a row (overworld idle, facing down, x3).
    Written to itch/banner-960x240.png and .github/readme/banner.png.
  - cover 1260x1000 (+ a 630x500 copy): the five Soul cards fanned across the middle, A3's portrait in front, the title
    on top, over a vignetted forest shot. itch/cover-*.png.
  - social preview 1280x640: the title and tagline over the battle screenshot (as before). .github/readme/.
Needs Pillow (py -3.9). Rerun whenever the sprites or the screenshots change.
"""
import os
import shutil
import sys

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))      # reverse-shell/
REPO = os.path.dirname(ROOT)
ART = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')
CARDS = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Cards')
FONT = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Fonts', 'monogram-extended.ttf')
# finish_shots.py's cleaned copies (*.clean.png) of a screenshot run: --run <name> (default run10)
_RUN = sys.argv[sys.argv.index('--run') + 1] if '--run' in sys.argv[:-1] else 'run10'
SHOTS = os.path.join(REPO, 'itch', 'screenshots', 'new', _RUN)
BACKGROUND = 'combat-card-merge'   # every piece sits on the battle with the merged card in hand
PREVIEW = os.path.join(REPO, 'itch', 'art_preview')
OLD = os.path.join(REPO, 'itch', 'art_old')

INK = (27, 26, 24)
GOLD_DARK_RULE = (120, 92, 48)
GOLD = (194, 160, 97)
GOLD_BRIGHT = (240, 205, 120)
PAPER = (232, 225, 210)
CREW = ['A3', 'Erin', 'Ashe', 'Lotus', 'Zuko']
TITLE = 'REVERSE SHELL'
TAGLINE = "It's all in the [soul] of the cards!"


def font(px):
    # monogram is a 16 px pixel font: keep sizes on multiples of 16 so it stays crisp.
    return ImageFont.truetype(FONT, px)


def shot(name):
    path = os.path.join(SHOTS, name + '.clean.png')
    if not os.path.exists(path):
        raise SystemExit('Missing screenshot ' + path + ' (run the screenshot player first).')
    return Image.open(path).convert('RGB')


def idle(name, scale):
    """Overworld idle frame 0, facing down (row 2), cropped to the sprite and scaled up with nearest."""
    sheet = Image.open(os.path.join(ART, name, 'idle.png')).convert('RGBA')
    cell = sheet.width // 6
    frame = sheet.crop((0, 2 * cell, cell, 3 * cell))
    frame = frame.crop(frame.getbbox())
    return frame.resize((frame.width * scale, frame.height * scale), Image.NEAREST)


def portrait(scale_h):
    sheet = Image.open(os.path.join(ART, 'A3', 'portrait.png')).convert('RGBA')
    cell = sheet.height // 4   # 4 facing rows of square cells
    frame = sheet.crop((0, 0, cell, cell))
    frame = frame.crop(frame.getbbox())
    k = scale_h / frame.height
    return frame.resize((round(frame.width * k), scale_h), Image.NEAREST)


def card_back_tile(w, h, scale=4, dim=0.55):
    """The card back (Resources/Cards/CardBack.png, original art) tiled across w x h, x scale nearest, dimmed."""
    back = Image.open(os.path.join(CARDS, 'CardBack.png')).convert('RGBA')
    back = back.resize((back.width * scale, back.height * scale), Image.NEAREST)
    tile = Image.new('RGBA', (w, h), INK + (255,))
    for y in range(0, h, back.height):
        for x in range(0, w, back.width):
            tile.alpha_composite(back, (x, y))
    return darken(tile.convert('RGB'), dim)


def soul_card(name, scale):
    card = Image.open(os.path.join(CARDS, 'Ult_Char_' + name + '.png')).convert('RGBA')
    return card.resize((card.width * scale, card.height * scale), Image.NEAREST)


def cover_crop(img, w, h, focus=(0.5, 0.5)):
    """Scales img to cover w x h and crops around focus (fractions)."""
    k = max(w / img.width, h / img.height)
    big = img.resize((round(img.width * k), round(img.height * k)), Image.LANCZOS)
    x = int((big.width - w) * focus[0])
    y = int((big.height - h) * focus[1])
    return big.crop((x, y, x + w, y + h))


def darken(img, amount):
    return ImageEnhance.Brightness(img).enhance(amount)


def shadow_text(draw, xy, text, fnt, fill, anchor='la', offset=3):
    x, y = xy
    draw.text((x, y + offset), text, font=fnt, fill=(10, 9, 8), anchor=anchor)
    draw.text((x, y), text, font=fnt, fill=fill, anchor=anchor)


def soft_shadow(size, box, radius=10, alpha=150):
    mask = Image.new('L', size, 0)
    ImageDraw.Draw(mask).ellipse(box, fill=alpha)
    return mask.filter(ImageFilter.GaussianBlur(radius))


# ------------------------------------------------------------------ Banner

def banner():
    W, H = 960, 240
    # The battle with the merged card on the right: the party, the enemies and the top of the hand (the merged card).
    out = Image.new('RGB', (W, H), INK)
    battle = shot(BACKGROUND).crop((250, 400, 1550, 920)).resize((600, 240), Image.LANCZOS)
    out.paste(darken(battle, 0.85), (W - 600, 0))
    # The title panel on the left: the card back, tiled, fading into the battle.
    panel = card_back_tile(W, H, 2, 0.55)
    fade = Image.new('L', (W, H), 0)
    d = ImageDraw.Draw(fade)
    for x in range(W):
        a = 255 if x < 360 else max(0, 255 - int((x - 360) / 120 * 255))
        d.line([(x, 0), (x, H)], fill=a)
    out = Image.composite(panel, out, fade)
    # A soft ink plate behind the words so they read over the pattern.
    plate = Image.new('L', (W, H), 0)
    ImageDraw.Draw(plate).rounded_rectangle((18, 44, 372, 212), 10, fill=205)
    plate = plate.filter(ImageFilter.GaussianBlur(8))
    out = Image.composite(Image.new('RGB', (W, H), (14, 13, 12)), out, plate)
    d = ImageDraw.Draw(out)
    d.line([(372, 0), (372, H)], fill=GOLD_DARK_RULE, width=1)

    d = ImageDraw.Draw(out)
    shadow_text(d, (34, 58), TITLE, font(64), PAPER)
    d.line([(34, 104), (352, 104)], fill=GOLD, width=2)
    d.rectangle([352, 101, 357, 106], fill=GOLD)
    d.text((34, 116), "It's all in the", font=font(32), fill=GOLD)
    d.text((34, 146), '[soul] of the cards!', font=font(32), fill=GOLD)
    d.text((34, 192), 'CHAPTER ONE DEMO  v1.0', font=font(16), fill=(150, 142, 128))
    d.rectangle([0, H - 4, W, H], fill=GOLD)
    return out


# ------------------------------------------------------------------ Cover

def cover():
    W, H = 1260, 1000
    bg = darken(cover_crop(shot(BACKGROUND), W, H, (0.5, 0.5)), 0.5).convert('RGBA')
    # Vignette.
    vig = Image.new('L', (W, H), 0)
    ImageDraw.Draw(vig).ellipse((-W * 0.25, -H * 0.15, W * 1.25, H * 1.2), fill=255)
    vig = vig.filter(ImageFilter.GaussianBlur(120))
    dark = Image.new('RGBA', (W, H), (8, 7, 6, 255))
    bg = Image.composite(bg, dark, vig)

    # The five Soul cards fanned behind (A3 in the middle, on top).
    order = ['Erin', 'Lotus', 'A3', 'Ashe', 'Zuko']
    angles = [-22, -11, 0, 11, 22]
    offsets = [-330, -170, 0, 170, 330]
    layers = []
    for name, angle, dx in zip(order, angles, offsets):
        card = soul_card(name, 6)
        rot = card.rotate(-angle, resample=Image.NEAREST, expand=True)
        cx, cy = W // 2 + dx, 560 + abs(dx) // 6
        layers.append((0 if name == 'A3' else 1, rot, (cx - rot.width // 2, cy - rot.height // 2)))
    for _, rot, pos in sorted(layers, key=lambda l: -l[0]):
        glow = Image.new('RGBA', bg.size, (0, 0, 0, 0))
        glow.alpha_composite(rot, pos)
        glow = glow.filter(ImageFilter.GaussianBlur(14))
        tint = Image.new('RGBA', bg.size, GOLD + (0,))
        tint.putalpha(glow.getchannel('A').point(lambda a: int(a * 0.55)))
        bg.alpha_composite(tint)
        bg.alpha_composite(rot, pos)

    # A3 in front, feet on the bottom edge.
    hero = portrait(560)
    shadow = soft_shadow(bg.size, (W // 2 - 170, H - 40, W // 2 + 170, H + 10), 18, 200)
    bg.paste((0, 0, 0, 255), (0, 0), shadow)
    bg.alpha_composite(hero, (W // 2 - hero.width // 2, H - hero.height + 10))

    out = bg.convert('RGB')
    d = ImageDraw.Draw(out)
    shadow_text(d, (W // 2, 70), TITLE, font(128), PAPER, anchor='mt', offset=6)
    d.line([(W // 2 - 380, 196), (W // 2 + 380, 196)], fill=GOLD, width=3)
    d.text((W // 2, 214), 'CHAPTER ONE DEMO', font=font(48), fill=GOLD_BRIGHT, anchor='mt')
    # Gold corner brackets, as on the old cover.
    for x, y, sx, sy in [(24, 24, 1, 1), (W - 24, 24, -1, 1), (24, H - 24, 1, -1), (W - 24, H - 24, -1, -1)]:
        d.line([(x, y), (x + 60 * sx, y)], fill=GOLD, width=5)
        d.line([(x, y), (x, y + 60 * sy)], fill=GOLD, width=5)
    return out


# ------------------------------------------------------------------ Social preview

def social():
    W, H = 1280, 640
    base = cover_crop(shot(BACKGROUND), W, H, (0.5, 0.6)).convert('RGBA')
    # The title band: the card back, tiled, fading down into the battle.
    band = card_back_tile(W, 200, 2, 0.5).convert('RGBA')
    mask = Image.new('L', (W, 200), 0)
    d = ImageDraw.Draw(mask)
    for y in range(200):
        d.line([(0, y), (W, y)], fill=int(255 * min(1.0, (1 - y / 200) * 1.6)))
    base.paste(band, (0, 0), mask)
    out = base.convert('RGB')
    d = ImageDraw.Draw(out)
    shadow_text(d, (W // 2, 30), TITLE, font(96), PAPER, anchor='mt', offset=5)
    d.line([(W // 2 - 280, 118), (W // 2 + 280, 118)], fill=GOLD, width=3)
    d.text((W // 2, 128), TAGLINE, font=font(32), fill=GOLD_BRIGHT, anchor='mt')
    return out


def main():
    write = '--write' in sys.argv
    os.makedirs(PREVIEW, exist_ok=True)
    made = {'banner-960x240.png': banner(), 'cover-1260x1000.png': cover(), 'social-preview-1280x640.png': social()}
    made['cover-630x500.png'] = made['cover-1260x1000.png'].resize((630, 500), Image.LANCZOS)
    for name, img in made.items():
        img.save(os.path.join(PREVIEW, name))
        print('preview', os.path.join(PREVIEW, name), img.size)
    if not write:
        return
    targets = {
        'banner-960x240.png': [os.path.join(REPO, 'itch', 'banner-960x240.png'), os.path.join(REPO, '.github', 'readme', 'banner.png')],
        'cover-1260x1000.png': [os.path.join(REPO, 'itch', 'cover-1260x1000.png')],
        'cover-630x500.png': [os.path.join(REPO, 'itch', 'cover-630x500.png')],
        'social-preview-1280x640.png': [os.path.join(REPO, '.github', 'readme', 'social-preview-1280x640.png')],
    }
    os.makedirs(OLD, exist_ok=True)
    for name, paths in targets.items():
        for path in paths:
            if os.path.exists(path):
                keep = os.path.join(OLD, os.path.basename(os.path.dirname(path)) + '_' + os.path.basename(path))
                if not os.path.exists(keep):
                    shutil.copy2(path, keep)
            made[name].save(path)
            print('wrote', path)


if __name__ == '__main__':
    main()
