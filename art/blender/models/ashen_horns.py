"""Corna di Cenere — an ash-grey brow plate cracked by a glowing seam, an ember set in it and
two short charred horns rising from its corners; a burnt band holds it on (CLS-03)."""

import armourkit as a
import ninjakit as n
import temins as t


def build():
    ash = t.material("ash_plate", (0.30, 0.29, 0.29), metal=0.5, rough=0.55)
    char = t.material("char", (0.10, 0.09, 0.09), rough=0.8)
    ember = t.material("ember", glow=1.2, rough=0.4)

    rx, ry, y = n.BROW
    front = y - ry - 0.03
    parts = n.headband(char, ends=char, lengths=(0.30, 0.22))
    parts += [
        n.brow_plate(ash, z0=1.82, z1=1.98, width=76),
        t.sphere("ember", 0.04, ember, at=(0, front - 0.005, 1.90), subdivisions=1, squash=(1, 0.6, 1.3)),
        a.rod("seam", (0.05, front, 1.83), (0.16, front + 0.03, 1.97), 0.01, ember, sides=5),
    ]
    # The horns: each in two pieces, out and then up, so it curves.
    for side in (-1, 1):
        root, knee, tip = (side * 0.25, front + 0.06, 1.95), (side * 0.36, front + 0.07, 2.08), (side * 0.37, front + 0.03, 2.27)
        parts.append(a.rod(f"horn_low_{side}", root, knee, 0.05, char, sides=8))
        parts.append(a.spike(f"horn_{side}", knee, tip, 0.05, char, sides=8))
        parts.append(t.sphere(f"horn_knee_{side}", 0.05, char, at=knee, subdivisions=1))
    return n.finish_head("ashen_horns", parts)
