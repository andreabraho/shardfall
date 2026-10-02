"""Zanne di Brace — blackened curved blades whose edges glow like coals, flames licking up
from the guard (CLS-03)."""

import swordkit as k
import temins as t


def build():
    black = t.material("blackened", (0.20, 0.17, 0.16), metal=0.5, rough=0.45)
    ember = t.material("ember", glow=1.2, rough=0.4)
    iron = t.material("iron", metal=0.5, rough=0.55)
    cloth = t.material("cloth_red")

    # A sabre's curve in a hand's length: both sides lean the same way.
    outline = [(-0.06, 0.0), (0.075, 0.0), (0.10, 0.20), (0.135, 0.40), (0.19, 0.58), (0.26, 0.72),
               (0.15, 0.62), (0.06, 0.46), (0.0, 0.28), (-0.045, 0.12)]
    flames = t.profile_solid("flames", [
        (-0.17, 0.08), (-0.13, 0.02), (-0.08, 0.06), (-0.09, -0.015), (0.09, -0.015), (0.08, 0.06),
        (0.13, 0.02), (0.17, 0.08), (0.145, -0.04), (0.07, -0.055), (-0.07, -0.055), (-0.145, -0.04),
    ], 0.08, iron, bevel=0.012, segments=3)
    parts = [
        *k.glowing_edge(outline, 0.07, black, ember, inset=0.8),
        k.stand(flames, z=0.015),
        *k.grip(0.26, 0.04, cloth, iron, wraps=4),
        *k.pommel("cup", 0.055, iron, at=-0.30, gem=ember),
    ]
    return k.finish("ember_fangs", parts)
