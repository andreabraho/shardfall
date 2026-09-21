# 02 — Single-Player Redesign: difficulty, pacing and system conversion

This is the design document that answers the second half of the brief: *adapt the difficulty
and the systems to accommodate a single-player game*. Every MMO assumption in Metin2 is
listed here with its replacement.

## 0. The five assumptions we have to break

Metin2's design rests on five things that are simply false offline:

| MMO assumption | Offline reality | Consequence for design |
|---|---|---|
| Other players fill the roles you lack | You are alone with one build | Every class must be able to solo everything |
| Time is the resource being sold | Time is the player's budget, not ours | Kill the grind curve entirely |
| Risk is punished so protection can be sold | There is nothing to sell | Remove destructive RNG; make risk cost *resources*, not *progress* |
| The world is persistent and shared | The world is yours | Save anywhere, pause, no respawn competition |
| Content is gated to last for years | Content must be reachable | Level cap 60, everything visible in 15–25 h |

---

## 1. Difficulty model

Four selectable difficulties, changeable **at any time from the menu** (no lock-in, no
achievement penalty — locking difficulty only makes people replay on Easy out of fear).

Because combat is click-to-move (decision D3), **difficulty cannot be tuned with i-frame
windows**. The levers are instead: telegraph length (how long you have to walk out of an
AoE), enemy damage, healing throughput, and enemy behaviour complexity.

| Tier | Enemy HP | Enemy damage | AoE telegraph | Flask charges | Death penalty | Target player |
|---|---|---|---|---|---|---|
| **Wanderer** (story) | ×0.70 | ×0.55 | 2.0 s | 7 | none, respawn at shrine | wants the world and the story |
| **Disciple** (normal) | ×1.00 | ×1.00 | 1.5 s | 5 | lose 10% carried yang | default |
| **Adept** (hard) | ×1.45 | ×1.60 | 1.1 s | 4 | lose 25% yang, gear durability −15% | knows ARPGs |
| **Shardbound** (nightmare) | ×2.10 | ×2.40 | 0.85 s | 3 | above + no mid-dungeon checkpoints | post-campaign / NG+ |

The telegraph column is the single most important number in the game. It must always be
longer than *(distance to safety ÷ move speed) + input latency*, or the mechanic becomes
unavoidable rather than hard — with click-to-move the player needs time to click **and**
walk. AoE radii and telegraph timings are therefore validated together by an automated
check (roadmap task BAL-03), not by eye.

Rules that keep this honest:

- **Difficulty never changes drop tables or XP.** No "play on hard for better loot" — that
  coerces players into a difficulty they are not enjoying. Hard tiers give **cosmetics,
  titles and leaderboard-style records** only.
- **Scaling is multiplicative on HP/damage only**, never on enemy count in the campaign
  (count scaling is reserved for the endgame tower affixes, where it is the point).
- Above Disciple, enemies gain **behaviour** upgrades, not just numbers: shorter telegraph
  windows, a second attack in the combo, and the ability to cancel into a dodge.
- **Accessibility overrides** sit outside the difficulty system and are always available:
  damage-taken slider (25–100%), telegraph-duration multiplier, auto-target-priority assist,
  slow-motion factor, and skip-a-boss after 5 deaths. Documented in requirements NFR-A.

### 1.1 Level scaling policy

**No global level scaling.** Zones have fixed level bands, so out-levelling a zone is a real,
earned feeling. Two guards against that breaking:

- A **catch-up curve**: XP gain gets +35% when you are 3+ levels under the zone band, and
  decays to 25% when 5+ levels over. Prevents both walls and mindless over-levelling.
- **Enemy floor**: trash mobs more than 8 levels below you die in one hit and stop
  aggroing — no tedium, no "running through a low-level zone for two minutes".

---

## 2. Combat conversion — depth inside click-to-move

**The constraint (D3):** click to move, click to auto-attack, skills on a hotbar. Faithful
to the original. No dodge roll, no i-frames, no aiming.

**The problem this creates, stated plainly:** in the original, solo survival is a function of
gear and potion stacks. Press attack, drink when low. That is a *gear check*, and a gear
check cannot carry a 20-hour single-player campaign on its own — there is no competition, no
party coordination and no economy to supply the missing tension. So the depth has to be put
back somewhere else. Four places, none of which require changing the control scheme:

### 2.1 Positioning is the skill layer

Every dangerous enemy attack is a **telegraphed ground decal** with the timing in the
difficulty table. You survive by clicking somewhere safe in time. This is exactly how
click-to-move ARPGs (Diablo, Path of Exile, Last Epoch, Lost Ark) create execution skill,
and it is compatible with the original's feel.

- Decals appear at wind-up start, fill toward detonation, and are readable at a glance.
- Enemies lead their telegraphs toward where you are *walking*, so mindless kiting fails.
- Melee enemies have a **commit window**: once their swing starts they cannot turn, so
  walking through/past them is rewarded. This is the click-to-move equivalent of a dodge.

### 2.2 Active defence on a cooldown (the dodge-roll replacement)

Each class gets one defensive ability, always bound to the same key:

| Class | Ability | Effect |
|---|---|---|
| Warrior | **Guard Stance** | 70% damage reduction for 2 s, but you cannot move |
| Blade | **Smoke Step** | Short blink to cursor + 1 s untargetable |
| Sura | **Dark Ward** | Absorb shield scaling with INT |
| Shaman | **Spirit Shield** | Smaller shield, also cleanses one debuff |

Short cooldown (8–12 s), so it is a *reaction*, not a rotation filler. This restores the
"did you respond in time" moment that i-frames would have provided, without WASD.

### 2.3 Resource management replaces potion spam

- **5-charge flask** (count per difficulty above), refilled at shrines and on zone entry,
  instead of the original's stack-of-500 potions. This is the single most important change:
  while a player can carry 500 potions, no encounter can ever threaten them, and every
  difficulty lever we build is void.
- Mana matters: skills cost real mana, and regeneration in combat is deliberately too slow to
  fund a rotation, so casting has an opportunity cost.
- **Kills pay the mana back** (10% of the pool, nothing from enemies far below you). This is
  what keeps the previous point from becoming "auto-attack and wait", which is where a pure
  regeneration economy always lands: a skill spent to end a fight faster funds the next one,
  so the resource follows the loop instead of throttling it.
- The flask restores **mana as well as health**. Not as a competing choice on the same
  charges — health always wins when it is the thing keeping you alive, so the mana half would
  never be picked. It rides along, which makes the flask the recovery button rather than the
  healing button.
- Consumables (buff scrolls, antidotes, throwing knives) are meaningful because they are
  finite and slot-limited.

### 2.4 Target priority and enemy composition

Waves are composed, not random: **Shielder** (projects damage reduction on nearby allies —
kill first), **Mender** (heals the shard/elites), **Bomber** (suicide AoE, must be pulled
away), **Archer** (forces you to close distance), **Bruiser** (the damage). Solving the wave
in the right order is the tactical layer — and it is a layer that *only* works well in a
click-to-move game, where you can precisely pick a target in a crowd.

### 2.5 Kept from the original

Heavy, committed swing animations; big floating damage numbers; visible weapon trails;
auto-attack chains; and the feeling that a +7 weapon hits noticeably harder than a +5.
Added on top: **hit-stop** (2–4 frames of freeze on impact) and screen shake, which is where
most of the "weight" in modern ARPG combat actually comes from — and which I can deliver with
placeholder capsule art (decision Q2).

### 2.6 WASD as an option, not a design

Options will offer direct WASD movement, because it costs little — both schemes feed the
same destination-move core, WASD simply sets a destination each frame. But **balance,
telegraph timings and enemy design all target click-to-move**, and WASD is labelled as an
alternative control scheme, not a mode.

---

## 3. Shard (metin stone) encounters — redesigned

The signature mechanic needs real encounter design once no other players are around.

**Structure of a shard fight:**

1. **Approach.** The shard pulses; a corruption zone (radius 18 m) is visible. Entering it
   starts the fight and seals the area with a soft barrier.
2. **Phase 1 (100–66% shard HP).** Spawns wave A: 4–6 trash. The shard periodically emits a
   **telegraphed radial pulse** the player must walk out of. Hitting the shard is safe
   only between pulses — so the fight is a rhythm of "clear adds → burst the shard → dodge".
3. **Phase 2 (66–33%).** Wave B adds an elite with a distinct mechanic (shielder, healer,
   bomber). The pulse gains a second ring.
4. **Phase 3 (33–0%).** The shard starts a **reclamation cast**: if it completes, it heals
   30% and re-spawns a wave. Player must burst it down or interrupt by killing the "anchor"
   add. This is the tension beat that replaces PvP competition.
5. **Break.** Screen shake, shockwave that kills remaining trash, loot burst,
   **Shard Essence** currency, and a short "power absorbed" buff.

**Tiers.** 9 shard tiers across the game. Tier determines wave composition, pulse pattern
and loot table — not just bigger numbers.

**Respawn and repetition.** Shards respawn on a 4-minute timer per node, and each respawn
rerolls its **modifier** from a small pool (Frenzied / Warded / Venomous / Twin). So farming
a node is varied rather than identical. A "Shard Divination" map overlay shows active nodes
and their modifiers so farming is a *choice*, never a search.

---

## 4. Itemisation without gambling

### 4.1 Upgrading +0 → +9

Replace "chance to destroy your item" with a **cost-and-pity ladder**:

| Upgrade | Material cost | Base success | Pity (guaranteed after N fails) |
|---|---|---|---|
| +1 → +3 | low | 100% | — |
| +4 → +6 | medium + rare mat | 75% → 55% | 2 |
| +7 | high + Shard Essence | 45% | 3 |
| +8 | high + 2× Shard Essence | 35% | 3 |
| +9 | very high + Radiant Core | 25% | 4 |

- **Failure never destroys and never downgrades.** It consumes the materials and advances
  the pity counter, which is **displayed in the UI**. Risk costs resources; it cannot cost
  progress. This kills save-scumming dead, which is the real design goal.
- On **Shardbound** difficulty only, an optional **Gambler's Anvil** exists: higher success
  rates in exchange for downgrade-on-fail, for players who miss the original thrill. Opt-in,
  clearly labelled, never required.

### 4.2 Sockets

- Socket count is a property of the item's rarity (0–3), visible at drop time. No random
  socket rolls.
- Opening a socket costs a **Boring Stone** (craftable, not RNG-gated).
- Stones slot in freely; **removing a stone costs a small fee and returns the stone intact**.
  Metin2 destroys it; destroying it just means players never experiment.

### 4.3 Bonus lines

- Items roll 2–5 bonus lines from a slot-appropriate pool at drop time.
- Rerolling costs **Mutation Ink** (a steady but finite drop).
- **Line locking:** you may lock 1 line (2 at high rarity) before rerolling, at extra cost.
  This turns rerolling from a slot machine into a convergent process — the player can always
  see themselves getting closer.
- The **damage-vs-monster-type** axis is kept and is a genuine build lever: zones and
  dungeons advertise their dominant enemy family, so swapping to "vs Undead" gear before the
  catacombs is a real, rewarded decision.

### 4.4 Yang economy (single sink)

Yang comes from kills, shards, selling, and quests; it leaves via upgrading, socket work,
rerolls, repairs, fast travel, and companion gear. It is tuned so a player playing normally
is **mildly yang-constrained until mid Act 2 and comfortable after** — money pressure
existing only in the first half is the right shape for a 20-hour game.

No premium currency. No cash shop. Cosmetics drop, or are bought with **Shard Essence**.

---

## 5. Classes, skills and builds

- Four classes, two trees each (as analysed). MVP ships Warrior only; the other three are
  Tier B.
- **Skill unlock:** skill points on level-up, no random books. Books still exist as *items*,
  but they grant a skill point or unlock a tree node deterministically when found —
  keeping the "I found a skill book!" moment without the RNG gate.
- **Mastery:** using a skill accrues mastery toward M/G/P ranks, which add a **mechanical
  change**, not just +damage (e.g. at Master, Whirlwind gains a second rotation; at Grand
  Master it pulls enemies in). This preserves the original's long-tail skill progression
  while making it feel like growth rather than a lottery.
- **Free respec at any shrine.** In an MMO you reroll a character; offline, a bad build is
  just a ruined save. Respec is free and instant.
- Build identity comes from: tree choice + socket/bonus focus + companion archetype.

---

## 6. Party → Companion system

One AI companion, recruited in Act 1, with three archetypes the player swaps freely:

| Archetype | Role | Carries the old party-role buff |
|---|---|---|
| **Guardian** | Taunts, blocks, body-blocks adds | +Defence aura (old "defender" role) |
| **Mender** | Heals on a cooldown, cleanses status | +HP regen aura (old "buffer" role) |
| **Striker** | High DPS, applies vulnerability | +Attack aura (old "attacker" role) |

- The companion levels with you, has 3 gear slots and 4 upgradable abilities.
- It **cannot solo content for you**: its damage is capped at ~30% of player DPS and it does
  not revive you. It fills a hole; it is not a second player.
- **Command wheel** (hold a key): Aggro here / Fall back / Use ability now.
- Companion death = it retreats for 45 s, not a game over.

Pets are separate and smaller: cosmetic + a passive (loot magnet, +XP%, +find rate).

---

## 7. Kingdoms, guilds and PvP → content

| MMO system | Single-player conversion |
|---|---|
| Three-kingdom war | Political backdrop of the campaign. You pick a kingdom at character creation; it changes your starting zone, a questline, and some vendor stock. Not a balance-relevant choice. |
| Kingdom reputation | **Faction reputation** with three factions, earned by quests and shard clearing; unlocks vendors, gear lines and a companion skin per tier. |
| Guild | **Order Hall**: a small upgradable home base (crafting bench, storage, trainer, companion quarters, trophy room). Upgrades are a satisfying yang/material sink and a visible progress display. |
| Castle sieges | Three **scripted set-piece battles** (one per act) with allied AI squads, objectives and a commander boss. The best worldbuilding in the original, finally visible to a solo player. |
| Duels / PvP | **Arena of Echoes**: challenge-mode fights vs AI "echoes" of the four classes, with modifiers. Unlocks titles and cosmetics. |
| World bosses | Stay, as solo-tuned optional super-bosses with real mechanics (the "am I strong enough yet?" checkpoints). |

---

## 8. Progression pacing (the 15–25 h shape)

| Act | Levels | Hours | Beats |
|---|---|---|---|
| Prologue | 1–5 | 0.5 | Tutorial disguised as an escape from a shard-fall; teaches move, dodge, light/heavy, first shard |
| **Act 1** — Valley | 5–22 | 5–7 | Hub village, 4 zones, 2 dungeons, companion recruit, first siege, upgrading introduced at +1–+5 |
| **Act 2** — Highlands & Wastes | 22–42 | 6–9 | 5 zones, 2 dungeons, mount, faction reputation, sockets/bonus rerolling become central, second siege |
| **Act 3** — The Fall | 42–60 | 4–7 | 3 zones, final dungeon, third siege, final boss; +7–+9 upgrading is the gear goal |
| **Endgame** | 60 (cap) | ∞ | Tower of Shards, world bosses, Arena, NG+ |

**Rule enforced by tooling:** the campaign must be completable without ever farming a zone
for XP. I will build a **progression simulator** (a C# console tool that walks the quest/XP
tables and reports level-vs-zone-band deltas) so this is verified by data on every balance
change, not by feel. This is one of the highest-value things I can build for you early.

### 8.1 Endgame: Tower of Shards

- Procedurally assembled floors from hand-made room modules (so it never looks generated).
- Every 5 floors: a boss. Every floor: pick 1 of 3 **affixes** that make it harder and
  increase rewards (this is where enemy-count scaling lives).
- A **daily seed** (offline, derived from the date) gives a shared-feeling challenge with a
  local personal-best board.
- Deaths end the run; you keep materials, lose the floor progress. Runs are 20–40 minutes.

### 8.2 New Game+

Keeps levels, gear, skills, cosmetics, codex. Resets the world and quests. Adds:
enemy tier +1, new elite variants, NG+-only gear line, alternate campaign ending.

---

## 9. Quests and narrative

**Decided 2026-09-21: few quests, and only ones that move the game forward.** This is not a
quest game. The loop is fields, shards, the tower and the upgrade bench; a quest exists only
where the player would otherwise not know where to go next, or where a border needs a reason
to open. Everything below is sized to that.

- **Main progress chain only.** Roughly one quest per step of the road — the village, each
  field map's border, the tower — so about **five in the MVP** and a handful per act after
  that. Each one points at the next place and pays out when you get there.
- **No side quests.** The old plan (~45, each with a mechanical reward) is dropped. Where a
  side quest would have unlocked a recipe or a vendor, that unlock comes from the world
  instead — a shard, a tower floor, a level.
- **No kill counts as objectives.** Still true, and now easier: the chain only ever asks you
  to *reach* somewhere, *break* something, or *talk* to someone.
- **Linear dialogue.** A few lines from an NPC, no branching choices, no ending variants.
- **No journal screen.** The current objective is one line on the HUD and one marker on the
  map; there is never more than one active quest to track.
- **Codex/Bestiary** stays, and is not a quest system: it fills as you kill and discover,
  and replaces the wiki that MMO players rely on.

---

## 10. Dungeons

Solo-tuned, 25–45 minutes, each with: a traversal gimmick, 2 mini-bosses, an optional secret
room, and a boss with 3 phases. Checkpoints before each boss (removed on Shardbound).
Re-runnable with a tier selector for loot.

### 10.1 The format: a floor tower

**Decided 2026-09-20.** Dungeons are built as a stack of floors, each one unlocked by
finishing a **task**, not by walking to the far wall. The analysis of the original's version
is in [01](01-metin2-analysis.md) §2.13.1; this is what we take from it.

The rule that carries the whole format: **every floor has a different verb.** Nine floors of
"clear the room" is one floor nine times. The verbs we have, in roughly the order they get
harder to read:

| Verb | What the player does |
|---|---|
| **Break** | Destroy a named shard; the stairs open |
| **Hold** | Survive waves for a duration, in a space that gets worse |
| **Find** | One of several identical shards is the real one — it has a *tell* |
| **Carry** | Each wave drops a key; seat them all in the seal |
| **Race** | A timer starts when you break the first thing; finish before it ends |
| **Fight** | A boss, alone, on an empty floor |

**A boss every third floor.** It gives the climb a pulse: the player always knows roughly how
far the next punctuation mark is. Floors between bosses escalate; the boss resets the tension.

**After every boss floor, a shrine and a bench.** This is the original's best idea — the
smith who appears after the floor-6 boss — and it lands even better here because we already
have both pieces. It is a reward that is a *decision* rather than an item, handed over in the
middle of a run at the exact moment the player knows which piece is holding them back.

**One floor in the middle has a refuge**: a corner with nothing in it. Cheap to build, and it
changes how the floor is played — somewhere to drink, re-read the room, and go back in.

**No "kill everything" floor.** It is a chore verb that exists to consume time, and we have no
subscription to defend.

**The find-the-real-one floor gets a tell.** In the original it is one of seven identical
stones against a timer, which with a party is a search and alone is a one-in-seven guess. Ours
differs in something the player can actually perceive — it is the one that is *not* doing what
the others are doing.

### 10.2 Two towers, on purpose

The endgame **Tower of Shards** (§8.1) is also a tower, and that is a deliberate relationship
rather than a duplication:

| | Act 1 dungeon | Tower of Shards |
|---|---|---|
| Built | Hand-authored, fixed | Procedural from room modules |
| Length | 6–9 floors, ends | Endless, run ends on death |
| Direction | **Descends** | **Ascends** |
| Job | Teaches the format | Varies it forever |

The campaign tower is where the player learns what a floor task is and what the boss pulse
feels like. The endgame tower then has a vocabulary to draw on from its first floor, instead
of spending its opening run explaining itself.

The descent is not decoration. The campaign dungeon is `zone_catacombs`, whose shrines were
already named for a mouth, a gallery and a vault — the shape was in the data before the format
was decided, and going down rather than up is what keeps the two towers from reading as the
same building twice.

---

## 11. Mounts, pets, cosmetics

- **Mount**: unlocked mid Act 2; +80% move speed, a charge attack that staggers, dismounts on
  heavy hit. Two more mounts as rewards.
- **Pets**: 6, each a passive (loot magnet / +XP / +material find / auto-revive once per zone).
- **Cosmetics**: full transmog, always cosmetic-only, earned from bosses, achievements,
  reputation and the tower.

---

## 12. Quality-of-life mandated by single-player

These are requirements, not nice-to-haves:

- Save anywhere; 3 manual slots + 5-deep autosave ring; pause anywhere.
- Fast travel between discovered shrines, with a small yang cost that vanishes late.
- Auto-loot with a rarity filter; inventory auto-sort; item comparison tooltips; a "mark as
  junk → sell all junk" flow.
- Map with fog of war, shard-node overlay, quest markers, custom pins.
- Skippable cutscenes, re-readable dialogue log, re-watchable cutscene gallery.
- Death → respawn at the last shrine in ≤ 4 seconds. Long death sequences are the fastest
  way to make a hard game feel unfair.
