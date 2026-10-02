"""Stiletti di Bronzo — leaf-shaped bronze blades with a raised rib and a ring to hang them
by, as the old hill people cast them (CLS-03)."""

import swordkit as k
import temins as t


def build():
    bronze = t.material("bronze", metal=0.7, rough=0.4)
    dark = t.material("bronze_dark", (0.36, 0.23, 0.12), metal=0.6, rough=0.5)
    leather = t.material("leather")

    outline = k.mirrored([(0.05, 0.0), (0.095, 0.16), (0.10, 0.28), (0.06, 0.48), (0.0, 0.62)])
    parts = [
        k.blade(outline, 0.06, bronze, edge=0.02),
        k.fuller(0.02, 0.44, 0.016, 0.075, dark),
        k.crossguard(0.10, 0.035, 0.07, dark, flare=0.2),
        *k.grip(0.24, 0.038, leather, dark, wraps=3),
        t.cylinder("ring", 0.055, 0.022, bronze, at=(0, 0, -0.30), sides=20, axis="Y", bevel=0.008),
    ]
    return k.finish("bronze_spikes", parts)
