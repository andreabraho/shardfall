"""Mietitrice della Conca — a wide curved blade with a toothed back and a red-glowing edge, horns for a guard."""

import math

import swordkit as k
import temins as t


def build():
    dark = t.material("reaver_steel", (0.30, 0.30, 0.34), metal=0.55, rough=0.4)
    red = t.material("blood_glow", (0.95, 0.12, 0.08), glow=1.3, rough=0.4)
    horn = t.material("horn", (0.20, 0.15, 0.12), rough=0.6)
    leather = t.material("leather_dark")
    iron = t.material("iron", metal=0.5, rough=0.55)

    # The belly curves out to the right and the point sweeps back left; teeth along the back.
    outline = [
        (0.13, 0.0), (0.17, 0.5), (0.23, 1.0), (0.24, 1.35), (0.18, 1.68), (0.02, 1.96),
        (-0.06, 1.86), (-0.04, 1.62), (-0.12, 1.55), (-0.10, 1.40), (-0.16, 1.30), (-0.13, 1.14),
        (-0.18, 1.04), (-0.14, 0.9), (-0.14, 0.0),
    ]

    parts = [*k.glowing_edge(outline, 0.12, dark, red, inset=0.8)]

    for side in (False, True):
        h = t.profile_solid(f"horn_{side}", [
            (0.0, -0.04), (0.16, -0.05), (0.30, 0.04), (0.36, 0.22), (0.30, 0.12), (0.16, 0.05), (0.0, 0.05),
        ], 0.08, horn, bevel=0.01)
        parts.append(k.stand(h, z=0.02, mirror=side))

    # A spike for a pommel, pointing down.
    spike = t.cylinder("spike", 0.06, 0.16, iron, sides=6, radius_top=0.0)
    spike.rotation_euler.x = math.pi
    spike.location.z = -0.48

    parts += [
        t.box("collar", (0.16, 0.14, 0.1), iron, at=(0, 0, 0.02), bevel=0.02),
        *k.grip(0.40, 0.046, leather, iron, wraps=4),
        spike,
    ]
    return k.finish("hollow_reaver", parts)
