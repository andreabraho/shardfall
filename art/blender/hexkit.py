"""The Asura's twelve cursed swords (CLS-05, 2026-10-02), each its own design.

Redrawn at your call ("dark magic swords"): they were the Warrior's swords re-forged dark
(swordkit.cursed) — the Warrior's shapes. Now each is a blade nobody else carries: cracked and
lit from inside, split round a core of light, toothed, flamed, hooked, grown of crystal, with
runes on the flat, an eye in the guard, shards hanging loose in the air beside it. Dark steel in
several darks, and a light of its own colour for each — the Asura is dark, not violet.

Built with swordkit, so the hand holds them as any sword: the grip (named `grip`) at the origin,
the blade up Z to about 1.5, its flat facing Y. Outlines are drawn in XY (X across, Y along).
"""

import math

import swordkit as k
import temins as t


def flat(name, outline, thickness, mat, edge=0.0, bevel=0.0):
    return k.stand(t.profile_solid(name, outline, thickness, mat, edge=edge, bevel=bevel))


def runes(mat, heights, size=0.03, depth=0.07, x=0.0):
    """Marks cut through the flat, one at each height, each a different stroke."""
    strokes = [
        [(-1, -1), (1, -1), (0.2, 0), (1, 1), (-1, 1), (-0.2, 0)],
        [(-0.3, -1.2), (0.3, -1.2), (0.3, 1.2), (-0.3, 1.2)],
        [(-1, -0.3), (0, -1.2), (1, -0.3), (0, 1.2)],
        [(-1, -1), (1, -1), (1, -0.4), (-0.3, -0.4), (-0.3, 0.4), (1, 0.4), (1, 1), (-1, 1)],
    ]
    return [flat(f"rune_{i}", [(x + px * size, h + py * size) for px, py in strokes[i % len(strokes)]], depth, mat) for i, h in enumerate(heights)]


def crack(mat, points, width=0.016, depth=0.07):
    """A line of light wandering up the flat."""
    left = [(x - width, y) for x, y in points]
    right = [(x + width, y) for x, y in reversed(points)]
    return flat("fuller", left + right, depth, mat)


def shardlet(name, x, y, size, mat, lean=0.0, thickness=0.05):
    """A splinter of the blade hanging loose beside it."""
    c, s = math.cos(math.radians(lean)), math.sin(math.radians(lean))
    shape = [(0, -1.6), (0.5, -0.3), (0.35, 0.9), (0, 1.7), (-0.4, 0.6), (-0.5, -0.6)]
    return flat(name, [(x + (px * c - py * s) * size, y + (px * s + py * c) * size) for px, py in shape], thickness, mat, edge=thickness * 0.3)


def horn_guard(mat, reach=0.30, rise=0.16, thick=0.06, at=0.0, droop=False):
    """A guard of two horns curving up beside the blade (or down over the hand)."""
    half = [(0.0, at - 0.05), (reach * 0.55, at - 0.045), (reach * 0.95, at + rise * 0.35), (reach, at + rise),
            (reach * 0.78, at + rise * 0.45), (reach * 0.45, at + 0.05), (0.0, at + 0.055)]
    whole = half + [(-x, y) for x, y in reversed(half[1:-1])]
    if droop:
        whole = [(x, 2 * at - y) for x, y in reversed(whole)]
    return flat("guard", whole, thick, mat, bevel=0.012)


def wing_guard(mat, reach=0.36, thick=0.06, at=0.0, feathers=3):
    """A guard of spread wings: `feathers` points a side."""
    half = [(0.0, at - 0.06), (reach * 0.3, at - 0.07)]
    for i in range(feathers):
        f = (i + 1) / feathers
        half += [(reach * (0.3 + 0.7 * f), at - 0.06 + 0.16 * f), (reach * (0.3 + 0.7 * f) - 0.05, at - 0.01 + 0.10 * f)]
    half += [(reach * 0.2, at + 0.07), (0.0, at + 0.07)]
    whole = half + [(-x, y) for x, y in reversed(half[1:-1])]
    return flat("guard", whole, thick, mat, bevel=0.01)


def eye(glow, dark, at=0.0, size=0.06, depth=0.10):
    """An eye set in the guard, looking out of both sides."""
    return [t.sphere("gem", size, glow, at=(0, 0, at), subdivisions=2, squash=(1.2, depth / size / 2, 0.8)),
            t.sphere("pupil", size * 0.4, dark, at=(0, 0, at), subdivisions=1, squash=(0.6, depth / size / 2 * 2.4, 1.6))]


def hilt(grip, wrap, metal, length=0.30, pommel="facet", gem=None, wraps=4, size=0.065):
    return k.grip(length, 0.04, grip, wrap=wrap, wraps=wraps) + k.pommel(pommel, size, metal, at=-length - 0.04, gem=gem)


def build(name):
    """The cursed sword called `name`, one of the twelve."""
    m = t.material
    hide = m("hex_grip", (0.06, 0.05, 0.07))
    bone = m("bone")
    gold = m("hex_gold", (0.70, 0.48, 0.14), metal=0.8, rough=0.32)

    def worn():
        # Lama Maledetta Consumata: a chipped black blade, a crack up its middle with a dull light in it.
        steel = m("hexw_steel", (0.10, 0.09, 0.12), metal=0.6, rough=0.45)
        glow = m("hexw_glow", (0.55, 0.28, 0.90), glow=0.9)
        iron = m("hexw_iron", (0.20, 0.18, 0.20), metal=0.55, rough=0.5)
        outline = [(-0.105, 0.06), (0.105, 0.06), (0.11, 0.40), (0.075, 0.47), (0.105, 0.56), (0.10, 0.95), (0.06, 1.02), (0.085, 1.12), (0.0, 1.36),
                   (-0.07, 1.18), (-0.10, 0.86), (-0.065, 0.78), (-0.105, 0.68)]
        parts = [k.blade(outline, 0.07, steel), crack(glow, [(0.0, 0.12), (0.03, 0.34), (-0.025, 0.55), (0.02, 0.78), (-0.01, 0.98), (0.0, 1.14)], depth=0.08)]
        parts += [k.crossguard(0.20, 0.07, 0.07, iron, droop=0.02)] + hilt(hide, m("hexw_cord", (0.26, 0.05, 0.08)), iron, length=0.28, pommel="disc")
        return parts

    def hunters():
        # Zanna Maledetta: a curved fang of a blade, one edge, bone for a guard, venom in the groove.
        steel = m("hexh_steel", (0.07, 0.11, 0.09), metal=0.6, rough=0.4)
        glow = m("hexh_glow", (0.40, 1.0, 0.35), glow=1.0)
        outline = [(-0.10, 0.06), (0.10, 0.06), (0.13, 0.36), (0.17, 0.70), (0.17, 1.00), (0.10, 1.26), (-0.04, 1.44),
                   (0.0, 1.20), (0.02, 0.96), (0.0, 0.66), (-0.05, 0.38)]
        parts = [k.blade(outline, 0.075, steel), crack(glow, [(0.015, 0.14), (0.045, 0.40), (0.085, 0.70), (0.095, 0.98), (0.06, 1.20)], width=0.016, depth=0.086)]
        parts.append(horn_guard(bone, reach=0.24, rise=0.20, droop=True, at=0.02))
        parts += hilt(hide, m("hexh_cord", (0.12, 0.26, 0.12)), bone, length=0.28, pommel="facet")
        return parts

    def bronze_():
        # Lama delle Rune: a leaf of dark bronze, four runes burning down its flat.
        steel = m("hexb_steel", (0.20, 0.12, 0.07), metal=0.7, rough=0.38)
        glow = m("hexb_glow", (1.0, 0.20, 0.10), glow=1.1)
        dark = m("hexb_dark", (0.09, 0.06, 0.05), metal=0.6, rough=0.45)
        outline = k.mirrored([(0.07, 0.06), (0.075, 0.28), (0.15, 0.62), (0.155, 0.88), (0.10, 1.16), (0.0, 1.40)])
        parts = [k.blade(outline, 0.08, steel)] + runes(glow, [0.40, 0.62, 0.84, 1.04], size=0.036, depth=0.086)
        parts += [k.crossguard(0.17, 0.09, 0.08, dark, flare=0.3), t.sphere("gem", 0.045, glow, at=(0, 0, 0.0), subdivisions=1, squash=(1, 1.3, 1))]
        parts += hilt(hide, m("hexb_cord", (0.36, 0.06, 0.05)), dark, length=0.28, pommel="ball", size=0.055)
        return parts

    def iron_():
        # Mannaia Nera: a broad black chopper with a hook at its tip, its edge red.
        steel = m("hexi_steel", (0.07, 0.07, 0.09), metal=0.65, rough=0.4)
        glow = m("hexi_glow", (1.0, 0.12, 0.08), glow=1.1)
        iron = m("hexi_iron", (0.22, 0.20, 0.22), metal=0.55, rough=0.5)
        outline = [(-0.09, 0.06), (0.10, 0.06), (0.20, 0.40), (0.22, 0.90), (0.20, 1.14), (0.27, 1.26), (0.10, 1.40), (-0.06, 1.30),
                   (-0.09, 1.00), (-0.09, 0.50)]
        parts = k.glowing_edge(outline, 0.085, steel, glow, inset=0.80)
        parts += [t.cylinder("hole", 0.045, 0.10, glow, at=(0.08, 0, 1.12), sides=12, axis="Y")]
        parts += [k.crossguard(0.19, 0.08, 0.08, iron, droop=0.04, flare=0.2)] + hilt(hide, m("hexi_cord", (0.30, 0.04, 0.05)), iron, length=0.30, pommel="disc")
        return parts

    def ridge():
        # Fiamma di Mezzanotte: a blade in waves, midnight blue, ice in the groove, horns for a guard.
        steel = m("hexr_steel", (0.04, 0.07, 0.18), metal=0.7, rough=0.34)
        glow = m("hexr_glow", (0.45, 0.80, 1.0), glow=1.1)
        cold = m("hexr_edge", (0.40, 0.48, 0.64), metal=0.75, rough=0.3)
        half = [(0.085, 0.06)]
        for i in range(7):
            y = 0.16 + 0.17 * i
            half += [(0.125 - 0.008 * i, y), (0.075 - 0.006 * i, y + 0.085)]
        half.append((0.0, 1.48))
        parts = [k.blade(k.mirrored(half), 0.075, steel), k.fuller(0.12, 1.10, 0.02, 0.082, glow)]
        parts.append(horn_guard(cold, reach=0.28, rise=0.22))
        parts += hilt(hide, m("hexr_cord", (0.08, 0.14, 0.36)), cold, length=0.30, pommel="facet")
        return parts

    def warden():
        # Sterminacustodi: a wardens' blade split down its length, a bar of violet light held between the halves.
        steel = m("hexwd_steel", (0.07, 0.06, 0.10), metal=0.7, rough=0.34)
        glow = m("hexwd_glow", (0.62, 0.30, 1.0), glow=1.3)
        prong = [(0.035, 0.30), (0.13, 0.30), (0.145, 0.90), (0.11, 1.22), (0.02, 1.46), (0.05, 1.16), (0.045, 0.80)]
        parts = [flat("blade", prong, 0.075, steel, edge=0.03), flat("blade_b", [(-x, y) for x, y in reversed(prong)], 0.075, steel, edge=0.03),
                 flat("root", k.mirrored([(0.13, 0.06), (0.14, 0.34), (0.0, 0.40)]), 0.08, steel),
                 flat("fuller", [(-0.022, 0.36), (0.022, 0.36), (0.026, 1.10), (0.0, 1.30), (-0.026, 1.10)], 0.04, glow)]
        for i in range(3):
            parts.append(flat(f"tie_{i}", [(-0.05, 0.52 + 0.24 * i), (0.05, 0.52 + 0.24 * i), (0.05, 0.56 + 0.24 * i), (-0.05, 0.56 + 0.24 * i)], 0.085, gold))
        for i in range(4):
            r = math.radians(90 * i + 45)
            parts.append(flat(f"star_{i}", [(0.05 * math.cos(r + 1.4), 0.0 + 0.05 * math.sin(r + 1.4)), (0.26 * math.cos(r) * 1.0, 0.16 * math.sin(r)),
                                            (0.05 * math.cos(r - 1.4), 0.05 * math.sin(r - 1.4))], 0.07, gold, bevel=0.008))
        parts += [t.sphere("gem", 0.05, glow, at=(0, 0, 0.0), subdivisions=1, squash=(1, 1.4, 1))]
        parts += hilt(hide, m("hexwd_cord", (0.30, 0.03, 0.05)), gold, length=0.30, pommel="cup", gem=glow, size=0.055)
        return parts

    def steel_():
        # Sega del Boia: an executioner's blade, square at the end, saw teeth down one side, an eye in the guard.
        steel = m("hexs_steel", (0.12, 0.12, 0.14), metal=0.75, rough=0.3)
        glow = m("hexs_glow", (1.0, 0.18, 0.10), glow=1.2)
        dark = m("hexs_dark", (0.05, 0.05, 0.06), metal=0.6, rough=0.45)
        outline = [(-0.11, 0.08), (0.11, 0.08), (0.13, 0.30)]
        for i in range(8):
            y = 0.34 + 0.125 * i
            outline += [(0.20, y + 0.03), (0.125, y + 0.11)]
        outline += [(0.14, 1.38), (-0.02, 1.46), (-0.13, 1.36), (-0.12, 0.60)]
        parts = [k.blade(outline, 0.08, steel), k.fuller(0.20, 1.18, 0.018, 0.088, dark)]
        parts += [k.crossguard(0.24, 0.12, 0.09, dark, flare=0.25)] + eye(glow, dark, at=0.0, size=0.065, depth=0.12)
        parts += hilt(hide, m("hexs_cord", (0.26, 0.03, 0.04)), dark, length=0.32, pommel="disc", size=0.07)
        return parts

    def grave():
        # Spina della Tomba: a blade with a backbone — vertebrae down its spine, ribs for a guard, a skull at the end.
        steel = m("hexg_steel", (0.10, 0.07, 0.15), metal=0.65, rough=0.38)
        glow = m("hexg_glow", (0.70, 0.40, 1.0), glow=1.2)
        outline = k.mirrored([(0.09, 0.06), (0.125, 0.30), (0.12, 0.98), (0.075, 1.26), (0.0, 1.46)])
        parts = k.glowing_edge(outline, 0.07, steel, glow, inset=0.78)
        for i in range(9):
            z = 0.16 + 0.125 * i
            w = 0.062 - 0.004 * i
            parts.append(t.sphere(f"vertebra_{i}", w, bone, at=(0, 0, z), subdivisions=1, squash=(1.0, 0.95, 0.72)))
        for side in (-1, 1):
            for i in range(3):
                rib = [(0.0, -0.02 + 0.045 * i), (side * (0.20 + 0.05 * i), 0.02 + 0.07 * i), (side * (0.26 + 0.05 * i), 0.14 + 0.07 * i),
                       (side * (0.19 + 0.05 * i), 0.05 + 0.07 * i), (0.0, 0.01 + 0.045 * i)]
                parts.append(flat(f"rib_{side}_{i}", rib if side > 0 else rib[::-1], 0.05, bone, bevel=0.006))
        parts += k.grip(0.30, 0.04, hide, wrap=m("hexg_cord", (0.26, 0.12, 0.40)), wraps=4)
        parts += [t.sphere("pommel", 0.075, bone, at=(0, 0, -0.36), subdivisions=2, squash=(0.9, 1.0, 1.05)),
                  t.sphere("socket_a", 0.022, glow, at=(-0.03, -0.062, -0.35), subdivisions=1), t.sphere("socket_b", 0.022, glow, at=(0.03, -0.062, -0.35), subdivisions=1)]
        return parts

    def ember():
        # Vetro di Brace: black glass, its edge in tongues of flame, fire running in the cracks.
        glass = m("hexe_glass", (0.035, 0.03, 0.04), metal=0.5, rough=0.18)
        glow = m("hexe_glow", (1.0, 0.40, 0.06), glow=1.4)
        coal = m("hexe_coal", (0.14, 0.07, 0.05), metal=0.4, rough=0.6)
        outline = [(-0.10, 0.06), (0.10, 0.06), (0.13, 0.30), (0.20, 0.44), (0.12, 0.56), (0.19, 0.76), (0.115, 0.86), (0.17, 1.08), (0.09, 1.18), (0.07, 1.36), (0.0, 1.50),
                   (-0.05, 1.28), (-0.11, 1.16), (-0.08, 1.00), (-0.14, 0.86), (-0.10, 0.66), (-0.15, 0.48), (-0.11, 0.30)]
        parts = [k.blade(outline, 0.08, glass),
                 crack(glow, [(0.0, 0.12), (0.04, 0.36), (-0.03, 0.58), (0.05, 0.80), (0.0, 1.02), (0.03, 1.22)], width=0.02, depth=0.086),
                 crack(glow, [(0.04, 0.36), (0.12, 0.44)], width=0.012, depth=0.07), crack(glow, [(-0.03, 0.58), (-0.10, 0.52)], width=0.012, depth=0.07),
                 crack(glow, [(0.05, 0.80), (0.13, 0.78)], width=0.012, depth=0.07)]
        parts[2].name, parts[3].name, parts[4].name = "vein_a", "vein_b", "vein_c"
        parts += [horn_guard(coal, reach=0.26, rise=0.14, thick=0.08), t.sphere("gem", 0.055, glow, at=(0, 0, 0.0), subdivisions=1, squash=(1, 1.3, 1))]
        parts += hilt(hide, m("hexe_cord", (0.40, 0.10, 0.03)), coal, length=0.30, pommel="cup", gem=glow, size=0.055)
        return parts

    def hollow():
        # Falce della Conca: half sword, half sickle — a long black blade ending in a great hook, red inside the curve.
        steel = m("hexo_steel", (0.05, 0.05, 0.07), metal=0.65, rough=0.38)
        glow = m("hexo_glow", (1.0, 0.14, 0.10), glow=1.3)
        blood = m("hexo_blood", (0.34, 0.04, 0.06), metal=0.5, rough=0.45)
        outline = [(-0.09, 0.06), (0.09, 0.06), (0.11, 0.80), (0.16, 1.06), (0.12, 1.30), (-0.02, 1.48), (-0.24, 1.52), (-0.42, 1.36), (-0.46, 1.14),
                   (-0.36, 1.26), (-0.22, 1.32), (-0.10, 1.24), (-0.07, 1.04), (-0.10, 0.80)]
        parts = k.glowing_edge(outline, 0.08, steel, glow, inset=0.86)
        for i in range(4):
            y = 0.24 + 0.17 * i
            parts.append(flat(f"thorn_{i}", [(0.08, y), (0.21, y + 0.10), (0.09, y + 0.09)], 0.05, blood, bevel=0.004))
        parts += [horn_guard(blood, reach=0.25, rise=0.18, droop=True, at=0.02)] + eye(glow, steel, at=0.02, size=0.055, depth=0.11)
        parts += hilt(hide, m("hexo_cord", (0.40, 0.04, 0.06)), blood, length=0.32, pommel="facet")
        return parts

    def shard():
        # Scheggia Maledetta: no forged blade — a spire of dark crystal with two lesser spires, splinters adrift beside it.
        crystal = m("hexc_crystal", (0.20, 0.80, 0.78), glow=0.8)
        deep = m("hexc_deep", (0.03, 0.22, 0.26), metal=0.3, rough=0.25)
        dark = m("hexc_dark", (0.06, 0.07, 0.09), metal=0.65, rough=0.4)
        parts = [flat("blade", [(-0.09, 0.06), (0.09, 0.06), (0.135, 0.50), (0.11, 1.10), (0.02, 1.52), (-0.10, 1.14), (-0.13, 0.56)], 0.10, deep, edge=0.035),
                 flat("fuller", [(-0.035, 0.16), (0.04, 0.16), (0.06, 0.54), (0.04, 1.08), (0.01, 1.34), (-0.035, 1.06), (-0.055, 0.56)], 0.108, crystal),
                 flat("spire_a", [(0.07, 0.10), (0.19, 0.20), (0.24, 0.62), (0.20, 0.92), (0.12, 0.58)], 0.07, crystal, edge=0.025),
                 flat("spire_b", [(-0.07, 0.10), (-0.17, 0.16), (-0.23, 0.44), (-0.20, 0.72), (-0.12, 0.46)], 0.07, deep, edge=0.025),
                 shardlet("drift_a", 0.27, 1.16, 0.07, crystal, lean=-12), shardlet("drift_b", -0.25, 0.98, 0.055, crystal, lean=16),
                 shardlet("drift_c", 0.20, 1.44, 0.045, deep, lean=-20)]
        parts += [k.crossguard(0.18, 0.08, 0.09, dark, flare=0.3)] + hilt(hide, m("hexc_cord", (0.06, 0.26, 0.28)), dark, length=0.30, pommel="cup", gem=crystal, size=0.055)
        return parts

    def kiln():
        # Anima del Demone: the last — a broad violet-black blade open down its middle on a core of light, a spine of gold,
        # wings for a guard, an eye between them, runes on the flat, and the tip come apart into splinters that hang in the air.
        steel = m("hexk_steel", (0.09, 0.04, 0.15), metal=0.75, rough=0.3)
        glow = m("hexk_glow", (0.66, 0.30, 1.0), glow=1.5)
        rail = [(0.045, 0.10), (0.15, 0.10), (0.185, 0.46), (0.165, 0.96), (0.19, 1.06), (0.12, 1.30), (0.035, 1.42), (0.06, 1.12), (0.05, 0.60)]
        parts = [flat("blade", rail, 0.085, steel, edge=0.03), flat("blade_b", [(-x, y) for x, y in reversed(rail)], 0.085, steel, edge=0.03),
                 flat("fuller", [(-0.03, 0.12), (0.03, 0.12), (0.035, 1.14), (0.0, 1.36), (-0.035, 1.14)], 0.045, glow),
                 flat("root", k.mirrored([(0.15, 0.04), (0.16, 0.16), (0.0, 0.20)]), 0.09, steel)]
        for side in (-1, 1):
            parts.append(flat(f"spine_{side}", [(side * 0.045, 0.14), (side * 0.065, 0.14), (side * 0.075, 1.10), (side * 0.055, 1.10)][::side], 0.095, gold))
            parts += runes(glow, [0.36, 0.58, 0.80], size=0.022, depth=0.094, x=side * 0.125)
            parts[-1].name, parts[-2].name, parts[-3].name = f"rune_c{side}", f"rune_b{side}", f"rune_a{side}"
        parts += [shardlet("drift_a", 0.0, 1.62, 0.07, steel), shardlet("drift_b", 0.13, 1.56, 0.045, glow, lean=-18), shardlet("drift_c", -0.14, 1.52, 0.04, glow, lean=20),
                  shardlet("drift_d", 0.25, 1.30, 0.04, steel, lean=-28), shardlet("drift_e", -0.26, 1.22, 0.035, steel, lean=30)]
        parts += [wing_guard(gold, reach=0.40, thick=0.07, feathers=3)] + eye(glow, steel, at=0.01, size=0.07, depth=0.13)
        parts += hilt(hide, m("hexk_cord", (0.30, 0.10, 0.44)), gold, length=0.32, pommel="cup", gem=glow, size=0.06)
        return parts

    def abyss():
        # Lama Maledetta dell'Abisso (P5, 2026-10-07): a reaping hook of black glass, a crimson crack running out to its point,
        # a spine of vertebrae down its back, bone wings for a guard and an eye between them, splinters hanging off the hook.
        steel = m("hexa_steel", (0.05, 0.03, 0.06), metal=0.8, rough=0.28)
        glow = m("hexa_glow", (0.95, 0.10, 0.38), glow=1.6)
        outline = [(-0.08, 0.06), (0.09, 0.06), (0.15, 0.50), (0.17, 0.95), (0.24, 1.20), (0.31, 1.44), (0.12, 1.36), (0.0, 1.30), (-0.07, 1.05), (-0.09, 0.55)]
        parts = [k.blade(outline, 0.08, steel), crack(glow, [(0.0, 0.12), (0.04, 0.40), (0.07, 0.75), (0.10, 1.05), (0.17, 1.30)], depth=0.088)]
        parts += [t.sphere(f"vertebra_{i}", 0.038, bone, at=(-0.09 + 0.01 * i, 0, 0.20 + 0.15 * i), subdivisions=2, squash=(1, 0.8, 0.7)) for i in range(6)]
        parts += [shardlet("drift_a", 0.36, 1.52, 0.05, glow, lean=-24), shardlet("drift_b", 0.42, 1.34, 0.04, steel, lean=-36), shardlet("drift_c", 0.26, 1.62, 0.035, steel, lean=-12)]
        parts += [wing_guard(bone, reach=0.34, thick=0.07, feathers=4)] + eye(glow, steel, at=0.01, size=0.065, depth=0.12)
        parts += hilt(hide, m("hexa_cord", (0.30, 0.04, 0.14)), bone, length=0.32, pommel="facet", gem=glow, size=0.06)
        return parts

    swords = {"abyss_hexblade": abyss, "worn_hexblade": worn, "hunters_hexblade": hunters, "bronze_hexblade": bronze_, "iron_hexblade": iron_, "ridge_hexblade": ridge,
              "warden_hexblade": warden, "steel_hexblade": steel_, "grave_hexblade": grave, "ember_hexblade": ember, "hollow_hexblade": hollow,
              "shard_hexblade": shard, "kiln_hexblade": kiln}
    return k.finish(name, swords[name]())
