"""Egida della Fornace — the relic: dark steel and gold, a furnace burning in the breastplate, a crimson cape."""

import armourkit as a
import temins as t


def build():
    steel = t.material("aegis_steel", (0.26, 0.24, 0.25), metal=0.6, rough=0.35)
    gold = t.material("gold", metal=0.8, rough=0.3)
    ember = t.material("ember", glow=1.4, rough=0.4)
    crimson = t.material("crimson", (0.50, 0.07, 0.06), rough=0.8)

    chest = [
        a.band("breast", 0.44, 0.41, 0.97, 1.29, steel, top=0.84, y=0.03),
        a.band("hem", 0.455, 0.425, 0.955, 0.99, gold, y=0.03),
        a.band("gorget", 0.32, 0.31, 1.25, 1.34, gold, y=0.05),
        # The furnace: a gold ring round a burning heart in the middle of the breast.
        t.cylinder("furnace", 0.13, 0.07, gold, at=(0, -0.40, 1.13), sides=12, axis="Y", bevel=0.01),
        t.cylinder("heart", 0.085, 0.08, ember, at=(0, -0.41, 1.13), sides=12, axis="Y"),
        t.box("bar_l", (0.04, 0.03, 0.26), gold, at=(0.22, -0.345, 1.12), bevel=0.008),
        t.box("bar_r", (0.04, 0.03, 0.26), gold, at=(-0.22, -0.345, 1.12), bevel=0.008),
    ]

    spine = []
    for i in range(3):
        z0 = 0.87 - i * 0.105
        spine.append(a.band(f"lame_{i}", 0.432, 0.402, z0, z0 + 0.105, steel, y=0.03))
        spine.append(a.band(f"edge_{i}", 0.438, 0.408, z0, z0 + 0.022, gold, y=0.03, bevel=0))
    spine += [
        a.cape("cape", crimson),
        a.cape("hem", gold, top=(0.36, 0.556, 0.487), bottom=(0.28, 0.572, 0.50), rows=1, thickness=0.03),
    ]

    hips = [
        a.band("belt", 0.425, 0.40, 0.575, 0.645, gold, y=0.03),
        t.sphere("clasp", 0.045, ember, at=(0, -0.39, 0.61), subdivisions=0, squash=(1, 0.6, 1)),
        t.box("tabard", (0.17, 0.02, 0.32), crimson, at=(0, -0.385, 0.42), bevel=0.005),
    ]
    for side in (-1, 1):
        hips.append(t.box(f"tasset_front_{side}", (0.16, 0.04, 0.2), gold, at=(side * 0.2, -0.36, 0.49), bevel=0.015))
        hips.append(t.box(f"tasset_side_{side}", (0.04, 0.2, 0.19), steel, at=(side * 0.41, 0.02, 0.49), bevel=0.015))

    left = [
        t.sphere("dome", 0.21, gold, at=(0.34, 0.0, 1.22), subdivisions=2, squash=(1.1, 0.95, 0.72)),
        t.sphere("lame", 0.18, steel, at=(0.43, 0.0, 1.13), subdivisions=2, squash=(0.9, 0.95, 0.72)),
        t.sphere("gem", 0.05, ember, at=(0.34, 0.0, 1.375), subdivisions=0, squash=(1, 1, 1.3)),
    ]
    bracer_l = [
        a.sleeve("bracer", 0.50, 0.74, 0.15, 0.135, gold),
        a.sleeve("band", 0.60, 0.64, 0.155, 0.155, ember, bevel=0),
    ]

    return a.finish("kiln_aegis", {
        "chest": chest, "spine": spine, "hips": hips,
        "upperarm.l": left, "upperarm.r": [a.mirror_x(p) for p in left],
        "lowerarm.l": bracer_l, "lowerarm.r": [a.mirror_x(p) for p in bracer_l],
    })
