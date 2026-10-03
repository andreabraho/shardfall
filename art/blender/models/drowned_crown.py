"""Corona Annegata — the Warrior's helm from act 2's Drowned Mire (A2-03): a cap of dark verdigris bronze coming to
an edge, open at the face, a nasal and cheek plates, a fin and a crown of coral, a pearl on the brow."""

import math

import armourkit as a
import temins as t


def build():
    bronze = t.material("mire_crown_bronze", (0.07, 0.24, 0.20), metal=0.65, rough=0.38)
    deep = t.material("mire_deep_bronze", (0.025, 0.09, 0.085), metal=0.55, rough=0.45)
    coral = t.material("mire_coral", (0.80, 0.30, 0.26), rough=0.6)
    pearl = t.material("mire_pearl", (0.92, 0.95, 0.90), glow=0.5, rough=0.2)

    # Taller than it is wide: the helm rises to a point rather than a dome.
    radii = (a.HELM[0], a.HELM[1] + 0.02, a.HELM[2] + 0.16)
    brow = a.around(a.HEAD, radii, 1.98, grow=0.02)
    rim = a.around(a.HEAD, radii, 1.72, grow=0.012)

    head = [
        # The cap: the skull covered, the face left open under the brow.
        a.shell("cap", bronze, centre=a.HEAD, radii=radii, below=1.72, thickness=0.035),
        a.band("brow", brow[0], brow[1], 1.94, 2.03, deep, sides=40, y=a.HEAD[1]),
        a.band("rim", rim[0], rim[1], 1.70, 1.76, deep, sides=40, y=a.HEAD[1]),
        # A fin of coral from the brow over the crown to the nape: the helm comes to an edge.
        a.shell("fin", coral, centre=a.HEAD, radii=(0.035, radii[1] + 0.07, radii[2] + 0.10), below=1.98, thickness=0.035, segments=6),
        # A nasal down the face and a plate over each cheek.
        t.box("nasal", (0.07, 0.04, 0.34), deep, at=(0, -0.645, 1.74), bevel=0.012),
        t.box("cheek_l", (0.05, 0.26, 0.30), bronze, at=(0.60, -0.22, 1.56), bevel=0.02),
        t.box("cheek_r", (0.05, 0.26, 0.30), bronze, at=(-0.60, -0.22, 1.56), bevel=0.02),
        # The pearl on the brow, in its setting.
        t.sphere("pearl", 0.07, pearl, at=(0, -0.66, 1.99), subdivisions=2, squash=(1, 0.7, 1.1)),
        t.sphere("setting", 0.085, deep, at=(0, -0.63, 1.99), subdivisions=2, squash=(1, 0.45, 1.1)),
    ]

    # The crown: branches of coral standing off the brow band all round, leaning out; the fin stands at the front.
    for i in range(9):
        if i == 4:
            continue

        bearing = math.radians(-90 + (i - 4) * 36)
        x, y = math.cos(bearing) * brow[0], a.HEAD[1] + math.sin(bearing) * brow[1]
        tall = 0.30 + 0.03 * abs(i - 4)
        tip = (x * 1.22, a.HEAD[1] + (y - a.HEAD[1]) * 1.22, 2.02 + tall)

        head.append(a.spike(f"coral_{i}", (x, y, 2.0), tip, 0.07, coral, sides=7))
        head.append(a.spike(f"twig_{i}", (x * 1.08, a.HEAD[1] + (y - a.HEAD[1]) * 1.08, 2.02 + tall * 0.45),
                            (x * 1.36, a.HEAD[1] + (y - a.HEAD[1]) * 1.36, 2.02 + tall * 0.8), 0.03, coral, sides=6))

    return a.finish("drowned_crown", {"head": head})
