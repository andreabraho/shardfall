"""The Runic Pole — the end of the Abyss Crypts' portal maze (P5, 2026-10-07): a tall post of black stone bound with
iron rings, runes burning between them, and a crystal held in claws at the top. Striking it opens the way down.

The game stretches it to the visual's height.
"""

import math

import armourkit as a
import temins as t


def build():
    stone = t.material("judgement_stone", (0.09, 0.08, 0.11), rough=0.75)
    iron = t.material("crypt_iron", (0.10, 0.09, 0.11), metal=0.7, rough=0.4)
    seal = t.material("abyss_seal", (0.62, 0.30, 1.0), glow=1.4, rough=0.3)
    crystal = t.material("abyss_crystal", (1.0, 0.40, 0.70), glow=1.6, rough=0.15)
    base = t.material("crypt_stone", (0.16, 0.13, 0.20), rough=0.85)

    pieces = [
        t.cylinder("foot", 0.55, 0.3, base, at=(0, 0, 0.15), sides=8, radius_top=0.45, bevel=0.03),
        t.cylinder("pole", 0.18, 3.0, stone, at=(0, 0, 1.75), sides=8, radius_top=0.14, bevel=0.02),
        t.sphere("crystal", 0.2, crystal, at=(0, 0, 3.55), subdivisions=1, squash=(0.8, 0.8, 1.6)),
    ]

    for i, z in enumerate((0.6, 1.3, 2.0, 2.7)):
        pieces.append(t.cylinder(f"ring_{i}", 0.22, 0.1, iron, at=(0, 0, z), sides=24, smooth=True))

    for i, z in enumerate((0.95, 1.65, 2.35)):
        for k in range(4):
            rad = k / 4 * math.tau + i * 0.4
            pieces.append(t.box(f"rune_{i}_{k}", (0.05, 0.02, 0.22), seal, at=(math.cos(rad) * 0.17, math.sin(rad) * 0.17, z), bevel=0.005))
            pieces[-1].rotation_euler[2] = rad + math.pi / 2

    # Four claws of iron holding the crystal.
    for k in range(4):
        rad = k / 4 * math.tau + math.pi / 4
        pieces.append(a.spike(f"claw_{k}", (math.cos(rad) * 0.1, math.sin(rad) * 0.1, 3.2), (math.cos(rad) * 0.24, math.sin(rad) * 0.24, 3.75), 0.04, iron, sides=6))

    obj = t.join("runic_pole", pieces)
    t.soften(obj, 35)

    return {"folder": "crypts", "name": "runic_pole"}
