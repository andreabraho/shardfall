#!/usr/bin/env bash
# Builds a Blender model for the game (PIP-01).
#
#   scripts/blender.sh <model> [render-folder]
#
# <model> is art/blender/models/<model>.py. The .glb goes to game/assets/temins/…; with a
# render folder, four views of it go there too.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BLENDER="${BLENDER:-/c/Program Files/Blender Foundation/Blender 5.0/blender.exe}"

if [[ ! -x "$BLENDER" ]]; then
  echo "Blender not found at: $BLENDER (set BLENDER)" >&2
  exit 1
fi

args=(-- "$1")
[[ $# -ge 2 ]] && args+=(--render "$2")

"$BLENDER" -b --factory-startup --python-exit-code 1 --python "$ROOT/art/blender/build.py" "${args[@]}" 2>&1 \
  | grep -E "^\[blender\]|Error|Traceback|File \"|^\s+\w+Error" || true
