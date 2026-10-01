"""The parts the Ninja's armours are made of (CLS-03, 2026-10-01): clothes and pieces of armour
worn over the Ninja's own dark clothes, which stay on under them (visuals.json "own_clothes").

Built with armourkit, the same way — rigid pieces, each riding one bone — but round the Rogue's
body, which the Ninja wears, not the Knight's: the chest about 0.40 across and 0.34 deep, the
belt from z 0.50 to 0.67, the legs 0.165 either side of the middle with the shin from 0.05 to
0.33, the forearm as the Knight's. Nothing covers the waist between the belt and the chest,
bare on the woman, and nothing goes round the head: the man's hood and the woman's hair stay.
"""

import math
import os

from mathutils import Vector

import armourkit as a
import temins as t

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
WEARER = os.path.join(ROOT, "game", "assets", "temins", "characters", "ninja_woman_body.glb")

# Half the width and depth of what goes round the chest, and how far out a leg stands.
RX, RY = 0.42, 0.365
LEG = 0.165


def sides(parts):
    """The left pieces and their mirror for the right."""
    return parts, [a.mirror_x(p) for p in parts]


def strip(name, base, tip, width, mat, thick=0.022, bevel=0.006):
    """A flat ribbon from `base` to `tip`, its face turned to the front and back."""
    base, tip = Vector(base), Vector(tip)
    obj = t.box(name, (width, thick, (tip - base).length), mat, bevel=bevel)
    obj.rotation_euler = (tip - base).to_track_quat("Z", "Y").to_euler()
    obj.location = (base + tip) / 2
    return obj


def vest(mat, hem=None, z0=0.84, z1=1.21, top=0.90, grow=0.0):
    """A vest from under the chest to the shoulders, as (the spine's half, the chest's half)."""
    rx, ry = RX + grow, RY + grow
    low = [a.band("vest_low", rx, ry, z0, 0.99, mat, y=0.02)]
    high = [a.band("vest_top", rx, ry, 0.985, z1, mat, top=top, y=0.02)]
    if hem is not None:
        low.append(a.band("vest_hem", rx + 0.007, ry + 0.007, z0 - 0.005, z0 + 0.03, hem, y=0.02, bevel=0.006))
    return low, high


def binding(mat, z0=0.85, z1=1.05, count=3):
    """Cloth wound round the chest in `count` turns."""
    step = (z1 - z0) / count
    return [a.roll(f"binding_{i}", 0.405 + 0.004 * (i % 2), 0.35 + 0.004 * (i % 2), z0 + i * step, z0 + (i + 1) * step - 0.008, mat, y=0.01)
            for i in range(count)]


def sash(mat, tail=None, z0=0.60, z1=0.71, side=1, lengths=(0.30, 0.22), knot=True):
    """Cloth tied round the waist over the belt, knotted at one hip, its ends hanging."""
    parts = [a.roll("sash", 0.408, 0.348, z0, z1, mat, y=0.0)]
    if knot:
        x, y, z = side * 0.31, -0.25, (z0 + z1) / 2
        parts.append(t.sphere("knot", 0.062, mat, at=(x, y - 0.02, z), subdivisions=2, squash=(1, 0.8, 1)))
        for i, length in enumerate(lengths):
            lean = side * (0.02 + 0.05 * i)
            parts.append(strip(f"sash_end_{i}", (x, y - 0.035, z), (x + lean, y - 0.06 - 0.01 * i, z - length), 0.075, tail or mat))
    return parts


def tails(mat, length=0.55, width=0.13, top=1.17, y=0.385, spread=0.13, tip=None):
    """Two ends of a scarf down the back, a little apart; `tip` colours their last tenth."""
    parts = []
    for i, side in enumerate((-1, 1)):
        base = Vector((side * 0.06, y, top))
        end = Vector((side * spread, y + 0.14 + 0.03 * i, top - length * (1 - 0.12 * i)))
        parts.append(strip(f"tail_{i}", base, end, width, mat))
        if tip is not None:
            parts.append(strip(f"tail_tip_{i}", base + (end - base) * 0.86, base + (end - base) * 1.02, width + 0.012, tip, thick=0.03))
    parts.append(t.box("tail_knot", (0.17, 0.05, 0.07), mat, at=(0, y - 0.005, top), bevel=0.02))
    return parts


def collar(mat, z0=1.13, z1=1.24, rx=0.27, ry=0.25):
    """A scarf round the neck, under the chin."""
    return [a.roll("collar", rx, ry, z0, z1, mat, y=0.0)]


def pad(mat, rim=None, size=0.19, at=(0.41, 0.0, 1.20), flat=0.6):
    """A shoulder guard for the left shoulder: a shallow dome, with a rim under it."""
    parts = [t.sphere("pad", size, mat, at=at, subdivisions=2, squash=(1.05, 0.95, flat))]
    if rim is not None:
        parts.append(t.sphere("pad_rim", size * 1.03, rim, at=(at[0], at[1], at[2] - 0.022), subdivisions=2, squash=(1.05, 0.95, flat * 0.6)))
    return parts


def wraps(mat, x0=0.50, x1=0.76, r=0.142, count=4):
    """Bandage wound up the left forearm."""
    step = (x1 - x0) / count
    return [a.sleeve(f"wrap_{i}", x0 + i * step, x0 + (i + 1) * step - 0.012, r + 0.005 * (i % 2), r + 0.005 * (i % 2), mat, bevel=0.007)
            for i in range(count)]


def bracer(mat, trim, plate=None, x0=0.50, x1=0.76, r=0.148):
    """A guard round the left forearm, banded at both ends; `plate` lays a splint along its back."""
    parts = [
        a.sleeve("bracer", x0, x1, r, r - 0.008, mat),
        a.sleeve("bracer_a", x0, x0 + 0.03, r + 0.008, r + 0.008, trim, bevel=0),
        a.sleeve("bracer_b", x1 - 0.03, x1, r + 0.002, r + 0.002, trim, bevel=0),
    ]
    if plate is not None:
        parts.append(t.box("splint", (x1 - x0 - 0.05, 0.13, 0.03), plate, at=((x0 + x1) / 2, 0, 1.11 + r), bevel=0.012))
    return parts


def shin(mat, trim=None, plate=None, z0=0.10, z1=0.31, r=0.128, turns=0):
    """A guard round the left shin: `turns` of bandage, or a sleeve with bands and a front plate."""
    if turns:
        step = (z1 - z0) / turns
        return [t.cylinder(f"shin_{i}", r + 0.004 * (i % 2), step - 0.01, mat, at=(LEG, 0.005, z0 + (i + 0.5) * step), sides=18, bevel=0.007)
                for i in range(turns)]
    parts = [t.cylinder("shin", r, z1 - z0, mat, at=(LEG, 0.005, (z0 + z1) / 2), sides=18, bevel=0.008)]
    if trim is not None:
        parts += [t.cylinder(f"shin_band_{i}", r + 0.007, 0.028, trim, at=(LEG, 0.005, z), sides=18)
                  for i, z in enumerate((z0 + 0.03, z1 - 0.03))]
    if plate is not None:
        parts.append(t.box("shin_plate", (0.12, 0.035, z1 - z0 + 0.03), plate, at=(LEG, -r + 0.002, (z0 + z1) / 2 + 0.015), bevel=0.014))
    return parts


def tassets(mat, trim=None, z=0.60, drop=0.17, count=2, wide=0.15):
    """Plates hanging from the waist over each hip and thigh, front and side."""
    parts = []
    for side in (-1, 1):
        parts.append(t.box(f"tasset_side_{side}", (0.035, wide + 0.04, drop), mat, at=(side * 0.415, 0.02, z - drop / 2), bevel=0.012))
        if count > 1:
            parts.append(t.box(f"tasset_front_{side}", (wide, 0.035, drop * 0.9), mat, at=(side * 0.20, -0.335, z - drop * 0.45), bevel=0.012))
        if trim is not None:
            parts.append(t.box(f"tasset_edge_{side}", (0.042, wide + 0.047, 0.03), trim, at=(side * 0.415, 0.02, z - drop + 0.012), bevel=0.006))
    return parts


def straps(mat, z0=0.86, z1=1.19, width=0.06):
    """Two straps crossing on the chest and on the back."""
    parts = []
    for y, tag in ((-RY - 0.012, "front"), (RY + 0.045, "back")):
        for side in (-1, 1):
            parts.append(strip(f"strap_{tag}_{side}", (side * 0.27, y, z0), (-side * 0.22, y * 0.93, z1), width, mat, thick=0.03))
    return parts


def finish(name, by_bone):
    """As armourkit's, worn over the Ninja's clothes: nothing under it, shown on the Ninja."""
    return a.finish(name, by_bone, underlay=False, reference=WEARER, replaces=[], focus=((0, 0, 0.75), 0.95))
