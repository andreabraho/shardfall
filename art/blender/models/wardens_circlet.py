"""Diadema del Custode — a circlet of dark steel edged in gold, the wardens' star over the
brow and a small swept wing at each temple (CLS-03)."""

import math

import armourkit as a
import ninjakit as n
import temins as t


def build():
    steel = t.material("shadow_steel", (0.13, 0.15, 0.20), metal=0.7, rough=0.35)
    gold = t.material("gold", metal=0.75, rough=0.35)

    rx, ry, y = n.BROW
    parts = n.headband(steel, z0=1.86, z1=1.93)
    parts += [
        a.band("rim_low", rx + 0.008, ry + 0.008, 1.853, 1.87, gold, sides=40, y=y, bevel=0.004),
        a.band("rim_high", rx + 0.008, ry + 0.008, 1.92, 1.937, gold, sides=40, y=y, bevel=0.004),
        n.brow_plate(steel, z0=1.84, z1=1.99, width=40),
    ]
    # The star, standing a little over the plate.
    front = y - ry - 0.035
    for i in range(4):
        angle = math.radians(90 * i)
        parts.append(a.spike(f"star_{i}", (0, front, 1.915), (0.065 * math.sin(angle), front - 0.004, 1.915 + 0.065 * math.cos(angle)), 0.026, gold, sides=4))
    # The wings: three feathers at each temple, swept up and back.
    for side in (-1, 1):
        for i in range(3):
            base = (side * (rx - 0.01), 0.02 + 0.05 * i, 1.90)
            tip = (side * (rx + 0.07 + 0.03 * i), 0.20 + 0.10 * i, 2.06 + 0.07 * i)
            parts.append(a.spike(f"wing_{side}_{i}", base, tip, 0.034, gold, sides=4))
    return n.finish_head("wardens_circlet", parts)
