"""Spada di Ferro — a plain arming sword of grey iron, a groove down the blade."""

import swordkit as k
import temins as t


def build():
    steel = t.material("iron_blade", (0.62, 0.63, 0.66), metal=0.55, rough=0.42)
    groove = t.material("steel_dark", metal=0.5, rough=0.5)
    iron = t.material("iron", metal=0.5, rough=0.55)
    leather = t.material("leather")
    cord = t.material("leather_dark")

    parts = [
        k.blade(k.mirrored([(0.115, 0.0), (0.12, 0.86), (0.10, 1.18), (0.0, 1.40)]), 0.09, steel),
        k.fuller(0.1, 0.92, 0.024, 0.094, groove),
        k.crossguard(0.24, 0.06, 0.09, iron, droop=0.03, at=0.03),
        *k.grip(0.30, 0.042, leather, cord, wraps=4),
        *k.pommel("disc", 0.062, iron, at=-0.34),
    ]
    return k.finish("iron_sword", parts)
