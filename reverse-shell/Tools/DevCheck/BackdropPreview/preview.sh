#!/bin/bash
# Renders the battle backdrop painter (BackdropPainterCore) to a PNG without Unity, for quick visual checks.
#   bash Tools/DevCheck/BackdropPreview/preview.sh [out.png]
set -e
U="/c/Program Files/Unity/Hub/Editor/2019.4.14f1/Editor/Data"
D="$(cd "$(dirname "$0")" && pwd)"
P="$(cd "$D/../../.." && pwd)"
OUT_PNG="${1:-$D/.build/backdrop.png}"
mkdir -p "$D/.build"
w() { cygpath -w "$1"; }
TOOLS="$(w "$P/Tools/SpriteForge")"
python -I -c "
import sys,struct; sys.path.insert(0,sys.argv[1])
from pngtool import read_png
w,h,px=read_png(sys.argv[2])
open(sys.argv[3],'wb').write(struct.pack('<ii',w,h)+bytes(v for row in px for p in row for v in p))
" "$TOOLS" "$(w "$P/Assets/_Project/Art/Tiles/ruins.png")" "$(w "$D/.build/atlas.raw")"
API="$U/MonoBleedingEdge/lib/mono/4.7.1-api"
"$U/Tools/Roslyn/csc.exe" -nologo -nostdlib -langversion:7.3 -r:"$(w "$API/mscorlib.dll")" -r:"$(w "$API/System.dll")" \
  -out:"$(w "$D/.build/preview.exe")" "$(w "$D/Program.cs")" "$(w "$P/Assets/_Project/Scripts/View/BackdropPainterCore.cs")"
"$U/MonoBleedingEdge/bin/mono.exe" "$D/.build/preview.exe" "$(w "$D/.build/atlas.raw")" "$(w "$D/.build/out.raw")"
python -I -c "
import sys; sys.path.insert(0,sys.argv[1])
from pngtool import write_png
W,H,s=480,270,3
d=open(sys.argv[2],'rb').read()
rows=[[(d[(y*W+x)*3],d[(y*W+x)*3+1],d[(y*W+x)*3+2],255) for x in range(W) for _ in range(s)] for y in range(H) for _ in range(s)]
write_png(sys.argv[3],W*s,H*s,rows)
" "$TOOLS" "$(w "$D/.build/out.raw")" "$(w "$OUT_PNG")"
echo "wrote $OUT_PNG"
