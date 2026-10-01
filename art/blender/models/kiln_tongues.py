"""Lingue della Fornace — the relic pair: blades that wave like flame, gold with a burning
edge, a sun-disc guard and an ember held in the pommel (CLS-03)."""

import swordkit as k
import temins as t


def build():
    gold = t.material("kiln_gold", (0.95, 0.66, 0.20), metal=0.8, rough=0.28)
    ember = t.material("ember", glow=1.5, rough=0.35)
    dark = t.material("blackened", (0.20, 0.17, 0.16), metal=0.5, rough=0.45)
    cloth = t.material("cloth_red")

    # A flame: three waves, each side out where the other is in.
    outline = [(-0.07, 0.0), (0.07, 0.0), (0.105, 0.12), (0.06, 0.26), (0.10, 0.40), (0.05, 0.54), (0.075, 0.66),
               (0.0, 0.84), (-0.03, 0.66), (-0.075, 0.54), (-0.03, 0.40), (-0.085, 0.26), (-0.045, 0.12)]
    parts = [
        *k.glowing_edge(outline, 0.075, gold, ember, inset=0.7),
        k.fuller(0.06, 0.50, 0.012, 0.085, dark),
        t.cylinder("sun", 0.115, 0.03, gold, at=(0, 0, 0.0), sides=24, bevel=0.008),
        t.cylinder("sun_core", 0.075, 0.045, dark, at=(0, 0, 0.0), sides=24, bevel=0.006),
        *k.grip(0.26, 0.04, cloth, gold, wraps=5),
        *k.pommel("cup", 0.06, gold, at=-0.30, gem=ember),
    ]
    return k.finish("kiln_tongues", parts)
