# Project Shardfall — a single-player action RPG in the spirit of Metin2

A stylized oriental-fantasy action RPG for PC, built solo (you + Claude) in **Godot 4 / C#**.
It takes the parts of Metin2 that were actually fun — metin stone hunting, chunky
skill combat, the upgrade/socket item chase, oriental art direction — and rebuilds
them as a **complete, offline, 15–25 hour single-player campaign** with a real
difficulty curve instead of an MMO grind curve.

> **Codename:** `Shardfall` (placeholder, rename freely — Mount Shardfall is a Metin2 map name,
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

## Status

Pre-production. Nothing implemented yet. Start at
[docs/07-what-i-need-from-you.md](docs/07-what-i-need-from-you.md) — it lists the
handful of things that unblock Phase 0.
