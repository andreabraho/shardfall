"""Veste del Battitore — the ridge scouts' harness: lames of blue-grey steel over the chest, a
grey fur collar, rimmed shoulder guards, splinted arms and shins, a moss sash with a scarf's
ends down the back (CLS-03)."""

import armourkit as a
import ninjakit as n
import temins as t


def build():
    steel = t.material("ridge_steel", (0.20, 0.26, 0.33), metal=0.6, rough=0.4)
    edge = t.material("steel_dark", metal=0.55, rough=0.45)
    fur = t.material("grey_fur", (0.34, 0.33, 0.32), rough=0.95)
    moss = t.material("moss", (0.20, 0.32, 0.14), rough=0.85)
    dark = t.material("leather_dark")

    # Three lames under the chest, each lapping the one below, and a plate over it.
    low = [a.band(f"lame_{i}", n.RX + 0.012 - 0.006 * i, n.RY + 0.012 - 0.006 * i, 0.84 + i * 0.05, 0.905 + i * 0.05, steel, y=0.02, bevel=0.01)
           for i in range(3)]
    high = [
        a.band("plate", n.RX, n.RY, 0.985, 1.19, steel, top=0.90, y=0.02),
        a.band("plate_foot", n.RX + 0.008, n.RY + 0.008, 0.985, 1.01, edge, y=0.02, bevel=0.006),
        a.rod("rib", (0, -n.RY - 0.005, 1.0), (0, -n.RY * 0.91, 1.19), 0.02, edge),
    ]
    high += n.collar(fur, rx=0.30, ry=0.28, z0=1.14, z1=1.26)
    high += n.tails(moss, length=0.50)

    hips = n.sash(moss) + n.tassets(steel, trim=edge)

    shoulder_l, shoulder_r = n.sides(n.pad(steel, rim=edge))
    arm_l, arm_r = n.sides(n.bracer(dark, edge, plate=steel))
    leg_l, leg_r = n.sides(n.shin(dark, trim=edge, plate=steel))

    return n.finish("ridge_stalker", {
        "spine": low, "chest": high, "hips": hips,
        "upperarm.l": shoulder_l, "upperarm.r": shoulder_r,
        "lowerarm.l": arm_l, "lowerarm.r": arm_r,
        "lowerleg.l": leg_l, "lowerleg.r": leg_r,
    })
