# 03 — Requirements

Priority: **MUST** (MVP blocker) · **SHOULD** (v1.0) · **COULD** (Tier C).
Every MUST is an MVP exit gate — the vertical slice is not done until all of them pass.

---

## FR-1 Player character and control

| ID | Req | Pri |
|---|---|---|
| FR-1.1 | Click-to-move with NavMesh pathfinding; hold-to-continue-moving; path recalculates on obstacle | MUST |
| FR-1.2 | Click-to-attack: click an enemy to close distance and begin auto-attack chain; keeps attacking until the target dies, is out of range, or a new order is given | MUST |
| FR-1.3 | 6 hotbar slots (1–6) for skills, 2 for consumables (Q/E), 1 defensive ability (Space) | MUST |
| FR-1.4 | Per-class defensive ability on 8–12 s cooldown (design §2.2) | MUST |
| FR-1.5 | Healing flask: finite charges, refills at shrines/zone entry, cast time interruptible by damage | MUST |
| FR-1.6 | Camera: fixed-pitch third-person follow, mouse-wheel zoom range, Q/E or middle-drag rotate, collision-aware | MUST |
| FR-1.7 | Alternative WASD control scheme in options, feeding the same destination-move core | SHOULD |
| FR-1.8 | Full key rebinding | SHOULD |
| FR-1.9 | Controller support | COULD |

## FR-2 Stats, levelling, classes

| ID | Req | Pri |
|---|---|---|
| FR-2.1 | Four attributes STR/DEX/INT/VIT; derived stats per formulas in doc 06 §2 | MUST |
| FR-2.2 | Levels 1–60 on the published XP curve; attribute + skill points per level | MUST |
| FR-2.3 | Warrior class with two trees (Body / Mental), 8 skills at MVP, 16 at v1.0 | MUST |
| FR-2.4 | Blade, Sura, Shaman classes | SHOULD |
| FR-2.5 | Skill mastery track (Normal → M → G → P) where each rank adds a *mechanical* change, not only numbers | SHOULD |
| FR-2.6 | Free, instant respec of attributes and skills at any shrine | MUST |
| FR-2.7 | Level-scaling policy: fixed zone bands, catch-up XP curve, low-level enemy floor (design §1.1) | MUST |

## FR-3 Combat

| ID | Req | Pri |
|---|---|---|
| FR-3.1 | Damage pipeline exactly as doc 06 §4, including crit, pierce, vs-type bonus, resist, mitigation, difficulty multiplier | MUST |
| FR-3.2 | Telegraphed enemy attacks: readable wind-up + ground decal that fills toward detonation | MUST |
| FR-3.3 | Melee commit window — enemies cannot rotate once a swing starts | MUST |
| FR-3.4 | Status effects: poison, bleed, stun, slow, weaken, vulnerability; stacking and duration rules | MUST |
| FR-3.5 | Hit-stop (2–4 frames), screen shake, floating damage numbers, hit flash | MUST |
| FR-3.6 | Threat/aggro system: proximity + damage + taunt; aggro leash and de-aggro on leash break | MUST |
| FR-3.7 | Enemy roles: Bruiser, Archer, Shielder, Mender, Bomber — each with distinct AI and a kill-priority identity | MUST |
| FR-3.8 | Elite and boss variants with phases, at least 3 mechanics each | MUST |
| FR-3.9 | Death → respawn at last shrine in ≤ 4 s, with the difficulty-appropriate penalty | MUST |

## FR-4 Shard (metin stone) encounters

| ID | Req | Pri |
|---|---|---|
| FR-4.1 | Shard node: fixed spawn point, respawn timer, tier, modifier rolled per respawn | MUST |
| FR-4.2 | Three-phase encounter with wave escalation and a radial pulse AoE (design §3) | MUST |
| FR-4.3 | Phase-3 reclamation cast healing the shard unless interrupted by killing the anchor add | MUST |
| FR-4.4 | Soft arena barrier while the encounter is active; leaving resets the shard | MUST |
| FR-4.5 | Break payoff: shockwave, loot burst, Shard Essence, temporary buff | MUST |
| FR-4.6 | 5 tiers at MVP, 9 at v1.0; modifiers Frenzied / Warded / Venomous / Twin | MUST |
| FR-4.7 | Map overlay showing active nodes, tier and modifier | SHOULD |

## FR-5 Items and inventory

| ID | Req | Pri |
|---|---|---|
| FR-5.1 | Grid inventory with multi-cell items (1×1, 1×2, 1×3); 5 pages; drag/drop; auto-sort | MUST |
| FR-5.2 | Equipment slots: weapon, armour, helmet, shield, boots, bracelet, necklace, earring, 2 rings | MUST |
| FR-5.3 | Rarity tiers (Common → Fine → Rare → Epic → Relic) driving socket count and bonus-line count | MUST |
| FR-5.4 | Bonus lines rolled at drop from slot-appropriate pools, including damage-vs-monster-type | MUST |
| FR-5.5 | Upgrade +0→+9: material cost, displayed success chance, **displayed pity counter**, never destroys or downgrades | MUST |
| FR-5.6 | Sockets: count from rarity, opened with Boring Stone, stones removable intact for a fee | MUST |
| FR-5.7 | Bonus reroll with Mutation Ink, with 1–2 lockable lines | MUST |
| FR-5.8 | Item comparison tooltips against the currently equipped item | MUST |
| FR-5.9 | Auto-loot with rarity filter; mark-as-junk and sell-all-junk | MUST |
| FR-5.10 | Warehouse storage at the Order Hall | SHOULD |
| FR-5.11 | Gambler's Anvil (opt-in downgrade-on-fail) on Shardbound only | COULD |

## FR-6 Enemies, spawning, AI

| ID | Req | Pri |
|---|---|---|
| FR-6.1 | Data-driven enemy definitions: stats, family (animal/undead/devil/orc/human/mystic), role, abilities, drop table, XP | MUST |
| FR-6.2 | Behaviour-tree AI with idle / patrol / chase / attack / reposition / flee states | MUST |
| FR-6.3 | Spawn zones with density caps, respawn timers and distance-based activation | MUST |
| FR-6.4 | 18 enemy types at MVP, ~55 at v1.0 | MUST |
| FR-6.5 | Bestiary/codex filling on kills, revealing weaknesses and drop tables | SHOULD |

## FR-7 World and progression

| ID | Req | Pri |
|---|---|---|
| FR-7.1 | Zone streaming/loading with a loading screen; zone level bands | MUST |
| FR-7.2 | Hub village with blacksmith, merchant, trainer, storage, shrine | MUST |
| FR-7.3 | Shrines: save point, respawn point, fast-travel node, flask refill, respec | MUST |
| FR-7.4 | Fast travel between discovered shrines with a yang cost | MUST |
| FR-7.5 | Map with fog of war, markers and custom pins; minimap | MUST |
| FR-7.6 | One dungeon at MVP (25–45 min, 2 mini-bosses, 3-phase boss, checkpoints) | MUST |
| FR-7.7 | 3 regions / 12 zones / 5 dungeons | SHOULD |
| FR-7.8 | Order Hall with upgradable facilities | SHOULD |
| FR-7.9 | Tower of Shards procedural endgame with per-floor affix draft and daily seed | SHOULD |
| FR-7.10 | New Game+ | SHOULD |

## FR-8 Quests and narrative

| ID | Req | Pri |
|---|---|---|
| FR-8.1 | Data-driven quest system: objectives (kill / collect / reach / interact / escort / survive), prerequisites, rewards, state machine | MUST |
| FR-8.2 | Dialogue system with conditional nodes and player choices | MUST |
| FR-8.3 | Quest journal with active/completed tracking and map markers | MUST |
| FR-8.4 | MVP: prologue + 6 story quests + 6 side quests, each side quest granting a mechanical reward | MUST |
| FR-8.5 | v1.0: 3 acts, ~28 story quests, ~45 side quests, 3 scripted siege set-pieces | SHOULD |
| FR-8.6 | Faction reputation with 3 factions unlocking vendors and gear lines | SHOULD |

## FR-9 Companion, mounts, pets

| ID | Req | Pri |
|---|---|---|
| FR-9.1 | One AI companion, 3 swappable archetypes, levelling, 3 gear slots, 4 abilities | SHOULD |
| FR-9.2 | Companion damage capped at ~30% of player DPS; cannot revive the player; retreats 45 s on death | SHOULD |
| FR-9.3 | Command wheel: aggro here / fall back / use ability | SHOULD |
| FR-9.4 | Mount with +80% move speed and a staggering charge; dismount on heavy hit | SHOULD |
| FR-9.5 | 6 pets with passive effects | COULD |

## FR-10 Economy and vendors

| ID | Req | Pri |
|---|---|---|
| FR-10.1 | Yang as the single currency; sinks: upgrades, sockets, rerolls, repairs, fast travel | MUST |
| FR-10.2 | Shard Essence as the premium-equivalent earned currency (cosmetics, high upgrades) | MUST |
| FR-10.3 | Vendors with seed-deterministic rotating stock | SHOULD |
| FR-10.4 | Crafting bench: recipes for materials, stones, consumables | SHOULD |
| FR-10.5 | **No cash shop, no premium currency, no purchasable advantage** — hard constraint | MUST |

## FR-11 Save and settings

| ID | Req | Pri |
|---|---|---|
| FR-11.1 | Save anywhere: 3 manual slots + 5-deep autosave ring | MUST |
| FR-11.2 | Versioned save format with forward migration; a save must survive a patch | MUST |
| FR-11.3 | Save integrity check with a clear error, never a silent corrupt load | MUST |
| FR-11.4 | Difficulty changeable at any time from the menu | MUST |
| FR-11.5 | Settings: graphics presets, resolution, vsync, FPS cap, audio buses, language, accessibility | MUST |
| FR-11.6 | Pause anywhere outside cutscenes | MUST |

## FR-12 UI

| ID | Req | Pri |
|---|---|---|
| FR-12.1 | HUD: HP/mana orbs, flask charges, XP bar, hotbar with cooldown sweeps, target frame with cast bar, buff/debuff strip, minimap | MUST |
| FR-12.2 | Screens: inventory, character sheet, skills, map, quest journal, codex, settings, blacksmith, socket, reroll, vendor | MUST |
| FR-12.3 | Every screen keyboard-navigable and closable with Esc | MUST |
| FR-12.4 | Tooltips explain every stat in plain language; no unexplained acronyms | MUST |
| FR-12.5 | UI scale slider (80–150%) | SHOULD |

---

## NFR — non-functional

| ID | Req | Target |
|---|---|---|
| NFR-P.1 | Framerate | ≥ 60 fps @1080p on GTX 1060 / RX 580 with 60 enemies on screen |
| NFR-P.2 | Zone load time | ≤ 5 s from SSD |
| NFR-P.3 | Memory | ≤ 4 GB RAM, ≤ 3 GB VRAM |
| NFR-P.4 | Save/load | ≤ 300 ms |
| NFR-P.5 | No GC hitching | no frame > 16.6 ms caused by allocation during combat; pooled projectiles, damage numbers, VFX |
| NFR-R.1 | Stability | crash-free session rate ≥ 99%; no softlocks; every quest state recoverable |
| NFR-R.2 | Determinism | combat maths identical given the same seed — required for the balance simulator |
| NFR-M.1 | Testability | all combat/progression/item logic in a pure C# `Core` assembly, unit-testable with no engine boot |
| NFR-M.2 | Data-driven | adding an enemy, item, skill or quest requires **no code changes** |
| NFR-M.3 | Validation | every JSON data file validated against a schema in CI; broken references fail the build |
| NFR-M.4 | Art swap boundary | replacing placeholder primitives with real models must be a data/scene edit, never a code edit |
| NFR-A.1 | Accessibility | damage-taken slider, telegraph-duration multiplier, target-priority assist, slow-motion, skip-boss-after-5-deaths, colourblind-safe telegraphs (shape + colour), subtitle size, no required audio cues |
| NFR-L.1 | Localisation | all player-facing strings in translation files, zero hardcoded text; EN + IT |
| NFR-B.1 | Build | one-command reproducible export; CI produces a Windows build per push to `main` |

---

## Content requirements — MVP (Tier A)

| Asset class | Count | Placeholder form (decision Q2) |
|---|---|---|
| Playable class | 1 (Warrior) | capsule + coloured weapon primitive |
| Skills | 8 | particle + decal VFX only |
| Enemy types | 18 | capsules/boxes, colour-coded by family, scaled by tier |
| Bosses | 2 | larger primitive + unique VFX |
| Shard tiers | 5 | emissive monolith primitive, colour per tier |
| Zones | 3 outdoor + 1 dungeon + hub | greybox terrain + modular blockout |
| Items | ~70 | icon-less coloured slot frames until icons exist |
| Quests | 12 | — |
| NPCs | 6 | capsules with name tags |
| Music tracks | 3 | CC0 placeholder |
| SFX | ~40 | CC0 placeholder |

**Art-swap requirement:** every visual is instanced from a `VisualRoot` node resolved through
a mesh/material registry keyed by id, so the whole placeholder set is replaceable by editing
registry entries.

---

## Out of scope (explicitly, for the whole project)

Multiplayer of any kind · player trading/economy · cash shop or microtransactions ·
PvP · procedural open world · full voice acting · mobile or console ports ·
mod SDK (Tier C only) · macOS build (Tier C only).
