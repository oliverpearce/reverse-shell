"""design/CARDS.xlsx -> a Google Sheets-friendly copy with plain words and dropdown chips.

    py Tools/CardForge/cards_to_gsheet.py [out.xlsx]        (or double-click design/google_export.bat)

Writes design/google/Reverse Shell Cards (upload this).xlsx. Upload it to Google Drive and open it with Google Sheets;
every list column becomes a dropdown chip. When you're done, File > Download > Microsoft Excel, put the file in
design/google/ and run gsheet_to_cards.py (design/google_import.bat) to bring the changes back.

Tabs: Help, Cards (each row tinted in its pool's colour; the "(in game)" columns are formulas giving the game's exact
card text for each rank), Abilities (one row per ability; its "(auto)" columns are that ability's sentence at each
rank), Decks (each resonance's Main card and the starter cards), Areas & fights (each area's pack and its fights,
grouped by area), Enemies, Packs, Settings. The text formulas copy CardText.ForRank and the effects' Text() in
Scripts/Effects (keep them in step if that wording changes). The layout and plain words are in gsheet_format.py.
"""
import os
import re
import sys

import openpyxl
from openpyxl.formatting.rule import FormulaRule
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.utils import get_column_letter as L
from openpyxl.worksheet.datavalidation import DataValidation

import gsheet_format as G
import sheet_io
import sheet_to_json as S

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project'))
OUT_DIR = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'design', G.FOLDER_NAME))
CARDS_DIR = os.path.join(PROJECT, 'Content', 'Cards')
ENEMIES_DIR = os.path.join(PROJECT, 'Content', 'Enemies')
STATUS_DIR = os.path.join(PROJECT, 'Content', 'Statuses')
AREAS_DIR = os.environ.get('CARD_AREAS') or os.path.join(PROJECT, 'Resources', 'Areas')
ROWS = 600   # how far down the dropdowns and formulas reach (room for new cards and abilities)

HEAD_FILL = PatternFill('solid', fgColor='2B2620')
HEAD_FONT = Font(bold=True, color='E3C47E')
AUTO_FILL = PatternFill('solid', fgColor='EDE9E0')
POOL_COLORS = {'Arc': 'FFE680', 'Root': 'B9F2B0', 'Cryo': 'BDE8FF', 'Cinder': 'FFC199', 'Prism': 'EEE0FF',
               'Basic': 'DDDAD3', 'Ult': 'F2DFA7'}
AREA_FILLS = ['F4EFE3', 'E4ECF4']
ALIAS_BACK = {v: k for k, v in S.STATUS_ALIASES.items()}   # Regeneration -> Regen
WRAP = Alignment(wrap_text=True, vertical='top')


def num(v):
    """A whole number as an int, otherwise rounded to 2 places."""
    v = round(float(v), 2)
    return int(v) if v.is_integer() else v


def ability_row(card_id, when, fx):
    k = fx['type']
    does = 'interrupt attack' if k == 'interrupt' and fx['attacksOnly'] else k
    row = {'Card ID': card_id, 'When': G.WHEN[when], 'Does': G.DOES[does]}
    if k in ('dmg', 'heal', 'shield', 'counter'):
        row['Strength / amount'] = num(fx['mult'] * 100)
    elif k in ('ult', 'ap'):
        row['Strength / amount'] = fx['amount']
    elif k == 'raise':
        row['Strength / amount'] = num(fx['chance'] * 100)
    if k in ('status', 'raise'):
        row['Status'] = ALIAS_BACK.get(fx['status'], fx['status'])
    if k != 'raise':
        row['Who'] = G.WHO[fx['target']]
    if k in ('status', 'shield', 'cover'):
        row['Turns'] = fx['turns']
    times = fx['hits'] if k == 'dmg' else fx['triggers'] if k == 'counter' else fx['stacks'] if k == 'status' else 1
    if times > 1:
        row['Times'] = times
    if k == 'status' and fx['chance'] < 1:
        row['Chance %'] = num(fx['chance'] * 100)
    if k == 'dmg' and fx['bonusVs']:
        row['Bonus vs status'] = ALIAS_BACK.get(fx['bonusVs'], fx['bonusVs'])
        row['Bonus % per stack'] = num(fx['bonusPerStack'] * 100)
        row['Clear that status after?'] = 'Yes' if fx['consume'] else 'No'
    if k == 'counter':
        if fx['lowhp']:
            row['Low-HP bonus %'] = num(fx['lowhp'] * 100)
        if fx['onHit']:
            row['Then give status'] = ALIAS_BACK.get(fx['onHit'], fx['onHit'])
            row['For turns'] = fx['onHitTurns']
    return row


def yaml_value(text, key):
    m = re.search(r'^  %s: ?(.*)$' % key, text, re.M)
    return m.group(1).strip().strip("'") if m else ''


def read_statuses():
    """Sheet status name -> (display name, verb or "-"), from the status assets (the words card text uses)."""
    out = {}
    for f in os.listdir(STATUS_DIR) if os.path.isdir(STATUS_DIR) else []:
        if not f.endswith('.asset'):
            continue
        t = open(os.path.join(STATUS_DIR, f), encoding='utf-8').read()
        display = yaml_value(t, 'displayName') or f[7:-6]
        out[display] = (display, yaml_value(t, 'verb') or '-')
    table = []
    for name in S.STATUSES:
        display = S.STATUS_ALIASES.get(name, name)
        table.append((name,) + out.get(display, (display, '-')))
    return table


def read_enemy_names():
    out = {}
    for f in sorted(os.listdir(ENEMIES_DIR)) if os.path.isdir(ENEMIES_DIR) else []:
        if f.endswith('.asset'):
            out[f[:-6]] = yaml_value(open(os.path.join(ENEMIES_DIR, f), encoding='utf-8').read(), 'displayName') or f[:-6]
    return out


def read_areas():
    """Each area asset: (id, name, fights); a fight is (enemy assets, level, boss, story flag)."""
    import area_fights
    return area_fights.read_all(AREAS_DIR)


# ---------------------------------------------------------------- workbook helpers

def header(ws, columns, freeze='B2'):
    for i, (name, width) in enumerate(columns, 1):
        c = ws.cell(1, i, name)
        c.fill, c.font = HEAD_FILL, HEAD_FONT
        c.alignment = Alignment(wrap_text=True, vertical='center')
        ws.column_dimensions[L(i)].width = width
    ws.row_dimensions[1].height = 34
    ws.freeze_panes = freeze


def col_of(columns, name):
    return [c[0] for c in columns].index(name) + 1


def chips(ws, columns, name, ref, allow_blank=True, first=2, last=ROWS):
    c = L(col_of(columns, name))
    dv = DataValidation(type='list', formula1=ref, allow_blank=allow_blank, showErrorMessage=True,
                        errorTitle='Not on the list', error='Pick one of the chips.')
    dv.add('%s%d:%s%d' % (c, first, c, last))
    ws.add_data_validation(dv)


def whole(ws, columns, name, lo, hi):
    c = L(col_of(columns, name))
    dv = DataValidation(type='whole', operator='between', formula1=str(lo), formula2=str(hi), allow_blank=True,
                        showErrorMessage=True, errorTitle='Out of range', error='A whole number %d to %d.' % (lo, hi))
    dv.add('%s2:%s%d' % (c, c, ROWS))
    ws.add_data_validation(dv)


def write_rows(ws, columns, rows, first=2):
    names = [c[0] for c in columns]
    for r, item in enumerate(rows, first):
        for name, v in item.items():
            if name in names and v not in (None, ''):
                ws.cell(r, names.index(name) + 1, v)


def tint_rows(ws, ncols, pool_col_letter):
    """Each row tinted in its pool's colour (a conditional format on the row's pool cell)."""
    for pool, color in POOL_COLORS.items():
        ws.conditional_formatting.add('A2:%s%d' % (L(ncols), ROWS), FormulaRule(
            formula=['$%s2="%s"' % (pool_col_letter, pool)], fill=PatternFill('solid', bgColor=color)))


# ---------------------------------------------------------------- the card text formulas

# Hidden helper columns after the Abilities tab's own (the steps of each ability's numbers and the running text).
HELPERS = ['RankNum', 'Code', 'Mult', 'Who phrase', 'Hits all', 'Base level', 'Best I', 'Best II', 'Best III',
           'x mult I', 'x mult II', 'x mult III', '/ best I', '/ best II', '/ best III', 'x area I', 'x area II',
           'x area III', 'x 100 I', 'x 100 II', 'x 100 III',
           'Pct I', 'Pct II', 'Pct III', 'Chance I', 'Chance II', 'Chance III', 'Acc I', 'Acc II a', 'Acc II b',
           'Acc III a', 'Acc III b', 'Acc III c', 'Text I', 'Text II', 'Text III']


def text_formulas(wa, refs):
    """The Abilities tab's (auto) columns and hidden helpers: each ability's sentence at each rank, worded and
    numbered exactly as the game's card text, and the card's whole text per rank on its last row."""
    A = {name: L(i) for i, (name, _) in enumerate(G.ABILITY_COLUMNS, 1)}
    first_helper = len(G.ABILITY_COLUMNS) + 1
    helpers = HELPERS
    H = {name: L(first_helper + i) for i, name in enumerate(helpers)}
    for i, name in enumerate(helpers):
        c = wa.cell(1, first_helper + i, name)
        c.font = Font(color='999999')
        wa.column_dimensions[L(first_helper + i)].hidden = True
    R = ROWS
    cards = 'Cards!$A$2:$G$%d' % R
    g = G.GAME
    a, card, when, does, amt, stat, who = A['Card ID'], A['Card name (auto)'], A['When'], A['Does'], A['Strength / amount'], A['Status'], A['Who']
    times, chance, bvs, bper, bclr, low, then, fort = (A['Times'], A['Chance %'], A['Bonus vs status'], A['Bonus % per stack'],
                                                       A['Clear that status after?'], A['Low-HP bonus %'], A['Then give status'], A['For turns'])

    def rng(col):
        return '$%s$2:$%s$%d' % (col, col, R)

    for n in range(2, R + 1):
        f = {}
        blank = '$%s%d=""' % (a, n)
        aims = 'IFERROR(VLOOKUP($%s%d,%s,6,FALSE),"")' % (a, n, cards)
        f[card] = '=IF(%s,"",IFERROR(VLOOKUP($%s%d,Cards!$A$2:$B$%d,2,FALSE),"? no such card"))' % (blank, a, n, R)
        f[A['Pool (auto)']] = '=IF(%s,"",IFERROR(VLOOKUP($%s%d,%s,3,FALSE),""))' % (blank, a, n, cards)
        f[H['RankNum']] = '=IF($%s%d="%s",3,IF($%s%d="%s",2,1))' % (when, n, G.WHEN['r3'], when, n, G.WHEN['r2'])
        f[H['Code']] = '=IF(%s,"",IFERROR(VLOOKUP($%s%d,%s,2,FALSE),""))' % (blank, does, n, refs['DoesT'])
        code = '$%s%d' % (H['Code'], n)
        f[H['Mult']] = '=IF(OR(%s="dmg",%s="heal",%s="shield",%s="counter"),%s,0)' % (code, code, code, code,
                                                                                    f32('IF($%s%d="",100,$%s%d)/100' % (amt, n, amt, n)))
        cardwho = 'OR($%s%d="",$%s%d="%s")' % (who, n, who, n, G.WHO['card'])
        f[H['Who phrase']] = '=IF(%s,IFERROR(VLOOKUP(%s,%s,2,FALSE),""),IFERROR(VLOOKUP($%s%d,%s,2,FALSE),""))' % (
            cardwho, aims, refs['AimT'], who, n, refs['WhoT'])
        f[H['Hits all']] = '=IF(OR($%s%d="%s",$%s%d="%s"),1,IF(AND(%s,OR(%s="All enemies",%s="All allies")),1,0))' % (
            who, n, G.WHO['enemies'], who, n, G.WHO['allies'], cardwho, aims, aims)
        f[H['Base level']] = '=IFERROR(VLOOKUP($%s%d,%s,7,FALSE),0)' % (a, n, cards)
        for k, best in ((1, 'Best I'), (2, 'Best II'), (3, 'Best III')):
            f[H[best]] = '=IF(%s,0,SUMPRODUCT(MAX((%s=$%s%d)*(%s<=%d)*%s)))' % (blank, rng(a), a, n, rng(H['RankNum']), k, rng(H['Mult']))
        # The game works these out in 32-bit floats (CardLevels.Of, then x 100 in the effect's text), one step at a
        # time, then Mathf.RoundToInt (halves to even). Copying each float step keeps every number exactly the game's.
        for k, rn in ((1, 'I'), (2, 'II'), (3, 'III')):
            bonus = 0 if k == 1 else g['rank2'] if k == 2 else g['rank3']
            power = '%s*MIN(%d,$%s%d+%d)' % (g['power'], g['cap'], H['Base level'], n, bonus)
            f[H['x mult ' + rn]] = '=' + f32('%s*$%s%d' % (power, H['Mult'], n))
            f[H['/ best ' + rn]] = '=IF($%s%d=0,0,%s)' % (H['Best ' + rn], n, f32('$%s%d/$%s%d' % (H['x mult ' + rn], n, H['Best ' + rn], n)))
            f[H['x area ' + rn]] = '=IF($%s%d=1,%s,$%s%d)' % (H['Hits all'], n, f32('$%s%d*%r' % (H['/ best ' + rn], n, SPREAD32)), H['/ best ' + rn], n)
            f[H['x 100 ' + rn]] = '=' + f32('$%s%d*100' % (H['x area ' + rn], n))
            x = '$%s%d' % (H['x 100 ' + rn], n)
            f[H['Pct ' + rn]] = '=IF(ABS(%s-INT(%s)-0.5)<0.0000001,IF(ISEVEN(INT(%s)),INT(%s),INT(%s)+1),ROUND(%s,0))' % (x, x, x, x, x, x)
        c0 = 'IF($%s%d="",100,$%s%d)' % (chance, n, chance, n)

        def raise_at(j):
            total = 'SUMIFS(%s,%s,$%s%d,%s,"raise",%s,$%s%d,%s,%d)' % (rng(amt), rng(a), a, n, rng(H['Code']), rng(stat), stat, n, rng(H['RankNum']), j)
            below = lambda col: '$%s%d:$%s$%d' % (col, n + 1, col, R + 1)
            last = 'IF(AND($%s%d<=%d,COUNTIFS(%s,$%s%d,%s,"status",%s,$%s%d,%s,"<=%d")=0),1,0)' % (
                H['RankNum'], n, j, below(a), a, n, below(H['Code']), below(stat), stat, n, below(H['RankNum']), j)
            return '%s*%s' % (last, total)
        f[H['Chance I']] = '=MIN(100,%s)' % c0
        f[H['Chance II']] = '=MIN(100,%s+%s)' % (c0, raise_at(2))
        f[H['Chance III']] = '=MIN(100,%s+%s+%s)' % (c0, raise_at(2), raise_at(3))

        wp = '$%s%d' % (H['Who phrase'], n)
        cap_who = 'UPPER(LEFT(%s,1))&MID(%s,2,200)' % (wp, wp)
        disp = lambda col: 'IFERROR(VLOOKUP($%s%d,%s,2,FALSE),$%s%d)' % (col, n, refs['StatusT'], col, n)
        verb = 'IFERROR(VLOOKUP($%s%d,%s,3,FALSE),"-")' % (stat, n, refs['StatusT'])
        for k, col, pct, ch in ((1, A['Rank I text (auto)'], 'Pct I', 'Chance I'), (2, A['Rank II text (auto)'], 'Pct II', 'Chance II'),
                                (3, A['Rank III text (auto)'], 'Pct III', 'Chance III')):
            p = '$%s%d' % (H[pct], n)
            c = '$%s%d' % (H[ch], n)
            dmg = ('"Deal "&%s&"%% ATK damage to "&%s&IF(N($%s%d)>1," over "&$%s%d&" hits","")&IF($%s%d="",".",", +"&IF($%s%d="",30,$%s%d)&"%% per "&%s&IF($%s%d="Yes",", then clears it.","."))'
                   % (p, wp, times, n, times, n, bvs, n, bper, n, bper, n, disp(bvs), bclr, n))
            heal = '"Heal "&%s&" for "&%s&"%% of ATK."' % (wp, p)
            shield = '"Shield "&%s&" for "&%s&"%% of ATK."' % (wp, p)
            counter = ('%s&" hits back for "&%s&"%% ATK when attacked"&IF(N($%s%d)>1," (up to "&$%s%d&" times)","")&IF(N($%s%d)>0,", +"&$%s%d&"%% below half HP","")&IF($%s%d="","",", giving "&%s)&"."'
                       % (cap_who, p, times, n, times, n, low, n, low, n, then, n, disp(then)))
            status = ('IF(%s<100,IF(%s<>"-",%s&"%% chance to "&%s&" "&%s&".",%s&"%% chance to give "&%s&" "&%s&"."),IF(%s<>"-",UPPER(LEFT(%s,1))&MID(%s,2,50)&" "&%s&".","Give "&%s&" "&%s&"."))'
                      % (c, verb, c, verb, wp, c, wp, disp(stat), verb, verb, verb, wp, wp, disp(stat)))
            amount = 'IF($%s%d="",1,$%s%d)' % (amt, n, amt, n)
            body = ('IF({c}="dmg",{dmg},IF({c}="heal",{heal},IF({c}="shield",{shield},IF({c}="counter",{counter},IF({c}="status",{status},'
                    'IF({c}="cover","Take the hits aimed at "&{wp}&" this turn.",IF({c}="interrupt","Cancel "&{wp}&"\'s move.",'
                    'IF({c}="interrupt attack","Cancel "&{wp}&"\'s attack.",IF({c}="cleanse","Clear "&{wp}&"\'s debuffs.",'
                    'IF({c}="ult",{capwho}&" gains "&{amount}&" Ult.",IF({c}="ap","+"&{amount}&" AP next turn.","")))))))))))').format(
                c=code, dmg=dmg, heal=heal, shield=shield, counter=counter, status=status, wp=wp, capwho=cap_who, amount=amount)
            f[col] = '=IF(OR(%s,$%s%d>%d,%s="",%s="raise"),"",%s)' % (blank, H['RankNum'], n, k, code, code, body)

        def acc(name, text_col, group):
            prev = '$%s%d' % (H[name], n - 1) if n > 2 else '""'
            return '=IF($%s%d=$%s%d,%s,"")&IF(AND($%s%d=%d,$%s%d<>"")," "&$%s%d,"")' % (
                a, n, a, n - 1, prev, H['RankNum'], n, group, text_col, n, text_col, n)
        f[H['Acc I']] = acc('Acc I', A['Rank I text (auto)'], 1)
        f[H['Acc II a']] = acc('Acc II a', A['Rank II text (auto)'], 1)
        f[H['Acc II b']] = acc('Acc II b', A['Rank II text (auto)'], 2)
        f[H['Acc III a']] = acc('Acc III a', A['Rank III text (auto)'], 1)
        f[H['Acc III b']] = acc('Acc III b', A['Rank III text (auto)'], 2)
        f[H['Acc III c']] = acc('Acc III c', A['Rank III text (auto)'], 3)
        f[H['Text I']] = '=TRIM($%s%d)' % (H['Acc I'], n)
        f[H['Text II']] = '=TRIM($%s%d&$%s%d)' % (H['Acc II a'], n, H['Acc II b'], n)
        f[H['Text III']] = '=TRIM($%s%d&$%s%d&$%s%d)' % (H['Acc III a'], n, H['Acc III b'], n, H['Acc III c'], n)
        for col, formula in f.items():
            cell = wa['%s%d' % (col, n)]
            cell.value = formula
            if col in (card, A['Pool (auto)'], A['Rank I text (auto)'], A['Rank II text (auto)'], A['Rank III text (auto)']):
                cell.fill = AUTO_FILL
                if col not in (card, A['Pool (auto)']):
                    cell.alignment = WRAP
    return H


SPREAD32 = 0.60000002384185791015625   # 0.6 as a 32-bit float (CombatConfig.spreadShare)


def f32(v):
    """A sheet formula rounding the value <v> to the nearest 32-bit float (24 significant bits)."""
    e = '2^(FLOOR(LOG(ABS(%s),2)+0.000000001,1)-23)' % v
    return 'IF((%s)=0,0,ROUND((%s)/%s,0)*%s)' % (v, v, e, e)


def card_text_formula(n, text_col, ult_blank):
    """A Cards row's in-game text: the card's last Abilities row's whole text for that rank."""
    R = ROWS
    ids = 'Abilities!$A$2:$A$%d' % R
    cnt = 'COUNTIF(%s,$A%d)' % (ids, n)
    first = 'MATCH($A%d,%s,0)' % (n, ids)
    together = 'COUNTIF(INDEX(%s,%s):INDEX(%s,%s+%s-1),$A%d)=%s' % (ids, first, ids, first, cnt, n, cnt)
    text = 'INDEX(Abilities!$%s$2:$%s$%d,%s+%s-1)' % (text_col, text_col, R, first, cnt)
    body = 'IF(%s=0,"(no abilities yet)",IF(%s,%s,"Keep this card\'s Abilities rows together"))' % (cnt, together, text)
    if ult_blank:
        body = 'IF($C%d="Ult","",%s)' % (n, body)
    return '=IF($A%d="","",%s)' % (n, body)


def lists_tab(wb, lists, tables):
    ws = wb.create_sheet('Lists')
    refs, c = {}, 1
    for name, items in lists.items():
        ws.cell(1, c, name).font = Font(bold=True)
        for r, v in enumerate(items, 2):
            ws.cell(r, c, v)
        refs[name] = "Lists!$%s$2:$%s$%d" % (L(c), L(c), len(items) + 1)
        c += 1
    for name, rows in tables.items():
        width = len(rows[0])
        ws.cell(1, c, name).font = Font(bold=True)
        for r, row in enumerate(rows, 2):
            for i, v in enumerate(row):
                ws.cell(r, c + i, v)
        refs[name] = "Lists!$%s$2:$%s$%d" % (L(c), L(c + width - 1), len(rows) + 1)
        c += width
    ws.sheet_state = 'hidden'
    return refs


# ---------------------------------------------------------------- main

def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(OUT_DIR, G.EXPORT_NAME)
    src = sheet_io.load()
    ws = src['Cards']
    hr = sheet_io.header_row(ws)
    col = sheet_io.columns(ws, hr)

    cards, abilities, assets, names = [], [], [], []
    main_cards, starters = {}, []
    for r in range(hr + 1, ws.max_row + 1):
        get = lambda h: ws.cell(r, col[h]).value if h in col else None
        if not get('Name'):
            continue
        pool = get('Pool')
        tags = [t.strip() for t in str(get('Tags') or '').split(',') if t.strip()]
        ult_res = tags[0] if pool == 'Ult' and tags else ''
        rest = [t for t in tags[1 if ult_res else 0:] if t not in ('Starter', 'Enemy')]
        icon = str(get('Icon (Raven fb#)') or '').replace('fb', '').strip()
        cid = get('ID') or ''
        if str(get('Main') or '').strip().upper() == 'Y' and pool in G.RESONANCES and get('State') != 'Cut':
            main_cards.setdefault(pool, get('Name'))
        if 'Starter' in tags and get('State') != 'Cut':
            starters.append(get('Name'))
        cards.append({
            'Card ID': cid, 'Card name': get('Name'), 'Pool': pool, 'Card type': get('Category'),
            'Rarity': get('Rarity'), 'Aims at': get('Target'), 'Level (1-20)': get('Base level'),
            'Damage kind': get('Attack type'), 'Break': get('Break'), 'Stage': get('State'),
            'Ult resonance': ult_res, 'Enemy Ult?': 'Yes' if 'Enemy' in tags else 'No', 'Other tags': ', '.join(rest),
            'Art (Raven #)': int(icon) if icon.isdigit() else icon, 'Pixel effect': get('Pixel effect'),
            'Notes': get('Notes'), 'Unity asset (leave as is)': get('In-game asset'),
        })
        assets.append(get('In-game asset'))
        names.append(get('Name'))
        for when, h in (('base', 'Effects'), ('r2', 'Rank II adds'), ('r3', 'Rank III adds')):
            try:
                for fx in S.parse_list(get(h)):
                    abilities.append(ability_row(cid, when, fx))
            except ValueError as e:
                sys.exit('%s (%s) %s: %s. Fix it in CARDS.xlsx first.' % (cid, get('Name'), h, e))

    # Enemies.
    enemy_names = read_enemy_names()
    we = src['Enemies']
    er = sheet_io.header_row(we, 'Enemy asset')
    ec = sheet_io.columns(we, er)
    enemies = []
    for r in range(er + 1, we.max_row + 1):
        get = lambda h: we.cell(r, ec[h]).value if h in ec else None
        if not get('Enemy asset'):
            continue
        weak = [w.strip() for w in str(get('Weak to') or '').split(',')]
        e = {'Enemy asset (leave as is)': get('Enemy asset'), 'Enemy': get('Enemy'), 'Innate card': get('Innate card asset'),
             'Innate +levels': get('Innate +levels'), 'Innate weight': get('Innate weight'), 'Ult card': get('Ult asset'),
             'Break': get('Break'), 'Notes': get('Notes')}
        for w in G.WEAK_COLUMNS:
            e['Weak: ' + w] = 'Yes' if w in weak else 'No'
        enemies.append(e)
        for x in (get('Innate card asset'), get('Ult asset')):
            if x and x not in assets:
                assets.append(x)
    if os.path.isdir(CARDS_DIR):
        for f in sorted(os.listdir(CARDS_DIR)):
            if f.endswith('.asset') and f[:-6] not in assets:
                assets.append(f[:-6])

    def table(name, first):
        t = src[name]
        hrow = sheet_io.header_row(t, first)
        cols = sheet_io.columns(t, hrow)
        heads = sorted(cols, key=cols.get)
        rows = []
        for r in range(hrow + 1, t.max_row + 1):
            if not t.cell(r, cols[first]).value:
                continue
            rows.append({h: t.cell(r, cols[h]).value for h in heads
                         if not (isinstance(t.cell(r, cols[h]).value, str) and str(t.cell(r, cols[h]).value).startswith('='))})
        return [h for h in heads if any(h in row for row in rows)], rows

    pack_heads, packs = table('Packs', 'Pack ID')
    _, area_rows = table('Areas', 'Area ID')
    area_packs = {a['Area ID']: a.get('Pack') for a in area_rows}
    settings = []
    st = src['Settings']
    for label, r in sheet_io.setting_rows(st).items():
        v = st.cell(r, 2).value
        if isinstance(v, (int, float)) and not isinstance(v, bool) and label not in ('Attack', 'Buff', 'Debuff', 'Defense', 'Counter', 'Heal'):
            settings.append((r, label, v, st.cell(r, 3).value or '', label in [s[0] for s in S.BREAK_SETTINGS]))
    settings.sort()
    lists_src = sheet_io.dropdown_lists(src)

    def items(name):
        rng = lists_src[name].split('!')[1].replace('$', '')
        return [c.value for row in st[rng] for c in row if c.value not in (None, '')]

    # ---------------------------------------------------------------- build the workbook
    wb = openpyxl.Workbook()
    help_ws = wb.active
    help_ws.title = 'Help'
    for r, (a, b) in enumerate(G.HELP, 1):
        help_ws.cell(r, 1, a).font = Font(bold=True, size=14 if r == 1 else 11, color='9A7424' if a.isupper() or r == 1 else '000000')
        help_ws.cell(r, 2, b).alignment = WRAP
    help_ws.column_dimensions['A'].width = 26
    help_ws.column_dimensions['B'].width = 120

    wc = wb.create_sheet('Cards')
    header(wc, G.CARD_COLUMNS, 'C2')
    write_rows(wc, G.CARD_COLUMNS, cards)
    wa = wb.create_sheet('Abilities')
    header(wa, G.ABILITY_COLUMNS, 'C2')
    write_rows(wa, G.ABILITY_COLUMNS, abilities)

    fights_ws = wb.create_sheet('Areas & fights')
    decks = wb.create_sheet('Decks')
    enemy_cols = [('Enemy asset (leave as is)', 24), ('Enemy', 16), ('Innate card', 30), ('Innate +levels', 9),
                  ('Innate weight', 9), ('Ult card', 30), ('Break', 8)] + [('Weak: ' + w, 11) for w in G.WEAK_COLUMNS] + \
                 [('Appears in', 30), ('Notes', 44)]
    wen = wb.create_sheet('Enemies')
    pack_cols = [(h, 13 if h != 'Notes' else 44) for h in pack_heads]
    wp = wb.create_sheet('Packs')
    wst = wb.create_sheet('Settings')

    statuses = read_statuses()
    refs = lists_tab(wb, {
        'Pools': items('Pools'), 'Card types': items('Categories'), 'Rarity': items('Rarity'), 'Aims at': G.TARGETS,
        'Stage': items('State'), 'Yes/No': G.YES_NO, 'Damage kind': S.ATTACK_TYPES, 'Resonance': items('Resonance'),
        'Does': list(G.DOES.values()), 'Who': list(G.WHO.values()), 'When': list(G.WHEN.values()),
        'Statuses': S.STATUSES, 'Card assets': assets, 'Packs': [p['Pack ID'] for p in packs] + ['Random'],
        'Enemies': list(read_enemy_names().values()),
    }, {
        'DoesT': [(v, k) for k, v in G.DOES.items()],
        'WhoT': [(G.WHO[k], v) for k, v in G.WHO_PHRASE.items()],
        'AimT': list(G.AIM_PHRASE.items()),
        'StatusT': statuses,
    })

    # Cards: chips, colour and the in-game text columns.
    for name, ref in (('Pool', 'Pools'), ('Card type', 'Card types'), ('Rarity', 'Rarity'), ('Aims at', 'Aims at'),
                      ('Damage kind', 'Damage kind'), ('Stage', 'Stage'), ('Ult resonance', 'Resonance'), ('Enemy Ult?', 'Yes/No')):
        chips(wc, G.CARD_COLUMNS, name, refs[ref])
    whole(wc, G.CARD_COLUMNS, 'Level (1-20)', 1, 20)
    whole(wc, G.CARD_COLUMNS, 'Break', 0, 9)
    tint_rows(wc, len(G.CARD_COLUMNS), L(col_of(G.CARD_COLUMNS, 'Pool')))
    H = text_formulas(wa, refs)
    for name, helper in zip(G.TEXT_COLUMNS, ('Text I', 'Text II', 'Text III')):
        c = col_of(G.CARD_COLUMNS, name)
        for n in range(2, ROWS + 1):
            cell = wc.cell(n, c, card_text_formula(n, H[helper], helper != 'Text I'))
            cell.alignment = WRAP
        wc.cell(1, c).fill = PatternFill('solid', fgColor='5A4A2A')
    for name in ('Notes', 'Card name'):
        for n in range(2, len(cards) + 2):
            wc.cell(n, col_of(G.CARD_COLUMNS, name)).alignment = WRAP
    for n in range(2, len(cards) + 2):
        wc.cell(n, len(G.CARD_COLUMNS)).fill = AUTO_FILL

    # Abilities: chips and the same colours (from its Pool (auto) column).
    chips(wa, G.ABILITY_COLUMNS, 'Card ID', 'Cards!$A$2:$A$%d' % ROWS)
    for name, ref in (('When', 'When'), ('Does', 'Does'), ('Status', 'Statuses'), ('Who', 'Who'),
                      ('Bonus vs status', 'Statuses'), ('Clear that status after?', 'Yes/No'), ('Then give status', 'Statuses')):
        chips(wa, G.ABILITY_COLUMNS, name, refs[ref])
    for name, lo, hi in (('Turns', 1, 9), ('Times', 1, 9), ('For turns', 1, 9)):
        whole(wa, G.ABILITY_COLUMNS, name, lo, hi)
    tint_rows(wa, col_of(G.ABILITY_COLUMNS, 'Pool (auto)') - 1, L(col_of(G.ABILITY_COLUMNS, 'Pool (auto)')))

    # Decks: each resonance's Main card and the starter cards (card-name chips).
    deck_cols = [('What', 16), ('Resonance', 12), ('Card', 28), ('', 4), ('About', 80)]
    header(decks, deck_cols, 'A2')
    r = 2
    for res in G.RESONANCES:
        decks.cell(r, 1, 'Main card')
        decks.cell(r, 2, res)
        decks.cell(r, 3, main_cards.get(res))
        r += 1
    for i in range(G.STARTER_SLOTS):
        decks.cell(r, 1, 'Starter card')
        decks.cell(r, 3, starters[i] if i < len(starters) else None)
        r += 1
    decks.cell(2, 5, "Main card: the resonance's innate card (always in that kit's deck; pick one of that pool's cards).")
    decks.cell(3, 5, 'Starter cards: owned from a new game, in the Alt slots until the player picks others.')
    dv = DataValidation(type='list', formula1='Cards!$B$2:$B$%d' % ROWS, allow_blank=True, showErrorMessage=True,
                        errorTitle='Not a card', error='Pick a card name.')
    dv.add('C2:C%d' % (r - 1))
    decks.add_data_validation(dv)

    # Areas & fights, grouped by area.
    header(fights_ws, G.FIGHT_COLUMNS, 'C2')
    areas = read_areas()
    order = {'city': 0, 'tunnel': 1, 'chamber': 2}
    areas.sort(key=lambda x: (order.get(x['id'], 5 if x['id'].startswith('intro') else 9), x['id']))
    r, appears = 2, {}
    for i, area in enumerate(areas):
        fill = PatternFill('solid', fgColor=AREA_FILLS[i % 2])
        fights = area['fights'] or [None]
        for fnum, fight in enumerate(fights, 1):
            vals = {'Area ID (leave as is)': area['id'], 'Area': area['name'], 'Pack (whole area)': area_packs.get(area['id'], 'Basic')}
            if fight is not None:
                vals['Fight #'] = fnum
                for j, enemy in enumerate(fight['enemies'][:G.MAX_FIGHT_ENEMIES], 1):
                    vals['Enemy %d' % j] = enemy_names.get(enemy, enemy)
                    appears.setdefault(enemy, []).append(area['name'])
                vals['Level'] = fight['level']
                vals['Boss?'] = 'Yes' if fight['boss'] else 'No'
                vals['Story fight'] = fight['flag'] or ''
            for name, v in vals.items():
                fights_ws.cell(r, col_of(G.FIGHT_COLUMNS, name), v)
            for c in range(1, len(G.FIGHT_COLUMNS) + 1):
                fights_ws.cell(r, c).fill = fill
                if fnum == 1:
                    fights_ws.cell(r, c).font = Font(bold=c <= 3)
            r += 1
    for j in range(1, G.MAX_FIGHT_ENEMIES + 1):
        chips(fights_ws, G.FIGHT_COLUMNS, 'Enemy %d' % j, refs['Enemies'], last=r - 1)
    chips(fights_ws, G.FIGHT_COLUMNS, 'Pack (whole area)', refs['Packs'], last=r - 1)
    chips(fights_ws, G.FIGHT_COLUMNS, 'Boss?', refs['Yes/No'], last=r - 1)
    lv = L(col_of(G.FIGHT_COLUMNS, 'Level'))
    dv = DataValidation(type='whole', operator='between', formula1='1', formula2='99', allow_blank=True)
    dv.add('%s2:%s%d' % (lv, lv, r - 1))
    fights_ws.add_data_validation(dv)

    # Enemies, packs, settings.
    header(wen, enemy_cols)
    for e in enemies:
        e['Appears in'] = ', '.join(sorted(set(appears.get(e['Enemy asset (leave as is)'], [])))) or '(story only)'
    write_rows(wen, enemy_cols, enemies)
    chips(wen, enemy_cols, 'Innate card', refs['Card assets'])
    chips(wen, enemy_cols, 'Ult card', refs['Card assets'])
    for w in G.WEAK_COLUMNS:
        chips(wen, enemy_cols, 'Weak: ' + w, refs['Yes/No'], False)
    whole(wen, enemy_cols, 'Break', 0, 99)

    header(wp, pack_cols)
    write_rows(wp, pack_cols, packs)
    for c, (h, _) in enumerate(pack_cols, 1):
        if h.endswith('%'):
            for rr in range(2, len(packs) + 2):
                wp.cell(rr, c).number_format = '0.0%'
    if 'Resonance' in pack_heads:
        chips(wp, pack_cols, 'Resonance', refs['Resonance'])

    set_cols = [('Setting (leave as is)', 40), ('Value', 10), ('Goes into the game?', 13), ('What it does', 100)]
    header(wst, set_cols)
    for i, (_, label, v, note, game) in enumerate(settings, 2):
        wst.cell(i, 1, label)
        wst.cell(i, 2, v)
        wst.cell(i, 3, 'Yes, game' if game else 'Sheet maths')
        wst.cell(i, 4, note).alignment = WRAP

    os.makedirs(os.path.dirname(out), exist_ok=True)
    wb.save(out)
    print('Wrote %d cards and %d abilities to %s' % (len(cards), len(abilities), out))
    print('Upload it to Google Drive, open it with Google Sheets, edit, then File > Download > Microsoft Excel (.xlsx)')
    print('into design/%s/ and run design/google_import.bat.' % G.FOLDER_NAME)


if __name__ == '__main__':
    main()
