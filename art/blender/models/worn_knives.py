"""Coltelli Consunti — the Ninja's first pair: a plain kitchen-thin knife, pitted steel on a
wooden grip, no guard at all (CLS-03)."""

import swordkit as k
import temins as t


def build():
    steel = t.material("worn_steel", (0.50, 0.50, 0.50), metal=0.45, rough=0.6)
    wood = t.material("wood")
    cord = t.material("leather_dark")

    # One edge, a straight back, the belly rising to a clipped point.
    outline = [(-0.055, 0.0), (0.06, 0.0), (0.07, 0.30), (0.045, 0.44), (-0.05, 0.52), (-0.058, 0.25)]
    parts = [
        k.blade(outline, 0.05, steel, edge=0.02),
        t.cylinder("collar", 0.05, 0.03, cord, at=(0, 0, 0.0), sides=16, bevel=0.005),
        *k.grip(0.24, 0.038, wood, cord, wraps=2),
    ]
    return k.finish("worn_knives", parts)
