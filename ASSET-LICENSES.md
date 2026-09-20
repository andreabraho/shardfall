# Asset licences

**Rule (doc 00 §2): every third-party asset gets an entry here the moment it enters the
repo — before it is committed.** No exceptions, including "temporary" placeholders. An
unlicensed asset discovered near release is a release-blocking problem; caught at import it
is a thirty-second decision.

For each asset record: what it is, where it came from, the exact licence, and whether
attribution is required in-game.

| Asset | Path | Source | Licence | Attribution required | Added |
|---|---|---|---|---|---|
| KayKit Dungeon Remastered 1.0 — 14 models (walls, floor, stairs, pillar, column, rubble, banner, torches) | `game/assets/kaykit/dungeon/` | [github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0](https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0) | **CC0 1.0** | No | 2026-09-20 |
| KayKit Character Pack: Adventurers 1.0 — Knight (the player), Barbarian (heavy enemies) | `game/assets/kaykit/characters/` | [github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0) | **CC0 1.0** | No | 2026-09-20 |
| KayKit Character Pack: Skeletons 1.0 — Warrior (humanoid enemies), Minion (small enemies), Rogue (the Heart Keeper), Mage (the Vault Chorister) | `game/assets/kaykit/characters/` | [github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0) | **CC0 1.0** | No | 2026-09-20 |

All three are by Kay Lousberg (www.kaylousberg.com). The packs' own `LICENSE.txt` files are
kept verbatim beside the models, in `game/assets/kaykit/`, and each states Creative Commons
Zero with a link to the deed, explicitly permitting commercial use and explicitly making
credit optional.

**The licence text was read before anything was committed, not the download page.** GitHub's
own licence detector reports `NOASSERTION` for all three repositories, which means only that
its matcher did not recognise the file's wording — the files themselves name CC0 plainly.

Crediting is not required, and we will do it anyway in the credits screen when there is one:
an author who gives work away for nothing is the cheapest possible thing to be generous
about.

**Only the models actually used are in the repo.** The packs together hold over two hundred
models; carrying the rest would be paying repository weight for things no scene references.
Adding another is a copy, and another row here.

## What is still procedural

Everything with no row above. Cliffs, boulders, shrubs and marker posts stay Godot primitives
because the pack is a *dungeon* pack and has no outdoor geometry — a rubble pile standing in
for a cliff would look worse than the honest grey box it replaced.

## When art starts (Phase 9)

Acceptable licences for a commercial release: CC0, CC-BY (with attribution), and commercial
asset-store licences (Synty, KayKit, Quaternius). **Not acceptable:** CC-BY-NC, CC-BY-SA
(viral), "free for personal use", anything without an explicit licence, and anything
extracted from another game.

Preferred sources are listed in
[docs/04-tech-stack-and-infrastructure.md](docs/04-tech-stack-and-infrastructure.md) §2.
