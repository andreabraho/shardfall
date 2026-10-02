"""The suits and headpieces of the Archer, the Asura and the Dragonseer (CLS-04, 05, 06 — 2026-10-02).

"Their armour in the style of theirs" (your call; the analysis is docs/09-archer-asura-dragonseer.md):
light leather and cloth with a cloak and a bow-arm bracer for the Archer; dark plate with flared,
spiked shoulders, a gorget and a skirt of plates for the Asura; a mantle, a stole down the front,
a wide sash and hanging seals for the Dragonseer — never metal on the last, never plate on the first.

All worn over the class's own clothes, as the Ninja's are (visuals.json "own_clothes"), and built
of the same pieces (ninjakit, armourkit): each rides one bone. Seven suits and four headpieces a
class, one for each of the Warrior's, their twins. `build(name)` makes the one called `name`.
"""

import math
import os

import armourkit as a
import ninjakit as n
import temins as t

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BODIES = os.path.join(ROOT, "game", "assets", "temins", "characters")
WEARER = {
    "archer": os.path.join(BODIES, "archer_woman_body.glb"),
    "asura": os.path.join(BODIES, "asura_woman_body.glb"),
    "seer": os.path.join(BODIES, "seer_woman_body.glb"),
}

RX, RY = n.RX, n.RY


def m(name, colour=None, metal=0.0, rough=0.7, glow=0.0):
    return t.material(name, colour, metal=metal, rough=rough, glow=glow)


def finish(name, by_bone, wearer):
    return a.finish(name, by_bone, underlay=False, reference=WEARER[wearer], replaces=[], focus=((0, 0, 0.75), 0.95))


def finish_head(name, parts, wearer):
    return a.finish(name, {"head": parts}, underlay=False, reference=WEARER[wearer], replaces=[], focus=((0, 0, 1.75), 0.8))


def both(parts):
    """The left pieces and their mirror for the right."""
    return parts, [a.mirror_x(p) for p in parts]


def horns(mat, base_z, out, up, back=0.0, radius=0.05, x=None, name="horn"):
    """A horn at each temple, from the band out and up."""
    rx = n.BROW[0] if x is None else x
    return [a.spike(f"{name}_{side}", (side * (rx - 0.02), 0.02, base_z), (side * (rx + out), 0.02 + back, base_z + up), radius, mat, sides=6)
            for side in (-1, 1)]


# ------------------------------------------------------------------ the Archer

def archer_suit(name, cloth, leather, trim, plate=None, scale=None, cloak=None, collar=None, pads=1, skirt=False, gem=None, cloak_length=0.5):
    """A jerkin, the quiver's strap across it, a guard on the bow arm and a wrap on the other,
    a pad on the bow shoulder (`pads` 2 for both), shin guards; `scale` rows of scales on the
    chest; `cloak` a short cloak down the back, `collar` its hood rolled at the neck; `skirt`
    flaps over the hips; `gem` a clasp at the throat."""
    low, high = n.vest(cloth, hem=trim)

    if scale is not None:
        for row in range(2):
            k = 1 - 0.028 * row
            high += a.scales(f"scale_{row}", RX * k, RY * k, 1.03 + row * 0.07, scale, 16, size=(0.155, 0.02, 0.10), y=0.02, offset=row * 0.5)

    # The quiver's strap: from the right hip to the left shoulder, front and back.
    for y, tag in ((-RY - 0.012, "front"), (RY + 0.045, "back")):
        high.append(n.strip(f"strap_{tag}", (-0.27, y, 0.86), (0.22, y * 0.93, 1.19), 0.07, leather, thick=0.03))

    if gem is not None:
        high.append(t.sphere("clasp", 0.045, gem, at=(0, -RY * 0.86, 1.19), subdivisions=1))

    if cloak is not None:
        high += n.tails(cloak, length=cloak_length, width=0.17, tip=trim)

    if collar is not None:
        high += n.collar(collar)

    hips = n.sash(leather, knot=False)

    if skirt:
        hips += n.tassets(plate or leather, trim=trim, count=1)

    shoulder_l, shoulder_r = both(n.pad(plate or leather, rim=trim, size=0.165))
    bracer_l = n.bracer(leather, trim, plate=plate)
    wrap_r = [a.mirror_x(p) for p in n.wraps(cloth)]
    leg_l, leg_r = both(n.shin(leather, trim=trim, plate=plate))

    by_bone = {
        "spine": low, "chest": high, "hips": hips,
        "upperarm.l": shoulder_l,
        "lowerarm.l": bracer_l, "lowerarm.r": wrap_r,
        "lowerleg.l": leg_l, "lowerleg.r": leg_r,
    }
    if pads > 1:
        by_bone["upperarm.r"] = shoulder_r
    else:
        for p in shoulder_r:
            import bpy
            bpy.data.objects.remove(p, do_unlink=True)

    return finish(name, by_bone, "archer")


def archer_head(name, band, feather=None, feathers=0, plate=None, antlers=None, ends=None, gem=None):
    """A band round the brow; `feathers` standing behind the left ear; `plate` over the brow;
    `antlers` branching up from each temple; `ends` the band's tails hanging behind."""
    rx, ry, y = n.BROW
    parts = n.headband(band, ends=ends)

    if plate is not None:
        parts.append(n.brow_plate(plate, width=44))

    for i in range(feathers):
        base = (rx - 0.02, 0.10 + 0.07 * i, 1.90)
        tip = (rx + 0.05 + 0.03 * i, 0.26 + 0.11 * i, 2.22 + 0.08 * i)
        parts.append(a.spike(f"feather_{i}", base, tip, 0.045, feather, sides=4))

    if antlers is not None:
        for side in (-1, 1):
            root = (side * (rx - 0.02), 0.04, 1.92)
            top = (side * (rx + 0.16), 0.10, 2.34)
            parts.append(a.spike(f"antler_{side}", root, top, 0.045, antlers, sides=6))
            for j, (along, reach) in enumerate(((0.45, 0.20), (0.72, 0.15))):
                at = tuple(root[k] + (top[k] - root[k]) * along for k in range(3))
                parts.append(a.spike(f"tine_{side}_{j}", at, (at[0] + side * reach, at[1] - 0.05, at[2] + reach * 0.9), 0.03, antlers, sides=5))

    if gem is not None:
        parts.append(t.sphere("gem", 0.05, gem, at=(0, y - ry - 0.03, 1.895), subdivisions=1))

    return finish_head(name, parts, "archer")


# ------------------------------------------------------------------ the Asura

def asura_suit(name, steel, trim, cloth, spikes=0, pads=2, gorget=False, skirt=1, splint=True, gem=None, cape=0.0, scale=None, pad_size=0.19):
    """A dark cuirass edged in `trim`, flared guards on the shoulders with `spikes` each, guards
    on both forearms and shins, a sash of `cloth` and `skirt` plates over the hips (0 for none);
    `gorget` a collar of steel; `gem` a stone at the breast; `cape` the length of two tails of
    cloth down the back; `scale` rows of scales over the belly."""
    low, high = n.vest(steel, hem=trim)
    high.append(a.band("breast_rim", RX * 0.92, RY * 0.92, 1.175, 1.215, trim, y=0.02, bevel=0.006))

    if scale is not None:
        for row in range(2):
            low += a.scales(f"belly_{row}", RX, RY, 0.875 + row * 0.07, scale, 18, size=(0.15, 0.02, 0.10), y=0.02, offset=row * 0.5)

    if gorget:
        high.append(a.band("gorget", 0.29, 0.27, 1.16, 1.27, steel, y=0.0, top=0.9, bevel=0.01))
        high.append(a.band("gorget_rim", 0.27, 0.25, 1.255, 1.285, trim, y=0.0, bevel=0.005))

    if gem is not None:
        high.append(t.sphere("heart", 0.062, gem, at=(0, -RY * 0.96, 1.09), subdivisions=1, squash=(1, 0.6, 1.2)))
        high.append(a.band("heart_ring", 0.085, 0.03, 1.02, 1.16, trim, y=-RY * 0.95, bevel=0.004))

    if cape > 0:
        high += n.tails(cloth, length=cape, width=0.19, tip=trim, spread=0.16)

    hips = n.sash(cloth, knot=False)

    if skirt:
        hips += n.tassets(steel, trim=trim, count=skirt)

    shoulder = n.pad(steel, rim=trim, size=pad_size, flat=0.7)
    for i in range(spikes):
        angle = math.radians(-35 + 35 * i) if spikes > 1 else 0.0
        base = (0.41 + 0.06 * math.sin(angle), 0.10 * math.sin(angle * 1.4), 1.27)
        tip = (0.50 + 0.22 * math.sin(angle) + 0.10, 0.22 * math.sin(angle * 1.4), 1.50 - 0.06 * abs(i - (spikes - 1) / 2))
        shoulder.append(a.spike(f"pauldron_spike_{i}", base, tip, 0.05, trim, sides=5))

    shoulder_l, shoulder_r = both(shoulder)
    arm_l, arm_r = both(n.bracer(steel, trim, plate=trim if splint else None))
    leg_l, leg_r = both(n.shin(steel, trim=trim, plate=steel))

    by_bone = {
        "spine": low, "chest": high, "hips": hips,
        "upperarm.l": shoulder_l,
        "lowerarm.l": arm_l, "lowerarm.r": arm_r,
        "lowerleg.l": leg_l, "lowerleg.r": leg_r,
    }
    if pads > 1:
        by_bone["upperarm.r"] = shoulder_r
    else:
        import bpy
        for p in shoulder_r:
            bpy.data.objects.remove(p, do_unlink=True)

    return finish(name, by_bone, "asura")


def asura_head(name, band, trim=None, horn=None, horn_size=(0.12, 0.30), crest=None, crown=0, gem=None, ends=None):
    """A dark band round the brow; `horn` a horn at each temple, `horn_size` how far out and up;
    `crest` a blade standing over the brow; `crown` so many spikes round the band; `gem` a stone
    on the brow; `ends` the band's tails."""
    rx, ry, y = n.BROW
    parts = n.headband(band, ends=ends)

    if trim is not None:
        parts.append(a.band("rim_low", rx + 0.008, ry + 0.008, 1.845, 1.862, trim, sides=40, y=y, bevel=0.004))
        parts.append(a.band("rim_high", rx + 0.008, ry + 0.008, 1.925, 1.942, trim, sides=40, y=y, bevel=0.004))
        parts.append(n.brow_plate(band, z0=1.84, z1=1.99, width=38))

    if horn is not None:
        parts += horns(horn, 1.90, horn_size[0], horn_size[1], back=0.10, radius=0.06)

    if crest is not None:
        front = y - ry - 0.02
        parts.append(a.spike("crest", (0, front, 1.93), (0, front - 0.03, 2.28), 0.05, crest, sides=4))

    for i in range(crown):
        angle = math.radians(-70 + 140 * i / max(1, crown - 1))
        base = ((rx + 0.005) * math.sin(angle), y - (ry + 0.005) * math.cos(angle), 1.93)
        tip = ((rx + 0.06) * math.sin(angle), y - (ry + 0.06) * math.cos(angle), 2.10 + 0.07 * math.cos(angle * 1.3))
        parts.append(a.spike(f"crown_{i}", base, tip, 0.035, trim or band, sides=4))

    if gem is not None:
        parts.append(t.sphere("gem", 0.055, gem, at=(0, y - ry - 0.04, 1.90), subdivisions=1, squash=(1, 0.7, 1.2)))

    return finish_head(name, parts, "asura")


# ------------------------------------------------------------------ the Dragonseer

def seer_suit(name, cloth, trim, sash, stole=None, seals=0, cuffs=True, scale=None, gem=None, tails=0.55, shoulders=None, cord=None):
    """A mantle over the shoulders edged in `trim`, a `stole` down the front in two bands, a wide
    `sash` knotted at the hip, bell cuffs, two tails down the back; `seals` paper seals hanging
    from the sash; `scale` rows of scales on the mantle; `gem` a clasp at the throat;
    `shoulders` a flat cap on each shoulder; `cord` a rope over the sash."""
    mantle = [
        a.band("mantle", RX + 0.035, RY + 0.035, 1.02, 1.215, cloth, top=0.86, y=0.02),
        a.band("mantle_hem", RX + 0.044, RY + 0.044, 1.012, 1.045, trim, y=0.02, bevel=0.006),
    ]
    mantle += n.collar(trim)

    if scale is not None:
        for row in range(2):
            k = 1 - 0.03 * row
            mantle += a.scales(f"scale_{row}", (RX + 0.04) * k, (RY + 0.04) * k, 1.07 + row * 0.065, scale, 16, size=(0.16, 0.02, 0.10), y=0.02, offset=row * 0.5)

    if gem is not None:
        mantle.append(t.sphere("clasp", 0.05, gem, at=(0, -RY * 0.84, 1.20), subdivisions=1))

    mantle += n.tails(cloth, length=tails, width=0.16, tip=trim, top=1.05)

    # The stole: two bands from the collar down the front, hanging free below the sash.
    front = []
    if stole is not None:
        for side in (-1, 1):
            front.append(n.strip(f"stole_{side}", (side * 0.13, -RY - 0.045, 1.14), (side * 0.15, -RY - 0.05, 0.86), 0.12, stole, thick=0.026))
            front.append(n.strip(f"stole_tip_{side}", (side * 0.15, -RY - 0.052, 0.90), (side * 0.15, -RY - 0.052, 0.86), 0.135, trim, thick=0.032))

    hips = n.sash(sash, tail=trim, z0=0.60, z1=0.76, lengths=(0.36, 0.28))

    if cord is not None:
        hips.append(a.roll("cord", 0.418, 0.358, 0.665, 0.70, cord, y=0.0))

    for i in range(seals):
        x = -0.22 + 0.15 * i
        hips.append(t.box(f"seal_{i}", (0.085, 0.012, 0.17), m("seal_paper", (0.93, 0.88, 0.70)), at=(x, -0.375, 0.50), bevel=0.003))
        hips.append(t.box(f"seal_mark_{i}", (0.03, 0.016, 0.09), m("seal_ink", (0.62, 0.12, 0.10)), at=(x, -0.378, 0.50)))

    by_bone = {"chest": mantle, "hips": hips}

    if front:
        by_bone["spine"] = front

    if shoulders is not None:
        cap = n.pad(shoulders, rim=trim, size=0.17, flat=0.45)
        by_bone["upperarm.l"], by_bone["upperarm.r"] = both(cap)

    if cuffs:
        cuff = [a.sleeve("cuff", 0.56, 0.74, 0.150, 0.185, cloth), a.sleeve("cuff_rim", 0.715, 0.745, 0.192, 0.192, trim, bevel=0)]
        by_bone["lowerarm.l"], by_bone["lowerarm.r"] = both(cuff)

    return finish(name, by_bone, "seer")


def seer_head(name, band, trim=None, ends=None, gem=None, disc=None, pendants=None, halo=None, seal=False):
    """A fillet round the brow; `ends` its tails; `gem` a stone on the brow; `disc` a sun standing
    over it; `pendants` a bead hanging at each temple; `halo` a ring behind the head; `seal` a
    paper seal hanging from the knot."""
    rx, ry, y = n.BROW
    parts = n.headband(band, ends=ends)

    if trim is not None:
        parts.append(a.band("rim_low", rx + 0.008, ry + 0.008, 1.845, 1.862, trim, sides=40, y=y, bevel=0.004))
        parts.append(a.band("rim_high", rx + 0.008, ry + 0.008, 1.925, 1.942, trim, sides=40, y=y, bevel=0.004))

    front = y - ry - 0.03

    if gem is not None:
        parts.append(t.sphere("gem", 0.055, gem, at=(0, front, 1.895), subdivisions=1, squash=(1, 0.7, 1.2)))

    if disc is not None:
        parts.append(t.cylinder("disc", 0.12, 0.03, disc, at=(0, front + 0.005, 2.08), sides=20, axis="Y"))
        parts.append(a.spike("disc_stem", (0, front, 1.93), (0, front, 2.0), 0.03, trim or band, sides=4))

    if pendants is not None:
        for side in (-1, 1):
            parts.append(t.cylinder(f"pendant_cord_{side}", 0.01, 0.20, trim or band, at=(side * (rx + 0.012), -0.02, 1.76), sides=6))
            parts.append(t.sphere(f"pendant_{side}", 0.045, pendants, at=(side * (rx + 0.012), -0.02, 1.64), subdivisions=1))

    if halo is not None:
        ring = t.cylinder("halo", 0.50, 0.03, halo, at=(0, ry + 0.10, 2.02), sides=28, axis="Y")
        hole = t.cylinder("halo_core", 0.40, 0.034, band, at=(0, ry + 0.10, 2.02), sides=28, axis="Y")
        parts += [ring, hole]

    if seal:
        parts.append(t.box("seal", (0.09, 0.012, 0.18), m("seal_paper", (0.93, 0.88, 0.70)), at=(-0.40, 0.50, 1.62), bevel=0.003))

    return finish_head(name, parts, "seer")


# ------------------------------------------------------------------ the pieces by name

def build(name):
    gold = m("gold", metal=0.75, rough=0.35)
    silver = m("class_silver", (0.80, 0.82, 0.86), metal=0.7, rough=0.3)
    bronze = m("bronze", metal=0.65, rough=0.42)
    iron = m("iron", metal=0.5, rough=0.55)
    bone = m("bone")
    leather, hide = m("leather"), m("leather_dark")
    ember = m("class_ember", (1.0, 0.42, 0.08), glow=1.3)
    violet = m("class_violet", (0.62, 0.30, 1.0), glow=1.1)
    blood = m("class_blood", (1.0, 0.16, 0.12), glow=1.1)
    jade_glow = m("class_jade_glow", (0.30, 0.90, 0.70), glow=0.9)

    # The Archer: cloth and leather, a cloak, never plate.
    moss, tan = m("archer_moss", (0.16, 0.30, 0.16)), m("archer_tan", (0.50, 0.40, 0.24))
    pieces = {
        "trail_jerkin": lambda: archer_suit(name, tan, hide, leather),
        "hunters_hide": lambda: archer_suit(name, leather, hide, bone, collar=m("archer_fur", (0.62, 0.56, 0.46)), cloak=moss, cloak_length=0.38),
        "fletchers_scales": lambda: archer_suit(name, moss, leather, bronze, scale=m("archer_scale", (0.20, 0.42, 0.22), metal=0.4, rough=0.5), skirt=True, plate=bronze),
        "ridge_ranger": lambda: archer_suit(name, m("ridge_grey", (0.36, 0.40, 0.36)), hide, iron, cloak=m("ridge_cloak", (0.24, 0.30, 0.24)), collar=m("ridge_cloak", (0.24, 0.30, 0.24)), plate=iron, skirt=True, cloak_length=0.58),
        "wardens_mantle": lambda: archer_suit(name, m("warden_blue", (0.20, 0.30, 0.50)), hide, silver, cloak=m("warden_cloak", (0.14, 0.22, 0.40)), collar=m("warden_cloak", (0.14, 0.22, 0.40)),
                                              plate=silver, pads=2, skirt=True, gem=silver, cloak_length=0.66),
        "ashen_stalker": lambda: archer_suit(name, m("ash_cloth", (0.16, 0.16, 0.18)), hide, m("ash_iron", (0.30, 0.30, 0.34), metal=0.6, rough=0.4), cloak=m("ash_cloak", (0.10, 0.10, 0.12)),
                                             collar=m("ash_cloak", (0.10, 0.10, 0.12)), plate=m("ash_iron", (0.30, 0.30, 0.34), metal=0.6, rough=0.4), pads=2, skirt=True, gem=ember,
                                             scale=m("ash_scale", (0.20, 0.20, 0.23), metal=0.5, rough=0.45), cloak_length=0.72),
        "kiln_plume": lambda: archer_suit(name, m("kiln_red", (0.50, 0.12, 0.10)), hide, gold, cloak=m("kiln_cloak", (0.62, 0.14, 0.10)), collar=gold, plate=gold, pads=2, skirt=True, gem=ember,
                                          scale=m("kiln_scale", (0.86, 0.60, 0.20), metal=0.7, rough=0.35), cloak_length=0.78),
        "trail_band": lambda: archer_head(name, tan, feather=m("feather_brown", (0.40, 0.28, 0.16)), feathers=1, ends=tan),
        "ironfeather_band": lambda: archer_head(name, hide, feather=m("feather_grey", (0.60, 0.62, 0.66)), feathers=2, plate=iron),
        "wardens_plume": lambda: archer_head(name, m("warden_blue", (0.20, 0.30, 0.50)), feather=silver, feathers=3, plate=silver, gem=silver),
        "ashen_antlers": lambda: archer_head(name, m("ash_cloth", (0.16, 0.16, 0.18)), antlers=bone, plate=m("ash_iron", (0.30, 0.30, 0.34), metal=0.6, rough=0.4), gem=ember),
    }

    # The Asura: dark plate, crimson cloth, violet light.
    black = m("asura_steel", (0.13, 0.12, 0.16), metal=0.65, rough=0.38)
    crimson = m("asura_crimson", (0.46, 0.07, 0.10))
    pieces.update({
        "branded_wraps": lambda: asura_suit(name, m("asura_cloth", (0.14, 0.13, 0.16)), crimson, crimson, pads=1, skirt=0, splint=False, pad_size=0.16),
        "bloodied_leathers": lambda: asura_suit(name, hide, crimson, crimson, pads=2, skirt=1, splint=False, pad_size=0.17),
        "hexscale_hauberk": lambda: asura_suit(name, black, bronze, crimson, spikes=1, skirt=1, scale=m("asura_scale", (0.22, 0.14, 0.26), metal=0.5, rough=0.45)),
        "ridge_dreadmail": lambda: asura_suit(name, black, iron, crimson, spikes=1, skirt=2, gorget=True, cape=0.45),
        "wardenbane_plate": lambda: asura_suit(name, black, m("asura_gilt", (0.70, 0.50, 0.20), metal=0.75, rough=0.35), crimson, spikes=2, skirt=2, gorget=True, gem=violet, cape=0.58, pad_size=0.21),
        "ashen_tyrant": lambda: asura_suit(name, m("tyrant_steel", (0.10, 0.10, 0.12), metal=0.7, rough=0.34), m("tyrant_red", (0.56, 0.10, 0.10), metal=0.5, rough=0.4), crimson, spikes=3, skirt=2,
                                           gorget=True, gem=blood, cape=0.68, pad_size=0.22, scale=m("tyrant_scale", (0.18, 0.10, 0.12), metal=0.5, rough=0.45)),
        "kiln_fiend": lambda: asura_suit(name, m("fiend_steel", (0.16, 0.10, 0.20), metal=0.7, rough=0.32), gold, m("fiend_cloth", (0.36, 0.10, 0.46)), spikes=3, skirt=2, gorget=True,
                                         gem=violet, cape=0.76, pad_size=0.23, scale=m("fiend_scale", (0.30, 0.16, 0.40), metal=0.55, rough=0.4)),
        "brand_band": lambda: asura_head(name, m("asura_cloth", (0.14, 0.13, 0.16)), ends=crimson),
        "iron_horns": lambda: asura_head(name, black, trim=iron, horn=iron, horn_size=(0.10, 0.22)),
        "wardenbane_crest": lambda: asura_head(name, black, trim=m("asura_gilt", (0.70, 0.50, 0.20), metal=0.75, rough=0.35), horn=black, horn_size=(0.14, 0.32),
                                               crest=m("asura_gilt", (0.70, 0.50, 0.20), metal=0.75, rough=0.35), gem=violet),
        "ashen_crown": lambda: asura_head(name, m("tyrant_steel", (0.10, 0.10, 0.12), metal=0.7, rough=0.34), trim=m("tyrant_red", (0.56, 0.10, 0.10), metal=0.5, rough=0.4),
                                          horn=m("tyrant_steel", (0.10, 0.10, 0.12), metal=0.7, rough=0.34), horn_size=(0.20, 0.42), crown=5, gem=blood),
    })

    # The Dragonseer: robes, a stole, a sash, seals — never metal but for a clasp.
    white, jade = m("seer_white", (0.90, 0.89, 0.84)), m("seer_jade", (0.16, 0.52, 0.44))
    pieces.update({
        "pilgrims_mantle": lambda: seer_suit(name, m("seer_linen", (0.74, 0.70, 0.60)), m("seer_hemp", (0.50, 0.42, 0.28)), m("seer_hemp", (0.50, 0.42, 0.28)), cuffs=False, tails=0.36),
        "reed_mantle": lambda: seer_suit(name, m("seer_reed", (0.56, 0.60, 0.40)), jade, jade, stole=m("seer_linen", (0.74, 0.70, 0.60)), seals=1, tails=0.42),
        "jadescale_vestment": lambda: seer_suit(name, white, jade, jade, stole=jade, seals=2, scale=m("seer_scale", (0.24, 0.62, 0.50), metal=0.35, rough=0.5), tails=0.50, cord=bronze),
        "ridge_oracle": lambda: seer_suit(name, m("oracle_grey", (0.58, 0.62, 0.64)), m("oracle_blue", (0.20, 0.32, 0.50)), m("oracle_blue", (0.20, 0.32, 0.50)), stole=white, seals=2,
                                          shoulders=m("oracle_blue", (0.20, 0.32, 0.50)), tails=0.58, cord=silver),
        "wardens_benediction": lambda: seer_suit(name, white, gold, m("bene_blue", (0.16, 0.28, 0.56)), stole=m("bene_blue", (0.16, 0.28, 0.56)), seals=3, shoulders=white, gem=gold,
                                                 tails=0.66, cord=gold),
        "ashen_vigil": lambda: seer_suit(name, m("vigil_grey", (0.26, 0.26, 0.30)), m("vigil_ember", (0.80, 0.34, 0.10)), m("vigil_dark", (0.14, 0.14, 0.17)), stole=m("vigil_dark", (0.14, 0.14, 0.17)),
                                         seals=3, shoulders=m("vigil_dark", (0.14, 0.14, 0.17)), gem=ember, tails=0.72, cord=m("vigil_ember", (0.80, 0.34, 0.10)),
                                         scale=m("vigil_scale", (0.20, 0.20, 0.24), metal=0.4, rough=0.5)),
        "kiln_dragon": lambda: seer_suit(name, jade, gold, m("dragon_red", (0.62, 0.14, 0.10)), stole=m("dragon_red", (0.62, 0.14, 0.10)), seals=4, shoulders=gold, gem=jade_glow, tails=0.80,
                                         cord=gold, scale=m("dragon_scale", (0.86, 0.62, 0.22), metal=0.7, rough=0.35)),
        "pilgrims_fillet": lambda: seer_head(name, m("seer_linen", (0.74, 0.70, 0.60)), ends=m("seer_linen", (0.74, 0.70, 0.60)), seal=True),
        "jade_fillet": lambda: seer_head(name, white, trim=jade, ends=jade, gem=m("jade_stone", (0.20, 0.70, 0.52), rough=0.3)),
        "wardens_diadem": lambda: seer_head(name, m("bene_blue", (0.16, 0.28, 0.56)), trim=gold, disc=gold, pendants=m("jade_stone", (0.20, 0.70, 0.52), rough=0.3)),
        "ashen_halo": lambda: seer_head(name, m("vigil_dark", (0.14, 0.14, 0.17)), trim=m("vigil_ember", (0.80, 0.34, 0.10)), gem=ember, pendants=ember, halo=ember),
    })

    return pieces[name]()
