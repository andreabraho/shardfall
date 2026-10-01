"""Frontale di Ferro — a black headband with an iron plate riveted over the forehead, a
ridge down its middle (CLS-03)."""

import armourkit as a
import ninjakit as n
import temins as t


def build():
    cloth = t.material("char", (0.10, 0.09, 0.09), rough=0.85)
    iron = t.material("brow_iron", (0.42, 0.44, 0.48), metal=0.6, rough=0.4)
    dark = t.material("iron", metal=0.5, rough=0.55)

    rx, ry, y = n.BROW
    parts = n.headband(cloth, ends=cloth, lengths=(0.28, 0.20))
    parts.append(n.brow_plate(iron))
    parts.append(a.rod("ridge", (0, y - ry - 0.03, 1.83), (0, y - ry - 0.03, 1.965), 0.018, dark, sides=6))
    for side in (-1, 1):
        parts.append(t.sphere(f"rivet_{side}", 0.022, dark, at=(side * 0.20, y - ry * 0.93 - 0.035, 1.89), subdivisions=1))
    return n.finish_head("iron_brow", parts)
