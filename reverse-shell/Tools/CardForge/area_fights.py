"""Reads and edits the fights in the area assets (Assets/_Project/Resources/Areas/Area_*.asset), for the Google Sheets
card sheet's "Areas & fights" tab. Only a fight's enemies, level and boss flag are changed; everything else in the
file (its map, warps, story flags, NPCs) is left exactly as it is. A fight belongs to an 'E' on the map, so fights
are never added or removed here (do that in the Level Designer).
"""
import glob
import os
import re


def _blocks(text):
    """The encounters list: (start, end) of the whole list, and each fight's (start, end)."""
    m = re.search(r'^  encounters:[ ]*(\[\])?\n', text, re.M)
    if not m:
        return None, []
    start = m.end()
    if m.group(1):
        return (m.start(), m.end()), []
    end = start
    lines = text[start:].split('\n')
    pos, fights, cur = start, [], None
    for line in lines:
        if line.startswith('  - '):
            if cur is not None:
                fights.append((cur, pos))
            cur = pos
        elif not line.startswith('    '):
            break
        pos += len(line) + 1
    if cur is not None:
        fights.append((cur, pos))
    return (m.start(), pos), fights


def _parse_fight(block):
    enemies = []
    m = re.match(r'  - Enemies:[ ]*(\[\])?\n', block)
    if m and not m.group(1):
        for line in block[m.end():].split('\n'):
            mm = re.match(r'    - (.+)$', line)
            if not mm:
                break
            enemies.append(mm.group(1).strip().strip("'"))
    level = re.search(r'^    Level: (\d+)', block, re.M)
    boss = re.search(r'^    Boss: (\d)', block, re.M)
    flag = re.search(r'^    WinFlag: ?(.*)$', block, re.M)
    return {'enemies': enemies, 'level': int(level.group(1)) if level else 5, 'boss': bool(boss and boss.group(1) == '1'),
            'flag': flag.group(1).strip() if flag else ''}


def read(path):
    text = open(path, encoding='utf-8').read()
    area_id = re.search(r'^  id: ?(.*)$', text, re.M)
    name = re.search(r'^  displayName: ?(.*)$', text, re.M)
    _, fights = _blocks(text)
    return {'id': area_id.group(1).strip() if area_id else os.path.basename(path)[5:-6],
            'name': (name.group(1).strip().strip("'") if name else '') or os.path.basename(path)[5:-6],
            'path': path, 'fights': [_parse_fight(text[a:b]) for a, b in fights]}


def read_all(folder):
    return [read(p) for p in sorted(glob.glob(os.path.join(folder, 'Area_*.asset')))]


def write(path, changes):
    """changes: {fight index: {'enemies': [...], 'level': n, 'boss': bool}}. Returns how many fights changed."""
    text = open(path, encoding='utf-8', newline='').read()
    nl = '\r\n' if '\r\n' in text else '\n'
    text = text.replace('\r\n', '\n')
    _, fights = _blocks(text)
    done = 0
    for index in sorted(changes, reverse=True):   # from the end, so earlier positions stay valid
        a, b = fights[index]
        block = text[a:b]
        old = _parse_fight(block)
        new = changes[index]
        if old['enemies'] == new['enemies'] and old['level'] == new['level'] and old['boss'] == new['boss']:
            continue
        m = re.match(r'  - Enemies:[ ]*(\[\])?\n((?:    - .*\n)*)', block)
        enemies = '  - Enemies:\n' + ''.join('    - %s\n' % e for e in new['enemies']) if new['enemies'] else '  - Enemies: []\n'
        block = enemies + block[m.end():]
        block = re.sub(r'^    Level: \d+', '    Level: %d' % new['level'], block, count=1, flags=re.M)
        block = re.sub(r'^    Boss: \d', '    Boss: %d' % (1 if new['boss'] else 0), block, count=1, flags=re.M)
        text = text[:a] + block + text[b:]
        done += 1
    if done:
        with open(path, 'w', encoding='utf-8', newline='') as f:
            f.write(text.replace('\n', nl))
    return done
