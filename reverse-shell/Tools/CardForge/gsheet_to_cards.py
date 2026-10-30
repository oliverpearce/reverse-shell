"""A downloaded copy of the Google Sheets card sheet -> design/CARDS.xlsx.

    py Tools/CardForge/gsheet_to_cards.py [downloaded.xlsx]       (or double-click design/google_import.bat)

With no file it takes the newest .xlsx in design/google/ (other than the exported "upload this" one). It turns the
plain-word columns and the Abilities rows back into the card sheet's columns and effect language, checks every card
with the game's own rules, and only then writes CARDS.xlsx (after a backup in design/backups/): cards by Card ID
(a new ID = a new card), enemies by asset, packs and areas by ID, settings by name. Cards missing from the Google
copy are left as they are (set Stage to Cut to drop a card). Then it runs sheet_to_json.py, and Unity imports it.
"""
import glob
import os
import re
import subprocess
import sys
import warnings

import openpyxl
from openpyxl.formula.translate import Translator

import area_fights
import cards_to_gsheet
import gsheet_format as G
import sheet_io
import sheet_to_json as S

HERE = os.path.dirname(os.path.abspath(__file__))
FOLDER = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'design', G.FOLDER_NAME))


def newest_download():
    files = [f for f in glob.glob(os.path.join(FOLDER, '*.xlsx'))
             if os.path.basename(f) != G.EXPORT_NAME and not os.path.basename(f).startswith('~$')]
    if not files:
        sys.exit('No downloaded sheet in %s. In Google Sheets: File > Download > Microsoft Excel (.xlsx), and save it '
                 'there.' % FOLDER)
    return max(files, key=os.path.getmtime)


def rows(ws):
    """Each row someone typed something into (formula-filled columns, "(auto)", "(in game)" and the hidden helpers,
    don't count)."""
    heads = [ws.cell(1, c).value for c in range(1, ws.max_column + 1)]
    typed = {h for h in heads if h and not ws.column_dimensions[openpyxl.utils.get_column_letter(heads.index(h) + 1)].hidden
             and '(auto)' not in str(h) and '(in game)' not in str(h) and h not in cards_to_gsheet.HELPERS}
    for r in range(2, ws.max_row + 1):
        item = {h: ws.cell(r, c).value for c, h in enumerate(heads, 1) if h}
        if any(item.get(h) not in (None, '') for h in typed):
            yield r, item


def text(v):
    return '' if v is None else str(v).strip()


def number(v, what):
    if v in (None, ''):
        return None
    if isinstance(v, (int, float)):
        return v
    s = str(v).strip().rstrip('%')
    try:
        return float(s)
    except ValueError:
        raise ValueError('%s should be a number, not "%s"' % (what, v))


def fmt(v):
    """1.4 -> "1.4", 1.0 -> "1.0" (the sheet's usual style), 0.35 -> "0.35"."""
    v = round(float(v), 4)
    s = ('%.4f' % v).rstrip('0')
    return s + '0' if s.endswith('.') else s


def ability_text(a):
    """One Abilities row -> one line of the effect language."""
    does = text(a.get('Does'))
    if does not in G.DOES_BACK:
        raise ValueError('Does "%s" isn\'t one of the chips' % does)
    kind = G.DOES_BACK[does]
    who = text(a.get('Who')) or "The card's target"
    if who not in G.WHO_BACK:
        raise ValueError('Who "%s" isn\'t one of the chips' % who)
    target = G.WHO_BACK[who]
    amount = number(a.get('Strength / amount'), 'Strength / amount')
    status = text(a.get('Status'))
    turns = number(a.get('Turns'), 'Turns')
    times = number(a.get('Times'), 'Times')
    chance = number(a.get('Chance %'), 'Chance %')
    t = [kind.split()[0]]
    if kind in ('dmg', 'heal', 'shield', 'counter'):
        t.append(fmt((amount if amount is not None else 100) / 100.0))
    if kind in ('status', 'raise'):
        if not status:
            raise ValueError('%s needs a Status' % does)
        t.append(status)
    if kind in ('ult', 'ap'):
        t.append(str(int(amount if amount is not None else 1)))
    if kind == 'raise':
        t.append('%d%%' % int(amount if amount is not None else 10))
        return ' '.join(t)
    if kind in ('status', 'shield', 'cover') and turns:
        t.append('%dt' % int(turns))
    if kind in ('dmg', 'counter', 'status') and times and int(times) > 1:
        t.append('x%d' % int(times))
    if kind == 'status' and chance is not None and chance < 100:
        t.append('%d%%' % int(round(chance)))
    if kind == 'interrupt attack':
        t.append('attack')
    if kind == 'dmg' and text(a.get('Bonus vs status')):
        per = number(a.get('Bonus % per stack'), 'Bonus % per stack')
        t += ['+' + text(a.get('Bonus vs status')), fmt((per if per is not None else 30) / 100.0)]
        if text(a.get('Clear that status after?')) == 'Yes':
            t.append('consume')
    if kind == 'counter':
        low = number(a.get('Low-HP bonus %'), 'Low-HP bonus %')
        if low:
            t += ['lowhp', fmt(low / 100.0)]
        if text(a.get('Then give status')):
            t += ['then', text(a.get('Then give status'))]
            ft = number(a.get('For turns'), 'For turns')
            if ft:
                t.append('%dt' % int(ft))
    if target != 'card' and not (kind in ('ult', 'ap') and target == 'self'):
        t.append('@' + target)
    return ' '.join(t)


def game_text(v):
    """A Cards "(in game)" formula's value (as Google Sheets worked it out), or None for its notices."""
    t = text(v)
    return t if t and not t.startswith('(no abilities') and not t.startswith('Keep this card') else None


def card_values(c, effects, main, starter):
    """A Cards row (plain words) -> the card sheet's columns. Main / Starter come from the Decks tab."""
    pool = text(c.get('Pool'))
    tags = []
    if pool == 'Ult' and text(c.get('Ult resonance')):
        tags.append(text(c.get('Ult resonance')))
    if text(c.get('Enemy Ult?')) == 'Yes':
        tags.append('Enemy')
    if starter:
        tags.append('Starter')
    tags += [t.strip() for t in text(c.get('Other tags')).split(',') if t.strip()]
    art = text(c.get('Art (Raven #)')).replace('fb', '')
    if art.endswith('.0'):
        art = art[:-2]
    level = number(c.get('Level (1-20)'), 'Level')
    brk = number(c.get('Break'), 'Break')
    out = {
        'ID': text(c.get('Card ID')), 'Name': text(c.get('Card name')), 'Pool': pool, 'Category': text(c.get('Card type')),
        'Rarity': text(c.get('Rarity')) or 'Common', 'Main': 'Y' if main else None,
        'Tags': ', '.join(tags) or None, 'Target': text(c.get('Aims at')),
        'Base level': int(level) if level is not None else None, 'Attack type': text(c.get('Damage kind')) or None,
        'Break': int(brk) if brk is not None else None, 'Effects': '; '.join(effects['base']) or None,
        'Rank II adds': '; '.join(effects['r2']) or None, 'Rank III adds': '; '.join(effects['r3']) or None,
        'Card text': game_text(c.get('Card text (in game)')), 'Icon (Raven fb#)': ('fb' + art) if art else None,
        'Pixel effect': text(c.get('Pixel effect')) or None, 'State': text(c.get('Stage')) or 'Idea',
        'In-game asset': text(c.get('Unity asset (leave as is)')), 'Notes': text(c.get('Notes')) or None,
    }
    return out


def asset_for(pool, name, taken):
    prefix = {'Basic': 'Physical', 'Ult': 'MC'}.get(pool, pool)
    base = 'Card_%s_%s' % (prefix, ''.join(w[:1].upper() + w[1:] for w in re.sub(r'[^A-Za-z0-9 ]', '', name).split()) or 'New')
    asset, n = base, 2
    while asset in taken:
        asset, n = '%s%d' % (base, n), n + 1
    return asset


def check_card(v):
    """The same checks sheet_to_json makes, in plain words."""
    if v['Pool'] not in S.POOLS: raise ValueError('Pool "%s" isn\'t one of the chips' % v['Pool'])
    if v['Category'] not in S.CATEGORIES: raise ValueError('Card type "%s" isn\'t one of the chips' % v['Category'])
    if v['Target'] not in S.TARGETS: raise ValueError('Aims at "%s" isn\'t one of the chips' % v['Target'])
    if v['Base level'] is None or not 1 <= v['Base level'] <= 20: raise ValueError('Level must be 1-20')
    if v['Break'] is not None and not 0 <= v['Break'] <= 9: raise ValueError('Break must be 0-9')
    base, r2, r3 = S.parse_list(v['Effects']), S.parse_list(v['Rank II adds']), S.parse_list(v['Rank III adds'])
    if not base: raise ValueError('it has no "Always (Rank I)" ability')
    if any(f['type'] == 'raise' for f in base): raise ValueError('"Raise a status chance" only works when added at Rank II / III')
    rank3 = S.add_rank(S.add_rank(base, r2), r3)
    if S.effect_count(rank3) > S.MAX_EFFECTS:
        raise ValueError('more than %d different kinds of ability (counting the Rank II / III ones)' % S.MAX_EFFECTS)
    if v['Pool'] == 'Ult' and not v['Tags']:
        raise ValueError('an Ult card needs its Ult resonance')


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else newest_download()
    print('Reading', path)
    with warnings.catch_warnings():
        warnings.simplefilter('ignore')
        g = openpyxl.load_workbook(path, data_only=True)
    for tab in ('Cards', 'Abilities'):
        if tab not in g.sheetnames:
            sys.exit('%s has no %s tab: is it the Google Sheets card sheet?' % (path, tab))

    problems = []
    cards, order = {}, []
    for r, c in rows(g['Cards']):
        cid = text(c.get('Card ID'))
        if not cid:
            problems.append('Cards row %d: no Card ID' % r)
            continue
        if cid in cards:
            problems.append('Cards row %d: Card ID %s is used twice' % (r, cid))
            continue
        cards[cid] = (r, c)
        order.append(cid)
    effects = {cid: {'base': [], 'r2': [], 'r3': []} for cid in cards}
    for r, a in rows(g['Abilities']):
        cid = text(a.get('Card ID'))
        if cid not in effects:
            problems.append('Abilities row %d: Card ID "%s" isn\'t on the Cards tab' % (r, cid))
            continue
        when = G.WHEN_BACK.get(text(a.get('When')) or G.WHEN['base'])
        if when is None:
            problems.append('Abilities row %d (%s): When "%s" isn\'t one of the chips' % (r, cid, a.get('When')))
            continue
        try:
            line = ability_text(a)
            S.parse_effect(line)   # the game's own reader
            effects[cid][when].append(line)
        except (ValueError, IndexError) as e:
            problems.append('Abilities row %d (%s): %s' % (r, cid, e))

    target = sheet_io.load()
    ws = target['Cards']
    hr = sheet_io.header_row(ws)
    col = sheet_io.columns(ws, hr)
    by_id = {}
    for r in range(hr + 1, ws.max_row + 1):
        if ws.cell(r, col['ID']).value:
            by_id[str(ws.cell(r, col['ID']).value).strip()] = r
    taken = {ws.cell(r, col['In-game asset']).value for r in by_id.values()}

    # Decks: each resonance's Main card and the starter cards, by card name. A download without a Decks tab keeps
    # CARDS.xlsx's own.
    names = {}
    for cid in order:
        names.setdefault(text(cards[cid][1].get('Card name')), []).append(cid)
    mains, starters = {}, set()
    if 'Decks' in g.sheetnames:
        for r, d in rows(g['Decks']):
            what, res, name = text(d.get('What')), text(d.get('Resonance')), text(d.get('Card'))
            if not name:
                continue
            ids = names.get(name, [])
            if len(ids) != 1:
                problems.append('Decks row %d: %s' % (r, 'no card called "%s"' % name if not ids else
                                                     'two cards are called "%s" (rename one)' % name))
                continue
            if what == 'Main card':
                pool = text(cards[ids[0]][1].get('Pool'))
                if pool != res:
                    problems.append("Decks row %d: %s's Main card must be a %s card (%s is %s)" % (r, res, res, name, pool))
                mains[res] = ids[0]
            elif what == 'Starter card':
                starters.add(ids[0])
    else:
        for cid, r in by_id.items():
            if str(ws.cell(r, col['Main']).value or '').strip().upper() == 'Y':
                mains[str(ws.cell(r, col['Pool']).value)] = cid
            if 'Starter' in str(ws.cell(r, col['Tags']).value or ''):
                starters.add(cid)

    updates = []
    for cid in order:
        r, c = cards[cid]
        try:
            v = card_values(c, effects[cid], cid in mains.values(), cid in starters)
            if not v['Name']: raise ValueError('no Card name')
            if not v['In-game asset']:
                v['In-game asset'] = asset_for(v['Pool'], v['Name'], taken)
                taken.add(v['In-game asset'])
            elif not re.match(r'^Card_\w+$', v['In-game asset']):
                raise ValueError('Unity asset should look like Card_Arc_ChainBolt')
            if v['State'] != 'Cut':
                check_card(v)
            updates.append(v)
        except (ValueError, IndexError) as e:
            problems.append('Cards row %d (%s %s): %s' % (r, cid, text(c.get('Card name')), e))

    # Areas & fights: each area's pack (onto the Areas tab) and its fights' enemies, level and boss flag (into the
    # area files). Checked here; the area files are written only after CARDS.xlsx saves.
    fight_changes, area_pack, areas = {}, {}, {}
    if 'Areas & fights' in g.sheetnames:
        enemy_assets = {v: k for k, v in cards_to_gsheet.read_enemy_names().items()}
        areas = {a['id']: a for a in area_fights.read_all(cards_to_gsheet.AREAS_DIR)}
        for r, f in rows(g['Areas & fights']):
            aid = text(f.get('Area ID (leave as is)'))
            if aid not in areas:
                problems.append('Areas & fights row %d: no area "%s" (leave the Area ID as it is)' % (r, aid))
                continue
            pack = text(f.get('Pack (whole area)')) or 'Basic'
            if area_pack.setdefault(aid, pack) != pack:
                problems.append('Areas & fights row %d: %s has two different packs (%s and %s); give every row of an '
                                'area the same one' % (r, aid, area_pack[aid], pack))
            try:
                fnum = number(f.get('Fight #'), 'Fight #')
                level = number(f.get('Level'), 'Level')
            except ValueError as e:
                problems.append('Areas & fights row %d: %s' % (r, e))
                continue
            if fnum is None:
                continue
            fights = areas[aid]['fights']
            if not 1 <= int(fnum) <= len(fights):
                problems.append('Areas & fights row %d: %s has no fight %d (fights are the E tiles on its map: add them '
                                'in the Level Designer)' % (r, aid, fnum))
                continue
            enemies = []
            for j in range(1, G.MAX_FIGHT_ENEMIES + 1):
                name = text(f.get('Enemy %d' % j))
                if name:
                    if name not in enemy_assets:
                        problems.append('Areas & fights row %d: no enemy called "%s"' % (r, name))
                    else:
                        enemies.append(enemy_assets[name])
            fight_changes.setdefault(aid, {})[int(fnum) - 1] = {
                'enemies': enemies, 'level': int(level) if level else fights[int(fnum) - 1]['level'],
                'boss': text(f.get('Boss?')) == 'Yes'}

    if problems:
        print('\nNothing was changed. Fix these in Google Sheets, download again and re-run:')
        for p in problems:
            print('  - ' + p)
        sys.exit(1)

    if area_pack:
        t = target['Areas']
        tr = sheet_io.header_row(t, 'Area ID')
        tc = sheet_io.columns(t, tr)
        trows = {t.cell(r, tc['Area ID']).value: r for r in range(tr + 1, t.max_row + 1) if t.cell(r, tc['Area ID']).value}
        for aid, pack in area_pack.items():
            if aid in trows:
                t.cell(trows[aid], tc['Pack']).value = pack
            elif pack != 'Basic':   # an area that isn't listed gives the Basic pack
                r = tr + 1
                while t.cell(r, tc['Area ID']).value:
                    r += 1
                t.cell(r, tc['Area ID']).value = aid
                t.cell(r, tc['Area']).value = areas[aid]['name']
                t.cell(r, tc['Pack']).value = pack

    # ---------------------------------------------------------------- write CARDS.xlsx
    new = changed = 0
    for v in updates:
        r = by_id.get(v['ID'])
        if r is None:
            r = hr + 1
            while ws.cell(r, col['Name']).value not in (None, '') or ws.cell(r, col['ID']).value not in (None, ''):
                r += 1
            above = r - 1
            for c in range(1, ws.max_column + 1):   # styles and the grey formulas from the row above
                src, dst = ws.cell(above, c), ws.cell(r, c)
                if src.has_style and not dst.has_style:
                    dst._style = src._style
                if isinstance(src.value, str) and src.value.startswith('=') and dst.value in (None, ''):
                    dst.value = Translator(src.value, origin=src.coordinate).translate_formula(dst.coordinate)
            by_id[v['ID']] = r
            new += 1
        for h, value in v.items():
            if h in col:
                old = ws.cell(r, col[h]).value
                if isinstance(old, str) and old.startswith('='):
                    continue
                if (old if old not in ('',) else None) != value:
                    ws.cell(r, col[h]).value = value
                    changed += 1

    if 'Enemies' in g.sheetnames:
        we = target['Enemies']
        er = sheet_io.header_row(we, 'Enemy asset')
        ec = sheet_io.columns(we, er)
        erows = {we.cell(r, 1).value: r for r in range(er + 1, we.max_row + 1) if we.cell(r, 1).value}
        for _, e in rows(g['Enemies']):
            r = erows.get(text(e.get('Enemy asset (leave as is)')))
            if not r:
                continue
            weak = [w for w in G.WEAK_COLUMNS if text(e.get('Weak: ' + w)) == 'Yes']
            for h, value in (('Enemy', e.get('Enemy')), ('Innate card asset', e.get('Innate card')),
                             ('Innate +levels', e.get('Innate +levels')), ('Innate weight', e.get('Innate weight')),
                             ('Ult asset', e.get('Ult card')), ('Break', e.get('Break')),
                             ('Weak to', ', '.join(weak)), ('Notes', e.get('Notes'))):
                if h in ec:
                    we.cell(r, ec[h]).value = value if value != '' else None

    for tab, key in (('Packs', 'Pack ID'), ('Areas', 'Area ID')):
        if tab not in g.sheetnames:
            continue
        t = target[tab]
        tr = sheet_io.header_row(t, key)
        tc = sheet_io.columns(t, tr)
        trows = {t.cell(r, tc[key]).value: r for r in range(tr + 1, t.max_row + 1) if t.cell(r, tc[key]).value}
        for _, p in rows(g[tab]):
            r = trows.get(p.get(key))
            if not r:
                continue
            for h, value in p.items():
                if h in tc and h != key and not (isinstance(t.cell(r, tc[h]).value, str) and str(t.cell(r, tc[h]).value).startswith('=')):
                    t.cell(r, tc[h]).value = value

    if 'Settings' in g.sheetnames:
        st = target['Settings']
        srows = sheet_io.setting_rows(st)
        for _, s in rows(g['Settings']):
            label = text(s.get('Setting (leave as is)'))
            value = s.get('Value')
            if label in srows and isinstance(value, (int, float)) and isinstance(st.cell(srows[label], 2).value, (int, float)):
                st.cell(srows[label], 2).value = value

    try:
        backup = sheet_io.save(target)
    except PermissionError:
        sys.exit('Excel (or the card editor) has CARDS.xlsx open: close it and run this again.')
    print('Copied into CARDS.xlsx: %d new card(s), %d changed cell(s). Backup: %s' % (new, changed, backup))
    moved = 0
    for aid, changes in fight_changes.items():
        moved += area_fights.write(areas[aid]['path'], changes)
    if moved:
        print('Updated %d fight(s) in the area files (Unity reloads them when you switch to it).' % moved)
    p = subprocess.run([sys.executable, os.path.join(HERE, 'sheet_to_json.py'), sheet_io.SHEET], capture_output=True, text=True)
    print(p.stdout.strip() or p.stderr.strip())
    if p.returncode == 0:
        print('Done. Switch to Unity and it imports the cards.')
    sys.exit(p.returncode)


if __name__ == '__main__':
    main()
