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
| CBT-11 | Healing flask with charges, cast time, interrupt | **[C]** | ✅ refilled by bought draughts since REF-02 |
| CBT-12 | Game feel: hit-stop, screen shake, hit flash, floating damage numbers, impact VFX | **[C→You]** | ✅ hit-stop, shake, flash, numbers, AoE flashes |
| CBT-13 | Object pooling for projectiles/numbers/VFX (NFR-P.5) | **[C]** | |
| CBT-14 | **Combat feel review** + written notes | **[You]** | |
| CBT-15 | Placeholder visual set, plus role markers (shape + colour) | **[C]** | ✅ role is what matters tactically, so markers encode role not family |
| CBT-16 | Crowd separation: enemies push each other apart with a soft radius, overlapping up to a limit rather than not at all | **[C]** | ✅ Added 2026-09-20 on your note. Hard collision makes a pack queue single-file down a corridor; none at all makes six creatures one creature. The player counts as a neighbour, so a pack no longer stands inside them |
| CBT-17 | **Four-hit basic attack chain**, each swing different, the fourth sweeping non-boss enemies away | **[C→You]** | ❌ **Replaced 2026-09-21 by REF-01** (area auto-attack on an attack-speed timer, Metin2 model). Was: ✅ Added 2026-09-20. Wants CBT-16 first: a finisher that throws a crowd is only readable once the crowd has shape. Jab, cross, cut, sweep — the fourth is 360°, throws what it catches, and is slow enough to be a decision. Knockback is new and lives on the brain |

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
| PRG-05 | Skill mastery accrual + rank-up mechanical changes | **[C]** | ✅ ranks change behaviour, not just numbers; Master is bought with points since REF-03 |
| PRG-06 | Free respec at shrines | **[C]** | ✅ (WLD-02) — free, per FR-2.6; refunds attributes only, as skills have nothing to respec |
| PRG-07 | Catch-up XP curve + low-level enemy floor (FR-2.7) | **[C]** | ✅ |
| PRG-08 | Skill VFX with primitives + Godot particles | **[C→You]** | You call readability |
| PRG-09 | **Balance simulator v1** — walks the XP table, reports level-vs-zone-band deltas | **[C]** | ✅ runs in CI; found the first draft gave quests 88% of all XP |
| PRG-10 | **Character sheet** (C): spend attribute points, derived stats live, skill list | **[C]** | ✅ added 2026-09-19 — the phase awarded points and nothing could spend them. Skill points are spent on their own screen (V) since REF-03 |
| PRG-11 | **Skill points have no sink** — decide whether they become a real choice | **[Provide]** | ✅ answered in REF-03: option 2. Points buy a skill up to Master (7 each, 56 for all eight against ~25 earned), and use carries it from there |

**PRG-11, answered (REF-03, 2026-09-22).** Points buy ranks. A skill takes seven, the seventh
masters it, and the campaign grants about twenty-five in total — so the eight skills cost more
than twice what the player will ever hold, and which of them a character is actually good at
is the build. Grand Master and Perfect stay earned by casting, so the long tail survives.

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
| ITM-10 | Yang economy + sinks; vendor buy/sell | **[C]** | ✅ Idra in Ember Hollow: staples (scrap, oil, boring stone, ink) at list price, a shelf of 5 common/fine pieces within −4…+1 of your level that changes on level-up (seed-deterministic, FR-10.3), sells at 25%, last 6 sales can be bought back. Purchases and buy-backs are saved with the game |
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
| WLD-02 | Shrine system: save, respawn, fast travel, respec | **[C]** | ✅ the flask refill moved to merchants in REF-02 |
| WLD-03 | Spawn-zone system: density caps, respawn timers, distance activation | **[C]** | ✅ camps come back as camps, and keep their timers while you are away |
| WLD-04 | Modular greybox kit (cliffs, paths, ruins, props) as primitives | **[C]** | ✅ 18 pieces in `tables/world_kit.json`; a scene names a piece id and nothing else. Editor preview works: content loads on demand in the editor, pieces rebuild when the id changes, and safe zones and camps draw their radius as a ring |
| WLD-05 | **Greybox the 4 field maps** — layout, landmarks, encounter placement, sightlines | **[C→You]** | ✅ all four built and connected: Vale Approach (a corridor with a pinch), Vale Floor (a basin with barrows), the Ridge (the old arena) and Broken Gate (a curtain wall with two ways through). Layouts are a first pass — level design by feel is yours |
| WLD-06 | **Greybox the 2 villages** + NPC placement | **[C→You]** | ◐ village 1 (`ember_hollow.tscn`) built: 36 m walled square, one gate, shrine, safe ground r22, three camps 40 m out. Village 2 waits on WLD-05; Elder, smith and merchant placed (QST-06). Layout is a first pass — level design by feel is yours |
| WLD-12 | **Safe zones**: no spawn overlap, no enemy entry, no damage inside — validated | **[C]** | ✅ `SafeRegion` + `SafetyField` in Core; declared per zone in data, placed by a `SafeZoneNode` in the scene. Enforced at all three points: aggro drops at the boundary, damage to the player is refused, and `ZoneRoot.Audit` errors on a camp within 6 m of the line |
| WLD-13 | **Multi-hub**: relax the one-hub rule, each village anchors its region | **[C]** | ✅ "exactly one hub" → "at least one"; reachability still seeds from the starting village, so village 2 must be walkable from village 1 or it reports as an orphan |
| WLD-07 | **Greybox the dungeon** as a floor tower: per-floor task, boss every third floor, shrine + bench after each boss | **[C→You]** | ✅ `catacombs.tscn` — nine floors on one footprint, stacked 24 m apart, one navigation bake. Verbs: break · hold · **fight** · find · carry · **fight** · race · hold · **fight**, which is six distinct and no repeat in a row. Shrine + bench on floors 4 and 7, refuge and the optional shard on floor 5. The rules are enforced in the content validator, not in review: FR-7.12/7.13/7.14/7.17 all fail the build if an edit breaks them. Floor pacing and boss difficulty are yours |
| WLD-08 | Map + minimap: fog of war, markers, custom pins, shard-node overlay | **[C]** | ◐ zone map on **M**: drawn from the live scene (kit pieces, gates, shrines, shards, camps, safe ground), with fog of war remembered per zone across borders. Custom pins and a corner minimap still open |
| WLD-09 | Fast travel UI + yang cost | **[C]** | ✅ flat fee by destination band; refused in combat, and to a dungeon checkpoint |
| WLD-10 | 2 bosses: phases, mechanics, arenas | **[C→You]** | ◐ three exist — the Gallery Warden, the Vault Chorister and the Heart Keeper — each alone on its own floor with three telegraphed abilities. Arenas done, mechanics done, **phases not**: they fight the same way at 10 % as at 100 %. Difficulty tuning is yours |
| WLD-11 | **Zone-layout review and iteration** | **[You]** | |

### Map themes (decided 2026-09-23)

Each of the six maps gets one theme: its own monsters, its own shards (the metin-stone
equivalent) and its own boss, all built from **free CC0 packs only**. The themes were picked
from what free 3D models actually exist, so that no map has to wait on art that does not.
Themes are generic genre settings; no names, layouts or assets from any existing game.

| Zone | Levels | Theme | Environment | Monsters | Shard | Boss |
|---|---|---|---|---|---|---|
| Ember Hollow | 1–3 | Village and woods with **animals** | Medieval Village MegaKit, Stylized Nature MegaKit | Wolf, Fox, Deer, Bull, Rat | Moss- and root-grown stone | Alpha wolf |
| The approach | 3–7 | Second village overrun by **humanoid monsters** | Medieval Village MegaKit (ruins), Fantasy Props MegaKit | Tribal, Ninja, Wizard, KayKit bandits | Stone with a camp banner and chains | Bandit chief |
| Valley floor | 7–11 | **Orc** valley | Stylized Nature MegaKit, KayKit Forest, Fantasy Props | Orc, Orc Enemy, Goleling, Mushnub | Stone with crude totems and spikes | Orc warlord |
| The ridge | 11–15 | **Desert with spiders**, the giant spider lair a cave inside it | Ultimate Nature Pack (cacti, palms, dead trees, rocks), KayKit Dungeon for the lair | Spider in three sizes, Snake, Cactoro, Wasp | Web- and sand-wrapped stone | Spider queen, in the lair |
| The broken gate | 15–19 | **Undead mountain** in snow | Ultimate Nature Pack (snowy pines, rocks), Kenney Graveyard Kit, KayKit Halloween Bits | KayKit Skeletons, Ghost, Ghost Skull, Yeti | Frosted stone with runes | Lich, or the Yeti as the mountain's guardian |
| The catacombs | 19–23 | **Demon tower with zombies** | KayKit Dungeon Remastered, KayKit Halloween Bits | Zombies (four kinds), Demon, Blue Demon on the upper floors | Cursed stone with a violet glow | Demon lord |

**Built so far (2026-09-23):** maps one to five, each with its roster, a boss in its own
stronghold, and three themed shards on two-minute respawns. Bosses: Greymane (the alpha wolf),
Garrow the Sacker (bandit chief), Gorthak the Warlord (orc), the Spider Queen, and Vaelith the
Frost Lich. The sixth, the catacombs, became the **Demon Tower** (2026-09-23): zombies below, demons
above, red and purple flagstones, and floors that fight back — everything a floor sends hunts the
player, hold floors keep a pack at strength, the first and seventh floors are real demon stones,
each lantern lets out a wave, each keystone is sealed until its guard dies, boss floors send
waves too (FR-7.13 relaxed), and the Demon Lord calls help at 75/50/25 per cent and enrages at
half.

Fire land was dropped: there is no free lava kit, and it would have been a seventh map. The
downloads (about 450 MB, all CC0, licence files checked) sit outside the repository; a model
is copied into `game/assets/` only when a map uses it, with its `ASSET-LICENSES.md` row first.
Building the maps is folded into REF-04 (enemies), REF-05 (bosses), REF-06 (shards) and REF-13
(world).

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
| QST-01 | Quest state machine: one active quest, objectives, prerequisites, rewards, saved with the game | **[C]** | ✅ `QuestChain` in Core: progress only counts while its quest is active, a save naming a renamed quest recomputes where it is, loaded progress stops one short so a lowered count can never strand a finished quest |
| QST-02 | Objective types: kill / reach / break / talk / clear a tower | **[C]** | ✅ kill, reach, shard, clear_tower live; talk waits on NPCs. The validator rejects a hunt for anything no camp or floor spawns, and any second active quest |
| QST-03 | Linear dialogue: a few lines per NPC, no choices | **[C]** | ✅ bottom-of-screen box, F / click / Space to go on, Esc to close. One ordinary line per conversation in turn; the elder adds a line for the active quest first. Lines are plain text until Phase 11 |
| QST-04 | Current objective as one HUD line + one map marker | **[C]** | ✅ HUD panel top right: quest, count, and which map the creature lives in. On the map (M), every camp with the quarry is marked through the fog; in any other map, the border on the shortest road there is marked instead |
| QST-05 | **The chain: about five main-progress quests**, village → fields → tower | **[C]** | ✅ six: reed hoppers, thornback boars, pale gnawers, corrupted wolves (opens the Broken Gate), gate breakers, then clear the catacombs. Rewards on the chain formula; a real-content test locks "mostly hunts, one is the tower" |
| QST-06 | The NPCs the chain and the economy need (elder, smith, vendor) | **[C]** | ✅ Corwen (elder, talks), Hesk (smith, opens the workbench), Idra (merchant, opens the shop), from `data/npcs/`; F at close range, shown on the map. Placeholder capsules until real models |
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
| UIX-01 | Save system: 3 slots + 5-deep autosave ring, versioned, migratable, integrity-checked | **[C]** | ✅ 5-deep autosave ring (every shrine, every border, and on leaving), **3 manual slots** from the pause menu (a slot that holds something takes a second press to overwrite), quick save on **F10**, load newest on **F12**; SHA-256 checksum, version header with a migration table, atomic writes, damaged saves listed as damaged and never loaded. Also saved: play time, every broken shard's respawn timer (kept across borders too, so stepping out and back in no longer resets a shard), and each merchant's purchases and buy-back list. Open: nothing |
| UIX-02 | Main menu, pause, settings (graphics, audio, controls, language, accessibility) | **[C]** | ◐ main menu (Continue shows where it goes, New game, Load, Settings, Quit); pause on **Esc** when no panel is open (Resume, Save, Load, Settings, Quit to menu or desktop — leaving autosaves); settings apply at once and live in `user://settings.cfg`, apart from saves: fullscreen, vsync, frame limit, 3D resolution, three volume buses, and the list of keys. Language picker added with UIX-06. Open: key rebinding, accessibility (UIX-04) |
| UIX-03 | Difficulty selection, changeable any time | **[C]** | ✅ chosen on New game (four cards with the numbers), changeable from pause → Settings; saved with the game. A change applies to creatures spawned after it. Each new game also gets a fresh seed |
| UIX-05 | Full HUD polish pass | **[C]** | |
| UIX-06 | Localisation infrastructure + EN strings extracted | **[C]** | ✅ one table per language in `game/data/strings/<code>.json`. Content names and villager lines are `$keys` with the English in `en.json` (176); interface text is English in the code wrapped in `L10n.T("…")` / `L10n.F("… {0} …", value)`, the English being its own key (374). A missing translation shows English, never a key. Numbers follow the language (12,000 / 12.000). `strings` in the tool reports coverage and fails the build on a content key with no English or an interpolated string handed to L10n; `strings --template it` lays out a file to translate. Language picker in Settings. Debug tools (F1–F9) stay English |
| UIX-07 | IT translation | **[C→You]** | ◐ draft done: `game/data/strings/it.json`, all 550 entries (names, villager lines, interface). Map and creature names translated (Conca delle Braci, Crinale, Saltacanne…); **yang** kept. `strings` fails the build if a translation drops or invents a `{0}`. Yours: read it in game (Settings → Lingua → Italiano) and correct the wording |
| AUD-01 | Audio buses, mixing, 3D positional SFX, music transitions | **[C]** | ✅ `data/tables/sounds.json`: 27 effects and 4 music tracks by meaning (hit, crit, swing, telegraph warning, death, pickup, yang, rare drop, shard break, shrine, quest, upgrade success/fail, trade, UI click/open/close…), all wired into the game and all silent until files are listed. Random variant and pitch per play, a voice cap per sound, positional where it matters, music per map crossfaded over 1.5 s at every border and on the menu. Volumes on the Master/Music/Effects sliders. `validate` warns about a listed file that is not on disk; F3 shows the last sound asked for |
| AUD-02 | Source ~40 CC0 SFX + 3 music tracks | **[You]** | ◐ effects done 2026-09-21: 114 sounds from three Kenney CC0 packs (RPG Audio, Impact Sounds, Interface Sounds) cover all 27 effects, several variants each; licences in ASSET-LICENSES.md and beside the files. **Music still yours to choose**: menu, village, wilds, catacombs (drop the files in `game/audio/` and name them in `sounds.json`) |
| BAL-03 | **Telegraph/AoE validator**: asserts every telegraph is longer than time-to-safety at that AoE radius and move speed | **[C]** | Non-negotiable for click-to-move (redesign §1) |
| BAL-05 | Bug-fix sweep | **[C]** | |
| BAL-06 | **Tower pass** — per-floor balance, bespoke catacomb monsters, boss phases (WLD-10), a visible exit door on floor 1 and a free "return to the entrance" at the floor-4 and floor-7 shrines | **[C→You]** | Deferred here on 2026-09-21 at your call: the tower works and is fine as it stands until then. The exit today is an unmarked sphere in floor 1's corner (hold Alt to see its sign) |
| QST-10 | ~~**To analyse at the end: targeted quests with unique rewards**~~ **Left out 2026-09-25 at your call (REF-16).** — a few hand-placed quests that pay out a unique piece of gear (or something else worth the detour), if the finished game turns out to need them | **[C→You]** | Added 2026-09-21 at your call, after the main chain landed. Not a commitment: decide once the full loop is playable whether the chain alone carries it. Must stay few (doc 02 §9) |
| QST-07 | ~~**To analyse at the end, probably dropped: codex/bestiary**~~ **Dropped 2026-09-25 at your call (REF-16).** filling on kills | **[C→You]** | Moved here 2026-09-21 at your call: not seen as very useful. Only if the finished game turns out to miss it |
| UIX-04 | **Deferred to the end: accessibility** set per NFR-A.1 (text size, colour-blind-safe markers, less shake and flash) | **[C]** | Moved here 2026-09-21 at your call. Done after the refine phase, together with REF-11 (interface) |
| BAL-01 | Balance pass using the simulators (XP, yang, DPS, TTK) | **[C]** | **Moved to the end 2026-09-21 — you decide later what to do with it** |
| BAL-02 | Difficulty tier tuning across all four tiers | **[C→You]** | **Moved to the end 2026-09-21 — you decide later what to do with it** |
| BAL-04 | Performance pass to NFR-P.1 on the low-end machine | **[C→You]** | You run it on the 1060-class box · **Moved to the end 2026-09-21 — you decide later what to do with it** |
| TST-01 | **Play the whole slice start to finish, 3 times, on 3 difficulties** | **[You]** | |
| TST-02 | **Exit test: a stranger plays 60 min unattended, unassisted** | **[You]** | The gate to Tier B |

---

## Phase 8.5 — Refine: every system to its final version (added 2026-09-21)

**Added at your call.** Once Phase 8 is finished — and before anything in Tier B — we stop
adding and go back over what exists. One system at a time, together: I lay out how it works
today, what the design docs promised, what playing it has shown, and the options; you decide;
I build the final version. Nothing moves to the next system until the current one is signed off.

**Goal:** every system in the game is in its final, decided form, not the first working one.
**Exit:** every row below is ✅ with your sign-off.

| ID | System | Owner | What gets decided |
|---|---|---|---|
| REF-01 | **Combat core** — basic attack, targeting, damage formula (crit, pierce, evasion, mitigation), hit feel | **[C↔You]** | ✅ **signed off by you 2026-09-22.** Decided: the Metin2 model. Auto-attack as a state machine (approach → windup → hit check for reach, line of sight and life → damage or miss → recovery), timed by attack speed alone (`AttackCycle`); every blow hits the target and every enemy in a 120° arc in front, within reach, at full damage; every fourth blow of a fight also throws back what it catches (bosses excepted), with no arc drawn; moving cuts the recovery animation, never the timer. Standing still during the windup and every skill, turning to follow. Space strikes the nearest enemy in front once, no lock-on; held, it repeats. No knockback: hits make the enemy flinch in place. Hit-stop and camera shake on hits removed. Stuns only from Ground Slam (30%, 1.5 s); tower bosses immune (`stun_resist`, enforced by the validator). Shield Bash puts −20% defence on the target for 5 s. Replaces the four-swing chain (CBT-17) |
| REF-02 | **Defence and survival** — guard, status effects on the player, health flask, death and respawn | **[C↔You]** | ✅ **signed off by you 2026-09-22.** Decided: Guard Stance stays an active 70% block with the character rooted, shortened to **1.2 s** (1.6 s at Grand Master). **Stagger removed** — the pool is gone from every enemy, the value from every skill, and the two mastery notes that promised more of it now promise damage; control comes from Ground Slam's stun, Shield Bash's vulnerability and the sweeping fourth blow. **The flask is refilled only by buying draughts** (`mat_flask_draught`, 250 yang, sold by every merchant, one per charge, poured by clicking it in the bag): no refill out of combat, at a shrine, or on death; charges are saved. **Dying costs experience, not yang** — 10% of the level on Disciple, 25% on Adept and Shardbound, nothing on Wanderer, never taking a level. Statuses on the player are unchanged |
| REF-03 | **Skills** — the eight Warrior skills, mana, cooldowns, mastery ranks, skill bar | **[C↔You]** | ✅ **signed off for now by you 2026-09-23** — a second pass is booked as REF-21 at the end. Decided: **skill points are spent by the player** — K opens the skill screen, nothing is learned automatically and nothing is gated on level, a new character starts with one point, the first point learns a skill and the seventh masters it, and each point adds 6% to its damage (or to its duration when it deals none). **Grand Master and Perfect are earned by casting a mastered skill** (20 and 35 casts since Master — cut from 200 and 600 on 2026-09-25 at your call). **Iron Skin became a real buff** (−35% damage taken for 10 s) and **Rally Cry became the Blade Aura** (+25% damage dealt for 20 s); both wear a turning ground-ring aura, are cast with a raised weapon, and sit on slots 6 and 7. **Whirlwind is now a 160° cone 6 m deep** in front of the Warrior, two and a half times the basic attack's reach, its hits 0.42 s apart with the Warrior turning a full spin on every one; the arc itself stays in front. Skills without a target go straight ahead of the Warrior, never towards the cursor. **Ground Slam is centred on the Warrior** instead of aimed at the cursor. Hovering a slot, or a row in the skill screen, states the damage, the shape, the effect and what the next rank costs. The player puts skills on keys by dragging them onto the bar, takes them off by dragging them away or right-clicking, and the arrangement is saved. **Every effect wears its mastery rank**: the skill's own colour at Normal, amber at Master, violet at Grand Master and, at Perfect, a deep violet with the blade itself burning gold, each brighter and wider than the last — the two auras, the ground flash of every area skill, a small ring under a single target, and the guard dome. A shrine refunds skill points along with attribute points. Closes PRG-11 |
| REF-04 | **Enemies and AI** — the five roles, aggro and leash, telegraphs, what each creature does | **[C↔You]** | First pass 2026-09-24: every telegraphed attack now used in turn; camps join a fight within 8 m; shield rings; per-creature traits (rat and spiderling packs, wolf howl, cutthroat flank, orc enrage, thornguard thorns, bone minion rise, frost and venom slows). The one-hit kill of trivial enemies is gone: 8+ levels below, they drop half. Second round: rats and foxes passive, 12 m call, 24 m leash, packs respawn together, about 30% more creatures per camp (cap 48), death animations, lighter telegraphs (thin outline, faint fill). Tower creatures left as they are for now |
| REF-05 | **Bosses** — phases, mechanics, arenas, rewards | **[C↔You]** | Folds in WLD-10 (boss phases). First pass 2026-09-24: phases in data for all eight bosses (adds, enrage, faster cooldowns, slowing blows, the sorcerer's blink), a boss bar at the top of the screen and the boss plate, a leash measured from the player (40 m) with only half the lost health back on reset, and a table of its own per boss with guaranteed picks and a rare step up. Respawn stays 5 minutes. A unique item per boss is for REF-09 |
| REF-06 | **Shards** — the metin-stone equivalent: tiers, waves, modifiers, anchors, respawn, rewards | **[C↔You]** | First pass 2026-09-24: four shards a map at random spots (a new one each respawn, none on the map view), waves from the map's own creatures and never a boss, a 5% chance a field shard calls the map's boss at phase two, and a table of its own per shard (guaranteed picks, a socket gem in a third of breaks). Fight shape, all four modifiers, the anchor and the 2-minute respawn stay |
| REF-07 | **Movement and camera** — click-to-move, WASD, pathing, camera angles and zoom | **[C↔You]** | Folds in the MOV-08 feel review. First pass 2026-09-24: a click on a creature reaches it through scenery and counts its whole model (TargetPicker); pitch dragged between 30° and 70°; zoom to 20 m; the map and inventory are side panels at the screen edges that leave play running. Speed, camera reset, rebinding and shake stay as they are, at your call |
| REF-08 | **Progression** — levels, experience curve, attributes and respec | **[C↔You]** | First pass 2026-09-25: monster experience −20% (quest experience −4% to keep the mix in tolerance), DEX worth about a third more, 3 attribute points a level, a chat line on level-up, shrines fixed (the panel lookup missed the Session node) and restyled as waystones, the Colossus ×3 and the Demon Lord ×10 health. Level cap, experience mix, INT and the free respec stay |
| REF-09 | **Items and loot** — rarities, bonus lines, drop rates, ground loot, tooltips | **[C↔You]** | First pass 2026-09-25: loot is picked up on purpose — Z gathers everything within 4 m, a click on a drop picks it up from 2.5 m or walks there first; walking over it no longer does (camera turning moved to the arrow keys). Resistance lines removed (no creature deals elemental damage, and "all" covered physical too); the Ward stone gives defence instead, armour and shields gained a mana and a regeneration line. The Ridge creatures drop from the Ridge table, which nothing used. Equipment drop chances −30% everywhere; boss picks unchanged. Relics from the tower bosses: at least one in 10% of Colossus kills, 14% of the Sorcerer's, 18% of the Demon Lord's (0.28% each from catacomb creatures). Rarity stays fixed per item, bonus ranges stay flat, no loot filter, names always shown, 60 s on the ground |
| REF-10 | **Inventory and equipment** — bag grid, slots, locking, dropping, destroying | **[C↔You]** | First pass 2026-09-25, as the original: the bag is two pages of five columns by nine rows (nothing lies across two pages), the worn pieces stand on a figure above it, and items are carried — dragged within the bag, onto the figure to wear, off it to take off, out of the window to drop. Right-click wears or uses; Shift + right-click destroys (asks); Ctrl + right-click locks. An instant coloured item card replaces the plain tooltip, and a refused equip says why. Icons from game-icons.net (CC-BY 3.0) wait on your download OK. No stack splitting, no storage |
| REF-22 | **The main character** — the Warrior's look, model, animations, how worn gear shows | **[C↔You]** | Added 2026-09-25 at your call, placed after REF-10 so the look is settled with the equipment it has to show. First pass 2026-09-25: worn gear shows on the model (sword or great sword, a shield shaped by its rarity, the helmet, a cape over epic armour), a blade glows from +7 (faint, lit, gold at +9); weapons are swords or great swords (the axes and spears became great swords), each with its own four-blow chain timed to the attack, the fourth the widest; every skill has its own move; one skill at a time, the next pressed is cast when it ends; the Barbarian holds one axe. One character, no customisation. The Warrior is now the KayKit Adventurers 2.0 Knight with the KayKit Character Animations (both CC0, downloaded at your OK): swords and shields are separate pieces put in its hands, and the clips come from four shared animation files |
| REF-11 | **Upgrade and workbench** — anvil ladder, pity, materials, sockets and stones, rerolls | **[C↔You]** | First pass 2026-09-25: upgrading only at a smith or a tower bench (the U key is gone); a smith window beside the bag in the original's style — an item carried or right-clicked onto the anvil, three tabs (upgrade, sockets, bonus lines), the next level's numbers, every material as its icon with held/needed; the Blessing Scroll (+10% on one attempt) drops from bosses only — map bosses 10%, Colossus and Sorcerer 30%, the Demon Lord always; the Gambler's Anvil is dropped. Ladder, odds, costs, stones and the merchant's Boring Stones and ink stay |
| REF-12 | **Economy and merchant** — yang income and sinks, prices, shop stock | **[C↔You]** | Folds in the "yang" naming question. First pass 2026-09-25: the currency is **gan** on screen (the code still says Yang), with a gold coin drawn before every sum; the merchant is a grid of icons beside the bag in the original's style (right-click buys, Shift ten, drag from the bag to sell, a buy-back row); a pedlar, Tobin, stands by the Broken Gate's yard shrine with the same stock. Sale price 25%, prices and the economy balance stay |
| REF-13 | **World** — maps, camps and spawns, safe zones, borders | **[C↔You]** | First pass 2026-09-25: the six camps round Ember Hollow moved out to 58–60 m, past the reach of their 24 m leashes; nothing is born on safe ground — a creature placed there is born just past the edge (SafetyField.PushOut); a small safe camp (11 m, a shrine, fire, shelter, banner, bench, no merchant) at the entrance of each of the four open maps past the village. Density, respawn times and the level gates 6/10/14+quest/18 stay |
| REF-14 | **Shrines and travel** — shrines, fast travel, checkpoints | **[C↔You]** | First pass 2026-09-25: the rest at a shrine stays as it was; a shrine saves at most every 5 minutes (a new shrine always saves); travel costs ×5 (600 gan + 225 per level of the destination band); no return scroll; the travel list is grouped by map, maps in level order with their level range, the coin before every fare, the shrine underfoot shown as "you are here"; on death the player chooses where to get up — at the last shrine (the last safe floor, in the tower) or in the village, as in the original |
| REF-15 | **Tower** — the nine catacomb floors, tasks, checkpoints, exit | **[C↔You]** | Folds in BAL-06. First pass 2026-09-25: the way out on floor one keeps its sign up ("Way out · Broken Gate") while the player is on that floor; the checkpoint shrines on floors 4 and 7 offer a free "Leave the tower" to the entrance; the stair past the last boss leads out to the entrance instead of into a fresh climb. Kept as they are, at your call: the free upgrades on every climb, every climb from floor 1, the boss drop as the only reward, the one timer on floor 7. **Per-floor balance waits for your full playthrough at the end of the refine phase** |
| REF-16 | **Quests and villagers** — the main chain, dialogue, NPCs | **[C↔You]** | Folds in QST-10 (unique-reward quests) and QST-07 (bestiary). First pass 2026-09-25: **the name "Demon Tower" stays at your call**, although it is the original's; the chain stays as it is (six quests, finished and paid on the spot, no turn-in, experience and gan only, no more lines); **QST-10 left out and QST-07 dropped**; the dialogue is a window in the original's style (the speaker on a title bar, the words written out a letter at a time, Next and Close); **a new villager, Brann the armourer**, in the square's fourth corner, always selling the base weapons and armour of the first levels (levels 1–8, no bonus lines, sockets as the item has them). Villager models: the free KayKit Adventurers 2.0 has only the Knight, so the villagers wait on your OK for another CC0 pack |
| REF-17 | **Difficulty** — the four tiers and what each changes | **[C↔You]** | Folds in BAL-02. 2026-09-25: **the four tiers stay as they are, at your call** (values, changeable mid-game, no extra reward; Shardbound's unbuilt "no tower checkpoints" stays unbuilt), numbers left to the final playthrough. Added at your call: **a draught cannot be poured into the flask within 3 s of taking a hit** |
| REF-18 | **Saving** — slots, autosaves, what is kept | **[C↔You]** | First pass 2026-09-25: **several characters**, each with a folder of its own (3 slots, the quick save, 5 autosaves) — a new game asks for a name, Load game lists the characters (level, map, difficulty, time played; Play, Saves, Delete with a second press), Continue picks up the character played last, older saves move into a folder of their own on first launch; **an autosave after every roll at the smith** (upgrade, gift, reroll) so a result cannot be undone by loading; **an autosave every ten minutes of play** without another save; slots unchanged; F10/F12 and the other debug keys stay for now. Also fixed: **a shard no longer lets its waves out again** when the player walks out and back in (walking out still heals the stone) |
| REF-19 | **Interface** — HUD, panels, map, menus, settings | **[C↔You]** | Accessibility (UIX-04) stays at the very end |
| REF-20 | **Audio** — which sound for what, music | **[C↔You]** | |
| REF-21 | **Skills, second pass** — back to the skill system once everything else is final | **[C↔You]** | Added 2026-09-23 at your call: REF-03 is good enough to leave for now, and gets another look last, when enemies, bosses, items and balance around it are settled. Candidates already noted: Whirlwind's swirl and spin, the aura art (the Kenney Particle Pack, CC0, is still waiting on your OK), how fast Grand Master and Perfect arrive, and whether 6% a point is the right growth |
| REF-23 | **Boss rewards: skins** — each boss drops a cosmetic skin for weapons or armour | **[C↔You]** | Added 2026-09-25 at your call, after every other refine: first an analysis of whether it can be done, what it costs and how (models, licences, how a skin is worn over an item), then the build. Replaces the unique boss item REF-05 had pointed here |

For each system: I lay out how it works today, you say how you want it, I build it, you
test it, and only then do we move to the next. Order is yours to change.

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
