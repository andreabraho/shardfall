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
    # Smooth where it is round (grips, pommels, rings), crisp where a blade is ground: the
    # angle is low enough that the bevel down to the edge stays a hard line.
    t.soften(t.join(name, parts), angle=30)
    return {"folder": folder, "name": name}
