# 09 — The Archer, the Asura and the Dragonseer: analysis (CLS-04, CLS-05, CLS-06)

Written 2026-10-02, at your call: "create all three classes; take fitting models; change their
look after how the original describes its characters; copy the skills, and the animations as
close as we can; make their weapons and armour in the style of theirs; a good analysis first,
after the original, of look, skills, way of playing and animations."

This is that analysis: what the original does for each of the three, and what ours is. The
standing rules hold — **mechanics may be the original's, every name, model and picture is
ours** — and so does the rule of 2026-10-02: **one branch of skills a class**, the defence on
Shift and six skills.

Where I describe the original I am working from what I know of it, not from its files: nothing
of it was downloaded, opened or copied.

---

## 1. What the original does

### 1.1 The bow fighter (the original's assassin, archery branch)

| | |
|---|---|
| **Look** | Slight and quick. Light cloth and leather close to the body, little metal, a hood or a band in the hair, a quiver on the back. Greens, browns, dark reds. |
| **Weapon and armour** | A bow, and arrows as a separate piece of kit. Armour is light suits — cloth and leather, later scaled or lacquered — never plate. |
| **Way of playing** | The only class whose ordinary blow is at range: it stands off, picks one target and shoots; it is frail, so it lives by distance and by speed. Dexterity is its attribute. Alone, it kites: shoot, walk, shoot. |
| **Skills** | *Repeated shot* — several arrows one after another at one target. *Arrow shower* — arrows on the target and on everything round it. *Fire arrow* — one burning arrow that bursts where it lands. *Feather walk* — a buff: much faster on foot. *Poison arrow* — one arrow through whatever is in its line, poisoning. *Spark* (later) — a heavy single shot that bursts. |
| **Animations** | Stands with the bow low in the left hand; every shot is nock–draw–loose; the shower is loosed upward; the buff is a quick lift of the arm; it runs with the bow held out to the side. |

### 1.2 The cursed sword fighter (the original's sura, weaponry branch)

| | |
|---|---|
| **Look** | A swordsman who took a demon's seed into one arm for power: dark armour, reds, blacks and violets, pale or white hair, the marked arm. Grim, upright. |
| **Weapon and armour** | One-handed swords — the same family the warrior wears, which is why they share them — and dark plate of its own: horned or flared shoulders, a skirt of plates. |
| **Way of playing** | A melee fighter whose skills are magic: it stands in the pack as the warrior does, but its skills hit through armour, weaken what stands near, and buff the sword. Intelligence feeds the skills, strength the blows. Tough rather than fast. |
| **Skills** | *Finger strike* — a claw of dark force from the ground at a target a little way off, ignoring much of its defence. *Dragon swirl* — the body turns and dark flame hits everything round it, several times. *Enchanted blade* — a buff on the sword: more attack, and a share of the damage dealt comes back as health. *Fear* — an aura: what stands near strikes weaker. *Enchanted armour* — a buff: more defence, and a share of blows taken is thrown back. *Dispel* — a bolt that strips the target's buffs. |
| **Animations** | The claw is a hand thrust forward, palm out; the swirl a spin with the sword out; the buffs a blade raised before the face and a hand drawn down it; the bolt a throw from the off hand. |

### 1.3 The dragon shaman (the original's shaman, dragon-force branch, with the heal of its other branch)

| | |
|---|---|
| **Look** | Robes and long sleeves, talismans and tassels, a headdress or ornaments in the hair; whites, blues, jade and gold. Calm, a priest rather than a fighter. |
| **Weapon and armour** | A fan or a bell, never a blade. Armour is robes, each richer than the last — never metal. |
| **Way of playing** | The caster: it fights with fire at range and keeps itself (and, online, its party) standing with blessings. Intelligence is its attribute. Frail when caught; strong when its blessings are up. |
| **Skills** | *Flying talisman* — paper talismans thrown at a target, several hits. *Shooting dragon* — a dragon of fire down a long line through everything. *Dragon's roar* — fire round the caster, and what it hits burns. *Blessing* — a long buff: less damage taken. *Reflect* — a buff: blows thrown back. *Dragon's aid* — a buff: more critical hits. From the other branch: *Cure* — heals. |
| **Animations** | The fan is swept forward for the talismans; both hands pushed out for the dragon; arms thrown wide for the roar; hands raised over the head for a blessing. |

---

## 2. Ours

### 2.1 Shared decisions

| Decision | What | Why |
|---|---|---|
| Names | **Arciere** (Archer), **Asura**, **Dragoveggente** (Dragonseer) | Yours of 2026-09-30. In the code: `Archer` (new), `Sura`, `Shaman` — never shown. |
| One branch | Shift and six skills each | Your rule of 2026-10-02. |
| Skill names | All ours; none is the original's | Standing rule. |
| Models | **KayKit Adventurers 2.0** again (CC0, already on disk in your `Downloads` — nothing downloaded): the **Ranger** for the Archer, the **Mage** for the Dragonseer, the Knight's head on the Ranger's body for the Asura; the women the Rogue, as every woman so far. Each recoloured, and changed, by a Blender script of ours. | Same rig as everything else: armour, helmets and animations fit unchanged. |
| Animations | **KayKit Character Animations 1.1** (CC0, already on disk): `CombatRanged` (bow: idle, draw, release, upward release; magic: shoot, spellcasting, raise, summon) and `MovementAdvanced` (running with a bow, the dodges) — the two files not yet in the game. | The original's motions, as close as a free set gets. |
| Weapons and armour | Made in Blender by our kits: twelve bows, twelve cursed swords, twelve fans; seven suits and four headpieces a class. Each is the **twin** of a Warrior piece — same level, rarity and worth — so every drop, shop and reward already hands the right one over. | Yours of 2026-09-30: no stand-ins. |
| Range | The Archer's ordinary blow is **a shot at one target**, not an arc, from 13 m; the Dragonseer strikes hand to hand. It needs line of sight. No sweep on the fourth blow. | The original's ranged classes; an arc at 13 m would be a different game. |

### 2.2 The Archer

**Look.** Dressed as the Ninja but for a detail (at your call, 2026-10-02, as the Mental Warrior is
the Body Warrior but for one — in the original the two are one class): the same dark clothes,
hood and mask, tail and bare legs, with the hood, hems and bands **ochre** where the Ninja's are
green, the leather and her hair chestnut, and a **quiver** on the back. Under armour the quiver stays.

**Attribute.** Dexterity: full attack from DEX, a third from STR — the Ninja's rule.

**Shift — Balzo Indietro (Backstep).** A leap away from where it faces, 4.5 m, a moment
untouched: distance is its defence. Free, 6 s.

| Skill | After | What it does | Motion |
|---|---|---|---|
| **Raffica** (Volley) | repeated shot | 4 arrows at the chosen target, 60% each, 0.16 s apart. Needs a target, 14 m. | draw, then a release per arrow |
| **Pioggia di Dardi** (Rain of Shafts) | arrow shower | Arrows fall on a 4 m circle on the target (or ahead): 3 falls of 85%. | released upward |
| **Freccia Ardente** (Blazing Arrow) | fire arrow | One arrow, 230% fire damage, bursting 3 m round where it lands; what it hits burns. | long draw, release |
| **Passo Leggero** (Light Step) | feather walk | 15 s: 35% faster on foot, 10% more evasion. | arm lifted |
| **Freccia Venefica** (Venom Shaft) | poison arrow | One arrow down a 14 m line through everything: 140%, and poison worth 18% of the hit each second for 6 s. | draw, release |
| **Colpo Folgore** (Thunder Shot) | spark | One heavy arrow at the target: 320%, a 40% chance to stun for 1.2 s. A wait of 0.6 s while it is drawn. | long draw, release |

### 2.3 The Asura

**Look.** Dark and arcane (at your call, 2026-10-02): **ashen skin, white hair, violet eyes**; black
cloth and black leather with deep violet at the neck and hems; the **left arm violet and lit from
within**, the mark. The man the Knight's face on the Ranger's body, the woman the Rogue. Its suits
are black plate with flared shoulders, violet-black cloth, and violet stones lit on every one.

**Attribute.** Strength for the sword; **every skill scales with intelligence** (at your call): 1.2
attack power a point on what deals damage, 0.3 a point on the Hexed Blade, and every share — the
mail, its thorns, the dread, the ward, the spellbreak — a little larger a point, up to a cap.

**Weapon.** One-handed cursed swords: each is the Warrior's sword of that tier re-forged — a
dark blade with a violet edge that glows. Twelve.

**Shift — Contraccolpo (Backlash).** For 1 s the next blow that would land does nothing, and dark
force bursts round the Asura: 180% to everything within 4 m. Free to move meanwhile. 10 mana, 10 s.
A shell that took blows at first; changed at your call (2026-10-02).

| Skill | After | What it does | Motion |
|---|---|---|---|
| **Artiglio d'Ombra** (Shadow Claw) | finger strike | A claw from the ground under a target up to 10 m off and everything within 1.8 m of it: 370%, ignoring defence (doubled at your call, 2026-10-05). Needs a target. Lands 0.45 s in. | a blow of the marked hand, about a second |
| **Vortice Oscuro** (Dark Vortex) | dragon swirl | One turn, dark flame on everything within 3.2 m: one hit of 200%, half a second in. | one turn of a second |
| **Lama Stregata** (Hexed Blade) | enchanted blade | 25 s: +25 attack power (+33 at Master, +40 Perfect) and 0.3 more a point of INT, and average damage on blows, +30% rising to +55% at Master, +75% Grand Master, +100% Perfect (2026-10-06, at your call: the attack halved from 50/65/80, the average damage added, then raised from 10–40% so a build on blows stands beside one on skills) — and 6% of the damage of every blow comes back as health. | a spell worked over the blade with both hands |
| **Terrore** (Dread) | fear | Everything within 6 m strikes 20% weaker for 10 s, and the Asura gains 10 defence and 0.8 more a point of INT for as long (defence, not a share less damage, at your call). | a long summoning |
| **Corazza Arcana** (Arcane Mail) | enchanted armour | 20 s: 20% less damage taken, and 25% of every blow taken from close by thrown back. | power drawn up with both arms |
| **Spezzaincanti** (Spellbreak) | dispel | A bolt at a target up to 12 m off: 50% — it is cast for what it leaves, not for the blow — strips its rage and its shield, and leaves it taking 18% more for 8 s. Needs a target. | a throw |

### 2.4 The Dragonseer

**Look.** The Mage's robe in white and jade with gold bands, without the pointed hat (ours are
headpieces of their own); the man the Mage's face, the woman the Rogue's on the same robe.

**Attribute.** Intelligence: full attack from INT. Its ordinary blow is the fan, hand to hand, as
the original's (at your call, 2026-10-02): the magic is in its skills.

**Weapon.** Fans — a spread of ribs and leaf on a handle — twelve, from paper and bamboo to
jade and gold.

**Shift — Sigillo Puro (Pure Seal).** 3 s: a shell that takes blows up to 25% of maximum
health, and on raising it every harmful effect falls away. 12 mana, 12 s.

| Skill | After | What it does | Motion |
|---|---|---|---|
| **Sigilli Volanti** (Flying Seals) | flying talisman | 3 paper seals at the chosen target, 88% each. Needs a target, 13 m. | the fan swept forward |
| **Soffio del Drago** (Dragon's Breath) | shooting dragon | Fire down a 13 m line through everything: 240%. | both hands pushed out, after a breath |
| **Ruggito di Fiamma** (Flame Roar) | dragon's roar | Fire on everything within 5 m: 220%, and it burns — 18% of the hit each second for 5 s. | arms thrown wide |
| **Benedizione del Drago** (Dragon's Blessing) | blessing | 40 s: 18% less damage taken. | hands raised; an aura of points round the body while it lasts |
| **Globo di Folgore** (Storm Orb) | lightning throw | A ball of lightning at a target up to 13 m off, 180%, leaping from it to the nearest four within 6 m of each other. In place of the heal, at your call (2026-10-02). | a throw from the hand |
| **Occhio del Drago** (Dragon's Eye) | dragon's aid | 30 s: +15% critical chance. | hands raised, a ball of light between the hand and the fan |

*Reflect* is left with the Asura's mail: one class with it is enough for a game played alone.

The numbers of the three are those the balance tool left them with (CLS-07, 2026-10-02; `kiln balance`):
the Asura's blows and what it leaves on a creature brought down, the Dragonseer's fire brought up.

### 2.5 What the game has to learn

| New | For | How |
|---|---|---|
| A shot as the ordinary blow | Archer, Dragonseer | `PlayerCombat`: range from the class, one target, an arrow or a bolt drawn to it |
| A weapon in the left hand | Archer | `GearPartsDef.weapon_hand` |
| Stance by class | all three | `ModelAnimator.Style`: bow, caster — idle, run and blows of their own |
| Burning | Archer, Dragonseer | a `Burn` status: ticks like poison, in the fire's colour |
| Health back from blows | Asura | a `Leech` status read where damage lands |
| Blows thrown back | Asura | a `Thorns` status on the player, the creatures' thorns turned round |
| A shell that takes blows | Asura, Dragonseer | `Combatant.Barrier`: points absorbed before health |
| A heal, a critical buff, a faster step | Dragonseer, Archer | statuses `Haste`, `Focus`; a heal applied by a self skill |
| Skills from a second attribute | Asura | `SkillAttackBonus`, as the Mental Warrior |

### 2.6 What is not in this pass

- **Balance.** Every number above is a first one. CLS-07 (each class finishes act 1 and the
  tower alone) is still to run for all of them.
- **Shields, boots and jewellery** stay shared by every class.
- **The creator** (faces, hair colour, skin) stays as it is: class, man or woman, name.

## Armour, redrawn (2026-10-02)

"Review the armours of the three classes: more their own, and better made", then "the Asura's are
all one colour — other dark colours too" and "the women's can be a little more uncovered" (your
calls). Every suit is its own design now (`art/blender/classkit.py`):

| | Archer | Asura | Dragonseer |
|---|---|---|---|
| 1 | Trail Jerkin: laced linen, green scarf, pouch | Branded Wraps: black bindings, paper wards, wine sash, rings of light on the marked arm | Pilgrim's Mantle: linen, rope belt, wooden beads, a gourd |
| 6 | Hunter's Hide: fur collar, bone toggles, a tail at the belt | Bloodied Leathers: crossed belts on an iron ring, studs, a torn blood-red cape | Reed Mantle: a rain cape of reeds in tiers, a reed skirt |
| 10 | Fletcher's Scales: green and bronze leaves on chest, bow arm and hips | Hexscale Hauberk: petrol-green scales throat to thigh, a blade on each shoulder, a green stone | Jadescale Vestment: a cape of jade scales, a jade ring at the breast, bell sleeves |
| 14 | Ridge Ranger: cowl, harness of pouches, iron on the bow arm, a cape from the belt | Ridge Dreadmail: blue-black plate with a keel and lames, midnight cape, eyes of ice on the breast | Ridge Oracle: fur, a bronze mirror, streamers of three colours, bells |
| 18 | Warden's Mantle: a pointed mantle over the bow shoulder, silver breast guard, swallow-tailed cape | Wardenbane Plate: black and old gold, a standing collar, gold chevrons, crimson cape | Warden's Benediction: two-tiered mantle scalloped in gold, the sun on a blue panel, a wheel of rays on the back |
| 22 | Ashen Stalker: torn black, a mask of cloth, antler on the shoulder, embers in the seams | Ashen Tyrant: a cage of ashen ribs over black, spines down the back, horned shoulders, an ember heart | Ashen Vigil: charred robes torn at every hem, a burning censer, ember beads, burnt prayers |
| 24 | Kiln Plume: a collar of gold feathers, feathered arms, a tail of red plumes tipped with fire | Kiln Fiend: violet-black and gold, a lit sigil, horned shoulders, a wing of three blades behind each | Kiln Dragon: gold dragon scales, gold antlers, the pearl in a claw, a red sash of heaven behind |

**The women's cut.** The same suits, with a band round the chest in place of the vest: shoulders and
belly bare. The Asura woman's own bodice is cut short as the Ninja's; the Dragonseer woman no longer
wears the Mage's robe — an ivory bodice, bare shoulders and arms, a jade wrap round the hips over bare
legs. The men are as they were.

## Weapons, redrawn (2026-10-02)

"The same for the weapons": "big, heavy bows with many branches for the Archer, showy fans for the
Dragonseer, dark magic swords for the Asura" (your call). Twelve each, each its own design
(`bowkit.py`, `fankit.py`, `hexkit.py`):

| Tier | Bow | Fan | Cursed sword |
|---|---|---|---|
| Worn | a heavy stave with the stubs of its boughs | mended paper, a frayed edge | a chipped black blade, a lit crack |
| Hunter's | crowned with antlers | seven pheasant feathers | a curved fang, bone guard, venom |
| Bronze | bronze laurel leaves | a round fan, the sun in a bronze rim | a leaf of dark bronze, four runes |
| Iron | hooks and thorns of iron | a war fan of iron slats, the red sun | a black chopper, red edge |
| Ridge | ram's horns curling back | seven broad green leaves | a midnight blade in waves, ice in the groove |
| Warden | a pair of silver wings | blue silk, the moon and the star | a blade split round a bar of violet light |
| Steel | all blades | nine steel blades, no leaf | a saw-toothed executioner's blade, an eye in the guard |
| Grave | a spine and its ribs | a bat's wing on fingers of bone | vertebrae down the spine, a skull pommel |
| Ember | a burnt bough still alight | a leaf of flame | black glass with fire in the cracks |
| Hollow | black briar with two red hooks | torn black, an eye of red light | half sword, half sickle |
| Shard | silver grown through with crystal | a spray of crystal shards | a spire of crystal, splinters adrift |
| Kiln | the firebird, every feather a flame | the firebird's tail, jade and gold | open on a core of light, winged guard, the tip come apart |

A bow is held below its middle, the lower limb about half the upper, so it clears the ground.
F8 (debug) empties the bag and gives the class's armours and headpieces; pressed again, its weapons.
