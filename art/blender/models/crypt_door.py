"""A sealed door of the Abyss Crypts (P5, 2026-10-07): two leaves of black stone bound in iron, in a frame of crypt
stone, and across them a seal of violet light that the door's guard keeps lit. Floor two has four, one in each
doorway of the court.

Built wide along X and thin along Y, as it stands in its doorway; the game stretches it to the visual's height.
"""

import math

import armourkit as a
import temins as t


def build():
    stone = t.material("crypt_stone", (0.16, 0.13, 0.20), rough=0.85)
    leaf = t.material("crypt_leaf", (0.07, 0.06, 0.09), rough=0.8)
    iron = t.material("crypt_iron", (0.10, 0.09, 0.11), metal=0.7, rough=0.4)
    seal = t.material("abyss_seal", (0.62, 0.30, 1.0), glow=1.4, rough=0.3)

    pieces = [
        # The frame: two posts and a lintel, a step at the foot.
        t.box("post_l", (0.34, 0.5, 2.9), stone, at=(-1.66, 0, 1.45), bevel=0.05, segments=3),
        t.box("post_r", (0.34, 0.5, 2.9), stone, at=(1.66, 0, 1.45), bevel=0.05, segments=3),
        t.box("lintel", (3.7, 0.56, 0.36), stone, at=(0, 0, 3.0), bevel=0.06, segments=3),
        t.box("step", (3.6, 0.7, 0.1), stone, at=(0, 0, 0.05), bevel=0.03, segments=2),
        # The two leaves, meeting in the middle.
        t.box("leaf_l", (1.48, 0.22, 2.8), leaf, at=(-0.75, 0, 1.45), bevel=0.03, segments=2),
        t.box("leaf_r", (1.48, 0.22, 2.8), leaf, at=(0.75, 0, 1.45), bevel=0.03, segments=2),
    ]

    # Iron bands across both leaves, studded.
    for i, z in enumerate((0.45, 1.45, 2.45)):
        pieces.append(t.box(f"band_{i}", (3.0, 0.27, 0.14), iron, at=(0, 0, z), bevel=0.02, segments=2))

        for j in range(7):
            x = -1.3 + j * (2.6 / 6)
            pieces.append(t.sphere(f"stud_{i}_{j}", 0.045, iron, at=(x, -0.14, z), subdivisions=2))

    # The seal: a ring across the meeting of the leaves, with spokes, and a point of light in the middle.
    arcs = 24

    for k in range(arcs):
        r0, r1 = k / arcs * math.tau, (k + 1) / arcs * math.tau
        p0 = (math.cos(r0) * 0.58, -0.15, 1.6 + math.sin(r0) * 0.58)
        p1 = (math.cos(r1) * 0.58, -0.15, 1.6 + math.sin(r1) * 0.58)
        pieces.append(a.rod(f"ring_{k}", p0, p1, 0.04, seal, sides=8))

    for k in range(6):
        rad = k / 6 * math.tau
        tip = (math.cos(rad) * 0.5, -0.15, 1.6 + math.sin(rad) * 0.5)
        pieces.append(a.spike(f"spoke_{k}", (0, -0.15, 1.6), tip, 0.03, seal, sides=5))

    pieces.append(t.sphere("seal_eye", 0.12, seal, at=(0, -0.17, 1.6), subdivisions=2))

    # Horns of stone on the lintel's ends, curling up.
    for side in (-1, 1):
        pieces.append(a.spike(f"horn_{side}", (side * 1.6, 0, 3.15), (side * 2.0, -0.05, 3.7), 0.14, stone, sides=10))

    obj = t.join("crypt_door", pieces)
    t.soften(obj, 35)

    return {"folder": "crypts", "name": "crypt_door"}
