"""
The machines' and NPCs' sprites, from two third-party packs (Settings > Machine Look switches between them):

  default  Art/Characters/<Name>/      "Pixel RPG Prototype Characters" by Snoblin (snoblin.itch.io/pixel-rpg-free-npc)
  alt      Art/Characters/Alt_<Name>/  "Tech Dungeon Roguelite (DEMO)" by Trevor Pupkin (trevor-pupkin.itch.io), the
                                       demo's gunner, recoloured per character

    py -3.9 Tools/ChibiForge/npc_packs.py

Both packs allow use and changes in free and commercial games but not redistribution, so their files (the sources in
Tools/ChibiForge/sources/ and everything this writes) are git-ignored (.github/PRIVATE_ASSETS.txt). They replaced the
Fantasy Knight robots (knight_model.py), whose licence rules out AI creations.

Output is what SampleContentGenerator.AnimSet reads: one sheet per clip, square cells, 4 facing rows (up, left, down,
right; hurt is one row), nearest-neighbour upscaled so they stand about as tall as the old robots. Clips:
  Snoblin       idle 2, walk 4 (+ the standing frame first), slash 6 (built: wind-up, lunge, swipe, recover),
                spellcast 7 (built: the idle with a pulsing glow), hurt 5 (flash, hurt, the death fall)
  Tech Dungeon  idle 3 (idle, talk), walk 4 (+ standing) = run, slash = shoot, spellcast = reload, hurt = death
                (side view only, so up and down use the side frames)
New sheets get a copy of the matching default sheet's .meta (point filter, uncompressed) with a fresh guid. Then run
Tools > Combat > Generate Sample Content in Unity and Tools/AnimCheck (--names).
"""
import colorsys
import os
import re
import shutil
import uuid

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
ART = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')
SNOBLIN = os.path.join(HERE, 'sources', 'Snoblin', 'Characters', 'Prototype_Character')
TECH = os.path.join(HERE, 'sources', 'TechDungeon', 'Players', 'players blue x1.png')

# Who gets which colour. Snoblin has Default, Blue, Green, Red, Yellow; Tech Dungeon's blue gunner is hue-shifted to
# the same family (None = greyed out).
CAST = {
    'RustSentry': ('Red', 0.0),
    'ScrapStalker': ('Green', 0.33),
    'Courier': ('Blue', None),        # the gunner's own blue
    'Drifter': ('Yellow', 0.14),
    'NoodleVendor': ('Default', 'grey'),
}

SNOB_CELL, SNOB_SCALE, SNOB_OUT = 32, 3, 64
TECH_CELL, TECH_SCALE, TECH_OUT = 32, 3, 96
FEET = 2   # px of empty cell under the feet

CREDIT_SNOBLIN = ('"Pixel RPG Prototype Characters" by Snoblin (https://snoblin.itch.io/pixel-rpg-free-npc), {colour} '
                  'variant, laid out by `Tools/ChibiForge/npc_packs.py` (the attack and cast poses are built from its '
                  'frames). Free for free and commercial games and may be edited; it may not be redistributed, so this '
                  'folder is git-ignored.\n')
CREDIT_TECH = ('"Tech Dungeon Roguelite - Asset Pack (DEMO)" by Trevor Pupkin (https://trevor-pupkin.itch.io/tech-dungeon-roguelite), '
               'the gunner, recoloured by `Tools/ChibiForge/npc_packs.py`. Free for free and commercial games and may be '
               'modified; it may not be redistributed, so this folder is git-ignored.\n')


# ------------------------------------------------------------------ helpers

def cells(sheet, cell, row, count, start=0):
    return [sheet.crop(((start + i) * cell, row * cell, (start + i + 1) * cell, (row + 1) * cell)) for i in range(count)]


def flip(frame):
    return frame.transpose(Image.FLIP_LEFT_RIGHT)


def place(frame, scale, out, dx=0, dy=0):
    """Scales a source cell (nearest) and stands it in an out x out cell, feet FEET px above the bottom, centred."""
    big = frame.resize((frame.width * scale, frame.height * scale), Image.NEAREST)
    bb = big.getbbox()
    cell = Image.new('RGBA', (out, out), (0, 0, 0, 0))
    if bb is None:
        return cell
    body = big.crop(bb)
    # keep the source's horizontal offset relative to its cell centre (so poses that lean stay leaning)
    src_centre = big.width / 2.0
    x = int(round(out / 2.0 + (bb[0] - src_centre))) + dx
    y = out - FEET - body.height + dy
    cell.alpha_composite(body, (max(0, min(out - body.width, x)), max(0, y)))
    return cell


def sheet(rows):
    """rows: list of lists of cells (all the same size) -> one sheet."""
    size = rows[0][0].width
    w = max(len(r) for r in rows)
    out = Image.new('RGBA', (w * size, len(rows) * size), (0, 0, 0, 0))
    for y, r in enumerate(rows):
        for x, c in enumerate(r):
            out.alpha_composite(c, (x * size, y * size))
    return out


def tint_white(frame, colour, amount):
    """Blends every opaque pixel toward colour (a flash or a glow)."""
    out = frame.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = (int(r + (colour[0] - r) * amount), int(g + (colour[1] - g) * amount), int(b + (colour[2] - b) * amount), a)
    return out


def glow(frame, colour, strength):
    """A soft 1 px halo round the sprite in colour (strength 0..1), under it."""
    halo = Image.new('RGBA', frame.size, (0, 0, 0, 0))
    src = frame.load()
    hp = halo.load()
    a = int(200 * strength)
    for y in range(frame.height):
        for x in range(frame.width):
            if src[x, y][3]:
                continue
            for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + ox, y + oy
                if 0 <= nx < frame.width and 0 <= ny < frame.height and src[nx, ny][3]:
                    hp[x, y] = colour + (a,)
                    break
    halo.alpha_composite(frame)
    return halo


def swipe(frame, colour, facing_right=True):
    """Draws a short arc of light in front of the sprite (the strike)."""
    out = frame.copy()
    bb = out.getbbox()
    if bb is None:
        return out
    px = out.load()
    cx = bb[2] + 1 if facing_right else bb[0] - 2
    cy = (bb[1] + bb[3]) // 2
    for i, (ox, oy) in enumerate([(0, -4), (1, -3), (2, -2), (2, -1), (3, 0), (2, 1), (2, 2), (1, 3), (0, 4)]):
        x = cx + (ox if facing_right else -ox)
        y = cy + oy
        if 0 <= x < out.width and 0 <= y < out.height:
            px[x, y] = colour + (255,)
    return out


def hue_shift(img, hue):
    """Moves the gunner's blues to another hue (None = unchanged, 'grey' = desaturated); skin and outline stay."""
    if hue is None:
        return img.copy()
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if not a:
                continue
            h, l, s = colorsys.rgb_to_hls(r / 255.0, g / 255.0, b / 255.0)
            if 0.45 < h < 0.75 and s > 0.25:   # the blue / cyan suit and effects
                if hue == 'grey':
                    s *= 0.12
                else:
                    h = hue
                nr, ng, nb = colorsys.hls_to_rgb(h, l, s)
                px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    return out


def write(folder, name, img, meta_from):
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + '.png')
    img.save(path)
    meta = path + '.meta'
    if not os.path.exists(meta) and meta_from and os.path.exists(meta_from):
        text = open(meta_from, encoding='utf-8').read()
        text = re.sub(r'^guid: [0-9a-f]+', 'guid: ' + uuid.uuid4().hex, text, flags=re.M)
        open(meta, 'w', encoding='utf-8').write(text)
    return path


def check_size(img, name):
    if img.width > 2048 or img.height > 2048:
        raise SystemExit(name + ' is over 2048 px')


# ------------------------------------------------------------------ Snoblin (the default look)

def snoblin(colour):
    base = os.path.join(SNOBLIN, colour)
    load = lambda n: Image.open(os.path.join(base, n + '.png')).convert('RGBA')
    idle, walk, hurt, death = load('idle'), load('walk'), load('hurt'), load('death')
    # Source rows: 0 down, 1 side (facing right), 2 up. Output rows: up, left, down, right.
    def dirs(src, count, start=0):
        down, side, up = (cells(src, SNOB_CELL, r, count, start) for r in (0, 1, 2))
        return up, [flip(f) for f in side], down, side

    def out_rows(per_dir, transform=None):
        rows = []
        for d, frames in enumerate(per_dir):
            row = []
            for i, f in enumerate(frames):
                row.append(transform(f, d, i) if transform else place(f, SNOB_SCALE, SNOB_OUT))
            rows.append(row)
        return rows

    clips = {}
    clips['idle'] = sheet(out_rows(dirs(idle, 2)))
    stand = dirs(idle, 1)
    w = dirs(walk, 4)
    clips['walk'] = sheet([[place(stand[d][0], SNOB_SCALE, SNOB_OUT)] + [place(f, SNOB_SCALE, SNOB_OUT) for f in w[d]] for d in range(4)])

    # Slash: stand, wind back, lunge (walk frame) forward, strike with a light swipe, follow through, recover.
    flash = (255, 244, 214)
    rows = []
    for d in range(4):
        # direction of the facing, in output pixels: sideways lunges far; up a little; down not at all (the feet are
        # on the cell's bottom edge)
        fwd = {0: (0, -0.34), 1: (-1, 0), 2: (0, 0), 3: (1, 0)}[d]
        s = stand[d][0]
        def at(f, k):
            return place(f, SNOB_SCALE, SNOB_OUT, dx=int(round(fwd[0] * k)), dy=int(round(fwd[1] * k)))
        strike = at(s, 9)
        if d in (1, 3):
            strike = swipe(strike, flash, facing_right=(d == 3))
        rows.append([at(s, 0), at(s, -3), at(w[d][1], 5), strike, at(w[d][3], 6), at(s, 2)])
    clips['slash'] = sheet(rows)

    # Spellcast: the idle with a glow that swells and fades in the pack colour's light tint.
    gl = (255, 230, 160)
    pulse = [0.0, 0.35, 0.7, 1.0, 0.7, 0.35, 0.0]
    i2 = dirs(idle, 2)
    clips['spellcast'] = sheet([[glow(place(i2[d][k % 2], SNOB_SCALE, SNOB_OUT), gl, p) if p else place(i2[d][k % 2], SNOB_SCALE, SNOB_OUT)
                                 for k, p in enumerate(pulse)] for d in range(4)])

    # Hurt: one row (the battle plays it facing left): the hit flash, the hurt pose, then the fall.
    h = cells(hurt, SNOB_CELL, 1, 2)
    dth = cells(death, SNOB_CELL, 1, 3)
    clips['hurt'] = sheet([[place(flip(f), SNOB_SCALE, SNOB_OUT) for f in h + dth]])
    return clips


# ------------------------------------------------------------------ Tech Dungeon (the alternate look)

# The gunner sheet's rows (8 columns of 32 px; the pack's hand-written labels sit in the cells after the frames).
TECH_ROWS = {'idle': (0, 1), 'talk': (1, 2), 'reload': (2, 5), 'run': (3, 4), 'shoot': (4, 4), 'death': (5, 7)}


def fixed(frame, scale, out):
    """Scales a whole source cell and keeps its layout (the pack draws every frame on one baseline, shadow and all),
    with FEET px of room under it."""
    big = frame.resize((frame.width * scale, frame.height * scale), Image.NEAREST)
    cell = Image.new('RGBA', (out, out), (0, 0, 0, 0))
    top = out - FEET - big.height
    if top < 0:                       # taller than the cell: lose rows off the top (always empty in these packs)
        big = big.crop((0, -top, big.width, big.height))
        top = 0
    cell.alpha_composite(big, ((out - big.width) // 2, top))
    return cell


def tech(hue):
    src = hue_shift(Image.open(TECH).convert('RGBA'), hue)
    # drop the cells with (next to) nothing in them: the death's last fading wisps
    get = lambda k: [f for f in cells(src, TECH_CELL, TECH_ROWS[k][0], TECH_ROWS[k][1])
                     if sum(1 for p in f.getdata() if p[3] > 128) > 12]
    P = lambda f: fixed(f, TECH_SCALE, TECH_OUT)

    def four(frames):
        right = [P(f) for f in frames]
        left = [flip(f) for f in right]
        return [right, left, left, right]   # up, left, down, right (side view only)

    clips = {}
    clips['idle'] = sheet(four(get('idle') + get('talk')))
    stand = get('idle')[0]
    clips['walk'] = sheet(four([stand] + get('run')))
    clips['slash'] = sheet(four(get('shoot') + [stand]))
    clips['spellcast'] = sheet(four(get('reload') + [stand]))
    clips['hurt'] = sheet([[flip(P(f)) for f in get('death')]])
    return clips


# ------------------------------------------------------------------

def main():
    for name, (colour, hue) in CAST.items():
        default = os.path.join(ART, name)
        alt = os.path.join(ART, 'Alt_' + name)
        # The old Fantasy Knight clips that the packs don't replace (sit / read) go.
        for old in ('sit', 'read'):
            for ext in ('.png', '.png.meta'):
                p = os.path.join(default, old + ext)
                if os.path.exists(p):
                    os.remove(p)
        for clip, img in snoblin(colour).items():
            check_size(img, name + '/' + clip)
            write(default, clip, img, os.path.join(ART, 'RustSentry', clip + '.png.meta'))
        for clip, img in tech(hue).items():
            check_size(img, 'Alt_' + name + '/' + clip)
            write(alt, clip, img, os.path.join(default, clip + '.png.meta'))
        open(os.path.join(default, 'CREDITS.md'), 'w', encoding='utf-8').write(
            '# %s: sprite credits\n\n' % name + CREDIT_SNOBLIN.format(colour=colour))
        open(os.path.join(alt, 'CREDITS.md'), 'w', encoding='utf-8').write(
            '# Alt_%s: sprite credits (Settings > Machine Look: Tech Dungeon)\n\n' % name + CREDIT_TECH)
        print('built', name, '(Snoblin', colour + ') and Alt_' + name, '(Tech Dungeon)')
    # Folder metas for the new Alt_ folders (Unity would make them; a fixed guid keeps the repo tidy).
    for name in CAST:
        meta = os.path.join(ART, 'Alt_' + name + '.meta')
        if not os.path.exists(meta):
            open(meta, 'w', encoding='utf-8').write(
                'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n'
                '  assetBundleName: \n  assetBundleVariant: \n' % uuid.uuid4().hex)


if __name__ == '__main__':
    main()
