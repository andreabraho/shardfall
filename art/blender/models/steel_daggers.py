"""Pugnali d'Acciaio — broad polished daggers, wide at the foot and tapering straight to the
point, a guard that droops and a faceted pommel (CLS-03)."""

import swordkit as k
import temins as t


def build():
    steel = t.material("bright_steel", (0.80, 0.82, 0.86), metal=0.7, rough=0.25)
    dark = t.material("steel_dark", metal=0.55, rough=0.45)
    red = t.material("cloth_red", rough=0.8)

    outline = k.mirrored([(0.105, 0.0), (0.09, 0.22), (0.055, 0.50), (0.0, 0.70)])
    parts = [
        k.blade(outline, 0.075, steel),
        k.fuller(0.03, 0.34, 0.03, 0.08, dark),
        k.crossguard(0.17, 0.05, 0.08, dark, droop=0.035, flare=0.15),
        *k.grip(0.26, 0.04, red, dark, wraps=4),
        *k.pommel("facet", 0.055, dark, at=-0.29),
    ]
    return k.finish("steel_daggers", parts)
