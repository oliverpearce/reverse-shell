"""The Google Sheets version of the card sheet: plain-word columns and dropdown chips (cards_to_gsheet.py writes it,
gsheet_to_cards.py reads a downloaded copy back into design/CARDS.xlsx). This file holds what both share: the tab
layouts and the plain words for the effect language.

Abilities are one row each on the Abilities tab (card ID, when it applies, what it does and its numbers), so every
part is a dropdown chip or a number instead of a line of the effect language.
"""

FOLDER_NAME = 'google'
EXPORT_NAME = 'Reverse Shell Cards (upload this).xlsx'

# ---------------------------------------------------------------- the Cards tab
# (plain header, width) in order. The importer maps them back to CARDS.xlsx's columns. Pool, Aims at and Level must
# stay the 3rd, 6th and 7th columns: the Abilities tab's text formulas look them up by position. The "(in game)"
# columns are formulas: the game's own card text, worked out from the Abilities rows.
CARD_COLUMNS = [
    ('Card ID', 10), ('Card name', 22), ('Pool', 10), ('Card type', 12), ('Rarity', 13), ('Aims at', 16),
    ('Level (1-20)', 9), ('Damage kind', 14), ('Break', 8), ('Card text (in game)', 62), ('Stage', 12),
    ('Ult resonance', 13), ('Enemy Ult?', 11), ('Other tags', 12), ('Art (Raven #)', 11),
    ('Rank II text (in game)', 62), ('Rank III text (in game)', 62), ('Pixel effect', 16), ('Notes', 60),
    ('Unity asset (leave as is)', 30),
]
TEXT_COLUMNS = ['Card text (in game)', 'Rank II text (in game)', 'Rank III text (in game)']

TARGETS = ['Single enemy', 'All enemies', 'Random enemy', 'Self', 'Single ally', 'All allies', 'Random ally']
# The card's own target as the game writes it in card text (CardText.TargetPhrase).
AIM_PHRASE = {'Single enemy': '1 enemy', 'All enemies': 'all enemies', 'Random enemy': 'a random enemy', 'Self': 'self',
              'Single ally': 'the weakest ally', 'All allies': 'all allies', 'Random ally': 'a random ally'}
YES_NO = ['Yes', 'No']

# ---------------------------------------------------------------- the Abilities tab
ABILITY_COLUMNS = [
    ('Card ID', 10), ('Card name (auto)', 20), ('When', 18), ('Does', 24), ('Strength / amount', 11), ('Status', 22),
    ('Who', 20), ('Turns', 7), ('Times', 7), ('Chance %', 9), ('Bonus vs status', 18), ('Bonus % per stack', 10),
    ('Clear that status after?', 11), ('Low-HP bonus %', 10), ('Then give status', 18), ('For turns', 8),
    ('Pool (auto)', 9), ('Rank I text (auto)', 46), ('Rank II text (auto)', 46), ('Rank III text (auto)', 46),
]

# The game's numbers for card text (CombatConfig defaults; the game ignores the sheet's Settings copies).
GAME = {'power': 0.25, 'spread': 0.6, 'rank2': 2, 'rank3': 5, 'cap': 20}

# ---------------------------------------------------------------- the Decks and Areas & fights tabs
RESONANCES = ['Basic', 'Arc', 'Root', 'Cryo', 'Cinder', 'Prism']
STARTER_SLOTS = 6
FIGHT_COLUMNS = [('Area ID (leave as is)', 22), ('Area', 22), ('Pack (whole area)', 14), ('Fight #', 8),
                 ('Enemy 1', 18), ('Enemy 2', 18), ('Enemy 3', 18), ('Level', 8), ('Boss?', 8), ('Story fight', 26)]
MAX_FIGHT_ENEMIES = 3

WHEN = {'base': 'Always (Rank I)', 'r2': 'Added at Rank II', 'r3': 'Added at Rank III'}
WHEN_BACK = {v: k for k, v in WHEN.items()}

# effect kind -> plain words (the Does chip)
DOES = {
    'dmg': 'Deal damage',
    'heal': 'Heal',
    'shield': 'Shield',
    'counter': 'Counter-attack',
    'status': 'Give a status',
    'cover': 'Cover (take hits for)',
    'interrupt': 'Interrupt their move',
    'interrupt attack': 'Interrupt an attack',
    'cleanse': 'Cleanse debuffs',
    'ult': 'Gain Ult points',
    'ap': 'Gain AP next turn',
    'raise': 'Raise a status chance',
}
DOES_BACK = {v: k for k, v in DOES.items()}

# effect target -> plain words (the Who chip)
WHO = {
    'card': "The card's target",
    'self': 'Self',
    'enemy': 'One enemy',
    'enemies': 'All enemies',
    'random': 'A random enemy',
    'ally': 'The weakest ally',
    'allies': 'All allies',
    'randomally': 'A random ally',
}
WHO_BACK = {v: k for k, v in WHO.items()}
WHO_PHRASE = {'self': 'self', 'enemy': '1 enemy', 'enemies': 'all enemies', 'random': 'a random enemy',
              'ally': 'the weakest ally', 'allies': 'all allies', 'randomally': 'a random ally'}

WEAK_COLUMNS = ['Physical', 'Incantation', 'Arc', 'Root', 'Cryo', 'Cinder', 'Prism']

HELP = [
    ('Reverse Shell card sheet (Google Sheets version)', ''),
    ('', ''),
    ('How it works', 'Edit the tabs, then File > Download > Microsoft Excel (.xlsx), put the file in the design/google '
                     'folder and double-click design/google_import.bat. It checks everything, copies it into '
                     'CARDS.xlsx (after a backup) and the area files, and Unity imports it.'),
    ('Chips', "Click a chip to pick from its list. Typing something that isn't on the list is rejected. Grey columns "
              'marked (auto) or (in game) fill themselves in: leave them.'),
    ('', ''),
    ('CARDS TAB', "One row per card, tinted in its pool's colour."),
    ('Card ID', 'Its code, e.g. ARC-21. Must be unique. A new card is a new row with a new ID.'),
    ('Pool', 'Which resonance the card belongs to (or Basic). "Ult" = a Soul / Ult card.'),
    ('Card type', 'Attack, Buff, Debuff, Defense, Counter or Heal (the icon on the card).'),
    ('Aims at', 'Who the card targets when you play it.'),
    ('Level (1-20)', "The card's power. Its main hit / heal = 25% of ATK per level (Lv 4 = 100% ATK). "
                     'Party cards are about 3-6. Merging adds +2 (Rank II) and +5 (Rank III).'),
    ('Damage kind', "Physical or Incantation. An enemy weak to it takes this card's Break."),
    ('Break', 'Break damage at Rank I (0 = none). x2 at Rank II, x3 at Rank III. Only cards that hit deal Break, '
              'once per enemy.'),
    ('Card text (in game)', 'Exactly what the card says in the game at Rank I (the Cards menu), worked out from its '
                            'abilities, level and target. Rank II / III text: what it says once merged in battle. '
                            'Change a Strength or Level and they update.'),
    ('Stage', 'Idea / Designed / In game / Cut. Cut cards are left out of the game.'),
    ('Ult resonance', "Ult cards only: whose resonance kit it belongs to. Enemy Ult? = Yes for an enemy's Ult."),
    ('Art (Raven #)', "The Raven icon number for the card's art (e.g. 1026). Blank = keep its drawn art."),
    ('Notes', 'Your notes (the old hand-written card lines are kept here as "Design idea").'),
    ('Unity asset', "The card's file name in Unity. Leave it; new cards get one made from their name."),
    ('', ''),
    ('ABILITIES TAB', 'One row per ability. A card can have at most two different kinds of ability.'),
    ('Card ID', "Which card it belongs to (the name fills in). Keep a card's rows together, in order: they become "
                'its sentences in that order.'),
    ('When', "Always (Rank I) = the card's own abilities. Added at Rank II / III = extra abilities when merged."),
    ('Does', 'What happens. Deal damage, Heal, Shield and Counter-attack use Strength; Give a status uses Status.'),
    ('Strength / amount', "Deal damage / Heal / Shield / Counter-attack: % of the card's power. The biggest one on a "
                          'card is its full power; 50 next to a 100 = half as strong. Ult points / AP: how many. '
                          'Raise a status chance: how many % points.'),
    ('Status', 'Give a status / Raise a status chance: which one.'),
    ('Who', "Who this ability hits. The card's target = whoever the card aims at."),
    ('Turns', 'How long a status, shield or cover lasts (blank = 2 for statuses, 1 for shield / cover).'),
    ('Times', 'Deal damage: split into this many hits. Give a status: stacks. Counter-attack: how many times.'),
    ('Chance %', 'Give a status: the chance it lands (blank = 100).'),
    ('Bonus vs status', 'Deal damage: hits harder on a target with this status, by Bonus % per stack. '
                        'Clear that status after? = Yes removes it.'),
    ('Low-HP bonus %', 'Counter-attack: extra % while the holder is below half HP.'),
    ('Then give status', 'Counter-attack: also gives the attacker this status, For turns.'),
    ('Rank I / II / III text', "This ability's sentence at each rank, as the game writes it."),
    ('', ''),
    ('DECKS TAB', "Main card: each resonance's innate card (pick a card of that pool). Starter cards: owned from a "
                  'new game. Pick by card name.'),
    ('AREAS & FIGHTS TAB', 'Grouped by area. Pack (whole area) = the pack a won fight there gives (same on every row '
                           'of the area). Each fight (an E on the map): up to 3 enemies, their level, and Boss? '
                           '(3x HP, more Break). New fights are added in the Level Designer.'),
    ('ENEMIES TAB', "Innate card = a card the enemy always has (played Innate +levels higher). Break = its grey bar "
                    "(0 = can't be broken; a boss gets Boss extra Break more). Weak columns: Yes = weak to it."),
    ('PACKS TAB', 'One pack per chapter. Common + Rare + Epic + Legendary must add up to 100%.'),
    ('SETTINGS TAB', 'Numbers marked "Yes, game" go straight into the game. The others drive the sheet maths.'),
]
