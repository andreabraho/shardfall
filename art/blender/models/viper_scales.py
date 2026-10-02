"""Scaglie di Vipera — a jerkin of green scales edged in bronze, scale flaps over the hips,
bronze-bound guards on the arms and shins, an ochre sash (CLS-03)."""

import armourkit as a
import ninjakit as n
import temins as t


def build():
    scale = t.material("viper_scale", (0.05, 0.17, 0.09), metal=0.45, rough=0.45)
    pale = t.material("viper_pale", (0.09, 0.25, 0.11), metal=0.45, rough=0.45)
    bronze = t.material("bronze", metal=0.65, rough=0.42)
    dark = t.material("leather_dark")
    ochre = t.material("ochre", (0.62, 0.42, 0.12), rough=0.85)

    low, high = n.vest(dark, hem=bronze)
    for row in range(2):
        low += a.scales(f"low_{row}", n.RX, n.RY, 0.875 + row * 0.07, scale, 18, size=(0.15, 0.02, 0.10), y=0.02, offset=row * 0.5, alt=pale)
    for row in range(3):
        k = 1 - 0.028 * row
        high += a.scales(f"high_{row}", n.RX * k, n.RY * k, 1.02 + row * 0.065, scale, 16, size=(0.155, 0.02, 0.10), y=0.02, offset=row * 0.5, alt=pale)
    high.append(a.band("neck", n.RX * 0.91, n.RY * 0.91, 1.18, 1.225, bronze, y=0.02, bevel=0.006))

    hips = n.sash(ochre) + n.tassets(scale, trim=bronze)

    shoulder_l, shoulder_r = n.sides(n.pad(scale, rim=bronze, size=0.17))
    arm_l, arm_r = n.sides(n.bracer(scale, bronze))
    leg_l, leg_r = n.sides(n.shin(dark, trim=bronze, plate=scale))

    return n.finish("viper_scales", {
        "spine": low, "chest": high, "hips": hips,
        "upperarm.l": shoulder_l, "upperarm.r": shoulder_r,
        "lowerarm.l": arm_l, "lowerarm.r": arm_r,
        "lowerleg.l": leg_l, "lowerleg.r": leg_r,
    })
