"""Cuoio del Predatore — a short leather jerkin with straps crossed over it, one shoulder
padded, leather on the forearms and shins, throwing spikes in the belt (CLS-03)."""

import armourkit as a
import ninjakit as n
import temins as t


def build():
    leather = t.material("leather")
    dark = t.material("leather_dark")
    stitch = t.material("stitch", (0.80, 0.70, 0.50), rough=0.9)
    iron = t.material("iron", metal=0.5, rough=0.55)
    green = t.material("ninja_green", (0.08, 0.26, 0.15), rough=0.85)

    low, high = n.vest(leather, hem=stitch)
    chest = high + n.straps(dark)

    hips = n.sash(dark, tail=green)
    # Three spikes tucked in the sash at the other hip.
    hips += [a.spike(f"spike_{i}", (-0.30 - 0.035 * i, -0.27 + 0.03 * i, 0.60), (-0.31 - 0.04 * i, -0.30 + 0.03 * i, 0.80), 0.018, iron)
             for i in range(3)]

    arm_l, arm_r = n.sides(n.bracer(leather, dark))
    leg_l, leg_r = n.sides(n.shin(leather, trim=dark))

    return n.finish("prowler_leathers", {
        "spine": low, "chest": chest, "hips": hips,
        "upperarm.l": n.pad(leather, rim=stitch),
        "lowerarm.l": arm_l, "lowerarm.r": arm_r,
        "lowerleg.l": leg_l, "lowerleg.r": leg_r,
    })
