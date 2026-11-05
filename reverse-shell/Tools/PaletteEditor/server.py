"""The palette editor: a local web page for choosing Lospec palettes per area (the world), per character (their sprite,
portrait and eyes) and for the cards (from the character's and their home area's colours together).

    py -3.9 Tools/PaletteEditor/server.py [--port 8766] [--no-browser]      (or double-click design/palette_editor.bat)

Serves index.html on http://127.0.0.1:8766 (this computer only). Lospec is reached through this server (no CORS in the
page) and every answer is cached in Tools/ChibiForge/palettes/lospec_cache/. Save:
  1. backs up palettes.json to design/backups/ (the last 30 are kept);
  2. writes Tools/ChibiForge/palettes/palettes.json + the .hex files, and Resources/Palettes/WorldPalettes.json (the game);
  3. sets each Char_<name>.asset colour (the card swirl) and soulGlow (the card glow);
  4. rebuilds: a3_model.py (sprites, portraits), card_forge.py (cards), characters_banner.py (the README banner).
Guide: design/PALETTE_EDITOR_GUIDE.md. Needs Pillow for the rebuild (py -3.9).
"""
import glob
import json
import os
import re
import shutil
import subprocess
import sys
import time
import urllib.parse
import urllib.request
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(HERE, '..', '..'))      # reverse-shell/
REPO = os.path.dirname(PROJECT)
CHIBI = os.path.join(PROJECT, 'Tools', 'ChibiForge')
sys.path.insert(0, CHIBI)

import palette_data  # noqa: E402

CACHE = os.path.join(CHIBI, 'palettes', 'lospec_cache')
BACKUPS = os.path.join(REPO, 'design', 'backups')
ASSETS = os.path.join(PROJECT, 'Assets', '_Project')
LOSPEC = 'https://lospec.com'
UA = {'User-Agent': 'reverse-shell-palette-editor'}

# Files the page may show (read only): the study portrait, the card swirl and frame, screenshots for area previews.
PICTURES = {
    'font': os.path.join(ASSETS, 'Resources', 'Fonts', 'monogram-extended.ttf'),
    'swirl': os.path.join(ASSETS, 'Resources', 'Cards', 'CardSwirl.png'),
    'frame': os.path.join(ASSETS, 'Resources', 'Cards', 'CardFrame.png'),
}
SHOTS = {   # area id -> a screenshot of it (README images) for the world palette preview
    'city': 'story-awakening.png',
    'intro_forest': '02-overgrown-forest.png',
    'office': 'story-office.png',
}


def chapter_of(aid):
    """(order, label) of an area's chapter, like the Level Designer's sections."""
    if aid in ('city', 'tunnel', 'chamber'):
        return 1, 'Chapter 1 · Ruined City'
    if aid == 'office':
        return 1, 'Chapter 1 · Office'
    if aid.startswith('intro_forest'):
        return 1, 'Chapter 1 · Forest (intro)'
    for i, (prefix, label) in enumerate((('intro_conservatory', 'Chapter 2 · Conservatory'), ('intro_frozen', 'Chapter 3 · Frozen'),
                                         ('intro_cinderworks', 'Chapter 4 · Cinderworks'), ('intro_sanctuary', 'Chapter 5 · Sanctuary'))):
        if aid.startswith(prefix):
            return i + 2, label
    return 9, 'Sandbox'


def areas():
    out = []
    for f in glob.glob(os.path.join(ASSETS, 'Resources', 'Areas', 'Area_*.asset')):
        text = open(f, encoding='utf-8').read()
        m = re.search(r'^  id: (\S+)', text, re.M)
        if not m:
            continue
        aid = m.group(1)
        name = re.search(r'^  displayName: (.*)$', text, re.M)
        order, chapter = chapter_of(aid)
        out.append({'id': aid, 'name': (name.group(1).strip() if name else aid).strip('"'), 'chapter': chapter,
                    'order': order, 'shot': aid in SHOTS})
    return sorted(out, key=lambda a: (a['order'], a['chapter'], a['id']))


def fetch_json(url, cache_name, max_age=86400 * 3):
    os.makedirs(CACHE, exist_ok=True)
    path = os.path.join(CACHE, re.sub(r'[^A-Za-z0-9_.-]+', '_', cache_name) + '.json')
    if os.path.exists(path) and time.time() - os.path.getmtime(path) < max_age:
        return json.load(open(path, encoding='utf-8'))
    data = json.loads(urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=25).read())
    json.dump(data, open(path, 'w', encoding='utf-8'))
    return data


def search(q):
    params = {
        'colorNumberFilterType': q.get('filter', 'any'),
        'colorNumber': q.get('count', '8'),
        'page': q.get('page', '0'),
        'tag': q.get('tag', ''),
        'sortingType': q.get('sort', 'default'),
    }
    url = LOSPEC + '/palette-list/load?' + urllib.parse.urlencode(params)
    data = fetch_json(url, 'search_' + urllib.parse.urlencode(params))
    out = []
    for p in data.get('palettes', []):
        out.append({'slug': p.get('slug'), 'title': p.get('title'), 'colors': p.get('colors', []),
                    'author': (p.get('user') or {}).get('name', ''), 'tags': p.get('tags', [])})
    return {'palettes': out, 'total': data.get('totalCount', 0)}


def palette(slug):
    slug = re.sub(r'[^a-z0-9-]', '', slug.lower())
    data = fetch_json(LOSPEC + '/palette-list/%s.json' % slug, 'palette_' + slug, max_age=86400 * 30)
    return {'slug': slug, 'title': data.get('name', slug), 'author': data.get('author', ''),
            'colors': [c.lower() for c in data.get('colors', [])]}


def _rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def _dist(a, b):
    return (a[0] - b[0]) ** 2 * 2 + (a[1] - b[1]) ** 2 * 4 + (a[2] - b[2]) ** 2 * 3


def nearest(hexcode, pages=30):
    """The Lospec palettes (the most downloaded `pages` x 10, plus any cached) with a colour closest to `hexcode`."""
    target = _rgb(hexcode.lstrip('#').lower())
    seen = {}
    for page in range(pages):
        try:
            for p in search({'sort': 'downloads', 'page': str(page)})['palettes']:
                seen[p['slug']] = p
        except Exception:
            break
    for f in glob.glob(os.path.join(CACHE, 'palette_*.json')):
        d = json.load(open(f, encoding='utf-8'))
        slug = os.path.basename(f)[len('palette_'):-5]
        seen.setdefault(slug, {'slug': slug, 'title': d.get('name', slug), 'colors': d.get('colors', [])})
    rows = []
    for p in seen.values():
        if not p['colors']:
            continue
        best = min(p['colors'], key=lambda c: _dist(_rgb(c), target))
        rows.append({'slug': p['slug'], 'title': p['title'], 'color': best.lower(), 'distance': _dist(_rgb(best), target),
                     'colors': p['colors']})
    rows.sort(key=lambda r: r['distance'])
    return rows[:20]


_strip = {}


def study_strip():
    """The study's idle loop as drawn (A2's own colours, so the page can recolour it): its frames side by side."""
    if 'png' not in _strip:
        import io
        import a3_study
        from PIL import Image
        frames = a3_study.frames()
        w, h = frames[0].size
        sheet = Image.new('RGBA', (w * len(frames), h), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            sheet.paste(f, (i * w, 0))
        buf = io.BytesIO()
        sheet.save(buf, 'PNG')
        _strip['png'] = buf.getvalue()
    return _strip['png']


def set_char_colours(data):
    """Char_<name>.asset: color = the card swirl, soulGlow = the card glow."""
    for name, c in data['characters'].items():
        path = os.path.join(ASSETS, 'Content', 'Characters', 'Char_%s.asset' % name)
        if not os.path.exists(path):
            continue
        text = open(path, encoding='utf-8').read()
        r, g, b = (v / 255.0 for v in _rgb(c['card']['swirl']))
        text = re.sub(r'^  color: \{r: [^}]*\}', '  color: {r: %.6f, g: %.6f, b: %.6f, a: 1}' % (r, g, b), text, 1, re.M)
        gr, gg, gb = (v / 255.0 for v in _rgb(c['card']['glow']))
        glow = '  soulGlow: {r: %.6f, g: %.6f, b: %.6f, a: 1}' % (gr, gg, gb)
        if re.search(r'^  soulGlow: ', text, re.M):
            text = re.sub(r'^  soulGlow: \{[^}]*\}', glow, text, 1, re.M)
        else:   # a field Unity hasn't written yet: add it (field order doesn't matter in the YAML)
            text = text.rstrip('\n') + '\n' + glow + '\n'
        open(path, 'w', encoding='utf-8', newline='').write(text)


def save(data):
    os.makedirs(BACKUPS, exist_ok=True)
    stamp = time.strftime('%Y%m%d-%H%M%S')
    shutil.copy(palette_data.PATH, os.path.join(BACKUPS, 'palettes-%s.json' % stamp))
    old = sorted(glob.glob(os.path.join(BACKUPS, 'palettes-*.json')))
    for f in old[:-30]:
        os.remove(f)
    palette_data.save(data)
    set_char_colours(data)
    log = []
    py = [sys.executable]
    for script in (os.path.join(CHIBI, 'a3_model.py'), os.path.join(PROJECT, 'Tools', 'CardForge', 'card_forge.py'),
                   os.path.join(CHIBI, 'palettes', 'characters_banner.py')):
        r = subprocess.run(py + [script], cwd=PROJECT, capture_output=True, text=True)
        log.append('$ %s\n%s%s' % (os.path.relpath(script, PROJECT), r.stdout[-1500:], r.stderr[-1500:]))
        if r.returncode != 0:
            log.append('FAILED (exit %d)' % r.returncode)
            break
    return '\n'.join(log)


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def send(self, code, body, ctype='application/json'):
        if isinstance(body, (dict, list)):
            body = json.dumps(body).encode('utf-8')
        elif isinstance(body, str):
            body = body.encode('utf-8')
        self.send_response(code)
        self.send_header('Content-Type', ctype)
        self.send_header('Content-Length', str(len(body)))
        self.send_header('Cache-Control', 'no-store')
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        u = urllib.parse.urlparse(self.path)
        q = {k: v[0] for k, v in urllib.parse.parse_qs(u.query).items()}
        try:
            if u.path in ('/', '/index.html'):
                return self.send(200, open(os.path.join(HERE, 'index.html'), 'rb').read(), 'text/html; charset=utf-8')
            if u.path == '/api/data':
                import a3_portrait
                import a3_study
                return self.send(200, {
                    'palettes': palette_data.load(), 'areas': areas(),
                    'study': {'hair': ['%02x%02x%02x' % c for c in a3_portrait.STUDY_HAIR],
                              'iris': ['%02x%02x%02x' % c for c in a3_portrait.STUDY_IRIS],
                              'frames': a3_study.FRAMES, 'fps': a3_study.FPS, 'cell': a3_portrait.CELL,
                              'crop': a3_portrait.CROP, 'w': 64, 'h': 84,
                              'offset': [a3_portrait.CELL // 2 - a3_portrait.FOOT[0],
                                         a3_portrait.CELL - a3_portrait.FEET_ROW - a3_portrait.FOOT[1]]},
                })
            if u.path == '/api/lospec':
                return self.send(200, search(q))
            if u.path == '/api/palette':
                return self.send(200, palette(q.get('slug', '')))
            if u.path == '/api/nearest':
                return self.send(200, nearest(q.get('hex', '6495ed'), int(q.get('pages', '30'))))
            if u.path == '/picture/study':
                return self.send(200, study_strip(), 'image/png')
            if u.path.startswith('/picture/'):
                key = u.path[len('/picture/'):]
                path = PICTURES.get(key)
                if key.startswith('shot_'):
                    shot = SHOTS.get(key[len('shot_'):])
                    path = os.path.join(REPO, '.github', 'readme', shot) if shot else None
                if path and os.path.exists(path):
                    ctype = 'font/ttf' if path.endswith('.ttf') else 'image/png'
                    return self.send(200, open(path, 'rb').read(), ctype)
            return self.send(404, {'error': 'not found'})
        except Exception as e:
            return self.send(500, {'error': str(e)})

    def do_POST(self):
        if self.path != '/api/save':
            return self.send(404, {'error': 'not found'})
        try:
            body = json.loads(self.rfile.read(int(self.headers.get('Content-Length', '0'))))
            return self.send(200, {'log': save(body)})
        except Exception as e:
            return self.send(500, {'error': str(e)})


def main():
    port = 8766
    if '--port' in sys.argv:
        port = int(sys.argv[sys.argv.index('--port') + 1])
    server = ThreadingHTTPServer(('127.0.0.1', port), Handler)
    url = 'http://127.0.0.1:%d/' % port
    print('Palette editor on', url, '(Ctrl+C to stop)')
    if '--no-browser' not in sys.argv:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == '__main__':
    main()
