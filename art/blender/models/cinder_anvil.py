"""The Cinder Anvil — the Cinderlands' shard (A2-01): the orcs' great anvil, left in the fire until the fire stayed
in it. Black iron on a block of basalt, its face white-hot, flame standing round its foot.

A shard's visual is stretched to its height by the game: the size here only sets its proportions.
"""

import math
import random

import armourkit as a
import temins as t


def build():
    iron = t.material("cinder_iron", (0.07, 0.06, 0.06), metal=0.55, rough=0.5)
    basalt = t.material("basalt", (0.16, 0.14, 0.14), rough=0.85)
    lava = t.material("lava", (1.0, 0.36, 0.06), glow=1.7, rough=0.4)
    hot = t.material("white_heat", (1.0, 0.78, 0.42), glow=2.0, rough=0.3)

    rng = random.Random(37)
    pieces = [
        t.rock("block", 1.0, basalt, at=(0, 0, 0.55), seed=9, squash=(1.15, 0.95, 0.62), roughness=0.2, subdivisions=1),
        # The anvil: foot, waist, body, and the horn to one side.
        t.box("foot", (1.3, 0.8, 0.28), iron, at=(0, 0, 1.28), bevel=0.05, segments=2),
        t.box("waist", (0.7, 0.5, 0.62), iron, at=(0, 0, 1.72), bevel=0.08, segments=2),
        t.box("body", (1.7, 0.72, 0.5), iron, at=(0.1, 0, 2.25), bevel=0.06, segments=2),
        a.spike("horn", (0.95, 0, 2.27), (1.75, 0, 2.42), 0.24, iron, sides=12),
        t.box("heel", (0.36, 0.62, 0.3), iron, at=(-0.86, 0, 2.33), bevel=0.04, segments=2),
        # Its face, white-hot, and the seam of fire round its waist.
        t.box("face", (1.6, 0.62, 0.06), hot, at=(0.1, 0, 2.52), bevel=0.02),
        t.box("seam", (0.76, 0.56, 0.07), lava, at=(0, 0, 1.44), bevel=0.02),
        t.box("seam_high", (0.76, 0.56, 0.07), lava, at=(0, 0, 2.0), bevel=0.02),
        # A blade left on it, half made.
        t.box("billet", (0.9, 0.12, 0.05), hot, at=(0.0, 0.05, 2.58), bevel=0.015),
    ]

    # Flame standing round the foot of the block, and cracks of fire in the rock.
    for i in range(9):
        rad = i / 9 * math.tau + rng.uniform(-0.2, 0.2)
        far = rng.uniform(1.0, 1.3)
        tall = rng.uniform(0.5, 1.1)
        base = (math.cos(rad) * far, math.sin(rad) * far * 0.85, 0.0)
        pieces.append(a.spike(f"flame_{i}", base, (base[0] * 0.92, base[1] * 0.92, tall), 0.14, lava, sides=6))

    for i in range(5):
        rad = i / 5 * math.tau + 0.3
        pieces.append(a.spike(f"crack_{i}", (math.cos(rad) * 0.5, math.sin(rad) * 0.42, 1.1), (math.cos(rad) * 1.08, math.sin(rad) * 0.9, 0.35), 0.05, lava, sides=5))

    obj = t.join("cinder_anvil", pieces)
    t.soften(obj, 35)

    return {"folder": "shards", "name": "cinder_anvil"}
