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


def finish(name, by_bone, folder="armour", focus=None):
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

    helmet = list(by_bone) == ["head"]

    if focus is None:
        focus = ((0, 0, 1.75), 0.75) if helmet else ((0, 0, 0.9), 0.85)

    replaces = ["Knight_Helmet", "Knight_HelmetVisor"] if helmet else ["Knight_Cape", "Knight_Helmet", "Knight_HelmetVisor"]

    return {"folder": folder, "name": name, "reference": KNIGHT, "focus": focus, "replaces": replaces}


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


def gap_band(name, rx, ry, z0, z1, mat, gap=70, sides=20, y=0.0, thickness=0.03, top=1.0, front=None):
    """
    A band round the head or body left open over `gap` degrees at the front (-Y), for the
    face; with `front`, only that many degrees at the front are kept instead.
    """
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=False, cap_tris=False, segments=sides,
                          radius1=rx, radius2=rx * top, depth=z1 - z0)
    bmesh.ops.scale(bm, vec=(1, ry / rx, 1), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(0, y, (z0 + z1) / 2), verts=bm.verts)

    doomed = []
    for f in bm.faces:
        c = f.calc_center_median()
        angle = math.degrees(math.atan2(c.x, -(c.y - y)))
        if (front is None and abs(angle) < gap / 2) or (front is not None and abs(angle) > front / 2):
            doomed.append(f)

    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    obj = t.mesh_object(name, bm, mat)
    obj.modifiers.new("Solid", "SOLIDIFY").thickness = thickness
    return obj


def shell(name, mat, centre, radii, below=None, opening=None, thickness=0.035, segments=18, rings=12, smooth=False):
    """
    A helmet's shell: an ellipsoid round the head, cut off below `below` and, with `opening`
    (half width, bottom, top), open over the face at the front.
    """
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
    bmesh.ops.scale(bm, vec=radii, verts=bm.verts)
    bmesh.ops.translate(bm, vec=centre, verts=bm.verts)

    doomed = []
    for f in bm.faces:
        c = f.calc_center_median()
        if below is not None and c.z < below:
            doomed.append(f)
        elif opening is not None and c.y < centre[1] and abs(c.x) < opening[0] and opening[1] < c.z < opening[2]:
            doomed.append(f)

    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    obj = t.mesh_object(name, bm, mat, smooth=smooth)
    obj.modifiers.new("Solid", "SOLIDIFY").thickness = thickness
    return obj


def roll(name, rx, ry, z0, z1, mat, y=0.03, top=1.0):
    """A padded roll of quilting: a band with its edges rounded right off."""
    obj = band(name, rx, ry, z0, z1, mat, top=top, y=y, bevel=0)
    t.add_bevel(obj, (z1 - z0) * 0.4, segments=3)
    return obj


def sleeve(name, x0, x1, r0, r1, mat, z=1.11, sides=8, bevel=0.01, squash=1.0):
    """A sleeve or bracer round the left arm from x0 to x1 (mirror it for the right)."""
    obj = t.cylinder(name, r0, x1 - x0, mat, at=((x0 + x1) / 2, 0, z), sides=sides, radius_top=r1, axis="X", bevel=bevel)
    obj.scale.y = squash
    return obj


def spike(name, base, tip, radius, mat, sides=5):
    """A cone from `base` to `tip`."""
    from mathutils import Vector

    base, tip = Vector(base), Vector(tip)
    length = (tip - base).length
    obj = t.cylinder(name, radius, length, mat, at=(0, 0, length / 2), sides=sides, radius_top=0.0)
    obj.rotation_euler = (tip - base).to_track_quat("Z", "Y").to_euler()
    obj.location = base
    return obj


def scales(name, rx, ry, z, mat, count, size=(0.1, 0.02, 0.1), y=0.03, offset=0.0, tilt=18, alt=None):
    """
    A row of overlapping scales round the body at height z, each standing a little out at its
    foot; `alt` gives every other scale another material.
    """
    parts = []
    for i in range(count):
        a = 2 * math.pi * (i + offset) / count
        px, py = rx * math.sin(a), ry * -math.cos(a)
        nx, ny = px / (rx * rx), py / (ry * ry)
        # No bevel: a hauberk has hundreds of these, and a bevelled box is four times the triangles.
        scale = t.box(f"{name}_{i}", size, alt if alt is not None and i % 2 else mat)
        scale.rotation_euler = (math.radians(tilt), 0, math.atan2(-nx, ny))
        scale.location = (px * 1.02, py * 1.02 + y, z)
        parts.append(scale)
    return parts


def around(centre, radii, z, grow=0.015):
    """The half width and depth of a shell at height z, a little out from it: for a band to sit on it."""
    k = max(0.0, 1 - ((z - centre[2]) / radii[2]) ** 2) ** 0.5
    return radii[0] * k + grow, radii[1] * k + grow


# The head a helmet goes round (2026-09-30): wide enough that no hair shows between its facets.
HEAD = (0, -0.02, 1.76)
HELM = (0.635, 0.60, 0.66)
