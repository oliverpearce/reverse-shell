"""Editable story text -> the game's Resources/Story folder.

    py Tools/StoryForge/sync_story.py [--force]

The editable copies of the story text (World.txt, Chapter1.txt, Guide.txt, UI.txt) live in design/story/ at the repo
root, which is git-ignored. The game reads the copies in Assets/_Project/Resources/Story/, which are tracked so the
repo still builds. This copies every changed .txt from design/story/ into Resources/Story/.

A Resources file that is newer than its design/story copy (changed in Unity or by a code edit) is not overwritten;
it is listed instead, so it can be copied back first. --force overwrites it anyway.
"""
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCE = os.path.normpath(os.path.join(HERE, '..', '..', '..', 'design', 'story'))
TARGET = os.path.normpath(os.path.join(HERE, '..', '..', 'Assets', '_Project', 'Resources', 'Story'))


def read(path):
    with open(path, 'rb') as f:
        return f.read()


def main():
    force = '--force' in sys.argv[1:]
    if not os.path.isdir(SOURCE):
        print('No story folder at ' + SOURCE)
        return 1
    copied, skipped = [], []
    for name in sorted(os.listdir(SOURCE)):
        if not name.endswith('.txt'):
            continue
        src, dst = os.path.join(SOURCE, name), os.path.join(TARGET, name)
        if os.path.exists(dst):
            if read(src) == read(dst):
                continue
            if not force and os.path.getmtime(dst) > os.path.getmtime(src):
                skipped.append(name)
                continue
        shutil.copy2(src, dst)
        copied.append(name)
    for name in copied:
        print('Copied ' + name)
    for name in skipped:
        print('Skipped ' + name + ': the game\'s copy is newer (copy it back to design/story, or use --force)')
    if not copied and not skipped:
        print('Story text already up to date.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
