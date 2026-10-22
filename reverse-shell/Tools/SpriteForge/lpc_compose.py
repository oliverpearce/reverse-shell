"""
SpriteForge / LPC composer: builds 32x48-style (64x64 frame) animated characters from the
Universal LPC Spritesheet Character Generator layers.
https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator

    python Tools/SpriteForge/lpc_compose.py            # build every recipe in RECIPES
    python Tools/SpriteForge/lpc_compose.py Miku Teto  # build only these

For each layer the script:
  1. reads the layer's sheet definition JSON (paths, zPos, recolor slots, credits),
  2. downloads the PNG per animation (cached in Tools/SpriteForge/.lpc_cache/),
  3. recolors it by swapping the material's source ramp for the target ramp
     (a named ULPC palette such as "red", or a custom list of hex colors),
  4. stacks the layers by zPos.

Output goes to Assets/_Project/Art/Characters/<Name>/<animation>.png (64 px frames, 4 direction rows:
up, left, down, right; "hurt" has one row). Each character also gets a CREDITS.md listing the
authors and licenses of every layer used (CC-BY-SA 3.0 / GPL 3.0 and others, so credit is required).

There are no dependencies; it uses pngtool.py next to this file.
"""
import json
import os
import sys
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from pngtool import read_png, write_png  # noqa: E402

REPO_RAW = 'https://raw.githubusercontent.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator/master/'
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
CACHE = os.path.join(HERE, '.lpc_cache')
OUT_ROOT = os.environ.get('SPRITEFORGE_OUT') or os.path.join(ROOT, 'Assets', '_Project', 'Art', 'Characters')

ANIMATIONS = ['idle', 'walk', 'slash', 'spellcast', 'hurt']
FRAME = 64

# Source ramps each material is authored in (from palette_definitions/*/meta_*.json "base").
MATERIAL_BASE = {'body': 'light', 'hair': 'orange', 'cloth': 'white', 'eye': 'blue', 'metal': 'steel'}
PALETTE_FILE = {
    'body': 'palette_definitions/body/body_ulpc.json',
    'hair': 'palette_definitions/hair/hair_ulpc.json',
    'cloth': 'palette_definitions/cloth/cloth_ulpc.json',
    'eye': 'palette_definitions/eye/eye_ulpc.json',
    'metal': 'palette_definitions/metal/metal_ulpc.json',
}

# Custom ramps (dark -> light), 6 colors like ULPC hair/cloth ramps.
CUSTOM = {
    'miku_teal': ['#062126', '#0B4A4F', '#11807E', '#1BB5AB', '#3FD9CB', '#A6F5EC'],
    'teto_red': ['#2A0610', '#5E0B1E', '#9A1430', '#C82340', '#E8465E', '#FF8A9A'],
    'rei_white': ['#2A2833', '#5C5A6E', '#9C9BB0', '#C9C8D8', '#E6E6F0', '#FFFFFF'],
    'yorha_white': ['#3A3C46', '#6E7180', '#A5A8B4', '#CDD0D8', '#E8EAEE', '#FFFFFF'],
    # Machine lifeform plating: sun-baked rust over brown steel, and a moss-grown variant.
    # Party robots V1/V2: clean white and charcoal plating.
    'android_white': ['#2A2C34', '#5A5E6A', '#9599A6', '#C4C7D0', '#E4E6EC', '#FFFFFF'],
    'android_dark': ['#0E0F13', '#1C1E25', '#2D3039', '#41454F', '#5C616C', '#8A909C'],
    'machine_rust':['#22140E', '#45281A', '#6E4226', '#94622F', '#BC8A4A', '#E2C27E'],
    'machine_moss': ['#1A1E14', '#2E3A22', '#4C5A30', '#6E7A3E', '#98A256', '#C8CC88'],
    'neon_orange':['#2A0D04', '#6B2406', '#B44409', '#E86C10', '#FF9A3C', '#FFC98A'],
}

# ---------------------------------------------------------------------------------------------
# Recipes. Each layer is (sheet definition path, {recolor slot: palette name or CUSTOM key}).
# Slots: "material" for single-recolor layers, or "color_1"/"color_2" for multi-recolor ones.
# Body/head color follows the recipe's "skin".
# ---------------------------------------------------------------------------------------------
RECIPES = {
    '2B': {
        'body': 'female', 'skin': 'light', 'eyes': 'blue',
        'layers': [
            ('sheet_definitions/legs/leggings/legs_hose.json', {'material': 'black'}),
            ('sheet_definitions/feet/boots/feet_boots_rim.json', {'material': 'black'}),
            ('sheet_definitions/torso/dresses/dress_slit.json', {'variant': 'black'}),
            # The shades layer isn't drawn: it marks the eye rows for a painted blindfold band (apply_masks).
            ('sheet_definitions/headwear/accessories/glasses/facial_glasses_shades.json', {'variant': 'black', 'mask': 'blindfold'}),
            ('sheet_definitions/hair/bob/hair_bob.json', {'material': 'yorha_white'}),
        ],
    },
    'Miku': {
        'body': 'female', 'skin': 'light', 'eyes': 'blue',
        'layers': [
            ('sheet_definitions/feet/boots/feet_boots_basic.json', {'material': 'black'}),
            ('sheet_definitions/legs/skirts/legs_skirts_plain.json', {'material': 'black'}),
            ('sheet_definitions/torso/shirts/sleeveless/torso_clothes_sleeveless2_vneck.json', {'material': 'slate'}),
            ('sheet_definitions/hair/pigtails/hair_pigtails.json', {'color_1': 'miku_teal', 'color_2': 'red'}),
            # Long strands either side turn the short pigtails into Miku's twin-tails.
            ('sheet_definitions/hair/extensions/bangs/hairext_xlong_bangl.json', {'material': 'miku_teal'}),
            ('sheet_definitions/hair/extensions/bangs/hairext_xlong_bangr.json', {'material': 'miku_teal'}),
        ],
    },
    'Teto': {
        'body': 'female', 'skin': 'light', 'eyes': 'red',
        'layers': [
            ('sheet_definitions/feet/boots/feet_boots_basic.json', {'material': 'black'}),
            ('sheet_definitions/legs/skirts/legs_skirts_plain.json', {'material': 'charcoal'}),
            ('sheet_definitions/torso/shirts/longsleeve/torso_clothes_longsleeve2_buttoned.json', {'material': 'gray'}),
            ('sheet_definitions/hair/pigtails/hair_bunches.json', {'material': 'teto_red'}),
            # Curly extensions read as Teto's drills.
            ('sheet_definitions/hair/extensions/bangs/hairext_xlong_curlyl.json', {'material': 'teto_red'}),
            ('sheet_definitions/hair/extensions/bangs/hairext_xlong_curlyr.json', {'material': 'teto_red'}),
        ],
    },
    'Rei': {
        'body': 'female', 'skin': 'light', 'eyes': 'blue',
        'layers': [
            ('sheet_definitions/feet/boots/feet_boots_basic.json', {'material': 'white'}),
            ('sheet_definitions/legs/pants/legs_pants.json', {'material': 'white'}),
            ('sheet_definitions/torso/shirts/longsleeve/torso_clothes_longsleeve2_vneck.json', {'material': 'black'}),
            ('sheet_definitions/hair/bob/hair_bob_side_part.json', {'material': 'neon_orange'}),
            ('sheet_definitions/hair/extensions/bangs/hairext_braidl.json', {'material': 'neon_orange'}),
        ],
    },
    # Party robots: fully plated LPC bodies, helmets for heads.
    'V1': {
        'body': 'male', 'skin': 'fur_grey', 'eyes': 'blue', 'head': 'sheet_definitions/head/heads/human/heads_human_male.json',
        'layers': [
            ('sheet_definitions/feet/feet_armour.json', {'material': 'android_dark'}),
            ('sheet_definitions/legs/legs_armour.json', {'material': 'android_white'}),
            ('sheet_definitions/torso/armour/torso_armour_plate.json', {'material': 'android_white'}),
            ('sheet_definitions/arms/arms_armour.json', {'material': 'android_dark'}),
            ('sheet_definitions/headwear/helmets/helmets/hat_helmet_xeon.json', {'material': 'android_white'}),
        ],
    },
    'V2': {
        'body': 'female', 'skin': 'fur_grey', 'eyes': 'blue',
        'layers': [
            ('sheet_definitions/feet/feet_armour.json', {'material': 'android_white'}),
            ('sheet_definitions/legs/legs_armour.json', {'material': 'android_dark'}),
            ('sheet_definitions/torso/armour/torso_armour_plate.json', {'material': 'android_dark'}),
            ('sheet_definitions/arms/arms_armour.json', {'material': 'android_white'}),
            ('sheet_definitions/headwear/helmets/helmets/hat_helmet_armet_simple.json', {'material': 'android_white'}),
        ],
    },
    # Machine lifeforms: armored LPC bodies recolored as rusted plating. Helmets hide the faces.
    'RustSentry': {
        'body': 'male', 'skin': 'bronze', 'eyes': 'red', 'head': 'sheet_definitions/head/heads/human/heads_human_male.json',
        'layers': [
            ('sheet_definitions/feet/feet_armour.json', {'material': 'machine_rust'}),
            ('sheet_definitions/legs/legs_armour.json', {'material': 'machine_rust'}),
            ('sheet_definitions/torso/armour/torso_armour_plate.json', {'material': 'machine_rust'}),
            ('sheet_definitions/arms/arms_armour.json', {'material': 'machine_rust'}),
            ('sheet_definitions/headwear/helmets/helmets/hat_helmet_greathelm.json', {'material': 'machine_rust'}),
        ],
    },
    'ScrapStalker': {
        'body': 'male', 'skin': 'bronze', 'eyes': 'red', 'head': 'sheet_definitions/head/heads/human/heads_human_male.json',
        'layers': [
            ('sheet_definitions/feet/feet_armour.json', {'material': 'machine_moss'}),
            ('sheet_definitions/legs/legs_armour.json', {'material': 'machine_rust'}),
            ('sheet_definitions/torso/armour/torso_armour_plate.json', {'material': 'machine_moss'}),
            ('sheet_definitions/arms/arms_armour.json', {'material': 'machine_rust'}),
            ('sheet_definitions/headwear/helmets/helmets/hat_helmet_xeon.json', {'material': 'machine_rust'}),
        ],
    },
    'StaticThug': {
        'body': 'male', 'skin': 'olive', 'eyes': 'purple', 'head': 'sheet_definitions/head/heads/human/heads_human_male.json',
        'layers': [
            ('sheet_definitions/feet/boots/feet_boots_basic.json', {'material': 'black'}),
            ('sheet_definitions/legs/pants/legs_pants.json', {'material': 'navy'}),
            ('sheet_definitions/torso/shirts/longsleeve/torso_clothes_longsleeve.json', {'material': 'black'}),
            ('sheet_definitions/torso/jacket/torso_jacket_collared.json', {'variant': 'charcoal'}),
            ('sheet_definitions/hair/spiky/hair_spiked.json', {'material': 'pink'}),
        ],
    },
    'StaticEnforcer': {
        'body': 'male', 'skin': 'bronze', 'eyes': 'red', 'head': 'sheet_definitions/head/heads/human/heads_human_male.json',
        'layers': [
            ('sheet_definitions/feet/boots/feet_boots_basic.json', {'material': 'black'}),
            ('sheet_definitions/legs/pants/legs_formal.json', {'material': 'black'}),
            ('sheet_definitions/torso/shirts/longsleeve/torso_clothes_longsleeve_formal.json', {'variant': 'white'}),
            ('sheet_definitions/torso/jacket/torso_jacket_collared.json', {'variant': 'black'}),
            ('sheet_definitions/hair/bald/hair_buzzcut.json', {'material': 'gray'}),
        ],
    },
    'NoodleVendor': {
        'body': 'female', 'skin': 'amber', 'eyes': 'brown',
        'layers': [
            ('sheet_definitions/feet/shoes/feet_shoes_basic.json', {'material': 'brown'}),
            ('sheet_definitions/legs/pants/legs_pants.json', {'material': 'bluegray'}),
            ('sheet_definitions/torso/shirts/longsleeve/torso_clothes_longsleeve.json', {'material': 'white'}),
            ('sheet_definitions/torso/aprons/torso_aprons_apron_half.json', {'material': 'maroon'}),
            ('sheet_definitions/hair/bob/hair_bob.json', {'material': 'black'}),
        ],
    },
    'Courier': {
        'body': 'male', 'skin': 'light', 'eyes': 'green', 'head': 'sheet_definitions/head/heads/human/heads_human_male.json',
        'layers': [
            ('sheet_definitions/feet/boots/feet_boots_basic.json', {'material': 'teal'}),
            ('sheet_definitions/legs/pants/legs_pants.json', {'material': 'black'}),
            ('sheet_definitions/torso/shirts/longsleeve/torso_clothes_longsleeve.json', {'material': 'white'}),
            ('sheet_definitions/torso/jacket/torso_jacket_collared.json', {'variant': 'teal'}),
            ('sheet_definitions/hair/braids/hair_ponytail.json', {'material': 'violet'}),
        ],
    },
    'Drifter': {
        'body': 'male', 'skin': 'taupe', 'eyes': 'gray', 'head': 'sheet_definitions/head/heads/human/heads_human_male.json',
        'layers': [
            ('sheet_definitions/feet/boots/feet_boots_basic.json', {'material': 'brown'}),
            ('sheet_definitions/legs/pants/legs_pants.json', {'material': 'gray'}),
            ('sheet_definitions/torso/shirts/longsleeve/torso_clothes_longsleeve.json', {'material': 'gray'}),
            ('sheet_definitions/torso/jacket/torso_jacket_trench.json', {'variant': 'dark gray'}),
            ('sheet_definitions/hair/short/hair_messy1.json', {'material': 'dark_gray'}),
        ],
    },
}

BODY_DEF = 'sheet_definitions/body/body.json'
HEAD_DEF = 'sheet_definitions/head/heads/human/heads_human_female.json'


# ---------------------------------------------------------------------------------------------
def fetch(path, binary=False):
    local = os.path.join(CACHE, path.replace('/', os.sep))
    if not os.path.exists(local):
        os.makedirs(os.path.dirname(local), exist_ok=True)
        try:
            with urllib.request.urlopen(REPO_RAW + path, timeout=60) as r:
                data = r.read()
        except Exception as e:  # missing animation for a layer, etc.
            open(local + '.missing', 'w').close()
            raise FileNotFoundError(path) from e
        open(local, 'wb').write(data)
    return local if binary else json.load(open(local, encoding='utf-8'))


def exists_missing(path):
    return os.path.exists(os.path.join(CACHE, path.replace('/', os.sep)) + '.missing')


_palettes = {}


def ramp(material, name):
    if name in CUSTOM:
        return [hex_rgb(c) for c in CUSTOM[name]]
    if material not in _palettes:
        _palettes[material] = fetch(PALETTE_FILE[material])
    pal = _palettes[material]
    if name not in pal:
        raise KeyError('palette %r not in %s palettes: %s' % (name, material, ', '.join(pal)))
    return [hex_rgb(c) for c in pal[name]]


def hex_rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def color_map(source, target):
    return {s: t for s, t in zip(source, target)}


def recolor(px, mapping):
    if not mapping:
        return px
    out = []
    for row in px:
        new = []
        for p in row:
            if p[3] == 0:
                new.append(p)
                continue
            rgb = p[:3]
            hit = mapping.get(rgb)
            if hit is None:
                for s, t in mapping.items():  # tolerance +-1 like the generator
                    if abs(s[0] - rgb[0]) <= 1 and abs(s[1] - rgb[1]) <= 1 and abs(s[2] - rgb[2]) <= 1:
                        hit = t
                        break
            new.append(hit + (p[3],) if hit else p)
        out.append(new)
    return out


def slot_mappings(definition, choices, skin, eyes):
    """Builds {rgb: rgb} for this layer from its recolor slots and the recipe's choices."""
    rec = definition.get('recolors') or {}
    slots = {'material': rec} if 'material' in rec else {k: v for k, v in rec.items() if k.startswith('color_')}
    mapping = {}
    for slot, spec in slots.items():
        material = spec.get('material')
        if material not in MATERIAL_BASE:
            continue
        if material == 'body':
            target_name = skin
        elif material == 'eye':
            target_name = eyes
        else:
            target_name = choices.get(slot) or choices.get('material')
        if not target_name:
            continue
        source = [hex_rgb(c) for c in spec['source']] if spec.get('source') else ramp(material, MATERIAL_BASE[material])
        mapping.update(color_map(source, ramp(material, target_name)))
    return mapping


def layer_paths(definition, body_type):
    out = []
    for key, layer in definition.items():
        if key.startswith('layer_') and isinstance(layer, dict) and body_type in layer:
            out.append((layer.get('zPos', 0), layer[body_type]))
    return out


def layer_file(path, anim, variant):
    if variant:
        return 'spritesheets/' + path + anim + '/' + variant.replace(' ', '_') + '.png'
    return 'spritesheets/' + path + anim + '.png'


def load_layer(path, anim, variant):
    src = layer_file(path, anim, variant)
    if exists_missing(src):
        return None
    try:
        return read_png(fetch(src, binary=True))[2]
    except FileNotFoundError:
        return None


def idle_from_walk(path, variant, idle_frames=2):
    """Builds a 2-frame idle sheet from the standing frame (column 0) of the walk sheet."""
    walk = load_layer(path, 'walk', variant)
    if walk is None:
        return None
    rows = len(walk)
    return [walk[y][0:FRAME] * idle_frames for y in range(rows)]


def compose(name, recipe):
    body_type = recipe['body']
    layer_specs = [(BODY_DEF, {}), (recipe.get('head', HEAD_DEF), {})] + list(recipe['layers'])
    resolved, credits = [], []
    for def_path, choices in layer_specs:
        d = fetch(def_path)
        mapping = slot_mappings(d, choices, recipe['skin'], recipe['eyes'])
        paths = layer_paths(d, body_type)
        if not paths:
            print('  ! %s has no "%s" variant - layer skipped' % (def_path, body_type))
        variant = None
        if d.get('variants'):
            # Older layers ship pre-colored files (<anim>/<variant>.png) instead of a recolorable source.
            variant = choices.get('variant') or choices.get('material') or d['variants'][0]
            if variant not in d['variants']:
                raise KeyError('%s: variant %r not in %s' % (def_path, variant, d['variants']))
            mapping = {}
        for z, path in paths:
            resolved.append((z, path, mapping, variant, choices.get('mask')))
        for c in d.get('credits', []):
            credits.append(c)

    resolved.sort(key=lambda r: r[0])
    out_dir = os.path.join(OUT_ROOT, name)
    os.makedirs(out_dir, exist_ok=True)
    for anim in ANIMATIONS:
        canvas = None
        masks = {}
        for z, path, mapping, variant, mask in resolved:
            px = load_layer(path, anim, variant)
            if px is None and anim == 'idle':
                px = idle_from_walk(path, variant)  # many clothing layers have no idle sheet
            if px is None:
                continue
            h, w = len(px), len(px[0])
            px = recolor(px, mapping)
            if canvas is None:
                canvas = [[(0, 0, 0, 0)] * w for _ in range(h)]
                cw, ch = w, h
            if (w, h) != (cw, ch):
                continue
            if mask:  # not drawn: only marks where an effect goes (see apply_masks)
                masks[mask] = px
                continue
            for y in range(h):
                crow, prow = canvas[y], px[y]
                for x in range(w):
                    p = prow[x]
                    if p[3] == 0:
                        continue
                    if p[3] == 255 or crow[x][3] == 0:
                        crow[x] = p
                    else:  # alpha blend
                        a = p[3] / 255.0
                        q = crow[x]
                        crow[x] = (int(p[0] * a + q[0] * (1 - a)), int(p[1] * a + q[1] * (1 - a)), int(p[2] * a + q[2] * (1 - a)), max(p[3], q[3]))
        if canvas is None:
            print('  ! no layers for', anim)
            continue
        apply_masks(canvas, masks)
        write_png(os.path.join(out_dir, anim + '.png'), cw, ch, canvas)
    write_credits(name, out_dir, credits)
    print('built', name, '->', os.path.relpath(out_dir, ROOT))


def apply_masks(canvas, masks):
    """Post effects driven by mask layers. 'blindfold': a black band over the eyes, using a glasses layer
    to find the eye rows in every frame. Back-facing frames (row 0, walking away from the camera) get no
    band: from behind the hair covers it."""
    mask = masks.get('blindfold')
    if mask is None:
        return
    h, w = len(canvas), len(canvas[0])
    rows, cols = h // FRAME, w // FRAME
    band, edge = (18, 18, 22, 255), (62, 62, 72, 255)
    for r in range(rows):
        if r == 0 and rows >= 3:
            continue
        source_r = r
        for c in range(cols):
            spans = {}
            for fy in range(FRAME):
                xs = [fx for fx in range(FRAME) if mask[source_r * FRAME + fy][c * FRAME + fx][3] > 0]
                if xs:
                    spans[fy] = (min(xs) - 1, max(xs) + 1)
            if not spans:
                continue
            top = min(spans)
            for fy, (x0, x1) in spans.items():
                y = r * FRAME + fy
                for fx in range(max(0, x0), min(FRAME, x1 + 1)):
                    x = c * FRAME + fx
                    if canvas[y][x][3] > 0:
                        canvas[y][x] = edge if fy == top else band


def write_credits(name, out_dir, credits):
    seen, lines = set(), []
    for c in credits:
        key = (c.get('file'), tuple(c.get('authors', [])))
        if key in seen:
            continue
        seen.add(key)
        lines.append('| `%s` | %s | %s | %s |' % (
            c.get('file', ''), ', '.join(c.get('authors', [])), ', '.join(c.get('licenses', [])),
            ' '.join('<%s>' % u for u in c.get('urls', []))))
    text = ('# %s: sprite credits\n\n'
            'Composed by `Tools/SpriteForge/lpc_compose.py` from the Universal LPC Spritesheet Character Generator\n'
            '(https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator).\n'
            'These sprites are a derivative work. Credit every author below, and keep the sprites under one of the listed\n'
            'licenses (CC-BY-SA 3.0 is share-alike).\n\n'
            '| Layer | Authors | Licenses | Sources |\n|---|---|---|---|\n' % name) + '\n'.join(lines) + '\n'
    open(os.path.join(out_dir, 'CREDITS.md'), 'w', encoding='utf-8').write(text)


if __name__ == '__main__':
    names = sys.argv[1:] or list(RECIPES)
    for n in names:
        compose(n, RECIPES[n])
