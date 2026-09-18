# Project Kiln — a single-player action RPG in the spirit of Metin2

A stylized oriental-fantasy action RPG for PC, built solo (you + Claude) in **Godot 4 / C#**.
It takes the parts of Metin2 that were actually fun — metin stone hunting, chunky
skill combat, the upgrade/socket item chase, oriental art direction — and rebuilds
them as a **complete, offline, 15–25 hour single-player campaign** with a real
difficulty curve instead of an MMO grind curve.

> **Codename:** `Kiln` (placeholder, rename freely — Mount Kiln is a Metin2 map name,
> so pick something original before any public release; see the IP section in the overview).

## Documentation map

| Doc | What's in it |
|---|---|
| [docs/00-overview-and-decisions.md](docs/00-overview-and-decisions.md) | Pitch, scope tiers, IP/legal ground rules, time & effort estimate, open decisions |
| [docs/01-metin2-analysis.md](docs/01-metin2-analysis.md) | Full teardown of Metin2's systems, with keep / change / cut verdicts |
| [docs/02-singleplayer-redesign.md](docs/02-singleplayer-redesign.md) | How every MMO system is converted to single-player, difficulty model, pacing |
| [docs/03-requirements.md](docs/03-requirements.md) | Functional + non-functional + content requirements (the spec) |
| [docs/04-tech-stack-and-infrastructure.md](docs/04-tech-stack-and-infrastructure.md) | Engine choice, every program/app/service, repo layout, CI, hardware |
| [docs/05-roadmap.md](docs/05-roadmap.md) | 14 phases, every task, who does it (me or you), exit criteria |
| [docs/06-data-model-and-formulas.md](docs/06-data-model-and-formulas.md) | Stats, damage math, XP curve, item/mob/skill/quest schemas, save format |
| [docs/07-what-i-need-from-you.md](docs/07-what-i-need-from-you.md) | Consolidated checklist of your decisions, installs, accounts and assets |

## Running it

Double-click **`play.bat`** (builds the C#, then launches the game), or **`edit.bat`**
to open the Godot editor and press F5.

Both resolve the real Godot executable. Do **not** launch through the winget shim at
`%LOCALAPPDATA%\Microsoft\WinGet\Links\godot.exe` — Godot looks for its C# API assemblies
next to the executable and that folder has none, so it fails with
".NET: Assemblies not found" and crashes. If Godot lives somewhere else, set `GODOT_EXE`.

### Controls (Phase 1)

| Input | Action |
|---|---|
| Left click | Move (hold for continuous movement) |
| Mouse wheel | Zoom |
| Z / C, or middle-drag | Rotate camera |
| F3 | Debug overlay |
| F4 | Toggle WASD (alternative scheme) |

Skills, flask and the defensive ability are bound but not implemented until Phases 2–3.

### Checks

`scripts/verify.sh` runs everything CI runs: build, content validation, generated-id check,
tests, Godot build and headless import.

## Status

Phase 0 (foundations) and Phase 1 (click-to-move, camera, greybox arena) are implemented.
Phase 1 is awaiting the MOV-08 feel review: run `play.bat` and judge whether click-to-move
feels responsive. What is needed from you at any point is tracked in
[docs/07-what-i-need-from-you.md](docs/07-what-i-need-from-you.md).
