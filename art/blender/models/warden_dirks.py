"""Daghe del Custode — the wardens' long narrow dirks: a stiff ribbed blade, a gilded guard
whose arms turn up to catch a sword, a blue-bound grip (CLS-03)."""

import swordkit as k
import temins as t


def build():
    steel = t.material("steel", metal=0.62, rough=0.32)
    gold = t.material("gold", metal=0.75, rough=0.35)
    blue = t.material("warden_blue", (0.12, 0.20, 0.42), rough=0.8)

    outline = k.mirrored([(0.06, 0.0), (0.05, 0.40), (0.035, 0.62), (0.0, 0.76)])
    # A guard like a trident's foot: the arms rise beside the blade.
    guard = t.profile_solid("guard", [
        (-0.17, 0.16), (-0.19, 0.02), (-0.12, -0.04), (0.12, -0.04), (0.19, 0.02), (0.17, 0.16),
        (0.135, 0.05), (0.09, 0.03), (-0.09, 0.03), (-0.135, 0.05),
    ], 0.075, gold, bevel=0.014, segments=3)
    parts = [
        k.blade(outline, 0.07, steel),
        k.fuller(0.05, 0.56, 0.012, 0.085, gold),
        k.stand(guard),
        *k.grip(0.26, 0.039, blue, gold, wraps=4),
        *k.pommel("ball", 0.05, gold, at=-0.29),
    ]
    return k.finish("warden_dirks", parts)
