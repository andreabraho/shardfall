"""Pugnali di Ferro — a soldier's straight dagger: two edges, a short bar guard, a groove
down the flat and a disc pommel (CLS-03)."""

import swordkit as k
import temins as t


def build():
    steel = t.material("steel", metal=0.6, rough=0.36)
    iron = t.material("iron", metal=0.5, rough=0.55)
    leather = t.material("leather_dark")

    outline = k.mirrored([(0.07, 0.0), (0.068, 0.36), (0.045, 0.54), (0.0, 0.66)])
    parts = [
        k.blade(outline, 0.065, steel),
        k.fuller(0.04, 0.40, 0.013, 0.07, iron),
        k.crossguard(0.15, 0.045, 0.075, iron, droop=0.0),
        *k.grip(0.25, 0.039, leather, iron, wraps=4),
        *k.pommel("disc", 0.052, iron, at=-0.28),
    ]
    return k.finish("iron_daggers", parts)
