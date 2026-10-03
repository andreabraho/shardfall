"""What act 2's Quaternius creatures wear (2026-10-03): pieces of ours hung on their bones, so a model from a pack
belongs to its land — crystal grown through a mushroom's cap, weed and mud on a toad's back, a crest of black rock
and fire on a drake, a flame on a whelp's head. Built like `bosskit`'s: round the origin in units of what it sits
on (a head or a back of radius 1), sized and placed by the visual's `attachments`.

`build(name)` makes one of: lichen_crystals, mire_weed, drake_crest, whelp_flame.
"""

import math

import armourkit as a
import temins as t


def lichen_crystals():
    """Crystal standing out of a mushroom's cap, leaning out from the middle, and pale lichen in crusts round them."""
    crystal = t.material("stag_crystal", (0.25, 0.90, 0.82), glow=1.5, rough=0.15)
    deep = t.material("geode_crystal", (0.36, 0.22, 0.72), glow=0.8, rough=0.2)
    lichen = t.material("lichen", (0.62, 0.70, 0.52), rough=0.9)
    slate = t.material("slate_slab", (0.11, 0.12, 0.13), rough=0.75)

    parts = []

    # A cluster at the crown, the tallest in the middle.
    for i, (x, y, tall, lean, width) in enumerate(((0.0, 0.0, 1.25, 0.0, 0.26), (0.32, 0.12, 0.85, 0.35, 0.2),
                                                   (-0.28, 0.2, 0.8, 0.3, 0.19), (0.1, -0.34, 0.75, 0.3, 0.18),
                                                   (-0.15, -0.22, 0.55, 0.4, 0.14))):
        reach = math.hypot(x, y) or 1.0
        tip = (x + x / reach * lean, y + y / reach * lean, tall)
        parts.append(a.spike(f"crystal_{i}", (x, y, -0.1), tip, width, deep if i == 4 else crystal, sides=6))

    # Smaller ones breaking the cap further out.
    for i in range(6):
        rad = i / 6 * math.tau + 0.4
        x, y = math.cos(rad) * 0.85, math.sin(rad) * 0.85
        parts.append(a.spike(f"shard_{i}", (x * 0.9, y * 0.9, -0.15), (x * 1.25, y * 1.25, 0.25 + 0.12 * (i % 2)), 0.11, crystal, sides=5))

    # Crusts of lichen and a slab or two of stone round the cluster's foot.
    for i in range(7):
        rad = i / 7 * math.tau
        x, y = math.cos(rad) * 0.55, math.sin(rad) * 0.55
        parts.append(t.sphere(f"lichen_{i}", 0.2 + 0.05 * (i % 3), lichen, at=(x, y, -0.08), subdivisions=2, squash=(1.3, 1.1, 0.35)))
    parts.append(t.rock("slab_a", 0.24, slate, at=(0.2, 0.32, 0.0), seed=3, squash=(1.2, 0.8, 0.6)))
    parts.append(t.rock("slab_b", 0.2, slate, at=(-0.35, -0.12, 0.0), seed=5, squash=(1.0, 1.2, 0.6)))

    return parts


def mire_weed():
    """A toad's back in the marsh: a hump of mud, weed hanging off it to either side, reeds and a lily pad on top."""
    mud = t.material("mire_mud", (0.16, 0.13, 0.08), rough=0.95)
    kelp = t.material("mire_kelp", (0.10, 0.30, 0.16), rough=0.8)
    dark = t.material("mire_kelp_dark", (0.04, 0.14, 0.09), rough=0.85)
    reed = t.material("mire_reed", (0.42, 0.40, 0.20), rough=0.8)
    pad = t.material("mire_lily", (0.18, 0.40, 0.14), rough=0.7)
    light = t.material("bell_light", (0.35, 1.0, 0.62), glow=1.6, rough=0.2)

    parts = [t.rock("mud", 0.55, mud, at=(0, 0, 0.05), seed=7, squash=(1.3, 1.6, 0.45))]

    for i in range(10):
        side = 1 if i % 2 else -1
        y = -0.7 + (i // 2) * 0.35
        root = (side * 0.45, y, 0.12)
        parts.append(a.spike(f"weed_{i}", root, (side * 1.0, y + 0.1, -0.55 - 0.15 * (i % 3)), 0.14, kelp if i % 3 else dark, sides=5))

    for i, (x, y, tall) in enumerate(((0.1, 0.3, 0.9), (-0.15, 0.4, 0.7), (0.05, 0.55, 1.05))):
        parts.append(a.spike(f"reed_{i}", (x, y, 0.1), (x + 0.08, y + 0.12, 0.1 + tall), 0.045, reed, sides=5))
        parts.append(t.sphere(f"head_{i}", 0.06, mud, at=(x + 0.08, y + 0.12, 0.1 + tall * 0.85), subdivisions=1, squash=(1, 1, 2.2)))

    parts.append(a.band("pad", 0.32, 0.32, 0.2, 0.24, pad, sides=14))
    parts[-1].location = (-0.15, -0.35, 0.0)

    # A will-o'-the-wisp's light caught in the weed.
    parts.append(t.sphere("wisp", 0.09, light, at=(-0.2, -0.32, 0.36), subdivisions=2))

    return parts


def drake_crest():
    """A crest of black rock from a drake's brow down its neck, fire in the cracks between the plates."""
    rock = t.material("cinder_rock", (0.06, 0.05, 0.05), rough=0.7)
    lava = t.material("lava", (1.0, 0.36, 0.06), glow=1.7, rough=0.4)
    hot = t.material("white_heat", (1.0, 0.78, 0.42), glow=2.0, rough=0.3)

    parts = []

    for i in range(6):
        y = -0.4 + i * 0.32
        tall = 0.95 - 0.12 * i
        parts.append(a.spike(f"plate_{i}", (0, y, 0.0), (0, y + 0.3, tall), 0.26 - 0.02 * i, rock, sides=5))
        parts.append(a.spike(f"fire_{i}", (0, y + 0.08, 0.0), (0, y + 0.3, tall * 0.55), 0.14, hot if i % 2 else lava, sides=5))

    for side in (1, -1):
        for i in range(3):
            y = -0.2 + i * 0.4
            parts.append(a.spike(f"side{side}_{i}", (side * 0.28, y, 0.0), (side * 0.7, y + 0.3, 0.38 - 0.08 * i), 0.12, rock, sides=5))

    return parts


def whelp_flame():
    """A whelp's head alight: three tongues of flame standing back from the brow, white at the root."""
    lava = t.material("lava", (1.0, 0.36, 0.06), glow=1.7, rough=0.4)
    hot = t.material("white_heat", (1.0, 0.78, 0.42), glow=2.0, rough=0.3)

    parts = []

    for i, (x, tall) in enumerate(((0.0, 1.2), (0.32, 0.8), (-0.32, 0.8))):
        parts.append(a.spike(f"flame_{i}", (x, -0.1, 0.0), (x * 1.3, 0.45, tall), 0.3 if i == 0 else 0.22, lava, sides=6))
        parts.append(a.spike(f"core_{i}", (x, -0.05, 0.0), (x * 1.2, 0.25, tall * 0.55), 0.15, hot, sides=6))

    return parts


PIECES = {"lichen_crystals": lichen_crystals, "mire_weed": mire_weed, "drake_crest": drake_crest, "whelp_flame": whelp_flame}


def build(name):
    obj = t.join(name, PIECES[name]())
    t.soften(obj, 40)

    return {"folder": "creatures", "name": name}
