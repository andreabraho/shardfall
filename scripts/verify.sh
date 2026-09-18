#!/usr/bin/env bash
# Everything CI checks, locally. Run before pushing.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== build =="
dotnet build Sohan.sln --nologo

echo "== validate content =="
dotnet run --project src/Sohan.Tools --no-build -- validate

echo "== generated ids current? =="
dotnet run --project src/Sohan.Tools --no-build -- codegen --check

echo "== tests =="
dotnet test Sohan.sln --no-build --nologo

echo "== godot project builds =="
dotnet build game/Sohan.Game.csproj --nologo

echo "== godot import =="
scripts/godot.sh --headless --path game --import --quit-after 400 >/dev/null

echo
echo "All checks passed."
