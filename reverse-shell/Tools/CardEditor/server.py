"""The card editor: a local web page for the card sheet (design/CARDS.xlsx).

    py Tools/CardEditor/server.py [--port 8765] [--no-browser]      (or double-click design/card_editor.bat)

Serves index.html on http://127.0.0.1:8765 (this computer only) and reads and writes the sheet with openpyxl, so the
sheet keeps its colours, formulas and dropdowns. Every save:
  1. checks the sheet wasn't changed on disk since the page loaded it (Excel), else asks you to reload;
  2. backs it up to design/backups/ (the last 30 are kept);
  3. writes only the cells you changed (new cards go in the first empty row, with the grey formulas filled in);
  4. runs Tools/CardForge/sheet_to_json.py and shows its problems in the page;
  5. rebuilds the card art atlas (card_forge.py --atlas-only) when an icon changed.
Unity's CardSheetWatcher sees the saved sheet and imports it (Reverse Shell > Convert Card Sheet Now does it by hand).
The Raven icons are served from Art/ThirdParty/RavenFantasyIcons only to this page; nothing is copied anywhere.
Guide: design/CARD_EDITOR_GUIDE.html.
"""
import json
import os
import re
import subprocess
import sys
import threading
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(HERE, '..', '..'))
FORGE = os.path.join(PROJECT, 'Tools', 'CardForge')
sys.path.insert(0, FORGE)

import sheet_io  # noqa: E402
import sheet_to_json  # noqa: E402
from openpyxl.formula.translate import Translator  # noqa: E402

RAVEN_DIR = os.path.join(PROJECT, 'Assets', '_Project', 'Art', 'ThirdParty', 'RavenFantasyIcons', '32x32')
FONT = os.path.join(PROJECT, 'Assets', '_Project', 'Resources', 'Fonts', 'monogram-extended.ttf')
LOCK = threading.Lock()

# The columns the page edits on each tab (the grey, worked-out ones are left to the sheet's formulas).
CARD_COLUMNS = ['ID', 'Name', 'Pool', 'Category', 'Rarity', 'Main', 'Tags', 'Target', 'Base level', 'Attack type', 'Break',
                'Effects', 'Rank II adds', 'Rank III adds', 'Card text', 'Icon (Raven fb#)', 'Pixel effect', 'State',
                'In-game asset', 'Notes']
TABS = {
    # tab: (first header, editable columns or None for every column, key column)
    'Cards': ('ID', CARD_COLUMNS, 'Name'),
    'Enemies': ('Enemy asset', None, 'Enemy asset'),
    'Packs': ('Pack ID', None, 'Pack ID'),
    'Areas': ('Area ID', None, 'Area ID'),
}
NUMBER = (int, float)


def stamp():
    return str(os.stat(sheet_io.SHEET).st_mtime_ns)


def cell_value(v):
    if v is None:
        return ''
    if isinstance(v, float) and v.is_integer():
        return int(v)
    return v if isinstance(v, (int, float, str)) else str(v)


def read_tab(wb, tab):
    first, editable, key = TABS[tab]
    ws = wb[tab]
    row = sheet_io.header_row(ws, first)
    cols = sheet_io.columns(ws, row)
    headers = [h for h in (editable or sorted(cols, key=cols.get)) if h in cols]
    rows, formulas = [], set()
    for r in range(row + 1, ws.max_row + 1):
        if ws.cell(r, cols[key]).value in (None, ''):
            continue
        item = {'_row': r}
        for h in headers:
            v = ws.cell(r, cols[h]).value
            if isinstance(v, str) and v.startswith('='):   # worked out by the sheet: not editable here
                formulas.add(h)
                v = ''
            item[h] = cell_value(v)
        rows.append(item)
    return {'headers': [h for h in headers if h not in formulas], 'rows': rows}


def read_settings(wb):
    """Single-value settings: column A label, column B number, column C note (above the card-count table, and the
    Break & Stagger block)."""
    ws = wb['Settings']
    out = []
    for r in range(1, ws.max_row + 1):
        label, value = ws.cell(r, 1).value, ws.cell(r, 2).value
        if label == 'Break & Stagger':
            out.append({'section': label, 'note': ws.cell(r, 3).value or ''})
            continue
        if isinstance(label, str) and isinstance(value, NUMBER) and not isinstance(value, bool):
            if label in ('Attack', 'Buff', 'Debuff', 'Defense', 'Counter', 'Heal'):
                continue   # the card-count table
            out.append({'label': label, 'value': cell_value(value), 'note': ws.cell(r, 3).value or '', '_row': r,
                        'game': label in [s[0] for s in sheet_to_json.BREAK_SETTINGS]})
    return out


def read_key(wb):
    ws = wb['Effects key']
    rows = []
    for r in range(1, ws.max_row + 1):
        a, b, c = (ws.cell(r, i).value for i in (1, 2, 3))
        if a and a != 'Write' and r > 3:
            rows.append([a or '', b or '', c or ''])
    return rows


def sheet_json():
    wb = sheet_io.load()
    lists = {}
    ws = wb['Settings']
    for name, ref in sheet_io.dropdown_lists(wb).items():
        rng = ref.split('!')[1].replace('$', '')
        lists[name] = [c.value for row in ws[rng] for c in row if c.value not in (None, '')]
    return {
        'stamp': stamp(),
        'sheet': sheet_io.SHEET,
        'cards': read_tab(wb, 'Cards'),
        'enemies': read_tab(wb, 'Enemies'),
        'packs': read_tab(wb, 'Packs'),
        'areas': read_tab(wb, 'Areas'),
        'settings': read_settings(wb),
        'key': read_key(wb),
        'lists': lists,
        'language': {'statuses': sheet_to_json.STATUSES, 'targets': sheet_to_json.EFFECT_TARGETS,
                     'targetModes': list(sheet_to_json.TARGETS), 'weaknesses': sheet_to_json.WEAKNESSES,
                     'attackTypes': sheet_to_json.ATTACK_TYPES, 'maxEffects': sheet_to_json.MAX_EFFECTS},
        'icons': sorted(int(m.group(1)) for m in (re.match(r'^fb(\d+)\.png$', f) for f in os.listdir(RAVEN_DIR)) if m),
    }


def parse(body):
    """Checks one card's effects with the real parser: each rank's effects, or the problem."""
    try:
        base = sheet_to_json.parse_list(body.get('effects'))
        r2 = sheet_to_json.parse_list(body.get('rank2'))
        r3 = sheet_to_json.parse_list(body.get('rank3'))
        if not base:
            raise ValueError('Effects is empty')
        if any(f['type'] == 'raise' for f in base):
            raise ValueError('raise is only for Rank II / III adds')
        rank2 = sheet_to_json.add_rank(base, r2)
        rank3 = sheet_to_json.add_rank(rank2, r3)
        if sheet_to_json.effect_count(rank3) > sheet_to_json.MAX_EFFECTS:
            raise ValueError('more than %d effects (counting its rank adds)' % sheet_to_json.MAX_EFFECTS)
        return {'ok': True, 'ranks': [base, rank2, rank3]}
    except (ValueError, IndexError) as e:
        return {'ok': False, 'error': str(e)}


def first_empty_row(ws, row, cols, key):
    r = row + 1
    while r <= ws.max_row and ws.cell(r, cols[key]).value not in (None, ''):
        r += 1
    return r


def fill_new_row(ws, r, header_row):
    """A new row past the sheet's prepared rows: styles and the grey formulas from the row above it."""
    above = r - 1
    if above <= header_row:
        return
    for c in range(1, ws.max_column + 1):
        src, dst = ws.cell(above, c), ws.cell(r, c)
        if src.has_style and not dst.has_style:
            dst._style = src._style
        v = src.value
        if isinstance(v, str) and v.startswith('=') and dst.value in (None, ''):
            dst.value = Translator(v, origin=src.coordinate).translate_formula(dst.coordinate)


def coerce(v):
    if isinstance(v, str):
        s = v.strip()
        if s == '':
            return None
        if re.match(r'^-?\d+$', s):
            return int(s)
        if re.match(r'^-?\d*\.\d+$', s):
            return float(s)
        return s
    return v


def apply(wb, changes):
    icons_changed = False
    for tab in TABS:
        edits = changes.get(tab.lower()) or []
        if not edits:
            continue
        first, editable, key = TABS[tab]
        ws = wb[tab]
        row = sheet_io.header_row(ws, first)
        cols = sheet_io.columns(ws, row)
        for edit in edits:
            r = edit.get('_row')
            if not r:
                r = first_empty_row(ws, row, cols, key)
                fill_new_row(ws, r, row)
            for h, v in edit.items():
                if h.startswith('_') or h not in cols or (editable and h not in editable):
                    continue
                old = ws.cell(r, cols[h]).value
                if isinstance(old, str) and old.startswith('='):
                    continue   # never overwrite the sheet's formulas
                ws.cell(r, cols[h]).value = coerce(v)
                if h == 'Icon (Raven fb#)':
                    icons_changed = True
    ws = wb['Settings']
    for s in changes.get('settings') or []:
        if isinstance(s.get('_row'), int) and isinstance(ws.cell(s['_row'], 2).value, NUMBER):
            v = coerce(s.get('value'))
            if isinstance(v, NUMBER):
                ws.cell(s['_row'], 2).value = v
    return icons_changed


def run(script, *args):
    p = subprocess.run([sys.executable, os.path.join(FORGE, script)] + list(args), cwd=PROJECT, capture_output=True,
                       text=True, encoding='utf-8', errors='replace', env=dict(os.environ, PYTHONIOENCODING='utf-8'))
    return p.returncode, (p.stdout + p.stderr).strip()


def save(body):
    with LOCK:
        if body.get('stamp') != stamp():
            return 409, {'ok': False, 'error': 'The sheet changed on disk since this page loaded it (saved in Excel?). '
                                               'Reload the page, then make your changes again.'}
        wb = sheet_io.load()
        icons = apply(wb, body.get('changes') or {})
        try:
            backup = sheet_io.save(wb)
        except PermissionError:
            return 423, {'ok': False, 'error': 'Excel has CARDS.xlsx open. Close it there, then press Save again.'}
        code, log = run('sheet_to_json.py', sheet_io.SHEET)
        atlas = ''
        if code == 0 and icons:
            _, atlas = run('card_forge.py', '--atlas-only')
        return 200, {'ok': True, 'converted': code == 0, 'log': log, 'atlas': atlas, 'backup': backup, 'stamp': stamp()}


class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        pass

    def send(self, code, body, kind='application/json; charset=utf-8'):
        data = body if isinstance(body, bytes) else json.dumps(body).encode('utf-8')
        self.send_response(code)
        self.send_header('Content-Type', kind)
        self.send_header('Content-Length', str(len(data)))
        self.send_header('Cache-Control', 'no-store' if 'json' in kind or 'html' in kind else 'max-age=3600')
        self.end_headers()
        self.wfile.write(data)

    def file(self, path, kind):
        with open(path, 'rb') as f:
            self.send(200, f.read(), kind)

    def do_GET(self):
        path = self.path.split('?')[0]
        try:
            if path in ('/', '/index.html'):
                return self.file(os.path.join(HERE, 'index.html'), 'text/html; charset=utf-8')
            if path == '/font.ttf':
                return self.file(FONT, 'font/ttf')
            m = re.match(r'^/icons/fb(\d+)\.png$', path)
            if m and os.path.exists(os.path.join(RAVEN_DIR, 'fb%s.png' % m.group(1))):
                return self.file(os.path.join(RAVEN_DIR, 'fb%s.png' % m.group(1)), 'image/png')
            if path == '/api/sheet':
                return self.send(200, sheet_json())
            self.send(404, {'error': 'not found'})
        except Exception as e:   # shown in the page
            self.send(500, {'ok': False, 'error': '%s: %s' % (type(e).__name__, e)})

    def do_POST(self):
        if self.headers.get('Origin') not in (None, 'http://127.0.0.1:%d' % PORT, 'http://localhost:%d' % PORT):
            return self.send(403, {'ok': False, 'error': 'only this page can save'})
        try:
            body = json.loads(self.rfile.read(int(self.headers.get('Content-Length') or 0)) or b'{}')
            if self.path == '/api/parse':
                return self.send(200, parse(body))
            if self.path == '/api/save':
                code, result = save(body)
                return self.send(code, result)
            self.send(404, {'error': 'not found'})
        except Exception as e:
            self.send(500, {'ok': False, 'error': '%s: %s' % (type(e).__name__, e)})


PORT = 8765


def main():
    global PORT
    if '--port' in sys.argv:
        PORT = int(sys.argv[sys.argv.index('--port') + 1])
    if not os.path.exists(sheet_io.SHEET):
        sys.exit('No card sheet at ' + sheet_io.SHEET)
    server = ThreadingHTTPServer(('127.0.0.1', PORT), Handler)
    url = 'http://127.0.0.1:%d/' % PORT
    print('Card editor: %s  (sheet: %s)' % (url, sheet_io.SHEET))
    print('Leave this window open while you edit; close it (or Ctrl+C) to stop.')
    if '--no-browser' not in sys.argv:
        threading.Timer(0.6, lambda: webbrowser.open(url)).start()
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == '__main__':
    main()
