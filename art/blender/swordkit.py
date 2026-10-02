"""The parts every sword is made of, so the game's swords read as one family.

Sizes follow the KayKit swords the character's hands were rigged for: the grip at the origin,
the blade up the Z axis, its flat facing Y. A one-handed sword reaches about 1.4 up and 0.37
down, a great sword about 1.95 up, 0.4 down and 0.42 across the guard.

Outlines are drawn in the XY plane (X across the blade, Y along it) and stood up by stand().
"""

import math

import bpy

import temins as t


def stand(obj, x=0.0, y=0.0, z=0.0, mirror=False):
    """Stands a piece drawn in XY up along Z, then moves it; mirror flips it across the blade."""
    obj.rotation_euler.x = math.radians(90)
    if mirror:
        obj.scale.x = -1
    obj.location = (x, y, z)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.select_set(False)
    return obj


def mirrored(half):
    """A symmetric outline from its right half, drawn from the base up to the tip (last point on x=0)."""
    left = [(-x, y) for x, y in reversed(half[:-1])]
    return half + left


def blade(outline, thickness, mat, edge=None, name="blade"):
    """The blade: thick down the middle, thin at the rim unless edge is 0."""
    edge = thickness * 0.5 if edge is None else edge
    return stand(t.profile_solid(name, outline, thickness, mat, edge=edge))


def glowing_edge(outline, thickness, steel, glow, inset=0.72):
    """
    A blade whose rim glows: a thin glowing shape of the full outline, and the steel inside it
    a little narrower and thicker, so only the edge shows the light.
    """
    core = [(x * inset, y) for x, y in outline]
    return [
        blade(outline, thickness * 0.55, glow, edge=0, name="edge"),
        blade(core, thickness, steel, name="blade"),
    ]


def fuller(bottom, top, width, thickness, mat):
    """A groove or an inlay down the middle of both flats."""
    return stand(t.profile_solid("fuller", [
        (-width, bottom), (width, bottom), (width * 0.8, top), (0, top + width * 2), (-width * 0.8, top),
    ], thickness, mat))


def crossguard(width, height, depth, mat, droop=0.0, flare=0.0, at=0.0, bevel=0.012):
    """A bar across the blade's foot; droop bends its ends down, flare widens them."""
    w, h = width, height
    outline = [
        (-w, at - droop - h * (0.5 + flare)), (-w * 0.6, at - h * 0.5), (w * 0.6, at - h * 0.5),
        (w, at - droop - h * (0.5 + flare)), (w, at - droop + h * (0.5 + flare)),
        (w * 0.6, at + h * 0.5), (-w * 0.6, at + h * 0.5), (-w, at - droop + h * (0.5 + flare)),
    ]
    return stand(t.profile_solid("guard", outline, depth, mat, bevel=bevel * 1.6, segments=3))


def grip(length, radius, leather, wrap=None, wraps=4, bottom=None, sides=16):
    """The grip, from the guard down; bound in `wraps` bands of `wrap`."""
    bottom = -length if bottom is None else bottom
    parts = [t.cylinder("grip", radius, length, leather, at=(0, 0, bottom + length / 2), sides=sides)]

    if wrap is not None:
        for i in range(wraps):
            z = bottom + length * (i + 0.5) / wraps
            parts.append(t.cylinder(f"wrap_{i}", radius * 1.15, length * 0.07, wrap, at=(0, 0, z), sides=sides))

    return parts


def pommel(kind, size, mat, at, gem=None):
    """The knob at the grip's end: a disc, a ball, a faceted stone, or a cup holding a gem."""
    if kind == "disc":
        return [t.cylinder("pommel", size, size * 0.7, mat, at=(0, 0, at), sides=20, axis="Y", bevel=size * 0.15)]
    if kind == "ball":
        return [t.sphere("pommel", size, mat, at=(0, 0, at), subdivisions=2)]
    if kind == "facet":
        return [t.sphere("pommel", size, mat, at=(0, 0, at), subdivisions=0, squash=(1, 1, 1.3))]
    if kind == "cup":
        return [
            t.cylinder("cup", size * 0.8, size * 0.8, mat, at=(0, 0, at + size * 0.3), sides=12, radius_top=size * 0.6),
            t.sphere("gem", size, gem or mat, at=(0, 0, at - size * 0.4), subdivisions=0, squash=(0.9, 0.9, 1.3)),
        ]
    raise ValueError(kind)


def finish(name, parts, folder="weapons"):
    # The hand holds the origin (2026-09-30): the sword is moved so the origin falls on its
    # grip, not on its guard — six tenths up a one-handed grip, as the KayKit swords the hand
    # was made for, four tenths up a great sword's, leaving room for the other hand above.
    grip_part = next((p for p in parts if p.name == "grip"), None)
    if grip_part is not None:
        zs = [(grip_part.matrix_world @ v.co).z for v in grip_part.data.vertices]
        bottom, length = min(zs), max(zs) - min(zs)
        offset = -(bottom + length * (0.4 if length >= 0.38 else 0.6))
        for part in parts:
            part.location.z += offset

    # Smooth where it is round (grips, pommels, rings), crisp where a blade is ground: the
    # angle is low enough that the bevel down to the edge stays a hard line.
    t.soften(t.join(name, parts), angle=30)
    return {"folder": folder, "name": name}


def variant(source, name, great):
    """Sword `source` (a model's file name) for the other number of hands, as model `name`
    (2026-10-02: every sword has its great sword and every great sword its sword).

    The same pieces, stretched or shrunk about the guard: the blade to a great sword's reach or
    back to a sword's, the grip to two hands' length or to one's, everything a little wider or
    narrower with it — so the pair read as one smith's work at a glance.
    """
    import importlib

    import bpy

    caught = {}
    kit = globals()
    kept = kit["finish"]
    kit["finish"] = lambda _name, parts, folder="weapons": caught.update(parts=parts, folder=folder)
    try:
        importlib.import_module(source).build()
    finally:
        kit["finish"] = kept

    parts = caught["parts"]
    bpy.context.view_layer.update()

    def heights(part):
        return [(part.matrix_world @ v.co).z for v in part.data.vertices]

    grip_part = next(p for p in parts if p.name == "grip")
    grip_length = max(heights(grip_part)) - min(heights(grip_part))
    reach = max(max(heights(p)) for p in parts)

    up = (1.95 if great else 1.40) / reach
    down = (0.40 if great else 0.30) / grip_length
    wide = 1.18 if great else 0.85

    for part in parts:
        world, back = part.matrix_world.copy(), part.matrix_world.inverted()
        for v in part.data.vertices:
            p = world @ v.co
            p.x *= wide
            p.y *= wide
            p.z *= up if p.z > 0 else down
            v.co = back @ p

    return kept(name, parts, caught["folder"])


def _pieces(source):
    """The pieces model `source` is made of, before they are joined, and the folder it goes to."""
    import importlib

    caught = {}
    kit = globals()
    kept = kit["finish"]
    kit["finish"] = lambda _name, parts, folder="weapons": caught.update(parts=parts, folder=folder)
    try:
        importlib.import_module(source).build()
    finally:
        kit["finish"] = kept

    bpy.context.view_layer.update()
    return caught["parts"], caught["folder"]


def cursed(source, name):
    """Sword `source` re-forged for the Asura (CLS-05, 2026-10-02) as model `name`: the same
    pieces in dark steel, the hilt in blood-dark iron and crimson cord, and violet light down the
    blade — its groove if it has one, an inlay of ours if not."""
    parts, folder = _pieces(source)

    steel = t.material("hex_blade", (0.15, 0.13, 0.20), metal=0.6, rough=0.35)
    glow = t.material("hex_glow", (0.62, 0.30, 1.0), glow=1.1)
    iron = t.material("hex_iron", (0.24, 0.10, 0.14), metal=0.55, rough=0.45)
    hide = t.material("hex_grip", (0.10, 0.08, 0.10))
    cord = t.material("hex_cord", (0.50, 0.08, 0.12))

    lit = False
    for part in parts:
        base = part.name.split(".")[0]
        if base in ("fuller", "edge", "gem"):
            mat, lit = glow, True
        elif base == "blade":
            mat = steel
        elif base == "grip":
            mat = hide
        elif base.startswith("wrap"):
            mat = cord
        else:
            mat = iron
        part.data.materials.clear()
        part.data.materials.append(mat)

    if not lit:
        blade_part = next(p for p in parts if p.name.split(".")[0] == "blade")
        zs = [(blade_part.matrix_world @ v.co).z for v in blade_part.data.vertices]
        ys = [abs((blade_part.matrix_world @ v.co).y) for v in blade_part.data.vertices]
        parts.append(fuller(0.12, max(zs) * 0.62, 0.022, max(ys) * 2 + 0.006, glow))

    return finish(name, parts, folder)
