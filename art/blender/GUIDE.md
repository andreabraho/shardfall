# Making Temins art in Blender — the house guide

Written 2026-09-30, at the end of P1, for whoever makes the next model (most likely Claude).
Read it before you model anything: a new sword, an act-2 monster, a prop for a new map, an
icon. Everything in the game made so far follows it, and the player notices when something
doesn't.

Everything is a Python script. Blender 5.0 runs headless, builds the model from the script,
exports a `.glb`, and Godot imports it like any other asset. Nothing is modelled by hand, and
nothing comes from outside: every model here is ours. So the art is reproducible (rebuild with
one command), reviewable (a diff) and tunable (change a number, rebuild).

---

## 1. The look

**Stylised, rounded, flat-coloured, no textures.** It sits beside the CC0 packs already in the
game (KayKit's chunky characters, Quaternius' low-poly world) without clashing: simple shapes,
clean silhouettes, bold colour areas, soft light on smooth surfaces.

| Rule | Why (what went wrong without it) |
|---|---|
| **No visible polygons.** Round things get enough sides — 16 for a grip or a leg, 20–32 for a band or a body, 40 for a helm — and are shaded smooth with `t.soften(obj, angle)`. | "Some armours look too low-poly"; "the iron helm is too round and you can see the polygons" (2026-09-30). Rebuilt with more faces and smoothing, and liked. |
| **Crisp where it is made, soft where it grows.** `soften` angle: 30 for swords (a ground edge stays a line), 40 for trinkets, 50 for armour, 75 for creatures. | A blade shaded smooth all over looks like soap; a wolf with hard edges looks carved. |
| **Bevels on every made edge**, 2–3 segments (`bevel=…, segments=3` on profiles). | Guards and plates without them read as cardboard. |
| **Colour by material, one flat colour each**, named by what it is (`t.material("iron")`, `t.material("wolf_fur", (r, g, b))`). No image textures. | Keeps every model the same family, and a skin can recolour it. |
| **The palette first.** `temins.PALETTE` (steel, iron, gold, bronze, leather, wood, stone, moss, bone, chitin, venom, ember, violet, cloth red…). New colours only for what the palette lacks — an animal's coat, a new map's stone. | |
| **Glow sparingly.** `glow=` 0.7–1.6 on the part that should shine (runes, a gem, an edge, eyes); 2–2.6 only for a boss's eyes. It blooms much more in Godot than in the Blender render. | Early swords glowed like torches in the game and had to be dimmed. |
| **Colours come out lighter in Godot than they look in the numbers** (tonemapping). A grey coat meant as mid-grey wants ~0.2 linear; check in the game, not only in Blender. | The first wolf was near white in the game. |
| **Metal:** `metal` 0.5–0.85 and `rough` 0.25–0.55. **Cloth, fur, leather:** `metal` 0, `rough` 0.8–0.95. | |

**Axes and scale.** Z is up, the model faces **−Y**, left is +X. Work in metres at about the
real size; the game rescales creatures and props to the height their visual declares, but
gear is used at the size it is built.

**Triangle budget.** Weapons 2–5k, an armour 5–10k, creatures 10–17k (a boss up to ~17k), a
prop a few thousand. `build.py` prints the count; stay near these.

**Names.** Nothing from the original game: no names, no designs copied. Script and file names
are English snake_case; what the player reads is in `game/data/strings/it.json` and `en.json`.

---

## 2. Commands

```bash
scripts/blender.sh <model>                 # builds art/blender/models/<model>.py → game/assets/temins/<folder>/<name>.glb
scripts/blender.sh <model> <folder>        # …and four turntable views into <folder>, lit like a sunny map
```

| Script | For |
|---|---|
| `art/blender/build.py` | What `blender.sh` runs. A model's `build()` returns `{"folder", "name"}` and optionally `"reference"` (a glb to render it on, e.g. the Knight), `"focus"` ((centre), reach) and `"replaces"` (objects of the reference to hide). |
| `art/blender/lineup.py -- out.png a.glb b.glb …` | A family side by side, flat on: are the swords one set? |
| `art/blender/icons.py -- [item ids…]` | Bag icons (section 5). |
| `art/blender/poses.py -- model.glb out name` | Frames from a creature's clips (`POSE_CLIPS`, `POSE_YAW`). |
| `art/blender/closeup.py -- model.glb prefix` | A creature's face (front, side) and paws. |

Blender is at `/c/Program Files/Blender Foundation/Blender 5.0/blender.exe`
(`blender -b --factory-startup --python <script> -- <args>`).

---

## 3. The kits

Build with the kits, not from scratch: they hold what was learnt.

### `temins.py` — everything
`reset()`, `material(name, colour, metal, rough, glow)` (cached per name), `profile_solid`
(an outline in XY given thickness, bevel and an optional sharp edge), `box`, `cylinder`,
`sphere(…, squash)`, `rock`, `add_bevel`, `join`, `soften`, `triangles`, `export_glb`.

### `swordkit.py` — weapons
Outlines are drawn in XY (X across, Y along) and stood up with `stand()`. `blade`, `mirrored`,
`glowing_edge`, `fuller`, `crossguard`, `grip`, `pommel("disc" | "ball" | "cup", …)`, then
`finish(name, parts)`.
- **The hand holds the grip, not the guard.** `finish` moves the sword so the origin sits
  6/10 up a one-handed grip, 4/10 up a great sword's. The grip part **must be named `grip`**.
- Sizes: one-handed ≈ 1.4 up, 0.37 down; great sword ≈ 1.95 up, 0.42 across the guard.
- **Every sword has its pair** (2026-10-02): a great sword of it, or a sword of the great sword — one theme,
  two designs (`bladekit.py`; `k.variant` stretched one into the other at first, and read as the same sword).
  The two items name each other with `"pair"` in weapons.json, and a drop that names one gives either.
- Each sword is a character: a silhouette you could tell at a glance (a hooked tip, antler
  guard, ribs, horns), a material story (worn steel, grave steel, ember-black), one accent.
- Skins recolour a sword by **tinting** it (`BladeSkin`, `Wash` = 0.18) — keep a sword's own
  colours distinct enough to survive a tint.

### `armourkit.py` — armour and helmets on the Knight
Rigid pieces, each riding one bone; `finish(name, {bone: [parts]})` joins them into objects
named `bone__<bone>` which the game hangs on the Knight's bones (GearRig). Built round the
Knight's T-pose (measurements in the file's docstring). `band`, `gap_band`, `shell`, `roll`,
`sleeve`, `scales`, `spike`, `cape`, `lathe` (helms from a profile), `rod`, `around`.
- **An armour brings its own cape** if it has one; the Knight's cape is hidden under armour.
  (The Knight's cape passing through the plates was disliked.)
- Under armour the player's torso is hidden and the Ranger's plain body is worn
  (`visuals.json` gear graft); `finish` adds a dark cloth underlay over the hips.
- Helmets use `bone__head`, `HEAD` and `HELM` for the head's centre and radii; leave room for
  the hair (`around()`), or it pokes through.
- Render on the Knight: `finish` returns the reference, so `blender.sh <model> <folder>` shows
  it worn.

### `ninjakit.py` — the Ninja's armours
Built on `armourkit`, round the Rogue's body the Ninja wears and **over its clothes**, which stay
on (`"own_clothes"` in `visuals.json`): `vest`, `binding`, `sash`, `tails`, `collar`, `pad`, `wraps`,
`bracer`, `shin`, `tassets`, `straps`, `headband` and `brow_plate` (headgear, ended with `finish_head`), then `finish(name, {bone: [parts]})`, which shows it on the
Ninja woman. Nothing covers the waist or the head, and shoulder pieces stand **out** past the
chibi head (x ≥ 0.40) or it hides them. Daggers are swords from `swordkit`, about 0.6–0.85 long,
two cells tall in the bag.

### `bowkit.py`, `fankit.py`, `hexkit.py`, `classkit.py` — the Archer, the Asura, the Dragonseer

- **Every weapon of the three is its own design** (2026-10-02, at your call — they were one shape a
  class in other colours, and the Asura's were the Warrior's swords recoloured): one small function a
  weapon inside `build(name)` of its kit, the model file three lines that call it.
- **Bows** (`bowkit.py`) — "big, heavy, with many branches": two thick limbs and what grows from their
  backs (`branch`: antlers, leaves, thorns, horns, feathers, blades, ribs, roots, crystals, flames —
  `SHAPES`, with `twigs`, a lit `tip`, an `inner` colour). They lie along Y, flat in Z, the grip at the
  origin, as KayKit's bow, the way the left hand (`handslot.l`) holds one — and **the end towards +Y
  points down in the hand**: it is the short limb (`LOWER`), or a bow this long stands in the ground.
  `icons.py` stands a `_bow` up, long limb at the top. The string is a mesh of its own named `bowstring`
  (2026-10-07): the game hides it and draws it from its ends, pulled back to the right hand while the Archer draws
  (`BowString.cs`); the bow is turned 40° in the hand (`"weapon_turn"` in visuals.json) so the string clears the forearm.
- **Fans** (`fankit.py`) — "showy": drawn flat in XY round the pivot (`sector` with a cut edge —
  `scallop`, `petal`, `flame`, `torn` — `plume`, `stick`, `disc`, `crescent`, `rays`, `tassel`), stood
  up, turned a quarter turn so the leaf faces forward, and held as a sword (the part named `grip`).
  Drawn larger than worn: `SCALE`. `icons.py` turns a `_fan` back flat on.
- **Cursed swords** (`hexkit.py`) — "dark magic swords": swordkit's pieces with shapes of their own
  (split round a core of light, toothed, flamed, hooked, crystal), `runes`, a `crack` of light, an
  `eye` in the guard, `shardlet`s hanging loose. A dark steel and a light of its own colour each.
- **Suits and headpieces** are in `classkit.py`: **one function a suit** since 2026-10-02 ("more their
  own, and better made" — all seven of a class were one function in other colours before, and read
  as one suit), each with a shape nobody else has: a straw cape, a rib cage, a collar of feathers, a
  wing of blades. Three headpiece functions, one a class. What they share is the first half of the
  file: `patch` (a curved sheet that follows the body, with a cut hem: `pointed`, `swallow`,
  `zigzag`, `ragged`), `leaves` (a row of pointed scales, feathers or fur; `arm_frame` puts a row
  round an arm), `tube` and `curve` (horns, antlers, ribs, cords, ribbons), `hoop`, `fin`, `blade`,
  `feather`, `cloak`, `necklace`, `seal`, and `Suit`, which collects the pieces bone by bone.
  Use `classkit.mirror`, not armourkit's, for anything turned: armourkit's flips a turned piece wrong.
- **Two cuts of one suit** (2026-10-02, "the women a little more uncovered"): `s.add(bone, parts, "m")`
  is worn by the men only, `"w"` by the women only, and no tag by both; the pieces are exported as
  `bone__<bone>__m` / `__w` and a body wears its own cut (visuals.json `"cut": "w"`). The men wear a
  vest from the belly to the shoulders; the women a band round the chest (`bust`, heights `BUST`),
  shoulders and belly bare — so what lies on the vest above 1.07 or below 0.86 is tagged "m" and cut
  again for the band. Icons draw the full cut.
- **What stands up on an arm in the rest pose points sideways in the game**: the arms hang. A horn or
  a blade meant to rise beside the head rides the chest, not the upper arm.
- **The Archer carries a quiver across the back**: nothing of the Archer's hangs there above the belt
  (a cape from the belt, a mantle on the bow arm).
- **The Asura is dark, not violet**: each suit has a second dark colour of its own — wine, blood red,
  petrol green, midnight blue, old gold and crimson, bone and ash, violet and gold ("they are all one
  colour", your call).
  The men's trunks are narrower (the Ranger's by a sixth, the Mage's a little) and their heads
  smaller: visuals.json `armour_fit` and `helmet_fit` scale the trunk's pieces and the headpiece
  for each body, so a suit is made once.
- Never plate on the Archer or metal on the Dragonseer; the Asura is the one in dark plate.

### `warkit.py`, `shadowkit.py`, `bladekit.py`, `daggerkit.py` — the Warrior and the Ninja, redrawn

"The same work on the Warrior and the Ninja" (2026-10-02): their gear had a file each but one
silhouette, the women wore the men's, and a great sword was its sword stretched.
- **`warkit.py`** — the Warrior's seven suits, one function each, in two cuts. The men's is built round
  the Knight (`KX`, `KY`, `kat`) and worn in place of the body's torso, a dark cloth at the hips
  (tagged "m"); the women's is classkit's band round the chest over her own clothes — her visuals
  carry `"own_clothes": true` and `"cut": "w"`, and her bodice is cut short (`woman_body.py`).
  The four helmets are as they were (the pointed iron helm is liked).
- **`shadowkit.py`** — the Ninja's seven suits and four headpieces, over its own clothes, two cuts.
- **`bladekit.py`** — twelve swords and twelve great swords: one function a pair, `great` choosing the
  design. A great sword is drawn, not stretched — `swordkit.variant` is no longer used for them.
- **`daggerkit.py`** — the Ninja's twelve daggers.
- The model files of all of these are three lines that call the kit.

### `trinkets.py` — gear that is never worn visibly
Shields, boots, bracelets, necklaces, earrings, rings: built only for their icon. Add a
builder named like the item id (`shd_…`, `bts_…`, `brc_…`, `nck_…`, `ear_…`, `rng_…`) to
`BUILDERS`; `torus`, `gem`, `chain`, `disc`, `boot`, `cuff`, `kite`, `ring`, `hook` help.

### `creaturekit.py` + `beast.py` — creatures
`creaturekit` lofts tubes through rings (`loft`), weights each part to the bones it lies along
(`weigh`/`bind`), builds a skeleton (`Rig`) and keys clips from poses (`clip`, `keyed`,
`blend`, `add`). `beast` draws any **four-legged** animal from `Proportions` and a coat; the
model file adds what is its own (`tuft`, `fangs`, `whiskers`, a mane…) and calls
`made.finish(STYLE)`. See `models/wolf.py` (base), `fox.py`, `field_rat.py`, `greymane.py`.

- **Proportions:** height, half_length, girth, depth, leg, neck (+angle), head (+tilt), skull,
  muzzle, eye, eyes_aside, brow, ears (+round), tail (+bush, droop, thin), haunch, toes, and the
  menace knobs **snarl, lid, ears_back, hackles**.
- **Coat keys:** fur, dark (saddle, brows, lids), pale (belly, throat, jaw), nose, eye (iris),
  pupil, lip, mouth, tooth, paw, pad, claw, inner (inside the ears; a rodent's bare tail).
- **STYLE:** stride, run_frames, walk_frames, bob, flex, lunge, bite, tail, side.
- **Clips it makes, named as the game looks for them:** `Idle`, `Walk`, `Run`, `Attack`,
  `HitReact`, `Death`. ModelAnimator plays Walk below 2.4 m/s (monsters roam at a walk), Run
  above; Attack on a blow, HitReact when hit, Death once and held.

A creature must look like it means harm. What made the animals read as threats, and what made
them read wrong:

| Do | Don't |
|---|---|
| Head low, at the shoulders (`neck_angle` ~50) | Head up like a pet |
| Brows in a **V, low at the nose** (`brow`), a heavy lid over the eye (`lid`) | Brows or lids low at the outer corner: it looks sleepy or sad |
| Eyes **on the surface** of the skull, with rim, iris, pupil and a glint, glowing a little | Eyes sunk in the head (invisible) or huge (a rodent's are small and beady) |
| Lips drawn back on a dark gum, teeth bared at rest (`snarl`), wrinkles on the muzzle | A red mouth band stuck on the side of the face |
| Hackles up along the spine (`hackles`), ears laid back (`ears_back`) | |
| Legs that run on into paws with toes and claws (`beast.paw`) | A leg ending in a cut-off tube over a pebble |
| Fur locks (`tuft`, flat ones with `across`) **rooted inside the body** | Bristles or spikes floating over the back |
| A mane as a thick collar of fur ending in locks | A mane of long spikes (it reads as a porcupine) |
| The tail still at rest, low | A tail swinging side to side at rest: it reads as wagging |
| Colours painted by **position** (a clean line along the body) | Colours by normal alone: blotchy camouflage |

Two-legged characters have not been made yet. For people, keep to the KayKit rigs (Knight,
Ranger, Barbarian…): the game's animations are theirs. A humanoid of ours would need the same
bone names and rest pose to share them — try one small piece first before promising a class.

### Props, shard stones, map pieces
Plain `temins` pieces (`rock`, `box`, `cylinder`, `profile_solid`) joined and softened, as
`models/brazier.py` and `models/ember_shard.py`. Mind the scale of the Quaternius and KayKit
pieces already on the maps; glow on the part that should draw the eye.

---

## 4. Into the game

1. **Build** it: `scripts/blender.sh <model> <render folder>` and look at the views.
2. **A visual** in `game/data/tables/visuals.json`, `"primitive": "model"`,
   `"model_path": "res://assets/temins/<folder>/<name>.glb"`, a `height` and `radius`. Creatures
   and props are **scaled to `height`**; creatures face −Y in Blender, which needs
   `"model_yaw": 180` in the game. Keep the old visual in the file when replacing one, to
   compare and to fall back to.
3. **Point the data at it:** an item's `visual` (weapons.json, armor.json), an enemy's `visual`
   (and `visual_scale` for a boss drawn bigger), a cosmetic's `visual` (with `hover` for a
   flier).
4. `dotnet run --project src/Kiln.Tools -- codegen` (new visual ids), then the Godot import;
   **`git checkout game/project.godot` after every import** (Godot rewrites it).
5. **A row in `ASSET-LICENSES.md`** — the "Temins originals" row lists what we made; extend it.
6. `bash scripts/verify.sh` must end "All checks passed".
7. **Look at it in the game** (section 6) before calling it done.

---

## 5. Icons

`art/blender/icons.py` renders `game/assets/icons/items/<item id>.png`, which the bag prefers
over its drawn fallback. It picks up every weapon, armour and helmet whose visual is one of
ours, and every item in `trinkets.BUILDERS`.

```bash
"/c/Program Files/Blender Foundation/Blender 5.0/blender.exe" -b --factory-startup --python art/blender/icons.py -- [item ids…]
```

- The picture is the item's bag cell: 128 px per cell of its `grid_size` (a sword 1×3, an
  armour 2×3, a ring 1×1), transparent, orthographic, in the item's own colours (the bag draws
  the rarity round it).
- **All weapons at one scale** (`WEAPON_HEIGHT`): a sword must look shorter than a great sword.
- Each kind has its angle (`kind → (yaw, pitch)` in `icons.py`): a sword flat on and turned a
  little, a ring from above, boots from the side. A new kind of item gets its own angle there.
- Forearm pieces are dropped from armour icons (they stand out sideways in the T-pose).
- Check the whole set together — `F8` in a debug build gives every armour and headpiece of the Ninja (2026-10-01); open the bag.

---

## 6. Checking, before showing it

- **Turntable** (`blender.sh … <folder>`): silhouette, colours, anything poking through.
- **Close-ups** for creatures (`closeup.py`): faces and paws.
- **Poses** for creatures (`poses.py`): every clip, and that it survived the export.
- **Side by side** with what it replaces (`lineup.py`, or in the game).
- **In the game**, through a scripted run: a GDScript `SceneTree` that starts a new game, puts
  things in front of a camera and saves screenshots. Run Godot with `--log-file` and
  `KILN_SAVE_DIR` pointing at a scratch folder (never the user's). Count frames in `_process`
  (an `await` there quits the tree). What to look at: size against the player, facing, feet on
  the ground, colours under the map's light, glow, and — for creatures — walking, running,
  biting, dying.
- Then show the user pictures and say how to see it in the game.

---

## 7. What the user has said about the art (keep to it)

- Likes: the three test pieces, a model per sword and per armour, own capes, the pointed iron
  helm, one scale for all weapon icons, painted icons for all gear, sword skins as a **light
  tint** (a fifth), the redone map-one animals once they had faces, paws and menace, monsters
  that **roam** round their spawn, the painted ground.
- Dislikes: capes through armour, armour over armour, visible polygons, round helms, swords held
  by the guard, opaque skins, legs without feet, dead or sleepy faces, floating bristles,
  wagging tails, anything under the ground (a flier must fly).
- The other maps' monsters (Quaternius) are liked and **stay** for now; new monsters (act 2)
  are ours.

---

## 8. Act 2 (2026-10-02): a new land from act 1's models

- **A map piece in one colour:** `"recolour": "#rrggbb"` (and `"glow"`) on a kit piece in
  `world_kit.json` paints the whole model flat — a tree turned to stone, a rock burnt black, a
  crystal that shines. Leafy trees turn into a pile of cards up close: use pines and dead trees.
- **A creature in another land's colours:** `"visual_recolour"` (and `"visual_glow"`) on an enemy
  turns every colour of its model toward one hue, keeping how light or dark each was.
  `visual_tint` alone does nothing to a model.
- **Shard stones** are plain `temins` and `armourkit` pieces joined (`models/petrified_heart.py`,
  `drowned_bell.py`, `cinder_anvil.py`): `band` for anything turned (a bell), `spike` for anything
  that points (a root, a flame, a crystal).
- **A new headpiece for a class** is one line in its kit's table of heads (`classkit.build`,
  `shadowkit.build`) and a three-line model file; the Warrior's is a model file of its own
  (`models/drowned_crown.py`). Keep helms pointed, not domed.
- **Scenes** come from `kiln scenes` (docs/10-act-2.md §5), not from Blender.
- **A beast of another land** is a model file on the wolf's (`models/slate_wolf.py`, `ember_hound.py`,
  `stone_stag.py`): its own `Proportions` and coat, then what makes it that land's — slabs and crystal
  (`slate_wolf.stone_parts`), flame — as `beast.tuft`s rooted in the body and weighted to its bones.
  Antlers are a `loft` for the beam and `tuft`s for the tines, on the head bone.
- **A two-legged boss** keeps act 1's body and wears pieces of ours (`bosskit.py`): each built round the
  origin in units of what it sits on (a head of radius 1), and hung on a bone by the visual's
  `"attachments"` — `scale`, `up` and `forward` are shares of how high that bone stands, so they are
  tuned by looking, in the game. A recolour leaves hung pieces alone.
- **A creature from a pack** (Quaternius, CC0) is dressed the same way (`fieldkit.py`): its model as it comes, a
  `visual_recolour` toward the land, and one piece of ours on a bone — crystal on a cap, weed on a back, a
  crest, a flame. A pack whose clips have their own names (the frog's `Frog_*`) needs them in `ModelAnimator`.
- **A golem** (`golemkit.py`) is stones of ours, each skinned whole to one bone of a metarig-named skeleton, and
  clips keyed by *aiming* each limb in the model's space (`{"aim": (x, y, z)}`, +X its left, -Y its front), with
  `turn`s and a root `move` laid over and a few key poses eased; the clips are named as the game looks for them
  (Idle, Walk, Run, Attack, Spellcasting, HitReact, Death). Any body on the same bone names gets them for free.
