"""Spada d'Acciaio — bright steel, a bronze guard with flared ends, a faceted pommel."""

import swordkit as k
import temins as t


def build():
    steel = t.material("bright_steel", (0.80, 0.82, 0.86), metal=0.7, rough=0.3)
    groove = t.material("steel_dark", metal=0.5, rough=0.5)
    bronze = t.material("bronze", metal=0.6, rough=0.4)
    leather = t.material("leather_dark")

    parts = [
        k.blade(k.mirrored([(0.12, 0.0), (0.126, 0.8), (0.116, 1.1), (0.06, 1.33), (0.0, 1.46)]), 0.095, steel),
        k.fuller(0.1, 1.0, 0.026, 0.1, groove),
        k.crossguard(0.27, 0.06, 0.1, bronze, droop=0.02, flare=0.8, at=0.03),
        t.box("collar", (0.13, 0.12, 0.07), bronze, at=(0, 0, 0.09), bevel=0.02),
        *k.grip(0.31, 0.043, leather, bronze, wraps=5),
        *k.pommel("facet", 0.07, bronze, at=-0.36),
    ]
    return k.finish("steel_sword", parts)
