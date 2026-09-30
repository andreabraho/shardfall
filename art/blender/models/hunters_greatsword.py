"""Spadone del Cacciatore — a broad, plain great sword, a guard of antler, a bone pommel."""

import swordkit as k
import temins as t


def build():
    steel = t.material("worn_steel", (0.55, 0.53, 0.50), metal=0.35, rough=0.65)
    antler = t.material("bone", rough=0.7)
    wood = t.material("wood_dark")
    leather = t.material("leather")
    cord = t.material("leather_dark")

    antlers = t.profile_solid("antlers", [
        (-0.36, 0.10), (-0.30, 0.02), (-0.14, -0.02), (0.14, -0.02), (0.30, 0.02), (0.36, 0.10),
        (0.33, 0.12), (0.26, 0.06), (0.20, 0.10), (0.18, 0.05), (0.10, 0.05), (-0.10, 0.05),
        (-0.18, 0.05), (-0.20, 0.10), (-0.26, 0.06), (-0.33, 0.12),
    ], 0.11, antler, bevel=0.016, segments=3)

    parts = [
        k.blade(k.mirrored([(0.16, 0.0), (0.165, 1.30), (0.13, 1.70), (0.0, 1.95)]), 0.11, steel),
        k.stand(antlers),
        t.box("block", (0.24, 0.13, 0.08), wood, at=(0, 0, 0.0), bevel=0.02),
        *k.grip(0.40, 0.045, leather, cord, wraps=5),
        *k.pommel("ball", 0.07, antler, at=-0.44),
    ]
    return k.finish("hunters_greatsword", parts)
