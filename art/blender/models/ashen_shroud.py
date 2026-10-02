"""Sudario di Cenere — plates the colour of ash, cracked along seams that still glow, spines
on the shoulders, and a burnt mantle hanging from them (CLS-03)."""

import armourkit as a
import ninjakit as n
import temins as t


def build():
    ash = t.material("ash_plate", (0.30, 0.29, 0.29), metal=0.5, rough=0.55)
    char = t.material("char", (0.10, 0.09, 0.09), rough=0.8)
    ember = t.material("ember", glow=1.1, rough=0.4)
    shroud = t.material("burnt_cloth", (0.13, 0.11, 0.11), rough=0.95)

    low, high = n.vest(ash, grow=0.004)
    # The seams: a line round the foot of the plate, one under the chest, one down the breast.
    low.append(a.band("seam_low", n.RX + 0.009, n.RY + 0.009, 0.90, 0.915, ember, y=0.02, bevel=0))
    high.append(a.band("seam_high", n.RX * 0.975 + 0.008, n.RY * 0.975 + 0.008, 1.055, 1.07, ember, y=0.02, bevel=0))
    high.append(a.rod("seam_breast", (0.07, -n.RY * 0.99, 0.99), (-0.05, -n.RY * 0.93, 1.19), 0.012, ember))
    high += n.collar(char, rx=0.29, ry=0.27, z0=1.14, z1=1.25)

    spine = low + [a.cape("mantle", shroud, top=(1.20, 0.385, 0.34), bottom=(0.34, 0.60, 0.46), curve=0.13)]

    hips = n.sash(char, tail=shroud, lengths=(0.36, 0.30)) + n.tassets(ash, trim=ember, drop=0.20)

    shoulder = n.pad(ash, rim=char, size=0.20) + [
        a.spike(f"spine_{i}", (0.40 + 0.07 * i, 0.05 - 0.05 * i, 1.26 - 0.03 * i), (0.50 + 0.13 * i, 0.08 - 0.08 * i, 1.44 - 0.07 * i), 0.04, char)
        for i in range(3)
    ]
    shoulder_l, shoulder_r = n.sides(shoulder)
    arm_l, arm_r = n.sides(n.bracer(ash, char, plate=ash) + [a.sleeve("arm_seam", 0.62, 0.635, 0.153, 0.153, ember, bevel=0)])
    leg_l, leg_r = n.sides(n.shin(char, trim=ember, plate=ash))

    return n.finish("ashen_shroud", {
        "spine": spine, "chest": high, "hips": hips,
        "upperarm.l": shoulder_l, "upperarm.r": shoulder_r,
        "lowerarm.l": arm_l, "lowerarm.r": arm_r,
        "lowerleg.l": leg_l, "lowerleg.r": leg_r,
    })
