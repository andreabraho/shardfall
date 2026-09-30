"""Farsetto di Cuoio — a laced leather vest, shoulder pads, pouches on the belt, leather bracers."""

import armourkit as a
import temins as t


def build():
    leather = t.material("leather")
    dark = t.material("leather_dark")
    stitch = t.material("stitch", (0.80, 0.70, 0.50), rough=0.9)
    bronze = t.material("bronze", metal=0.6, rough=0.45)

    chest = [
        a.band("vest", 0.435, 0.405, 0.97, 1.29, leather, top=0.84, y=0.03),
        # The laced opening down the front.
        t.box("edge_l", (0.035, 0.03, 0.30), dark, at=(-0.06, -0.385, 1.12), bevel=0.008),
        t.box("edge_r", (0.035, 0.03, 0.30), dark, at=(0.06, -0.385, 1.12), bevel=0.008),
    ]
    chest += [t.box(f"lace_{i}", (0.13, 0.02, 0.018), stitch, at=(0, -0.40, 1.02 + i * 0.075)) for i in range(4)]

    spine = [
        a.band("belly", 0.43, 0.40, 0.64, 0.975, leather, y=0.03),
        a.band("seam", 0.435, 0.405, 0.80, 0.815, stitch, y=0.03, bevel=0),
    ]

    hips = [
        a.band("belt", 0.425, 0.40, 0.575, 0.645, dark, y=0.03),
        t.box("buckle", (0.09, 0.03, 0.07), bronze, at=(0, -0.38, 0.61), bevel=0.01),
        t.box("pouch_l", (0.13, 0.08, 0.12), leather, at=(0.28, -0.33, 0.55), bevel=0.02),
        t.box("pouch_r", (0.11, 0.08, 0.10), leather, at=(-0.30, -0.31, 0.56), bevel=0.02),
    ]
    for side in (-1, 1):
        hips.append(t.box(f"flap_front_{side}", (0.15, 0.03, 0.15), leather, at=(side * 0.16, -0.39, 0.49), bevel=0.012))
        hips.append(t.box(f"flap_side_{side}", (0.03, 0.18, 0.15), leather, at=(side * 0.43, 0.02, 0.49), bevel=0.012))

    left = [
        t.sphere("pad", 0.19, leather, at=(0.33, 0.0, 1.21), subdivisions=2, squash=(1.05, 0.95, 0.6)),
        t.sphere("pad_rim", 0.195, stitch, at=(0.33, 0.0, 1.19), subdivisions=2, squash=(1.05, 0.95, 0.35)),
    ]
    bracer_l = [
        a.sleeve("bracer", 0.50, 0.74, 0.145, 0.132, leather),
        a.sleeve("strap_0", 0.55, 0.58, 0.152, 0.152, dark, bevel=0),
        a.sleeve("strap_1", 0.66, 0.69, 0.145, 0.145, dark, bevel=0),
    ]

    return a.finish("leather_vest", {
        "chest": chest, "spine": spine, "hips": hips,
        "upperarm.l": left, "upperarm.r": [a.mirror_x(p) for p in left],
        "lowerarm.l": bracer_l, "lowerarm.r": [a.mirror_x(p) for p in bracer_l],
    })
