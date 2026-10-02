"""The parts every fan is made of (CLS-06, 2026-10-02): the Dragonseer's weapon.

A fan is held as a sword is — the grip at the origin, the leaf opening up the Z axis, its flat
facing Y — so the hand that was rigged for swords holds it unchanged. About 0.9 up, 0.3 down.
"""

import math

import bpy

import swordkit as k
import temins as t


def arc(radius, start, end, steps=14):
    """Points on a circle about the pivot, by angle from the +X axis, in the XY plane."""
    return [(radius * math.cos(math.radians(a)), radius * math.sin(math.radians(a)))
            for a in (start + (end - start) * i / steps for i in range(steps + 1))]


def leaf(radius, inner, spread, mat, thickness=0.022, name="leaf", scallop=0.0, ribs=7):
    """The fan's leaf: a sector `spread` degrees wide between `inner` and `radius`. `scallop`
    dips the outer edge between the ribs."""
    start, end = 90 - spread / 2, 90 + spread / 2
    outer = arc(radius, start, end, steps=(ribs - 1) * 4)
    if scallop > 0:
        dipped = []
        for i, (x, y) in enumerate(outer):
            dip = 1 - scallop * (0.5 - 0.5 * math.cos(i * math.pi / 2))
            dipped.append((x * dip, y * dip))
        outer = dipped
    return k.stand(t.profile_solid(name, outer + arc(inner, end, start, steps=8), thickness, mat))


def fan(name, paper, sticks, grip, radius=0.82, spread=130, ribs=7, band=None, scallop=0.0,
        tassel=None, rivet=None, edge=None, crest=None):
    """One fan. `band` a material for a stripe across the leaf; `edge` one for its outer rim;
    `crest` one for a mark at its middle; `tassel` one for the cord hanging from the grip."""
    # Sized to the hand that holds it: the leaves below are drawn larger than they are worn.
    radius *= 0.72
    parts = [leaf(radius, 0.20, spread, paper, scallop=scallop, ribs=ribs)]

    if band is not None:
        parts.append(leaf(radius * 0.72, radius * 0.56, spread, band, thickness=0.028, name="band", ribs=ribs))

    if edge is not None:
        parts.append(leaf(radius * (1.0 - scallop), radius * (0.93 - scallop), spread, edge, thickness=0.03, name="rim", ribs=ribs))

    # The ribs: sticks from the pivot out to the leaf's edge.
    start = 90 - spread / 2
    for i in range(ribs):
        angle = math.radians(start + spread * i / (ribs - 1))
        outer = i in (0, ribs - 1)
        length = radius * (1.0 if outer else 0.97)
        rib = t.box(f"rib_{i}", (0.026 if outer else 0.016, length, 0.034), sticks, at=(0, length / 2, 0), bevel=0.004)
        rib.rotation_euler.z = angle - math.pi / 2
        bpy.context.view_layer.objects.active = rib
        rib.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        rib.select_set(False)
        parts.append(k.stand(rib))

    if crest is not None:
        parts.append(t.cylinder("crest", radius * 0.13, 0.036, crest, at=(0, 0, radius * 0.5), sides=18, axis="Y"))

    parts.append(t.cylinder("rivet", 0.045, 0.06, rivet or sticks, at=(0, 0, 0.0), sides=14, axis="Y"))
    parts.append(t.cylinder("grip", 0.036, 0.26, grip, at=(0, 0, -0.13), sides=14))
    parts.append(t.sphere("pommel", 0.05, rivet or sticks, at=(0, 0, -0.28), subdivisions=1))

    if tassel is not None:
        parts.append(t.cylinder("cord", 0.012, 0.16, tassel, at=(0, 0, -0.40), sides=6))
        parts.append(t.cylinder("tassel", 0.04, 0.16, tassel, at=(0, 0, -0.55), sides=8, radius_top=0.018))

    return k.finish(name, parts)


def build(name):
    """The fan called `name`, one of the twelve."""
    m = t.material
    bamboo, dark = m("bamboo", (0.62, 0.50, 0.28)), m("wood_dark")
    paper = m("paper", (0.90, 0.86, 0.74))
    gold = m("gold", metal=0.8, rough=0.3)
    iron = m("iron", metal=0.5, rough=0.55)
    bronze = m("bronze", metal=0.6, rough=0.45)
    silver = m("fan_silver", (0.80, 0.82, 0.86), metal=0.7, rough=0.3)
    red = m("cloth_red")

    fans = {
        "worn_fan": dict(paper=m("paper_worn", (0.74, 0.68, 0.54)), sticks=bamboo, grip=dark, radius=0.70, spread=110, ribs=5),
        "hunters_fan": dict(paper=m("hide_leaf", (0.56, 0.40, 0.26)), sticks=dark, grip=m("leather"), radius=0.72, spread=116, ribs=6,
                            tassel=m("leather_dark")),
        "bronze_fan": dict(paper=paper, sticks=bronze, grip=dark, radius=0.74, spread=120, ribs=6, band=bronze),
        "iron_fan": dict(paper=m("ink_leaf", (0.30, 0.32, 0.38)), sticks=iron, grip=dark, radius=0.76, spread=124, ribs=7, edge=iron),
        "ridge_fan": dict(paper=m("ridge_leaf", (0.70, 0.74, 0.70)), sticks=bamboo, grip=m("leather"), radius=0.80, spread=130, ribs=7,
                          band=m("moss"), tassel=m("moss")),
        "warden_fan": dict(paper=m("warden_leaf", (0.24, 0.36, 0.58)), sticks=silver, grip=dark, radius=0.80, spread=132, ribs=7,
                           band=silver, crest=silver, tassel=m("warden_cord", (0.80, 0.82, 0.86))),
        "steel_fan": dict(paper=m("steel_leaf", (0.66, 0.68, 0.72), metal=0.6, rough=0.36), sticks=m("steel_dark", metal=0.5, rough=0.5),
                          grip=dark, radius=0.80, spread=134, ribs=8, scallop=0.10),
        "grave_fan": dict(paper=m("grave_leaf", (0.20, 0.16, 0.28)), sticks=m("bone"), grip=m("bone"), radius=0.84, spread=138, ribs=8,
                          scallop=0.14, crest=m("grave_gem", (0.62, 0.35, 0.95), glow=1.2), tassel=m("grave_cord", (0.62, 0.35, 0.95))),
        "ember_fan": dict(paper=m("ember_leaf", (0.42, 0.10, 0.08)), sticks=dark, grip=dark, radius=0.84, spread=138, ribs=8,
                          band=m("ember_glow", (1.0, 0.42, 0.08), glow=1.3), tassel=red),
        "hollow_fan": dict(paper=m("hollow_leaf", (0.10, 0.10, 0.13)), sticks=m("hollow_stick", (0.36, 0.08, 0.10)), grip=dark, radius=0.86,
                           spread=142, ribs=9, scallop=0.16, edge=m("hollow_gem", (1.0, 0.16, 0.12), glow=1.1), tassel=red),
        "shard_fan": dict(paper=m("shard_leaf", (0.30, 0.86, 0.80), glow=0.7), sticks=silver, grip=dark, radius=0.86, spread=142, ribs=9,
                          crest=silver, tassel=m("shard_cord", (0.30, 0.86, 0.80))),
        "kiln_fan": dict(paper=m("jade", (0.22, 0.62, 0.48)), sticks=gold, grip=m("kiln_wrap", (0.42, 0.10, 0.08)), radius=0.90, spread=148,
                         ribs=9, band=gold, edge=gold, crest=m("kiln_gem", (1.0, 0.36, 0.06), glow=1.6),
                         tassel=m("kiln_cord", (0.93, 0.68, 0.24)), rivet=gold),
    }
    return fan(name, **fans[name])
