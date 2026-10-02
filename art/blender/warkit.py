"""The Warrior's seven suits (P1, redrawn 2026-10-02), each its own design.

"The same work on the Warrior and the Ninja" as on the three new classes (your call): the suits
were stacked bands of plate, each a file of its own but all one silhouette, and the women wore the
men's. Now each has what nobody else wears — a quilted coat with toggles, a studded jerkin with a
fringe, scales from throat to thigh under a collar of mail, mail under a fur mantle and a plaid, the
wardens' blued plate with wings on its shoulders, a carapace of overlapping lames lit in the seams,
the furnace burning in a breastplate of gold — and each comes in two cuts.

The men's cut is built round the Knight, as armourkit's (the trunk about 0.43 across and 0.40 deep
from z 0.64 to 1.29), and worn in place of the body's own torso, a dark cloth under it at the hips.
The women's is a band round the chest over her own clothes, shoulders and belly bare (classkit's
`bust`; visuals.json "cut": "w" and "own_clothes"). What rides the arms, the hips and the legs is
shared. Pieces come from classkit.
"""

import math

import armourkit as a
import classkit as c
import ninjakit as n
import temins as t

KX, KY, KY0 = 0.43, 0.40, 0.03     # the Knight's trunk: half its width and depth, and how far back its middle is
m = c.m


def kat(angle, z, out=0.0):
    """The point at `angle` and height z on the men's breastplate, which narrows to the shoulders."""
    k = 1.0 if z < 0.97 else 1.0 - 0.16 * (z - 0.97) / 0.32
    return c.at(KX + out, KY + out, angle, z, KY0, k)


def front(cut, z_m=1.13, z_w=0.99, out=0.012, angle=0.0):
    """Where something sits on the breast of either cut."""
    return kat(angle, z_m, out) if cut == "m" else c.breast(angle, z_w, out)


def suit(name):
    s = c.Suit(name, "warrior")
    # Under the men's cut the body's own torso is not worn: a dark cloth over the hips closes the gap.
    s.add("hips", a.band("underlay", 0.40, 0.36, 0.33, 0.60, m("undercloth", (0.22, 0.19, 0.17), rough=0.9), y=0.02, bevel=0), "m")
    return s


def breastplate(s, steel, trim, lames=3, alt=None, gorget=None, edge=None):
    """The men's breastplate and the lames under it; the women's band; `gorget` a collar on both;
    `edge` a line of another material along the foot of each lame."""
    s.add("chest", [a.band("breast", KX, KY, 0.97, 1.29, steel, top=0.84, y=KY0),
                    a.band("breast_hem", KX + 0.015, KY + 0.015, 0.955, 0.99, trim, y=KY0)], "m")
    for i in range(lames):
        z0 = 0.87 - i * 0.105
        s.add("spine", a.band(f"lame_{i}", KX - 0.004 - 0.003 * i, KY - 0.004 - 0.003 * i, z0, z0 + 0.105, alt if alt is not None and i % 2 else steel, y=KY0), "m")
        if edge is not None:
            s.add("spine", a.band(f"lame_edge_{i}", KX + 0.004 - 0.003 * i, KY + 0.004 - 0.003 * i, z0, z0 + 0.022, edge, y=KY0, bevel=0), "m")
    c.bust(s, steel, trim)
    if gorget is not None:
        s.add("chest", a.band("gorget", 0.32, 0.31, 1.25, 1.34, gorget, y=0.05), "m")
        s.add("chest", a.band("gorget", 0.29, 0.27, 1.15, 1.25, gorget, y=0.0, top=0.9, bevel=0.01), "w")


def pauldron(steel, rim, size=0.20, lame=None, gem=None, spikes=0, spike_mat=None):
    """The Warrior's shoulder: a dome, a rim under it, a second plate down the arm."""
    parts = [t.sphere("dome", size, steel, at=(0.34, 0.0, 1.22), subdivisions=2, squash=(1.1, 0.95, 0.72)),
             t.sphere("rim", size * 1.03, rim, at=(0.34, 0.0, 1.195), subdivisions=2, squash=(1.1, 0.96, 0.45))]
    if lame is not None:
        parts += [t.sphere("lame", size * 0.86, lame, at=(0.43, 0.0, 1.14), subdivisions=2, squash=(0.9, 0.95, 0.72)),
                  t.sphere("lame_rim", size * 0.89, rim, at=(0.43, 0.0, 1.115), subdivisions=2, squash=(0.9, 0.96, 0.42))]
    if gem is not None:
        parts.append(t.sphere("gem", size * 0.25, gem, at=(0.34, 0.0, 1.22 + size * 0.74), subdivisions=1, squash=(1, 1, 1.3)))
    for i in range(spikes):
        r = math.radians(-55 + 110 * i / max(1, spikes - 1)) if spikes > 1 else 0.0
        parts.append(a.spike(f"spike_{i}", (0.36, 0.13 * math.sin(r), 1.30), (0.42, 0.30 * math.sin(r), 1.47 + 0.05 * math.cos(r)), 0.05, spike_mat or rim, sides=6))
    return parts


def bracer(mat, cuff=None, band=None):
    parts = [a.sleeve("bracer", 0.50, 0.74, 0.150, 0.135, mat)]
    if cuff is not None:
        parts += [a.sleeve("cuff", 0.725, 0.76, 0.142, 0.142, cuff, bevel=0), a.sleeve("cuff_b", 0.49, 0.525, 0.158, 0.158, cuff, bevel=0)]
    if band is not None:
        parts.append(a.sleeve("band", 0.60, 0.63, 0.156, 0.155, band, bevel=0))
    return parts


def belt(s, mat, buckle=None, gem=None):
    s.add("hips", a.band("belt", 0.425, 0.40, 0.575, 0.645, mat, y=KY0))
    if buckle is not None:
        s.add("hips", t.box("buckle", (0.11, 0.035, 0.085), buckle, at=(0, -0.385, 0.61), bevel=0.012))
    if gem is not None:
        s.add("hips", t.sphere("clasp", 0.045, gem, at=(0, -0.40, 0.61), subdivisions=1, squash=(1, 0.6, 1)))


def cape(s, cloth, trim=None, hem=None, length=0.28):
    s.add("spine", c.cloak("cape", cloth, top=(1.31, 0.40, 0.31), bottom=(length, 0.56, 0.50), curl=0.12, hem=hem, rows=10))
    if trim is not None:
        s.add("spine", c.cloak("cape_hem", trim, top=(1.31, 0.406, 0.315), bottom=(length, 0.566, 0.506), curl=0.12, hem=hem, rows=1, span=(0.93, 1.0), thick=0.03))


def padded_coat(name):
    """Giubba Imbottita: the first coat — quilted cloth in rolls, wooden toggles on loops of cord, a strap, puffed sleeves, a scalloped skirt."""
    s = suit(name)
    cloth = m("cloth_beige", (0.62, 0.50, 0.32), rough=0.9)
    brown = m("cloth_brown", (0.36, 0.24, 0.14), rough=0.9)
    wood, hide = m("wood"), m("leather_dark")

    s.add("chest", [a.roll("roll_0", KX, KY, 0.97, 1.08, cloth), a.roll("roll_1", KX - 0.005, KY - 0.005, 1.08, 1.19, cloth),
                    a.roll("roll_2", KX - 0.015, KY - 0.01, 1.19, 1.29, cloth, top=0.86), a.band("collar", 0.30, 0.29, 1.26, 1.33, brown, y=0.05)], "m")
    s.add("spine", [a.roll(f"roll_{i}", KX - 0.005, KY - 0.005, 0.64 + i * 0.112, 0.752 + i * 0.112, cloth) for i in range(3)], "m")
    for i in range(5):
        z = 0.70 + 0.112 * i
        p = kat(0, z, 0.012)
        s.add("chest" if z > 0.97 else "spine", [c.tube(f"toggle_{i}", [(-0.06, p[1], z), (0.06, p[1], z + 0.012)], (0.018, 0.014), wood, sides=6),
                                                c.tube(f"loop_{i}", [(0.04, p[1] + 0.004, z + 0.01), (0.13, p[1] + 0.03, z + 0.005)], 0.008, brown, sides=4)], "m")
    # A strap from the right hip to the left shoulder.
    s.add("chest", n.strip("strap", (-0.28, -KY + 0.005, 0.80), (0.24, -KY * 0.80, 1.27), 0.08, hide, thick=0.03), "m")

    s.add("spine", a.roll("bust_low", c.RX, c.RY, c.BUST[0], 0.99, cloth, y=c.Y0), "w")
    s.add("chest", a.roll("bust_top", c.RX, c.RY, 0.985, c.BUST[1], cloth, y=c.Y0, top=0.965), "w")
    for i in range(2):
        z = 0.92 + 0.085 * i
        p = c.breast(0, z, 0.012)
        s.add("chest", c.tube(f"toggle_w_{i}", [(-0.06, p[1], z), (0.06, p[1], z + 0.012)], (0.018, 0.014), wood, sides=6), "w")
    s.add("chest", n.collar(brown), "w")

    s.add("hips", [a.roll("rope_a", 0.425, 0.40, 0.575, 0.612, brown, y=KY0), a.roll("rope_b", 0.425, 0.40, 0.612, 0.648, brown, y=KY0),
                   t.sphere("rope_knot", 0.055, brown, at=(0.18, -0.385, 0.61), subdivisions=2)])
    s.add("hips", [c.tube(f"rope_end_{i}", c.curve([(0.18, -0.40, 0.60), (0.20 + 0.04 * i, -0.44, 0.48), (0.20 + 0.07 * i, -0.45, 0.36 + 0.05 * i)]), 0.018, brown, sides=6) for i in range(2)])
    s.add("hips", c.edged("skirt", 0.475, 0.445, 0.44, 0.585, cloth, brown, top=0.9, hem=lambda u: 0.03 * abs(math.sin(math.pi * 9 * u)), y=KY0, cols=36, rows=2, edge=0.2))

    s.both("upperarm", [a.sleeve("sleeve", 0.12, 0.42, 0.165, 0.15, cloth, bevel=0.03)], "m")
    s.both("upperarm", [t.sphere("puff", 0.17, cloth, at=(0.30, 0.0, 1.19), subdivisions=2, squash=(1.1, 1.0, 0.75)),
                        a.sleeve("puff_band", 0.40, 0.44, 0.15, 0.15, brown, bevel=0.01)])
    s.both("lowerarm", n.wraps(cloth, r=0.142))
    return s.done()


def leather_vest(name):
    """Giubbotto di Cuoio: a studded jerkin, laced up the front, one shoulder in boiled leather, a fringe at the hips, a knife at the belt."""
    s = suit(name)
    leather, dark = m("vest_leather", (0.32, 0.18, 0.09)), m("leather_dark")
    tan = m("vest_tan", (0.50, 0.34, 0.18))
    iron = m("vest_iron", (0.46, 0.46, 0.50), metal=0.65, rough=0.42)

    s.add("chest", [a.band("jerkin", KX, KY, 0.97, 1.29, leather, top=0.84, y=KY0), a.band("yoke", KX * 0.9, KY * 0.9, 1.20, 1.30, dark, top=0.86, y=KY0),
                    a.band("collar", 0.31, 0.30, 1.26, 1.32, dark, y=0.05)], "m")
    s.add("spine", [a.band("jerkin_low", KX - 0.004, KY - 0.004, 0.64, 0.975, leather, y=KY0), a.band("belly_belt", KX + 0.006, KY + 0.006, 0.80, 0.86, dark, y=KY0, bevel=0.008),
                    t.box("belly_buckle", (0.09, 0.03, 0.08), iron, at=(0, KY0 - KY - 0.02, 0.83), bevel=0.01)], "m")
    for z in (0.99, 1.19):
        s.add("chest", c.stud_row("stud", KX * (1.0 if z < 1.0 else 0.89) + 0.01, KY * (1.0 if z < 1.0 else 0.89) + 0.01, z, iron, 18, radius=0.022, y=KY0), "m")
    for i in range(3):
        z = 1.01 + 0.055 * i
        for side in (-1, 1):
            p = kat(0, z, 0.01)
            s.add("chest", a.rod(f"lace_{i}_{side}", (side * 0.05, p[1], z), (-side * 0.05, p[1], z + 0.05), 0.009, tan, sides=5), "m")

    c.bust(s, leather, dark)
    s.add("chest", c.stud_row("stud_w", c.RX + 0.012, c.RY + 0.012, 0.975, iron, 16, radius=0.02), "w")
    s.add("chest", n.collar(dark), "w")

    belt(s, dark, buckle=iron)
    s.add("hips", c.leaves("fringe", 0.455, 0.425, 0.47, tan, 22, height=0.22, y=KY0, lean=0.08, point=0.25, overlap=0.8, thick=0.02))
    s.add("hips", [t.box("pouch", (0.13, 0.07, 0.13), leather, at=(-0.30, -0.33, 0.56), bevel=0.02, segments=2), t.box("pouch_flap", (0.14, 0.075, 0.05), dark, at=(-0.30, -0.335, 0.61), bevel=0.015),
                   t.box("sheath", (0.06, 0.04, 0.26), dark, at=(0.44, -0.10, 0.50), bevel=0.012), t.cylinder("knife_grip", 0.022, 0.12, tan, at=(0.44, -0.10, 0.69), sides=8),
                   t.box("knife_guard", (0.08, 0.05, 0.022), iron, at=(0.44, -0.10, 0.635), bevel=0.006)])

    left = pauldron(leather, dark, size=0.19, lame=leather)
    left += [t.sphere(f"stud_{i}", 0.026, iron, at=(0.34 + 0.11 * math.cos(math.radians(72 * i)), 0.11 * math.sin(math.radians(72 * i)), 1.33 - 0.03 * abs(math.cos(math.radians(72 * i)))), subdivisions=1) for i in range(5)]
    s.add("upperarm.l", left)
    s.both("lowerarm", bracer(leather, cuff=dark) + [t.sphere(f"bracer_stud_{i}", 0.02, iron, at=(0.56 + 0.06 * i, 0.0, c.ARM_Z + 0.148), subdivisions=1) for i in range(3)])
    s.both("lowerleg", n.shin(leather, trim=dark))
    return s.done()


def scale_hauberk(name):
    """Usbergo a Scaglie: scales from throat to thigh, bronze and iron in turn, a collar of mail, scaled shoulders."""
    s = suit(name)
    base = m("hauberk_leather", (0.16, 0.13, 0.11), rough=0.8)
    bronze = m("bronze_scale", (0.62, 0.40, 0.16), metal=0.7, rough=0.38)
    iron = m("iron_scale", (0.50, 0.50, 0.54), metal=0.65, rough=0.42)
    mail = m("hauberk_mail", (0.34, 0.34, 0.38), metal=0.6, rough=0.5)
    brass = m("bronze", metal=0.65, rough=0.4)

    s.add("chest", a.band("under", KX - 0.01, KY - 0.01, 0.97, 1.29, base, top=0.84, y=KY0), "m")
    s.add("spine", a.band("under", KX - 0.012, KY - 0.012, 0.64, 0.975, base, y=KY0), "m")
    for i in range(8):
        z = 0.70 + 0.076 * i
        k = 1.0 if z < 0.97 else 1.0 - 0.16 * (z - 0.97) / 0.32
        s.add("chest" if z > 0.985 else "spine", c.leaves(f"scale_{i}", (KX + 0.004) * k, (KY + 0.004) * k, z, iron if i % 2 else bronze, 18, height=0.135, y=KY0, offset=i * 0.5, lean=0.045, thick=0.018), "m")
    s.add("chest", c.patch("aventail", 0.36, 0.34, 1.20, 1.33, mail, top=0.84, hem=c.zigzag(14, 0.05), cols=28, rows=2, thick=0.03, y=0.04), "m")

    c.bust(s, base, brass)
    for i in range(2):
        s.add("chest" if i else "spine", c.leaves(f"bust_scale_{i}", c.RX + 0.012, c.RY + 0.012, 0.935 + 0.065 * i, iron if i else bronze, 16, height=0.125, offset=i * 0.5, lean=0.045), "w")
    s.add("chest", c.patch("aventail", 0.31, 0.29, 1.12, 1.25, mail, top=0.84, hem=c.zigzag(12, 0.05), cols=24, rows=2, thick=0.03, y=0.0), "w")

    belt(s, m("leather_dark"), buckle=brass)
    for i in range(2):
        s.add("hips", c.leaves(f"skirt_{i}", 0.455 + 0.012 * (1 - i), 0.425 + 0.012 * (1 - i), 0.44 + 0.09 * i, iron if i else bronze, 16, height=0.17, y=KY0, offset=i * 0.5, lean=0.09, point=0.45, thick=0.022))

    s.both("upperarm", c.arm_leaves("shoulder", bronze, 3, alt=iron, r=0.215, x0=0.24, height=0.14) + [t.sphere("cap", 0.15, brass, at=(0.27, 0.0, 1.25), subdivisions=2, squash=(1.1, 0.95, 0.6))])
    s.both("lowerarm", bracer(base, cuff=brass))
    s.both("lowerleg", n.shin(base, trim=brass, plate=bronze))
    return s.done()


def ridge_mail(name):
    """Cotta della Cresta: the mountain's — mail under a mantle of fur, a plaid across the chest pinned with an iron ring, fur at the wrists and the shins."""
    s = suit(name)
    mail, mail_dark = m("ridge_mail", (0.40, 0.42, 0.46), metal=0.6, rough=0.45), m("ridge_mail_dark", (0.26, 0.28, 0.32), metal=0.55, rough=0.5)
    fur, fur_dark = m("ridge_fur", (0.70, 0.66, 0.58), rough=0.95), m("ridge_fur_dark", (0.34, 0.28, 0.22), rough=0.95)
    plaid, stripe = m("ridge_plaid", (0.10, 0.24, 0.16)), m("ridge_plaid_stripe", (0.56, 0.44, 0.14))
    iron = m("ridge_iron", (0.30, 0.31, 0.34), metal=0.65, rough=0.42)
    hide = m("leather_dark")

    for i in range(6):
        z0 = 0.97 + 0.053 * i
        k0, k1 = 1.0 - 0.16 * (z0 - 0.97) / 0.32, 1.0 - 0.16 * (z0 + 0.053 - 0.97) / 0.32
        s.add("chest", a.band(f"mail_{i}", KX * k0, KY * k0, z0, z0 + 0.056, mail if i % 2 else mail_dark, top=k1 / k0, y=KY0, bevel=0.01), "m")
    for i in range(6):
        s.add("spine", a.band(f"mail_low_{i}", KX - 0.004, KY - 0.004, 0.64 + 0.056 * i, 0.70 + 0.056 * i, mail if i % 2 else mail_dark, y=KY0, bevel=0.01), "m")
    for i in range(2):
        s.add("spine" if i == 0 else "chest", a.band(f"mail_w_{i}", c.RX, c.RY, 0.86 + 0.065 * i * 2, 0.99 + 0.075 * i, mail if i else mail_dark, y=c.Y0, top=1.0 if i == 0 else 0.965, bevel=0.01), "w")
    s.add("spine", a.band("mail_w_hem", c.RX + 0.007, c.RY + 0.007, 0.855, 0.885, iron, y=c.Y0, bevel=0.006), "w")

    for cut, (rx, ry, z0, z1, y, front_y) in (("m", (0.47, 0.44, 1.10, 1.32, 0.04, -KY)), ("w", (0.40, 0.37, 1.07, 1.25, 0.0, -c.RY))):
        s.add("chest", [c.fur_ring("mantle", rx, ry, z0, z1, fur, count=14, depth=0.09, flare=0.66, y=y),
                        c.fur_ring("mantle_low", rx + 0.02, ry + 0.02, z0 - 0.03, z0 + 0.08, fur_dark, count=14, depth=0.08, flare=0.86, y=y)], cut)
        # The plaid: from the right hip over the left shoulder, front and back, a ring to pin it.
        z_low, z_high = (0.70, 1.20) if cut == "m" else (0.87, 1.10)
        surface = (lambda ang, z, out: kat(ang, z, out)) if cut == "m" else (lambda ang, z, out: c.breast(ang, z, out))
        for a0, a1, tag in ((-62, 42, "front"), (242, 138, "back")):
            path = [surface(a0 + (a1 - a0) * j / 8, z_low + (z_high - z_low) * j / 8, 0.03) for j in range(9)]
            edge = [surface(a0 + (a1 - a0) * j / 8, z_low + (z_high - z_low) * j / 8, 0.045) for j in range(9)]
            s.add("chest", [c.tube(f"plaid_{tag}", path, 0.10, plaid, sides=6, flat=0.22, ref=(0, 0, 1)),
                            c.tube(f"plaid_stripe_{tag}", edge, 0.028, stripe, sides=5, flat=0.4, ref=(0, 0, 1))], cut)
        s.add("chest", [c.hoop("pin", (0.22, front_y * 0.93 - 0.04, z_high - 0.06), 0.07, 0.018, iron, segments=16, sides=6),
                        a.rod("pin_bar", (0.14, front_y * 0.93 - 0.05, z_high - 0.10), (0.30, front_y * 0.93 - 0.05, z_high - 0.02), 0.012, iron, sides=5)], cut)

    belt(s, hide, buckle=iron)
    s.add("hips", a.band("mail_skirt", 0.445, 0.415, 0.44, 0.58, mail_dark, top=0.94, y=KY0, bevel=0.01))
    s.add("hips", c.faulds(hide, iron, count=6, drop=0.17))
    s.add("hips", [n.strip("plaid_fall", (-0.30, -0.345, 0.62), (-0.33, -0.39, 0.30), 0.16, plaid, thick=0.026),
                   n.strip("plaid_fall_stripe", (-0.305, -0.352, 0.40), (-0.33, -0.394, 0.345), 0.165, stripe, thick=0.03)])

    s.both("upperarm", [a.sleeve("mail_sleeve", 0.14, 0.44, 0.158, 0.148, mail_dark, bevel=0.01)])
    s.both("lowerarm", bracer(hide, cuff=iron) + [c.arm_fur("bracer_fur", 0.53, 0.172, fur)])
    shin_fur = c.fur_ring("shin_fur", 0.155, 0.155, 0.27, 0.35, fur, count=7, depth=0.05, flare=0.95, y=0.005)
    shin_fur.data.transform(c.Matrix.Translation((n.LEG, 0, 0)))
    s.both("lowerleg", n.shin(hide, trim=iron) + [shin_fur])
    return s.done()


def warden_plate(name):
    """Piastre del Guardiano: the mountain gate's plate — blued steel edged in gold, the star on a blue tabard, an ice-blue stone, wings on the shoulders, a blue cape."""
    s = suit(name)
    steel = m("warden_steel", (0.30, 0.42, 0.68), metal=0.75, rough=0.28)
    dark = m("blue_groove", (0.14, 0.20, 0.36), metal=0.65, rough=0.38)
    gold = m("gold", metal=0.8, rough=0.3)
    cloth = m("cloth_blue", (0.08, 0.14, 0.38), rough=0.8)
    ice = m("ice", (0.55, 0.85, 1.0), glow=1.0, rough=0.2)

    breastplate(s, steel, gold, lames=3, alt=dark, gorget=gold)
    for cut in ("m", "w"):
        p = front(cut, 1.14, 0.99)
        s.add("chest", [t.sphere("stone", 0.055, ice, at=(0, p[1] - 0.02, p[2]), subdivisions=2, squash=(1, 0.7, 1.3)),
                        c.hoop("stone_ring", (0, p[1] - 0.012, p[2]), 0.075, 0.016, gold, segments=16, sides=6)], cut)
    s.add("chest", c.fin("ridge", [(-0.035, 0), (0.035, 0), (0.045, 0.06), (0.0, 0.10), (-0.045, 0.06)], 0.05, steel, (0, -KY + 0.012, 0.975), up=(0, 0.0, 1), edge=0.02), "m")
    for side in (-1, 1):
        s.add("chest", t.cylinder(f"besagew_{side}", 0.085, 0.03, gold, at=kat(side * 52, 1.21, 0.02), sides=16, axis="Y", bevel=0.006), "m")

    cape(s, m("cape_blue", (0.05, 0.09, 0.30), rough=0.9), trim=gold, hem=c.swallow(0.08))
    belt(s, m("leather_dark"), buckle=gold)
    s.add("hips", c.faulds(steel, gold, rows=2, count=6, drop=0.16))
    s.add("hips", c.edged("tabard", 0.46, 0.43, 0.26, 0.60, cloth, gold, a0=-20, a1=20, hem=c.pointed(0.09), top=0.94, y=KY0, rows=3, edge=0.14))
    star = c.at(0.468, 0.438, 0, 0.42, KY0)
    s.add("hips", [a.spike(f"star_{i}", star, (star[0] + 0.075 * math.sin(math.radians(90 * i)), star[1] - 0.004, star[2] + 0.075 * math.cos(math.radians(90 * i))), 0.028, gold, sides=4) for i in range(4)])

    wing = pauldron(steel, gold, size=0.20, lame=dark)
    for i in range(3):
        wing += c.feather(f"wing_{i}", (0.33, 0.07 - 0.07 * i, 1.30), (0.30 - 0.03 * i, 0.20 - 0.20 * i, 1.58 - 0.05 * abs(i - 1)), 0.11, gold, face=(1, 0, 0.2))
    s.both("upperarm", wing)
    s.both("lowerarm", bracer(steel, cuff=gold))
    s.both("lowerleg", n.shin(steel, trim=gold, plate=steel) + [t.sphere("knee", 0.075, gold, at=(n.LEG, -0.125, 0.34), subdivisions=2, squash=(1, 0.6, 1))])
    return s.done()


def ashen_carapace(name):
    """Carapace Cinereo: ash-dark plates lapped like a beetle's back, fire in every seam, a collar of spikes, spiked shoulders, a torn grey cape."""
    s = suit(name)
    ash = m("ash_steel", (0.13, 0.12, 0.13), metal=0.6, rough=0.42)
    dark = m("ash_dark", (0.06, 0.055, 0.06), metal=0.5, rough=0.5)
    ember = m("ember", glow=1.4, rough=0.4)
    horn = m("ash_horn", (0.34, 0.30, 0.26), rough=0.6)

    # Seven lames from the hips to the throat, each lapping the one below, light under every lap.
    for i in range(7):
        z0 = 0.64 + 0.092 * i
        k = 1.0 if z0 < 0.97 else 1.0 - 0.16 * (z0 - 0.97) / 0.32
        bone = "chest" if z0 > 0.96 else "spine"
        s.add(bone, c.patch(f"lame_{i}", (KX + 0.012) * k, (KY + 0.012) * k, z0, z0 + 0.115, ash if i % 2 == 0 else dark, top=0.93, y=KY0, cols=28, rows=1, thick=0.03), "m")
        s.add(bone, a.band(f"seam_{i}", (KX - 0.004) * k, (KY - 0.004) * k, z0 + 0.092, z0 + 0.118, ember, y=KY0, bevel=0), "m")
    s.add("spine", a.band("under", KX - 0.02, KY - 0.02, 0.64, 0.98, dark, y=KY0), "m")
    s.add("chest", a.band("under_top", KX - 0.02, KY - 0.02, 0.97, 1.29, dark, top=0.84, y=KY0), "m")

    c.bust(s, ash, dark)
    s.add("chest", a.band("seam_w", c.RX + 0.004, c.RY + 0.004, 0.975, 0.995, ember, y=c.Y0, bevel=0), "w")

    for cut, (rx, ry, z, y) in (("m", (0.33, 0.32, 1.27, 0.05)), ("w", (0.30, 0.28, 1.17, 0.0))):
        s.add("chest", a.band("collar", rx, ry, z - 0.02, z + 0.06, dark, y=y, bevel=0.01), cut)
        for i in range(9):
            ang = -120 + 30 * i
            s.add("chest", a.spike(f"collar_spike_{i}", c.at(rx, ry, ang, z + 0.02, y), c.at(rx + 0.13, ry + 0.13, ang, z + 0.07, y), 0.04, horn, sides=5), cut)

    cape(s, m("ash_cape", (0.16, 0.15, 0.16), rough=0.95), hem=c.ragged(4, 0.22))
    belt(s, dark, gem=ember)
    s.add("hips", c.faulds(ash, dark, rows=2, count=7, drop=0.16))
    s.add("hips", a.band("fauld_seam", 0.43, 0.405, 0.565, 0.58, ember, y=KY0, bevel=0))

    s.both("upperarm", pauldron(ash, dark, size=0.205, lame=ash, spikes=3, spike_mat=horn) + [a.sleeve("pad_seam", 0.36, 0.375, 0.20, 0.20, ember, bevel=0)])
    s.both("lowerarm", bracer(ash, cuff=dark, band=ember) + [a.spike("elbow", (0.50, 0.0, c.ARM_Z + 0.13), (0.42, 0.0, c.ARM_Z + 0.30), 0.045, horn, sides=5)])
    s.both("lowerleg", n.shin(ash, trim=dark, plate=ash) + [a.spike("knee", (n.LEG, -0.13, 0.30), (n.LEG, -0.25, 0.43), 0.05, horn, sides=5)])
    return s.done()


def kiln_aegis(name):
    """Egida della Fornace: the relic — dark steel and gold, the furnace burning in the breastplate with rays of gold round it, tongues of gold on the shoulders, a crimson cape hemmed in flame."""
    s = suit(name)
    steel = m("aegis_steel", (0.15, 0.13, 0.14), metal=0.7, rough=0.32)
    gold = m("gold", metal=0.8, rough=0.3)
    ember = m("ember", glow=1.4, rough=0.4)
    crimson = m("crimson", (0.42, 0.04, 0.03), rough=0.8)

    breastplate(s, steel, gold, lames=3, gorget=gold, edge=gold)
    for cut, size in (("m", 0.12), ("w", 0.085)):
        p = front(cut, 1.13, 0.985)
        where = (0, p[1] - 0.012, p[2])
        s.add("chest", [t.cylinder("furnace", size, 0.05, gold, at=where, sides=20, axis="Y", bevel=0.008),
                        t.cylinder("heart", size * 0.68, 0.06, ember, at=where, sides=16, axis="Y")], cut)
        s.add("chest", c.sun("furnace_ray", where, size * 0.9, gold, rays=8, ray=size * 0.5, right=(1, 0, 0), up=(0, 0, 1), disc=False), cut)
    for side in (-1, 1):
        s.add("chest", c.tube(f"bar_{side}", [kat(side * (30 + 6 * j), 0.99 + 0.07 * j, 0.012) for j in range(5)], 0.018, gold, sides=6, flat=0.6), "m")

    cape(s, crimson, trim=gold, hem=c.zigzag(3, 0.14))
    belt(s, gold, gem=ember)
    s.add("hips", c.faulds(steel, gold, rows=2, count=6, drop=0.17))
    s.add("hips", c.edged("tabard", 0.465, 0.435, 0.24, 0.60, crimson, gold, a0=-20, a1=20, hem=c.pointed(0.10), top=0.94, y=KY0, rows=3, edge=0.14))
    flame = c.at(0.475, 0.445, 0, 0.36, KY0)
    s.add("hips", c.fin("tabard_flame", [(-0.05, 0), (0.05, 0), (0.07, 0.07), (0.03, 0.10), (0.04, 0.17), (0.0, 0.24), (-0.05, 0.14), (-0.03, 0.09), (-0.07, 0.06)], 0.02, gold, flame, right=(1, 0, 0), up=(0, 0.03, 1)))

    shoulder = pauldron(gold, steel, size=0.215, lame=steel, gem=ember)
    for i in range(3):
        shoulder.append(c.blade(f"tongue_{i}", (0.36, (i - 1) * 0.10, 1.29), 0.26 - 0.05 * abs(i - 1), 0.11, gold, right=(1, 0, 0), up=(-0.12, (i - 1) * 0.5, 1), hook=0.4, thick=0.03))
    s.both("upperarm", shoulder)
    s.both("lowerarm", bracer(gold, cuff=steel, band=ember))
    s.both("lowerleg", n.shin(steel, trim=gold, plate=gold) + [t.sphere("knee", 0.075, gold, at=(n.LEG, -0.125, 0.34), subdivisions=2, squash=(1, 0.6, 1)),
                                                               t.cylinder("shin_light", 0.137, 0.016, ember, at=(n.LEG, 0.005, 0.205), sides=18)])
    return s.done()


SUITS = {"padded_coat": padded_coat, "leather_vest": leather_vest, "scale_hauberk": scale_hauberk, "ridge_mail": ridge_mail,
         "warden_plate": warden_plate, "ashen_carapace": ashen_carapace, "kiln_aegis": kiln_aegis}


def build(name):
    return SUITS[name](name)
