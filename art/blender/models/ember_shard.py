"""The Ember Shard: a basalt standing stone split by glowing seams (PIP-02 test piece).

Meant for a fire land. A shard's visual is stretched to its height by the game, so the size
here only sets its proportions: about 1 m across and 3 m tall.
"""

import random

import bmesh
import bpy

import temins as t


def build():
    basalt = t.material("basalt", (0.16, 0.14, 0.14), rough=0.85)
    magma = t.material("magma", t.PALETTE["ember"], glow=1.6, rough=0.4)

    stone = t.rock("ember_shard", 0.55, basalt, at=(0, 0, 1.45), seed=7, squash=(0.95, 0.8, 2.7), roughness=0.24, subdivisions=1)
    stone.data.materials.append(magma)

    # Every facet becomes a plate with a seam round it; the seams on the upper two thirds
    # glow, and those plates stand a little proud so the light reads as cracks between them.
    rng = random.Random(11)
    bm = bmesh.new()
    bm.from_mesh(stone.data)

    faces = [f for f in bm.faces]
    result = bmesh.ops.inset_individual(bm, faces=faces, thickness=0.022, depth=0.0, use_even_offset=True)
    plates = set(faces)

    for f in bm.faces:
        centre = f.calc_center_median()
        lit = centre.z > 0.9 and rng.random() < 0.7

        if f in plates:
            if lit:
                bmesh.ops.translate(bm, verts=f.verts, vec=f.normal * 0.03)
        elif lit:
            f.material_index = 1

    bm.to_mesh(stone.data)
    bm.free()

    # Its foot sunk in a ring of broken slabs.
    pieces = [stone]
    for i in range(6):
        a = i / 6 * 6.283 + rng.uniform(-0.3, 0.3)
        import math
        at = (math.cos(a) * 0.75, math.sin(a) * 0.62, 0.08)
        pieces.append(t.rock(f"slab_{i}", rng.uniform(0.14, 0.22), basalt, at=at, seed=20 + i, squash=(1.2, 1.0, 0.55), roughness=0.2, subdivisions=1))

    t.join("ember_shard", pieces)

    return {"folder": "shards", "name": "ember_shard"}
