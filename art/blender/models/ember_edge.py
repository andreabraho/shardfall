"""Filo di Brace — a blackened blade whose edges glow like coals, a guard of flames."""

import swordkit as k
import temins as t


def build():
    black = t.material("blackened", (0.20, 0.17, 0.16), metal=0.5, rough=0.45)
    ember = t.material("ember", glow=1.2, rough=0.4)
    iron = t.material("iron", metal=0.5, rough=0.55)
    cloth = t.material("cloth_red")

    outline = k.mirrored([(0.12, 0.0), (0.13, 0.7), (0.12, 1.05), (0.07, 1.30), (0.0, 1.44)])

    # Flames licking up either side of the blade's foot.
    flames = t.profile_solid("flames", [
        (-0.26, 0.10), (-0.20, 0.02), (-0.12, 0.08), (-0.14, -0.02), (0.14, -0.02), (0.12, 0.08),
        (0.20, 0.02), (0.26, 0.10), (0.22, -0.05), (0.10, -0.07), (-0.10, -0.07), (-0.22, -0.05),
    ], 0.1, iron, bevel=0.01)

    parts = [
        *k.glowing_edge(outline, 0.09, black, ember),
        k.stand(flames, z=0.02),
        *k.grip(0.30, 0.043, cloth, iron, wraps=4),
        *k.pommel("cup", 0.07, iron, at=-0.36, gem=ember),
    ]
    return k.finish("ember_edge", parts)
