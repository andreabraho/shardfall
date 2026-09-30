"""Maglia del Crinale — mail from the desert ridge: rings of iron under a sand-coloured cowl and sash, chitin on the shoulders."""

import math

import armourkit as a
import temins as t


def build():
    mail = t.material("mail", (0.52, 0.53, 0.56), metal=0.6, rough=0.45)
    rings = t.material("mail_dark", (0.30, 0.31, 0.34), metal=0.5, rough=0.5)
    sand = t.material("sand", (0.78, 0.64, 0.42), rough=0.9)
    chitin = t.material("chitin", rough=0.45)
    leather = t.material("leather")

    def stripes(z0, z1, count, rx, ry, top=1.0):
        """Mail read as rows of rings: thin bands in two tones, the dark ones a hair further in."""
        out = []
        h = (z1 - z0) / count
        for i in range(count):
            k = 1 + (top - 1) * (i + 0.5) / count
            dark = i % 2 == 1
            out.append(a.band(f"mail_{z0}_{i}", rx * k - (0.004 if dark else 0), ry * k - (0.004 if dark else 0),
                              z0 + i * h, z0 + (i + 1) * h + 0.002, rings if dark else mail, y=0.03, bevel=0))
        return out

    chest = stripes(0.97, 1.29, 8, 0.43, 0.40, top=0.86)
    chest += [
        # A cowl of sand-coloured cloth round the shoulders, and a sash across the breast.
        a.band("cowl", 0.47, 0.44, 1.15, 1.32, sand, top=0.74, y=0.04, bevel=0.03),
    ]
    sash = t.box("sash", (0.11, 0.03, 0.46), sand, at=(0, 0, 0), bevel=0.01)
    sash.rotation_euler = (0, math.radians(38), 0)
    sash.location = (0.02, -0.415, 1.08)
    chest.append(sash)

    spine = stripes(0.64, 0.975, 8, 0.425, 0.395)

    hips = [
        a.band("sash_belt", 0.43, 0.405, 0.56, 0.66, sand, y=0.03, bevel=0.02),
        t.sphere("knot", 0.06, sand, at=(0.24, -0.35, 0.60), subdivisions=1),
        t.box("tail", (0.07, 0.02, 0.24), sand, at=(0.27, -0.36, 0.46), bevel=0.008),
    ]
    hips += stripes(0.44, 0.56, 3, 0.45, 0.42)

    left = [
        t.sphere("chitin", 0.2, chitin, at=(0.34, 0.0, 1.22), subdivisions=2, squash=(1.1, 0.95, 0.6)),
        a.spike("barb", (0.40, 0.0, 1.30), (0.52, 0.0, 1.46), 0.05, chitin),
    ]
    bracer_l = [
        a.sleeve("wrap", 0.50, 0.74, 0.14, 0.13, sand),
        a.sleeve("strap_0", 0.53, 0.56, 0.148, 0.148, leather, bevel=0),
        a.sleeve("strap_1", 0.68, 0.71, 0.14, 0.14, leather, bevel=0),
    ]

    return a.finish("ridge_mail", {
        "chest": chest, "spine": spine, "hips": hips,
        "upperarm.l": left, "upperarm.r": [a.mirror_x(p) for p in left],
        "lowerarm.l": bracer_l, "lowerarm.r": [a.mirror_x(p) for p in bracer_l],
    })
