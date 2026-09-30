"""Carapace Cinereo — plates of ash-dark steel split by seams of ember light, spiked pauldrons, a charcoal cape."""

import armourkit as a
import temins as t


def build():
    ash = t.material("ash_steel", (0.20, 0.19, 0.20), metal=0.5, rough=0.45)
    dark = t.material("ash_dark", (0.10, 0.09, 0.10), metal=0.4, rough=0.5)
    ember = t.material("ember", glow=1.4, rough=0.4)

    chest = [
        a.band("plate", 0.44, 0.41, 0.995, 1.29, ash, top=0.82, y=0.03),
        a.band("seam", 0.43, 0.40, 0.96, 1.0, ember, y=0.03, bevel=0),
        a.band("gorget", 0.32, 0.31, 1.25, 1.34, dark, y=0.05),
        # A crack of fire down the breast.
        t.box("crack", (0.035, 0.03, 0.24), ember, at=(0, -0.405, 1.12)),
        a.spike("collar_l", (0.14, 0.30, 1.30), (0.2, 0.38, 1.46), 0.045, dark),
        a.spike("collar_r", (-0.14, 0.30, 1.30), (-0.2, 0.38, 1.46), 0.045, dark),
    ]

    # Three plates down the belly with the fire showing between them.
    spine = [a.band("fire", 0.415, 0.385, 0.64, 0.975, ember, y=0.03, bevel=0)]
    spine += [a.band(f"plate_{i}", 0.432, 0.402, 0.87 - i * 0.11, 0.96 - i * 0.11, ash, y=0.03) for i in range(3)]
    spine += [
        a.cape("cape", t.material("charcoal", (0.13, 0.12, 0.12), rough=0.9)),
        a.cape("hem", ember, top=(0.36, 0.556, 0.487), bottom=(0.28, 0.572, 0.50), rows=1, thickness=0.03),
    ]

    hips = [
        a.band("belt", 0.425, 0.40, 0.575, 0.645, dark, y=0.03),
        t.sphere("clasp", 0.045, ember, at=(0, -0.39, 0.61), subdivisions=0, squash=(1, 0.6, 1)),
    ]
    for side in (-1, 1):
        hips.append(t.box(f"tasset_front_{side}", (0.16, 0.04, 0.2), ash, at=(side * 0.2, -0.36, 0.49), bevel=0.015))
        hips.append(a.spike(f"tip_front_{side}", (side * 0.2, -0.37, 0.40), (side * 0.2, -0.40, 0.33), 0.06, ash, sides=4))
        hips.append(t.box(f"tasset_side_{side}", (0.04, 0.2, 0.19), ash, at=(side * 0.41, 0.02, 0.49), bevel=0.015))

    left = [
        t.sphere("dome", 0.22, ash, at=(0.34, 0.0, 1.22), subdivisions=2, squash=(1.1, 0.95, 0.72)),
        t.sphere("rim", 0.225, ember, at=(0.34, 0.0, 1.18), subdivisions=2, squash=(1.1, 0.96, 0.3)),
        a.spike("spike_0", (0.30, 0.0, 1.34), (0.30, 0.0, 1.56), 0.06, dark),
        a.spike("spike_1", (0.42, 0.08, 1.30), (0.52, 0.14, 1.48), 0.05, dark),
        a.spike("spike_2", (0.42, -0.08, 1.30), (0.52, -0.14, 1.48), 0.05, dark),
    ]
    bracer_l = [
        a.sleeve("bracer", 0.50, 0.74, 0.15, 0.135, ash),
        a.sleeve("band", 0.70, 0.73, 0.14, 0.14, ember, bevel=0),
        a.spike("fin", (0.60, 0.0, 1.22), (0.52, 0.0, 1.34), 0.04, dark, sides=4),
    ]

    return a.finish("ashen_carapace", {
        "chest": chest, "spine": spine, "hips": hips,
        "upperarm.l": left, "upperarm.r": [a.mirror_x(p) for p in left],
        "lowerarm.l": bracer_l, "lowerarm.r": [a.mirror_x(p) for p in bracer_l],
    })
