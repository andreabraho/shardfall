# 08 — Classes and the character creator: analysis (P2)

Written 2026-09-30, after your answers: the classes come **one at a time**, starting from the
**dagger fighter**; the four you want are the dagger fighter, the archer, the shaman and the
magic-weapons fighter; the creator should have **at least a female variant from the start**;
and you asked how hard the creator is, how looks are made, and how hair gets on with our
armour and helmets. This is that analysis, with what I measured in the files.

---

## 1. The classes

### What you asked for, and one reading to confirm

| You said | What it is | Reading |
|---|---|---|
| "ninja corpo" (daggers) | A fast melee fighter with two daggers | **Class 2, first branch** — the first one we build |
| "ninja arco" | The same fighter with a bow | **Class 2, second branch** |
| "sciamano" | Magic at range, heals and blessings | **Class 4** |
| "sura armi magiche" | A sword fighter whose weapon carries dark magic | **Class 3**, its "magic weapons" branch |

The Warrior already has **two branches** (body and mental, six skills each, `game/data/skills/warrior.json`).
So the dagger fighter and the archer read as **one class with two branches** — daggers and bow
— exactly as the Warrior has two. The other reading is two separate classes. **To confirm with
you**; the plan below assumes one class, two branches.

### Names

What the player reads must be our own (standing rule: nothing from the original). "Sura" and
"Shaman" are the original's class names, and the design doc already replaced them; in the code
the classes are `Warrior`, `Blade`, `Sura`, `Shaman` (the enum exists, `Kiln.Core.Foundation.CharacterClass`),
which the player never sees. Names for the screen, **to choose**:

| Class | Some ideas |
|---|---|
| Dagger / bow fighter | Lama, Ombra, Cacciatrice/Cacciatore, Lesto |
| Magic-weapons fighter | Rinnegato, Lama Maledetta, Stregaspada |
| Magic at range, heals | Veggente, Oracolo, Mistico |

### What each class needs, and what already exists

| Needs | Where it comes from | State |
|---|---|---|
| Animations | KayKit Character Animations 1.1 — **already downloaded** (`Downloads/KayKit_Character_Animations_1.1.zip`, CC0), same rig as the Knight. Four of its eight files are in the game; the other four are needed: **CombatRanged** (bow: aim, draw, release; magic: raise, shoot, spellcasting, summon), **MovementAdvanced** (dodges forward/back/left/right, sneaking, running with a bow), **Special** | Copy in, no download |
| Dagger fighting | CombatMelee has **Melee_Dualwield_Attack_Chop / Slice / Stab** (three blows; the fourth of the chain can be a 1H stab or slice), and the dodges for the Shift step | Ready |
| Weapons | KayKit has `dagger`, `bow`, `bow_withString`, `arrow_bow`, `quiver`, `staff`, `wand`, `spellbook` (CC0, in the zip). Ours in Blender, as the swords were: a model per dagger and per bow, with icons (`art/blender/GUIDE.md`) | KayKit to start; ours per item |
| Two weapons in hand | GearRig holds one weapon in `handslot.r`; the rig has `handslot.l` too | Small change |
| Items by class | Every item already has `class_restriction` (`["warrior"]` today) | Ready |
| Stats by class | `CharacterClass` already flows through Attributes, StatBlock, Equipment | Partly ready |
| Class in the save | Not yet; old saves become Warriors | To do |
| Skills | A skill file per class, two branches each, 6 skills per branch as the Warrior | To design with you |

### Order (one class at a time)

1. **CLS-01** the game with several classes (save, skills per class, starting gear, items per class) — no new class yet, the Warrior still works as today.
2. **CLS-02** the character creator (section 2).
3. **CLS-03** the dagger fighter, daggers branch: skills, weapons, animations, balance.
4. The archer branch; then class 3 (magic weapons); then class 4 (shaman). Each finishes act 1 and the tower alone before the next starts.

---

## 2. The character creator

### How a character's look is made today

The player is **KayKit Adventurers 2.0** (CC0): the **Knight's skeleton** (23 bones, the
"Rig_Medium" every KayKit character shares) wearing the **Ranger's plain body** (grafted at load,
2026-09-29) and the **Knight's head**. Each character in the pack is a handful of pieces:

| Piece | Rides | Note |
|---|---|---|
| Head | the head bone, rigid | **the hair is part of the head** — one mesh, one texture |
| Body, arms, legs | chest/spine/hips, arm and leg bones | skinned |
| Cape | chest/spine/hips | hidden under our armours, which bring their own |
| Extras | head | Knight's helmet and visor, Mage's hat, the hooded Rogue's mask — separate pieces |

Our armours and helmets are rigid pieces on bones (`bone__chest`, `bone__head`…), placed
against the skeleton's rest pose. Colours come from one small palette texture per character
(flat swatches), not painted detail.

### Who is in the pack (free edition, already downloaded)

Knight, Barbarian, Mage, Ranger, **Rogue** and **Rogue hooded** — the **Rogue is a woman**
(red hair, long at the back, an earring), and the hooded one is her with a hood and a face mask.
The other four are men: four faces (clean-shaven blond, bearded, long black hair, goatee).

**Measured on 2026-09-30:** every character has the **same bones in the same places** (head bone
at 1.24 m, chest 0.97, shoulders 0.21 out). So:

- **our armours fit the Rogue with no change** — tried with the Warden Plate and the Leather Vest;
- **the animations are shared** — a woman fights, runs and dies with the same clips.

The bodies are the same chibi shape for men and women in this style; what tells them apart is
the head: face, hair, earrings.

### Hair, armour and helmets

Tried: the Rogue wearing each of our four helmets and the leather cap.

- **Helmets:** her long hair comes out **under the back and sides of the helm** — the way hair
  does under a helmet. No hair through the metal: the helms were built with room for hair.
  Faces show through the open helms as they should.
- **Armour:** no interaction; hair falls onto the back of the armour (the plates start at the
  shoulders).
- **Where it can go wrong:** a hairstyle taller or wider than the Rogue's — buns, spikes, a
  high tail — would pierce a helm. The rule, if we add such styles: each hairstyle says what it
  becomes under a helm (hidden, cut to what shows below the rim, or unchanged), and a scripted
  check photographs **every hair under every helm** before it ships.

### How customisable, and what each step costs

| Level | What the player picks | How | Effort | Risk |
|---|---|---|---|---|
| **A — first version (recommended)** | class · **man / woman** · **face** (4 men, 1 woman; the hooded variant as a look for the dagger class) · **hair colour** (5–6) · **skin tone** (3) · name · difficulty. A turning 3D preview, idling with the class's weapon | Heads swapped as whole pieces (as the Ranger body is today); colours by swapping swatches in the palette texture (as the armour skins already recolour) | **Medium**: the screen, the save fields, the head swap, the recolouring. No new models | Low |
| **B — more hair** | + separate **hairstyles** (short, braid, buns, shaved…) on any face | In Blender (our pipeline): split each KayKit head into face + hair, then make new hairstyles ours, riding the head bone | **Medium-high**: modelling each style to KayKit's look, the helm rule above | Medium: they must look like KayKit's |
| **C — more faces** | + more faces, beards, scars, eyes | New faces modelled in Blender, or a larger pack | **High** | Style match; a paid pack is a purchase for you |

Not recommended: **Quaternius' modular characters** (the villagers' pack, also downloaded).
They have men and women, outfits and separate hair, but they are realistic in proportion,
not chibi, and use another skeleton — every armour, helm and animation would have to be redone
for them. They stay as the villagers.

### What the creator screen does

New game → **class** (only the ones built; others shown as "coming") with what it plays like
→ **look** (man/woman, face, hair colour, skin) on a turning preview → **name** → **difficulty**
→ start. The look is saved with the character and shown on the character list; a later
"barber" could change it in the game (not planned).

---

## 3. What I need from you

1. The dagger fighter and the archer: **one class with two branches** (my reading), or two classes?
2. **Names** for the three new classes (ideas above, or yours).
3. The creator: **level A first** (recommended), B later?
4. The dagger class's feel: its Shift (a short dash with a moment untouchable, as designed),
   and whether its first weapons are KayKit's dagger or ours from Blender straight away.

---

## 4. Your answers (2026-09-30)

1. **Two classes**, not one with two branches: the dagger fighter and the archer are separate.
2. **Names**: Ninja, Arciere (Archer), Asura, Dragoveggente (Dragonseer). "Ninja" is also the
   original's name for its dagger class — flagged, kept unless you change it.
3. **The woman first** — before the other classes and before the full creator.
4. **Every new class's weapons and armour made in Blender from the start**, no KayKit stand-ins.

The roadmap's P2 rows (CLS-00 … CLS-07, ART-01) follow these.
