# 05 — Roadmap: phases, tasks and ownership

**Committed scope: Tier A (MVP vertical slice).** Phases 0–8 are fully planned below.
Phases 9–13 (Tier B, the full v1.0) are a deliberate sketch — we re-plan them properly after
the MVP exit test, because what we learn from playing the slice should change them.

Ownership tags (from [00-overview-and-decisions.md](00-overview-and-decisions.md) §5):

| Tag | Meaning |
|---|---|
| **[C]** | I do it end to end |
| **[C→You]** | I produce it, you finish/tune it in the editor or judge it by feel |
| **[You]** | Only you can do it |
| **[Provide]** | I am blocked until you give me something |

Durations assume ~10 h/week of your time. My work is not the bottleneck in any phase except
where marked.

---

## Phase 0 — Foundations (week 1) — ✅ done

**Goal:** an empty but correct project that builds, tests and ships from day one.
**Exit:** `dotnet test` green in CI, Godot opens the project, a grey cube renders, a tagged
build artifact downloads from GitHub Actions.

**Status 2026-09-18: complete.** Godot 4.7.2 installed, repo pushed to
https://github.com/andreabraho/shardfall, and the whole chain verified end to end via
`scripts/verify.sh`: build clean (0 warnings), 59 tests passing, 40 content definitions
validating, Godot project building, headless import clean, content loading in-engine in
164 ms. One editor task remains (ENG-11).

| ID | Task | Owner | Status |
|---|---|---|---|
| ENG-01 | Install Godot .NET, .NET 8 SDK, Git LFS | **[You]** | ✅ Godot 4.7.2 mono, .NET SDK 8.0.425, Git LFS 3.7.1 |
| ENG-02 | `git init`, `.gitignore`, `.gitattributes` (LFS routing), branch conventions | **[C]** | ✅ |
| ENG-03 | Create GitHub repo, push | **[You]** | ✅ andreabraho/shardfall, `main` pushed |
| ENG-04 | Solution + project skeleton (`Kiln.Core`, `.Data`, `.Tests`, `.Tools`, `game/`) | **[C]** | ✅ |
| ENG-05 | Godot project config: rendering (Forward+), input map, physics layers, smoke scene | **[C]** | ✅ pinned to 4.7.2 |
| ENG-06 | CI: GitHub Actions — build, `dotnet test`, content validation, codegen check, headless Godot import, Windows export | **[C]** | ✅ (Godot export job auto-enables once `export_presets.cfg` exists) |
| ENG-07 | **Art-swap boundary**: `VisualRoot` node + visual registry keyed by id (NFR-M.4) | **[C]** | ✅ |
| ENG-08 | Data pipeline: JSON loader, validator (9 rules), id-constant codegen, cross-reference checker | **[C]** | ✅ |
| ENG-09 | Logging, debug overlay (FPS, frame time, draw calls, pluggable providers) | **[C]** | ✅ |
| ENG-10 | Verify the CI artifact runs on your machine | **[You]** | ⬜ after ENG-11 |
| ENG-11 | **Create the Windows export preset** in the Godot editor (Project → Export → Add → Windows Desktop). Set `export_path` to `../export/windows/Kiln.exe` and add `*.json` to the non-resource include filter, or game data will not ship in the build. This also auto-enables the CI export job. | **[You]** | ⬜ editor GUI, ~2 min (also downloads export templates) |

---

## Phase 1 — Movement, camera, click-to-move core (week 2) — ⏳ built, awaiting your feel review

**Status 2026-09-18:** MOV-01/02/04/05/06/07 implemented and running. Launch the project
and you are in the greybox arena. MOV-03 (responsiveness tuning) is deliberately left until
after MOV-08, because I should tune against your notes rather than my guesses.

**Goal:** the control scheme feels right. This phase is disproportionately important —
click-to-move lives or dies on responsiveness.
**Exit:** you can click around a greybox arena for two minutes and it feels good, not laggy.

| ID | Task | Owner | Notes |
|---|---|---|---|
| MOV-01 | NavMesh baking setup + navigation region on a greybox test level | **[C]** | |
| MOV-02 | Click-to-move controller: raycast to ground, path request, steering, hold-to-continue | **[C]** | |
| MOV-03 | Responsiveness pass: instant turn-start, path smoothing, corner cutting, stuck recovery | **[C→You]** | You judge the feel; I tune the numbers |
| MOV-04 | Move-order feedback: click decal, cursor states (move / attack / interact / blocked) | **[C]** | Critical for click-to-move legibility |
| MOV-05 | Camera rig: fixed-pitch follow, zoom range, rotate, collision, dead-zone, smoothing | **[C]** | |
| MOV-06 | Alternative WASD scheme feeding the same destination core (FR-1.7) | **[C]** | Cheap now, expensive later |
| MOV-07 | Input map + rebinding infrastructure | **[C]** | |
| MOV-08 | **Feel review** — 20 min of play, written notes | **[You]** | ⬅ **TESTABLE NOW.** The one thing I genuinely cannot do |
| MOV-09 | **One scheme, not two**: WASD and click-to-move both live at once, either interrupting the other; the F4 exclusive toggle goes | **[C]** | ✅ Added 2026-09-20 on your note. A mode switch asks the player to decide in advance which hand they will use |
| MOV-10 | Right-mouse look, and WASD relative to where the camera points | **[C]** | ✅ Added 2026-09-20. Pairs with MOV-09: camera-relative movement is what makes holding W usable while the camera swings. The cursor is captured while looking and put back on release |
| MOV-11 | Attack on Space | **[C→You]** | ✅ Added and done 2026-09-20. Guard moved to Shift. Swings where you face with nothing selected, so it works while the other hand steers |

**The Space collision (MOV-11), resolved.** Guard moved to **Shift**, Space became attack.

I had recorded the reason as "Guard is held, not tapped". That was wrong — Guard is a tap
that starts a timed stance on a ten-second cooldown. The conclusion survives the correction
and gets easier: a key pressed once every ten seconds has no claim on the best key on the
keyboard, and the attack is pressed more often than everything else combined. Shift is the
conventional home for walk or hold-position, neither of which exists, so the cost is a door
closed rather than a feature lost.

The HUD hint now reads the live binding instead of naming a key in a string, because it spent
an hour telling the player to press Space for Guard.

**Sequencing note.** MOV-09/10/11 changed the scheme MOV-08 is meant to review, which is why
they went first. MOV-08 is now worth doing.

---

## Phase 2 — Combat core (weeks 3–4)

**Goal:** killing a thing is satisfying with capsules and no art.
**Exit:** you fight 5 enemy roles in an arena, telegraphs are readable, hits feel weighty.

| ID | Task | Owner | Notes |
|---|---|---|---|
| CBT-01 | `Core` damage pipeline per [06](06-data-model-and-formulas.md) §4, fully unit-tested | **[C]** | Engine-free, ~40 tests |
| CBT-02 | Health/mana/stamina components, death, respawn at shrine ≤4 s | **[C]** | |
| CBT-03 | Click-to-attack: target acquisition, approach, auto-attack chain, retarget rules | **[C]** | |
| CBT-04 | Attack timing model: wind-up / active / recovery, commit windows (FR-3.3) | **[C]** | |
| CBT-05 | Threat/aggro system with leash and de-aggro | **[C]** | |
| CBT-06 | Behaviour-tree AI framework + idle/patrol/chase/attack/reposition/flee | **[C]** | ✅ framework in Core, unit-tested; patrol pending |
| CBT-07 | The 5 enemy roles (Bruiser, Archer, Shielder, Mender, Bomber) as data + behaviours | **[C]** | ✅ The tactical layer from redesign §2.4 |
| CBT-08 | Telegraph system: ground decals with fill animation, shape library (circle/cone/line), colourblind-safe shapes | **[C]** | ✅ circle + cone; line and colourblind shapes pending |
| CBT-09 | Status effects: poison, bleed, stun, slow, weaken, vulnerability + stacking rules | **[C]** | ✅ applied from ability data, shown in HUD |
| CBT-10 | Per-class defensive ability framework + Warrior Guard Stance | **[C]** | ✅ |
| CBT-11 | Healing flask with charges, cast time, interrupt | **[C]** | ✅ refills out of combat until shrines land (WLD-02) |
| CBT-12 | Game feel: hit-stop, screen shake, hit flash, floating damage numbers, impact VFX | **[C→You]** | ✅ hit-stop, shake, flash, numbers, AoE flashes |
| CBT-13 | Object pooling for projectiles/numbers/VFX (NFR-P.5) | **[C]** | |
| CBT-14 | **Combat feel review** + written notes | **[You]** | |
| CBT-15 | Placeholder visual set, plus role markers (shape + colour) | **[C]** | ✅ role is what matters tactically, so markers encode role not family |
| CBT-16 | Crowd separation: enemies push each other apart with a soft radius, overlapping up to a limit rather than not at all | **[C]** | ✅ Added 2026-09-20 on your note. Hard collision makes a pack queue single-file down a corridor; none at all makes six creatures one creature. The player counts as a neighbour, so a pack no longer stands inside them |
| CBT-17 | **Four-hit basic attack chain**, each swing different, the fourth sweeping non-boss enemies away | **[C→You]** | ✅ Added 2026-09-20. Wants CBT-16 first: a finisher that throws a crowd is only readable once the crowd has shape. Jab, cross, cut, sweep — the fourth is 360°, throws what it catches, and is slow enough to be a decision. Knockback is new and lives on the brain |

**When these land.** CBT-16/17 reopen Phase 2 while Phase 6 is still going, which is fine —
the phases are an order of dependency, not a promise never to return. Both want to be in
before CBT-14, the combat feel review, for the same reason MOV-09/10/11 want to be in before
MOV-08: reviewing a loop you are about to replace spends your time twice.

CBT-17 is the largest of the six additions by some distance. A chain means per-swing timing,
a window in which the next input extends the chain rather than restarting it, and a reset
rule — and the fourth swing throwing a crowd needs knockback, which nothing in the game does
yet. It is worth doing properly rather than quickly; the basic attack is the thing the player
presses more than everything else combined.

---

## Phase 3 — Stats, progression, Warrior skills (week 5)

**Goal:** levelling up changes how you play.
**Exit:** level 1→12 in a test arena, 8 skills usable, respec works.

| ID | Task | Owner | Notes |
|---|---|---|---|
| PRG-01 | Attributes, derived stats, stat aggregation from gear/buffs | **[C]** | ✅ attributes + derived; gear aggregation lands with Phase 4 |
| PRG-02 | XP curve, levelling, attribute + skill point award | **[C]** | ✅ |
| PRG-03 | Skill system: definitions, cooldowns, mana costs, cast types, targeting modes | **[C]** | ✅ |
| PRG-04 | 8 Warrior skills across the two trees | **[C]** | ✅ data + level gating; 3 are passive/self and land with their effects |
| PRG-05 | Skill mastery accrual + rank-up mechanical changes | **[C]** | ✅ ranks change behaviour, not just numbers |
| PRG-06 | Free respec at shrines | **[C]** | ✅ (WLD-02) — free, per FR-2.6; refunds attributes only, as skills have nothing to respec |
| PRG-07 | Catch-up XP curve + low-level enemy floor (FR-2.7) | **[C]** | ✅ |
| PRG-08 | Skill VFX with primitives + Godot particles | **[C→You]** | You call readability |
| PRG-09 | **Balance simulator v1** — walks the XP table, reports level-vs-zone-band deltas | **[C]** | ✅ runs in CI; found the first draft gave quests 88% of all XP |
| PRG-10 | **Character sheet** (C): spend attribute points, derived stats live, skill list | **[C]** | ✅ added 2026-09-19 — the phase awarded points and nothing could spend them |
| PRG-11 | **Skill points have no sink** — decide whether they become a real choice | **[Provide]** | ⬜ 8 skills vs ~24 points by level 24; see the note below |

**PRG-11, the open question.** Attribute points now have a home; skill points do not. The
Warrior has 8 skills, unlocking at levels 1/3/6/8/12/14/20/24, and the curve grants one point
per level — so the tree absorbs a third of what the player earns and the rest is dead weight.
Three ways out, none of them chosen yet:

1. **Leave it a gate.** Points stop existing; skills simply unlock at their level. Honest, and
   one less number on the sheet.
2. **Buy mastery ranks with points**, instead of (or alongside) earning them by use. Gives
   points a deep sink and lets a player specialise early.
3. **Make the two trees compete** — a shared budget that cannot unlock everything, so Body and
   Mental are a real fork rather than a reading order.

Option 2 is the one that adds a build decision without adding a system. Your call.

---

## Phase 4 — Items, inventory, the upgrade loop (weeks 6–7)

**Goal:** the gear chase works, with no gambling.
**Exit:** loot drops, equips, upgrades to +9 via the pity ladder, sockets and rerolls work.

| ID | Task | Owner | Notes |
|---|---|---|---|
| ITM-01 | Item model: bases, rarity, sockets, bonus lines, upgrade level, durability | **[C]** | ✅ durability cut — a repair tax is busywork, not a decision |
| ITM-02 | Item generation: rarity roll, bonus-line rolls from slot pools | **[C]** | ✅ deterministic from the save seed, so reloading cannot reroll a drop |
| ITM-03 | Drop tables + loot burst, auto-loot with rarity filter | **[C]** | ✅ six per-band tables; the rarity filter waits for the settings screen |
| ITM-04 | Grid inventory with multi-cell items, drag/drop, auto-sort, junk flow | **[C]** | ✅ grid, stacking, auto-sort, atomic spending; drag/drop lands with the UI |
| ITM-05 | Equipment slots + stat aggregation into PRG-01 | **[C]** | ✅ recomputed from scratch on every change |
| ITM-06 | **Upgrade ladder +0→+9** with material costs, displayed chance, displayed pity counter, no destruction | **[C]** | ✅ Redesign §4.1 — the signature change |
| ITM-07 | Sockets: rarity-driven count, Boring Stone opening, non-destructive stone removal | **[C]** | ✅ removal returns the stone intact |
| ITM-08 | Bonus reroll with Mutation Ink + line locking | **[C]** | ✅ locking makes rerolling converge instead of gamble |
| ITM-09 | Comparison tooltips, plain-language stat descriptions | **[C]** | ✅ hovering a bag item shows the delta against what it would replace |
| ITM-10 | Yang economy + sinks; vendor buy/sell | **[C]** | ◐ sinks and costs done; the vendor itself needs NPCs |
| ITM-11 | ~70 MVP items as data | **[C]** | ✅ 62 items — 47 equippable across every slot and band, plus materials and stones |
| ITM-12 | **Economy simulator** — verifies you are yang-constrained early and comfortable later | **[C]** | ✅ runs in CI; its first honest version showed money never mattered before Act 2 |
| ITM-13 | Item icons | **[You]** *(deferred)* | Coloured frames until art phase |
| ITM-14 | Drop, destroy and lock items from the bag | **[C]** | ✅ added 2026-09-19 — drop is reversible so it is instant, destroy always asks, lock is the standing answer |
| ITM-15 | The upgrade bench shows what is equipped, as icons | **[C]** | ✅ Added 2026-09-20 on your note. Small and self-contained — the bench currently makes you remember what you are wearing while deciding what to improve. A rarity tile, ringed when worn, plus the slot name. The tile is the placeholder ITM-13 replaces |

---

## Phase 5 — Shard encounters (week 8)

**Goal:** the signature mechanic, as a real encounter.
**Exit:** a tier-3 shard fight is tense, readable and repeatable without feeling identical.

| ID | Task | Owner | Notes |
|---|---|---|---|
| SHD-01 | Shard node entity, respawn timer, tier data, modifier roll | **[C]** | ✅ rerolled on every respawn, and tinted so you see it before engaging |
| SHD-02 | Three-phase encounter state machine | **[C]** | ✅ engine-free — Redesign §3 |
| SHD-03 | Wave composer: role mixes per tier and phase | **[C]** | ✅ waves are roles, so any zone can dress the same shape |
| SHD-04 | Radial pulse AoE with difficulty-scaled telegraph | **[C]** | ✅ validated under BAL-03 at every difficulty |
| SHD-05 | Reclamation cast + anchor-add interrupt | **[C]** | ✅ the phase-3 tension beat; a landed cast starts another |
| SHD-06 | Soft arena barrier, leave-resets-encounter | **[C]** | ◐ leaving resets and clears the adds; the visible barrier is art |
| SHD-07 | Break payoff: shockwave, loot burst, Shard Essence, buff | **[C]** | ◐ shockwave, burst and essence done; the absorbed-power buff needs the buff system |
| SHD-08 | 4 modifiers (Frenzied / Warded / Venomous / Twin) | **[C]** | ◐ three live; Twin needs a second node spawned at runtime |
| SHD-09 | 5 shard tiers as data | **[C]** | ✅ |
| SHD-10 | **Encounter tuning pass** — you play each tier, I adjust | **[C→You]** | |

---

## Phase 6 — The world (weeks 9–10)

**Goal:** a place to play, not an arena.
**Exit:** hub → 3 zones → dungeon traversable, shrines and fast travel working.

**Status 2026-09-19:** the world exists as a validated graph — 6 zones, 11 shrines, 15 spawn
fields, 25 enemies across every band from 3 to 23 — and the ridge is playable with it. What
is left is level design: five of the six zones are declared but not laid out, which CI now
reports as a warning on every run rather than as something to remember.

### World structure (decided 2026-09-19)

The shape is the one the genre established: **a village with a safe zone, fields of monsters
immediately around it, and a chain of further maps banded by level.** Two villages, not one,
so the pattern is established and then repeated — a single village reads as a menu, two read
as a world. (Layout convention only; no names, maps or assets from any existing game — see
[00 §2](00-overview-and-decisions.md).)

Three things in the current model have to change to express it, and none of them are large:

1. **A village is a safe region inside a map, not a map of its own.** Today the hub is a
   separate zone, which puts a loading boundary at the village gate and means you can never
   stand in the village and see the field you are about to walk into. Both of those are the
   opposite of what makes the layout work.
2. **Safe zones become a validated thing, not a convention.** No spawn field may overlap one,
   no enemy may follow the player into one, and nothing inside one can damage the player. A
   safe zone that is only safe because nobody happened to place a camp there will stop being
   safe the first time somebody does.
3. **More than one hub.** The validator currently *errors* on anything other than exactly one
   hub zone — a rule written when one village was the plan, and now the thing standing in the
   way. It becomes "at least one", with each village anchoring the region around it.

Tier A scope in [00 §3](00-overview-and-decisions.md) moves from `hub village + 3 outdoor
zones + 1 dungeon` to `2 villages + 4 field maps + 1 dungeon`. That is one more village and
one more map than planned — real added scope, taken deliberately, because the second village
is what proves the structure rather than decorating it.

| ID | Task | Owner | Notes |
|---|---|---|---|
| WLD-01 | Zone loading/streaming, level bands, transitions | **[C]** | ✅ gates cross between maps, carrying the character; level and quest gates are read from the world graph, never re-stated by a scene. Streaming (as opposed to a scene swap) is not needed at this map size |
| WLD-02 | Shrine system: save, respawn, fast travel, flask refill, respec | **[C]** | ✅ save waits on Phase 8; respec unblocks PRG-06 |
| WLD-03 | Spawn-zone system: density caps, respawn timers, distance activation | **[C]** | ✅ camps come back as camps, and keep their timers while you are away |
| WLD-04 | Modular greybox kit (cliffs, paths, ruins, props) as primitives | **[C]** | ✅ 18 pieces in `tables/world_kit.json`; a scene names a piece id and nothing else. Editor preview works: content loads on demand in the editor, pieces rebuild when the id changes, and safe zones and camps draw their radius as a ring |
| WLD-05 | **Greybox the 4 field maps** — layout, landmarks, encounter placement, sightlines | **[C→You]** | ✅ all four built and connected: Vale Approach (a corridor with a pinch), Vale Floor (a basin with barrows), the Ridge (the old arena) and Broken Gate (a curtain wall with two ways through). Layouts are a first pass — level design by feel is yours |
| WLD-06 | **Greybox the 2 villages** + NPC placement | **[C→You]** | ◐ village 1 (`ember_hollow.tscn`) built: 36 m walled square, one gate, shrine, safe ground r22, three camps 40 m out. Village 2 waits on WLD-05; NPCs wait on ITM-10. Layout is a first pass — level design by feel is yours |
| WLD-12 | **Safe zones**: no spawn overlap, no enemy entry, no damage inside — validated | **[C]** | ✅ `SafeRegion` + `SafetyField` in Core; declared per zone in data, placed by a `SafeZoneNode` in the scene. Enforced at all three points: aggro drops at the boundary, damage to the player is refused, and `ZoneRoot.Audit` errors on a camp within 6 m of the line |
| WLD-13 | **Multi-hub**: relax the one-hub rule, each village anchors its region | **[C]** | ✅ "exactly one hub" → "at least one"; reachability still seeds from the starting village, so village 2 must be walkable from village 1 or it reports as an orphan |
| WLD-07 | **Greybox the dungeon** as a floor tower: per-floor task, boss every third floor, shrine + bench after each boss | **[C→You]** | ✅ `catacombs.tscn` — nine floors on one footprint, stacked 24 m apart, one navigation bake. Verbs: break · hold · **fight** · find · carry · **fight** · race · hold · **fight**, which is six distinct and no repeat in a row. Shrine + bench on floors 4 and 7, refuge and the optional shard on floor 5. The rules are enforced in the content validator, not in review: FR-7.12/7.13/7.14/7.17 all fail the build if an edit breaks them. Floor pacing and boss difficulty are yours |
| WLD-08 | Map + minimap: fog of war, markers, custom pins, shard-node overlay | **[C]** | ◐ zone map on **M**: drawn from the live scene (kit pieces, gates, shrines, shards, camps, safe ground), with fog of war remembered per zone across borders. Custom pins and a corner minimap still open |
| WLD-09 | Fast travel UI + yang cost | **[C]** | ✅ flat fee by destination band; refused in combat, and to a dungeon checkpoint |
| WLD-10 | 2 bosses: phases, mechanics, arenas | **[C→You]** | ◐ three exist — the Gallery Warden, the Vault Chorister and the Heart Keeper — each alone on its own floor with three telegraphed abilities. Arenas done, mechanics done, **phases not**: they fight the same way at 10 % as at 100 %. Difficulty tuning is yours |
| WLD-11 | **Zone-layout review and iteration** | **[You]** | |

---

## Phase 7 — The main road (week 11)

**Reshaped 2026-09-21 on your call: this is not a quest game.** A handful of quests that
say where to go next, no side quests, no journal. Design in
[02](02-singleplayer-redesign.md) §9; requirements FR-8.

**Goal:** the player always knows where to go next.
**Exit:** a new character can follow the chain from the village to the tower without being
told anything outside the game.

| ID | Task | Owner | Notes |
|---|---|---|---|
| QST-01 | Quest state machine: one active quest, objectives, prerequisites, rewards, saved with the game | **[C]** | Small on purpose: at most one active quest |
| QST-02 | Objective types: reach / break / talk / collect | **[C]** | Escort and survive dropped; no kill counts |
| QST-03 | Linear dialogue: a few lines per NPC, no choices | **[C]** | |
| QST-04 | Current objective as one HUD line + one map marker | **[C]** | Replaces the journal screen |
| QST-05 | **The chain: about five main-progress quests**, village → fields → tower | **[C]** | One per step of the road. Re-gates the Broken Gate border properly |
| QST-06 | The NPCs the chain and the economy need (elder, smith, vendor) | **[C]** | Vendor also closes ITM-10 |
| QST-07 | Codex/bestiary filling on kills | **[C]** | Not a quest system; kept |
| QST-08 | **Story and tone review** — names, writing, whether it lands | **[You]** | Taste call |
| QST-09 | **Final naming pass** — replace `Kiln` and any placeholder names (IP hygiene) | **[Provide]** | Your call on the name |

Dropped from the old plan: 6 side quests, the quest journal screen, dialogue choices, the
escort and survive objectives, and faction reputation.

---

## Phase 8 — Save, settings, audio, balance, MVP exit (weeks 12–13)

**Goal:** a build a stranger can play unattended.
**Exit:** the Tier A exit test passes.

| ID | Task | Owner | Notes |
|---|---|---|---|
| UIX-01 | Save system: 3 slots + 5-deep autosave ring, versioned, migratable, integrity-checked | **[C]** | ◐ brought forward from Phase 8 because the tower made testing without it impractical. Done: 5-deep autosave ring (every shrine, every border), a quick save on **F10**, load newest on **F12**, new game on **Shift+F12**, continue on launch; SHA-256 checksum, version header with a migration table, atomic writes, damaged saves skipped rather than trusted. Open: **one** manual slot rather than three (needs a menu), and shard timers are not saved |
| UIX-02 | Main menu, pause, settings (graphics, audio, controls, language, accessibility) | **[C]** | |
| UIX-03 | Difficulty selection, changeable any time | **[C]** | |
| UIX-04 | Accessibility set per NFR-A.1 | **[C]** | |
| UIX-05 | Full HUD polish pass | **[C]** | |
| UIX-06 | Localisation infrastructure + EN strings extracted | **[C]** | |
| UIX-07 | IT translation | **[C→You]** | I draft, you correct — you're the native speaker |
| AUD-01 | Audio buses, mixing, 3D positional SFX, music transitions | **[C]** | |
| AUD-02 | Source ~40 CC0 SFX + 3 music tracks | **[You]** | Taste + licence check; I'll wire them up |
| BAL-01 | Balance pass using the simulators (XP, yang, DPS, TTK) | **[C]** | |
| BAL-02 | Difficulty tier tuning across all four tiers | **[C→You]** | |
| BAL-03 | **Telegraph/AoE validator**: asserts every telegraph is longer than time-to-safety at that AoE radius and move speed | **[C]** | Non-negotiable for click-to-move (redesign §1) |
| BAL-04 | Performance pass to NFR-P.1 on the low-end machine | **[C→You]** | You run it on the 1060-class box |
| BAL-05 | Bug-fix sweep | **[C]** | |
| BAL-06 | **Tower pass** — per-floor balance, bespoke catacomb monsters, boss phases (WLD-10), a visible exit door on floor 1 and a free "return to the entrance" at the floor-4 and floor-7 shrines | **[C→You]** | Deferred here on 2026-09-21 at your call: the tower works and is fine as it stands until then. The exit today is an unmarked sphere in floor 1's corner (hold Alt to see its sign) |
| TST-01 | **Play the whole slice start to finish, 3 times, on 3 difficulties** | **[You]** | |
| TST-02 | **Exit test: a stranger plays 60 min unattended, unassisted** | **[You]** | The gate to Tier B |

---

## Phases 9–13 — Tier B sketch (re-planned after the MVP)

Not detailed on purpose. Rough shape, ~10–16 months:

| Phase | Content | Main risk |
|---|---|---|
| **9 — Art pass** | Replace all placeholders: characters, enemies, environments, VFX, icons, UI skin | The deferred cost from decision Q2; 80–150 h of your time or €150–400 in packs |
| **10 — Classes** | Blade, Sura, Shaman: ~16 skills each, defensive abilities, balance | Balancing 4 classes solo is genuinely hard; the simulator carries it |
| **11 — Content scale-out** | 3 regions, 12 zones, 5 dungeons, ~55 enemies, 9 shard tiers, 10 bosses, 3 acts, ~73 quests | Pure volume; the data pipeline is what makes it survivable |
| **12 — Systems depth** | Companion, mounts, pets, faction reputation, Order Hall, crafting, sieges, Arena | Scope creep — cut ruthlessly against the 15–25 h target |
| **13 — Endgame + ship** | Tower of Shards, NG+, achievements, Steam/itch integration, store page, trailer, launch | Marketing is a real job; budget 4–6 weeks for it alone |

---

## Critical path and risks

| Risk | Impact | Mitigation |
|---|---|---|
| **Click-to-move feels unresponsive** | Kills the project | Phase 1 is dedicated to it and gated on your feel review (MOV-08) before any combat work |
| **Click-to-move depth doesn't land** | 20 h campaign gets boring | Phase 2 front-loads the four depth systems; CBT-14 is an explicit go/no-go |
| **Placeholder art hides feel problems** | Late rework | Compensate with VFX/hit-stop/audio (CBT-12); accept that the Phase 9 art pass will need a feel re-tune |
| **Scope creep into Tier B** | Never ships | Phases 0–8 only; TST-02 is the gate |
| **Balance by vibes** | Grind or trivialisation | Simulators (PRG-09, ITM-12, BAL-03) make it measurable |
| **Your available hours drop** | Timeline slips | My work is parallelisable and not the bottleneck; the plan degrades gracefully, just slower |
| **Asset licence contamination** | Legal exposure | `ASSET-LICENSES.md` entry required at the moment of import, checked in CI |
