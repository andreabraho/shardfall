"""A Stone of Judgement — the Abyss Crypts' third floor (P5, 2026-10-07): a slab of black stone standing on its end,
split by a seam of violet light, carved with rings of runes. Seven stand in a ring and look alike; one is the true
one.

The game stretches it to the visual's height.
"""

import math
import random

import armourkit as a
import temins as t


def build():
    stone = t.material("judgement_stone", (0.09, 0.08, 0.11), rough=0.75)
    seal = t.material("abyss_seal", (0.62, 0.30, 1.0), glow=1.4, rough=0.3)
    base = t.material("crypt_stone", (0.16, 0.13, 0.20), rough=0.85)

    rng = random.Random(41)
    pieces = [
        t.rock("foot", 0.75, base, at=(0, 0, 0.18), seed=5, squash=(1.2, 1.2, 0.35), roughness=0.18, subdivisions=1),
        # The slab, narrowing to a slanted top.
        t.box("slab", (0.95, 0.55, 2.3), stone, at=(0, 0, 1.35), bevel=0.08, segments=3),
        t.rock("crown", 0.52, stone, at=(0.04, 0, 2.55), seed=7, squash=(0.95, 0.56, 0.62), roughness=0.16, subdivisions=1),
        # The seam down the middle, front and back.
        t.box("seam_front", (0.07, 0.04, 1.9), seal, at=(0.02, -0.28, 1.4), bevel=0.01),
        t.box("seam_back", (0.07, 0.04, 1.6), seal, at=(-0.03, 0.28, 1.3), bevel=0.01),
    ]

    # Rings of runes, a few short marks to each side.
    for z in (0.75, 1.55, 2.2):
        for k in range(5):
            x = -0.36 + k * 0.18
            if abs(x) < 0.08:
                continue
            pieces.append(t.box(f"rune_{z}_{k}", (0.06, 0.03, rng.uniform(0.1, 0.2)), seal, at=(x, -0.285, z), bevel=0.005))

    # Shards of the same stone fallen round its foot.
    for i in range(5):
        rad = i / 5 * math.tau + rng.uniform(-0.3, 0.3)
        far = rng.uniform(0.8, 1.05)
        pieces.append(t.rock(f"chip_{i}", rng.uniform(0.12, 0.2), stone, at=(math.cos(rad) * far, math.sin(rad) * far, 0.08), seed=i + 10, squash=(1, 0.8, 0.6)))

    obj = t.join("judgement_stone", pieces)
    t.soften(obj, 30)

    return {"folder": "crypts", "name": "judgement_stone"}
