"""The Ninja's seven suits and four headpieces (CLS-03, redrawn 2026-10-02), each its own design.

"The same work on the Warrior and the Ninja" as on the three new classes (your call): the Ninja's
suits were one vest in other colours. Now: bindings and a long scarf, a harness of throwing knives,
snake scales with fangs on the shoulder, a wolf's pelt and climbing claws, the wardens' night
harness with its star, a shroud in tatters wound with chain, black lacquer and gold with a
demon's face on the shoulder and two sheaths crossed on the back.

Worn over the Ninja's own dark clothes, as before (visuals.json "own_clothes"), and in two cuts as
every class's: the men a vest, the women a band round the chest (classkit `vest`, `bust`). Nothing
goes round the head but a band: the man's hood and the woman's hair stay. Pieces from classkit and
ninjakit.
"""

import math

import armourkit as a
import classkit as c
import ninjakit as n
import temins as t

RX, RY, Y0 = c.RX, c.RY, c.Y0
m = c.m


def scarf(s, mat, tip=None, length=0.62, spread=0.20):
    """A scarf round the neck, two long ends flying out behind."""
    s.add("chest", n.collar(mat))
    for i, side in enumerate((-1, 1)):
        path = c.curve([(side * 0.05, 0.26, 1.19), (side * (0.08 + spread * 0.3), 0.50, 1.20 + 0.03 * i), (side * (0.10 + spread * 0.7), 0.50 + length * 0.55, 1.10 - 0.05 * i),
                        (side * (0.10 + spread), 0.50 + length, 1.16 - 0.10 * i)])
        s.add("chest", c.tube(f"scarf_end_{i}", path, (0.075, 0.06), mat, sides=6, flat=0.2, ref=(0, 0, 1)))
        if tip is not None:
            s.add("chest", c.tube(f"scarf_tip_{i}", path[-4:], (0.066, 0.064), tip, sides=6, flat=0.26, ref=(0, 0, 1)))


def knives(s, steel, wrap, cut, start, end, count=4):
    """Throwing knives in a row along a strap, hilts up."""
    for i in range(count):
        f = (i + 0.5) / count
        x, y, z = (start[k] + (end[k] - start[k]) * f for k in range(3))
        s.add("chest", [t.box(f"knife_{i}", (0.032, 0.014, 0.10), steel, at=(x, y - 0.012, z - 0.03), bevel=0.004),
                        t.box(f"knife_hilt_{i}", (0.026, 0.02, 0.05), wrap, at=(x, y - 0.014, z + 0.045), bevel=0.004)], cut)


def night_wraps(name):
    """Bende della Notte: cloth wound round the chest, the arms and the shins, a green sash, a long scarf flying behind."""
    s = c.Suit(name, "ninja")
    grey, dark = m("wrap_grey", (0.42, 0.42, 0.40)), m("wrap_dark", (0.10, 0.11, 0.12))
    green = m("ninja_green", (0.06, 0.26, 0.14))

    s.add("chest", n.binding(grey, z0=0.99, z1=1.20, count=3), "m")
    s.add("spine", n.binding(grey, z0=0.85, z1=0.98, count=2), "m")
    s.add("chest", n.binding(grey, z0=0.99, z1=1.07, count=1), "w")
    s.add("spine", n.binding(grey, z0=0.87, z1=0.985, count=2), "w")
    for cut, (z0, z1) in (("m", (0.86, 1.19)), ("w", (0.88, 1.06))):
        s.add("chest", n.strip("strap", (-0.26, -RY - 0.02, z0), (0.20, -RY * 0.95 - 0.02, z1), 0.06, dark, thick=0.03), cut)
    scarf(s, green, length=0.50)

    s.add("hips", n.sash(green, tail=green, lengths=(0.34, 0.26)))
    # A pouch of throwing stars on the thigh strap.
    s.add("hips", [t.box("pouch", (0.11, 0.06, 0.11), dark, at=(-0.36, -0.20, 0.50), bevel=0.02, segments=2),
                   c.fin("star", [(0, 0.07), (0.02, 0.02), (0.07, 0), (0.02, -0.02), (0, -0.07), (-0.02, -0.02), (-0.07, 0), (-0.02, 0.02)], 0.012,
                         m("wrap_steel", (0.60, 0.62, 0.66), metal=0.7, rough=0.3), (-0.36, -0.235, 0.50), right=(1, 0, 0), up=(0, 0, 1))])
    s.both("lowerarm", n.wraps(grey))
    s.both("lowerleg", n.shin(grey, turns=4))
    return s.done()


def prowler_leathers(name):
    """Cuoio del Predone: a harness of crossed belts carrying a row of throwing knives, one shoulder guarded, a cowl at the neck."""
    s = c.Suit(name, "ninja")
    leather, dark = m("prowler_leather", (0.26, 0.15, 0.08)), m("leather_dark")
    tan = m("prowler_tan", (0.46, 0.32, 0.18))
    steel = m("prowler_steel", (0.62, 0.64, 0.68), metal=0.7, rough=0.3)

    c.vest(s, leather, hem=dark)
    for cut, (z0, z1) in (("m", (0.86, 1.19)), ("w", (0.88, 1.06))):
        front, back = -RY - 0.016, RY + 0.045
        for side in (-1, 1):
            s.add("chest", [n.strip(f"strap_{side}", (side * 0.27, front, z0), (-side * 0.22, front * 0.94, z1), 0.07, dark, thick=0.03),
                            n.strip(f"strap_back_{side}", (side * 0.27, back, z0), (-side * 0.22, back * 0.94, z1), 0.07, dark, thick=0.03)], cut)
        knives(s, steel, tan, cut, (-0.24, front - 0.02, z0 + 0.03), (0.19, front * 0.94 - 0.02, z1 - 0.03), count=4 if cut == "m" else 3)
    s.add("chest", [a.roll("cowl", 0.31, 0.29, 1.12, 1.26, dark, y=0.0)])

    s.add("hips", n.sash(dark, knot=False))
    s.add("hips", c.leaves("flaps", 0.44, 0.38, 0.50, leather, 8, height=0.20, y=0.0, lean=0.07, point=0.3, overlap=0.92, thick=0.026))
    s.add("hips", [t.box("pouch", (0.13, 0.07, 0.13), tan, at=(0.30, -0.31, 0.55), bevel=0.02, segments=2), t.box("pouch_flap", (0.14, 0.075, 0.05), dark, at=(0.30, -0.315, 0.60), bevel=0.015)])

    s.add("upperarm.l", n.pad(leather, rim=dark, size=0.18) + [t.sphere(f"stud_{i}", 0.024, steel, at=(0.41 + 0.10 * math.cos(math.radians(72 * i)), 0.10 * math.sin(math.radians(72 * i)), 1.285), subdivisions=1) for i in range(5)])
    s.both("lowerarm", n.bracer(leather, dark, plate=tan))
    s.both("lowerleg", n.shin(leather, trim=dark))
    return s.done()


def viper_scales(name):
    """Scaglie di Vipera: green scales in rows, two fangs standing on the shoulder, a vial of venom at the belt, a yellow sash."""
    s = c.Suit(name, "ninja")
    dark = m("viper_dark", (0.04, 0.12, 0.07))
    scale, pale = m("viper_scale", (0.08, 0.34, 0.14), metal=0.35, rough=0.4), m("viper_pale", (0.34, 0.56, 0.20), metal=0.35, rough=0.4)
    yellow = m("viper_yellow", (0.70, 0.54, 0.10))
    bone = m("bone")
    venom = m("venom", (0.35, 0.95, 0.20), glow=1.0)

    c.vest(s, dark, hem=yellow)
    for row in range(4):
        z = 0.90 + 0.072 * row
        k = 1.0 if z < 0.985 else 1.0 - 0.10 * (z - 0.985) / 0.225
        s.add("spine" if z < 0.985 else "chest", c.leaves(f"scale_{row}", (RX + 0.012) * k, (RY + 0.012) * k, z, pale if row % 2 else scale, 16, height=0.125, offset=row * 0.5, lean=0.045), None if row < 2 else "m")
    s.add("chest", n.collar(dark))

    s.add("hips", n.sash(yellow, tail=yellow, lengths=(0.34, 0.26)))
    s.add("hips", c.leaves("skirt", 0.44, 0.385, 0.50, scale, 12, height=0.17, y=0.0, lean=0.09, point=0.5, thick=0.022))
    s.add("hips", [t.cylinder("vial", 0.035, 0.10, venom, at=(-0.34, -0.24, 0.50), sides=10), t.cylinder("vial_stop", 0.028, 0.03, bone, at=(-0.34, -0.24, 0.565), sides=8),
                   a.rod("vial_cord", (-0.34, -0.24, 0.58), (-0.33, -0.25, 0.64), 0.006, yellow, sides=4)])

    shoulder = c.arm_leaves("shoulder", scale, 3, alt=pale, r=0.20, x0=0.30)
    shoulder += [c.tube(f"fang_{i}", c.curve([(0.36, -0.08 + 0.16 * i, 1.26), (0.42, -0.11 + 0.22 * i, 1.40), (0.38, -0.10 + 0.20 * i, 1.54)]), (0.045, 0.006), bone, sides=7) for i in range(2)]
    s.add("upperarm.l", shoulder)
    s.right("upperarm", c.arm_leaves("shoulder_r", scale, 2, alt=pale, r=0.20, x0=0.32))
    s.both("lowerarm", n.bracer(dark, yellow, plate=scale))
    s.both("lowerleg", n.shin(dark, trim=yellow, plate=scale))
    return s.done()


def ridge_stalker(name):
    """Predatore della Cresta: a wolf's pelt over one shoulder, its tail down the back, a plate on the heart, a coil of rope, claws on the bracers."""
    s = c.Suit(name, "ninja")
    cloth = m("stalker_cloth", (0.12, 0.15, 0.20))
    steel = m("stalker_steel", (0.44, 0.50, 0.58), metal=0.7, rough=0.34)
    fur, fur_dark = m("stalker_fur", (0.54, 0.54, 0.52), rough=0.95), m("stalker_fur_dark", (0.24, 0.24, 0.25), rough=0.95)
    rope = m("stalker_rope", (0.50, 0.42, 0.26))
    hide = m("leather_dark")

    c.vest(s, cloth, hem=hide)
    s.add("chest", c.plastron(steel, hide, side=1), "m")
    s.add("chest", c.plastron(steel, hide, side=1, z0=0.93, z1=1.055, depth=0.05), "w")
    # The pelt: fur over the left shoulder and round the neck, the tail hanging behind.
    s.add("chest", [c.fur_ring("pelt", 0.36, 0.33, 1.10, 1.25, fur, count=12, depth=0.08, flare=0.70, y=0.0),
                    c.tube("pelt_tail", c.curve([(0.20, 0.30, 1.14), (0.26, 0.44, 0.98), (0.24, 0.50, 0.78), (0.18, 0.50, 0.62)]), (0.06, 0.085), fur_dark, sides=8),
                    t.sphere("pelt_tail_tip", 0.08, fur, at=(0.18, 0.50, 0.59), subdivisions=2, squash=(1, 1, 1.3))])
    s.add("upperarm.l", [c.arm_fur(f"pelt_arm_{i}", 0.26 + 0.09 * i, 0.21 - 0.01 * i, fur if i % 2 == 0 else fur_dark, width=0.13, count=9) for i in range(3)])
    s.right("upperarm", n.pad(steel, rim=hide, size=0.16))

    s.add("hips", n.sash(hide, knot=False))
    s.add("hips", [c.hoop(f"rope_{i}", (0.43 + 0.012 * i, -0.06, 0.52), 0.11 - 0.012 * i, 0.022, rope, right=(0, 1, 0), up=(0, 0, 1), segments=18, sides=6) for i in range(3)])
    s.add("hips", c.leaves("flaps", 0.44, 0.38, 0.50, cloth, 6, height=0.20, y=0.0, lean=0.07, point=0.3, overlap=0.9, thick=0.024))

    claws = n.bracer(hide, steel, plate=steel) + [a.spike(f"claw_{i}", (0.72, -0.06 + 0.06 * i, c.ARM_Z + 0.13), (0.94, -0.07 + 0.07 * i, c.ARM_Z + 0.16), 0.022, steel, sides=5) for i in range(3)]
    s.both("lowerarm", claws)
    s.both("lowerleg", n.shin(hide, trim=steel, plate=steel))
    return s.done()


def wardens_shadow(name):
    """Ombra del Custode: the wardens' own night harness — dark steel edged in gold, their star on the chest and on the cape, two-plated shoulders, a blue sash."""
    s = c.Suit(name, "ninja")
    steel = m("shadow_steel", (0.07, 0.09, 0.14), metal=0.75, rough=0.3)
    gold = m("gold", metal=0.8, rough=0.3)
    blue = m("shadow_blue", (0.06, 0.12, 0.36), rough=0.8)
    hide = m("leather_dark")

    c.cuirass(s, steel, gold, lames=(steel, m("shadow_dull", (0.12, 0.16, 0.26), metal=0.65, rough=0.4)))
    for cut, z in (("m", 1.09), ("w", 0.99)):
        p = c.breast(0, z, 0.014)
        for i in range(4):
            r = math.radians(90 * i)
            s.add("chest", a.spike(f"star_{i}", p, (p[0] + 0.085 * math.sin(r), p[1] - 0.004, p[2] + 0.085 * math.cos(r)), 0.032, gold, sides=4), cut)
    s.add("chest", n.collar(blue))
    s.add("spine", [c.cloak("cape", blue, top=(1.00, 0.41, 0.28), bottom=(0.46, 0.55, 0.38), curl=0.09, hem=c.swallow(0.10), rows=6),
                    c.cloak("cape_hem", gold, top=(1.00, 0.416, 0.285), bottom=(0.46, 0.556, 0.386), curl=0.09, hem=c.swallow(0.10), rows=1, span=(0.9, 1.0), thick=0.03)])
    s.add("chest", t.box("cape_bar", (0.40, 0.05, 0.06), gold, at=(0, 0.40, 1.02), bevel=0.02))
    back = (0.0, 0.50, 0.76)
    s.add("spine", [a.spike(f"cape_star_{i}", back, (back[0] + 0.11 * math.sin(math.radians(90 * i)), back[1] + 0.012, back[2] + 0.11 * math.cos(math.radians(90 * i))), 0.04, gold, sides=4) for i in range(4)])

    s.add("hips", n.sash(blue, tail=gold, lengths=(0.34, 0.26)))
    s.add("hips", c.faulds(steel, gold, count=6, drop=0.17))

    s.both("upperarm", c.pauldron(steel, gold, size=0.195, lame=True))
    s.both("lowerarm", n.bracer(steel, gold, plate=steel))
    s.both("lowerleg", n.shin(hide, trim=gold, plate=steel))
    return s.done()


def ashen_shroud(name):
    """Sudario Cinereo: a shroud in tatters over the shoulders and from the belt, chain wound round the arm, a row of kunai lit from within."""
    s = c.Suit(name, "ninja")
    ash, soot = m("shroud_ash", (0.20, 0.19, 0.20)), m("shroud_soot", (0.07, 0.07, 0.08))
    iron = m("shroud_iron", (0.26, 0.26, 0.29), metal=0.65, rough=0.42)
    ember = m("ember", glow=1.4, rough=0.4)

    c.vest(s, soot, hem=iron)
    for cut, (z0, z1) in (("m", (0.98, 1.22)), ("w", (1.04, 1.22))):
        s.add("chest", [c.patch("shroud", RX + 0.05, RY + 0.05, z0, z1, ash, top=0.74, hem=c.ragged(3, 0.13), cols=36, rows=3, thick=0.026),
                        c.patch("shroud_over", RX + 0.066, RY + 0.066, z0 + 0.08, z1 + 0.005, soot, top=0.72, hem=c.ragged(9, 0.09), cols=36, rows=2, thick=0.026)], cut)
    s.add("chest", a.roll("mask", 0.33, 0.32, 1.12, 1.30, soot, y=-0.02))

    s.add("hips", n.sash(soot, knot=False))
    s.add("hips", [c.patch("tatters", 0.455, 0.40, 0.34, 0.62, ash, top=0.88, hem=c.ragged(5, 0.20), a0=60, a1=300, cols=30, rows=3, thick=0.024, y=0.0)])
    for i in range(4):
        p = c.at(0.43, 0.37, -40 + 26 * i, 0.50, 0.0)
        s.add("hips", [t.box(f"kunai_{i}", (0.04, 0.016, 0.14), ember, at=(p[0], p[1] - 0.012, p[2] - 0.03), bevel=0.004),
                       c.hoop(f"kunai_ring_{i}", (p[0], p[1] - 0.012, p[2] + 0.07), 0.022, 0.008, iron, segments=10, sides=4)])

    chain = n.wraps(soot) + [c.hoop(f"chain_{i}", (0.52 + 0.045 * i, 0.0, c.ARM_Z), 0.158, 0.016, iron, right=(0, 1, 0), up=(0.25 if i % 2 else -0.25, 0, 1), segments=14, sides=5) for i in range(6)]
    s.add("lowerarm.l", chain)
    s.right("lowerarm", n.bracer(soot, iron, plate=iron))
    s.both("upperarm", c.pauldron(iron, soot, size=0.17, spikes=2, rune=ember))
    s.both("lowerleg", n.shin(soot, trim=iron, plate=iron))
    return s.done()


def kiln_shade(name):
    """Ombra della Fornace: black lacquer lined with gold, a demon's face on the shoulder, two sheaths crossed on the back, a red scarf flying."""
    s = c.Suit(name, "ninja")
    lacquer = m("shade_lacquer", (0.03, 0.03, 0.04), metal=0.6, rough=0.18)
    gold = m("gold", metal=0.8, rough=0.3)
    red = m("shade_red", (0.50, 0.04, 0.03))
    ember = m("ember", glow=1.4, rough=0.4)

    c.cuirass(s, lacquer, gold, lames=(lacquer, lacquer), grow=0.004)
    for cut, z in (("m", 1.10), ("w", 0.99)):
        p = c.breast(0, z, 0.012, 0.004)
        s.add("chest", [t.cylinder("sun", 0.07, 0.03, gold, at=(0, p[1] - 0.006, z), sides=18, axis="Y", bevel=0.006),
                        t.cylinder("sun_heart", 0.042, 0.04, ember, at=(0, p[1] - 0.008, z), sides=14, axis="Y")], cut)
    scarf(s, red, tip=gold, length=0.72, spread=0.26)
    # Two sheaths crossed on the back, gold at their mouths and their ends.
    for side in (-1, 1):
        low, high = c.Vector((side * 0.34, RY + 0.10, 0.74)), c.Vector((-side * 0.26, RY + 0.12 + 0.03 * side, 1.34))
        s.add("chest", [a.rod(f"sheath_{side}", low, high, 0.04, lacquer, sides=8),
                        a.rod(f"sheath_mouth_{side}", low + (high - low) * 0.80, low + (high - low) * 0.86, 0.048, gold, sides=8),
                        a.rod(f"sheath_end_{side}", low, low + (high - low) * 0.07, 0.048, gold, sides=8),
                        a.rod(f"sheath_hilt_{side}", high, high + (high - low).normalized() * 0.20, 0.028, red, sides=8),
                        t.box(f"sheath_guard_{side}", (0.10, 0.03, 0.03), gold, at=tuple(high), bevel=0.006)])

    s.add("hips", n.sash(red, tail=gold, lengths=(0.36, 0.28)))
    s.add("hips", a.band("belt_gold", 0.412, 0.352, 0.635, 0.67, gold, y=0.0, bevel=0.006))
    s.add("hips", c.faulds(lacquer, gold, rows=2, count=6, drop=0.16))

    # The demon's face on the left shoulder: brow, horns, tusks, eyes alight.
    face = c.pauldron(lacquer, gold, size=0.20, lame=True)
    face += [t.sphere("brow", 0.10, gold, at=(0.44, -0.13, 1.27), subdivisions=2, squash=(1.4, 0.6, 0.5))]
    for i in (-1, 1):
        face += [t.sphere(f"eye_{i}", 0.03, ember, at=(0.44 + 0.055 * i, -0.17, 1.23), subdivisions=1),
                 a.spike(f"tusk_{i}", (0.44 + 0.05 * i, -0.17, 1.16), (0.44 + 0.07 * i, -0.21, 1.25), 0.02, gold, sides=5),
                 c.tube(f"horn_{i}", c.curve([(0.44 + 0.09 * i, -0.10, 1.30), (0.44 + 0.15 * i, -0.12, 1.42), (0.44 + 0.12 * i, -0.14, 1.52)]), (0.04, 0.006), gold, sides=6)]
    s.add("upperarm.l", face)
    s.right("upperarm", c.pauldron(lacquer, gold, size=0.19, lame=True))
    s.both("lowerarm", n.bracer(lacquer, gold, plate=gold) + [c.rune_band("bracer_light", 0.62, 0.153, ember)])
    s.both("lowerleg", n.shin(lacquer, trim=gold, plate=lacquer) + [t.cylinder("shin_light", 0.136, 0.016, ember, at=(n.LEG, 0.005, 0.205), sides=18)])
    return s.done()


def head(name, band, ends=None, plate=None, mark=None, wings=None, horns=None, gem=None, trim=None, lengths=(0.34, 0.26)):
    """A band round the brow; `ends` its tails behind; `plate` a forehead guard with `mark` cut in it;
    `wings` blades swept back at the temples; `horns` curved horns over the brow; `gem` a stone."""
    rx, ry, y = n.BROW
    s = c.Suit(name, "ninja", head=True)
    s.add("head", n.headband(band, ends=ends, lengths=lengths))
    if trim is not None:
        s.add("head", [a.band("rim_low", rx + 0.008, ry + 0.008, 1.845, 1.862, trim, sides=40, y=y, bevel=0.004),
                       a.band("rim_high", rx + 0.008, ry + 0.008, 1.925, 1.942, trim, sides=40, y=y, bevel=0.004)])
    front = y - ry - 0.03
    if plate is not None:
        s.add("head", n.brow_plate(plate, width=58))
        for side in (-1, 1):
            s.add("head", t.sphere(f"rivet_{side}", 0.018, band, at=c.at(rx + 0.03, ry + 0.03, side * 24, 1.89, y), subdivisions=1))
    if mark is not None:
        s.add("head", c.fin("mark", [(0, 0.045), (0.014, 0.014), (0.045, 0), (0.014, -0.014), (0, -0.045), (-0.014, -0.014), (-0.045, 0), (-0.014, 0.014)], 0.016, mark,
                            (0, front - 0.012, 1.893), right=(1, 0, 0), up=(0, 0, 1)))
    if wings is not None:
        left = []
        for i in range(2):
            left.append(c.blade(f"wing_{i}", (rx + 0.01, 0.0 + 0.10 * i, 1.90), 0.44 - 0.10 * i, 0.13, wings, right=(0.2, 1, 0), up=(0.25, 0.75 + 0.2 * i, 0.9 - 0.3 * i), hook=0.3, thick=0.03))
        s.add("head", left + [c.mirror(p) for p in left])
    if horns is not None:
        left = [c.tube("horn", c.curve([(0.20, front + 0.01, 1.94), (0.30, front - 0.06, 2.08), (0.36, front - 0.04, 2.26), (0.30, front + 0.04, 2.40)]), (0.065, 0.008), horns, sides=8)]
        s.add("head", left + [c.mirror(p) for p in left])
    if gem is not None:
        s.add("head", t.sphere("gem", 0.05, gem, at=(0, front - 0.01, 1.895), subdivisions=2, squash=(1, 0.7, 1.2)))
    return s.done()


SUITS = {"night_wraps": night_wraps, "prowler_leathers": prowler_leathers, "viper_scales": viper_scales, "ridge_stalker": ridge_stalker,
         "wardens_shadow": wardens_shadow, "ashen_shroud": ashen_shroud, "kiln_shade": kiln_shade}


def build(name):
    if name in SUITS:
        return SUITS[name](name)

    green = m("ninja_green", (0.06, 0.26, 0.14))
    iron = m("brow_iron", (0.44, 0.46, 0.50), metal=0.7, rough=0.35)
    steel = m("shadow_steel", (0.07, 0.09, 0.14), metal=0.75, rough=0.3)
    gold = m("gold", metal=0.8, rough=0.3)
    soot = m("shroud_soot", (0.07, 0.07, 0.08))
    ember = m("ember", glow=1.4, rough=0.4)
    heads = {
        # Fascia della Notte: a green band, its ends long behind.
        "night_band": lambda: head(name, green, ends=green, lengths=(0.52, 0.40)),
        # Fronte di Ferro: a forehead guard of iron on a dark band, a star cut in it.
        "iron_brow": lambda: head(name, m("wrap_dark", (0.10, 0.11, 0.12)), ends=m("wrap_dark", (0.10, 0.11, 0.12)), plate=iron, mark=m("wrap_dark", (0.10, 0.11, 0.12))),
        # Cerchio del Custode: dark steel rimmed in gold, the star on the brow, gold blades swept back from the temples.
        "wardens_circlet": lambda: head(name, steel, trim=gold, plate=steel, mark=gold, wings=gold),
        # Corna Cineree: a brow of black iron, two horns over it, an ember between them.
        "ashen_horns": lambda: head(name, soot, trim=m("shroud_iron", (0.26, 0.26, 0.29), metal=0.65, rough=0.42), plate=m("shroud_iron", (0.26, 0.26, 0.29), metal=0.65, rough=0.42),
                                    horns=m("ash_horn", (0.34, 0.30, 0.26), rough=0.6), gem=ember, ends=soot),
        # Velo Annegato (act 2, the Mire): deep teal, a brow of verdigris, a pearl, fins of coral at the temples.
        "drowned_veil": lambda: head(name, m("mire_deep", (0.03, 0.09, 0.11)), trim=m("mire_verdigris", (0.16, 0.44, 0.36), metal=0.6, rough=0.4),
                                     plate=m("mire_verdigris", (0.16, 0.44, 0.36), metal=0.6, rough=0.4), wings=m("mire_coral", (0.80, 0.30, 0.26), rough=0.6),
                                     gem=m("mire_pearl", (0.92, 0.95, 0.90), glow=0.5, rough=0.2), ends=m("mire_teal", (0.04, 0.17, 0.19)), lengths=(0.50, 0.38)),
    }
    return heads[name]()
