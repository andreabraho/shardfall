"""Visiera Cinerea — a closed helm of ash-dark steel, a visor over the face with a slit of ember light, horns sweeping back."""

import armourkit as a
import temins as t


def build():
    ash = t.material("ash_steel", (0.20, 0.19, 0.20), metal=0.5, rough=0.45)
    dark = t.material("ash_dark", (0.10, 0.09, 0.10), metal=0.4, rough=0.5)
    ember = t.material("ember", glow=1.4, rough=0.4)
    horn = t.material("horn", (0.20, 0.15, 0.12), rough=0.6)

    radii = (a.HELM[0], a.HELM[1] + 0.02, a.HELM[2])
    brow = a.around(a.HEAD, radii, 2.0, grow=0.02)
    rim = a.around(a.HEAD, radii, 1.36)

    head = [
        # A closed helm: the shell all round, down to the jaw.
        a.shell("bowl", ash, centre=a.HEAD, radii=radii, below=1.34, thickness=0.035),
        # The visor, standing proud of the face, and the glowing slit and breaths across it.
        a.shell("visor", dark, centre=(0, -0.06, 1.72), radii=(0.58, 0.63, 0.52), below=1.34, thickness=0.03),
        a.gap_band("slit", *a.around((0, -0.06, 1.72), (0.58, 0.63, 0.52), 1.86, grow=0.012), 1.835, 1.885, ember,
                   y=-0.06, front=70, sides=72),
        t.box("breath", (0.04, 0.04, 0.22), ember, at=(0, -0.67, 1.55), bevel=0.01),
        # A heavy brow ridge and a rim round the jaw break the dome.
        a.band("brow", brow[0], brow[1], 1.97, 2.06, dark, sides=40, y=a.HEAD[1]),
        a.band("rim", rim[0], rim[1], 1.33, 1.39, dark, sides=40, y=a.HEAD[1]),
        # A crest of ember down the middle, and horns sweeping back from the temples.
        a.shell("crest", ember, centre=a.HEAD, radii=(0.03, radii[1] + 0.015, radii[2] + 0.015), below=2.0, thickness=0.03, segments=6),
        a.spike("horn_l", (0.52, 0.0, 2.0), (0.80, 0.35, 2.3), 0.09, horn, sides=10),
        a.spike("horn_r", (-0.52, 0.0, 2.0), (-0.80, 0.35, 2.3), 0.09, horn, sides=10),
    ]

    return a.finish("ashen_visor", {"head": head})
