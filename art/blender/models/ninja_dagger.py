"""Pugnale del Ninja — a short single-edged blade with a round guard and a cord-bound grip,
held one in each hand, point down along the forearm or forward (the ninja prototype, 2026-09-30)."""

import swordkit as k
import temins as t


def build():
    steel = t.material("dagger_blade", (0.66, 0.67, 0.7), metal=0.6, rough=0.38)
    dark = t.material("steel_dark", metal=0.5, rough=0.5)
    wrap = t.material("dagger_cord", (0.08, 0.07, 0.07), rough=0.8)
    grip = t.material("dagger_grip", (0.45, 0.08, 0.07), rough=0.7)

    # One edge, straight back, the point swept up to the back like a tanto's.
    outline = [(-0.07, 0.0), (0.075, 0.0), (0.08, 0.36), (0.06, 0.5), (-0.07, 0.58), (-0.075, 0.3)]
    parts = [
        k.blade(outline, 0.07, steel, edge=0.03),
        k.fuller(0.06, 0.4, 0.014, 0.074, dark),
        t.cylinder("tsuba", 0.1, 0.025, dark, at=(0, 0, 0.0), sides=20, bevel=0.006),
        *k.grip(0.26, 0.04, grip, wrap, wraps=5),
        *k.pommel("ball", 0.045, dark, at=-0.27),
    ]
    return k.finish("ninja_dagger", parts)
