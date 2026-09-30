"""Berretto di Cuoio — a leather cap over the crown of the head, stitched in panels, the hair showing under it."""

import armourkit as a
import temins as t


def build():
    leather = t.material("leather")
    dark = t.material("leather_dark")
    stitch = t.material("stitch", (0.80, 0.70, 0.50), rough=0.9)

    head = [
        # The crown, cut off above the ears, and a rolled band round its edge.
        a.shell("cap", leather, centre=(0, -0.02, 1.84), radii=(0.615, 0.585, 0.52), below=1.9, thickness=0.03),
        a.band("band", 0.62, 0.59, 1.86, 1.95, dark, sides=20, y=-0.02),
        # Seams over the top, front to back and side to side.
        a.shell("seam_a", stitch, centre=(0, -0.02, 1.84), radii=(0.025, 0.60, 0.535), below=1.9, thickness=0.01, segments=6),
        a.shell("seam_b", stitch, centre=(0, -0.02, 1.84), radii=(0.63, 0.025, 0.535), below=1.9, thickness=0.01, segments=6),
        t.sphere("knot", 0.05, dark, at=(0, -0.02, 2.37), subdivisions=1, squash=(1, 1, 0.6)),
    ]

    return a.finish("leather_cap", {"head": head})
