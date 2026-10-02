"""Artigli Cavi — claws of black chitin: a blade forked into two hooked talons, venom in the
cleft between them, a spur under the hand (CLS-03)."""

import swordkit as k
import temins as t


def build():
    chitin = t.material("chitin", rough=0.4, metal=0.3)
    venom = t.material("venom", (0.35, 0.85, 0.12), glow=1.0, rough=0.3)
    iron = t.material("iron", metal=0.5, rough=0.55)
    wrap = t.material("leather_dark")

    # Two talons from one foot, the outer longer, both hooking the same way.
    outline = [(-0.08, 0.0), (0.085, 0.0), (0.12, 0.22), (0.17, 0.44), (0.25, 0.66), (0.20, 0.74),
               (0.10, 0.56), (0.05, 0.36), (0.02, 0.36), (0.03, 0.50), (0.075, 0.62), (0.03, 0.66),
               (-0.045, 0.50), (-0.08, 0.30)]
    cleft = t.profile_solid("cleft", [(0.0, 0.10), (0.05, 0.10), (0.045, 0.34), (0.02, 0.38), (0.0, 0.30)], 0.085, venom)
    spur = t.profile_solid("spur", [(-0.05, -0.27), (0.05, -0.27), (0.03, -0.34), (-0.07, -0.42), (-0.02, -0.33)], 0.05, chitin, edge=0.02)
    parts = [
        k.blade(outline, 0.075, chitin, edge=0.025),
        k.stand(cleft),
        k.crossguard(0.13, 0.045, 0.08, iron, droop=0.03, flare=0.3),
        *k.grip(0.26, 0.04, wrap, iron, wraps=5),
        k.stand(spur),
    ]
    return k.finish("hollow_talons", parts)
