"""
CardForge / collection art: builds the card-collection art in Assets/_Project/Resources/Cards/ from the third-party
packs in Assets/_Project/Art/ThirdParty/ (the user's picks, 2026-10-27):

  Packs/Pack_<area id>.png   the card pack for each area (argametina "Free Card Packs", CardPacks/CardPacks.png:
                             9 colours A-I across, 20 designs 1-20 down, 80 x 154 each)
                             city 6F, tunnel 11E, chamber 2C, sandbox 18A
  Packs/Pack_any_<A-I>.png   row 18 in every colour: areas without their own pack pick one at random
  ArcanaIcon_Sun / Moon / Star   the user's Raven Fantasy Icons picks (Sun fb990, Moon fb978, Star fb986), for the
                             centre window of our own card format (CardView): 34 px with the same whole plum outline as
                             every card icon (card_forge.framed), so they match every other card

    python Tools/CardForge/collection_art.py

Change a pick below and run it again (Unity reimports the files).
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'SpriteForge'))
from pngtool import read_png, write_png  # noqa: E402
sys.path.insert(0, HERE)
import card_forge  # noqa: E402

THIRD = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'ThirdParty')
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Resources', 'Cards')

# ---------------------------------------------------------------- Picks

AREA_PACKS = {'city': '6F', 'tunnel': '11E', 'chamber': '2C', 'sandbox': '18A'}
ANY_ROW = 18
PACK_COLUMNS_X = [5, 100, 195, 290, 385, 480, 575, 669, 764]
PACK_W, PACK_H, PACK_TOP, PACK_STEP = 80, 154, 3, 157

ARCANA = {'Sun': 990, 'Moon': 978, 'Star': 986}   # Raven Fantasy Icons (32x32/fb<n>.png), the user's picks


def crop(px, x, y, w, h):
    return [row[x:x + w] for row in px[y:y + h]]


def pack_cell(sheet, cell):
    row, col = int(cell[:-1]), 'ABCDEFGHI'.index(cell[-1])
    return crop(sheet, PACK_COLUMNS_X[col], PACK_TOP + (row - 1) * PACK_STEP, PACK_W, PACK_H)


def main():
    os.makedirs(os.path.join(OUT, 'Packs'), exist_ok=True)

    _, _, sheet = read_png(os.path.join(THIRD, 'CardPacks', 'CardPacks.png'))
    for area, cell in AREA_PACKS.items():
        write_png(os.path.join(OUT, 'Packs', 'Pack_%s.png' % area), PACK_W, PACK_H, pack_cell(sheet, cell))
    for letter in 'ABCDEFGHI':
        write_png(os.path.join(OUT, 'Packs', 'Pack_any_%s.png' % letter), PACK_W, PACK_H, pack_cell(sheet, '%d%s' % (ANY_ROW, letter)))

    for name, n in ARCANA.items():
        card_forge.save(card_forge.framed(card_forge.raven_icon(n)), 'ArcanaIcon_%s.png' % name)

    print('wrote packs and arcana emblems into %s' % os.path.relpath(OUT, ROOT))


if __name__ == '__main__':
    main()
