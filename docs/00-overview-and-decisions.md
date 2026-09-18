# 00 — Overview, Scope and Decisions

## 1. The pitch

> A stylized oriental-fantasy **action RPG**. You are one of the last of a broken order,
> hunting the corrupting **Metin shards** that fell from the sky and are twisting the land's
> beasts and people. Break the shards, take their power, upgrade your gear, push deeper into
> corrupted regions, and end the thing that threw them.

Single player. Offline. No accounts, no servers, no cash shop, no grind walls.
15–25 hours to credits, plus an endless endgame tower and New Game+.

## 2. Ground rules on IP (read once, then it's handled)

This matters and it's simple to stay clean:

- **Game mechanics, systems and genre conventions are not copyrightable.** Breakable
  world-spawn objects, +0→+9 upgrading, sockets, bonus rolls, four-class archetypes — all of
  that can be reimplemented freely. That is what we are doing.
- **What we never touch:** original Metin2 art, models, textures, sounds, music, UI, map
  files, `.eix`/`.epk` pack files, the client binary, or any of the circulated Metin2
  **server source files**. Those are copyrighted works of Ymir Entertainment / Webzen, and
  the circulated server files are illegally distributed. We build clean-room, from scratch,
  with original or properly licensed assets only.
- **Names and trademarks:** no "Metin2", no kingdom names, no map or NPC names from the
  original. In code we use `Shard` / `ShardMonolith` as a generic mechanic name. Pick a final
  project name before any public build.
- **Describing the inspiration is fine** ("inspired by classic Korean MMORPGs"). Claiming
  affiliation is not.

All game code is written originally. Every third-party asset gets an entry in
`ASSET-LICENSES.md` with source + license the moment it enters the repo.

## 3. Scope tiers

Scope is the number-one risk on a project like this, so it is explicit.

### Tier A — MVP / Vertical Slice (proves the game works)
- 1 playable class (Warrior), 8 skills, 3 tiers of gear
- 1 region: hub village + 3 outdoor zones + 1 dungeon
- ~18 enemy types, 5 shard tiers, 2 bosses
- Full loop: explore → fight → break shard → loot → upgrade → skill up → next zone
- ~90 minutes of gameplay, fully polished, difficulty settings working
- **Exit test:** someone who has never seen the project plays 60 minutes without
  instructions and wants to keep going.

### Tier B — v1.0 ship target
- 4 classes (Warrior, Blade, Sura, Shaman), ~16 skills each
- 3 regions, 12 zones, 5 dungeons, hub village + 2 outposts
- ~55 enemy types, 9 shard tiers, 10 bosses
- Campaign in 3 acts, ~40 quests, companion system, mounts
- Endgame: Tower of Shards (procedural), New Game+, 4 difficulty tiers
- Full UI, settings, controller support, EN + IT localization
- **15–25 hours to credits**

### Tier C — post-launch / stretch (only if v1.0 lands)
- 5th class, 4th region, boss-rush mode, data-pack modding, Steam Workshop,
  co-op (architecture keeps it possible; we do not build it)

**Tier A is completed before Tier B starts.** This is the single biggest predictor of
whether the project ships.

## 4. Effort and time — honest numbers

Metin2 was built by a studio of dozens over years. We are not rebuilding Metin2; we are
building a focused ARPG that *feels* like it.

| Tier | My work (code, data, tooling, docs) | Your hours (editor, decisions, playtest) | Calendar at ~10 h/week of your time |
|---|---|---|---|
| Tier A (MVP), placeholder art | heavy, front-loaded | **35–60 h** | **6–9 weeks** |
| Tier B (v1.0) | heavy, sustained | 350–550 h | 10–16 months (+ art) |
| Tier C | — | — | open-ended |

The split matters: **I can write effectively all of the code, all of the data, all of the
tooling, and author Godot scene/resource files directly as text.** Your time goes into
in-editor layout and level design, taste calls, and playtesting.

The placeholder-art decision (Q2) is what drops Tier A to 35–60 h — it removes the entire
art pipeline from the MVP. The cost is that **you cannot judge game feel from looks or show
it to anyone** until art lands; we compensate by making combat readability come from VFX,
decals, hit-stop and audio, which I can build with primitives and Godot's built-in
particle/shader systems. Budget the deferred art cost honestly: roughly 80–150 h of your
time, or €150–400 in asset packs, whenever we pick it up.

## 5. Division of labour — what the task tags mean

Every task in [05-roadmap.md](05-roadmap.md) carries one of these:

| Tag | Meaning |
|---|---|
| **[C]** | **I do it** end to end in this repo: code, data files, scene/resource text files, docs, tooling, tests. You review and commit. |
| **[C→You]** | I produce it, you finish it in the Godot editor or judge it by feel (placement, timing, "does this feel good"). |
| **[You]** | Only you can do it: GUI/editor work I cannot drive, art creation, purchases, account signups, taste decisions, playtesting. |
| **[Provide]** | I am blocked until you hand me something: a decision, an asset, a key, a file. |

## 6. Decisions already made (rationale included — overrule any of them)

| # | Decision | Why |
|---|---|---|
| D1 | **Engine: Godot 4.x + C#** | Scenes (`.tscn`) and resources (`.tres`) are **plain text**, so I can author whole scenes, prefabs and data assets directly — that shifts a large amount of work from you to me. Free, MIT, no royalties, good stylized 3D. Unity is the alternative (bigger asset store, but GUID-keyed `.meta` files that are brittle to hand-author). Unreal is overkill for this art style. |
| D2 | **Camera: locked third-person, semi-isometric** | Matches the source's read and keeps the art budget low — no close-up fidelity needed. |
| D3 | **Controls: faithful click-to-move / click-to-attack** *(your call, 2026-09-18)* | Authentic to the original and to what Metin2 players expect. I had recommended WASD action combat; you chose fidelity. **Consequence:** there is no dodge roll and no i-frames, so solo difficulty cannot come from execution timing — it is rebuilt on positioning, active defensive cooldowns, target priority and resource management. Fully redesigned in [02-singleplayer-redesign.md](02-singleplayer-redesign.md) §2. WASD is offered as an alternative control scheme in options (cheap, since movement resolves to the same destination-move core), but the game is designed and balanced for click-to-move. |
| D4 | **Defence is an active cooldown, not a roll** | Each class gets a defensive ability (Guard Stance / Smoke Step / Dark Ward / Spirit Shield) on a short cooldown. This is the replacement for the dodge roll: it preserves a "did you react in time" skill layer inside a click-to-move scheme without breaking the original's feel. |
| D5 | **Art: placeholder primitives for MVP** *(your call, 2026-09-18)* | We defer the art decision until the loop is proven fun. **Consequence:** all visuals sit behind a strict swap boundary — a mesh/material registry and scene-level `VisualRoot` node — so replacing capsules with real models later is a data edit, never a code edit. Target style when we do commit: stylized low-poly, ~3–8k tris/character. |
| D6 | **Data-driven everything: JSON + generated C# records** | Items, mobs, skills, drops and quests live in JSON that I can author and validate by the hundred. Balance passes become data edits, not code edits. |
| D7 | **No RNG that can destroy player progress** | See [02-singleplayer-redesign.md](02-singleplayer-redesign.md) §4. Item-destruction gambling exists to sell blessing scrolls; with no shop it is pure punishment. |
| D8 | **Single process, simulation/presentation split** | Game logic lives in a pure C# `Core` assembly with no Godot types; presentation lives in Godot. Logic becomes unit-testable without booting the engine, and a co-op option stays open for Tier C. |
| D9 | **Save anywhere: 3 manual slots + autosave ring** | Single-player standard; removes all the death-anxiety design of the MMO original. |
| D10 | **Windows first, Steam + itch.io, Linux/Proton verified** | Godot exports both cheaply; macOS needs paid notarization, so it is Tier C. |

## 7. Open decisions — your call (details in [07-what-i-need-from-you.md](07-what-i-need-from-you.md))

### Answered 2026-09-18

| # | Question | Your answer | Effect |
|---|---|---|---|
| Q1 | Engine | **Godot 4 + C#** | Confirms D1. I author `.tscn`/`.tres` directly. |
| Q2 | Art pipeline | **Placeholder primitives for now** | D5 rewritten. Adds the art-swap boundary (Phase 1 task ENG-07) and defers all art tasks to a later phase. |
| Q3 | Combat | **Faithful click-to-move** | D3/D4 rewritten, difficulty model rebuilt — redesign doc §1 and §2. |
| Q4 | Scope commitment | **MVP vertical slice first** | Roadmap Phases 0–8 are detailed and committed; Phases 9–13 (Tier B) are a sketch we re-plan after the MVP exit test. |

### Still open

| # | Question | Default if you have no preference |
|---|---|---|
| ~~Q5~~ | ~~Final project name~~ | **Answered 2026-09-18: Shardfall.** Matches the repo, and drops the `Sohan` codename which was a Metin2 map name (IP hygiene, §2). Applied across solution, assemblies, namespaces and the Godot project. |
| Q6 | Commercial release, or portfolio/learning project? | Affects asset licensing only — assume commercial |
| Q7 | Launch languages | EN + IT |
| Q8 | Controller support in v1.0 scope? | Yes — but note click-to-move makes controller support *harder*, so this is a Tier B decision |

## 8. Success criteria

At v1.0 the project is a success if:

1. A player who has never heard of Metin2 finishes the campaign and rates the combat "good".
2. Time-to-credits is 15–25 h with **zero** required grinding — no "kill 200 boars to afford the next tier".
3. Framerate ≥ 60 fps at 1080p on a GTX 1060-class GPU.
4. Crash-free session rate ≥ 99%.
5. Every `MUST` requirement in [03-requirements.md](03-requirements.md) is implemented and tested.
