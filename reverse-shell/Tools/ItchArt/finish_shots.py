"""
Finishes the screenshot run's PNGs (itch/screenshots/new/<run>/, made by the development player's -shots run,
Game.World.ShotTour) for the store page and the README:
  - paints out the "Development Build" label in the bottom-right corner (a development player always draws it): the
    label's box is filled with the strip of picture just to its left, so the texture carries on;
  - copies each shot to where it's used, under the names the store page and README already use.
Old files are kept once in itch/screenshots/old/ (git-ignored).

    py -3.9 Tools/ItchArt/finish_shots.py run10 [run10alt]

The optional second run is one made with -altLook (Settings > Machine Look: Tech Dungeon): its battle becomes
09-machine-look-tech-dungeon. Afterwards the README's release pictures are copied to builds/release-assets/ (the files
the GitHub release `demo-v1.0` carries).
"""
import os
import shutil
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, '..', '..', '..'))
NEW = os.path.join(REPO, 'itch', 'screenshots', 'new')
OLD = os.path.join(REPO, 'itch', 'screenshots', 'old')
LABEL = (1800, 1055, 1920, 1077)   # the label and its shadow, at 1920 x 1080

SHOTS = os.path.join(REPO, 'itch', 'screenshots')
HELP = os.path.join(REPO, 'itch', 'helper-images')
README = os.path.join(REPO, '.github', 'readme')

# shot name -> [(folder, file name)]
TARGETS = {
    '01-title-screen': [(SHOTS, '01-title-screen.png'), (README, '01-title-screen.png')],
    '02-overgrown-forest': [(SHOTS, '02-overgrown-forest.png'), (README, '02-overgrown-forest.png')],
    '03-pause-menu': [(SHOTS, '03-pause-menu.png'), (README, '03-pause-menu.png')],
    '04-pre-combat': [(SHOTS, '04-pre-combat.png'), (README, '04-pre-combat.png')],
    '05-unit-inspect': [(SHOTS, '05-unit-inspect.png'), (README, '05-unit-inspect.png')],
    'units-page': [(SHOTS, '06-units-page.png'), (HELP, 'units-page.png'), (README, '06-units-page.png')],
    'ashe-leng-storm': [(SHOTS, '07-ashe-leng-storm.png'), (HELP, 'ashe-leng-storm.png'), (README, '07-ashe-leng-storm.png')],
    'combat-turn-playing-out': [(SHOTS, '08-turn-playing-out.png'), (HELP, 'combat-turn-playing-out.png'),
                                (README, 'combat-turn-playing-out.png')],
    'story-office': [(HELP, 'story-office.png'), (README, 'story-office.png')],
    'story-everlasting': [(HELP, 'story-everlasting.png'), (README, 'story-everlasting.png')],
    'story-awakening': [(HELP, 'story-awakening.png'), (README, 'story-awakening.png')],
    'combat-battle': [(HELP, 'combat-battle.png'), (README, 'combat-battle.png')],
    'combat-card-detail': [(HELP, 'combat-card-detail.png'), (README, 'combat-card-detail.png')],
    'combat-card-merge': [(HELP, 'combat-card-merge.png'), (README, 'combat-card-merge.png')],
    'combat-break': [(HELP, 'combat-break.png'), (README, 'combat-break.png')],
    # Chapter One's reflection and ending (story shots: spoilers, so they are extras, not in the README).
    'story-reflection': [(SHOTS, '14-reflection.png'), (HELP, 'story-reflection.png')],
    'story-reflection-core': [(HELP, 'story-reflection-core.png')],
    'story-reflection-card': [(HELP, 'story-reflection-card.png')],
    'story-ending-cage': [(SHOTS, '15-ending-cage.png'), (HELP, 'story-ending-cage.png')],
    'story-ending-machine': [(SHOTS, '16-ending-machine.png'), (HELP, 'story-ending-machine.png')],
    # The finished areas (City Ruins and the [bracketed] ones): overworld shots.
    '10-city-ruins': [(SHOTS, '10-city-ruins.png'), (README, '10-city-ruins.png')],
    '11-overgrowth': [(SHOTS, '11-overgrowth.png'), (README, '11-overgrowth.png')],
    '12-conservatory': [(SHOTS, '12-conservatory.png'), (README, '12-conservatory.png')],
    '13-greenhouse': [(SHOTS, '13-greenhouse.png'), (README, '13-greenhouse.png')],
}


def clean(img):
    x0, y0, x1, y1 = LABEL
    w = x1 - x0
    strip = img.crop((x0 - w - 12, y0, x0 - 12, y1))   # 12 px clear of whatever ends at the label (the version text)
    img.paste(strip, (x0, y0))
    return img


# From the -altLook run: the battle in Tech Dungeon's look.
ALT_TARGETS = {
    'combat-battle': [(SHOTS, '09-machine-look-tech-dungeon.png'), (README, '09-machine-look-tech-dungeon.png')],
}

# The README pictures the GitHub release carries (README.md links them from releases/download/readme-images/).
RELEASE = os.path.join(REPO, 'builds', 'release-assets')


def main():
    run = sys.argv[1] if len(sys.argv) > 1 else 'run2'
    finish(os.path.join(NEW, run), TARGETS)
    if len(sys.argv) > 2:
        finish(os.path.join(NEW, sys.argv[2]), ALT_TARGETS)
    release()


def release():
    import re
    readme = open(os.path.join(REPO, 'README.md'), encoding='utf-8').read()
    names = sorted(set(re.findall(r'releases/download/(?:demo-v1\.0|readme-images)/([\w.-]+)', readme)))
    os.makedirs(RELEASE, exist_ok=True)
    for n in names:
        src = os.path.join(README, n)
        if os.path.exists(src):
            shutil.copy2(src, os.path.join(RELEASE, n))
            print('release', n)
        else:
            print('release: missing', src)


def finish(src, targets):
    os.makedirs(OLD, exist_ok=True)
    for name, places in targets.items():
        path = os.path.join(src, name + '.png')
        if not os.path.exists(path):
            print('missing', path)
            continue
        img = clean(Image.open(path).convert('RGB'))
        img.save(os.path.join(src, name + '.clean.png'))
        for folder, file in places:
            os.makedirs(folder, exist_ok=True)
            dest = os.path.join(folder, file)
            keep = os.path.join(OLD, os.path.basename(folder) + '_' + file)
            if os.path.exists(dest) and not os.path.exists(keep):
                shutil.copy2(dest, keep)
            img.save(dest)
            print('wrote', dest)


if __name__ == '__main__':
    main()
