"""Corona del Guardiano — a blued open helm ringed by a gold crown of points, an ice-blue stone at the brow."""

import math

import armourkit as a
import temins as t


def build():
    steel = t.material("warden_steel", (0.55, 0.66, 0.86), metal=0.7, rough=0.3)
    gold = t.material("gold", metal=0.8, rough=0.3)
    ice = t.material("ice", (0.55, 0.85, 1.0), glow=1.0, rough=0.2)

    crown = a.around(a.HEAD, a.HELM, 2.02, grow=0.025)
    cheek = a.around(a.HEAD, a.HELM, 1.5)

    head = [
        a.shell("bowl", steel, centre=a.HEAD, radii=a.HELM, below=1.40, opening=(0.36, 1.25, 1.99), thickness=0.035),
        # Cheek guards hanging to the jaw, open over the face.
        a.gap_band("cheeks", cheek[0], cheek[1], 1.40, 1.56, steel, gap=62, y=a.HEAD[1], top=1.05),
        # The crown: a gold circlet round the brow with points standing up from it.
        a.band("circlet", crown[0], crown[1], 1.98, 2.07, gold, sides=40, y=a.HEAD[1]),
        t.sphere("stone", 0.06, ice, at=(0, a.HEAD[1] - crown[1] - 0.02, 2.03), subdivisions=0, squash=(1, 0.6, 1.3)),
    ]

    for i in range(9):
        angle = math.radians(-120 + i * 30)
        x, y = crown[0] * math.sin(angle), a.HEAD[1] - crown[1] * math.cos(angle)
        tall = 0.2 if i == 4 else 0.14
        head.append(a.spike(f"point_{i}", (x, y, 2.05), (x * 1.04, y * 1.04, 2.05 + tall), 0.045, gold, sides=4))

    return a.finish("warden_crown", {"head": head})
