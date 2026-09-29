"""Spadone di Bronzo — an old leaf-shaped great sword cast all in bronze."""

import swordkit as k
import temins as t


def build():
    bronze = t.material("bronze_blade", (0.74, 0.48, 0.22), metal=0.7, rough=0.35)
    dark = t.material("bronze", metal=0.6, rough=0.45)
    leather = t.material("leather")

    parts = [
        k.blade(k.mirrored([(0.12, 0.0), (0.11, 0.45), (0.19, 1.12), (0.16, 1.55), (0.0, 1.95)]), 0.11, bronze),
        k.fuller(0.05, 1.35, 0.02, 0.114, dark),
        k.crossguard(0.30, 0.08, 0.11, dark, droop=-0.03, at=0.03),
        *k.grip(0.40, 0.045, leather, dark, wraps=4),
        *k.pommel("disc", 0.08, dark, at=-0.44),
    ]
    return k.finish("bronze_greatsword", parts)
