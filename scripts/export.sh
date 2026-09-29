#!/usr/bin/env bash
# Builds the Windows release for testers and zips it.
#
#   scripts/export.sh            -> export/Temins-v<version>-windows.zip
#
# Needs the Godot 4.7.2 mono export templates in %APPDATA%/Godot/export_templates/4.7.2.stable.mono
# (Editor -> Manage Export Templates, or the .tpz from the Godot 4.7.2 release page).
# The version comes from game/project.godot (application/config/version).
set -euo pipefail

# MSBuild would leave a build node running that holds this script's output open for ever.
export MSBUILDDISABLENODEREUSE=1
# Likewise the shared C# compiler server.
export UseSharedCompilation=false

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="$(sed -n 's/^config\/version="\(.*\)"$/\1/p' "$ROOT/game/project.godot")"
NAME="Temins-v${VERSION}-windows"
OUT="$ROOT/export/windows"
STAGE="$ROOT/export/$NAME"

if [[ -z "$VERSION" ]]; then
  echo "No config/version in game/project.godot." >&2
  exit 1
fi

TEMPLATES="$APPDATA/Godot/export_templates/4.7.2.stable.mono"
if [[ ! -f "$TEMPLATES/windows_release_x86_64.exe" ]]; then
  echo "Export templates missing: $TEMPLATES" >&2
  exit 1
fi

echo "== Temins v$VERSION"

rm -rf "$OUT" "$STAGE" "$ROOT/export/$NAME.zip"
mkdir -p "$OUT"

# The export saves project.godot through the editor, which strips its comments: put the
# hand-written file back whatever happens.
cp "$ROOT/game/project.godot" "$ROOT/export/project.godot.keep"
trap 'mv -f "$ROOT/export/project.godot.keep" "$ROOT/game/project.godot"' EXIT

# Import first so a fresh checkout has its .godot cache; then the release export, which
# publishes the C# assembly in Release beside the exe.
"$ROOT/scripts/godot.sh" --headless --path "$ROOT/game" --import --quit-after 400 >/dev/null 2>&1 || true
"$ROOT/scripts/godot.sh" --headless --path "$ROOT/game" --export-release "Windows Desktop" "$OUT/Temins.exe"

if [[ ! -f "$OUT/Temins.exe" ]]; then
  echo "Export failed: no Temins.exe." >&2
  exit 1
fi

# What testers get: the game, its .NET folder, the credits the licences ask for, and the notes.
mkdir -p "$STAGE"
cp -r "$OUT"/. "$STAGE"/
cp "$ROOT/ASSET-LICENSES.md" "$STAGE/ASSET-LICENSES.md"
sed "s/{VERSION}/$VERSION/g" "$ROOT/release/LEGGIMI.txt" > "$STAGE/LEGGIMI.txt"
sed "s/{VERSION}/$VERSION/g" "$ROOT/release/FEEDBACK.txt" > "$STAGE/FEEDBACK.txt"

# No debug symbols for testers.
find "$STAGE" -name "*.pdb" -delete

(cd "$ROOT/export" && powershell -NoProfile -Command "Compress-Archive -Path '$NAME' -DestinationPath '$NAME.zip' -Force")

echo "== $(du -sh "$ROOT/export/$NAME.zip" | cut -f1)  export/$NAME.zip"
