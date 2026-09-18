# 01 — Metin2 Teardown: what it is, what it does well, what to keep

Metin2 (Ymir Entertainment, 2004; published in Europe by Gameforge from 2006) is a
free-to-play 3D MMORPG with an oriental-fantasy setting, three warring kingdoms,
click-to-move combat, and an extremely deep — and extremely monetised — item progression
system. It is still played on official and private servers two decades later, which tells
you the core loop is genuinely good.

Below: each system, how it actually works, **why it is designed that way**, and the verdict
for our project. The "why" column matters — a large fraction of Metin2's design exists to
serve retention and monetisation, not fun, and those parts must be redesigned rather than
copied.

---

## 1. The core loop

```
run to a spawn area → auto-attack trash mobs → find a Metin stone
   → hit it while it spawns waves of monsters → break it
      → loot yang, equipment, stones, upgrade materials
         → return to town → upgrade / socket / reroll gear
            → gear lets you survive the next zone → repeat one zone deeper
```

This loop is the reason the game survived. It is tight, legible, and has a clear
"one more stone" pull. **Keep it wholesale.** Everything else in this document is
either support for this loop or an MMO/monetisation layer around it.

---

## 2. System-by-system

### 2.1 Metin stones — the signature mechanic

- Stone monoliths spawn at fixed points in the world on a respawn timer.
- Attacking one spawns waves of monsters around it; the stone has a large HP pool.
- Breaking it grants XP, yang, a chance at gear, and "metin stone" items used for socketing.
- Stone tiers scale with zone level; higher tiers spawn nastier waves and drop better loot.

**Why it works:** it converts an open world into a set of self-assembling combat arenas
without needing scripted encounters. It also creates world-level competition on MMO servers
(who gets there first).

**Verdict: KEEP, redesign the waves.** In single-player there is no competition for spawns,
so the tension has to come from the encounter itself: telegraphed, escalating waves, a
break-timer, and a "shard pulse" AoE the player must walk out of. See
[02-singleplayer-redesign.md](02-singleplayer-redesign.md) §3.

### 2.2 Classes and skills

Four base classes, each with two specialisation trees:

| Class | Trees | Fantasy |
|---|---|---|
| Warrior | Body / Mental (two-hand vs sword+shield) | Tanky melee, big swings |
| Ninja | Dagger / Bow | Mobile assassin or ranged |
| Sura | Black Magic / Weaponry | Melee-caster hybrid, debuffs, dark magic |
| Shaman | Dragon / Healing | Nuker or support healer/buffer |

(Later expansions added Lycan/Wolfman.)

- Skills are unlocked with **skill books** (rare drops), levelled 1→20, then pushed through
  **Master (M) → Grand Master (G) → Perfect Master (P)** with progressively rarer books and
  random success chance.
- Passive skills, horse skills, and a separate "job change" at level 5.

**Why it works:** the two-tree split gives real build identity from four classes.
**Why it hurts:** book-gated skill levelling with random success is a grind/monetisation
gate, not a design.

**Verdict: KEEP the four classes and the two-tree split. CUT random skill-book gating** —
skills level from skill points + a deterministic mastery track. See §4 of the redesign doc.

### 2.3 Stats

Four attributes: **STR, DEX, INT, VIT** (called differently in various localisations).
Points are assigned per level, each class has a primary that scales its damage; VIT is
health, DEX is attack speed / dodge / crit, INT is mana and magic damage.

**Verdict: KEEP almost as-is.** It is clean, readable and works fine solo. We add a free
respec since there is no "reroll a new character on a server" escape hatch in single-player.

### 2.4 Item upgrading (+0 → +9)

- Any weapon/armour can be upgraded through nine levels at a blacksmith.
- Each level needs specific materials plus yang, and has a **declining success chance**
  (roughly 100% → ~20% at the top).
- On failure the item **loses upgrade levels or is destroyed outright**, unless protected by
  a **Blessing Scroll** (a cash-shop / rare-drop consumable).

**Why it exists:** this is the monetisation engine of the entire game. The destruction risk
exists to sell protection items.

**Verdict: KEEP the +0→+9 progression, CUT the gambling.** In an offline game with no shop,
item destruction is pure player punishment that produces save-scumming, not tension. Replaced
with a deterministic material-cost ladder plus a pity counter — redesign doc §4.

### 2.5 Sockets and stones

- Items roll with 0–3 sockets; sockets are opened with a Diamond and filled with attribute
  stones (attack, HP, crit, penetration, resistance…).
- Stones are removed/destroyed on swap.

**Verdict: KEEP.** This is the best part of the itemisation because it is player-directed,
not random. We make socket opening deterministic and stone removal non-destructive
(a small material cost instead).

### 2.6 Bonus lines

- Equipment rolls 1–5 random "bonus" lines from a per-slot pool: average damage %, skill
  damage %, crit chance, piercing, HP, mana, **damage vs monster type** (animal / undead /
  devil / orc / human / mystic), elemental resistances, and so on.
- Rerolled with consumables until you hit the lines you want.

**Why it works:** gives every drop a lottery-ticket feeling and creates a real theorycrafting
layer — "vs undead" gear for a specific dungeon is an actual strategy.
**Why it hurts:** unbounded reroll grind.

**Verdict: KEEP the concept and the monster-type damage axis (it is genuinely good build
texture). Bound the randomness:** limited reroll currency, plus the ability to **lock one
line** so rerolling converges instead of spiralling.

### 2.7 Combat feel

- Click-to-move, click-to-attack with auto-attack chains; skills on a hotbar.
- No dodging, no blocking beyond a stat, no i-frames. Survival is a function of gear and HP.
- Some positional elements (AoE cones/circles) but no real movement game.

**Verdict: KEEP the control scheme, REBUILD the depth underneath it.** (Decision D3, your
call.) Click-to-move stays, faithful to the original. But a pure gear check does not carry a
20-hour solo campaign, so depth is added in four places that do not touch the controls:
telegraphed ground AoEs you walk out of, a per-class defensive cooldown, a finite healing
flask replacing potion spam, and composed enemy waves with kill-priority targets.
Full design in [02-singleplayer-redesign.md](02-singleplayer-redesign.md) §2.

### 2.8 Progression curve and XP

- Level cap started at 99, later pushed to 105+ with expansions.
- The XP curve is deliberately brutal in the 90s; it is normal to spend weeks per level.
- XP rings, XP buffs and "exp events" are a core retention/monetisation lever.

**Why it exists:** an MMO must keep players subscribed/playing for years.

**Verdict: CUT ENTIRELY, rebuild.** A single-player game must reach its content, not gate it.
Level cap 60, sub-quadratic curve, ~18 hours to cap along the campaign. Numbers in
[06-data-model-and-formulas.md](06-data-model-and-formulas.md) §3.

### 2.9 Parties and group play

- 2–8 player parties, XP sharing with a group bonus, party roles (leader / attacker /
  buffer / berserker / defender) granting party-wide stat buffs.
- Most high-tier dungeon content assumes a party; Shaman healing is balanced around it.

**Verdict: REPLACE with a companion system.** One recruitable AI companion with three
archetypes (Guardian / Mender / Striker), levelling alongside you and carrying the party-role
buffs. This preserves the "my build plugs a hole in my kit" feel without multiplayer.

### 2.10 Guilds, kingdoms, PvP, sieges

- Three kingdoms permanently at war; open-world PvP in border maps; a PK/karma system;
  guilds with guild skills, guild land and buildings; castle sieges; duels.

**Verdict: CUT as multiplayer systems, REUSE as content.** The three-kingdom war becomes the
campaign's political backdrop and a **faction reputation system**; sieges become scripted
set-piece battles with allied AI squads; duels become an **arena challenge mode vs AI**.
This is a content win — the original's best worldbuilding is in systems no solo player can see.

### 2.11 Economy

- Yang (gold) with heavy sinks; NPC shops; player private shops; offline shop system;
  an item-shop currency for cosmetics, mounts, XP boosts and blessing scrolls.

**Verdict: SIMPLIFY.** No player economy exists offline. Yang becomes a well-tuned single
sink (upgrades, sockets, rerolls, repairs, fast travel). NPC merchants have a rotating,
seed-deterministic stock so shopping stays interesting. **No cash shop, no premium currency;
all cosmetics are unlockable in-game.**

### 2.12 Quests

- Lua-scripted. Mostly kill-X / collect-X / talk-to-NPC, plus level-up quests, the
  Biologist collection chain, and daily/repeatable quests.
- Very little narrative; the world story is in item descriptions and map design.

**Verdict: REPLACE.** Filler quests are an MMO time-sink. We need an actual campaign: three
acts, a named antagonist, per-region arcs, and side quests that unlock mechanics or
companions rather than counting corpses. Quest data stays data-driven (JSON, not Lua).

### 2.13 Dungeons

- Instanced multi-stage dungeons (spider dungeon, demon tower, catacombs, dragon lairs),
  usually: clear waves → kill mini-boss → solve a light gimmick → kill boss.
- Entry gated by level, keys, and party size.

**Verdict: KEEP the structure, retune for one player.** Dungeons are already the most
"designed" content in the game and translate directly.

### 2.14 Mounts, pets, costumes

- Horses with their own level and combat skills; later mounts purely cosmetic/utility;
  pets granting small buffs; costumes overlaying appearance (and later granting stats —
  a monetisation slide).

**Verdict: KEEP mounts (traversal + a small mounted-combat toy) and pets (buff + loot
magnet). Costumes are cosmetic only, earned, never stat-bearing.**

### 2.15 Alchemy / Dragon Soul (later expansions)

- A deep secondary gear system: dragon soul stones with grades, refinement, and an
  alchemy bench; widely considered over-complicated and opaque.

**Verdict: CUT.** It is a second itemisation system stacked on the first to extend the
grind. One deep system beats two shallow confusing ones.

### 2.16 UI/UX

- Grid inventory with multi-cell items (1×1, 1×2, 1×3), warehouse storage, hotbar,
  skill window, character sheet with dense stat list.

**Verdict: KEEP grid inventory (it is tactile and creates real decisions), MODERNISE
everything else:** search/filter, auto-sort, item comparison tooltips, a proper map, quest
journal with markers, and a codex/bestiary.

---

## 3. Summary verdict table

| System | Verdict | Where it is redesigned |
|---|---|---|
| Metin stone hunting | **Keep**, new wave design | redesign §3 |
| Core gameplay loop | **Keep** wholesale | — |
| 4 classes / 2 trees | **Keep** | redesign §5 |
| Skill books, random mastery | **Cut**, deterministic mastery | redesign §5 |
| STR/DEX/INT/VIT | **Keep** + free respec | data model §2 |
| +0→+9 upgrading | **Keep** ladder, **cut** destruction gamble | redesign §4 |
| Sockets and stones | **Keep**, make deterministic | redesign §4 |
| Bonus lines / vs-type damage | **Keep**, bound the rerolls | redesign §4 |
| Click-to-move combat | **Keep** controls, rebuild depth underneath | redesign §2 |
| MMO XP curve | **Cut**, rebuild for 15–25 h | data model §3 |
| Parties | **Replace** with AI companion | redesign §6 |
| Guilds / kingdoms / PvP / sieges | **Cut** as systems, **reuse** as content | redesign §7 |
| Player economy, cash shop | **Cut**, single-sink yang economy | redesign §8 |
| Lua kill-X quests | **Replace** with an authored campaign | redesign §9 |
| Dungeons | **Keep**, retune solo | redesign §10 |
| Mounts / pets / costumes | **Keep**, cosmetics never stat-bearing | redesign §11 |
| Alchemy / Dragon Soul | **Cut** | — |
| Grid inventory | **Keep** + modern QoL | requirements §UI |
