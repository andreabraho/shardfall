#!/usr/bin/env bash
# Launches the real Godot binary, not the winget shim.
#
# The shim at WinGet/Links/godot.exe has no GodotSharp/ beside it, and Godot resolves its
# C# API assemblies relative to the executable — so the shim fails with
# ".NET: Assemblies not found" and crashes. See docs/04 §2.
#
# Usage:  scripts/godot.sh [godot args...]
#         scripts/godot.sh --headless --path game --import --quit-after 400
set -euo pipefail

GODOT_HOME="${GODOT_HOME:-$HOME/AppData/Local/Microsoft/WinGet/Packages/GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe/Godot_v4.7.2-stable_mono_win64}"
GODOT_BIN="$GODOT_HOME/Godot_v4.7.2-stable_mono_win64_console.exe"

if [[ ! -x "$GODOT_BIN" ]]; then
  echo "Godot not found at: $GODOT_BIN" >&2
  echo "Set GODOT_HOME to your Godot folder (the one containing GodotSharp/)." >&2
  exit 1
fi

exec "$GODOT_BIN" "$@"
