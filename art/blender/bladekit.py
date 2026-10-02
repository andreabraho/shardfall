"""The Warrior's twelve swords and twelve great swords (P1, redrawn 2026-10-02).

"The same work on the Warrior and the Ninja" (your call). Every sword keeps its character — worn
iron, antler, bronze leaf, the ridge's chopper, the wardens' blue and gold, grave steel, ember,
the Hollow's red, crystal, the Kiln's white heat — drawn again with more in it; and every great
sword is a design of its own now, where it was its sword stretched (swordkit.variant): a snapped
tip, tusks at the foot of the blade, a ring-hilted zweihander, a chopper with nine rings in its
back, a claymore, a skull in the guard. A pair still reads as one smith's work.

Built with swordkit as any sword: the grip (named `grip`) at the origin, the blade up Z — a sword
to about 1.4, a great sword to about 2.0 — its flat facing Y. Marks and guards from hexkit.
"""

import math

import classkit as c
import hexkit as h
import swordkit as k
import temins as t

flat = h.flat


def lugs(mat, at, reach=0.24, thick=0.06):
    """Two parrying hooks standing out of the foot of a great blade."""
    half = [(0.0, at - 0.05), (reach * 0.7, at - 0.03), (reach, at + 0.07), (reach * 0.72, at + 0.03), (0.0, at + 0.05)]
    return flat("lugs", half + [(-x, y) for x, y in reversed(half[1:-1])], thick, mat, bevel=0.008)


def rings(mat, points, radius=0.05, wire=0.012):
    """Rings through the back of a blade."""
    return [c.hoop(f"ring_{i}", (x, 0, z), radius, wire, mat, right=(1, 0, 0), up=(0, 0, 1), segments=14, sides=5) for i, (x, z) in enumerate(points)]


def diamonds(mat, heights, size=0.03, depth=0.085, x=0.0):
    return [flat(f"diamond_{i}", [(x, z - size * 1.5), (x + size, z), (x, z + size * 1.5), (x - size, z)], depth, mat) for i, z in enumerate(heights)]


def build(name):
    """The sword or great sword called `name`."""
    m = t.material
    leather, hide = m("leather"), m("leather_dark")
    bone = m("bone")
    gold = m("gold", metal=0.8, rough=0.3)

    def worn(great):
        # Lama Consunta / Spadone Consunto: old iron, notched; the great one snapped at the tip and bound at the foot.
        iron = m("worn_steel", (0.44, 0.42, 0.40), metal=0.4, rough=0.6)
        dull = m("worn_iron", (0.24, 0.23, 0.23), metal=0.45, rough=0.6)
        cord = m("worn_cord", (0.50, 0.42, 0.28))
        if not great:
            outline = [(-0.10, 0.04), (0.10, 0.04), (0.105, 0.46), (0.07, 0.52), (0.10, 0.60), (0.095, 1.02), (0.05, 1.10), (0.075, 1.18), (0.0, 1.40),
                       (-0.075, 1.16), (-0.10, 0.84), (-0.06, 0.78), (-0.10, 0.70)]
            return [k.blade(outline, 0.07, iron), k.fuller(0.10, 0.60, 0.016, 0.076, dull), k.crossguard(0.22, 0.07, 0.07, dull, droop=0.03)] + h.hilt(leather, cord, dull, length=0.30, pommel="disc")
        outline = [(-0.15, 0.04), (0.15, 0.04), (0.155, 0.70), (0.11, 0.78), (0.15, 0.90), (0.145, 1.50), (0.09, 1.60), (0.12, 1.74), (0.05, 1.86), (-0.02, 1.80), (-0.10, 1.90),
                   (-0.15, 1.60), (-0.15, 1.20), (-0.10, 1.12), (-0.15, 1.02)]
        parts = [k.blade(outline, 0.10, iron), k.fuller(0.36, 1.10, 0.022, 0.106, dull), k.crossguard(0.36, 0.09, 0.09, dull, droop=0.05, flare=0.2)]
        parts += [t.cylinder(f"binding_{i}", 0.085, 0.045, cord, at=(0, 0, 0.12 + 0.06 * i), sides=10) for i in range(3)]
        for p in parts[-3:]:
            p.scale = (1.9, 0.75, 1)
        return parts + h.hilt(leather, cord, dull, length=0.42, pommel="disc", wraps=5, size=0.075)

    def hunters(great):
        # Spada / Spadone del Cacciatore: a hunting blade, antler for a guard; the great one carries a boar's tusks at its foot.
        steel = m("hunter_steel", (0.56, 0.56, 0.55), metal=0.55, rough=0.42)
        wood = m("wood_dark")
        antler = [(-0.36, 0.10), (-0.30, 0.02), (-0.14, -0.02), (0.14, -0.02), (0.30, 0.02), (0.36, 0.10), (0.33, 0.12), (0.26, 0.06), (0.20, 0.10), (0.18, 0.05),
                  (0.10, 0.05), (-0.10, 0.05), (-0.18, 0.05), (-0.20, 0.10), (-0.26, 0.06), (-0.33, 0.12)]
        if not great:
            outline = [(-0.10, 0.04), (0.10, 0.04), (0.12, 0.60), (0.14, 0.96), (0.10, 1.22), (-0.02, 1.40), (-0.04, 1.12), (-0.09, 1.04), (-0.10, 0.60)]
            return [k.blade(outline, 0.075, steel), k.fuller(0.12, 0.90, 0.016, 0.082, m("hunter_groove", (0.36, 0.36, 0.36), metal=0.5, rough=0.5)),
                    flat("guard", [(x * 0.72, y * 0.8) for x, y in antler], 0.08, bone, bevel=0.012),
                    t.box("block", (0.17, 0.09, 0.06), wood, at=(0, 0, 0.0), bevel=0.015)] + h.hilt(leather, hide, bone, length=0.30, pommel="ball", size=0.055)
        outline = k.mirrored([(0.16, 0.04), (0.165, 1.30), (0.20, 1.52), (0.13, 1.78), (0.0, 1.98)])
        parts = [k.blade(outline, 0.11, steel), k.fuller(0.40, 1.30, 0.024, 0.118, m("hunter_groove", (0.36, 0.36, 0.36), metal=0.5, rough=0.5)),
                 flat("guard", antler, 0.11, bone, bevel=0.016), t.box("block", (0.24, 0.13, 0.08), wood, at=(0, 0, 0.0), bevel=0.02)]
        for side in (-1, 1):
            parts.append(c.tube(f"tusk_{side}", c.curve([(side * 0.12, 0, 0.24), (side * 0.27, 0, 0.30), (side * 0.33, 0, 0.44), (side * 0.27, 0, 0.56)]), (0.045, 0.006), bone, sides=7))
        return parts + h.hilt(leather, hide, bone, length=0.42, pommel="ball", wraps=5, size=0.07)

    def bronze(great):
        # Spada / Spadone di Bronzo: a leaf of bronze with a rib down it, the sun in the guard; the great one a longer leaf under a crescent.
        metal = m("bronze_blade", (0.66, 0.42, 0.16), metal=0.75, rough=0.35)
        dark = m("bronze_dark", (0.36, 0.22, 0.10), metal=0.65, rough=0.45)
        red = m("bronze_cord", (0.46, 0.10, 0.06))
        if not great:
            outline = k.mirrored([(0.065, 0.04), (0.07, 0.30), (0.15, 0.70), (0.15, 0.92), (0.09, 1.20), (0.0, 1.40)])
            parts = [k.blade(outline, 0.08, metal), k.fuller(0.10, 1.10, 0.014, 0.092, dark), k.crossguard(0.20, 0.07, 0.07, metal, droop=-0.03, flare=0.2)]
            parts += [t.cylinder("sun", 0.07, 0.085, metal, at=(0, 0, 0.0), sides=18, axis="Y", bevel=0.008), t.cylinder("gem", 0.04, 0.10, red, at=(0, 0, 0.0), sides=12, axis="Y")]
            return parts + k.grip(0.30, 0.04, leather, wrap=red, wraps=4) + [c.hoop("pommel", (0, 0, -0.355), 0.055, 0.02, metal, segments=16, sides=6)]
        outline = k.mirrored([(0.09, 0.04), (0.10, 0.40), (0.21, 1.00), (0.21, 1.36), (0.12, 1.74), (0.0, 2.0)])
        parts = [k.blade(outline, 0.11, metal), k.fuller(0.14, 1.50, 0.016, 0.124, dark), k.fuller(0.50, 1.30, 0.05, 0.116, dark)]
        parts[-1].name = "fuller_wide"
        crescent = [(0.36 * math.cos(math.radians(a)), 0.02 + 0.20 * math.sin(math.radians(a))) for a in range(200, 341, 14)] + \
                   [(0.27 * math.cos(math.radians(a)), 0.07 + 0.12 * math.sin(math.radians(a))) for a in range(340, 199, -14)]
        parts += [flat("guard", crescent, 0.09, metal, bevel=0.012), t.cylinder("sun", 0.085, 0.10, metal, at=(0, 0, -0.02), sides=18, axis="Y", bevel=0.008),
                  t.cylinder("gem", 0.05, 0.115, red, at=(0, 0, -0.02), sides=12, axis="Y")]
        return parts + k.grip(0.42, 0.045, leather, wrap=red, wraps=6, bottom=-0.46) + [c.hoop("pommel", (0, 0, -0.52), 0.065, 0.024, metal, segments=16, sides=6)]

    def iron(great):
        # Spada / Spadone di Ferro: the soldier's sword, plain and true; the great one a two-hander with a leathered foot, hooks and rings at its guard.
        steel = m("iron_blade", (0.60, 0.61, 0.64), metal=0.7, rough=0.32)
        dark = m("iron_dark", (0.24, 0.25, 0.28), metal=0.6, rough=0.45)
        if not great:
            outline = k.mirrored([(0.095, 0.04), (0.095, 0.90), (0.075, 1.20), (0.0, 1.40)])
            return [k.blade(outline, 0.07, steel), k.fuller(0.10, 0.92, 0.018, 0.078, dark), k.crossguard(0.26, 0.06, 0.07, dark, droop=0.04, flare=0.25)] + \
                h.hilt(leather, hide, dark, length=0.30, pommel="ball", size=0.06)
        outline = k.mirrored([(0.075, 0.04), (0.075, 0.42), (0.13, 0.50), (0.13, 1.50), (0.10, 1.78), (0.0, 2.0)])
        parts = [k.blade(outline, 0.10, steel), k.fuller(0.56, 1.50, 0.022, 0.108, dark), lugs(dark, 0.46, reach=0.26),
                 t.cylinder("ricasso", 0.06, 0.36, hide, at=(0, 0, 0.24), sides=10), k.crossguard(0.42, 0.07, 0.09, dark, droop=0.03, flare=0.3)]
        parts[3].scale = (1.5, 0.8, 1)
        parts += [c.hoop(f"guard_ring_{side}", (side * 0.20, 0, 0.0), 0.09, 0.02, dark, right=(1, 0, 0), up=(0, 1, 0), segments=16, sides=6) for side in (-1, 1)]
        return parts + h.hilt(leather, hide, dark, length=0.44, pommel="ball", wraps=6, size=0.075)

    def ridge(great):
        # Lama / Spadone del Crinale: the ridge folk's chopper, notched at the back; the great one carries nine rings in its spine.
        steel = m("ridge_steel", (0.52, 0.56, 0.58), metal=0.65, rough=0.38)
        iron_ = m("iron", metal=0.5, rough=0.55)
        cord = m("moss", rough=0.85)
        if not great:
            outline = [(-0.08, 0.04), (0.09, 0.04), (0.10, 0.50), (0.17, 0.96), (0.15, 1.22), (0.04, 1.40), (-0.06, 1.34), (-0.09, 1.10), (-0.17, 1.04), (-0.09, 0.96), (-0.09, 0.50)]
            return [k.blade(outline, 0.08, steel), t.box("bolster", (0.26, 0.09, 0.07), iron_, at=(0, 0, 0.0), bevel=0.02)] + \
                rings(iron_, [(-0.12, 0.60), (-0.12, 0.78)], radius=0.04) + h.hilt(m("wood_dark"), cord, iron_, length=0.30, pommel="disc", wraps=5)
        outline = [(-0.10, 0.04), (0.12, 0.04), (0.14, 0.60), (0.26, 1.30), (0.25, 1.66), (0.10, 1.96), (-0.06, 1.90), (-0.12, 1.60), (-0.12, 0.60)]
        parts = [k.blade(outline, 0.11, steel), t.box("bolster", (0.34, 0.12, 0.09), iron_, at=(0, 0, 0.0), bevel=0.025)]
        parts += rings(iron_, [(-0.16, 0.30 + 0.165 * i) for i in range(9)], radius=0.055, wire=0.013)
        parts += [t.cylinder("tassel", 0.05, 0.22, m("ridge_red", (0.56, 0.08, 0.06)), at=(0, 0, -0.66), sides=10, radius_top=0.018)]
        return parts + h.hilt(m("wood_dark"), cord, iron_, length=0.44, pommel="disc", wraps=7, size=0.07)

    def warden(great):
        # Spada / Spadone del Guardiano: blued steel and gold, wings for a guard, the star cut in the flat, an ice-blue stone.
        steel = m("warden_blade", (0.42, 0.56, 0.86), metal=0.75, rough=0.28)
        ice = m("ice", (0.55, 0.85, 1.0), glow=1.0, rough=0.2)
        blue = m("warden_cord", (0.10, 0.18, 0.46))
        if not great:
            outline = k.mirrored([(0.09, 0.04), (0.10, 0.80), (0.085, 1.14), (0.0, 1.42)])
            parts = [k.blade(outline, 0.07, steel), k.fuller(0.12, 0.96, 0.016, 0.078, ice), h.wing_guard(gold, reach=0.30, thick=0.06, feathers=2)]
            parts += [t.sphere("gem", 0.045, ice, at=(0, 0, 0.0), subdivisions=2, squash=(1, 1.5, 1))]
            return parts + h.hilt(blue, gold, gold, length=0.30, pommel="cup", gem=ice, size=0.05)
        outline = k.mirrored([(0.10, 0.04), (0.10, 0.36), (0.14, 0.44), (0.13, 1.40), (0.17, 1.62), (0.10, 1.84), (0.0, 2.02)])
        parts = [k.blade(outline, 0.10, steel), k.fuller(0.50, 1.36, 0.018, 0.108, ice), h.wing_guard(gold, reach=0.44, thick=0.08, feathers=3)]
        for i in range(4):
            r = math.radians(90 * i)
            parts.append(flat(f"star_{i}", [(0.022 * math.cos(r + 1.57), 0.26 + 0.022 * math.sin(r + 1.57)), (0.10 * math.cos(r), 0.26 + 0.10 * math.sin(r)),
                                            (0.022 * math.cos(r - 1.57), 0.26 + 0.022 * math.sin(r - 1.57))], 0.115, gold))
        parts += [t.sphere("gem", 0.06, ice, at=(0, 0, 0.0), subdivisions=2, squash=(1, 1.5, 1))]
        return parts + h.hilt(blue, gold, gold, length=0.44, pommel="cup", gem=ice, wraps=6, size=0.06)

    def steel_(great):
        # Spada / Spadone d'Acciaio: fine steel in gold fittings; the great one a claymore, its arms sloping up to four-ringed ends.
        steel = m("steel_blade", (0.74, 0.76, 0.80), metal=0.8, rough=0.25)
        dark = m("steel_dark", metal=0.5, rough=0.5)
        red = m("steel_cord", (0.50, 0.08, 0.08))
        if not great:
            outline = k.mirrored([(0.10, 0.04), (0.105, 0.30), (0.095, 0.90), (0.07, 1.22), (0.0, 1.42)])
            parts = [k.blade(outline, 0.075, steel), k.fuller(0.10, 0.80, 0.022, 0.092, dark), k.crossguard(0.27, 0.07, 0.07, gold, droop=-0.02, flare=0.45),
                     t.box("langet", (0.08, 0.09, 0.14), gold, at=(0, 0, 0.09), bevel=0.012)]
            return parts + h.hilt(red, gold, gold, length=0.30, pommel="facet", size=0.06)
        outline = k.mirrored([(0.14, 0.04), (0.145, 1.20), (0.115, 1.70), (0.0, 2.0)])
        parts = [k.blade(outline, 0.10, steel), k.fuller(0.30, 1.30, 0.03, 0.122, dark)]
        arm = [(0.0, -0.04), (0.10, -0.035), (0.36, 0.16), (0.33, 0.20), (0.09, 0.05), (0.0, 0.05)]
        parts += [flat("guard", arm + [(-x, y) for x, y in reversed(arm[1:-1])], 0.085, gold, bevel=0.01), t.box("langet", (0.10, 0.11, 0.26), gold, at=(0, 0, 0.14), bevel=0.014)]
        for side in (-1, 1):
            for dx, dz in ((0.035, 0.0), (-0.035, 0.0), (0.0, 0.035), (0.0, -0.035)):
                parts.append(c.hoop("quatrefoil", (side * 0.37 + dx, 0, 0.20 + dz), 0.03, 0.011, gold, segments=10, sides=5))
        return parts + h.hilt(red, gold, gold, length=0.44, pommel="disc", wraps=6, size=0.075)

    def grave(great):
        # Lama Chiamatombe / Chiamatombe: grave steel, pale lights down the flat, ribs for a guard; the great one with a skull between them.
        steel = m("grave_steel", (0.34, 0.32, 0.42), metal=0.6, rough=0.4)
        glow = m("grave_light", (0.80, 0.70, 1.0), glow=1.1)
        cord = m("grave_cord", (0.22, 0.14, 0.34))

        def ribs(count, reach, at):
            parts = []
            for side in (-1, 1):
                for i in range(count):
                    rib = [(0.0, at - 0.02 + 0.05 * i), (side * (reach + 0.05 * i), at + 0.01 + 0.07 * i), (side * (reach + 0.07 + 0.05 * i), at + 0.14 + 0.07 * i),
                           (side * (reach - 0.01 + 0.05 * i), at + 0.05 + 0.07 * i), (0.0, at + 0.01 + 0.05 * i)]
                    parts.append(flat(f"rib_{side}_{i}", rib if side > 0 else rib[::-1], 0.055, bone, bevel=0.006))
            return parts

        if not great:
            outline = k.mirrored([(0.10, 0.04), (0.12, 0.30), (0.115, 1.04), (0.07, 1.24), (0.0, 1.42)])
            return [k.blade(outline, 0.075, steel)] + diamonds(glow, [0.36, 0.58, 0.80, 1.02], size=0.026, depth=0.084) + ribs(2, 0.19, 0.0) + \
                h.hilt(bone, cord, bone, length=0.30, pommel="cup", gem=glow, size=0.05)
        outline = k.mirrored([(0.12, 0.04), (0.155, 0.40), (0.15, 1.46), (0.19, 1.60), (0.09, 1.82), (0.0, 2.0)])
        parts = [k.blade(outline, 0.10, steel)] + diamonds(glow, [0.50 + 0.24 * i for i in range(5)], size=0.034, depth=0.11) + ribs(3, 0.26, 0.0)
        parts += [t.sphere("skull", 0.10, bone, at=(0, 0, 0.14), subdivisions=2, squash=(0.95, 0.9, 1.05)),
                  t.sphere("socket_a", 0.028, glow, at=(-0.04, -0.075, 0.16), subdivisions=1), t.sphere("socket_b", 0.028, glow, at=(0.04, -0.075, 0.16), subdivisions=1),
                  t.sphere("socket_c", 0.028, glow, at=(-0.04, 0.075, 0.16), subdivisions=1), t.sphere("socket_d", 0.028, glow, at=(0.04, 0.075, 0.16), subdivisions=1)]
        return parts + h.hilt(bone, cord, bone, length=0.44, pommel="cup", gem=glow, wraps=6, size=0.06)

    def ember(great):
        # Filo di Brace / Grande Filo di Brace: a black blade, its edge alight; the great one's edge runs in tongues of flame.
        steel = m("ember_blade", (0.12, 0.10, 0.10), metal=0.6, rough=0.4)
        glow = m("ember_edge", (1.0, 0.55, 0.10), glow=1.4)
        iron_ = m("ember_iron", (0.30, 0.28, 0.30), metal=0.6, rough=0.45)
        red = m("ember_cord", (0.56, 0.10, 0.06))
        if not great:
            outline = k.mirrored([(0.10, 0.04), (0.12, 0.34), (0.115, 1.00), (0.07, 1.24), (0.0, 1.44)])
            parts = k.glowing_edge(outline, 0.08, steel, glow, inset=0.72) + [h.horn_guard(iron_, reach=0.27, rise=0.14, thick=0.07), t.sphere("gem", 0.05, glow, at=(0, 0, 0.0), subdivisions=1, squash=(1, 1.4, 1))]
            return parts + h.hilt(hide, red, iron_, length=0.30, pommel="cup", gem=glow, size=0.05)
        half = [(0.11, 0.04), (0.13, 0.30)]
        for i in range(6):
            z = 0.36 + 0.24 * i
            half += [(0.20 - 0.012 * i, z + 0.06), (0.13 - 0.008 * i, z + 0.20)]
        half.append((0.0, 2.02))
        parts = k.glowing_edge(k.mirrored(half), 0.105, steel, glow, inset=0.70)
        parts += [h.crack(glow, [(0.0, 0.24), (0.03, 0.60), (-0.03, 0.96), (0.02, 1.30), (0.0, 1.60)], width=0.016, depth=0.115)]
        parts += [h.horn_guard(iron_, reach=0.40, rise=0.20, thick=0.09), t.sphere("gem", 0.065, glow, at=(0, 0, 0.0), subdivisions=2, squash=(1, 1.4, 1))]
        return parts + h.hilt(hide, red, iron_, length=0.44, pommel="cup", gem=glow, wraps=6, size=0.06)

    def hollow(great):
        # Lama della Conca / Mietitrice della Conca: a toothed blade edged in red; the great one a wide reaver bent like a scythe, horns for a guard.
        steel = m("reaver_steel", (0.26, 0.26, 0.30), metal=0.6, rough=0.4)
        red = m("blood_glow", (0.95, 0.12, 0.08), glow=1.3, rough=0.4)
        horn = m("horn", (0.20, 0.15, 0.12), rough=0.6)
        if not great:
            outline = [(-0.09, 0.04), (0.10, 0.04), (0.12, 0.90), (0.10, 1.18), (0.0, 1.42), (-0.07, 1.24), (-0.14, 1.16), (-0.08, 1.06), (-0.15, 0.92), (-0.08, 0.82), (-0.15, 0.68), (-0.08, 0.58), (-0.09, 0.30)]
            parts = k.glowing_edge(outline, 0.08, steel, red, inset=0.80) + [h.horn_guard(horn, reach=0.26, rise=0.16, thick=0.07, droop=True, at=0.02)]
            return parts + h.hilt(hide, m("reaver_cord", (0.36, 0.05, 0.06)), horn, length=0.30, pommel="facet", size=0.06)
        outline = [(-0.10, 0.04), (0.12, 0.04), (0.20, 0.60), (0.33, 1.20), (0.34, 1.56), (0.20, 1.86), (-0.04, 2.0), (-0.02, 1.72)]
        for i in range(5):
            z = 1.56 - 0.24 * i
            outline += [(-0.16 + 0.01 * i, z), (-0.07, z - 0.12)]
        parts = k.glowing_edge(outline, 0.11, steel, red, inset=0.84)
        parts += [t.cylinder("hole", 0.06, 0.13, red, at=(0.16, 0, 1.56), sides=14, axis="Y")]
        parts += [h.horn_guard(horn, reach=0.40, rise=0.26, thick=0.09)]
        return parts + h.hilt(hide, m("reaver_cord", (0.36, 0.05, 0.06)), horn, length=0.44, pommel="facet", wraps=6, size=0.075)

    def shard(great):
        # Spada / Claymore del Frammento: a blade of crystal in a gold hilt, crystals grown at the guard; the great one with lesser shards up its sides.
        crystal = m("shard_blade", (0.95, 0.60, 1.0), glow=0.8)
        deep = m("shard_core", (0.50, 0.26, 0.70), metal=0.3, rough=0.25)
        steel = m("shard_steel", (0.60, 0.62, 0.70), metal=0.7, rough=0.3)
        if not great:
            parts = [flat("blade", k.mirrored([(0.10, 0.04), (0.115, 0.70), (0.09, 1.10), (0.0, 1.42)]), 0.08, steel, edge=0.03),
                     flat("fuller", k.mirrored([(0.05, 0.10), (0.065, 0.70), (0.045, 1.06), (0.0, 1.30)]), 0.088, crystal)]
            parts += [k.crossguard(0.26, 0.07, 0.07, gold, flare=0.3)]
            for side in (-1, 1):
                parts += [h.shardlet(f"guard_shard_{side}_a", side * 0.21, 0.10, 0.045, crystal, lean=-side * 25), h.shardlet(f"guard_shard_{side}_b", side * 0.13, 0.12, 0.035, deep, lean=-side * 12)]
            return parts + h.hilt(m("shard_cord", (0.36, 0.20, 0.50)), gold, gold, length=0.30, pommel="cup", gem=crystal, size=0.055)
        parts = [flat("blade", k.mirrored([(0.12, 0.04), (0.15, 0.50), (0.14, 1.50), (0.10, 1.78), (0.0, 2.02)]), 0.11, steel, edge=0.04),
                 flat("fuller", k.mirrored([(0.06, 0.12), (0.085, 0.50), (0.08, 1.46), (0.05, 1.72), (0.0, 1.90)]), 0.12, crystal)]
        for i in range(3):
            for side in (-1, 1):
                z = 0.46 + 0.36 * i + (0.12 if side > 0 else 0)
                parts.append(flat(f"side_shard_{i}_{side}", [(side * 0.11, z), (side * 0.24, z + 0.10), (side * 0.27, z + 0.34), (side * 0.13, z + 0.24)][::side], 0.07, crystal if (i + side) % 2 else deep, edge=0.02))
        parts += [k.crossguard(0.40, 0.09, 0.09, gold, flare=0.3)]
        for side in (-1, 1):
            parts += [h.shardlet(f"guard_shard_{side}_a", side * 0.33, 0.14, 0.06, crystal, lean=-side * 25), h.shardlet(f"guard_shard_{side}_b", side * 0.21, 0.16, 0.045, deep, lean=-side * 12)]
        return parts + h.hilt(m("shard_cord", (0.36, 0.20, 0.50)), gold, gold, length=0.44, pommel="cup", gem=crystal, wraps=6, size=0.065)

    def kiln(great):
        # Brando / Gran Brando della Fornace: white-hot steel in gold, the sun in the guard; the great one with flames of gold at its foot and rays round the sun.
        steel = m("kiln_blade", (0.95, 0.93, 0.86), metal=0.6, rough=0.25, glow=0.25)
        fire = m("kiln_fire", (1.0, 0.62, 0.12), glow=1.5)
        red = m("kiln_wrap", (0.42, 0.08, 0.06))
        if not great:
            outline = k.mirrored([(0.10, 0.04), (0.105, 0.80), (0.12, 1.00), (0.07, 1.24), (0.0, 1.46)])
            parts = [k.blade(outline, 0.075, steel)] + diamonds(fire, [0.40, 0.62, 0.84], size=0.022, depth=0.084)
            parts += [k.crossguard(0.27, 0.08, 0.08, gold, flare=0.5), t.cylinder("sun", 0.075, 0.10, gold, at=(0, 0, 0.0), sides=18, axis="Y", bevel=0.008),
                      t.cylinder("gem", 0.045, 0.115, fire, at=(0, 0, 0.0), sides=12, axis="Y")]
            return parts + h.hilt(red, gold, gold, length=0.30, pommel="cup", gem=fire, size=0.055)
        outline = k.mirrored([(0.12, 0.04), (0.125, 1.20), (0.16, 1.46), (0.10, 1.80), (0.0, 2.04)])
        parts = [k.blade(outline, 0.10, steel)] + diamonds(fire, [0.60 + 0.22 * i for i in range(5)], size=0.028, depth=0.11)
        for side in (-1, 1):
            tongue = [(side * 0.10, 0.10), (side * 0.22, 0.20), (side * 0.19, 0.32), (side * 0.27, 0.46), (side * 0.16, 0.42), (side * 0.12, 0.54), (side * 0.11, 0.30)]
            parts.append(flat(f"flame_{side}", tongue[::side], 0.07, gold, bevel=0.006))
        parts += [k.crossguard(0.42, 0.10, 0.10, gold, flare=0.5), t.cylinder("sun", 0.10, 0.12, gold, at=(0, 0, 0.0), sides=18, axis="Y", bevel=0.008),
                  t.cylinder("gem", 0.06, 0.135, fire, at=(0, 0, 0.0), sides=12, axis="Y")]
        for i in range(8):
            r = math.radians(45 * i + 22.5)
            parts.append(flat(f"ray_{i}", [(0.09 * math.cos(r - 0.2), 0.09 * math.sin(r - 0.2)), (0.19 * math.cos(r), 0.19 * math.sin(r)), (0.09 * math.cos(r + 0.2), 0.09 * math.sin(r + 0.2))], 0.07, gold))
        return parts + h.hilt(red, gold, gold, length=0.44, pommel="cup", gem=fire, wraps=6, size=0.065)

    swords = {
        "worn_blade": (worn, False), "worn_greatblade": (worn, True), "hunters_sword": (hunters, False), "hunters_greatsword": (hunters, True),
        "bronze_sword": (bronze, False), "bronze_greatsword": (bronze, True), "iron_sword": (iron, False), "iron_greatsword": (iron, True),
        "ridge_blade": (ridge, False), "ridge_greatblade": (ridge, True), "warden_sword": (warden, False), "warden_greatsword": (warden, True),
        "steel_sword": (steel_, False), "steel_greatsword": (steel_, True), "gravecaller_blade": (grave, False), "gravecaller": (grave, True),
        "ember_edge": (ember, False), "ember_greatedge": (ember, True), "hollow_blade": (hollow, False), "hollow_reaver": (hollow, True),
        "shard_sword": (shard, False), "shard_claymore": (shard, True), "kiln_brand": (kiln, False), "kiln_greatbrand": (kiln, True),
    }
    make, great = swords[name]
    return k.finish(name, make(great))
