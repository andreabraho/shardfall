"""Elmo dell'Abisso — the Warrior's helm of the abyss set (P5, 2026-10-07): a pointed cap of black plate with a nasal lit down its middle,
 bone horns curving forward from the temples, a crest of bone spines over the crown."""

import math

import armourkit as a
import temins as t


def build():
    black = t.material("abyss_plate", (0.05, 0.04, 0.07), metal=0.75, rough=0.3)
    dark = t.material("abyss_dark", (0.10, 0.06, 0.12), metal=0.6, rough=0.4)
    glow = t.material("abyss_glow", (0.95, 0.10, 0.38), glow=1.4, rough=0.4)
    bone = t.material("bone")

    # Taller than it is wide: pointed, as the user likes a helm.
    radii = (a.HELM[0], a.HELM[1] + 0.02, a.HELM[2] + 0.18)
    brow = a.around(a.HEAD, radii, 1.98, grow=0.02)
    rim = a.around(a.HEAD, radii, 1.72, grow=0.012)

    head = [
        a.shell("cap", black, centre=a.HEAD, radii=radii, below=1.72, thickness=0.035),
        a.band("brow", brow[0], brow[1], 1.94, 2.03, dark, sides=40, y=a.HEAD[1]),
        a.band("rim", rim[0], rim[1], 1.70, 1.76, dark, sides=40, y=a.HEAD[1]),
        # A keel of plate from the brow over the crown to the nape: the helm comes to an edge, not a dome.
        a.shell("keel", dark, centre=a.HEAD, radii=(0.05, radii[1] + 0.06, radii[2] + 0.14), below=1.98, thickness=0.04, segments=6),
        # A nasal down the face with a seam of light in it.
        t.box("nasal", (0.08, 0.04, 0.36), black, at=(0, -0.645, 1.74), bevel=0.015),
        t.box("nasal_light", (0.025, 0.045, 0.24), glow, at=(0, -0.655, 1.76), bevel=0.006),
        t.box("cheek_l", (0.05, 0.26, 0.30), dark, at=(0.60, -0.22, 1.56), bevel=0.02),
        t.box("cheek_r", (0.05, 0.26, 0.30), dark, at=(-0.60, -0.22, 1.56), bevel=0.02),
        t.sphere("gem", 0.06, glow, at=(0, -0.66, 1.99), subdivisions=2, squash=(1, 0.7, 1.2)),
    ]

    # Horns of bone from the temples, out and forward.
    for side in (-1, 1):
        root = (side * brow[0] * 0.95, a.HEAD[1] - 0.10, 2.0)
        mid = (side * brow[0] * 1.35, a.HEAD[1] - 0.20, 2.22)
        tip = (side * brow[0] * 1.30, a.HEAD[1] - 0.48, 2.42)
        head.append(a.spike(f"horn_{side}_a", root, mid, 0.09, bone, sides=10))
        head.append(a.spike(f"horn_{side}_b", mid, tip, 0.06, bone, sides=10))

    # A crest of bone spines from the brow over the crown to the nape, longest in the middle.
    for i in range(7):
        rad = math.radians(-70 + i * 23)
        y = a.HEAD[1] + math.sin(rad) * (radii[1] + 0.06)
        z = a.HEAD[2] + math.cos(rad) * (radii[2] + 0.14)
        tall = 0.16 + 0.10 * (1 - abs(i - 3) / 3)
        head.append(a.spike(f"spine_{i}", (0, y, z), (0, y + math.sin(rad) * tall, z + math.cos(rad) * tall), 0.045, bone, sides=6))

    return a.finish("abyss_visor", {"head": head})
