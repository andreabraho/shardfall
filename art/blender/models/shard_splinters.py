"""Schegge di Frammento — splinters broken from a shard stone, jagged and glowing violet,
set in rough stone and bound with cord to be held at all (CLS-03)."""

import swordkit as k
import temins as t


def build():
    crystal = t.material("shard_crystal", (0.50, 0.30, 0.90), glow=1.0, rough=0.2, metal=0.2)
    core = t.material("shard_core", (0.24, 0.16, 0.42), rough=0.3, metal=0.4)
    stone = t.material("stone_dark", rough=0.9)
    cord = t.material("leather_dark")

    # Broken, not ground: steps and a notch down one side, a clean cleavage down the other.
    outline = [(-0.08, 0.0), (0.09, 0.0), (0.11, 0.18), (0.07, 0.24), (0.10, 0.40), (0.05, 0.47), (0.06, 0.62),
               (0.0, 0.80), (-0.045, 0.60), (-0.07, 0.36), (-0.06, 0.20)]
    parts = [
        *k.glowing_edge(outline, 0.08, core, crystal, inset=0.6),
        t.rock("setting", 0.085, stone, at=(0, 0, 0.0), seed=4, squash=(1.5, 0.9, 0.55)),
        *k.grip(0.26, 0.04, cord, stone, wraps=3),
        t.rock("pommel", 0.05, stone, at=(0, 0, -0.29), seed=9),
    ]
    return k.finish("shard_splinters", parts)
