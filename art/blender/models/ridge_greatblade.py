"""Spadone del Crinale — a heavy cleaver from the desert ridge, a hook on its back, wrapped in sand-coloured cloth."""

import swordkit as k
import temins as t


def build():
    steel = t.material("iron_blade", (0.62, 0.63, 0.66), metal=0.55, rough=0.42)
    iron = t.material("iron", metal=0.5, rough=0.55)
    sand = t.material("sand", (0.78, 0.64, 0.42), rough=0.9)

    parts = [
        # Straight back on the left with a hook, a belly widening to an angled tip on the right.
        k.blade([
            (0.14, 0.0), (0.16, 0.6), (0.22, 1.35), (0.22, 1.62), (0.02, 1.95),
            (-0.14, 1.78), (-0.14, 1.32), (-0.24, 1.22), (-0.15, 1.16), (-0.14, 0.0),
        ], 0.12, steel),
        k.crossguard(0.34, 0.09, 0.12, iron, at=0.03),
        t.box("wrap", (0.22, 0.14, 0.05), sand, at=(0, 0, 0.1), bevel=0.015),
        *k.grip(0.40, 0.046, sand, iron, wraps=3),
        *k.pommel("facet", 0.075, iron, at=-0.45),
    ]

    # A tail of cloth hanging from the guard.
    tail = t.profile_solid("tail", [(0.0, 0.0), (0.05, 0.0), (0.07, -0.28), (0.02, -0.25)], 0.02, sand)
    parts.append(k.stand(tail, x=0.2))

    return k.finish("ridge_greatblade", parts)
