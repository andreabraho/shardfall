"""The parts every bow is made of (CLS-04, 2026-10-02), so the Archer's bows read as one family.

A bow lies as KayKit's does, the way the left hand was rigged to hold it: the grip at the
origin, the limbs along Y — about a metre each way — the string towards -X, flat in Z.
"""

import temins as t


def centre(u, curve, recurve):
    """Where the limb's middle line is at `u` along it, -1 at the lower tip to 1 at the upper."""
    return -curve * u * u + recurve * abs(u) ** 5


def limbs(height, curve, recurve, width, mat, thickness=0.06, name="limbs", taper=0.62, flare=0.0):
    """Both limbs as one flat shape: widest at the grip, `taper` thinner at the tips, `flare`
    widening them again at the very end (a bladed bow)."""
    steps = 26
    back, belly = [], []
    for i in range(steps + 1):
        u = -1 + 2 * i / steps
        w = width * (1 - taper * abs(u)) + flare * max(0.0, abs(u) - 0.75) * 4
        x = centre(u, curve, recurve)
        back.append((x + w / 2, u * height))
        belly.append((x - w / 2, u * height))
    return t.profile_solid(name, back + belly[::-1], thickness, mat, bevel=0.012)


def bow(name, wood, grip, height=0.9, curve=0.36, recurve=0.0, width=0.085, string=None,
        caps=None, bands=None, inlay=None, gem=None, spikes=None, flare=0.0, riser=None):
    """One bow. `caps` a material for the tips; `bands` (how many a limb, material); `inlay` a
    material down the middle of the limbs; `gem` a material above the grip; `spikes` (how many a
    limb, material) on the back; `riser` a material for a plate round the grip."""
    parts = [limbs(height, curve, recurve, width, wood, flare=flare)]
    tip = centre(1, curve, recurve)

    if inlay is not None:
        parts.append(limbs(height * 0.9, curve * 0.81, recurve * 0.6, width * 0.34, inlay, thickness=0.068, name="inlay", taper=0.5))

    parts.append(t.cylinder("grip", 0.047, 0.27, grip, at=(0, 0, 0), sides=14, axis="Y"))

    if riser is not None:
        for y in (-0.165, 0.165):
            parts.append(t.box("riser", (width + 0.035, 0.06, 0.082), riser, at=(centre(y / height, curve, recurve), y, 0), bevel=0.008))

    parts.append(t.cylinder("string", 0.013, height * 1.96, string or t.material("bowstring", (0.86, 0.84, 0.74)), at=(tip, 0, 0), sides=6, axis="Y"))

    if caps is not None:
        for side in (-1, 1):
            parts.append(t.box("cap", (0.075 + flare * 0.6, 0.10, 0.078), caps, at=(tip, side * height * 0.955, 0), bevel=0.01))

    if bands is not None:
        count, mat = bands
        for i in range(count):
            for side in (-1, 1):
                u = side * (0.3 + 0.22 * i)
                w = width * (1 - 0.62 * abs(u))
                parts.append(t.box("band", (w + 0.03, 0.035, 0.078), mat, at=(centre(u, curve, recurve), u * height, 0), bevel=0.006))

    if spikes is not None:
        count, mat = spikes
        for i in range(count):
            for side in (-1, 1):
                u = side * (0.28 + 0.2 * i)
                w = width * (1 - 0.62 * abs(u))
                parts.append(t.cylinder("spike", 0.03, 0.13, mat, at=(centre(u, curve, recurve) + w / 2 + 0.05, u * height, 0),
                                        sides=6, radius_top=0.0, axis="X"))

    if gem is not None:
        for y in (-0.2, 0.2):
            parts.append(t.sphere("gem", 0.05, gem, at=(0.02, y, 0), subdivisions=1, squash=(1, 1, 0.9)))

    t.soften(t.join(name, parts), angle=40)
    return {"folder": "weapons", "name": name}


def build(name):
    """The bow called `name`, one of the twelve."""
    m = t.material
    wood, dark = m("wood"), m("wood_dark")
    leather, hide = m("leather"), m("leather_dark")
    bone, iron, gold = m("bone"), m("iron", metal=0.5, rough=0.55), m("gold", metal=0.8, rough=0.3)
    bronze = m("bronze", metal=0.6, rough=0.45)
    steel = m("bow_steel", (0.62, 0.64, 0.68), metal=0.6, rough=0.38)
    forged = m("steel_dark", metal=0.5, rough=0.5)
    silver = m("bow_silver", (0.80, 0.82, 0.86), metal=0.7, rough=0.3)

    bows = {
        "worn_bow": dict(wood=wood, grip=hide, height=0.80, curve=0.30),
        "hunters_bow": dict(wood=dark, grip=leather, height=0.84, curve=0.34, caps=bone, bands=(1, leather)),
        "bronze_bow": dict(wood=wood, grip=hide, height=0.86, curve=0.34, caps=bronze, bands=(2, bronze)),
        "iron_bow": dict(wood=dark, grip=hide, height=0.88, curve=0.36, caps=iron, riser=iron, bands=(1, iron)),
        "ridge_bow": dict(wood=m("ridge_wood", (0.42, 0.40, 0.36)), grip=leather, height=1.0, curve=0.40, recurve=0.10, caps=bone, bands=(2, leather)),
        "warden_bow": dict(wood=m("warden_blue", (0.22, 0.32, 0.50)), grip=hide, height=0.94, curve=0.42, recurve=0.14, caps=silver, riser=silver, bands=(1, silver)),
        "steel_bow": dict(wood=steel, grip=hide, height=0.92, curve=0.42, recurve=0.16, caps=forged, riser=forged),
        "grave_bow": dict(wood=bone, grip=m("grave_wrap", (0.20, 0.16, 0.26)), height=0.94, curve=0.40, recurve=0.12, spikes=(2, bone),
                          gem=m("grave_gem", (0.62, 0.35, 0.95), glow=1.2), string=m("grave_string", (0.72, 0.62, 0.92))),
        "ember_bow": dict(wood=m("ember_wood", (0.16, 0.10, 0.09)), grip=hide, height=0.94, curve=0.42, recurve=0.16,
                          inlay=m("ember_glow", (1.0, 0.42, 0.08), glow=1.3), caps=iron),
        "hollow_bow": dict(wood=m("hollow_black", (0.10, 0.10, 0.13), metal=0.4, rough=0.4), grip=m("hollow_wrap", (0.36, 0.08, 0.10)),
                           height=0.98, curve=0.44, recurve=0.18, flare=0.09, spikes=(1, steel), gem=m("hollow_gem", (1.0, 0.16, 0.12), glow=1.2)),
        "shard_bow": dict(wood=m("shard_crystal", (0.30, 0.86, 0.80), glow=0.8), grip=hide, height=0.98, curve=0.44, recurve=0.18,
                          caps=silver, riser=silver, bands=(2, silver)),
        "kiln_bow": dict(wood=gold, grip=m("kiln_wrap", (0.42, 0.10, 0.08)), height=1.02, curve=0.46, recurve=0.22, flare=0.05,
                         inlay=m("kiln_glow", (1.0, 0.50, 0.10), glow=1.5), caps=gold, riser=gold, gem=m("kiln_gem", (1.0, 0.36, 0.06), glow=1.6),
                         string=m("kiln_string", (1.0, 0.80, 0.45), glow=0.7)),
    }
    return bow(name, **bows[name])
