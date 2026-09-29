"""Brando della Fornace — the relic: gold, a furnace mouth glowing in the guard, runes of fire up the blade."""

import swordkit as k
import temins as t


def build():
    steel = t.material("bright_steel", (0.80, 0.82, 0.86), metal=0.7, rough=0.3)
    gold = t.material("gold", metal=0.8, rough=0.3)
    ember = t.material("ember", glow=1.3, rough=0.4)
    leather = t.material("leather_dark")

    parts = [
        k.blade(k.mirrored([(0.14, 0.0), (0.15, 0.75), (0.135, 1.1), (0.07, 1.36), (0.0, 1.50)]), 0.1, steel),
        k.crossguard(0.30, 0.07, 0.11, gold, droop=-0.04, flare=1.0, at=0.03),
        # The furnace: a gold ring round a glowing heart, set in the guard.
        t.cylinder("furnace", 0.085, 0.13, gold, at=(0, 0, 0.06), sides=10, axis="Y", bevel=0.01),
        t.cylinder("heart", 0.055, 0.14, ember, at=(0, 0, 0.06), sides=10, axis="Y"),
        *k.grip(0.31, 0.044, leather, gold, wraps=5),
        *k.pommel("cup", 0.08, gold, at=-0.37, gem=ember),
    ]

    # Runes: diamonds of fire up the middle of the blade.
    for i, y in enumerate((0.3, 0.55, 0.8, 1.05)):
        size = 0.035 - i * 0.004
        parts.append(k.stand(t.profile_solid(f"rune_{i}", [(0, y - size * 1.6), (size, y), (0, y + size * 1.6), (-size, y)], 0.106, ember)))

    return k.finish("kiln_brand", parts)
