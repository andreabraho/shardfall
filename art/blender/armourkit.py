"""The parts a suit of armour is made of, fitted round the Knight (the player's model).

An armour is rigid pieces, each riding one bone: a breastplate on the chest, lames on the
spine, a belt and tassets on the hips, pauldrons on the upper arms, bracers on the forearms.
The pieces are built round the Knight standing in its rest pose (a T-pose, facing -Y, Z up),
then joined per bone into objects called "bone__<bone>" — the game hangs each on its bone,
placed as it sits on the rest pose, so it follows every animation.

Measured on the Knight (2026-09-29): the body is a rounded box about 0.76 wide and 0.70 deep
from z 0.38 to 1.30 (chest bone above 1.05, spine 0.50 to 1.05, hips below); the upper arm
runs x 0.05 to 0.45 and the forearm 0.45 to 0.95, both about 0.27 thick round z 1.11.
"""

import math
import os

import bmesh
import bpy

import temins as t

KNIGHT = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                      "game", "assets", "kaykit", "adventurers2", "Knight.glb")


def band(name, rx, ry, z0, z1, mat, sides=14, top=1.0, y=0.0, bevel=0.012):
    """
    A ring of plate round the body: an elliptic prism from z0 to z1, rx across and ry deep,
    its top scaled by `top` so a breastplate can narrow to the shoulders.
    """
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=sides,
                          radius1=rx, radius2=rx * top, depth=z1 - z0)
    bmesh.ops.scale(bm, vec=(1, ry / rx, 1), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(0, y, (z0 + z1) / 2), verts=bm.verts)
    obj = t.mesh_object(name, bm, mat)

    if bevel > 0:
        t.add_bevel(obj, bevel)

    return obj


def mirror_x(obj):
    """The same piece on the other side of the body."""
    copy = obj.copy()
    copy.data = obj.data.copy()
    bpy.context.collection.objects.link(copy)
    copy.scale.x = -copy.scale.x
    copy.location.x = -copy.location.x
    bpy.context.view_layer.objects.active = copy
    copy.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    copy.select_set(False)
    return copy


def finish(name, by_bone, folder="armour"):
    """
    Joins each bone's pieces into one object called bone__<bone> ('.' becomes '_', which
    Godot's node names cannot hold) and leaves nothing else in the scene to export.
    """
    keep = set()

    for bone, parts in by_bone.items():
        joined = t.join("bone__" + bone.replace(".", "_"), parts)
        keep.add(joined)

    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)

    return {"folder": folder, "name": name, "reference": KNIGHT}


def cape(name, mat, top=(1.31, 0.40, 0.31), bottom=(0.28, 0.56, 0.50), curve=0.12, thickness=0.025, cols=8, rows=5):
    """
    A cape hanging behind, clear of the plates: from its top edge (z, y, half width) at the
    shoulders to its bottom edge, curved round the back so its middle stands furthest out.
    One rigid piece: worn on the spine, it sways with the body but never folds.
    """
    bm = bmesh.new()
    grid = []

    for r in range(rows + 1):
        v = r / rows
        z = top[0] + (bottom[0] - top[0]) * v
        y = top[1] + (bottom[1] - top[1]) * v
        half = top[2] + (bottom[2] - top[2]) * v
        row = []
        for c in range(cols + 1):
            u = c / cols * 2 - 1
            row.append(bm.verts.new((u * half, y - curve * u * u, z)))
        grid.append(row)

    for r in range(rows):
        for c in range(cols):
            bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = t.mesh_object(name, bm, mat)
    solid = obj.modifiers.new("Solid", "SOLIDIFY")
    solid.thickness = thickness
    return obj
