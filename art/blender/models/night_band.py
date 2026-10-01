"""Fascia Notturna — a strip of dark green cloth tied round the brow, knotted behind the ear
with its ends left hanging (CLS-03)."""

import ninjakit as n
import temins as t


def build():
    green = t.material("ninja_green", (0.08, 0.26, 0.15), rough=0.85)
    return n.finish_head("night_band", n.headband(green, ends=green))
