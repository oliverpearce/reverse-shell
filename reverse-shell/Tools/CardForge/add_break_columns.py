"""One-time: adds the Break & Stagger columns (bible v0.8) to the card sheet, with starting values.

    py Tools/CardForge/add_break_columns.py [../design/CARDS.xlsx]

Cards tab: "Attack type" (Basic cards and Basic Ults Physical, the rest Incantation) and "Break" (0 for cards that
don't hit, 2 for Ults and level 6+ attacks, else 1). Enemies tab: "Break" and "Weak to". Settings tab: the Break &
Stagger numbers, and an "Attack type" dropdown list. Columns or settings already there are left as they are, so it's
safe to run again. Backs the sheet up to design/backups/ first.
"""
import sys

import sheet_io

# Starting picks for Chapter One's enemies: all weak to Physical (the bible's rule), some to a resonance too.
ENEMY_BREAK = {
    'Enemy_ElectricDragon': (5, 'Physical'),
    'Enemy_Collector': (4, 'Physical, Arc'),
    'Enemy_RustSentry': (3, 'Physical, Arc'),
    'Enemy_ScrapStalker': (3, 'Physical, Incantation'),
    'Enemy_ScrapHound': (3, 'Physical, Arc'),
    'Enemy_WireCrawler': (3, 'Physical'),
}

SETTINGS = [
    ('Break & Stagger', None, 'Bible v0.8 §5. These go into the game (PackRates) when the sheet is imported.'),
    ('Break rank II x', 2, "A card's Break damage at Rank II = its Break x this"),
    ('Break rank III x', 3, "A card's Break damage at Rank III = its Break x this"),
    ('Stagger bonus', 0.5, 'Extra damage a Staggered enemy takes for the rest of the turn (0.5 = +50%), DoT included'),
    ('Stagger turns', 2, 'Turns a broken enemy stays Staggered, the one it broke in included (2 = it also loses its next move)'),
    ('Boss extra Break', 3, "A boss's Break value = its Enemies-tab Break + this"),
]


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else sheet_io.SHEET
    wb = sheet_io.load(path)

    ws = wb['Cards']
    row = sheet_io.header_row(ws, 'ID')
    had = sheet_io.columns(ws, row)
    c_type = sheet_io.add_column(ws, row, 'Attack type', 'Rarity')
    c_break = sheet_io.add_column(ws, row, 'Break', 'Base level')
    col = sheet_io.columns(ws, row)
    filled = 0
    for r in range(row + 1, ws.max_row + 1):
        get = lambda h: ws.cell(r, col[h]).value
        if not get('Name'):
            continue
        pool, tags = get('Pool'), str(get('Tags') or '')
        ult = pool == 'Ult'
        basic = pool == 'Basic' or (ult and tags.split(',')[0].strip() == 'Basic')
        if 'Attack type' not in had:
            ws.cell(r, c_type).value = 'Physical' if basic else 'Incantation'
        if 'Break' not in had:
            hits = 'dmg' in str(get('Effects') or '')
            level = get('Base level') or 0
            ws.cell(r, c_break).value = 0 if not hits else 2 if ult or (get('Category') == 'Attack' and level >= 6) else 1
        filled += 1

    ws = wb['Enemies']
    erow = sheet_io.header_row(ws, 'Enemy asset')
    ehad = sheet_io.columns(ws, erow)
    e_break = sheet_io.add_column(ws, erow, 'Break', 'Innate +levels')
    e_weak = sheet_io.add_column(ws, erow, 'Weak to', 'Enemy')
    for r in range(erow + 1, ws.max_row + 1):
        enemy = ws.cell(r, 1).value
        if not enemy:
            continue
        points, weak = ENEMY_BREAK.get(enemy, (3, 'Physical'))
        if 'Break' not in ehad: ws.cell(r, e_break).value = points
        if 'Weak to' not in ehad: ws.cell(r, e_weak).value = weak

    ws = wb['Settings']
    rows = sheet_io.setting_rows(ws)
    title = next((r for k, r in rows.items() if k.startswith('Target card counts')), 1)
    if 'Break rank II x' not in rows:
        start = ws.max_row + 2
        for i, (label, value, note) in enumerate(SETTINGS):
            r = start + i
            ws.cell(r, 1).value = label
            ws.cell(r, 1)._style = ws.cell(title if value is None else 4, 1)._style   # a section title / a label
            if value is not None:
                ws.cell(r, 2).value = value
                ws.cell(r, 2)._style = ws.cell(4, 2)._style   # a blue input
            ws.cell(r, 3).value = note
            ws.cell(r, 3)._style = ws.cell(4, 3)._style
    # The Attack type dropdown list, beside the others.
    lists_row = None
    for r in range(1, ws.max_row + 1):
        if ws.cell(r, 1).value == 'Pools':
            lists_row = r
    if lists_row is not None and 'Attack type' not in sheet_io.columns(ws, lists_row):
        c = max(sheet_io.columns(ws, lists_row).values()) + 1
        for i, v in enumerate(['Attack type', 'Physical', 'Incantation']):
            ws.cell(lists_row + i, c).value = v
            ws.cell(lists_row + i, c)._style = ws.cell(lists_row + i, 1)._style

    backup = sheet_io.save(wb, path)
    print('Added the Break columns (%d cards). Backup: %s' % (filled, backup))


if __name__ == '__main__':
    main()
