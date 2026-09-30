"""Elmo di Ferro — a plain iron helm open at the face, a nasal down the middle and a rim round the edge."""

import armourkit as a
import temins as t


def build():
    iron = t.material("iron_plate", (0.55, 0.56, 0.60), metal=0.6, rough=0.4)
    dark = t.material("iron", metal=0.5, rough=0.55)

    brow = a.around(a.HEAD, a.HELM, 2.02)
    rim = a.around(a.HEAD, a.HELM, 1.44)

    head = [
        # The bowl down to the jaw at the sides and back, open over the face.
        a.shell("bowl", iron, centre=a.HEAD, radii=a.HELM, below=1.42, opening=(0.36, 1.25, 1.99), thickness=0.035),
        # A band round the brow, a rim round the edge, open where the face is.
        a.gap_band("brow", brow[0], brow[1], 1.98, 2.06, dark, gap=62, y=a.HEAD[1]),
        a.gap_band("rim", rim[0], rim[1], 1.41, 1.47, dark, gap=62, y=a.HEAD[1]),
        # The nasal, down from the brow.
        t.box("nasal", (0.07, 0.05, 0.34), dark, at=(0, -0.60, 1.86), bevel=0.012),
        # A ridge over the top.
        a.shell("ridge", dark, centre=a.HEAD, radii=(0.04, a.HELM[1] + 0.015, a.HELM[2] + 0.015), below=1.95, thickness=0.03, segments=6),
    ]

    return a.finish("iron_helm", {"head": head})
