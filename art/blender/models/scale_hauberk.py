"""Usbergo a Scaglie — a coat of overlapping scales, rows of bronze and iron in turn, on dark leather, bronze pauldrons."""

import armourkit as a
import temins as t


def taper(rx, ry, z0, z1, top, z):
    """The size of a band tapering from (rx, ry) at z0 to `top` times that at z1, at height z."""
    k = 1 + (top - 1) * (z - z0) / (z1 - z0)
    return rx * k, ry * k


def build():
    base = t.material("hauberk_leather", (0.22, 0.20, 0.18), rough=0.8)
    bronze = t.material("bronze_scale", (0.66, 0.46, 0.22), metal=0.65, rough=0.4)
    iron = t.material("iron_scale", (0.55, 0.55, 0.58), metal=0.6, rough=0.45)
    belt = t.material("leather_dark")
    brass = t.material("bronze", metal=0.6, rough=0.45)

    size = (0.105, 0.02, 0.12)

    chest = [a.band("under", 0.43, 0.40, 0.97, 1.29, base, top=0.84, y=0.03)]
    for i, z in enumerate((1.03, 1.12, 1.21)):
        rx, ry = taper(0.43, 0.40, 0.97, 1.29, 0.84, z)
        chest += a.scales(f"chest_{i}", rx, ry, z, iron if i % 2 else bronze, 24 - i * 2, size, offset=0.5 * (i % 2))
    chest.append(a.band("collar", 0.33, 0.31, 1.25, 1.32, brass, y=0.04))

    spine = [a.band("under", 0.425, 0.395, 0.64, 0.975, base, y=0.03)]
    for i, z in enumerate((0.70, 0.79, 0.88, 0.955)):
        spine += a.scales(f"belly_{i}", 0.425, 0.395, z, iron if i % 2 else bronze, 24, size, offset=0.5 * (i % 2))

    hips = [
        a.band("belt", 0.425, 0.40, 0.575, 0.645, belt, y=0.03),
        t.box("buckle", (0.09, 0.03, 0.07), brass, at=(0, -0.38, 0.61), bevel=0.01),
    ]
    for i, z in enumerate((0.52, 0.44)):
        hips += a.scales(f"skirt_{i}", 0.45 + i * 0.015, 0.42 + i * 0.015, z, iron if i % 2 else bronze, 26, size, offset=0.5 * i)

    left = [
        t.sphere("dome", 0.2, bronze, at=(0.34, 0.0, 1.21), subdivisions=2, squash=(1.1, 0.95, 0.7)),
        t.sphere("lame", 0.17, iron, at=(0.42, 0.0, 1.13), subdivisions=2, squash=(0.9, 0.95, 0.7)),
    ]
    bracer_l = [a.sleeve("bracer", 0.50, 0.74, 0.145, 0.132, belt)]

    return a.finish("scale_hauberk", {
        "chest": chest, "spine": spine, "hips": hips,
        "upperarm.l": left, "upperarm.r": [a.mirror_x(p) for p in left],
        "lowerarm.l": bracer_l, "lowerarm.r": [a.mirror_x(p) for p in bracer_l],
    })
