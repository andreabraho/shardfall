"""Helpers the model scripts share (PIP-01).

A model script builds its pieces with these and nothing else, so every model made for Temins
has the same look: low-poly, flat colour, chunky bevels, the palette below. Blender is run
headless by build.py; nothing here needs its interface.
"""

import math

import bmesh
import bpy
from mathutils import Matrix, Vector

# The palette. Named by what they are for, so a model reads as the game's and not as a new
# style every time.
PALETTE = {
    "steel": (0.72, 0.74, 0.78),
    "steel_dark": (0.36, 0.38, 0.43),
    "iron": (0.22, 0.23, 0.26),
    "gold": (0.93, 0.68, 0.24),
    "bronze": (0.62, 0.40, 0.20),
    "leather": (0.34, 0.20, 0.12),
    "leather_dark": (0.18, 0.11, 0.07),
    "wood": (0.45, 0.29, 0.16),
    "wood_dark": (0.26, 0.16, 0.09),
    "stone": (0.46, 0.45, 0.43),
    "stone_dark": (0.28, 0.27, 0.27),
    "moss": (0.33, 0.48, 0.20),
    "bone": (0.86, 0.82, 0.70),
    "web": (0.92, 0.92, 0.88),
    "chitin": (0.13, 0.10, 0.12),
    "venom": (0.55, 0.95, 0.30),
    "ember": (1.00, 0.36, 0.06),
    "violet": (0.62, 0.35, 0.95),
    "cloth_red": (0.62, 0.12, 0.10),
}

_materials = {}


def reset():
    """An empty scene, metres, nothing left from Blender's default file."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _materials.clear()
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0


def material(name, colour=None, metal=0.0, rough=0.6, glow=0.0):
    """A flat-coloured material, one per name. glow > 0 makes it emit its own colour."""
    if name in _materials:
        return _materials[name]

    rgb = colour if colour is not None else PALETTE[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough

    if glow > 0:
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = glow

    _materials[name] = mat
    return mat


def mesh_object(name, bm, mat, smooth=False):
    """Turns a bmesh into an object in the scene, with one material."""
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)

    for poly in obj.data.polygons:
        poly.use_smooth = smooth

    return obj


def profile_solid(name, outline, thickness, mat, bevel=0.0, edge=0.0):
    """
    A flat shape (a blade, a guard) from an outline in the XY plane, given thickness along Z.
    edge > 0 thins the outline's rim to a sharp edge: the front and back faces are inset and
    pushed out, so the middle is thick and the rim is thin, like a real blade.
    """
    bm = bmesh.new()
    verts = [bm.verts.new((x, y, 0.0)) for x, y in outline]
    face = bm.faces.new(verts)
    bmesh.ops.recalc_face_normals(bm, faces=[face])

    if edge > 0:
        # Extrude to a thin slab, then inset both faces and push their middles out.
        ext = bmesh.ops.extrude_face_region(bm, geom=[face])
        top = [e for e in ext["geom"] if isinstance(e, bmesh.types.BMFace)][0]
        bmesh.ops.translate(bm, verts=top.verts, vec=(0, 0, thickness * 0.2))
        bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, -thickness * 0.1))

        for f in (face, top):
            res = bmesh.ops.inset_region(bm, faces=[f], thickness=edge, depth=0)
            sign = 1 if f is top else -1
            bmesh.ops.translate(bm, verts=f.verts, vec=(0, 0, sign * thickness * 0.4))
    else:
        ext = bmesh.ops.extrude_face_region(bm, geom=[face])
        top = [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]
        bmesh.ops.translate(bm, verts=top, vec=(0, 0, thickness))
        bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, -thickness / 2))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = mesh_object(name, bm, mat)

    if bevel > 0:
        add_bevel(obj, bevel)

    return obj


def box(name, size, mat, at=(0, 0, 0), bevel=0.0, segments=1):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    bmesh.ops.translate(bm, vec=at, verts=bm.verts)
    obj = mesh_object(name, bm, mat)

    if bevel > 0:
        add_bevel(obj, bevel, segments)

    return obj


def cylinder(name, radius, depth, mat, at=(0, 0, 0), sides=8, radius_top=None, axis="Z", smooth=False, bevel=0.0):
    """A cylinder or a cone (radius_top differs), standing on its axis through `at`."""
    bm = bmesh.new()
    bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=sides,
        radius1=radius, radius2=radius if radius_top is None else radius_top, depth=depth)

    if axis == "Y":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(-90), 3, "X"))
    elif axis == "X":
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "Y"))

    bmesh.ops.translate(bm, vec=at, verts=bm.verts)
    obj = mesh_object(name, bm, mat, smooth=smooth)

    if bevel > 0:
        add_bevel(obj, bevel)

    return obj


def sphere(name, radius, mat, at=(0, 0, 0), subdivisions=1, squash=(1, 1, 1), smooth=False):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=radius)
    bmesh.ops.scale(bm, vec=squash, verts=bm.verts)
    bmesh.ops.translate(bm, vec=at, verts=bm.verts)
    return mesh_object(name, bm, mat, smooth=smooth)


def rock(name, radius, mat, at=(0, 0, 0), seed=0, squash=(1, 1, 1), roughness=0.22, subdivisions=2):
    """A faceted boulder: an icosphere pushed in and out by a seeded noise, flat shaded."""
    import random

    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdivisions, radius=radius)

    # A few random planes cut into it read as chiselled facets rather than noise.
    for v in bm.verts:
        n = v.co.normalized()
        push = 1.0 + rng.uniform(-roughness, roughness)
        v.co = n * radius * push

    for _ in range(6):
        normal = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.4, 1))).normalized()
        cut = radius * rng.uniform(0.72, 0.88)
        for v in bm.verts:
            d = v.co.dot(normal)
            if d > cut:
                v.co -= normal * (d - cut)

    bmesh.ops.scale(bm, vec=squash, verts=bm.verts)
    bmesh.ops.translate(bm, vec=at, verts=bm.verts)
    return mesh_object(name, bm, mat)


def add_bevel(obj, width, segments=1):
    mod = obj.modifiers.new("Bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    mod.angle_limit = math.radians(35)
    return mod


def join(name, objects):
    """Applies every modifier and joins the pieces into one object, materials kept."""
    bpy.ops.object.select_all(action="DESELECT")

    for obj in objects:
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        for mod in list(obj.modifiers):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.select_set(False)

    for obj in objects:
        obj.select_set(True)

    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = name
    joined.data.name = name
    return joined


def triangles():
    """How many triangles the scene's meshes make: the budget a model is judged against."""
    count = 0
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            count += sum(len(p.vertices) - 2 for p in obj.data.polygons)
    return count


def export_glb(path):
    bpy.ops.export_scene.gltf(
        filepath=path,
        export_format="GLB",
        use_selection=False,
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
    )


def soften(obj, angle=50):
    """
    Smooth shading on what is curved, a hard edge only where faces meet at more than `angle`
    degrees: a band of plate reads as round steel, and its rim and corners stay crisp.
    """
    for poly in obj.data.polygons:
        poly.use_smooth = True

    obj.data.set_sharp_from_angle(angle=math.radians(angle))
