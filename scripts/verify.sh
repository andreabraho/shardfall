#!/usr/bin/env bash
# Everything CI checks, locally. Run before pushing.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== build =="
dotnet build Shardfall.sln --nologo

echo "== validate content =="
dotnet run --project src/Shardfall.Tools --no-build -- validate

echo "== generated ids current? =="
dotnet run --project src/Shardfall.Tools --no-build -- codegen --check

echo "== tests =="
dotnet test Shardfall.sln --no-build --nologo

echo "== godot project builds =="
dotnet build game/Shardfall.Game.csproj --nologo

echo "== godot import =="
scripts/godot.sh --headless --path game --import --quit-after 400 >/dev/null

echo
echo "All checks passed."
