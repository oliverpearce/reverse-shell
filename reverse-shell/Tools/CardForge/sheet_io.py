"""Shared helpers for tools that write the card sheet (design/CARDS.xlsx): add_break_columns.py and the card editor.

openpyxl keeps the sheet's values, formulas, styles, widths and comments, but drops Excel's newer kind of dropdown
lists (the ones that point at the Settings tab's "Dropdown lists"). save() puts them back as ordinary dropdowns, so
the sheet still has its dropdowns in Excel after a tool has saved it. Every save makes a backup first.
"""
import copy
import os
import shutil
import time

import openpyxl
from openpyxl.worksheet.datavalidation import DataValidation

HERE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.environ.get('CARD_SHEET') or os.path.normpath(os.path.join(HERE, '..', '..', '..', 'design', 'CARDS.xlsx'))
BACKUPS = os.environ.get('CARD_BACKUPS') or os.path.normpath(os.path.join(HERE, '..', '..', '..', 'design', 'backups'))
KEEP_BACKUPS = 30

# Column on a tab -> the header of its list under the Settings tab's "Dropdown lists".
DROPDOWNS = {
    'Cards': {'Pool': 'Pools', 'Category': 'Categories', 'Target': 'Targets', 'State': 'State', 'Main': 'Main',
              'Rarity': 'Rarity', 'Attack type': 'Attack type'},
    'Packs': {'Resonance': 'Resonance'},
}


def load(path=SHEET):
    import warnings
    with warnings.catch_warnings():
        warnings.simplefilter('ignore')   # "Data Validation extension is not supported": save() restores them
        return openpyxl.load_workbook(path)


def header_row(ws, first='ID', rows=10):
    """The row whose column A is <first> (the Cards tab's is 3; the other tabs' 4)."""
    for r in range(1, rows + 1):
        if ws.cell(r, 1).value == first:
            return r
    return None


def columns(ws, row):
    return {ws.cell(row, c).value: c for c in range(1, ws.max_column + 1) if ws.cell(row, c).value}


def add_column(ws, row, name, like):
    """Adds a column called <name> after the last one, styled like column <like> (header and cells), unless it's
    there already. Returns its number."""
    cols = columns(ws, row)
    if name in cols:
        return cols[name]
    c = max(cols.values()) + 1
    src = cols[like]
    for r in range(1, ws.max_row + 1):
        a, b = ws.cell(r, src), ws.cell(r, c)
        if a.has_style:
            b._style = copy.copy(a._style)
    ws.cell(row, c).value = name
    letter_src = openpyxl.utils.get_column_letter(src)
    letter = openpyxl.utils.get_column_letter(c)
    if letter_src in ws.column_dimensions:
        ws.column_dimensions[letter].width = ws.column_dimensions[letter_src].width
    return c


def setting_rows(ws):
    """The Settings tab's label (column A) -> row, for the single-value settings."""
    return {ws.cell(r, 1).value: r for r in range(1, ws.max_row + 1) if isinstance(ws.cell(r, 1).value, str)}


def dropdown_lists(wb):
    """The Settings tab's "Dropdown lists": header -> absolute range of its items (e.g. Settings!$A$24:$A$30)."""
    ws = wb['Settings']
    head = None
    for r in range(1, ws.max_row + 1):
        if ws.cell(r, 1).value == 'Pools':
            head = r
            break
    if head is None:
        return {}
    out = {}
    for c in range(1, ws.max_column + 1):
        name = ws.cell(head, c).value
        if not name:
            continue
        last = head
        while ws.cell(last + 1, c).value not in (None, ''):
            last += 1
        if last > head:
            L = openpyxl.utils.get_column_letter(c)
            out[name] = "Settings!$%s$%d:$%s$%d" % (L, head + 1, L, last)
    return out


def restore_dropdowns(wb):
    lists = dropdown_lists(wb)
    for tab, cols in DROPDOWNS.items():
        if tab not in wb.sheetnames:
            continue
        ws = wb[tab]
        row = header_row(ws, 'ID') or header_row(ws, 'Pack ID') or 4
        have = columns(ws, row)
        # Drop list validations on these columns (ours from an earlier save), keep everything else.
        targets = {openpyxl.utils.get_column_letter(have[h]) for h in cols if h in have}
        keep = []
        for dv in ws.data_validations.dataValidation:
            letters = {openpyxl.utils.get_column_letter(c) for rng in dv.sqref.ranges for c in range(rng.min_col, rng.max_col + 1)}
            if dv.type == 'list' and letters & targets:
                continue
            keep.append(dv)
        ws.data_validations.dataValidation = keep
        last = max(ws.max_row, row + 200)
        for h, list_name in cols.items():
            if h not in have or list_name not in lists:
                continue
            L = openpyxl.utils.get_column_letter(have[h])
            dv = DataValidation(type='list', formula1=lists[list_name], allow_blank=True)
            dv.add('%s%d:%s%d' % (L, row + 1, L, last))
            ws.add_data_validation(dv)


def backup(path=SHEET):
    os.makedirs(BACKUPS, exist_ok=True)
    dest = os.path.join(BACKUPS, 'CARDS_%s.xlsx' % time.strftime('%Y-%m-%d_%H-%M-%S'))
    shutil.copy2(path, dest)
    old = sorted(f for f in os.listdir(BACKUPS) if f.startswith('CARDS_') and f.endswith('.xlsx'))
    for f in old[:-KEEP_BACKUPS]:
        os.remove(os.path.join(BACKUPS, f))
    return dest


def save(wb, path=SHEET):
    """Backs the sheet up, puts the dropdowns back, asks Excel to recalculate on open, and saves."""
    dest = backup(path)
    restore_dropdowns(wb)
    wb.calculation.fullCalcOnLoad = True
    tmp = path + '.saving'
    wb.save(tmp)
    try:
        os.replace(tmp, path)   # PermissionError here = Excel has the sheet open
    except OSError:
        os.remove(tmp)
        raise
    return dest
