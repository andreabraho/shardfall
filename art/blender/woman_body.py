"""The woman's plain clothes (CLS-00, 2026-09-30): the KayKit Rogue in a sleeveless bodice with
two straps, bare shoulders and arms, her skirt, belt and boots kept.

    blender -b --factory-startup --python art/blender/woman_body.py

"Too covered; too many clothes", then "the chest and shoulders look all wrong" (your calls):
the Rogue's tunic was made to sit under a scarf, and with the scarf and sleeves taken off its
top was a ragged V over nothing. So the top half is ours now — a smooth skin torso and a plain
bodice lofted round it, in her own palette, weighted to her spine and chest — and the rest is
hers: head, skirt, belt and pouches, forearm bracers, trousers and boots. Writes
game/assets/temins/characters/woman_body.glb (the skeleton and her head, body, arms and legs),
which visuals.json grafts on the player as a woman.

The Rogue's body and arms are made of separate pieces (a tunic, a scarf, a buckle, a pouch…).
They are found by what colour they are and where they sit, not by their order in the file, and
the count of each is checked, so a changed source file fails loudly instead of losing the wrong
piece.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

import creaturekit as c  # noqa: E402

SOURCE = os.path.join(ROOT, "game", "assets", "kaykit", "adventurers2", "Rogue.glb")
OUT = os.path.join(ROOT, "game", "assets", "temins", "characters", "woman_body.glb")

KEEP = {"Rogue_Head", "Rogue_Body", "Rogue_ArmLeft", "Rogue_ArmRight", "Rogue_LegLeft", "Rogue_LegRight"}

# Her palette, as the texture has it (linear): what each swatch is found by.
SKIN = (0.96, 0.74, 0.59)
TEAL = (0.03, 0.43, 0.35)
SCARF = (0.0, 0.52, 0.33)
LEATHER = (0.69, 0.44, 0.32)


def pieces(bm):
    """The mesh's loose pieces, as lists of vertices."""
    bm.verts.ensure_lookup_table()
    seen, found = set(), []
    for start in bm.verts:
        if start.index in seen:
            continue
        stack, piece = [start], []
        seen.add(start.index)
        while stack:
            v = stack.pop()
            piece.append(v)
            for edge in v.link_edges:
                w = edge.other_vert(v)
                if w.index not in seen:
                    seen.add(w.index)
                    stack.append(w)
        found.append(piece)
    return found


def colour_at(image, uv):
    width, height = image.size
    x = min(width - 1, int(uv.x * width))
    y = min(height - 1, int(uv.y * height))
    i = (y * width + x) * 4
    return Vector(image.pixels[i:i + 3])


def near(a, b, by=0.06):
    return (a - Vector(b)).length < by


def swatch(obj, image, colour):
    """A UV that samples `colour` on this object's texture."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    uv = bm.loops.layers.uv.active
    for face in bm.faces:
        for loop in face.loops:
            if near(colour_at(image, loop[uv].uv), colour, 0.05):
                found = loop[uv].uv.copy()
                bm.free()
                return found
    bm.free()
    raise SystemExit(f"{obj.name}: no swatch of {colour} — has the Rogue changed?")


def edit(obj, image, drop, repaint, paint_uv, expect_drop, expect_paint):
    """Deletes the pieces drop(colour, centre, size) picks, repaints those repaint() picks."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    uv = bm.loops.layers.uv.active
    doomed, dropped, painted = {}, 0, 0
    for piece in pieces(bm):
        colour = colour_at(image, piece[0].link_loops[0][uv].uv)
        points = [obj.matrix_world @ v.co for v in piece]
        centre = sum(points, Vector()) / len(points)
        size = Vector([max(p[k] for p in points) - min(p[k] for p in points) for k in range(3)])
        if drop(colour, centre, size):
            doomed.update({v: None for v in piece})
            dropped += 1
        elif repaint(colour, centre, size):
            for v in piece:
                for loop in v.link_loops:
                    loop[uv].uv = paint_uv
            painted += 1
    bmesh.ops.delete(bm, geom=list(doomed), context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    print(f"[woman] {obj.name}: {dropped} pieces dropped, {painted} repainted")
    if dropped != expect_drop or painted != expect_paint:
        raise SystemExit(f"{obj.name}: expected {expect_drop} dropped and {expect_paint} repainted — has the Rogue changed?")


def ring_path(z, rx, ry, y=0.0):
    return (Vector((0, y, z)), rx, ry)


def weigh(obj, arm, bones):
    """Each vertex to the nearest of `bones`, shared where two meet, by distance to the bone."""
    segments = {b: (arm.matrix_world @ arm.data.bones[b].head_local, arm.matrix_world @ arm.data.bones[b].tail_local) for b in bones}
    for b in bones:
        obj.vertex_groups.new(name=b)
    for v in obj.data.vertices:
        p = obj.matrix_world @ v.co
        near_ = []
        for b, (a, t) in segments.items():
            ab = t - a
            u = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
            near_.append((1.0 / max((p - (a + ab * u)).length, 0.01) ** 4, b))
        near_.sort(reverse=True)
        near_ = near_[:2]
        total = sum(w for w, _ in near_)
        for w, b in near_:
            obj.vertex_groups[b].add([v.index], w / total, "REPLACE")


def painted(obj, uv_at, material):
    """Every corner of a new part sampling one swatch of her texture, smooth-shaded."""
    obj.data.materials.clear()
    obj.data.materials.append(material)
    layer = obj.data.uv_layers.new(name="UVMap")
    for loop in layer.data:
        loop.uv = uv_at
    for poly in obj.data.polygons:
        poly.use_smooth = True


def wear(image):
    """Every material's texture swapped for `image`."""
    for material in bpy.data.materials:
        if material.node_tree:
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE":
                    node.image = image


def top_half(body, arm, skin_uv, teal_uv, leather_uv, crop=False):
    """Her skin from the waist to the neck, a bodice over it and two straps over the shoulders.

    With `crop` the bodice stops under the chest and her midriff is bare (the Ninja, ninja_body.py).
    """
    material = body.data.materials[0]
    parts = []

    # The torso: her shape under the tunic, a rounded barrel narrowing to the neck.
    torso = c.loft("torso", [
        ring_path(0.60, 0.33, 0.28), ring_path(0.76, 0.355, 0.30), ring_path(0.90, 0.36, 0.305),
        ring_path(1.02, 0.345, 0.295), ring_path(1.11, 0.30, 0.26), ring_path(1.18, 0.21, 0.19),
        ring_path(1.24, 0.11, 0.11),
    ], material, sides=28, per=3)
    painted(torso, skin_uv, material)
    parts.append((torso, ["hips", "spine", "chest"]))

    # The bodice: from inside the skirt up to under the arms, its top edge straight across.
    low = [ring_path(0.83, 0.372, 0.317), ring_path(0.845, 0.380, 0.323), ring_path(0.88, 0.381, 0.324)] if crop else [
        ring_path(0.58, 0.345, 0.295), ring_path(0.72, 0.372, 0.318), ring_path(0.86, 0.380, 0.323)]
    bodice = c.loft("bodice", low + [
        ring_path(0.95, 0.372, 0.318), ring_path(1.00, 0.362, 0.308), ring_path(1.035, 0.350, 0.300),
    ], material, sides=28, per=3)
    painted(bodice, teal_uv, material)
    parts.append((bodice, ["hips", "spine", "chest"]))

    # A strap over each shoulder, front edge of the bodice to the back.
    for side in (1, -1):
        path = []
        for i in range(9):
            a = math.radians(-70 + i * 17.5)  # from the front, over the top, to the back
            y = math.sin(a) * 0.30
            z = 1.0 + math.cos(a) * 0.205
            x = side * (0.19 - 0.015 * math.cos(a))
            path.append((Vector((x, y, z)), 0.04, 0.012))
        strap = c.loft(f"strap{side}", path, material, sides=8, per=2)
        painted(strap, teal_uv, material)
        parts.append((strap, ["chest", "spine"]))

    # A leather band along the bodice's top edge, as its hem.
    hem = c.loft("hem", [(Vector((0, 0, 1.025)), 0.358, 0.307), (Vector((0, 0, 1.045)), 0.352, 0.302)], material, sides=28, per=1)
    painted(hem, leather_uv, material)
    parts.append((hem, ["spine", "chest"]))

    if crop:
        foot = c.loft("foot", [(Vector((0, 0, 0.825)), 0.376, 0.320), (Vector((0, 0, 0.85)), 0.386, 0.328)], material, sides=28, per=1)
        painted(foot, leather_uv, material)
        parts.append((foot, ["spine", "chest"]))

    for obj, bones in parts:
        weigh(obj, arm, bones)

    bpy.ops.object.select_all(action="DESELECT")
    for obj, _ in parts:
        obj.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()


def build(out=OUT, crop=False, trim=None, dye=None, style=None):
    """The body to `out`. `trim` is the colour of the hems in place of leather; `style(arm, image)`
    restyles her further; `dye(image)` returns the texture to wear."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SOURCE)

    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.name not in KEEP:
            bpy.data.objects.remove(obj, do_unlink=True)

    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    image = next(i for i in bpy.data.images if i.size[0] > 0)
    body = bpy.data.objects["Rogue_Body"]
    left = bpy.data.objects["Rogue_ArmLeft"]

    skin_uv = swatch(left, image, SKIN)
    teal_uv = swatch(body, image, TEAL)
    leather_uv = swatch(body, image, trim or LEATHER)

    # Off the body: the tunic's upper half (teal, reaching above the waist), the scarf and
    # the lacing on the chest. The skirt, the belt, its buckle and pouches stay.
    edit(body, image,
         drop=lambda col, at, size: (near(col, TEAL, 0.08) and at.z + size.z / 2 > 1.0)
         or near(col, SCARF, 0.08)
         or (at.y < -0.25 and 0.70 < at.z < 0.88 and abs(at.x) < 0.1 and col.x > 0.6),
         repaint=lambda col, at, size: False, paint_uv=skin_uv, expect_drop=13, expect_paint=0)

    # The arms: the sleeves and their green bands as skin, the rolled cuffs at the shoulder
    # gone; the bracers and the bands at the elbow stay.
    for side in ("Rogue_ArmLeft", "Rogue_ArmRight"):
        edit(bpy.data.objects[side], image,
             drop=lambda col, at, size: (col.x > 0.6 and 0.26 < abs(at.x) < 0.40 and size.z > 0.26)
             or (near(col, TEAL, 0.08) and abs(at.x) < 0.30),
             repaint=lambda col, at, size: near(col, TEAL, 0.08) or near(col, SCARF, 0.08),
             paint_uv=skin_uv, expect_drop=4, expect_paint=3)

    # In place of the upper sleeve, whose cap stood up off the shoulder: a smooth bare arm from
    # inside the shoulder to the elbow band.
    for name, side in (("Rogue_ArmLeft", 1), ("Rogue_ArmRight", -1)):
        limb = bpy.data.objects[name]
        upper = c.loft(f"upper{side}", [
            (Vector((side * 0.10, 0, 1.107)), 0.112, 0.112), (Vector((side * 0.22, 0, 1.107)), 0.108, 0.108),
            (Vector((side * 0.34, 0, 1.107)), 0.100, 0.100), (Vector((side * 0.47, 0.005, 1.107)), 0.094, 0.094),
        ], limb.data.materials[0], sides=20, per=3, across=(0, 1, 0))
        painted(upper, skin_uv, limb.data.materials[0])
        weigh(upper, arm, ["chest", f"upperarm.{'l' if side > 0 else 'r'}", f"lowerarm.{'l' if side > 0 else 'r'}"])
        bpy.ops.object.select_all(action="DESELECT")
        upper.select_set(True)
        limb.select_set(True)
        bpy.context.view_layer.objects.active = limb
        bpy.ops.object.join()

    top_half(body, arm, skin_uv, teal_uv, leather_uv, crop)

    if style:
        style(arm, image)

    if dye:
        wear(dye(image))

    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", use_selection=False,
                              export_animations=False, export_skins=True, export_yup=True)
    print(f"[woman] -> {os.path.relpath(out, ROOT)}")


# The Warrior woman wears the bodice cut short too (2026-10-02, at your call: "the women a little more
# uncovered" — so the women's cut of an armour leaves her belly bare, as on every other class).
if __name__ == "__main__":
    build(crop=True)
