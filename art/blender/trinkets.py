"""Gear that is never drawn on the character, built only to be drawn as its bag icon (2026-09-30).

Shields, boots, bracelets, necklaces, earrings and rings. Each builder puts one item in an
empty scene, in the same palette and finish as the worn gear; icons.py renders it. Nothing
here is exported to the game.
"""

import math

import bpy
from mathutils import Quaternion, Vector

import armourkit as a
import temins as t


# ---------------------------------------------------------------------------- materials

def mats():
    return {
        "tin": t.material("tin", (0.62, 0.62, 0.60), metal=0.5, rough=0.5),
        "silver": t.material("silver", (0.82, 0.84, 0.88), metal=0.85, rough=0.25),
        "copper": t.material("copper", (0.80, 0.46, 0.28), metal=0.75, rough=0.35),
        "gold": t.material("gold", metal=0.8, rough=0.3),
        "bronze": t.material("bronze", metal=0.6, rough=0.45),
        "iron": t.material("iron_plate", (0.55, 0.56, 0.60), metal=0.6, rough=0.4),
        "dark": t.material("iron", metal=0.5, rough=0.55),
        "blue": t.material("warden_steel", (0.55, 0.66, 0.86), metal=0.7, rough=0.3),
        "ash": t.material("ash_steel", (0.20, 0.19, 0.20), metal=0.5, rough=0.45),
        "leather": t.material("leather"),
        "leather_dark": t.material("leather_dark"),
        "wood": t.material("wood"),
        "wood_dark": t.material("wood_dark"),
        "bone": t.material("bone", rough=0.7),
        "sand": t.material("sand", (0.78, 0.64, 0.42), rough=0.9),
        "fur": t.material("fur", (0.66, 0.58, 0.48), rough=1.0),
        "chitin": t.material("chitin", rough=0.45),
        "cord": t.material("cord", (0.35, 0.25, 0.16), rough=0.9),
        "ember": t.material("ember", glow=1.4, rough=0.4),
        "ice": t.material("ice", (0.55, 0.85, 1.0), glow=0.8, rough=0.2),
        "jade": t.material("jade", (0.30, 0.72, 0.46), glow=0.15, rough=0.2),
        "storm": t.material("storm", (0.45, 0.50, 1.0), glow=0.9, rough=0.15),
        "violet": t.material("crystal", (0.55, 0.28, 1.0), glow=0.7, rough=0.15),
        "crimson": t.material("crimson", (0.50, 0.07, 0.06), rough=0.8),
        # Act 2 (A2-03): the Stonewood's slate and crystal, the Cinderlands' black iron and open fire.
        "slate": t.material("slate", (0.30, 0.32, 0.34), rough=0.7),
        "crystal": t.material("stone_crystal", (0.25, 0.90, 0.82), glow=0.9, rough=0.15),
        "cinder": t.material("cinder_iron", (0.07, 0.06, 0.06), metal=0.55, rough=0.5),
        "lava": t.material("lava", (1.0, 0.36, 0.06), glow=1.6, rough=0.4),
    }


# ---------------------------------------------------------------------------- shapes

def torus(name, major, minor, mat, at=(0, 0, 0), rot=(0, 0, 0), segments=40, ring=12):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=segments,
                                     minor_segments=ring, location=at, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def gem(name, size, mat, at, squash=(1, 1, 1.3)):
    return t.sphere(name, size, mat, at=at, subdivisions=0, squash=squash)


def chain(name, mat, points, link=0.06, wire=0.014):
    """Links along a path, every other one turned a quarter, each lying along the path."""
    links = []
    path = [Vector(p) for p in points]
    total = sum((q - p).length for p, q in zip(path, path[1:]))
    count = max(2, int(total / (link * 0.8)))
    for i in range(count):
        d = total * (i + 0.5) / count
        for p, q in zip(path, path[1:]):
            seg = (q - p).length
            if d <= seg:
                at = p + (q - p) * (d / seg)
                along = (q - p).normalized()
                break
            d -= seg
        quat = along.to_track_quat("X", "Z")
        obj = torus(f"{name}_{i}", link * 0.5, wire, mat, at=at, segments=12, ring=6)
        obj.rotation_mode = "QUATERNION"
        obj.rotation_quaternion = quat @ Quaternion((1, 0, 0), math.pi / 2) if i % 2 else quat
        links.append(obj)
    return links


def u_path(width, depth, drop, steps=12):
    """A necklace hanging in a U, seen from the front: from one shoulder, down, up to the other."""
    pts = []
    for i in range(steps + 1):
        u = i / steps * 2 - 1
        pts.append((u * width, 0, -drop * (1 - u * u) ** 0.8 - depth))
    return pts


def disc(name, radius, depth, mat, at=(0, 0, 0), sides=40, bevel=0.01, axis="Y"):
    return t.cylinder(name, radius, depth, mat, at=at, sides=sides, axis=axis, bevel=bevel)


def boot(name, m_leg, m_foot, x, tall=0.34, shaft=0.085, cuff=None, sole=None):
    """One boot: a shaft, a foot turned forward (-Y), a sole, a cuff at the top."""
    s = sole or m_foot
    parts = [
        t.cylinder(f"{name}_shaft", shaft, tall, m_leg, at=(x, 0, 0.1 + tall / 2), sides=24, radius_top=shaft * 1.08, bevel=0.01),
        t.box(f"{name}_foot", (0.16, 0.26, 0.11), m_foot, at=(x, -0.07, 0.07), bevel=0.04, segments=3),
        t.box(f"{name}_sole", (0.17, 0.28, 0.035), s, at=(x, -0.075, 0.018), bevel=0.012, segments=2),
    ]
    if cuff is not None:
        parts.append(t.cylinder(f"{name}_cuff", shaft * 1.22, 0.07, cuff, at=(x, 0, 0.1 + tall), sides=24, bevel=0.02))
    return parts


# ---------------------------------------------------------------------------- the items

def shd_wooden_buckler(m):
    parts = [disc("face", 0.42, 0.06, m["wood"])]
    for i in range(-2, 3):
        parts.append(t.box(f"plank_{i}", (0.012, 0.064, 0.78 - abs(i) * 0.1), m["wood_dark"], at=(i * 0.16, 0, 0)))
    parts += [
        torus("rim", 0.42, 0.028, m["dark"], rot=(math.pi / 2, 0, 0)),
        t.sphere("boss", 0.12, m["iron"], at=(0, -0.03, 0), subdivisions=2, squash=(1, 0.6, 1), smooth=True),
    ]
    return parts


def shd_banded_shield(m):
    parts = [disc("face", 0.46, 0.07, m["wood"])]
    for z in (-0.2, 0.2):
        w = (0.46 ** 2 - z ** 2) ** 0.5
        parts.append(t.box(f"band_{z}", (w * 2, 0.08, 0.06), m["iron"], at=(0, 0, z), bevel=0.01))
    parts += [
        t.box("band_v", (0.06, 0.08, 0.9), m["iron"], bevel=0.01),
        torus("rim", 0.46, 0.03, m["iron"], rot=(math.pi / 2, 0, 0)),
        t.sphere("boss", 0.1, m["dark"], at=(0, -0.04, 0), subdivisions=2, squash=(1, 0.6, 1), smooth=True),
    ]
    for i in range(8):
        a_ = i * math.pi / 4
        parts.append(t.sphere(f"rivet_{i}", 0.022, m["dark"], at=(0.38 * math.cos(a_), -0.045, 0.38 * math.sin(a_)), subdivisions=1))
    return parts


def kite(name, mat, width, top, bottom, depth=0.07, bevel=0.02):
    """A kite shield's face, drawn in XZ and facing -Y."""
    outline = [(-width, top), (width, top), (width, top - 0.35), (0.0, bottom), (-width, top - 0.35)]
    obj = t.profile_solid(name, outline, depth, mat, bevel=bevel, segments=2)
    obj.rotation_euler.x = math.radians(90)
    return obj


def shd_ridge_bulwark(m):
    return [
        kite("face", m["sand"], 0.36, 0.5, -0.6),
        kite("frame", m["dark"], 0.40, 0.54, -0.66, depth=0.05),
        t.box("stripe", (0.1, 0.06, 0.9), m["chitin"], at=(0, -0.02, -0.02), bevel=0.02, segments=2),
        a.spike("barb", (0, -0.04, 0.12), (0, -0.2, 0.12), 0.06, m["chitin"], sides=6),
    ]


def shd_kiln_ward(m):
    parts = [
        kite("face", m["ash"], 0.38, 0.52, -0.6),
        kite("frame", m["gold"], 0.42, 0.56, -0.66, depth=0.05),
        disc("furnace", 0.16, 0.05, m["gold"], at=(0, -0.04, 0.1)),
        disc("heart", 0.1, 0.06, m["ember"], at=(0, -0.05, 0.1)),
    ]
    for side in (-1, 1):
        parts.append(t.box(f"bar_{side}", (0.035, 0.05, 0.5), m["gold"], at=(side * 0.24, -0.03, 0.0), bevel=0.01))
    return parts


def bts_worn_boots(m):
    parts = []
    for x in (-0.12, 0.12):
        parts += boot(f"b{x}", m["leather"], m["leather"], x, tall=0.28, sole=m["leather_dark"])
        parts.append(t.box(f"patch{x}", (0.07, 0.02, 0.07), m["leather_dark"], at=(x + 0.02, -0.09, 0.28), bevel=0.01))
    return parts


def bts_scout_boots(m):
    parts = []
    for x in (-0.12, 0.12):
        parts += boot(f"b{x}", m["leather"], m["leather"], x, tall=0.42, cuff=m["fur"], sole=m["leather_dark"])
        for z in (0.22, 0.36):
            parts.append(t.cylinder(f"strap{x}{z}", 0.095, 0.03, m["leather_dark"], at=(x, 0, z), sides=24))
    return parts


def bts_ridge_greaves(m):
    parts = []
    for x in (-0.12, 0.12):
        parts += boot(f"b{x}", m["sand"], m["leather"], x, tall=0.42, sole=m["leather_dark"])
        parts.append(t.cylinder(f"greave{x}", 0.1, 0.26, m["iron"], at=(x, -0.012, 0.34), sides=24, radius_top=0.11, bevel=0.01))
        parts.append(t.sphere(f"knee{x}", 0.08, m["iron"], at=(x, -0.06, 0.5), subdivisions=2, squash=(1, 0.8, 1), smooth=True))
    return parts


def bts_ashen_striders(m):
    parts = []
    for x in (-0.12, 0.12):
        parts += boot(f"b{x}", m["ash"], m["ash"], x, tall=0.44, sole=m["dark"])
        parts.append(t.cylinder(f"seam{x}", 0.092, 0.025, m["ember"], at=(x, 0, 0.3), sides=24))
        parts.append(t.sphere(f"knee{x}", 0.085, m["ash"], at=(x, -0.06, 0.52), subdivisions=2, squash=(1, 0.8, 1), smooth=True))
        parts.append(a.spike(f"spike{x}", (x, -0.1, 0.54), (x, -0.22, 0.62), 0.035, m["dark"]))
    return parts


def cuff(name, mat, radius=0.13, height=0.3, top=1.0, sides=32):
    """A bracelet or vambrace standing upright: a short open tube."""
    return a.gap_band(name, radius, radius, 0, height, mat, gap=0.1, sides=sides, top=top, thickness=0.03)


def brc_leather_band(m):
    return [
        cuff("band", m["leather"], height=0.2),
        t.cylinder("strap", 0.137, 0.04, m["leather_dark"], at=(0, 0, 0.1), sides=32),
        t.box("buckle", (0.06, 0.03, 0.06), m["bronze"], at=(0, -0.145, 0.1), bevel=0.01),
    ]


def brc_iron_bracer(m):
    parts = [cuff("bracer", m["iron"], height=0.34, top=0.88)]
    for z in (0.05, 0.29):
        parts.append(t.cylinder(f"rim{z}", 0.135 * (1 - 0.12 * z / 0.34), 0.03, m["dark"], at=(0, 0, z), sides=32))
    for i in range(6):
        a_ = i * math.pi / 3
        parts.append(t.sphere(f"rivet{i}", 0.018, m["dark"], at=(0.135 * math.sin(a_), -0.135 * math.cos(a_), 0.17), subdivisions=1))
    return parts


def brc_warden_vambrace(m):
    return [
        cuff("vambrace", m["blue"], height=0.4, top=0.85),
        t.cylinder("cuff_top", 0.118, 0.035, m["gold"], at=(0, 0, 0.39), sides=32),
        t.cylinder("cuff_bottom", 0.137, 0.035, m["gold"], at=(0, 0, 0.02), sides=32),
        gem("stone", 0.04, m["ice"], at=(0, -0.13, 0.2)),
    ]


def brc_kiln_coil(m):
    parts = []
    turns, steps = 3, 60
    prev = None
    for i in range(steps + 1):
        a_ = 2 * math.pi * turns * i / steps
        p = (0.13 * math.sin(a_), -0.13 * math.cos(a_), 0.03 + 0.3 * i / steps)
        if prev is not None:
            parts.append(a.rod(f"coil{i}", prev, p, 0.022, m["gold"], sides=10))
            parts.append(t.sphere(f"joint{i}", 0.022, m["gold"], at=p, subdivisions=1))
        prev = p
    for z in (0.08, 0.18, 0.28):
        parts.append(gem(f"ember{z}", 0.032, m["ember"], at=(0, -0.155, z)))
    return parts


def nck_bone_charm(m):
    path = u_path(0.24, 0.0, 0.42)
    parts = []
    for i, (p, q) in enumerate(zip(path, path[1:])):
        parts.append(a.rod(f"cord{i}", p, q, 0.012, m["cord"], sides=6))
    tip = path[len(path) // 2]
    parts.append(a.spike("tooth", (tip[0], 0, tip[2] - 0.02), (tip[0] + 0.02, 0, tip[2] - 0.2), 0.035, m["bone"], sides=8))
    parts.append(t.sphere("bead_l", 0.025, m["bone"], at=(tip[0] - 0.08, 0, tip[2] + 0.01), subdivisions=1))
    parts.append(t.sphere("bead_r", 0.025, m["bone"], at=(tip[0] + 0.08, 0, tip[2] + 0.01), subdivisions=1))
    return parts


def pendant_chain(m, mat):
    path = u_path(0.24, 0.0, 0.40)
    return chain("chain", mat, path), path[len(path) // 2]


def nck_silver_pendant(m):
    links, tip = pendant_chain(m, m["silver"])
    return links + [
        disc("pendant", 0.07, 0.02, m["silver"], at=(tip[0], 0, tip[2] - 0.09)),
        gem("stone", 0.03, m["ice"], at=(tip[0], -0.015, tip[2] - 0.09), squash=(1, 0.6, 1)),
    ]


def nck_shard_locket(m):
    links, tip = pendant_chain(m, m["gold"])
    locket = t.sphere("locket", 0.08, m["gold"], at=(tip[0], 0, tip[2] - 0.1), subdivisions=2, squash=(0.8, 0.35, 1), smooth=True)
    return links + [locket, gem("shard", 0.035, m["violet"], at=(tip[0], -0.03, tip[2] - 0.1), squash=(0.7, 0.5, 1.6))]


def nck_emberheart(m):
    links, tip = pendant_chain(m, m["gold"])
    x, z = tip[0], tip[2] - 0.12
    return links + [
        t.sphere("lobe_l", 0.045, m["ember"], at=(x - 0.035, 0, z + 0.03), subdivisions=2, smooth=True),
        t.sphere("lobe_r", 0.045, m["ember"], at=(x + 0.035, 0, z + 0.03), subdivisions=2, smooth=True),
        a.spike("point", (x, 0, z + 0.02), (x, 0, z - 0.08), 0.07, m["ember"], sides=12),
        torus("setting", 0.1, 0.01, m["gold"], at=(x, 0, z), rot=(math.pi / 2, 0, 0)),
    ]


def ring(m, band, major=0.16, minor=0.035):
    return torus("band", major, minor, band, rot=(math.radians(70), 0, 0))


def rng_tin_band(m):
    return [ring(m, m["tin"], minor=0.04)]


def rng_hunters_signet(m):
    return [
        ring(m, m["bronze"]),
        disc("signet", 0.07, 0.04, m["bronze"], at=(0, -0.05, 0.16), axis="Z", sides=24),
        t.box("mark", (0.07, 0.07, 0.01), m["leather_dark"], at=(0, -0.05, 0.185)),
    ]


def rng_warden_seal(m):
    return [
        ring(m, m["blue"]),
        t.box("seal", (0.11, 0.11, 0.05), m["gold"], at=(0, -0.05, 0.16), bevel=0.012, segments=2),
        gem("stone", 0.04, m["ice"], at=(0, -0.05, 0.2), squash=(1, 1, 0.8)),
    ]


def rng_ashen_circle(m):
    parts = [ring(m, m["ash"], minor=0.04), torus("inlay", 0.16, 0.012, m["ember"], rot=(math.radians(70), 0, 0))]
    for i in range(6):
        a_ = i * math.pi / 3
        c = Vector((0.16 * math.cos(a_), 0.16 * math.sin(a_) * math.cos(math.radians(70)), 0.16 * math.sin(a_) * math.sin(math.radians(70))))
        parts.append(a.spike(f"spike{i}", tuple(c), tuple(c * 1.35), 0.02, m["dark"], sides=5))
    return parts


def rng_kiln_sigil(m):
    parts = [ring(m, m["gold"]), gem("stone", 0.06, m["ember"], at=(0, -0.05, 0.2))]
    for i in range(4):
        a_ = i * math.pi / 2 + math.pi / 4
        parts.append(a.spike(f"claw{i}", (0.05 * math.cos(a_), -0.05 + 0.05 * math.sin(a_), 0.14),
                             (0.035 * math.cos(a_), -0.05 + 0.035 * math.sin(a_), 0.24), 0.014, m["gold"], sides=5))
    return parts


def hook(m, mat):
    return [
        torus("hook", 0.05, 0.01, mat, at=(0, 0, 0.12), rot=(math.pi / 2, 0, 0), segments=24, ring=8),
        a.rod("wire", (0, 0, 0.07), (0, 0, 0.02), 0.01, mat, sides=6),
    ]


def ear_copper_stud(m):
    return [
        disc("stud", 0.07, 0.03, m["copper"], sides=24),
        t.sphere("dome", 0.045, m["copper"], at=(0, -0.02, 0), subdivisions=2, squash=(1, 0.6, 1), smooth=True),
        a.rod("post", (0, 0.01, 0), (0, 0.1, 0), 0.01, m["copper"], sides=6),
    ]


def ear_jade_drop(m):
    return hook(m, m["gold"]) + [
        t.sphere("drop", 0.06, m["jade"], at=(0, 0, -0.06), subdivisions=2, squash=(0.8, 0.8, 1.2), smooth=True),
        a.spike("tip", (0, 0, -0.02), (0, 0, 0.04), 0.035, m["jade"], sides=12),
    ]


def ear_stormglass(m):
    return hook(m, m["silver"]) + [
        gem("crystal", 0.06, m["storm"], at=(0, 0, -0.07), squash=(0.7, 0.7, 1.8)),
        torus("cap", 0.03, 0.01, m["silver"], at=(0, 0, 0.02), segments=16, ring=6),
    ]


# -- Act 2 (A2-03): one earring from the Stonewood, a shield and boots from the Cinderlands. --

def ear_stoneheart(m):
    """A heart of crystal in a cage of slate, on a silver hook."""
    parts = hook(m, m["silver"]) + [
        gem("heart", 0.075, m["crystal"], at=(0, 0, -0.085), squash=(0.8, 0.8, 1.5)),
        torus("cage_a", 0.062, 0.012, m["slate"], at=(0, 0, -0.085), rot=(math.pi / 2, 0, 0), segments=20, ring=6),
        torus("cage_b", 0.062, 0.012, m["slate"], at=(0, 0, -0.085), rot=(math.pi / 2, 0, math.pi / 2), segments=20, ring=6),
        gem("drop", 0.028, m["crystal"], at=(0, 0, -0.21), squash=(0.7, 0.7, 1.6)),
    ]
    return parts


def shd_cinder_bulwark(m):
    """Black iron cracked through with fire: a kite of cinder, a rim of bronze, the forge's eye at its heart."""
    parts = [
        kite("face", m["cinder"], 0.40, 0.54, -0.64),
        kite("frame", m["bronze"], 0.44, 0.58, -0.70, depth=0.05),
        disc("eye_rim", 0.17, 0.05, m["bronze"], at=(0, -0.04, 0.08)),
        disc("eye", 0.11, 0.06, m["lava"], at=(0, -0.05, 0.08)),
    ]
    # Cracks of fire running out from the eye toward the rim.
    for i, (x, z) in enumerate(((-0.24, 0.40), (0.26, 0.36), (-0.20, -0.26), (0.20, -0.30), (0.0, -0.52), (0.0, 0.46))):
        parts.append(a.spike(f"crack_{i}", (x * 0.45, -0.05, 0.08 + (z - 0.08) * 0.45), (x, -0.045, z), 0.022, m["lava"]))
    for side in (-1, 1):
        parts.append(a.spike(f"horn_{side}", (side * 0.30, -0.02, 0.44), (side * 0.44, -0.02, 0.66), 0.045, m["bronze"]))
    return parts


def bts_ember_treads(m):
    """Tall boots of black iron, fire in the seams and under the sole, a spur of bronze."""
    parts = []
    for x in (-0.12, 0.12):
        parts += boot(f"b{x}", m["cinder"], m["cinder"], x, tall=0.48, sole=m["lava"])
        parts.append(t.cylinder(f"seam_a{x}", 0.092, 0.022, m["lava"], at=(x, 0, 0.24), sides=24))
        parts.append(t.cylinder(f"seam_b{x}", 0.092, 0.022, m["lava"], at=(x, 0, 0.40), sides=24))
        parts.append(t.sphere(f"knee{x}", 0.09, m["bronze"], at=(x, -0.06, 0.56), subdivisions=2, squash=(1, 0.8, 1), smooth=True))
        parts.append(a.spike(f"spur{x}", (x, 0.10, 0.08), (x, 0.24, 0.14), 0.03, m["bronze"]))
    return parts


BUILDERS = {name: fn for name, fn in globals().items()
            if callable(fn) and name.split("_")[0] in ("shd", "bts", "brc", "nck", "ear", "rng")}


def build(item_id):
    """Builds the item in the scene and returns its pieces, joined and shaded like worn gear."""
    parts = BUILDERS[item_id](mats())
    obj = t.join(item_id, parts)
    t.soften(obj, angle=40)
    return obj
