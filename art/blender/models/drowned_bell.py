"""The Drowned Bell — the Mire's shard (A2-01): the village's bell, pulled under with it and risen again on a
mound of mud and bones, green with verdigris, a ring of light where the marsh has written on it.

A shard's visual is stretched to its height by the game: the size here only sets its proportions.
"""

import math
import random

import armourkit as a
import temins as t


def build():
    bronze = t.material("bell_verdigris", (0.12, 0.34, 0.28), metal=0.55, rough=0.5)
    deep = t.material("bell_deep", (0.05, 0.14, 0.13), metal=0.5, rough=0.55)
    glow = t.material("bell_light", (0.35, 1.0, 0.62), glow=1.5, rough=0.2)
    mud = t.material("mire_mud", (0.10, 0.11, 0.08), rough=1.0)
    bone = t.material("bone", rough=0.7)

    rng = random.Random(31)
    pieces = [
        t.rock("mound", 1.25, mud, at=(0, 0, 0.12), seed=5, squash=(1.1, 1.0, 0.32), roughness=0.16, subdivisions=2),
        # The bell, lip to crown: each band narrower at its top than at its foot.
        a.band("lip", 1.02, 1.02, 0.45, 0.72, deep, sides=36, top=0.88),
        a.band("waist", 0.90, 0.90, 0.72, 1.95, bronze, sides=36, top=0.74),
        a.band("shoulder", 0.666, 0.666, 1.95, 2.55, bronze, sides=36, top=0.52),
        t.sphere("crown", 0.36, deep, at=(0, 0, 2.55), subdivisions=2, squash=(1, 1, 0.75)),
        # What it hung from: a yoke of the same bronze, broken.
        t.box("yoke", (0.30, 0.16, 0.42), deep, at=(0, 0, 2.95), bevel=0.05, segments=2),
        t.cylinder("pin", 0.07, 0.62, deep, at=(0, 0, 3.08), sides=10, axis="X"),
        # Rings round it, and the lit one where the marsh has written.
        a.band("ring_low", 0.925, 0.925, 0.86, 0.94, deep, sides=36, top=0.985),
        a.band("ring_lit", 0.80, 0.80, 1.52, 1.62, glow, sides=36, top=0.975),
        a.band("ring_high", 0.70, 0.70, 1.90, 1.97, deep, sides=36, top=0.985),
        # The clapper, hanging below the lip.
        t.sphere("clapper", 0.2, deep, at=(0.1, -0.05, 0.42), subdivisions=2),
    ]

    # Marks of light between the rings: what is written on it.
    for i in range(9):
        rad = i / 9 * math.tau
        r = 0.845
        pieces.append(a.spike(f"mark_{i}", (math.cos(rad) * r, math.sin(rad) * r, 1.05), (math.cos(rad) * (r - 0.03), math.sin(rad) * (r - 0.03), 1.42), 0.04, glow, sides=4))

    # Bones in the mud at its foot.
    for i in range(6):
        rad = i / 6 * math.tau + rng.uniform(-0.3, 0.3)
        far = rng.uniform(1.15, 1.45)
        at = (math.cos(rad) * far, math.sin(rad) * far, 0.2)
        pieces.append(a.spike(f"bone_{i}", at, (at[0] + rng.uniform(-0.3, 0.3), at[1] + rng.uniform(-0.3, 0.3), 0.55), 0.05, bone, sides=6))

    obj = t.join("drowned_bell", pieces)
    t.soften(obj, 40)

    return {"folder": "shards", "name": "drowned_bell"}
