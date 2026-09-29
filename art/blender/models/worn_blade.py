"""Lama Consunta — the first sword: short, dull, its edge chipped from use."""

import swordkit as k
import temins as t


def build():
    steel = t.material("worn_steel", (0.55, 0.53, 0.50), metal=0.35, rough=0.65)
    iron = t.material("iron", metal=0.5, rough=0.55)
    leather = t.material("leather")
    cord = t.material("leather_dark")

    parts = [
        k.blade([
            (0.10, 0.0), (0.105, 0.42), (0.078, 0.47), (0.104, 0.53), (0.098, 0.92), (0.07, 1.14), (0.012, 1.27),
            (-0.03, 1.22), (-0.07, 1.10), (-0.095, 0.86), (-0.10, 0.70), (-0.075, 0.66), (-0.10, 0.61), (-0.10, 0.0),
        ], 0.085, steel),
        k.crossguard(0.19, 0.055, 0.08, iron, at=0.03),
        *k.grip(0.30, 0.04, leather, cord, wraps=3),
        *k.pommel("disc", 0.055, iron, at=-0.33),
    ]
    return k.finish("worn_blade", parts)
