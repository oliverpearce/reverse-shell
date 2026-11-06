"""
Every ally's sprite, dressed from A3's base sheets: A3, Ashe, Erin, Lotus and Zuko are all the same model in their
own clothes (bible: one model for the whole party; the old robot sprites are for enemies only).

    py -3.9 Tools/ChibiForge/alt_recolour.py      (a3_model.py runs it after making the base sheets)

Reads the uncoloured base sheets (Tools/ChibiForge/reference/A3_base/*.png, made by a3_model.py) and writes, for each
entry in LOOKS, Art/Characters/<folder>/<clip>.png: the hair recoloured (or left as it is), the bodysuit in the
character's clothes colour, and its trim and highlights in their accent colour. Skin, outline and the sword stay.
Also writes each PNG's .meta (copied from A3's import settings) the first time, and Content/Animations/Anim_<folder>.asset
(A3's clip list and timings). Existing .meta files are kept, so every GUID and reference stays intact.

The hair and the sword share A3's yellow-grey ramp, so in the clips with a sword (only the overworld's Settings > Sword
clips now) the hair is found by shape: a big yellow-grey patch is blade if it is long and thin, hair otherwise; small
patches join the hair when they touch it.
Needs Pillow (py -3.9).
"""
import colorsys
import os
import re
import uuid

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
CHARS = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')
ANIMS = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Animations')
CELL = 128

# folder: (hair dark, hair light, suit dark, suit light)
BASE = os.path.join(HERE, 'reference', 'A3_base')

# folder: hair (dark, light) or None to keep A3's, clothes (dark, light), accent (the trim, in the character's colour)
# folder: hair (dark, light) ramp or None to keep A3's, clothes ramp, accent (the trim, in the character's colour), all
# from the character's Lospec palette (palettes/palettes.json); every recoloured pixel snaps to that palette's colours.
import json
import palette_data
PALETTES = palette_data.load()['characters']


def _look(name, keep_hair=False):
    pal = PALETTES[name]
    hair = ['#' + c for c in pal['hair']]
    suit = ['#' + c for c in pal['outfit']]
    return dict(hair=None if keep_hair else (hair[0], hair[-1]), suit=(suit[0], suit[-1]), accent='#' + pal['accent'],
                hair_ramp=[hx(c) for c in hair], suit_ramp=[hx(c) for c in suit], trim_dark=hx(hair[1]),
                eyes_ramp=[hx(c) for c in pal['eyes']] if pal.get('eyes') else None)


LOOKS = {
    'A3': None,      # filled below (hx is defined further down)
    'Ashe': None,
    'Erin': None,
    'Lotus': None,
    'Zuko': None,
}
SWORD_CLIPS = {'idle_sword', 'walk_sword'}
# Every clip is now the A2 study's body wearing the old model's hair (a3_oldhair), battle included: the hair is dressed
# with the old model's hair rule (hair_only) and the eyes from the palette (a3_portrait.dress). recolour() and its suit
# rules were for the Last Cloudia sheet's body, which is no longer used (kept for reference).
STUDY_CLIPS = None   # None = every clip

# Frames where the blade touches the hair, so shape alone can't split them: the blade's outline, in the cell of a
# left-facing frame ((clip, frame): [polygon, ...]). Pixels inside stay steel; the rest of the patch counts as hair.
BLADES = {}   # none: battle has no sword now (kept for clips where a blade touches the hair)


def inside(pt, poly):
    x, y = pt[0] + 0.5, pt[1] + 0.5
    hit = False
    for (x0, y0), (x1, y1) in zip(poly, poly[1:] + poly[:1]):
        if (y0 > y) != (y1 > y) and x < x0 + (y - y0) * (x1 - x0) / float(y1 - y0):
            hit = not hit
    return hit

def hx(s):
    s = s.lstrip('#')
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


for _name in LOOKS:
    LOOKS[_name] = _look(_name)   # A3 too: her hair and eyes are AAP-64's greys to white, tinted in game by resonance


def set_tint(anim, name='A3'):
    """Writes the colours the game tints by the protagonist's resonance (SpriteAnimationSet.tintColors: her hair and
    eye ramps, which only her hair and eyes use) into her Anim asset, before its clip list."""
    pal = PALETTES[name]
    cols = []
    for c in pal['hair'] + (pal.get('eyes') or []):
        if c not in cols:
            cols.append(c)
    anim = re.sub(r'  tintColors:\n(  - .*\n)*', '', anim)
    block = '  tintColors:\n' + ''.join('  - %s\n' % c for c in cols)
    return anim.replace('  clips:\n', block + '  clips:\n', 1)


def snap(c, colours):
    """The nearest of the palette's colours (weighted RGB)."""
    return min(colours, key=lambda q: (q[0] - c[0]) ** 2 * 2 + (q[1] - c[1]) ** 2 * 4 + (q[2] - c[2]) ** 2 * 3)


def hsv(c):
    return colorsys.rgb_to_hsv(c[0] / 255.0, c[1] / 255.0, c[2] / 255.0)


def is_hairish(c):
    h, s, v = hsv(c)
    return abs(h * 360 - 60) < 2 and s > 0 and v > 0.1


def is_suit(c):
    h, s, v = hsv(c)
    return s > 0.1 and (abs(h * 360 - 300) < 2 or abs(h * 360 - 340) < 2)


def shade(c, dark, light, v0, v1):
    t = min(1.0, max(0.0, (hsv(c)[2] - v0) / (v1 - v0)))
    return tuple(int(round(dark[i] + (light[i] - dark[i]) * t)) for i in range(3))


def components(cell, mask):
    p = cell.load()
    seen, comps = set(), []
    for y in range(CELL):
        for x in range(CELL):
            if (x, y) in seen or not mask(p[x, y]):
                continue
            stack, pts = [(x, y)], []
            seen.add((x, y))
            while stack:
                a, b = stack.pop()
                pts.append((a, b))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    q = (a + dx, b + dy)
                    if 0 <= q[0] < CELL and 0 <= q[1] < CELL and q not in seen and mask(p[q]):
                        seen.add(q)
                        stack.append(q)
            comps.append(pts)
    return comps


def near(a, b, d):
    sb = set(b)
    for (x, y) in a:
        for dy in range(-d, d + 1):
            for dx in range(-d, d + 1):
                if (x + dx, y + dy) in sb:
                    return True
    return False


def hair_pixels(cell, sword, blades=()):
    p = cell.load()
    core = {(x, y) for y in range(CELL) for x in range(CELL) if any(inside((x, y), b) for b in blades)}
    steel = {(x + dx, y + dy) for (x, y) in core for dx in range(-1, 2) for dy in range(-1, 2)}   # and its edge glints
    hairish = components(cell, lambda c: c[3] > 0 and is_hairish(c))
    if steel:   # split the patches at the marked blade
        seen, hairish = set(), []
        for y in range(CELL):
            for x in range(CELL):
                if (x, y) in seen or (x, y) in steel or not (p[x, y][3] > 0 and is_hairish(p[x, y])):
                    continue
                stack, pts = [(x, y)], []
                seen.add((x, y))
                while stack:
                    a, b = stack.pop()
                    pts.append((a, b))
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        q = (a + dx, b + dy)
                        if (0 <= q[0] < CELL and 0 <= q[1] < CELL and q not in seen and q not in steel
                                and p[q][3] > 0 and is_hairish(p[q])):
                            seen.add(q)
                            stack.append(q)
                hairish.append(pts)
    if not sword or steel:   # no blade, or the blade is marked: every other yellow-grey pixel is hair
        return {pt for comp in hairish for pt in comp}, steel
    # Big patches: a blade is long and thin at any angle (its spread along one axis dwarfs the other); hair is a blob.
    # Small patches join the hair when they touch it (loose strands), and stay steel otherwise (hilt, edge glints).
    def elongation(comp):
        n = float(len(comp))
        mx, my = sum(x for x, _ in comp) / n, sum(y for _, y in comp) / n
        sxx = sum((x - mx) ** 2 for x, _ in comp) / n
        syy = sum((y - my) ** 2 for _, y in comp) / n
        sxy = sum((x - mx) * (y - my) for x, y in comp) / n
        tr, det = sxx + syy, sxx * syy - sxy * sxy
        disc = max(0.0, tr * tr / 4 - det) ** 0.5
        l1, l2 = tr / 2 + disc, max(1e-6, tr / 2 - disc)
        return (l1 / l2) ** 0.5

    big = [c for c in hairish if len(c) >= 20]
    hair = [c for c in big if elongation(c) < 2.6]
    rest = [c for c in hairish if len(c) < 20]
    grew = True
    while grew:
        grew = False
        for c in list(rest):
            if any(near(c, h, 1) for h in hair):
                hair.append(c)
                rest.remove(c)
                grew = True
    return {pt for comp in hair for pt in comp}, steel


def recolour(sheet, look, sword, clip=''):
    hair = look.get('hair')
    hd, hl = (hx(hair[0]), hx(hair[1])) if hair else (None, None)
    sd, sl = hx(look['suit'][0]), hx(look['suit'][1])
    acc = hx(look['accent'])
    acc_dark = look.get('trim_dark') or tuple(int(v * 0.5) for v in acc)
    hair_pal = look.get('hair_ramp') or []
    suit_pal = (look.get('suit_ramp') or []) + [acc]
    trim_pal = [acc_dark, acc]
    out = sheet.copy()
    for row, cy in enumerate(range(0, sheet.height, CELL)):
        mirrored = sheet.height >= 4 * CELL and row in (0, 3)   # up and right are the left-facing frames flipped
        for col, cx in enumerate(range(0, sheet.width, CELL)):
            cell = sheet.crop((cx, cy, cx + CELL, cy + CELL))
            if mirrored:
                cell = cell.transpose(Image.FLIP_LEFT_RIGHT)
            p = cell.load()
            hair_px, steel = hair_pixels(cell, sword, BLADES.get((clip, col), ()))
            done = cell.copy()
            q = done.load()
            for y in range(CELL):
                for x in range(CELL):
                    c = p[x, y]
                    if c[3] == 0:
                        continue
                    if (x, y) in hair_px:
                        if hd is not None:
                            q[x, y] = snap(shade(c, hd, hl, 0.13, 1.0), hair_pal) + (c[3],)
                    elif is_suit(c[:3]) and (x, y) not in steel:
                        h, sat, v = hsv(c[:3])
                        if abs(h * 360 - 340) < 2:
                            # the suit's trim (A3's rose seam): the accent colour
                            q[x, y] = snap(shade(c, acc_dark, acc, 0.3, 0.5), trim_pal) + (c[3],)
                        elif v >= 0.39:
                            # the suit's highlights: half clothes, half accent, so the colour runs through the outfit
                            base = shade(c, sd, sl, 0.13, 0.47)
                            q[x, y] = snap(tuple((base[i] + acc[i]) // 2 for i in range(3)), suit_pal) + (c[3],)
                        else:
                            q[x, y] = snap(shade(c, sd, sl, 0.13, 0.47), suit_pal) + (c[3],)
            if mirrored:
                done = done.transpose(Image.FLIP_LEFT_RIGHT)
            out.paste(done, (cx, cy))
    return out


def hair_only(sheet, look):
    """Only the old model's yellow-grey hair recoloured (recolour()'s hair rule and palette snap); the rest untouched."""
    if look.get('hair') is None:
        return sheet
    hd, hl = hx(look['hair'][0]), hx(look['hair'][1])
    out = sheet.copy()
    p = out.load()
    for y in range(out.height):
        for x in range(out.width):
            c = p[x, y]
            if c[3] and is_hairish(c[:3]):
                p[x, y] = snap(shade(c, hd, hl, 0.13, 1.0), look['hair_ramp']) + (c[3],)
    return out


def guid_for(path):
    rel = os.path.relpath(path, ROOT).replace('\\', '/')
    return uuid.uuid5(uuid.NAMESPACE_URL, 'reverse-shell/' + rel).hex


def meta_guid(path):
    m = re.search(r'^guid: ([0-9a-f]+)', open(path + '.meta', encoding='utf-8').read(), re.M)
    return m.group(1)


def write_meta(template, path):
    """A .meta for a new file (A3's import settings, a GUID from the path). An existing one is kept as it is."""
    if os.path.exists(path + '.meta'):
        return
    meta = open(template, encoding='utf-8').read()
    meta = re.sub(r'^guid: [0-9a-f]+', 'guid: ' + guid_for(path), meta, flags=re.M)
    open(path + '.meta', 'w', encoding='utf-8', newline='\n').write(meta)


# Clips only A3 has (the A2 study portrait, a3_portrait.py): left out of the others' Anim assets.
A3_ONLY = []   # every ally has their own portrait now (a3_portrait.py)


def add_missing_clips(anim, guids):
    """Adds a clip entry to Anim_A3 for every base sheet it doesn't list yet (e.g. new walk styles), with its fps."""
    import a3_walks
    for clip in sorted(guids):
        if re.search(r'^  - name: %s$' % re.escape(clip), anim, re.M):
            continue
        import a3_study
        fps = (a3_walks.fps(clip) if clip.startswith('walk') and clip != 'walk_sword'
               else 5 if clip == 'idle' else 5 if clip.startswith('battle_idle') else 10)
        anim = anim.rstrip('\n') + '\n' + (
            '  - name: %s\n    sheet: {fileID: 2800000, guid: %s, type: 3}\n    fps: %s\n    loop: 1\n'
            '    firstFrame: 0\n    frameCount: 0\n') % (clip, guids[clip], fps)
    return anim


def main():
    clips = sorted(f[:-4] for f in os.listdir(BASE) if f.endswith('.png'))
    a3_dir = os.path.join(CHARS, 'A3')
    anim_src = open(os.path.join(ANIMS, 'Anim_A3.asset'), encoding='utf-8').read()
    a3_guids = {}
    for folder, look in LOOKS.items():
        out_dir = os.path.join(CHARS, folder)
        os.makedirs(out_dir, exist_ok=True)
        write_meta(os.path.join(CHARS, 'Erin.meta' if folder == 'A3' else 'A3.meta'), out_dir)
        for clip in clips:
            sheet = Image.open(os.path.join(BASE, clip + '.png')).convert('RGBA')
            path = os.path.join(out_dir, clip + '.png')
            if STUDY_CLIPS is None or clip in STUDY_CLIPS:
                # the study's body wearing the old model's hair: that hair dressed like the old model's, the eyes
                # from the palette (a3_portrait.dress), nothing else touched
                import a3_portrait
                a3_portrait.dress(hair_only(sheet, look), look).save(path)
            else:
                recolour(sheet, look, clip in SWORD_CLIPS, clip).save(path)
            template = os.path.join(a3_dir, clip + '.png.meta')
            write_meta(template if os.path.exists(template) else os.path.join(a3_dir, 'idle.png.meta'), path)
            if folder == 'A3':
                a3_guids[clip] = meta_guid(path)
        if folder == 'A3':
            anim_src = set_tint(add_missing_clips(anim_src, a3_guids))
            open(os.path.join(ANIMS, 'Anim_A3.asset'), 'w', encoding='utf-8', newline='\n').write(anim_src)
            print('A3', len(clips), 'clips (Anim_A3 is the template the others copy)')
            continue
        # the others: A3's clip list and timings, pointing at their own sheets (not A3's own portrait)
        anim = anim_src.replace('m_Name: Anim_A3', 'm_Name: Anim_' + folder)
        anim = re.sub(r'  tintColors:\n(  - .*\n)*', '', anim)   # only the protagonist is tinted
        anim = re.sub(r'  - name: (%s)\n(    .*\n)+' % '|'.join(A3_ONLY), '', anim)
        for clip in clips:
            if clip in a3_guids:
                anim = anim.replace(a3_guids[clip], meta_guid(os.path.join(out_dir, clip + '.png')))
        anim_path = os.path.join(ANIMS, 'Anim_%s.asset' % folder)
        open(anim_path, 'w', encoding='utf-8', newline='\n').write(anim)
        write_meta(os.path.join(ANIMS, 'Anim_A3.asset.meta'), anim_path)
        print(folder, len(clips), 'clips; Anim_%s guid %s' % (folder, meta_guid(anim_path)))


if __name__ == '__main__':
    main()
