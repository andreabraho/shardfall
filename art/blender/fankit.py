"""The Dragonseer's twelve fans (CLS-06, 2026-10-02), each its own design.

Redrawn at your call ("showy fans"): a feather fan, a round one rimmed in bronze, a war fan of
iron slats, a fan of green leaves, a bat's wing on bone, a fan of blades, of crystal, of flame,
the firebird's tail — painted, rimmed, crested and hung with tassels and beads. Twelve fans
were one paper leaf in other colours before.

A fan is held as a sword is — the grip at the origin, the leaf opening up the Z axis — so the hand
that was rigged for swords holds it unchanged; it is drawn in the XY plane, the pivot at the origin
and the leaf towards +Y, stood up, and turned a quarter turn at the end so the leaf faces forward.
"""

import math

import bpy

import swordkit as k
import temins as t

# Sized to the hand that holds it: the leaves below are drawn larger than they are worn.
SCALE = 0.88


def polar(radius, angle):
    return (radius * math.cos(math.radians(angle)), radius * math.sin(math.radians(angle)))


def arc(radius, start, end, steps=14):
    """Points on a circle about the pivot, by angle from the +X axis, in the XY plane."""
    return [polar(radius, start + (end - start) * i / steps) for i in range(steps + 1)]


def flat(name, outline, thickness, mat, bevel=0.0, edge=0.0):
    """A flat piece drawn in XY, stood up."""
    return k.stand(t.profile_solid(name, outline, thickness, mat, bevel=bevel, edge=edge))


def along(outline, angle):
    """An outline drawn (across, along) laid out from the pivot at `angle`."""
    c, s = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    return [(v * c + u * s, v * s - u * c) for u, v in outline]


# How the leaf's outer edge runs between two ribs: `at` 0 at a rib, 0.5 half way to the next.
def scallop(depth):
    return lambda at, across: 1 - depth * math.sin(math.pi * at)


def petal(depth):
    return lambda at, across: 1 - depth + depth * math.sin(math.pi * at) ** 0.6


def flame(depth):
    return lambda at, across: 1 + depth * (abs(1 - 2 * at) ** 1.5) - depth * 0.5


def torn(depth, seed=1.0):
    return lambda at, across: 1 - depth * (0.5 + 0.5 * math.sin(across * 61.0 * seed) * math.sin(across * 23.0 + seed))


def sector(name, radius, inner, spread, mat, thickness=0.022, ribs=7, edge=None, per=6):
    """The leaf: a sector `spread` degrees wide between `inner` and `radius`, its outer edge cut by `edge`."""
    start = 90 - spread / 2
    count = (ribs - 1) * per
    outer = []
    for i in range(count + 1):
        k_ = edge((i % per) / per, i / count) if edge is not None else 1.0
        outer.append(polar(radius * k_, start + spread * i / count))
    return flat(name, outer + arc(inner, start + spread, start, steps=10), thickness, mat)


def stick(name, angle, length, width, mat, thickness=0.034, base=0.0):
    return flat(name, along([(-width / 2, base), (width / 2, base), (width / 2 * 0.7, length), (-width / 2 * 0.7, length)], angle), thickness, mat, bevel=0.004)


def plume(name, angle, r0, r1, width, mat, thickness=0.024, sharp=False, edge=0.0):
    """A feather, a leaf or a blade lying out along `angle` from r0 to r1."""
    w, span = width / 2, r1 - r0
    if sharp:
        shape = [(0, 0), (w, span * 0.16), (w * 0.9, span * 0.62), (0, span), (-w * 0.9, span * 0.62), (-w, span * 0.16)]
    else:
        shape = [(0, 0), (w * 0.6, span * 0.12), (w, span * 0.42), (w * 0.92, span * 0.70), (w * 0.5, span * 0.92), (0, span),
                 (-w * 0.5, span * 0.92), (-w * 0.92, span * 0.70), (-w, span * 0.42), (-w * 0.6, span * 0.12)]
    return flat(name, along([(u, v + r0) for u, v in shape], angle), thickness, mat, edge=edge)


def disc(name, radius, angle, size, mat, depth=0.04):
    """A round mark on the leaf at `radius` along `angle`."""
    x, z = polar(radius, angle)
    return t.cylinder(name, size, depth, mat, at=(x, 0, z), sides=20, axis="Y")


def crescent(name, radius, angle, size, mat, depth=0.042):
    outer = [(size * math.cos(math.radians(a)), size * math.sin(math.radians(a))) for a in range(-130, 131, 20)]
    inner = [(size * 0.5 + size * 0.86 * math.cos(math.radians(a)), size * 0.86 * math.sin(math.radians(a))) for a in range(118, -119, -20)]
    x, y = polar(radius, angle)
    return flat(name, [(x + px, y + py) for px, py in outer + inner], depth, mat)


def rays(name, radius, angle, size, reach, mat, count=8, depth=0.036):
    """Rays round a mark."""
    x, y = polar(radius, angle)
    parts = []
    for i in range(count):
        a = 360 * i / count
        tip = polar(size + reach, a)
        left, right = polar(size * 0.9, a - 14), polar(size * 0.9, a + 14)
        parts.append(flat(f"{name}_{i}", [(x + left[0], y + left[1]), (x + tip[0], y + tip[1]), (x + right[0], y + right[1])], depth, mat))
    return parts


def tassel(name, x, top, cord, tuft, length=0.20, beads=None, count=2):
    """A cord from the grip's end, beads on it, a tuft at its end."""
    parts = [t.cylinder(f"{name}_cord", 0.011, length, cord, at=(x, 0, top - length / 2), sides=6)]
    if beads is not None:
        for i in range(count):
            parts.append(t.sphere(f"{name}_bead_{i}", 0.03, beads, at=(x, 0, top - length * (i + 1) / (count + 1)), subdivisions=1))
    parts.append(t.sphere(f"{name}_knot", 0.034, cord, at=(x, 0, top - length), subdivisions=1))
    parts.append(t.cylinder(f"{name}_tuft", 0.05, 0.20, tuft, at=(x, 0, top - length - 0.11), sides=10, radius_top=0.02))
    return parts


def handle(grip, metal, length=0.26, radius=0.036, pommel="ball", gem=None):
    """The pivot, the grip under it and its end. The grip must be called `grip` (swordkit.finish)."""
    parts = [t.cylinder("rivet", 0.05, 0.07, metal, at=(0, 0, 0.0), sides=16, axis="Y"),
             t.cylinder("grip", radius, length, grip, at=(0, 0, -length / 2), sides=14)]
    if gem is not None:
        parts.append(t.cylinder("rivet_gem", 0.03, 0.085, gem, at=(0, 0, 0.0), sides=12, axis="Y"))
    if pommel == "ball":
        parts.append(t.sphere("pommel", 0.052, metal, at=(0, 0, -length - 0.02), subdivisions=2))
    elif pommel == "ring":
        import classkit
        parts.append(classkit.hoop("pommel", (0, 0, -length - 0.055), 0.055, 0.016, metal, segments=16, sides=6))
    return parts


def spokes(spread, ribs, radius, mat, guards=None, width=0.018, reach=1.0):
    """The sticks from the pivot out to the leaf's edge; the two outer ones wider, in `guards`."""
    start = 90 - spread / 2
    parts = []
    for i in range(ribs):
        outer = i in (0, ribs - 1)
        parts.append(stick(f"rib_{i}", start + spread * i / (ribs - 1), radius * (1.0 if outer else 0.97) * reach, 0.036 if outer else width,
                           guards if outer and guards is not None else mat, thickness=0.04 if outer else 0.034))
    return parts


def angles(spread, count):
    return [90 - spread / 2 + spread * i / (count - 1) for i in range(count)]


def finish(name, parts):
    # The leaf faces the way the Dragonseer does: a sword's flat is turned to the side, and a fan
    # held so showed only its edge from the front.
    for part in parts:
        part.scale = (SCALE, SCALE, SCALE)
        part.rotation_euler.z = math.pi / 2
        bpy.context.view_layer.objects.active = part
        part.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        part.select_set(False)
    return k.finish(name, parts)


def build(name):
    """The fan called `name`, one of the twelve."""
    m = t.material
    bamboo, dark = m("bamboo", (0.62, 0.50, 0.28)), m("wood_dark")
    paper = m("paper", (0.90, 0.86, 0.74))
    gold = m("gold", metal=0.8, rough=0.3)
    iron = m("fan_iron", (0.20, 0.21, 0.24), metal=0.6, rough=0.45)
    bronze = m("bronze", metal=0.65, rough=0.4)
    silver = m("fan_silver", (0.80, 0.82, 0.86), metal=0.75, rough=0.28)
    red = m("fan_red", (0.62, 0.06, 0.05))
    bone = m("bone")

    def worn():
        # Ventaglio Consumato: a paper fan mended with a patch, its edge frayed, a plain cord.
        old = m("paper_worn", (0.70, 0.62, 0.44))
        parts = [sector("leaf", 0.74, 0.22, 112, old, ribs=6, edge=torn(0.07)),
                 sector("patch", 0.60, 0.40, 26, m("paper_patch", (0.46, 0.34, 0.20)), thickness=0.028, ribs=2),
                 sector("stripe", 0.70, 0.66, 112, m("ink_faded", (0.36, 0.20, 0.14)), thickness=0.027, ribs=6)]
        parts += spokes(112, 6, 0.74, bamboo) + handle(dark, bamboo) + tassel("tassel", 0, -0.30, old, old, length=0.14)
        return parts

    def hunters():
        # Ventaglio del Cacciatore: no paper — seven pheasant feathers bound in leather, a bead of bone.
        brown, tip, bar = m("feather_brown", (0.34, 0.20, 0.10)), m("feather_tip", (0.86, 0.80, 0.66)), m("feather_bar", (0.14, 0.09, 0.06))
        parts = []
        for i, a in enumerate(angles(120, 7)):
            reach = 0.82 - 0.06 * abs(i - 3)
            parts += [plume(f"feather_{i}", a, 0.14, reach, 0.19, brown),
                      plume(f"feather_bar_{i}", a, reach * 0.52, reach * 0.74, 0.15, bar, thickness=0.028),
                      plume(f"feather_tip_{i}", a, reach * 0.72, reach + 0.02, 0.15, tip, thickness=0.03),
                      stick(f"quill_{i}", a, reach * 0.9, 0.014, bone, thickness=0.036)]
        parts.append(sector("binding", 0.24, 0.05, 130, m("leather"), thickness=0.05, ribs=4))
        parts += handle(m("leather"), bone, length=0.28) + tassel("tassel", 0, -0.32, m("leather_dark"), brown, beads=bone, count=2)
        return parts

    def bronze_():
        # Ventaglio di Bronzo: a round fan, a full moon of paper in a bronze rim, the sun on it.
        import classkit
        centre, size = 0.50, 0.40
        ring = [(size * math.cos(math.radians(a)), centre + size * math.sin(math.radians(a))) for a in range(0, 360, 10)]
        parts = [flat("leaf", ring, 0.022, paper),
                 classkit.hoop("rim", (0, 0, centre), size, 0.024, bronze, segments=36, sides=8),
                 t.cylinder("sun", 0.12, 0.04, red, at=(0, 0, centre + 0.04), sides=24, axis="Y"),
                 classkit.hoop("sun_ring", (0, 0, centre + 0.04), 0.19, 0.012, bronze, segments=28, sides=6)]
        parts += rays("ray", centre + 0.04, 90, 0.20, 0.10, bronze, count=12)
        for i, a in enumerate(angles(70, 7)):
            parts.append(stick(f"rib_{i}", a, 0.50, 0.012, bamboo, thickness=0.03, base=0.08))
        parts += [stick("stem", 90, 0.14, 0.05, bronze, thickness=0.05)]
        parts += handle(dark, bronze, length=0.30, pommel="ring") + tassel("tassel", 0, -0.40, red, red, beads=bronze, count=1)
        return parts

    def iron_():
        # Ventaglio di Ferro: a war fan — slats of iron ending in points, black with the red sun.
        ink = m("ink_leaf", (0.06, 0.06, 0.08))
        parts = [sector("leaf", 0.72, 0.22, 126, ink, ribs=8), sector("rim", 0.73, 0.68, 126, iron, thickness=0.03, ribs=8),
                 disc("sun", 0.47, 90, 0.15, red)]
        for i, a in enumerate(angles(126, 8)):
            outer = i in (0, 7)
            parts += [stick(f"rib_{i}", a, 0.72, 0.036 if outer else 0.024, iron, thickness=0.042 if outer else 0.036),
                      plume(f"point_{i}", a, 0.68, 0.90, 0.075, iron, thickness=0.03, sharp=True, edge=0.012)]
        parts += handle(m("fan_wrap", (0.36, 0.06, 0.06)), iron, length=0.28, pommel="ring") + tassel("tassel", 0, -0.38, red, red, length=0.16)
        return parts

    def ridge():
        # Ventaglio della Cresta: the mountain spirit's — seven broad leaves on a long staff.
        green, vein = m("ridge_leaf", (0.10, 0.34, 0.14)), m("ridge_vein", (0.52, 0.66, 0.26))
        parts = []
        for i, a in enumerate(angles(150, 7)):
            reach = 0.86 - 0.07 * abs(i - 3)
            parts += [plume(f"leaf_{i}", a, 0.10, reach, 0.27, green, sharp=True), stick(f"vein_{i}", a, reach * 0.9, 0.016, vein, thickness=0.03)]
        parts.append(disc("boss", 0.0, 90, 0.085, bamboo, depth=0.06))
        parts += handle(bamboo, m("leather"), length=0.36, radius=0.034) + tassel("tassel", 0, -0.42, m("moss"), green, beads=bone, count=3, length=0.24)
        return parts

    def warden():
        # Ventaglio del Custode: blue silk, the moon and the wardens' star in silver, petals of silver round its edge.
        blue, night = m("warden_leaf", (0.08, 0.16, 0.42)), m("warden_night", (0.04, 0.07, 0.22))
        ice = m("warden_ice", (0.55, 0.85, 1.0), glow=1.0, rough=0.2)
        parts = [sector("leaf", 0.80, 0.22, 136, blue, ribs=8, edge=petal(0.07)),
                 sector("rim", 0.82, 0.74, 136, silver, thickness=0.028, ribs=8, edge=petal(0.07)),
                 sector("band", 0.40, 0.32, 136, night, thickness=0.027, ribs=8), sector("band_line", 0.415, 0.40, 136, silver, thickness=0.029, ribs=8),
                 crescent("moon", 0.56, 90, 0.13, silver), disc("star_stone", 0.56, 90, 0.035, ice, depth=0.05)]
        for i, a in enumerate((58, 122)):
            parts += rays(f"star_{i}", 0.58, a, 0.012, 0.055, silver, count=4)
        parts += spokes(136, 8, 0.80, silver) + handle(night, silver, gem=ice)
        parts += tassel("tassel_a", -0.035, -0.28, m("warden_cord", (0.86, 0.88, 0.92)), blue, beads=silver) + tassel("tassel_b", 0.035, -0.28, m("warden_cord", (0.86, 0.88, 0.92)), blue, beads=silver, length=0.28)
        return parts

    def steel_():
        # Ventaglio d'Acciaio: no leaf at all — nine blades on a pivot, sharp to the tip.
        steel = m("steel_leaf", (0.66, 0.68, 0.72), metal=0.7, rough=0.3)
        forged = m("steel_dark", metal=0.5, rough=0.5)
        parts = []
        for i, a in enumerate(angles(140, 9)):
            reach = 0.86 - 0.03 * abs(i - 4)
            parts += [plume(f"blade_{i}", a, 0.16, reach, 0.165, steel, thickness=0.034, sharp=True, edge=0.02),
                      stick(f"spine_{i}", a, reach * 0.62, 0.02, forged, thickness=0.04)]
        parts.append(sector("collar", 0.26, 0.06, 150, forged, thickness=0.05, ribs=5))
        parts += handle(m("leather_dark"), forged, length=0.28, pommel="ring")
        return parts

    def grave():
        # Ventaglio della Tomba: a bat's wing — violet skin stretched between fingers of bone, an eye at the root.
        skin, glow = m("grave_leaf", (0.16, 0.08, 0.26)), m("grave_gem", (0.62, 0.35, 0.95), glow=1.2)
        parts = [sector("leaf", 0.84, 0.20, 140, skin, ribs=7, edge=scallop(0.24)),
                 sector("vein", 0.50, 0.47, 140, m("grave_vein", (0.36, 0.16, 0.56)), thickness=0.026, ribs=7, edge=scallop(0.10))]
        for i, a in enumerate(angles(140, 7)):
            parts += [stick(f"finger_{i}", a, 0.86, 0.034, bone, thickness=0.04), plume(f"claw_{i}", a, 0.84, 0.98, 0.05, bone, thickness=0.034, sharp=True),
                      disc(f"knuckle_{i}", 0.46, a, 0.03, bone, depth=0.05)]
        parts += [disc("eye", 0.30, 90, 0.085, glow, depth=0.05), disc("pupil", 0.30, 90, 0.03, skin, depth=0.058)]
        parts += handle(bone, bone, length=0.28) + tassel("tassel", 0, -0.32, m("grave_cord", (0.40, 0.20, 0.62)), skin, beads=bone, count=2)
        return parts

    def ember():
        # Ventaglio di Brace: a leaf of fire — its edge in tongues, alight along the rim and through the middle.
        coal, fire = m("ember_leaf", (0.36, 0.05, 0.03)), m("ember_glow", (1.0, 0.42, 0.08), glow=1.3)
        char = m("ember_stick", (0.10, 0.07, 0.06))
        parts = [sector("leaf", 0.84, 0.22, 140, coal, ribs=9, edge=flame(0.16)),
                 sector("rim", 0.86, 0.74, 140, fire, thickness=0.018, ribs=9, edge=flame(0.19)),
                 sector("leaf_top", 0.80, 0.22, 140, coal, thickness=0.026, ribs=9, edge=flame(0.16)),
                 sector("band", 0.52, 0.46, 140, fire, thickness=0.03, ribs=9, edge=flame(0.08)),
                 sector("band_low", 0.36, 0.33, 140, fire, thickness=0.03, ribs=9)]
        parts += spokes(140, 9, 0.84, char, reach=1.08) + handle(char, m("fan_iron", (0.20, 0.21, 0.24), metal=0.6, rough=0.45), gem=fire)
        parts += tassel("tassel", 0, -0.28, red, fire, beads=char, count=2)
        return parts

    def hollow():
        # Ventaglio della Conca: black, torn, an eye of red light open on it, a spike at every rib.
        black, glow = m("hollow_leaf", (0.05, 0.05, 0.07)), m("hollow_gem", (1.0, 0.16, 0.12), glow=1.2)
        blood = m("hollow_stick", (0.36, 0.05, 0.07))
        parts = [sector("leaf", 0.86, 0.22, 144, black, ribs=9, edge=torn(0.14, 1.7)),
                 sector("band", 0.30, 0.26, 144, blood, thickness=0.028, ribs=9)]
        eye = [(0.26 * math.cos(math.radians(a)), 0.55 + 0.11 * math.sin(math.radians(a)) * (1 - 0.4 * abs(math.cos(math.radians(a))))) for a in range(0, 360, 15)]
        parts += [flat("eye", eye, 0.034, glow), flat("eye_rim", [(x * 1.14, 0.55 + (y - 0.55) * 1.3) for x, y in eye], 0.028, blood),
                  disc("pupil", 0.55, 90, 0.05, black, depth=0.046)]
        for i, a in enumerate(angles(144, 9)):
            parts += [stick(f"rib_{i}", a, 0.80, 0.022, blood, thickness=0.036), plume(f"spike_{i}", a, 0.74, 1.0 - 0.05 * (i % 2), 0.06, blood, thickness=0.03, sharp=True)]
        parts += handle(black, blood, gem=glow, pommel="ring")
        parts += tassel("tassel_a", -0.035, -0.34, red, black, beads=glow, count=1) + tassel("tassel_b", 0.035, -0.34, red, black, beads=glow, count=1, length=0.28)
        return parts

    def shard():
        # Ventaglio della Scheggia: a spray of crystal — shards long and short on a silver frame, lit from within.
        crystal, deep = m("shard_leaf", (0.30, 0.86, 0.80), glow=0.8), m("shard_deep", (0.10, 0.46, 0.50), glow=0.3)
        parts = []
        for i, a in enumerate(angles(146, 11)):
            reach = (0.94 if i % 2 == 0 else 0.70) - 0.02 * abs(i - 5)
            parts.append(plume(f"shard_{i}", a, 0.18, reach, 0.15 if i % 2 == 0 else 0.12, crystal if i % 2 == 0 else deep, thickness=0.04 if i % 2 == 0 else 0.03, sharp=True, edge=0.02))
        parts += [sector("frame", 0.34, 0.26, 150, silver, thickness=0.046, ribs=6), sector("frame_low", 0.20, 0.06, 150, silver, thickness=0.05, ribs=4)]
        parts += handle(m("leather_dark"), silver, gem=crystal) + tassel("tassel", 0, -0.28, m("shard_cord", (0.30, 0.86, 0.80)), deep, beads=crystal, count=3, length=0.26)
        return parts

    def kiln():
        # Ventaglio della Fornace: the firebird's tail — jade and gold, a feather at every rib with an eye of fire, the sun at its heart.
        jade, deep = m("jade", (0.10, 0.50, 0.38)), m("jade_deep", (0.04, 0.26, 0.20))
        fire = m("kiln_gem", (1.0, 0.36, 0.06), glow=1.6)
        parts = [sector("leaf", 0.78, 0.22, 150, jade, ribs=9, edge=petal(0.06)),
                 sector("rim", 0.80, 0.72, 150, gold, thickness=0.028, ribs=9, edge=petal(0.06)),
                 sector("band", 0.50, 0.44, 150, gold, thickness=0.028, ribs=9), sector("band_in", 0.44, 0.34, 150, deep, thickness=0.026, ribs=9),
                 sector("band_low", 0.34, 0.31, 150, gold, thickness=0.028, ribs=9),
                 disc("sun", 0.61, 90, 0.075, fire, depth=0.046)]
        parts += rays("sun_ray", 0.61, 90, 0.085, 0.05, gold, count=10)
        for i, a in enumerate(angles(150, 9)):
            reach = 1.06 - 0.03 * abs(i - 4)
            parts += [plume(f"feather_{i}", a, 0.70, reach, 0.15, gold if i % 2 == 0 else jade, thickness=0.03),
                      disc(f"eye_ring_{i}", reach - 0.11, a, 0.05, deep if i % 2 == 0 else gold, depth=0.036),
                      disc(f"eye_{i}", reach - 0.11, a, 0.03, fire, depth=0.044)]
        parts += spokes(150, 9, 0.78, gold) + handle(m("kiln_wrap", (0.42, 0.08, 0.06)), gold, gem=fire, length=0.28)
        cord = m("kiln_cord", (0.93, 0.68, 0.24))
        parts += tassel("tassel_a", -0.04, -0.30, cord, red, beads=jade, count=2) + tassel("tassel_b", 0.04, -0.30, cord, red, beads=jade, count=2, length=0.30)
        return parts

    def abyss():
        # Ventaglio dell'Abisso (P5, 2026-10-07): a bat's wing of black membrane stretched on bone fingers, crimson veins in it,
        # a claw at every finger and an eye at the pivot.
        skin = m("abyss_membrane", (0.07, 0.03, 0.08), rough=0.7)
        glow = m("abyss_glow", (0.95, 0.10, 0.38), glow=1.5)
        parts = [sector("leaf", 0.82, 0.20, 150, skin, ribs=7, edge=scallop(0.10)),
                 sector("vein_band", 0.50, 0.47, 150, glow, thickness=0.026, ribs=7), sector("vein_low", 0.32, 0.30, 150, glow, thickness=0.026, ribs=7)]
        for i, a in enumerate(angles(150, 7)):
            parts += [stick(f"finger_{i}", a, 0.86, 0.026, bone, thickness=0.04), plume(f"claw_{i}", a, 0.84, 1.02, 0.05, bone, thickness=0.035, sharp=True)]
        parts += [disc("eye_ring", 0.0, 90, 0.08, bone, depth=0.05), disc("eye", 0.0, 90, 0.05, glow, depth=0.06)]
        parts += handle(m("abyss_wrap", (0.20, 0.03, 0.10)), bone, gem=glow, length=0.28)
        parts += tassel("tassel", 0, -0.30, m("abyss_cord", (0.40, 0.05, 0.18)), skin, beads=glow, count=3, length=0.26)
        return parts

    fans = {"abyss_fan": abyss, "worn_fan": worn, "hunters_fan": hunters, "bronze_fan": bronze_, "iron_fan": iron_, "ridge_fan": ridge, "warden_fan": warden,
            "steel_fan": steel_, "grave_fan": grave, "ember_fan": ember, "hollow_fan": hollow, "shard_fan": shard, "kiln_fan": kiln}
    return finish(name, fans[name]())
