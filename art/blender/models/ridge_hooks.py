"""Uncini del Crinale — the ridge folk's forward-bent chopping knives: the blade widens and
hooks toward its edge, a notch at its foot, moss-green cord on the grip (CLS-03)."""

import swordkit as k
import temins as t


def build():
    steel = t.material("ridge_steel", (0.52, 0.56, 0.58), metal=0.6, rough=0.4)
    iron = t.material("iron", metal=0.5, rough=0.55)
    wood = t.material("wood_dark")
    cord = t.material("moss", rough=0.85)

    # Narrow at the foot, bent forward and broad at the belly, a point that drops.
    outline = [(-0.045, 0.0), (0.05, 0.0), (0.05, 0.08), (0.075, 0.10), (0.07, 0.22), (0.13, 0.42), (0.14, 0.54),
               (0.10, 0.66), (0.04, 0.70), (0.015, 0.50), (-0.03, 0.30), (-0.05, 0.14)]
    parts = [
        k.blade(outline, 0.07, steel, edge=0.025),
        t.cylinder("bolster", 0.055, 0.05, iron, at=(0, 0, 0.0), sides=16, bevel=0.008),
        *k.grip(0.26, 0.04, wood, cord, wraps=5),
        *k.pommel("disc", 0.05, iron, at=-0.285),
    ]
    return k.finish("ridge_hooks", parts)
