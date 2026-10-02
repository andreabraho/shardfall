"""Zanne del Cacciatore — skinning knives: a curved single edge like a canine tooth, a bone
grip bound in sinew, an antler tip for a pommel (CLS-03)."""

import swordkit as k
import temins as t


def build():
    steel = t.material("steel", metal=0.55, rough=0.42)
    bone = t.material("bone", rough=0.7)
    sinew = t.material("leather")
    horn = t.material("antler", (0.55, 0.45, 0.32), rough=0.75)

    # A fang: the edge bellies out, the back curves in to a hooked point.
    outline = [(-0.05, 0.0), (0.07, 0.0), (0.10, 0.18), (0.105, 0.34), (0.07, 0.48), (-0.02, 0.60),
               (-0.035, 0.46), (-0.02, 0.32), (-0.035, 0.16)]
    parts = [
        k.blade(outline, 0.06, steel, edge=0.025),
        k.crossguard(0.085, 0.04, 0.07, horn, droop=0.01),
        *k.grip(0.25, 0.04, bone, sinew, wraps=3),
        t.cylinder("pommel", 0.03, 0.09, horn, at=(0, 0, -0.29), sides=12, radius_top=0.045),
    ]
    return k.finish("hunters_fangs", parts)
