"""Elmo di Ferro — a pointed iron helm: straight sides, a cone riveted along four ribs, a brow band and a nasal."""

import math

import armourkit as a
import temins as t

# Sides straight down to the jaw, then a cone to a point: wide enough everywhere for the head.
PROFILE = [(1.42, 0.635, 0.60), (1.70, 0.655, 0.615), (1.98, 0.645, 0.605), (2.12, 0.55, 0.52), (2.30, 0.36, 0.34), (2.46, 0.14, 0.13)]
TIP = 2.56


def build():
    iron = t.material("iron_plate", (0.55, 0.56, 0.60), metal=0.6, rough=0.4)
    dark = t.material("iron", metal=0.5, rough=0.55)
    rivet = t.material("steel_dark", metal=0.6, rough=0.4)

    y = a.HEAD[1]
    head = [
        a.lathe("bowl", iron, PROFILE, y=y, opening=(0.36, 1.25, 1.97), tip=TIP),
        # The brow band round the head, open where the face is, and a rim at the bottom edge.
        a.gap_band("brow", 0.665, 0.625, 1.93, 2.03, dark, gap=62, y=y),
        a.gap_band("rim", 0.65, 0.615, 1.41, 1.47, dark, gap=62, y=y),
        # The nasal, down from the brow over the nose.
        t.box("nasal", (0.08, 0.05, 0.36), dark, at=(0, -0.65, 1.84), bevel=0.012),
    ]

    # Four ribs up the cone from the band to the point, rivets along them.
    for k, angle in enumerate((0, 90, 180, 270)):
        a_rad = math.radians(angle)
        spine = [(z, rx + 0.02, ry + 0.02) for z, rx, ry in PROFILE[2:]]
        rib_pts = [(math.sin(a_rad) * rx, y - math.cos(a_rad) * ry, z) for z, rx, ry in spine] + [(0, y, TIP + 0.01)]
        for i, (p, q) in enumerate(zip(rib_pts, rib_pts[1:])):
            head.append(a.rod(f"rib_{k}_{i}", p, q, 0.03, dark))
            head.append(t.sphere(f"joint_{k}_{i}", 0.03, dark, at=q, subdivisions=1))
        for z, rx, ry in spine[:3]:
            head.append(t.sphere(f"rivet_{k}_{z}", 0.028, rivet, at=(math.sin(a_rad) * (rx + 0.01), y - math.cos(a_rad) * (ry + 0.01), z), subdivisions=1))

    return a.finish("iron_helm", {"head": head})
