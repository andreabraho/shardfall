"""The Ninja's twelve pairs of daggers (CLS-03, redrawn 2026-10-02), each its own design.

"The same work on the Warrior and the Ninja" (your call): a knife, a skinning fang with a gut hook,
a three-tined spike, a ringed throwing blade, a forward-bent chopper, a parrying dirk, a broad
five-fingered blade, a needle, a claw on a ring, three talons on one hilt, a splinter of crystal,
a tongue of flame. One is held in each hand (visuals.json "blade", "off_hand").

Built with swordkit as any sword — the grip (named `grip`) at the origin, the blade up Z, its flat
facing Y — but short: about 0.6 to 0.9 above the guard, two cells tall in the bag.
"""

import math

import classkit as c
import hexkit as h
import swordkit as k
import temins as t

flat = h.flat


def hilt(grip, wrap, metal, length=0.24, pommel="disc", gem=None, wraps=4, size=0.045, ring=False):
    parts = k.grip(length, 0.036, grip, wrap=wrap, wraps=wraps)
    if ring:
        return parts + [c.hoop("pommel", (0, 0, -length - 0.05), 0.05, 0.016, metal, segments=14, sides=6)]
    return parts + k.pommel(pommel, size, metal, at=-length - 0.03, gem=gem)


def build(name):
    """The dagger called `name`, one of the twelve."""
    m = t.material
    leather, hide = m("leather"), m("leather_dark")
    bone = m("bone")
    gold = m("gold", metal=0.8, rough=0.3)

    def worn():
        # Coltelli Consunti: a plain knife, one edge, a chip out of it, cord for a grip.
        steel = m("worn_steel", (0.44, 0.42, 0.40), metal=0.4, rough=0.6)
        outline = [(-0.045, 0.0), (0.05, 0.0), (0.06, 0.30), (0.035, 0.34), (0.06, 0.40), (0.05, 0.56), (-0.02, 0.70), (-0.045, 0.50)]
        return [k.blade(outline, 0.05, steel, edge=0.02), t.box("bolster", (0.12, 0.06, 0.035), m("worn_iron", (0.24, 0.23, 0.23), metal=0.45, rough=0.6), at=(0, 0, 0.0), bevel=0.008)] + \
            hilt(m("wood"), m("worn_cord", (0.50, 0.42, 0.28)), m("wood_dark"), wraps=5, pommel="disc", size=0.04)

    def hunters():
        # Zanne del Cacciatore: a skinning knife curved like a fang, a gut hook on its back, a grip of antler.
        steel = m("hunter_steel", (0.56, 0.56, 0.55), metal=0.55, rough=0.42)
        outline = [(-0.04, 0.0), (0.05, 0.0), (0.075, 0.26), (0.10, 0.50), (0.07, 0.70), (-0.03, 0.82), (0.0, 0.66), (-0.02, 0.58), (-0.075, 0.56), (-0.04, 0.50), (-0.02, 0.44), (-0.045, 0.24)]
        return [k.blade(outline, 0.055, steel, edge=0.022), t.cylinder("bolster", 0.05, 0.045, bone, at=(0, 0, 0.0), sides=12, bevel=0.008)] + \
            hilt(bone, hide, bone, wraps=3, pommel="ball", size=0.045) + [c.tube("lanyard", c.curve([(0, 0, -0.29), (0.05, 0, -0.36), (0.02, 0, -0.44)]), 0.008, hide, sides=4)]

    def bronze():
        # Stiletti di Bronzo: a long spike between two shorter tines turned up from the guard.
        metal = m("bronze_blade", (0.66, 0.42, 0.16), metal=0.75, rough=0.35)
        red = m("bronze_cord", (0.46, 0.10, 0.06))
        parts = [k.blade(k.mirrored([(0.032, 0.0), (0.034, 0.50), (0.0, 0.86)]), 0.06, metal, edge=0.02)]
        for side in (-1, 1):
            parts.append(c.tube(f"tine_{side}", c.curve([(side * 0.02, 0, 0.0), (side * 0.13, 0, 0.02), (side * 0.16, 0, 0.16), (side * 0.13, 0, 0.36)]), (0.026, 0.006), metal, sides=7))
        parts.append(t.sphere("boss", 0.045, metal, at=(0, 0, 0.0), subdivisions=2))
        return parts + hilt(leather, red, metal, wraps=5, pommel="ball", size=0.04)

    def iron():
        # Pugnali di Ferro: a throwing blade — a leaf of iron, a bound grip, a ring at its end.
        steel = m("iron_blade", (0.60, 0.61, 0.64), metal=0.7, rough=0.32)
        dark = m("iron_dark", (0.24, 0.25, 0.28), metal=0.6, rough=0.45)
        outline = k.mirrored([(0.03, 0.0), (0.035, 0.06), (0.10, 0.24), (0.085, 0.44), (0.0, 0.76)])
        return [k.blade(outline, 0.055, steel, edge=0.022), k.fuller(0.12, 0.48, 0.012, 0.062, dark), t.cylinder("bolster", 0.042, 0.04, dark, at=(0, 0, 0.0), sides=12, bevel=0.006)] + \
            hilt(hide, m("iron_cord", (0.40, 0.40, 0.42)), dark, wraps=6, ring=True)

    def ridge():
        # Uncini del Crinale: the ridge folk's forward-bent chopper, a notch at its foot, a spike on its back.
        steel = m("ridge_steel", (0.52, 0.56, 0.58), metal=0.65, rough=0.38)
        iron_ = m("iron", metal=0.5, rough=0.55)
        outline = [(-0.045, 0.0), (0.05, 0.0), (0.05, 0.08), (0.075, 0.10), (0.07, 0.22), (0.14, 0.44), (0.155, 0.58), (0.11, 0.72), (0.04, 0.78), (0.015, 0.56),
                   (-0.07, 0.52), (-0.02, 0.44), (-0.03, 0.30), (-0.05, 0.14)]
        return [k.blade(outline, 0.07, steel, edge=0.025), t.cylinder("bolster", 0.055, 0.05, iron_, at=(0, 0, 0.0), sides=16, bevel=0.008)] + \
            hilt(m("wood_dark"), m("moss", rough=0.85), iron_, length=0.26, wraps=5, pommel="disc", size=0.05)

    def warden():
        # Daghe del Custode: a parrying dirk — a narrow blued blade between two arms of gold swept up to catch a sword.
        steel = m("warden_blade", (0.42, 0.56, 0.86), metal=0.75, rough=0.28)
        ice = m("ice", (0.55, 0.85, 1.0), glow=1.0, rough=0.2)
        parts = [k.blade(k.mirrored([(0.045, 0.0), (0.05, 0.40), (0.0, 0.84)]), 0.055, steel, edge=0.022), k.fuller(0.06, 0.52, 0.01, 0.062, ice)]
        arm = [(0.0, -0.03), (0.10, -0.03), (0.20, 0.04), (0.23, 0.20), (0.19, 0.34), (0.17, 0.20), (0.14, 0.09), (0.08, 0.04), (0.0, 0.04)]
        parts.append(flat("guard", arm + [(-x, y) for x, y in reversed(arm[1:-1])], 0.06, gold, bevel=0.01))
        parts.append(t.sphere("gem", 0.036, ice, at=(0, 0, 0.0), subdivisions=1, squash=(1, 1.5, 1)))
        return parts + hilt(m("warden_cord", (0.10, 0.18, 0.46)), gold, gold, wraps=4, pommel="ball", size=0.045)

    def steel_():
        # Pugnali d'Acciaio: a broad blade a hand wide at the guard, three grooves down it.
        steel = m("steel_blade", (0.74, 0.76, 0.80), metal=0.8, rough=0.25)
        dark = m("steel_dark", metal=0.5, rough=0.5)
        parts = [k.blade(k.mirrored([(0.13, 0.0), (0.12, 0.20), (0.0, 0.80)]), 0.06, steel, edge=0.024)]
        for i, x in enumerate((-0.05, 0.0, 0.05)):
            parts.append(flat(f"groove_{i}", [(x - 0.012, 0.06), (x + 0.012, 0.06), (x * 0.5 + 0.008, 0.40 + 0.10 * (x == 0)), (x * 0.5 - 0.008, 0.40 + 0.10 * (x == 0))], 0.066, dark))
        parts += [k.crossguard(0.19, 0.05, 0.06, gold, droop=0.04, flare=0.3)]
        return parts + hilt(m("steel_cord", (0.50, 0.08, 0.08)), gold, gold, wraps=4, pommel="facet", size=0.045)

    def grave():
        # Sussurri della Tomba: a needle of grave steel, a thread of pale light down it, a little skull at the end.
        steel = m("grave_steel", (0.34, 0.32, 0.42), metal=0.6, rough=0.4)
        glow = m("grave_light", (0.80, 0.70, 1.0), glow=1.1)
        parts = [k.blade(k.mirrored([(0.03, 0.0), (0.036, 0.10), (0.022, 0.60), (0.0, 0.94)]), 0.05, steel, edge=0.018),
                 h.crack(glow, [(0.0, 0.06), (0.0, 0.70)], width=0.007, depth=0.056)]
        for side in (-1, 1):
            parts.append(c.tube(f"rib_{side}", c.curve([(side * 0.02, 0, 0.0), (side * 0.13, 0, 0.03), (side * 0.17, 0, 0.14)]), (0.022, 0.006), bone, sides=6))
        parts += k.grip(0.24, 0.036, bone, wrap=m("grave_cord", (0.22, 0.14, 0.34)), wraps=4)
        parts += [t.sphere("pommel", 0.055, bone, at=(0, 0, -0.28), subdivisions=2), t.sphere("socket_a", 0.016, glow, at=(-0.022, -0.046, -0.275), subdivisions=1),
                  t.sphere("socket_b", 0.016, glow, at=(0.022, -0.046, -0.275), subdivisions=1)]
        return parts

    def ember():
        # Zanne di Brace: a claw — a black blade curved round on itself, its inner edge alight, a ring for the finger.
        steel = m("ember_blade", (0.12, 0.10, 0.10), metal=0.6, rough=0.4)
        glow = m("ember_edge", (1.0, 0.55, 0.10), glow=1.4)
        outline = [(-0.05, 0.0), (0.05, 0.0), (0.09, 0.24), (0.07, 0.48), (-0.04, 0.68), (-0.22, 0.76), (-0.33, 0.66), (-0.20, 0.64), (-0.09, 0.52), (-0.03, 0.34), (-0.045, 0.16)]
        parts = k.glowing_edge(outline, 0.065, steel, glow, inset=0.80)
        parts += [t.cylinder("bolster", 0.05, 0.045, m("ember_iron", (0.30, 0.28, 0.30), metal=0.6, rough=0.45), at=(0, 0, 0.0), sides=12, bevel=0.008)]
        return parts + hilt(hide, m("ember_cord", (0.56, 0.10, 0.06)), m("ember_iron", (0.30, 0.28, 0.30), metal=0.6, rough=0.45), wraps=4, ring=True)

    def hollow():
        # Artigli Cavi: three talons on one hilt, hooked forward, red inside the hook.
        steel = m("reaver_steel", (0.26, 0.26, 0.30), metal=0.6, rough=0.4)
        red = m("blood_glow", (0.95, 0.12, 0.08), glow=1.3, rough=0.4)
        horn = m("horn", (0.20, 0.15, 0.12), rough=0.6)
        parts = []
        for i, (x, reach) in enumerate(((-0.10, 0.62), (0.0, 0.78), (0.10, 0.62))):
            talon = [(x - 0.03, 0.04), (x + 0.035, 0.04), (x + 0.055, reach * 0.55), (x + 0.02, reach * 0.86), (x - 0.10, reach), (x - 0.03, reach * 0.80), (x - 0.01, reach * 0.50)]
            parts += [flat("blade" if i == 1 else f"talon_{i}", talon, 0.055, steel, edge=0.02),
                      flat(f"talon_glow_{i}", [(x - 0.012, 0.10), (x + 0.012, 0.10), (x + 0.02, reach * 0.52), (x - 0.0, reach * 0.74)], 0.062, red)]
        parts += [t.box("knuckle", (0.30, 0.08, 0.07), horn, at=(0, 0, 0.03), bevel=0.02)]
        return parts + hilt(hide, m("reaver_cord", (0.36, 0.05, 0.06)), horn, wraps=4, pommel="facet", size=0.045)

    def shard():
        # Schegge di Frammento: a splinter of crystal bound to a hilt of stone, lesser splinters beside it.
        crystal = m("shard_blade", (0.95, 0.60, 1.0), glow=0.8)
        deep = m("shard_core", (0.50, 0.26, 0.70), metal=0.3, rough=0.25)
        stone = m("stone_dark")
        parts = [flat("blade", [(-0.06, 0.02), (0.05, 0.02), (0.09, 0.30), (0.05, 0.60), (0.0, 0.88), (-0.07, 0.56), (-0.085, 0.26)], 0.075, deep, edge=0.028),
                 flat("fuller", [(-0.025, 0.08), (0.02, 0.08), (0.045, 0.30), (0.02, 0.56), (0.0, 0.74), (-0.035, 0.52), (-0.045, 0.28)], 0.082, crystal),
                 flat("side_a", [(0.04, 0.04), (0.12, 0.10), (0.15, 0.32), (0.08, 0.24)], 0.05, crystal, edge=0.018),
                 flat("side_b", [(-0.04, 0.04), (-0.11, 0.08), (-0.14, 0.24), (-0.08, 0.20)], 0.05, crystal, edge=0.018),
                 t.rock("bolster", 0.07, stone, at=(0, 0, 0.0), seed=3, squash=(1.2, 0.8, 0.6))]
        return parts + hilt(hide, m("shard_cord", (0.36, 0.20, 0.50)), stone, wraps=4, pommel="facet", size=0.05)

    def kiln():
        # Lingue della Fornace: a tongue of flame — a blade in waves, white-hot in gold, fire at its heart.
        steel = m("kiln_blade", (0.95, 0.93, 0.86), metal=0.6, rough=0.25, glow=0.25)
        fire = m("kiln_fire", (1.0, 0.62, 0.12), glow=1.5)
        left, right = [], []
        for i in range(8):
            z = 0.04 + 0.11 * i
            sway = 0.035 * math.sin(i * 1.6)
            w = 0.075 * (1 - i / 9)
            right.append((sway + w, z))
            left.append((sway - w, z))
        outline = right + [(0.02, 0.96)] + left[::-1]
        parts = k.glowing_edge(outline, 0.065, steel, fire, inset=0.62)
        parts += [k.crossguard(0.16, 0.05, 0.06, gold, flare=0.5), t.sphere("gem", 0.04, fire, at=(0, 0, 0.0), subdivisions=1, squash=(1, 1.4, 1))]
        return parts + hilt(m("kiln_wrap", (0.42, 0.08, 0.06)), gold, gold, wraps=5, pommel="cup", gem=fire, size=0.045)

    daggers = {"worn_knives": worn, "hunters_fangs": hunters, "bronze_spikes": bronze, "iron_daggers": iron, "ridge_hooks": ridge, "warden_dirks": warden,
               "steel_daggers": steel_, "grave_whispers": grave, "ember_fangs": ember, "hollow_talons": hollow, "shard_splinters": shard, "kiln_tongues": kiln}
    return k.finish(name, daggers[name]())
