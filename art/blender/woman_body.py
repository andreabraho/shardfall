"""The woman's plain clothes (CLS-00, 2026-09-30): the KayKit Rogue lightened — a sleeveless
tunic, bare arms, no scarf, no lacing across the chest, no pouches at the hips.

    blender -b --factory-startup --python art/blender/woman_body.py

"Too covered; too many clothes" (your call): the Rogue's outfit is a thief's, all sleeves,
scarf and pouches. Her mesh is the pack's; what this does is take pieces away and repaint the
sleeves as skin, so she keeps the pack's look and its skinning to the shared skeleton. Writes
game/assets/temins/characters/woman_body.glb — the skeleton, her head, body, arms and legs —
which visuals.json grafts on the player as a woman.

The Rogue's body and arms are made of separate pieces (a tunic, a scarf, a buckle, a pouch…).
They are found by what colour they are and where they sit, not by their order in the file, and
the count of each is checked, so a changed source file fails loudly instead of losing the wrong
piece.
"""

import os

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SOURCE = os.path.join(ROOT, "game", "assets", "kaykit", "adventurers2", "Rogue.glb")
OUT = os.path.join(ROOT, "game", "assets", "temins", "characters", "woman_body.glb")

KEEP = {"Rogue_Head", "Rogue_Body", "Rogue_ArmLeft", "Rogue_ArmRight", "Rogue_LegLeft", "Rogue_LegRight"}


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


def lighten(obj, image, drop, repaint, skin_uv, expect):
    """Deletes the pieces drop(colour, centre) picks, repaints repaint(colour, centre) as skin."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    uv = bm.loops.layers.uv.active
    doomed, painted = [], 0
    for piece in pieces(bm):
        colour = colour_at(image, piece[0].link_loops[0][uv].uv)
        centre = sum((obj.matrix_world @ v.co for v in piece), Vector()) / len(piece)
        if drop(colour, centre):
            doomed.extend(piece)
        elif repaint(colour, centre):
            for v in piece:
                for loop in v.link_loops:
                    loop[uv].uv = skin_uv
            painted += 1
    dropped = len({id(v) for v in doomed})
    bmesh.ops.delete(bm, geom=list({v: None for v in doomed}), context="VERTS")
    bm.to_mesh(obj.data)
    bm.free()
    print(f"[woman] {obj.name}: {painted} pieces repainted, {dropped} vertices dropped")
    if expect is not None and painted != expect:
        raise SystemExit(f"{obj.name}: expected {expect} pieces to repaint, found {painted} — has the Rogue changed?")


def neck(body, skin_uv, centre=(0.0, -0.14, 1.04), radii=(0.16, 0.13, 0.13)):
    """A rounded patch of skin set into the neckline, weighted wholly to the chest."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1.0)
    bmesh.ops.scale(bm, vec=radii, verts=bm.verts)
    bmesh.ops.translate(bm, vec=centre, verts=bm.verts)
    uv = bm.loops.layers.uv.new()
    for face in bm.faces:
        face.smooth = True
        for loop in face.loops:
            loop[uv].uv = skin_uv
    mesh = bpy.data.meshes.new("neck")
    bm.to_mesh(mesh)
    bm.free()
    patch = bpy.data.objects.new("neck", mesh)
    bpy.context.collection.objects.link(patch)
    patch.data.materials.append(body.data.materials[0])
    patch.data.uv_layers[0].name = body.data.uv_layers.active.name
    patch.vertex_groups.new(name="chest").add(list(range(len(mesh.vertices))), 1.0, "REPLACE")

    bpy.ops.object.select_all(action="DESELECT")
    patch.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SOURCE)

    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.name not in KEEP:
            bpy.data.objects.remove(obj, do_unlink=True)

    image = next(i for i in bpy.data.images if i.size[0] > 0)

    # The skin swatch: where her hand's UVs point.
    hand = bpy.data.objects["Rogue_ArmLeft"]
    skin_uv = None
    bm = bmesh.new()
    bm.from_mesh(hand.data)
    uv = bm.loops.layers.uv.active
    for piece in pieces(bm):
        at = piece[0].link_loops[0][uv].uv.copy()
        if near(colour_at(image, at), (0.96, 0.74, 0.59)):
            skin_uv = at
            break
    bm.free()
    if skin_uv is None:
        raise SystemExit("no skin swatch found on the Rogue's hand")

    teal = (0.03, 0.43, 0.35)

    def scarf(c, _):
        return c.x < 0.02 and c.y > 0.46 and 0.30 < c.z < 0.36

    def lacing(c, at):
        return at.y < -0.25 and 0.70 < at.z < 0.88 and abs(at.x) < 0.1 and c.x > 0.6

    def pouch(_, at):
        # Hanging off the belt at the hips, forward on the right and back on the left.
        return abs(at.x) > 0.17 and 0.44 < at.z < 0.70 and abs(at.y) > 0.03

    lighten(bpy.data.objects["Rogue_Body"], image,
            drop=lambda c, at: scarf(c, at) or lacing(c, at) or pouch(c, at),
            repaint=lambda c, at: False, skin_uv=skin_uv, expect=None)

    # With the scarf gone the tunic's neck is a V open onto its own inside: skin behind it, riding
    # the chest like the tunic does.
    neck(bpy.data.objects["Rogue_Body"], skin_uv)

    # The sleeves, and the bands of green round them: skin. The leather bracers stay.
    for side in ("Rogue_ArmLeft", "Rogue_ArmRight"):
        lighten(bpy.data.objects[side], image,
                drop=lambda c, at: False,
                repaint=lambda c, at: near(c, teal, 0.08) or (c.x < 0.02 and c.y > 0.46),
                skin_uv=skin_uv, expect=4)

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=False,
                              export_animations=False, export_skins=True, export_yup=True)
    print(f"[woman] -> {os.path.relpath(OUT, ROOT)}")


main()
