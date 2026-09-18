# Asset licences

**Rule (doc 00 §2): every third-party asset gets an entry here the moment it enters the
repo — before it is committed.** No exceptions, including "temporary" placeholders. An
unlicensed asset discovered near release is a release-blocking problem; caught at import it
is a thirty-second decision.

For each asset record: what it is, where it came from, the exact licence, and whether
attribution is required in-game.

| Asset | Path | Source | Licence | Attribution required | Added |
|---|---|---|---|---|---|
| _(none yet — all visuals are procedurally generated primitives, see `game/scripts/Visual/VisualRegistry.cs`)_ | | | | | |

## Currently clean

The project contains **no third-party assets**. Every visual is a Godot primitive mesh
generated at runtime from `game/data/tables/visuals.json`, and all code is original.

## When art starts (Phase 9)

Acceptable licences for a commercial release: CC0, CC-BY (with attribution), and commercial
asset-store licences (Synty, KayKit, Quaternius). **Not acceptable:** CC-BY-NC, CC-BY-SA
(viral), "free for personal use", anything without an explicit licence, and anything
extracted from another game.

Preferred sources are listed in
[docs/04-tech-stack-and-infrastructure.md](docs/04-tech-stack-and-infrastructure.md) §2.
