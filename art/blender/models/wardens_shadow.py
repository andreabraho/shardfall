"""Ombra del Custode — the wardens' own night harness: a dark steel cuirass edged in gold with
their star on the chest, shoulder guards in two layers, a blue sash and a short blue cape of
its own (CLS-03)."""

import math

import armourkit as a
import ninjakit as n
import temins as t


def build():
    steel = t.material("shadow_steel", (0.13, 0.15, 0.20), metal=0.7, rough=0.35)
    gold = t.material("gold", metal=0.75, rough=0.35)
    blue = t.material("warden_blue", (0.10, 0.17, 0.38), rough=0.8)
    dark = t.material("leather_dark")

    low, high = n.vest(steel, hem=gold, grow=0.005)
    high.append(a.band("gorget", n.RX * 0.91, n.RY * 0.91, 1.18, 1.225, gold, y=0.02, bevel=0.008))
    # A four-pointed star on the breast.
    for i in range(4):
        angle = math.radians(90 * i)
        centre = (0, -n.RY * 0.96, 1.09)
        tip = (0.075 * math.sin(angle), -n.RY * 0.96 - 0.004, 1.09 + 0.075 * math.cos(angle))
        high.append(a.spike(f"star_{i}", centre, tip, 0.03, gold, sides=4))
    high += n.collar(blue)

    spine = low + [a.cape("cape", blue, top=(1.19, 0.385, 0.30), bottom=(0.50, 0.56, 0.40), curve=0.10)]

    hips = n.sash(blue, tail=gold, lengths=(0.34, 0.26)) + n.tassets(steel, trim=gold, drop=0.19)

    shoulder = n.pad(steel, rim=gold, size=0.20) + [
        t.sphere("pad_low", 0.17, steel, at=(0.49, 0.0, 1.15), subdivisions=2, squash=(1.0, 0.95, 0.55)),
        t.sphere("pad_low_rim", 0.175, gold, at=(0.49, 0.0, 1.13), subdivisions=2, squash=(1.0, 0.95, 0.33)),
    ]
    shoulder_l, shoulder_r = n.sides(shoulder)
    arm_l, arm_r = n.sides(n.bracer(steel, gold, plate=steel))
    leg_l, leg_r = n.sides(n.shin(dark, trim=gold, plate=steel))

    return n.finish("wardens_shadow", {
        "spine": spine, "chest": high, "hips": hips,
        "upperarm.l": shoulder_l, "upperarm.r": shoulder_r,
        "lowerarm.l": arm_l, "lowerarm.r": arm_r,
        "lowerleg.l": leg_l, "lowerleg.r": leg_r,
    })
