"""
The party's and the world's palettes: Tools/ChibiForge/palettes/palettes.json, edited by the palette editor
(Tools/PaletteEditor) and read by every art tool (alt_recolour, a3_portrait, card_forge, characters_banner).

    {
      "characters": { "<name>": { "home": area id, "area": shown name, "resonance": ..., "sources": [Lospec slugs],
                                   "hair": [dark..light], "eyes": [dark, light], "outfit": [dark..light],
                                   "accent": hex, "card": {"swirl": hex, "glow": hex} } },
      "areas": { "<area id>": { "source": Lospec slug or "", "colors": [hex...], "dither": 0..1, "enabled": bool } }
    }
Colours are lowercase hex without '#'. load() upgrades the old flat file (characters only) in memory.
The game reads the world part from Assets/_Project/Resources/Palettes/WorldPalettes.json (write_world()).
"""
import json
import os

HERE = os.path.dirname(os.path.abspath(__file__))
PATH = os.path.join(HERE, 'palettes', 'palettes.json')
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
WORLD_JSON = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Palettes', 'WorldPalettes.json')

# Home areas (bible: each resonance's region) and the eyes each character had before the editor existed.
HOMES = {'A3': 'city', 'Erin': 'city', 'Lotus': 'intro_conservatory', 'Ashe': 'intro_frozen', 'Zuko': 'intro_cinderworks'}
DEFAULT_EYES = {'A3': ['588dbe', '849be4'], 'Ashe': ['3f48c2', 'a3c0e6'], 'Erin': ['ab5130', 'ff8933'],
                'Lotus': ['1e6f50', '99e65f'], 'Zuko': ['9b1a0a', 'ef692f']}


def area_ids():
    """Every area's id (Resources/Areas/Area_*.asset), sorted."""
    import glob
    import re
    ids = []
    for f in glob.glob(os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Areas', 'Area_*.asset')):
        m = re.search(r'^  id: (\S+)', open(f, encoding='utf-8').read(), re.M)
        if m:
            ids.append(m.group(1))
    return sorted(ids)


def load():
    data = json.load(open(PATH, encoding='utf-8'))
    if 'characters' not in data:   # the first (flat) format: characters only
        data = {'characters': data, 'areas': {}}
    for name, c in data['characters'].items():
        c.setdefault('home', HOMES.get(name, 'city'))
        c.setdefault('sources', c.pop('source', []))
        if 'eyes' not in c:
            c['eyes'] = DEFAULT_EYES.get(name)
        c.setdefault('card', {'swirl': c['accent'], 'glow': c['accent']})
    for aid in area_ids():
        data['areas'].setdefault(aid, {'source': '', 'colors': [], 'dither': 0.0, 'enabled': False})
    return data


def save(data):
    json.dump(data, open(PATH, 'w', encoding='utf-8', newline='\n'), indent=2)
    for name, c in data['characters'].items():   # the .hex files: each character's colours, one per line
        seen = []
        for col in c['hair'] + (c.get('eyes') or []) + c['outfit'] + [c['accent'], c['card']['swirl'], c['card']['glow']]:
            if col not in seen:
                seen.append(col)
        open(os.path.join(HERE, 'palettes', name + '.hex'), 'w', newline='\n').write('\n'.join(seen) + '\n')
    write_world(data)


def write_world(data):
    """The game's copy of the world palettes (WorldPalette.cs): only enabled areas with colours."""
    os.makedirs(os.path.dirname(WORLD_JSON), exist_ok=True)
    areas = []
    for aid, a in sorted(data['areas'].items()):
        if a.get('enabled') and a.get('colors'):
            areas.append({'id': aid, 'source': a.get('source', ''), 'dither': float(a.get('dither', 0)),
                          'colors': a['colors'][:64]})
    json.dump({'areas': areas}, open(WORLD_JSON, 'w', encoding='utf-8', newline='\n'), indent=2)


def character(name):
    return load()['characters'][name]


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


if __name__ == '__main__':
    save(load())   # upgrade the file in place
    print('wrote', PATH, 'and', WORLD_JSON)
