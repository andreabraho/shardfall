"""The Petrified Heart — the Stonewood's shard (A2-01): the trunk of a great tree turned to stone, split down its
front, and in the split a heart of crystal that still beats with light. Crystal has grown out of its roots.

A shard's visual is stretched to its height by the game: the size here only sets its proportions.
"""

import math
import random

import armourkit as a
import temins as t


def build():
    stone = t.material("petrified_wood", (0.30, 0.31, 0.31), rough=0.9)
    dark = t.material("petrified_dark", (0.13, 0.14, 0.15), rough=0.9)
    crystal = t.material("stone_crystal", (0.25, 0.90, 0.82), glow=1.5, rough=0.15)

    rng = random.Random(29)
    pieces = [
        # The trunk, in three lengths so it narrows and leans a little as a tree does.
        t.cylinder("trunk_low", 0.78, 1.2, stone, at=(0, 0, 0.6), sides=14, radius_top=0.62, smooth=True),
        t.cylinder("trunk_mid", 0.62, 1.2, stone, at=(0.03, 0.02, 1.8), sides=14, radius_top=0.52, smooth=True),
        t.cylinder("trunk_top", 0.52, 0.9, stone, at=(0.06, 0.04, 2.85), sides=14, radius_top=0.40, smooth=True),
        # Broken off at the top: a jagged rim.
        t.rock("break", 0.44, stone, at=(0.07, 0.05, 3.32), seed=3, squash=(1, 1, 0.5), roughness=0.34, subdivisions=1),
        # The split: a dark hollow down the front, and the heart in it.
        t.box("hollow", (0.34, 0.30, 1.5), dark, at=(0, -0.45, 1.7), bevel=0.12, segments=3),
        t.sphere("heart", 0.26, crystal, at=(0, -0.57, 1.75), subdivisions=2, squash=(0.8, 0.7, 1.4)),
    ]

    # Veins of crystal running up the split from the heart, and down into the roots.
    for i, (x, z0, z1) in enumerate(((-0.10, 2.0, 2.7), (0.09, 2.05, 2.9), (0.0, 0.6, 1.45), (-0.12, 0.9, 1.5))):
        pieces.append(a.spike(f"vein_{i}", (x, -0.62, z0), (x * 1.6, -0.60, z1), 0.035, crystal, sides=6))

    # The bark: ridges running up the trunk, all round but where it split.
    for i in range(12):
        bearing = i / 12 * math.tau
        if abs(bearing - math.radians(270)) < 0.6:
            continue
        low = (math.cos(bearing) * 0.72, math.sin(bearing) * 0.72, 0.05)
        high = (math.cos(bearing + 0.25) * 0.43, math.sin(bearing + 0.25) * 0.43, 3.1)
        pieces.append(a.spike(f"bark_{i}", low, high, 0.13, stone, sides=6))

    # Stubs of branches, snapped.
    for i, (bearing, z, reach) in enumerate(((140, 2.5, 0.7), (20, 2.9, 0.6), (250, 2.1, 0.5))):
        rad = math.radians(bearing)
        base = (math.cos(rad) * 0.45, math.sin(rad) * 0.45, z)
        tip = (math.cos(rad) * (0.45 + reach), math.sin(rad) * (0.45 + reach), z + reach * 0.7)
        pieces.append(a.spike(f"branch_{i}", base, tip, 0.14, stone, sides=8))

    # Roots spreading over the ground, and crystal standing up between them.
    for i in range(7):
        rad = i / 7 * math.tau + rng.uniform(-0.2, 0.2)
        far = rng.uniform(1.15, 1.5)
        pieces.append(a.spike(f"root_{i}", (math.cos(rad) * 0.55, math.sin(rad) * 0.55, 0.32), (math.cos(rad) * far, math.sin(rad) * far, 0.03), 0.2, stone, sides=7))

    for i in range(6):
        rad = (i + 0.5) / 6 * math.tau + rng.uniform(-0.25, 0.25)
        far = rng.uniform(0.9, 1.25)
        tall = rng.uniform(0.45, 0.9)
        base = (math.cos(rad) * far, math.sin(rad) * far, 0.0)
        pieces.append(a.spike(f"crystal_{i}", base, (base[0] * 1.08, base[1] * 1.08, tall), 0.11, crystal, sides=6))

    obj = t.join("petrified_heart", pieces)
    t.soften(obj, 40)

    return {"folder": "shards", "name": "petrified_heart"}
