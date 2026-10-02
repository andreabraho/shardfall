"""Sussurri della Tomba — needles of pale grave steel with a violet line down the flat, a
guard like two finger bones and a skull-pale knob (CLS-03)."""

import swordkit as k
import temins as t


def build():
    pale = t.material("grave_steel", (0.62, 0.66, 0.64), metal=0.55, rough=0.35)
    bone = t.material("bone", rough=0.7)
    violet = t.material("violet", glow=1.1, rough=0.3)
    wrap = t.material("shroud", (0.16, 0.14, 0.20), rough=0.9)

    outline = k.mirrored([(0.045, 0.0), (0.075, 0.07), (0.05, 0.14), (0.04, 0.50), (0.0, 0.80)])
    parts = [
        k.blade(outline, 0.06, pale),
        k.fuller(0.10, 0.62, 0.011, 0.068, violet),
        k.crossguard(0.16, 0.038, 0.07, bone, droop=-0.03, flare=0.3),
        *k.grip(0.26, 0.037, wrap, bone, wraps=3),
        *k.pommel("cup", 0.055, bone, at=-0.30, gem=violet),
    ]
    return k.finish("grave_whispers", parts)
