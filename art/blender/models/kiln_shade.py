"""Ombra della Fornace — the relic: black lacquer edged in gold, the kiln's burning eye on the
breast, shoulder guards that rise like flames, and a long scarf whose ends still glow (CLS-03)."""

import armourkit as a
import ninjakit as n
import temins as t


def build():
    black = t.material("lacquer", (0.05, 0.05, 0.06), metal=0.6, rough=0.25)
    gold = t.material("kiln_gold", (0.95, 0.66, 0.20), metal=0.8, rough=0.28)
    ember = t.material("ember", glow=1.5, rough=0.35)
    red = t.material("kiln_red", (0.45, 0.07, 0.05), rough=0.8)

    low, high = n.vest(black, hem=gold, grow=0.006)
    low.append(a.band("waist_line", n.RX + 0.012, n.RY + 0.012, 0.94, 0.955, gold, y=0.02, bevel=0))
    high.append(a.band("gorget", n.RX * 0.91, n.RY * 0.91, 1.18, 1.225, gold, y=0.02, bevel=0.008))
    # The eye: a gold ring on the breast with the fire in it.
    high.append(t.cylinder("eye_ring", 0.10, 0.03, gold, at=(0, -n.RY * 0.96 - 0.004, 1.09), sides=24, axis="Y", bevel=0.008))
    high.append(t.cylinder("eye", 0.065, 0.045, ember, at=(0, -n.RY * 0.96 - 0.006, 1.09), sides=20, axis="Y", bevel=0.006))
    high += n.collar(red)
    high += n.tails(red, length=0.80, width=0.12, spread=0.16, tip=ember)

    hips = n.sash(red, tail=gold, lengths=(0.36, 0.28)) + n.tassets(black, trim=gold, drop=0.20)

    # Flames: three tongues of gold rising from a black guard.
    shoulder = n.pad(black, rim=gold, size=0.20) + [
        a.spike(f"flame_{i}", (0.38 + 0.075 * i, 0.0, 1.25 - 0.03 * i), (0.48 + 0.14 * i, 0.03 * (i - 1), 1.47 - 0.08 * i), 0.055, gold, sides=6)
        for i in range(3)
    ]
    shoulder_l, shoulder_r = n.sides(shoulder)
    arm_l, arm_r = n.sides(n.bracer(black, gold, plate=black) + [a.sleeve("arm_fire", 0.62, 0.636, 0.154, 0.154, ember, bevel=0)])
    leg_l, leg_r = n.sides(n.shin(black, trim=gold, plate=black))

    return n.finish("kiln_shade", {
        "spine": low, "chest": high, "hips": hips,
        "upperarm.l": shoulder_l, "upperarm.r": shoulder_r,
        "lowerarm.l": arm_l, "lowerarm.r": arm_r,
        "lowerleg.l": leg_l, "lowerleg.r": leg_r,
    })
