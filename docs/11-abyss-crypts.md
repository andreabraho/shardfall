# 11 — The Abyss Crypts (P5)

Decided with the user on 2026-10-07. The last dungeon, after the Burning Warlord, modelled on the shape of the
original's Devil's Catacombs (seven rooms, each its own idea, three bosses at the bottom) with nothing of its
names or its art.

## 1. What was asked

- **Seven floors after the original's catacombs**: a key carried from the creatures to a statue; sealed doors
  broken through to the centre; the one true stone among seven; a labyrinth of portals; a boss; a boss with
  three wardens, fought only once they are dead; the last boss with three of its spawn.
- **No total time limit.** (The original's hour was declined.)
- **Opens after the Burning Warlord**, at level 40, from the Cinderlands.
- **A whole new set of gear — every slot, not only the weapon and the armour — dropped only by the last boss,
  one piece a run.**
- **Names of our own**: Cripte dell'Abisso; il Carceriere, il Traghettatore d'Anime, il Signore della Cripta.

## 2. The floors

| # | Floor | Task | What the player does |
|---|---|---|---|
| 1 | **Ossario** (the Ossuary) | key | Kill the crypt's creatures until one of them drops the **Soul Key**; take it to the statue in the middle and strike the statue to set it. One kill in ten carries it, and never more than fifteen are needed. The gate back to the Cinderlands and a shrine stand here. |
| 2 | **Porte Sigillate** (the Sealed Doors) | carry | The stair stands inside a walled court with four doors. Each door's guard rises as the player comes near; kill it, then break the door. All four open the court. |
| 3 | **Pietre del Giudizio** (the Stones of Judgement) | find | Seven stones that look alike; one is the true one. Every stone lets out a crowd when it breaks. |
| 4 | **Labirinto dei Portali** (the Portal Maze) | maze | Four halls one after another, three portals at the end of each. One leads on; the other two send the player back to the first hall. Which one changes every run. At the end of the last hall stands the runic pole: strike it. A few weak creatures wander. |
| 5 | **Cella del Carceriere** (the Jailer's Cell) | fight | **il Carceriere** — a heavy undead jailer, help at 75 / 50 / 25 %. |
| 6 | **Approdo delle Anime** (the Landing of Souls) | fight | **il Traghettatore d'Anime**, shielded by three **Custodi della Cripta**: it takes no harm while any of them stands. |
| 7 | **Trono dell'Abisso** (the Abyss Throne) | fight | **il Signore della Cripta** and three of its **Progenie**, an enrage at half. Its fall drops one piece of the abyss set. |

Shrines and benches: floor 1 (the way in), and floors 6 and 7 — a shrine and a bench after every boss, as the
tower has (FR-7.14). The last floor's stair leads out to the Cinderlands.

Bosses on three floors in a row break the tower's "a boss every third floor" (FR-7.13): the crypts are the
original's descent, not a tower, so a zone can say `"boss_pulse": false` and keep the rest of the rules
(no task twice in a row except fights, five distinct tasks, a shrine and bench after a boss).

## 3. The creatures

Devils and undead, the two families the tower already taught the player to carry stones against.

| Creature | Family | Role | Model (recoloured toward the abyss: black and violet, a red glow) |
|---|---|---|---|
| Segugio dell'Abisso (Abyss Hound) | undead | bruiser | the ember hound |
| Divoratore della Cripta (Crypt Ghoul) | undead | bruiser | the ghoul |
| Arciere Infernale (Infernal Archer) | devil | archer | the blue demon |
| Bruto della Cripta (Crypt Brute) | undead | shielder | the zombie brute |
| Sacerdote dell'Abisso (Abyss Priest) | devil | mender | the blue demon |
| Diavoletto dell'Abisso (Abyss Imp) | devil | bomber | the imp |
| Guerriero dell'Abisso (Abyss Warrior) | devil | bruiser | the demon |
| il Carceriere | undead | boss | the zombie brute, large |
| Custode della Cripta | undead | miniboss | the zombie brute |
| il Traghettatore d'Anime | devil | boss | the blue demon, large |
| Progenie della Cripta | devil | miniboss | the demon |
| il Signore della Cripta | devil | boss | the demon, very large |

Level 40–42, a step above the Cinderlands. Stand-ins until they have models of their own, as act 2's were.

## 4. The abyss set

Level 40, relic. Upgraded with **Cenere dell'Abisso** (any creature of the crypts) and **Cuore dell'Abisso**
(the three bosses).

| Slot | Piece | Base |
|---|---|---|
| Weapon | one per class (the Warrior's sword or great sword) | 98–142 (great sword 147–213) |
| Armour | one per class | 140 |
| Helmet | one per class | 60 |
| Shield | Egida dell'Abisso | 80 |
| Boots | Passi dell'Abisso | 50 |
| Bracelet | Spira dell'Abisso | +40 attack, an attribute born with it |
| Necklace | Cuore Nero | +420 health |
| Earring | Lacrima dell'Abisso | +220 mana, 3 % health a kill |
| Ring | Sigillo dell'Abisso | average or skill damage born with it |

The last boss's table draws one of the nine slots, the same chance each; the piece comes in the player's class.

## 5. What is ours, and what is a stand-in

- **Ours, made in Blender:** the four props (a sealed door, the Statue of Souls, a Stone of Judgement, the Runic
  Pole: `art/blender/models/crypt_door.py`, `soul_statue.py`, `judgement_stone.py`, `runic_pole.py`), and the whole
  abyss set — six weapons in the class kits, five armours (`warkit`, `shadowkit`, `classkit`), five headpieces
  (`abyss_visor.py` and the kits), the shield, boots and jewellery and the two parts in `trinkets.py`, with their
  icons. Black plate, bone and a crimson light.
- **Stand-ins:** the creatures and bosses are act 1's models recoloured toward the abyss, as act 2's were at first.
- The scene is written by `kiln scenes` (`src/Kiln.Tools/Commands/CryptLayout.cs`), as act 2's maps are.
