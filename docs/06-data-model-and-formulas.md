# 06 — Data Model and Formulas

Every number here is a **starting point to be tuned by simulation** (roadmap PRG-09, ITM-12,
BAL-01), not a final value. What matters is that the *shape* of each curve is right and that
all of it lives in `Kiln.Core` as pure, deterministic, unit-tested C# (NFR-R.2).

---

## 1. Entity model

```
Entity
├─ Stats          (attributes + derived, recomputed on any modifier change)
├─ Health/Mana    (current, max, regen)
├─ StatusEffects  (list of timed modifiers, stacking rules)
├─ Threat         (per-entity aggro table, for enemies)
├─ Equipment      (player/companion only)
└─ Visual         (VisualRoot → registry id; the art-swap boundary, ENG-07)
```

---

## 2. Stats

### Attributes
`STR`, `DEX`, `INT`, `VIT`. Start at 6 each; +4 points per level, plus class bonuses.

### Class primaries

| Class | Primary | Secondary |
|---|---|---|
| Warrior | STR | VIT |
| Blade | DEX | STR |
| Sura | STR / INT (per tree) | DEX |
| Shaman | INT | VIT |

### Derived

```
MaxHP        = 120 + VIT * 18 + Level * 14
MaxMana      = 60  + INT * 12 + Level * 6
HPRegen/s    = 0.4 + VIT * 0.05            (× 0.25 while in combat)
ManaRegen/s  = 1.2 + INT * 0.10 + MaxMana * 0.012   (× 0.30 while in combat)
ManaPerKill  = MaxMana * 0.10              (nothing from trivial enemies)

AttackPower  = WeaponDamage + Primary * 1.8 + Level * 1.2
MagicPower   = WeaponDamage + INT     * 2.1 + Level * 1.2
Defense      = ArmorValue + VIT * 0.8 + Level * 0.6

CritChance   = 0.03 + DEX * 0.0012         (cap 0.50)
CritDamage   = 1.50 + bonuses              (no cap)
PierceChance = DEX * 0.0008                (cap 0.35)  — ignores mitigation
Evasion      = DEX * 0.0006                (cap 0.30)
AttackSpeed  = 100 + DEX * 0.5             (cap 180; 100 = 1.0 attacks/s)
MoveSpeed    = 5.2 m/s * (1 + bonuses)
```

**Rule:** every stat above must have a plain-language tooltip (FR-12.4). If a stat cannot be
explained in one sentence to a new player, it should not exist.

---

## 3. XP and levelling

Level cap **60**.

```
XPToNextLevel(n) = round(55 * n^1.85)
```

| Level | XP to next | Cumulative |
|---|---|---|
| 1 | 55 | 55 |
| 10 | ~3,900 | ~21,000 |
| 20 | ~14,000 | ~112,000 |
| 30 | ~30,700 | ~330,000 |
| 45 | ~64,000 | ~1,010,000 |
| 59 | ~108,000 | **~2,140,000** |

### XP budget — the anti-grind constraint

The campaign must reach level 60 **without any farming**. Budget:

| Source | Share of total XP |
|---|---|
| Quests (story + side) | **45%** |
| Shard breaks | **35%** |
| Trash kills along the way | **20%** |

`Kiln.Tools.BalanceSim` walks the quest list, zone shard counts and expected kill counts and
asserts the player enters every zone within its level band. **A failing balance sim fails
CI** — this is how "no grinding required" stops being a wish.

```
TrashMobXP(n)   = round(1.1 * n^1.85)
ShardXP(tier,n) = round(11  * n^1.85 * tierMult)   // tierMult = 1 + 0.25*(tier-1)
QuestXP(n)      = round(41  * n^1.85)             // side quests are worth half
```

**All three share the same exponent on purpose.** With different exponents the mix between
questing, shard-breaking and killing drifts as the player levels, so a game balanced at
level 10 quietly becomes a different game at level 40. These coefficients were derived by
`kiln simulate`, not chosen by eye — the first draft gave quests 88% of all experience.

### Catch-up and floor (FR-2.7)

```
if playerLevel <= zoneBand - 3:  XP × 1.35
if playerLevel >= zoneBand + 5:  XP × 0.25
if enemyLevel  <= playerLevel - 8:  dies in one hit, does not aggro
```

---

## 4. Damage pipeline

Order matters; this is the exact evaluation sequence.

```
 0. evade     = roll(Evasion) ? MISS : continue        // skipped when Unavoidable
 1. base      = AttackPower × weaponCoef × skillCoef
 2. flat      = base + flatBonuses
 3. pct       = flat × (1 + Σ percentBonuses)          // avg damage %, skill damage %
 4. vsType    = pct  × (1 + vsFamilyBonus)             // animal/undead/devil/orc/human/mystic
 5. crit      = roll(CritChance) ? vsType × CritDamage : vsType
 6. pierce    = roll(PierceChance)                     // if true, skip step 7
 7. mitigated = crit × (1 - DamageReduction)
 8. resisted  = mitigated × (1 - elementalResist)      // resist capped at 0.70
 9. difficulty= resisted × difficultyDamageMult        // table in redesign §1
10. variance  = difficulty × rand(0.95, 1.05)
11. final     = max(1, round(variance))
```

```
DamageReduction = Defense / (Defense + 60 + 14 × AttackerLevel)     // soft cap 0.75
```

This curve is deliberately **soft-capped and asymptotic**: stacking defence always helps a
little and never makes you invulnerable, which is what prevents the gear-check spiral that
would otherwise dominate a click-to-move game.

**Step 0 (evade) was added during implementation.** §2 defines an Evasion stat, and the
original pipeline draft never used it — a stat that exists but does nothing is a bug waiting
to be found. Unavoidable effects (shard pulses, scripted boss mechanics) set
`Unavoidable = true` and skip it. If random misses turn out to feel bad in a game where the
player cannot dodge manually, the cleanest change is to repurpose Evasion as flat damage
reduction rather than to delete the stat.

### Caps, and why they exist

| Stat | Cap | Reason |
|---|---|---|
| Crit chance | 50% | Past this, crit stops being a gamble and becomes baseline damage |
| Crit damage | none | The intended investment sink for a crit build |
| Pierce chance | 35% | It ignores mitigation entirely, so it must stay a spike, not a norm |
| Evasion | 30% | Higher, and fights become unreadable coin-flips |
| Elemental resist | 70% | Leaves every element meaningfully dangerous |
| Mitigation | 75% | The single most important cap: it is what stops gear trivialising the game |

Every cap is enforced in `StatBlock` and covered by tests, so a bonus line with an absurd
roll cannot quietly break the curve.

### Stagger

Enemies have `StaggerPool` (regenerates 8%/s out of combat). Designated skills apply stagger
damage; filling the pool interrupts the current action and opens a **1.5 s vulnerable
window** at ×1.35 damage. This is the melee goal that replaces dodge-punish timing.

---

## 5. Item schema

```jsonc
// game/data/items/weapons.json
{
  "id": "wpn_iron_sword",
  "name": "$item.wpn_iron_sword.name",      // localisation key, never literal text
  "slot": "weapon",
  "class_restriction": ["warrior"],
  "level_req": 8,
  "rarity": "fine",                          // common|fine|rare|epic|relic
  "grid_size": [1, 3],
  "base_stats": { "weapon_damage_min": 24, "weapon_damage_max": 38, "attack_speed": 0 },
  "sockets": 1,                              // from rarity, not random (FR-5.6)
  "bonus_line_count": [2, 3],                // rolled at drop within this range
  "bonus_pool": "weapon_melee",
  "upgrade_path": "standard_weapon",
  "sell_value": 1200,
  "visual": "mesh_sword_a"                   // registry id → placeholder now, model later
}
```

### Rarity → sockets and bonus lines

| Rarity | Sockets | Bonus lines | Drop weight |
|---|---|---|---|
| Common | 0 | 0–1 | 60 |
| Fine | 1 | 2 | 25 |
| Rare | 2 | 3 | 10 |
| Epic | 2 | 4 | 4 |
| Relic | 3 | 5 | 1 |

### Bonus lines (FR-5.4)

Pools are per slot. Representative entries:

```jsonc
{ "id": "bon_avg_dmg",     "stat": "damage_pct",        "range": [3, 12],  "weight": 100 },
{ "id": "bon_vs_undead",   "stat": "vs_family.undead",  "range": [5, 25],  "weight": 40  },
{ "id": "bon_crit",        "stat": "crit_chance",       "range": [1, 6],   "weight": 45  },
{ "id": "bon_hp",          "stat": "max_hp_flat",       "range": [40, 260],"weight": 90  },
{ "id": "bon_pierce",      "stat": "pierce_chance",     "range": [1, 5],   "weight": 30  }
```

### Upgrade ladder (FR-5.5 — the no-gambling system)

```jsonc
{
  "path": "standard_weapon",
  "steps": [
    { "to": 1, "yang":   5000, "mats": { "iron_scrap": 2 },                      "chance": 1.00, "pity": 0 },
    { "to": 4, "yang":  19000, "mats": { "iron_scrap": 8, "tempering_oil": 1 },  "chance": 0.75, "pity": 2 },
    { "to": 7, "yang":  60000, "mats": { "steel_core": 4, "shard_essence": 1 },  "chance": 0.45, "pity": 3 },
    { "to": 9, "yang": 120000, "mats": { "steel_core": 12, "radiant_core": 1 },  "chance": 0.25, "pity": 4 }
  ],
  "on_failure": "consume_materials_only"    // never destroy, never downgrade
}
```

`pity` = guaranteed success after that many consecutive failures at this step. **The counter
is shown in the UI.** Failure costs resources; it can never cost progress — which is what
removes the incentive to save-scum.

Pity also makes the ladder *costable*. Without it the expected attempts on a rung are `1/p`
with an unbounded tail, so no budget can be planned around it; with it the worst case is
`pity + 1` and the economy simulator can price the whole chase.

### Upgrade level → stats

```
multiplier(n) = 1 + 0.07n + 0.005n²          // +9 ≈ 2.03x
```

Applied to the item's **base** numbers only — weapon damage, armour value, a ring's flat
stats. Rolled bonus lines are never scaled. The player upgrades the frame and rerolls the
lines, and the two systems stay legible because they never touch each other.

Yang costs are set against the campaign's income per band (see `kiln economy`), not picked
by feel: the first half is meant to spend ~60% of income on gear, and the +9 capstone is
meant to still be out of reach when the credits roll.

---

## 6. Enemy schema

```jsonc
{
  "id": "mob_corrupted_wolf",
  "name": "$mob.corrupted_wolf.name",
  "family": "animal",                  // animal|undead|devil|orc|human|mystic
  "role": "bruiser",                   // bruiser|archer|shielder|mender|bomber
  "level": 12,
  "stats": { "hp": 640, "attack_power": 58, "defense": 40, "move_speed": 6.1 },
  "stagger_pool": 100,
  "abilities": [
    { "id": "abl_bite",  "cooldown": 2.4, "windup": 0.55, "damage_coef": 1.0 },
    { "id": "abl_lunge", "cooldown": 7.0, "windup": 1.50, "damage_coef": 1.6,
      "telegraph": { "shape": "cone", "radius": 2.5, "angle": 60 } }
  ],
  "ai": "bt_melee_chaser",
  "aggro_radius": 12,
  "leash_radius": 35,
  "xp": 310,
  "drop_table": "dt_valley_animal_t2",
  "visual": "mesh_placeholder_quadruped", "visual_scale": 1.1, "visual_tint": "#8a6b4f"
}
```

`telegraph` is structured (shape + radius + angle) rather than a shorthand string, so BAL-03
can validate every one automatically against move speed and radius. Omit it entirely for
fast untelegraphed melee swings — the rule correctly does not apply to those.

**Consequence worth knowing up front:** the escape rule forces big AoEs to have long
wind-ups. A 5 m circle needs a **2.31 s** authored wind-up to stay escapable on Shardbound;
a 2.5 m cone needs 1.46 s. This is correct for click-to-move — the player must aim, click
and walk — and it pushes design toward *few, large, well-telegraphed* attacks rather than
many small ones. Shrink the radius if you want a faster attack.

---

## 7. Skill schema

```jsonc
{
  "id": "skl_whirlwind",
  "class": "warrior", "tree": "body", "unlock_level": 14,
  "mana_cost": 32, "cooldown": 9.0, "cast_type": "instant",
  "targeting": "self_aoe", "radius": 4.5,
  "damage_coef": 1.35, "hits": 3, "stagger": 25,
  "mastery": {
    "master":       { "hits": 4, "note": "$skill.whirlwind.m" },
    "grand_master": { "pull_enemies": true, "note": "$skill.whirlwind.g" },
    "perfect":      { "radius": 6.0, "cooldown": 7.0, "note": "$skill.whirlwind.p" }
  }
}
```

Mastery ranks change **behaviour**, not only numbers (redesign §5).

---

## 8. Quest schema

```jsonc
{
  "id": "qst_a1_03_the_first_shard",
  "act": 1, "type": "story", "level_req": 6,
  "prerequisites": ["qst_a1_02_the_broken_gate"],
  "objectives": [
    { "type": "reach",  "target": "loc_valley_ridge" },
    { "type": "shard",  "target": "shard_node_valley_01", "count": 1 },
    { "type": "talk",   "target": "npc_elder_hwan" }
  ],
  "rewards": { "xp": 2400, "yang": 900, "items": ["wpn_iron_sword"], "unlock": "feature_upgrading" },
  "dialogue": "dlg_a1_03"
}
```

There are no side quests (FR-8.4, reshaped 2026-09-21): every quest is on the main progress chain. Most are kill quests — N of one kind of creature — and one is clearing the catacombs. The `side` type and the side-quest reward rule in the validator go when the chain is built (QST-05).

---

## 9. Save format

```jsonc
{
  "save_version": 3,                 // migrations required on load (FR-11.2)
  "game_version": "0.4.1",
  "checksum": "…",                   // integrity check, clear error on mismatch (FR-11.3)
  "created_utc": "…", "playtime_s": 40213,
  "difficulty": "disciple",
  "player": { "level": 28, "xp": 14200, "attributes": {…}, "skills": {…}, "equipment": {…}, "inventory": […] },
  "world":   { "zone": "zone_highlands", "position": [x,y,z], "discovered_shrines": […], "fog": "…" },
  "quests":  { "active": […], "completed": […], "flags": {…} },
  "shards":  { "shard_node_valley_01": { "respawn_at": 1223.4, "modifier": "frenzied" } },
  "economy": { "yang": 418000, "shard_essence": 62 },
  "codex":   { "mob_corrupted_wolf": { "kills": 214, "revealed": ["weakness","drops"] } },
  "stats":   { "deaths": 11, "shards_broken": 96, "upgrades_attempted": 143 }
}
```

Written atomically (temp file → fsync → rename) so a crash mid-save can never corrupt an
existing save.

---

## 10. Data validation (enforced in CI, ENG-08)

The build fails if any of these is false:

- Every id is unique within its type and matches `^[a-z]+_[a-z0-9_]+$`.
- Every cross-reference resolves: drop tables, bonus pools, upgrade paths, dialogue ids,
  quest prerequisites, visual registry ids, telegraph shapes.
- Every player-facing string is a `$localisation.key`, never a literal (NFR-L.1).
- Every quest is reachable from the prologue via its prerequisite graph, and the graph is acyclic.
- Every enemy's telegraphs pass the BAL-03 time-to-safety check at every difficulty.
- Every item's `level_req` falls inside a zone band where it can actually drop.
- Every stat a bonus line or an item's `base_stats` names resolves to a real modifier. A
  mistyped stat key does not crash and does not look wrong — the line appears in the tooltip
  and silently does nothing — so it has to be caught here or it ships.
- Every equippable item's socket count and bonus-line range match its rarity (§5). Rarity is
  a one-glance promise about an item's worth, and it only works if nothing contradicts it.
- Every standard upgrade path is `consume_materials_only`, ascends, stays at or below +9, and
  gives every fallible rung a pity counter.
