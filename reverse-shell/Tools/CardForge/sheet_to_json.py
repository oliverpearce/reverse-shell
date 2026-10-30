"""Card sheet -> cards.json for the Unity card importer.

    py Tools/CardForge/sheet_to_json.py [../design/CARDS.xlsx]

Reads the Cards, Packs, Areas, Enemies and Settings tabs of the card sheet and writes Tools/CardForge/cards.json, which Unity's
"Reverse Shell / Import Cards From Sheet" (Editor/CardImporter.cs) turns into card assets. Rows without a name are
skipped, and so are rows whose State is Cut. Problems (an unknown status, a bad effect) are listed and nothing is
written. Each pack's picture is cut from its Art column / row of Art/ThirdParty/CardPacks/CardPacks.png into
Resources/Cards/Packs/Pack_<Pack ID>.png.

Break & Stagger (bible v0.8): each card's Attack type (Physical / Incantation) and Break, each enemy's Break and
Weak to, and the Settings tab's Break & Stagger numbers. A sheet from before them gets the old defaults (run
add_break_columns.py once to add the columns). The Icon (Raven fb#) column picks each card's art (card_forge.py).

The Effects, Rank II adds and Rank III adds columns use the effect language on the sheet's "Effects key" tab. Rank II
is Effects + Rank II adds; Rank III is all three. In the adds, "raise Stunned 10%" raises the chance of a status the
card already gives instead of adding another roll.
"""
import json
import os
import re
import sys

import openpyxl

HERE = os.path.dirname(os.path.abspath(__file__))
SHEET = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, '..', '..', '..', 'design', 'CARDS.xlsx')
OUT = os.path.join(HERE, 'cards.json')
PROJECT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project'))
PACK_SHEET = os.path.join(PROJECT, 'Art', 'ThirdParty', 'CardPacks', 'CardPacks.png')
PACK_OUT = os.path.join(PROJECT, 'Resources', 'Cards', 'Packs')
RARITIES = ['Common', 'Rare', 'Epic', 'Legendary']

POOLS = ['Arc', 'Root', 'Cryo', 'Cinder', 'Prism', 'Basic', 'Ult']
CATEGORIES = ['Attack', 'Buff', 'Debuff', 'Defense', 'Counter', 'Heal']
TARGETS = {'Single enemy': 'SingleEnemy', 'All enemies': 'AllEnemies', 'Random enemy': 'RandomEnemy', 'Self': 'Self',
           'Single ally': 'SingleAlly', 'All allies': 'AllAllies', 'Random ally': 'RandomAlly'}
STATUSES = ['Poison', 'Regen', 'ATK Up', 'ATK Down', 'DEF Up', 'Physical DEF Down', 'Incantation Might Up', 'Stunned',
            'Burn', 'Chill', 'Frozen']
STATUS_ALIASES = {'Regen': 'Regeneration'}   # sheet name -> the status asset's display name
EFFECT_TARGETS = ['self', 'enemy', 'enemies', 'random', 'ally', 'allies', 'randomally']
ATTACK_TYPES = ['Physical', 'Incantation']
WEAKNESSES = ['Physical', 'Incantation', 'Arc', 'Root', 'Cryo', 'Cinder', 'Prism']
# Settings tab label -> cards.json settings key, and the default for a sheet without it.
BREAK_SETTINGS = [('Break rank II x', 'breakRankII', 2.0), ('Break rank III x', 'breakRankIII', 3.0),
                  ('Stagger bonus', 'staggerBonus', 0.5), ('Stagger turns', 'staggerTurns', 2),
                  ('Boss extra Break', 'bossBreakBonus', 3)]
REQUIRED = ['ID', 'Name', 'Pool', 'Category', 'Rarity', 'Main', 'Target', 'Base level', 'Effects', 'Rank II adds',
            'Rank III adds', 'State', 'In-game asset']


def is_flag(tok):
    return bool(re.match(r'^(\d+t|x\d+|@\w+|\d+%|consume|attack|then|lowhp|[+-]?\d+(\.\d+)?)$', tok))


def take_status(tokens, i):
    """Reads a status name that may be several words, from tokens[i]; returns (name, next index)."""
    words = []
    while i < len(tokens) and not is_flag(tokens[i]):
        words.append(tokens[i])
        i += 1
    name = ' '.join(words)
    for s in STATUSES:
        if s.lower() == name.lower():
            return STATUS_ALIASES.get(s, s), i
    raise ValueError('unknown status "%s" (known: %s)' % (name, ', '.join(STATUSES)))


def parse_effect(text):
    tokens = text.split()
    if not tokens:
        raise ValueError('empty effect')
    kind = tokens[0].lower()
    if kind == 'raise':   # rank adds only: "raise Stunned 10%" = the card's Stunned chance goes up 10 points
        status, i = take_status(tokens, 1)
        if i != len(tokens) - 1 or not re.match(r'^\d+%$', tokens[i]):
            raise ValueError('write raise like "raise Stunned 10%"')
        return {'type': 'raise', 'status': status, 'chance': int(tokens[i][:-1]) / 100.0}
    fx = {'type': kind, 'target': 'card', 'mult': 1.0, 'hits': 1, 'turns': 2, 'stacks': 1, 'chance': 1.0,
          'status': '', 'bonusVs': '', 'bonusPerStack': 0.0, 'consume': False, 'triggers': 1, 'lowhp': 0.0,
          'onHit': '', 'onHitTurns': 2, 'attacksOnly': False, 'amount': 1}
    if kind in ('ult', 'ap'):
        fx['target'] = 'self'
    i = 1
    if kind in ('dmg', 'heal', 'shield', 'counter'):
        if i < len(tokens) and re.match(r'^\d+(\.\d+)?$', tokens[i]):
            fx['mult'] = float(tokens[i]); i += 1
        if kind == 'shield':
            fx['turns'] = 1
    elif kind == 'status':
        fx['status'], i = take_status(tokens, i)
    elif kind in ('ult', 'ap'):
        if i < len(tokens) and re.match(r'^-?\d+$', tokens[i]):
            fx['amount'] = int(tokens[i]); i += 1
    elif kind == 'cover':
        fx['turns'] = 1
    elif kind not in ('interrupt', 'cleanse'):
        raise ValueError('unknown effect "%s"' % kind)
    while i < len(tokens):
        t = tokens[i]
        if t.startswith('@'):
            if t[1:] not in EFFECT_TARGETS:
                raise ValueError('unknown target %s (use @%s)' % (t, ', @'.join(EFFECT_TARGETS)))
            fx['target'] = t[1:]; i += 1
        elif re.match(r'^\d+t$', t):
            fx['turns'] = int(t[:-1]); i += 1
        elif re.match(r'^x\d+$', t):
            n = int(t[1:])
            if kind == 'dmg': fx['hits'] = n
            elif kind == 'counter': fx['triggers'] = n
            else: fx['stacks'] = n
            i += 1
        elif re.match(r'^\d+%$', t):
            fx['chance'] = int(t[:-1]) / 100.0; i += 1
        elif t.startswith('+') and kind == 'dmg':
            tokens[i] = t[1:]
            fx['bonusVs'], i = take_status(tokens, i)
            fx['bonusPerStack'] = float(tokens[i]); i += 1
        elif t == 'consume':
            fx['consume'] = True; i += 1
        elif t == 'attack' and kind == 'interrupt':
            fx['attacksOnly'] = True; i += 1
        elif t == 'lowhp' and kind == 'counter':
            fx['lowhp'] = float(tokens[i + 1]); i += 2
        elif t == 'then' and kind == 'counter':
            fx['onHit'], i = take_status(tokens, i + 1)
            if i < len(tokens) and re.match(r'^\d+t$', tokens[i]):
                fx['onHitTurns'] = int(tokens[i][:-1]); i += 1
        else:
            raise ValueError('did not understand "%s" in "%s"' % (t, text))
    if kind == 'dmg' and fx['consume'] and not fx['bonusVs']:
        raise ValueError('"consume" needs a +Status bonus to clear')
    return fx


MAX_EFFECTS = 2   # a card has at most two effects (repeats of one effect, e.g. an extra Stun roll, count once)


def effect_count(effects):
    seen = []
    for f in effects:
        key = (f['type'], f['status'] if f['type'] == 'status' else f['target'] if f['type'] == 'dmg' else '')
        if key not in seen: seen.append(key)
    return len(seen)


def parse_list(text):
    if text is None or str(text).strip() == '':
        return []
    return [parse_effect(part.strip()) for part in str(text).split(';') if part.strip()]


def add_rank(effects, adds):
    """The next rank's effects: these plus the rank's adds. A "raise" add changes the chance of the status already
    on the card (its last one of that status) instead of adding a second roll."""
    out = [dict(f) for f in effects]
    for a in adds:
        if a['type'] != 'raise':
            out.append(a)
            continue
        for f in reversed(out):
            if f['type'] == 'status' and f['status'] == a['status']:
                f['chance'] = min(1.0, round(f['chance'] + a['chance'], 4))
                break
        else:
            raise ValueError('raise %s: the card has no %s to raise' % (a['status'], a['status']))
    return out


def main():
    wb = openpyxl.load_workbook(SHEET, data_only=True)
    ws = wb['Cards']
    header_row = None
    for r in range(1, 10):
        if ws.cell(r, 1).value == 'ID':
            header_row = r
            break
    if header_row is None:
        sys.exit('No header row with "ID" in column A of the Cards tab.')
    col = {ws.cell(header_row, c).value: c for c in range(1, ws.max_column + 1) if ws.cell(header_row, c).value}
    missing = [h for h in REQUIRED if h not in col]
    if missing:
        sys.exit('The Cards tab is missing columns: ' + ', '.join(missing))

    cards, problems, assets = [], [], set()
    for r in range(header_row + 1, ws.max_row + 1):
        get = lambda h: ws.cell(r, col[h]).value
        name = (get('Name') or '').strip() if isinstance(get('Name'), str) else get('Name')
        if not name or (get('State') or '') == 'Cut':
            continue
        where = 'row %d (%s)' % (r, name)
        try:
            pool, cat, target = get('Pool'), get('Category'), get('Target')
            if pool not in POOLS: raise ValueError('Pool "%s" should be one of %s' % (pool, POOLS))
            if cat not in CATEGORIES: raise ValueError('Category "%s" should be one of %s' % (cat, CATEGORIES))
            if target not in TARGETS: raise ValueError('Target "%s" should be one of %s' % (target, list(TARGETS)))
            level = get('Base level')
            if not isinstance(level, (int, float)) or not 1 <= level <= 20: raise ValueError('Base level must be 1-20')
            asset = get('In-game asset') or ''
            if not re.match(r'^Card_\w+$', asset): raise ValueError('In-game asset should look like Card_Arc_ChainBolt')
            if asset in assets: raise ValueError('In-game asset %s is used twice' % asset)
            assets.add(asset)
            base, r2, r3 = parse_list(get('Effects')), parse_list(get('Rank II adds')), parse_list(get('Rank III adds'))
            if not base: raise ValueError('Effects is empty')
            if any(f['type'] == 'raise' for f in base): raise ValueError('raise is only for Rank II / III adds')
            rank2 = add_rank(base, r2)
            rank3 = add_rank(rank2, r3)
            if effect_count(rank3) > MAX_EFFECTS:
                raise ValueError('more than %d effects (counting its rank adds); a card has at most %d' % (MAX_EFFECTS, MAX_EFFECTS))
            ult = pool == 'Ult'
            rarity = get('Rarity') or 'Common'
            if rarity not in RARITIES: raise ValueError('Rarity "%s" should be one of %s' % (rarity, RARITIES))
            tags = [t.strip() for t in (get('Tags') or '').split(',')] if 'Tags' in col else []
            resonance = tags[0] if ult and tags else ''
            enemy = ult and 'Enemy' in tags   # an enemy's Ult (Enemies tab), not a resonance kit's
            starter = 'Starter' in tags       # owned from the start, in the Alt slots until the player picks others
            if ult and resonance not in POOLS[:6]:
                raise ValueError('an Ult row needs its resonance (Arc, Root, ...) first in Tags')
            basic = pool == 'Basic' or (ult and resonance == 'Basic')
            attack_type = (get('Attack type') if 'Attack type' in col else None) or ('Physical' if basic else 'Incantation')
            if attack_type not in ATTACK_TYPES: raise ValueError('Attack type "%s" should be one of %s' % (attack_type, ATTACK_TYPES))
            brk = get('Break') if 'Break' in col else None
            brk = 1 if brk is None or brk == '' else brk
            if not isinstance(brk, (int, float)) or not 0 <= brk <= 9 or brk != int(brk): raise ValueError('Break must be a whole number 0-9')
            icon = str(get('Icon (Raven fb#)') or '').strip() if 'Icon (Raven fb#)' in col else ''
            if icon and not re.match(r'^(fb)?\d+$', icon): raise ValueError('Icon should be a Raven number like fb1026')
            cards.append({'id': get('ID') or '', 'name': name, 'pool': pool, 'category': cat, 'target': TARGETS[target],
                          'level': int(level), 'main': str(get('Main') or '').strip().upper() == 'Y', 'ult': ult,
                          'resonance': resonance, 'enemy': enemy, 'starter': starter, 'asset': asset, 'rarity': rarity,
                          'attackType': attack_type, 'breakValue': int(brk), 'icon': int(icon.replace('fb', '')) if icon else 0,
                          'rank1': base, 'rank2': rank2, 'rank3': rank3})
        except (ValueError, IndexError) as e:
            problems.append('%s: %s' % (where, e))
    packs, areas = read_packs(wb, problems), read_areas(wb, problems)
    enemies = read_enemies(wb, problems, assets)
    settings = read_settings(wb, problems)
    if problems:
        print('Not written; fix these first:')
        for p in problems: print('  ' + p)
        sys.exit(1)
    with open(OUT, 'w', encoding='utf-8') as f:
        json.dump({'cards': cards, 'packs': packs, 'areas': areas, 'enemies': enemies, 'settings': settings}, f, indent=1)
    print('Wrote %d cards, %d packs, %d areas and %d enemies to %s' % (len(cards), len(packs), len(areas), len(enemies), os.path.normpath(OUT)))
    cut_pack_art(packs)


def table(ws, header_row=4):
    col = {ws.cell(header_row, c).value: c for c in range(1, ws.max_column + 1) if ws.cell(header_row, c).value}
    for r in range(header_row + 1, ws.max_row + 1):
        yield r, (lambda h, r=r: ws.cell(r, col[h]).value if h in col else None)


def read_packs(wb, problems):
    packs = []
    for r, get in table(wb['Packs']):
        pid = get('Pack ID')
        if not pid: continue
        where = 'Packs row %d (%s)' % (r, pid)
        try:
            res = get('Resonance')
            if res not in POOLS[:6]: raise ValueError('Resonance "%s" should be one of %s' % (res, POOLS[:6]))
            nums = {}
            for h in ('Resonance %', 'Common %', 'Rare %', 'Epic %', 'Legendary %', 'Soul card %', 'Sun/Moon/Star %', 'Blank card %'):
                v = get(h)
                if h == 'Legendary %' and v is None: v = 0   # sheets from before Legendary cards
                if not isinstance(v, (int, float)) or not 0 <= v <= 1: raise ValueError('%s must be 0%%-100%%' % h)
                nums[h] = float(v)
            if abs(nums['Common %'] + nums['Rare %'] + nums['Epic %'] + nums['Legendary %'] - 1) > 1e-4:
                raise ValueError('Common + Rare + Epic + Legendary must add up to 100%')
            if nums['Soul card %'] + nums['Sun/Moon/Star %'] > 1: raise ValueError('Soul + Sun/Moon/Star are over 100%')
            ac, ar = get('Art column'), get('Art row')
            if not (isinstance(ac, (int, float)) and 1 <= ac <= 9 and isinstance(ar, (int, float)) and 1 <= ar <= 20):
                raise ValueError('Art column must be 1-9 and Art row 1-20')
            cards, wins = get('Cards per pack'), get('Packs per win')
            if not isinstance(cards, (int, float)) or cards < 1: raise ValueError('Cards per pack must be 1 or more')
            if not isinstance(wins, (int, float)) or wins < 0: raise ValueError('Packs per win must be 0 or more')
            packs.append({'id': str(pid), 'name': get('Name') or str(pid), 'pool': res, 'artColumn': int(ac), 'artRow': int(ar),
                          'cardsPerPack': int(cards), 'packsPerWin': int(wins), 'poolShare': nums['Resonance %'],
                          'common': nums['Common %'], 'rare': nums['Rare %'], 'epic': nums['Epic %'], 'legendary': nums['Legendary %'],
                          'soulChance': nums['Soul card %'], 'arcanaChance': nums['Sun/Moon/Star %'], 'blankChance': nums['Blank card %']})
        except ValueError as e:
            problems.append('%s: %s' % (where, e))
    if not packs: problems.append('Packs: no packs')
    return packs


def read_areas(wb, problems):
    areas = []
    ids = [str(get('Pack ID')) for _, get in table(wb['Packs']) if get('Pack ID')]
    for r, get in table(wb['Areas']):
        area, pack = get('Area ID'), get('Pack')
        if not area: continue
        if pack != 'Random' and pack not in ids:   # Random: a random pack each win
            problems.append('Areas row %d (%s): Pack "%s" is not on the Packs tab (or Random)' % (r, area, pack))
            continue
        areas.append({'area': str(area), 'pack': str(pack)})
    return areas


def read_enemies(wb, problems, sheet_assets):
    """The Enemies tab: each enemy's innate card, its level bonus and weight, and its Ult (card asset names)."""
    if 'Enemies' not in wb.sheetnames:
        return []
    cards_dir = os.path.join(PROJECT, 'Content', 'Cards')
    exists = lambda a: a in sheet_assets or os.path.exists(os.path.join(cards_dir, a + '.asset'))
    out = []
    for r, get in table(wb['Enemies']):
        enemy = get('Enemy asset')
        if not enemy: continue
        where = 'Enemies row %d (%s)' % (r, enemy)
        if not os.path.exists(os.path.join(PROJECT, 'Content', 'Enemies', str(enemy) + '.asset')):
            problems.append('%s: no Content/Enemies/%s.asset' % (where, enemy)); continue
        innate, ult = get('Innate card asset') or '', get('Ult asset') or ''
        if innate and not exists(innate): problems.append('%s: no card %s' % (where, innate)); continue
        if ult and not exists(ult): problems.append('%s: no card %s' % (where, ult)); continue
        levels, weight = get('Innate +levels'), get('Innate weight')
        try:
            levels, weight = int(float(levels or 0)), float(weight if weight is not None else 1)
        except ValueError:
            problems.append('%s: Innate +levels and weight must be numbers' % where); continue
        points = get('Break')
        points = 3 if points is None or points == '' else points
        if not isinstance(points, (int, float)) or not 0 <= points <= 99 or points != int(points):
            problems.append('%s: Break must be a whole number 0-99 (0 = can\'t be broken)' % where); continue
        weak = [w.strip() for w in str(get('Weak to') if get('Weak to') is not None else 'Physical').split(',') if w.strip()]
        bad = [w for w in weak if w not in WEAKNESSES]
        if bad:
            problems.append('%s: Weak to "%s" should be from %s' % (where, ', '.join(bad), ', '.join(WEAKNESSES))); continue
        out.append({'enemy': str(enemy), 'innate': innate, 'levels': levels, 'weight': weight, 'ult': ult,
                    'breakPoints': int(points), 'weaknesses': weak})
    return out


def read_settings(wb, problems):
    """The Settings tab's Break & Stagger numbers (by their label in column A)."""
    out = {key: default for _, key, default in BREAK_SETTINGS}
    if 'Settings' not in wb.sheetnames:
        return out
    ws = wb['Settings']
    rows = {ws.cell(r, 1).value: r for r in range(1, ws.max_row + 1) if isinstance(ws.cell(r, 1).value, str)}
    for label, key, default in BREAK_SETTINGS:
        if label not in rows: continue
        v = ws.cell(rows[label], 2).value
        if not isinstance(v, (int, float)) or v < 0:
            problems.append('Settings: %s must be a number, 0 or more' % label); continue
        out[key] = int(v) if isinstance(default, int) else float(v)
    return out


def cut_pack_art(packs):
    """Cuts each pack's picture out of CardPacks.png (9 colours x 20 designs, packs on transparent gaps)."""
    try:
        from PIL import Image
    except ImportError:
        print('Pillow is missing (py -m pip install pillow): pack pictures not cut.')
        return
    sheet = Image.open(PACK_SHEET).convert('RGBA')
    W, H = sheet.size
    cw = W / 9.0
    os.makedirs(PACK_OUT, exist_ok=True)
    for pack in packs:
        x0, x1 = int((pack['artColumn'] - 1) * cw), int(pack['artColumn'] * cw)
        alpha = sheet.crop((x0, 0, x1, H)).split()[3]
        runs, start = [], None
        for y in range(H + 1):
            filled = y < H and alpha.crop((0, y, x1 - x0, y + 1)).getbbox() is not None
            if filled and start is None: start = y
            if not filled and start is not None:
                runs.append((start, y)); start = None
        if len(runs) < pack['artRow']:
            print('  %s: column %d has only %d packs' % (pack['id'], pack['artColumn'], len(runs)))
            continue
        y0, y1 = runs[pack['artRow'] - 1]
        cell = sheet.crop((x0, y0, x1, y1))
        cell = cell.crop(cell.split()[3].getbbox())
        path = os.path.join(PACK_OUT, 'Pack_%s.png' % pack['id'])
        cell.save(path)
        print('  pack art: %s (%dx%d)' % (os.path.relpath(path, PROJECT), cell.size[0], cell.size[1]))


if __name__ == '__main__':
    main()
