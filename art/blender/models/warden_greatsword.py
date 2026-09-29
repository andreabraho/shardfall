"""Spadone del Guardiano — the mountain gate's knightly great sword: blued steel, drooping quillons, an ice-blue stone."""

import swordkit as k
import temins as t


def build():
    steel = t.material("blue_steel", (0.62, 0.70, 0.82), metal=0.7, rough=0.3)
    groove = t.material("blue_groove", (0.22, 0.30, 0.45), metal=0.5, rough=0.4)
    iron = t.material("steel_dark", metal=0.6, rough=0.4)
    cloth = t.material("cloth_blue", (0.20, 0.28, 0.50), rough=0.8)
    ice = t.material("ice", (0.55, 0.85, 1.0), glow=1.2, rough=0.2)

    parts = [
        k.blade(k.mirrored([(0.14, 0.0), (0.145, 1.40), (0.12, 1.72), (0.0, 1.98)]), 0.11, steel),
        k.fuller(0.12, 1.45, 0.03, 0.114, groove),
        k.crossguard(0.42, 0.07, 0.11, iron, droop=0.10, flare=0.6, at=0.03),
        t.sphere("stone", 0.05, ice, at=(0, 0, 0.04), subdivisions=0, squash=(1, 1.4, 1.2)),
        *k.grip(0.40, 0.045, cloth, iron, wraps=5),
        *k.pommel("cup", 0.075, iron, at=-0.45, gem=ice),
    ]
    return k.finish("warden_greatsword", parts)
