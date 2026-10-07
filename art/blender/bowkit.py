"""The Archer's twelve bows (CLS-04, 2026-10-02), each its own design.

Redrawn at your call ("big, heavy bows with many branches"): a bow is two thick limbs and what
grows out of their backs — antlers, leaves, thorns, horns, feathers, blades, ribs, roots,
crystals, flames — a different growth for each bow, more of it and longer the better the bow.
Twelve bows were one shape in other colours before.

A bow lies as KayKit's does, the way the left hand was rigged to hold it: the grip at the
origin, the limbs along Y — a metre and more up, half that down — flat in Z. It is drawn with the string
towards -X and the branches towards +X, and turned round at the end (the string towards the body).
"""

import math

import bmesh

import temins as t


# How long the lower limb is, of the upper.
LOWER = 0.52


def centre(u, curve, recurve):
    """Where the limb's middle line is at `u` along it, -1 at the lower tip to 1 at the upper."""
    return -curve * u * u + recurve * abs(u) ** 5


def ribbon(points, widths):
    """The outline of a strip along `points`, as wide as `widths` at each."""
    left, right = [], []
    last = len(points) - 1
    for i, (x, y) in enumerate(points):
        ax, ay = points[max(i - 1, 0)]
        bx, by = points[min(i + 1, last)]
        length = math.hypot(bx - ax, by - ay) or 1.0
        nx, ny = -(by - ay) / length, (bx - ax) / length
        w = widths[i] / 2
        left.append((x + nx * w, y + ny * w))
        right.append((x - nx * w, y - ny * w))
    if widths[-1] < 1e-4:
        right = right[:-1]
    return left + right[::-1]


def limbs(height, curve, recurve, width, mat, thickness=0.085, name="limbs", taper=0.55, flare=0.0):
    """Both limbs as one flat shape: widest at the grip, `taper` thinner at the tips, `flare`
    widening them again at the very end (a bladed bow)."""
    steps = 30
    points, widths = [], []
    for i in range(steps + 1):
        u = -1 + 2 * i / steps
        points.append((centre(u, curve, recurve), u * height))
        widths.append(width * (1 - taper * abs(u)) + flare * max(0.0, abs(u) - 0.75) * 4)
    return t.profile_solid(name, ribbon(points, widths), thickness, mat, bevel=0.014, segments=2)


SHAPES = {
    "horn": lambda s: (1 - s) ** 0.8,
    "blade": lambda s: 1 - s,
    "leaf": lambda s: 0.22 * (1 - s) + (4 * s * (1 - s)) ** 0.6 * 0.9 if s < 1 else 0.0,
    "feather": lambda s: (0.35 + 0.65 * math.sin(math.pi * min(1.0, s * 1.15)) ** 0.7) * (1 - s ** 6) if s < 1 else 0.0,
    "shard": lambda s: 0.55 + 0.45 * s if s < 0.72 else (1 - s) / 0.28,
    "thorn": lambda s: 1 - s,
}


def growth(start, angle, length, width, bend=0.0, style="horn", wave=0.0, steps=9):
    """The middle line and widths of something growing from `start` at `angle` (degrees from +X),
    turning by `bend` degrees over its length; `wave` swings it side to side (a flame)."""
    x, y = start
    a = math.radians(angle)
    points, widths = [], []
    for i in range(steps + 1):
        s = i / steps
        points.append((x, y))
        widths.append(width * SHAPES[style](s))
        turn = math.radians(bend) / steps + math.radians(wave) * math.cos(2 * math.pi * s) * 2 * math.pi / steps
        a += turn
        x += length / steps * math.cos(a)
        y += length / steps * math.sin(a)
    return points, widths


def branch(index, u, length, lean, width, mat, shape, bend=0.0, style="horn", wave=0.0, twigs=(), tip=None, thickness=0.07, inner=None):
    """What grows from the back of the upper limb at `u`, and its mirror on the lower: out and
    towards the tip by `lean` degrees. `twigs` (where along it, length, turn) grow from it in
    turn; `tip` a lit bead at its end; `inner` a material down its middle."""
    height, curve, recurve, limb_width, taper = shape
    du = 0.01
    tx = centre(u + du, curve, recurve) - centre(u - du, curve, recurve)
    ty = 2 * du * height
    out = math.degrees(math.atan2(ty, tx)) - 90
    start = (centre(u, curve, recurve) + limb_width * (1 - taper * abs(u)) * 0.2, u * height)

    points, widths = growth(start, out + lean, length, width, bend, style, wave)
    shapes = [(ribbon(points, widths), mat, thickness)]
    ends = [points[-1]]

    if inner is not None:
        shapes.append((ribbon(points[:-1], [w * 0.36 for w in widths[:-1]]), inner, thickness + 0.012))

    for where, twig_length, turn in twigs:
        i = max(1, min(len(points) - 2, round(where * (len(points) - 1))))
        heading = math.degrees(math.atan2(points[i + 1][1] - points[i - 1][1], points[i + 1][0] - points[i - 1][0]))
        twig_points, twig_widths = growth(points[i], heading + turn, twig_length, widths[i] * 0.8, bend * 0.5 * (1 if turn > 0 else -1), style, wave)
        shapes.append((ribbon(twig_points, twig_widths), mat, thickness * 0.9))
        ends.append(twig_points[-1])

    parts = []
    for side in (1, -1):
        for j, (outline, material, thick) in enumerate(shapes):
            flipped = outline if side > 0 else [(x, -y) for x, y in reversed(outline)]
            parts.append(t.profile_solid(f"branch_{index}_{side}_{j}", flipped, thick, material, bevel=0.008))
        if tip is not None:
            for x, y in ends:
                parts.append(t.sphere(f"bud_{index}_{side}", min(0.045, width * 0.36), tip, at=(x, side * y, 0), subdivisions=1))
    return parts


def bow(name, wood, grip, height=1.0, curve=0.40, recurve=0.0, width=0.14, thick=0.085, taper=0.55, string=None, caps=None, bands=None,
        inlay=None, gem=None, flare=0.0, riser=None, branches=(), knob=None):
    """One bow. `branches` what grows from its limbs (see `branch`); `caps` a material for the
    tips; `bands` (how many a limb, material); `inlay` a material down the middle of the limbs;
    `gem` a material above and below the grip; `riser` a material for the plates round the grip;
    `knob` a material for a boss on the grip's back."""
    shape = (height, curve, recurve, width, taper)
    parts = [limbs(height, curve, recurve, width, wood, thickness=thick, taper=taper, flare=flare)]
    tip = centre(1, curve, recurve)

    if inlay is not None:
        parts.append(limbs(height * 0.9, curve * 0.81, recurve * 0.6, width * 0.30, inlay, thickness=thick + 0.012, name="inlay", taper=0.5))

    parts.append(t.cylinder("grip", 0.058, 0.30, grip, at=(0, 0, 0), sides=14, axis="Y"))

    if riser is not None:
        for y in (-0.19, 0.19):
            parts.append(t.box("riser", (width + 0.05, 0.075, thick + 0.03), riser, at=(centre(y / height, curve, recurve), y, 0), bevel=0.01))

    if knob is not None:
        parts.append(t.sphere("knob", 0.075, knob, at=(0.06, 0, 0), subdivisions=2, squash=(0.8, 1.2, 0.9)))

    parts.append(t.cylinder("string", 0.015, height * 1.96, string or t.material("bowstring", (0.86, 0.84, 0.74)), at=(tip, 0, 0), sides=6, axis="Y"))

    if caps is not None:
        for side in (-1, 1):
            parts.append(t.box("cap", (width * 0.6 + flare * 0.6, 0.11, thick + 0.02), caps, at=(tip, side * height * 0.955, 0), bevel=0.012))

    if bands is not None:
        count, mat = bands
        for i in range(count):
            for side in (-1, 1):
                u = side * (0.26 + 0.2 * i)
                w = width * (1 - taper * abs(u))
                parts.append(t.box("band", (w + 0.035, 0.04, thick + 0.02), mat, at=(centre(u, curve, recurve), u * height, 0), bevel=0.008))

    for i, spec in enumerate(branches):
        parts += branch(i, shape=shape, **spec)

    if gem is not None:
        for y in (-0.24, 0.24):
            parts.append(t.sphere("gem", 0.06, gem, at=(0.03, y, 0), subdivisions=2, squash=(1, 1, 0.9)))

    whole = t.join(name, parts)

    # Turned round (2026-10-02, at your call: the Archer held it backwards): the string towards +X,
    # the side the left hand keeps towards the body.
    mesh = bmesh.new()
    mesh.from_mesh(whole.data)
    for v in mesh.verts:
        v.co.x = -v.co.x
        # Held below its middle, as the great bows are: the lower limb is the shorter, or a bow this
        # long would stand in the ground at the Archer's feet.
        # (In the hand the bow's +Y is the end that points down.)
        if v.co.y > 0:
            v.co.y *= LOWER
    bmesh.ops.reverse_faces(mesh, faces=mesh.faces)
    mesh.to_mesh(whole.data)
    mesh.free()

    t.soften(whole, angle=40)
    return {"folder": "weapons", "name": name}


def B(u, length, lean, width, mat, **more):
    return dict(u=u, length=length, lean=lean, width=width, mat=mat, **more)


def build(name):
    """The bow called `name`, one of the twelve."""
    m = t.material
    wood, dark = m("wood"), m("wood_dark")
    leather, hide = m("leather"), m("leather_dark")
    bone, gold = m("bone"), m("gold", metal=0.8, rough=0.3)
    iron = m("bow_iron", (0.20, 0.21, 0.24), metal=0.6, rough=0.45)
    bronze = m("bronze", metal=0.6, rough=0.45)
    steel = m("bow_steel", (0.62, 0.64, 0.68), metal=0.7, rough=0.32)
    forged = m("steel_dark", metal=0.5, rough=0.5)
    silver = m("bow_silver", (0.80, 0.82, 0.86), metal=0.75, rough=0.28)

    def worn():
        # Arco Consumato: a heavy stave still carrying the stubs of its boughs, bound in cord.
        cord = m("worn_cord", (0.50, 0.42, 0.28))
        return dict(wood=wood, grip=hide, height=0.94, curve=0.30, width=0.15, bands=(2, cord), branches=[
            B(0.34, 0.16, 30, 0.085, wood, bend=20), B(0.62, 0.20, 42, 0.075, wood, bend=-25, twigs=[(0.5, 0.09, 50)]),
            B(0.86, 0.11, 55, 0.055, wood)])

    def hunters():
        # Arco del Cacciatore: dark wood crowned with antlers, tine on tine.
        return dict(wood=dark, grip=leather, height=0.98, curve=0.34, width=0.15, caps=bone, bands=(1, leather), branches=[
            B(0.30, 0.30, 25, 0.075, bone, bend=45, twigs=[(0.45, 0.14, -55), (0.75, 0.10, -50)]),
            B(0.58, 0.36, 38, 0.07, bone, bend=40, twigs=[(0.4, 0.16, -55), (0.7, 0.11, -50)]),
            B(0.84, 0.24, 55, 0.055, bone, bend=30, twigs=[(0.5, 0.10, -55)])])

    def bronze_():
        # Arco di Bronzo: a laurel in bronze — leaves up both limbs.
        return dict(wood=wood, grip=hide, height=1.0, curve=0.34, width=0.15, caps=bronze, bands=(2, bronze), riser=bronze, branches=[
            B(0.24 + 0.14 * i, 0.26 - 0.02 * i, 48 + 3 * i, 0.15 - 0.012 * i, bronze, style="leaf", bend=14, inner=wood) for i in range(5)])

    def iron_():
        # Arco di Ferro: banded in iron, hooks and thorns all the way up.
        return dict(wood=dark, grip=hide, height=1.02, curve=0.36, width=0.16, caps=iron, riser=iron, bands=(3, iron), branches=[
            B(0.22, 0.34, 15, 0.10, iron, bend=85, style="blade"), B(0.42, 0.14, 40, 0.07, iron, style="thorn"),
            B(0.54, 0.30, 28, 0.09, iron, bend=80, style="blade"), B(0.68, 0.13, 45, 0.065, iron, style="thorn"),
            B(0.80, 0.24, 40, 0.08, iron, bend=75, style="blade"), B(0.93, 0.12, 60, 0.05, iron, style="thorn")])

    def ridge():
        # Arco della Cresta: the mountain ram's — great horns curling back on themselves.
        stone = m("ridge_wood", (0.30, 0.29, 0.26))
        horn = m("ridge_horn", (0.70, 0.64, 0.50))
        return dict(wood=stone, grip=leather, height=1.10, curve=0.42, recurve=0.12, width=0.17, caps=horn, bands=(2, m("moss")), branches=[
            B(0.26, 0.62, 10, 0.13, horn, bend=215, twigs=[(0.35, 0.13, -60)]),
            B(0.60, 0.50, 30, 0.11, horn, bend=205, twigs=[(0.35, 0.11, -60)]),
            B(0.88, 0.30, 55, 0.08, horn, bend=170)])

    def warden():
        # Arco del Custode: a pair of wings in silver on blue, feather over feather.
        blue = m("warden_bow_blue", (0.08, 0.16, 0.40))
        return dict(wood=blue, grip=hide, height=1.08, curve=0.42, recurve=0.14, width=0.15, caps=silver, riser=silver, bands=(1, silver),
                    gem=m("warden_ice", (0.55, 0.85, 1.0), glow=1.0, rough=0.2), branches=[
            B(0.22 + 0.115 * i, 0.30 + 0.025 * i, 62 - 4 * i, 0.12, silver, style="feather", bend=-12, inner=blue) for i in range(7)])

    def steel_():
        # Arco d'Acciaio: all blades — the limbs themselves are edged, and more stand out of them.
        return dict(wood=steel, grip=hide, height=1.06, curve=0.42, recurve=0.16, width=0.16, flare=0.07, riser=forged, knob=forged, branches=[
            B(0.20, 0.50, 8, 0.15, steel, style="blade", bend=18, inner=forged), B(0.38, 0.20, 30, 0.09, forged, style="blade"),
            B(0.52, 0.44, 30, 0.13, steel, style="blade", bend=18, inner=forged), B(0.66, 0.18, 45, 0.08, forged, style="blade"),
            B(0.78, 0.36, 50, 0.11, steel, style="blade", bend=16, inner=forged), B(0.94, 0.26, 78, 0.09, steel, style="blade")])

    def grave():
        # Arco della Tomba: a spine and its ribs, the marrow lit.
        glow = m("grave_gem", (0.62, 0.35, 0.95), glow=1.2)
        return dict(wood=bone, grip=m("grave_wrap", (0.16, 0.10, 0.24)), height=1.08, curve=0.40, recurve=0.12, width=0.15, inlay=glow, gem=glow,
                    knob=bone, string=m("grave_string", (0.72, 0.62, 0.92), glow=0.5), branches=[
            B(0.20 + 0.105 * i, 0.44 - 0.035 * i, 20 + 3 * i, 0.07, bone, bend=-110, tip=glow if i % 2 == 0 else None) for i in range(8)])

    def ember():
        # Arco di Brace: a burnt bough still alight — gnarled roots, a coal at every end.
        char = m("ember_wood", (0.10, 0.07, 0.06))
        coal = m("ember_glow", (1.0, 0.42, 0.08), glow=1.3)
        return dict(wood=char, grip=hide, height=1.10, curve=0.42, recurve=0.16, width=0.17, inlay=coal, caps=iron, knob=coal, branches=[
            B(0.22, 0.40, 20, 0.10, char, bend=-50, tip=coal, twigs=[(0.35, 0.20, 60), (0.65, 0.16, -55)], inner=coal),
            B(0.46, 0.44, 35, 0.095, char, bend=45, tip=coal, twigs=[(0.3, 0.20, -60), (0.6, 0.18, 55)], inner=coal),
            B(0.70, 0.36, 48, 0.085, char, bend=-40, tip=coal, twigs=[(0.4, 0.17, 60), (0.7, 0.12, -50)]),
            B(0.90, 0.26, 62, 0.07, char, bend=35, tip=coal, twigs=[(0.5, 0.12, -55)])])

    def hollow():
        # Arco della Conca: black briar — thorns the length of it, two great hooks edged in red.
        black = m("hollow_black", (0.06, 0.06, 0.08), metal=0.4, rough=0.4)
        red = m("hollow_gem", (1.0, 0.16, 0.12), glow=1.2)
        briar = m("hollow_briar", (0.30, 0.03, 0.05))
        thorns = [B(0.18 + 0.075 * i, 0.20 + 0.05 * (i % 3), 30 + 4 * i, 0.09, briar, style="thorn", bend=25 if i % 2 else -25) for i in range(11)]
        return dict(wood=black, grip=m("hollow_wrap", (0.36, 0.06, 0.08)), height=1.12, curve=0.44, recurve=0.18, width=0.17, flare=0.09, gem=red, knob=red,
                    string=m("hollow_string", (0.90, 0.30, 0.26), glow=0.5), branches=thorns + [
            B(0.34, 0.58, 14, 0.15, black, style="blade", bend=95, inner=red), B(0.72, 0.50, 42, 0.13, black, style="blade", bend=90, inner=red)])

    def shard():
        # Arco della Scheggia: silver grown through with crystal — shards standing out of it like frost.
        crystal = m("shard_crystal", (0.30, 0.86, 0.80), glow=0.8)
        deep = m("shard_deep", (0.10, 0.46, 0.50), glow=0.3)
        shards = []
        for i in range(9):
            u = 0.18 + 0.09 * i
            shards.append(B(u, 0.26 + 0.20 * (i % 3 == 0) + 0.08 * (i % 2), 28 + 5 * i + (12 if i % 2 else -10), 0.11 - 0.004 * i, crystal if i % 2 == 0 else deep, style="shard"))
        return dict(wood=silver, grip=hide, height=1.12, curve=0.44, recurve=0.18, width=0.16, inlay=crystal, caps=crystal, riser=silver, branches=shards)

    def kiln():
        # Arco della Fornace: the firebird — gold, every feather a tongue of flame.
        fire = m("kiln_glow", (1.0, 0.22, 0.03), glow=1.5)
        flames = [B(0.18 + 0.10 * i, 0.40 + 0.015 * i, 56 - 3 * i, 0.14, gold, style="feather", bend=-10, wave=4, inner=m("kiln_red", (0.55, 0.05, 0.03)), tip=fire,
                    twigs=[(0.45, 0.16, 48)] if i % 2 == 0 else ()) for i in range(8)]
        return dict(wood=gold, grip=m("kiln_wrap", (0.42, 0.08, 0.06)), height=1.18, curve=0.46, recurve=0.22, width=0.18, flare=0.06, inlay=fire,
                    caps=gold, riser=gold, gem=m("kiln_gem", (1.0, 0.36, 0.06), glow=1.6), knob=m("kiln_gem", (1.0, 0.36, 0.06), glow=1.6),
                    string=m("kiln_string", (1.0, 0.80, 0.45), glow=0.7), branches=flames)

    def abyss():
        # Arco dell'Abisso (P5, 2026-10-07): black limbs grown with bone — ribs curling back off both limbs like a carcass, thorns
        # of crimson glass between them, an eye at the grip.
        black = m("abyss_wood", (0.05, 0.035, 0.07), metal=0.4, rough=0.35)
        glow = m("abyss_glow", (0.95, 0.10, 0.38), glow=1.5)
        ribs = [B(0.18 + 0.13 * i, 0.38 - 0.03 * i, 64 - 6 * i, 0.08, bone, style="horn", bend=-95) for i in range(6)]
        thorns = [B(0.22 + 0.17 * i, 0.16, 30 + 6 * i, 0.06, glow, style="thorn", bend=20) for i in range(4)]
        return dict(wood=black, grip=m("abyss_wrap", (0.20, 0.03, 0.10)), height=1.18, curve=0.46, recurve=0.24, width=0.17, flare=0.08, inlay=glow,
                    caps=bone, riser=black, gem=glow, knob=glow, string=m("abyss_string", (0.95, 0.40, 0.70), glow=0.6), branches=ribs + thorns)

    bows = {"abyss_bow": abyss, "worn_bow": worn, "hunters_bow": hunters, "bronze_bow": bronze_, "iron_bow": iron_, "ridge_bow": ridge, "warden_bow": warden,
            "steel_bow": steel_, "grave_bow": grave, "ember_bow": ember, "hollow_bow": hollow, "shard_bow": shard, "kiln_bow": kiln}
    return bow(name, **bows[name]())
