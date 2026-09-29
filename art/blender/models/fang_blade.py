"""The Fang Blade: an act-2 sword from the spider lands (PIP-02 test piece).

Sized and placed as the KayKit one-handed sword it would replace in the hand: the grip at the
origin, the blade up to 1.4, the guard about 0.5 across.
"""

import math

import bpy

import temins as t


def stand(obj):
    """Outlines are drawn in XY; the sword stands along Z with its flat facing Y."""
    obj.rotation_euler.x = math.radians(90)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(rotation=True)
    obj.select_set(False)
    return obj


def build():
    steel = t.material("steel", metal=0.6, rough=0.35)
    chitin = t.material("chitin", rough=0.45)
    leather = t.material("leather")
    wrap = t.material("leather_dark")
    venom = t.material("venom", (0.35, 0.85, 0.12), glow=0.9, rough=0.3)

    # The blade: a fang, straight on one edge, with a barb near its middle on the other.
    blade = stand(t.profile_solid("blade", [
        (-0.13, 0.10), (0.13, 0.10),
        (0.15, 0.42), (0.145, 0.78), (0.11, 1.08), (0.055, 1.30), (0.0, 1.42),
        (-0.045, 1.22), (-0.08, 1.00), (-0.095, 0.80), (-0.20, 0.66), (-0.115, 0.58),
        (-0.13, 0.34),
    ], thickness=0.10, mat=steel, edge=0.05))

    # A line of venom down the middle of both flats.
    inlay = stand(t.profile_solid("inlay", [
        (-0.026, 0.18), (0.026, 0.18), (0.02, 0.92), (0.0, 1.06), (-0.02, 0.92),
    ], thickness=0.108, mat=venom))

    # The guard: a knot of chitin with two legs each side bending down.
    guard = [t.box("guard", (0.22, 0.14, 0.14), chitin, at=(0, 0, 0.05), bevel=0.03)]

    for side in (-1, 1):
        for i, (reach, drop) in enumerate(((0.30, -0.08), (0.24, -0.15))):
            leg = t.profile_solid(f"leg_{side}_{i}", [
                (0.0, 0.03), (reach * 0.55, 0.09 - i * 0.02), (reach, drop + 0.04),
                (reach + 0.01, drop), (reach * 0.5, 0.01 - i * 0.03), (0.0, -0.04),
            ], thickness=0.065 - i * 0.01, mat=chitin, bevel=0.006)
            stand(leg)
            leg.scale.x = side
            leg.location.z = 0.06 - i * 0.02
            leg.location.y = 0.02 * (1 if i == 0 else -1)
            bpy.context.view_layer.objects.active = leg
            leg.select_set(True)
            bpy.ops.object.transform_apply(location=True, scale=True)
            leg.select_set(False)
            guard.append(leg)

    # The grip, bound in dark leather.
    grip = [t.cylinder("grip", 0.042, 0.30, leather, at=(0, 0, -0.14), sides=8)]
    for i in range(4):
        grip.append(t.cylinder(f"wrap_{i}", 0.048, 0.022, wrap, at=(0, 0, -0.26 + i * 0.08), sides=8))

    # The pommel: a chitin cup holding a venom stone.
    cup = t.cylinder("cup", 0.06, 0.06, chitin, at=(0, 0, -0.31), sides=6, radius_top=0.045)
    gem = t.sphere("gem", 0.075, venom, at=(0, 0, -0.39), subdivisions=0, squash=(0.9, 0.9, 1.25))

    t.join("fang_blade", [blade, inlay, *guard, *grip, cup, gem])

    return {"folder": "weapons", "name": "fang_blade"}
