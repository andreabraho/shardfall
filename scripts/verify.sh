#!/usr/bin/env bash
# Everything CI checks, locally. Run before pushing.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== build =="
dotnet build Kiln.sln --nologo

echo "== validate content =="
dotnet run --project src/Kiln.Tools --no-build -- validate

echo "== generated ids current? =="
dotnet run --project src/Kiln.Tools --no-build -- codegen --check

echo "== campaign pacing =="
dotnet run --project src/Kiln.Tools --no-build -- simulate

echo "== campaign economy =="
dotnet run --project src/Kiln.Tools --no-build -- economy

echo "== strings =="
# Every content key has English, and no interpolated string is handed to L10n (UIX-06).
dotnet run --project src/Kiln.Tools --no-build -- strings

echo "== tests =="
dotnet test Kiln.sln --no-build --nologo

echo "== godot project builds =="
dotnet build game/Kiln.Game.csproj --nologo

echo "== godot import =="
# A throwaway save folder: the autoload runs here too, and it must neither read nor write the
# player's own saves.
save_dir="$(mktemp -d)"
command -v cygpath >/dev/null && save_dir="$(cygpath -w "$save_dir")"
KILN_SAVE_DIR="$save_dir" scripts/godot.sh --headless --path game --import --quit-after 400 >/dev/null

echo
echo "All checks passed."
