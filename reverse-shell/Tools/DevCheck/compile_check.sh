#!/bin/bash
# Compile-checks every project assembly with Unity's own Roslyn (C# 7.3), without opening Unity.
# Mirrors the asmdef layout: Combat.Runtime, Combat.Editor, Game.World, Game.Editor, Combat.Tests.
#
#   bash Tools/DevCheck/compile_check.sh
#
# Needs: Unity 2019.4.14f1 installed via Hub, and the project opened in Unity at least once
# (for Library/ScriptAssemblies/UnityEngine.UI.dll and Unity.Postprocessing.Runtime.dll).
# This only checks that the code compiles; run the EditMode tests in Unity's Test Runner.
set -e
U="/c/Program Files/Unity/Hub/Editor/2019.4.14f1/Editor/Data"
P="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="$P/Tools/DevCheck/.build"
mkdir -p "$OUT"
CSC="$U/Tools/Roslyn/csc.exe"
API="$U/MonoBleedingEdge/lib/mono/4.7.1-api"
LIB="$P/Library/ScriptAssemblies"
w() { cygpath -w "$1"; }
srcs() { find "$1" -name '*.cs' "${@:2}" | while read -r f; do w "$f"; done; }

REFS=()
for f in "$API"/mscorlib.dll "$API"/System.dll "$API"/System.Core.dll "$API"/Facades/netstandard.dll; do REFS+=("-r:$(w "$f")"); done
for f in "$U"/Managed/UnityEngine/*.dll; do REFS+=("-r:$(w "$f")"); done
REFS+=("-r:$(w "$LIB/UnityEngine.UI.dll")")
EDITOR="-r:$(w "$U/Managed/UnityEditor.dll")"
COMMON=(-nologo -nostdlib -noconfig -langversion:7.3 -target:library -nowarn:1701,1702 -define:UNITY_EDITOR -define:UNITY_INCLUDE_TESTS)
POSTFX=()
if [ -f "$LIB/Unity.Postprocessing.Runtime.dll" ]; then POSTFX=(-define:NEONHEX_POSTFX "-r:$(w "$LIB/Unity.Postprocessing.Runtime.dll")"); fi

build() { # name, extra args...
  local name="$1"; shift
  "$CSC" "${COMMON[@]}" "${REFS[@]}" -out:"$(w "$OUT/$name.dll")" "$@" && echo "  ok  $name"
}

S="$P/Assets/_Project"
mapfile -t RT < <(srcs "$S/Scripts" -not -path '*/World/*')
build Combat.Runtime "${RT[@]}"
mapfile -t ED < <(srcs "$S/Editor" -not -path '*/World/*')
build Combat.Editor "$EDITOR" "-r:$(w "$OUT/Combat.Runtime.dll")" "${ED[@]}"
mapfile -t WS < <(srcs "$S/Scripts/World")
build Game.World "${POSTFX[@]}" "-r:$(w "$OUT/Combat.Runtime.dll")" "${WS[@]}"
mapfile -t GE < <(srcs "$S/Editor/World")
build Game.Editor "${POSTFX[@]}" "$EDITOR" "-r:$(w "$OUT/Combat.Runtime.dll")" "-r:$(w "$OUT/Combat.Editor.dll")" "-r:$(w "$OUT/Game.World.dll")" "${GE[@]}"
NUNIT=$(find "$P/Library/PackageCache" -name nunit.framework.dll | head -1)
mapfile -t TS < <(srcs "$S/Tests")
build Combat.Tests "-r:$(w "$NUNIT")" "-r:$(w "$OUT/Combat.Runtime.dll")" "-r:$(w "$OUT/Game.World.dll")" "${TS[@]}"
echo "All assemblies compile."
