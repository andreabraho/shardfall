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

echo "== tests =="
dotnet test Kiln.sln --no-build --nologo

echo "== godot project builds =="
dotnet build game/Kiln.Game.csproj --nologo

echo "== godot import =="
scripts/godot.sh --headless --path game --import --quit-after 400 >/dev/null

echo
echo "All checks passed."
