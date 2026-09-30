"""A four-legged beast from its proportions: a wolf, a fox, a rat are the same body drawn with
different numbers (PIP-03).

    looks = beast.Proportions(height=0.74, half_length=0.40, ...)
    made = beast.make("wolf", looks, colours)

make() lays out the skeleton, lofts the body, neck, head, jaw, legs, ears and tail round it and
paints the coat; the caller adds what is its own (a mane, a ruff, whiskers) with tuft() and
made.add(), then calls made.finish(style) to skin it and key its clips.
"""

import math
from dataclasses import dataclass

from mathutils import Vector

import creaturekit as c
import temins as t


@dataclass
class Proportions:
    height: float = 0.74          # top of the withers
    half_length: float = 0.40     # middle of the body to the withers (and to the rump)
    girth: float = 0.17           # half the body's width
    depth: float = 0.22           # half the chest's height
    leg: float = 1.0              # leg thickness, as a share of a wolf's
    neck: float = 0.30            # withers to the back of the skull
    neck_angle: float = 38.0      # degrees the neck leans forward from upright
    head: float = 0.34            # back of the skull to the nose
    skull: float = 0.11           # half the skull's width
    muzzle: float = 1.0           # the muzzle's thickness, as a share of a wolf's
    head_tilt: float = 6.0        # degrees the head looks down
    eye: float = 1.0              # eye size, as a share of a wolf's
    eyes_aside: float = 0.45      # how far the eyes look to the side rather than ahead (0..1)
    brow: float = 1.0             # how heavy the brow over the eyes is; 0 for none
    ear: float = 0.13             # ear length
    ear_width: float = 0.045
    ear_round: bool = False       # a rodent's round ears rather than a dog's points
    tail: float = 0.44            # tail length
    tail_bush: float = 0.08       # the tail's widest half-width
    tail_droop: float = 45.0      # degrees the tail hangs below level
    tail_thin: bool = False       # a rodent's thin tail rather than a brush
    haunch: float = 1.0           # the hind quarters, as a share of the chest
    toes: int = 4                 # toes on each paw


class Made:
    def __init__(self, name, rig, arm, spec, parts, anchors):
        self.name, self.rig, self.arm, self.spec, self.parts = name, rig, arm, spec, parts
        self.anchors = anchors

    def add(self, obj, bones):
        self.parts.append((obj, bones))

    def finish(self, style):
        c.bind(self.name, self.arm, self.rig, self.parts)
        c.quadruped_clips(self.arm, self.spec, style)


def tuft(name, root, tip, width, mat, sides=6, flat=0.7, across=(1, 0, 0)):
    """A lock of fur: a cone from `root` to `tip`, `flat` as thick as it is wide."""
    root, tip = Vector(root), Vector(tip)
    return c.loft(name, [(root, width, width * flat), (root.lerp(tip, 0.4), width * 0.8, width * flat * 0.75),
                         (tip, width * 0.06, width * 0.05)], mat, sides=sides, per=2, across=across)


def fangs(made, mat, length, spread=0.26, at=0.84, lift=-0.40):
    """Two teeth down from the upper jaw, the canines that show when it bites."""
    along, S, up = made.anchors["along"], made.anchors["S"], made.anchors["up"]
    for side in (1, -1):
        root = along(at, lift + 0.12, spread * side)
        made.add(tuft(f"fang{side}", root, root - up * (length + S * 0.12), S * 0.075, mat, sides=8), ["head"])


def whiskers(made, mat, count=3, length=1.6):
    """Thin whiskers fanned out from the sides of the muzzle."""
    along, S = made.anchors["along"], made.anchors["S"]
    for side in (1, -1):
        for i in range(count):
            root = along(0.84, -0.12 - i * 0.08, 0.30 * side)
            tip = root + Vector((side * S * length, S * 0.5 * length, S * (0.25 - 0.25 * i) * length))
            made.add(tuft(f"whisker{side}{i}", root, tip, S * 0.022, mat, sides=4), ["head"])


def paw(name, heel, ground, G, k, fur, pad, claw, toes):
    """
    The foot at the end of a leg: the leg runs on down into a rounded paw with its toes in
    front and a claw at each, so a foot is part of its leg and not a pebble under it.
    Returns the pieces; the first is the tube that carries on from the leg.
    """
    x = ground.x
    ball = Vector((x, ground.y + G * 0.04 * k, G * 0.15 * k))
    tube = c.loft(name, [
        (heel, G * 0.20 * k, G * 0.21 * k),
        (heel.lerp(ball, 0.55) + Vector((0, 0, G * 0.03 * k)), G * 0.21 * k, G * 0.20 * k),
        (ball, G * 0.26 * k, G * 0.16 * k),
        (Vector((x, ground.y - G * 0.12 * k, G * 0.12 * k)), G * 0.25 * k, G * 0.12 * k),
        (Vector((x, ground.y - G * 0.20 * k, G * 0.09 * k)), G * 0.10 * k, G * 0.06 * k),
    ], fur, sides=14)
    pieces = [tube]
    spread = [(-1.5 + i) / max(toes - 1, 1) * 2 for i in range(toes)] if toes > 1 else [0]
    for i, s in enumerate(spread):
        middle = 1 - abs(s) / 1.5
        toe = Vector((x + s * G * 0.085 * k, ground.y - G * (0.17 + 0.04 * middle) * k, G * 0.075 * k))
        pieces.append(c.blob(f"{name}_toe{i}", (G * 0.075 * k, G * 0.09 * k, G * 0.07 * k), pad, toe, subdivisions=2))
        tip = toe + Vector((0, -G * 0.13 * k, -G * 0.055 * k))
        pieces.append(tuft(f"{name}_claw{i}", toe + Vector((0, -G * 0.05 * k, 0)), tip, G * 0.028 * k, claw, sides=6))
    return pieces


def eye(made, side, iris, rim, pupil, glint, size, aside, lift=0.46, at=0.40, out=0.74):
    """
    An eye set into the skull: a dark rim, the iris, the pupil and a point of light, all
    facing the way the eye looks — ahead for a hunter, out to the side for prey.
    """
    along, S, up, ahead = made.anchors["along"], made.anchors["S"], made.anchors["up"], made.anchors["ahead"]
    centre = along(at, lift, out * side)
    look = (Vector((side, 0, 0)) * (0.35 + 0.65 * aside) + ahead * (1 - aside) + up * 0.1).normalized()
    r = S * 0.19 * size
    made.add(c.blob(f"rim{side}", (r * 1.25, r * 1.25, r * 1.05), rim, centre), ["head"])
    made.add(c.blob(f"eye{side}", (r, r, r), iris, centre + look * r * 0.35), ["head"])
    made.add(c.blob(f"pupil{side}", (r * 0.52, r * 0.52, r * 0.52), pupil, centre + look * r * 0.78), ["head"])
    made.add(c.blob(f"glint{side}", (r * 0.17, r * 0.17, r * 0.17), glint, centre + look * r * 1.2 + up * r * 0.32 + ahead * r * 0.15, subdivisions=2), ["head"])
    return centre, look, r


def make(name, p, coat):
    """
    Lays the beast out and lofts it. `coat` names its materials: fur, and optionally dark
    (the saddle along the back), pale (belly, throat and underjaw), nose, eye (the iris),
    pupil, lip (the line of the mouth), mouth (inside it), tooth, pad (toes), claw, inner
    (inside the ears; a rodent's bare tail).
    """
    fur = coat["fur"]
    H, L, G, D = p.height, p.half_length, p.girth, p.depth
    z = H - D  # the body's middle line under the withers

    mid = Vector((0, 0, z))
    rump = Vector((0, L, z + D * 0.05))
    withers = Vector((0, -L, z + D * 0.15))
    lean = math.radians(p.neck_angle)
    poll = withers + Vector((0, -math.sin(lean), math.cos(lean))) * p.neck
    tilt = math.radians(p.head_tilt)
    ahead = Vector((0, -math.cos(tilt), -math.sin(tilt)))
    up = ahead.cross(Vector((1, 0, 0))).normalized()
    nose = poll + ahead * p.head
    S = p.skull

    def along(u, lift=0.0, side=0.0):
        return poll + ahead * (p.head * u) + up * (S * lift) + Vector((side * S, 0, 0))

    def lift_of(q):
        return (q - poll).dot(up) / S

    # The tail leaves the rump and curves down to its droop.
    droop = math.radians(p.tail_droop)
    tail_root = rump + Vector((0, D * 0.35, D * 0.35))
    tail_points = [tail_root]
    for i in range(1, 5):
        bend = droop * (i / 4) ** 0.8
        step = p.tail / 4
        tail_points.append(tail_points[-1] + Vector((0, math.cos(bend), -math.sin(bend))) * step)

    w = G * 0.55
    front = [Vector((w, -L * 0.82, z + D * 0.55)), Vector((w, -L * 0.72, H * 0.50)),
             Vector((w, -L * 0.80, H * 0.13)), Vector((w, -L * 0.98, 0.0))]
    back = [Vector((w, L * 0.86, z + D * 0.45)), Vector((w, L * 0.58, H * 0.52)),
            Vector((w, L * 0.95, H * 0.22)), Vector((w, L * 0.80, 0.0))]

    spec = {
        "mid": mid, "rump": rump, "withers": withers, "poll": poll, "nose": nose,
        "jaw": (along(0.28, -0.35), along(0.95, -0.55)),
        "tail": tail_points, "front": front, "back": back, "length": 2 * L,
    }
    rig = c.quadruped_rig(spec)
    arm = rig.build(name + "_rig")
    parts = []
    made = Made(name, rig, arm, spec, parts, {"along": along, "S": S, "up": up, "ahead": ahead})

    # Body and neck, one tube from the rump to the skull.
    hq = p.haunch
    body_rings = [
        ((0, L * 1.28, z + D * 0.12), G * 0.30 * hq, D * 0.30 * hq),
        ((0, L * 1.12, z + D * 0.05), G * 0.82 * hq, D * 0.78 * hq),
        ((0, L * 0.80, z), G * 0.95 * hq, D * 0.86 * hq),
        ((0, L * 0.35, z + D * 0.14), G * 0.84, D * 0.72),
        ((0, -L * 0.05, z), G * 0.92, D * 0.88),
        ((0, -L * 0.50, z - D * 0.06), G * 1.0, D * 1.0),
        ((0, -L * 0.88, z + D * 0.02), G * 0.98, D * 0.98),
    ]
    neck_r = G * 0.62
    for u in (0.2, 0.55, 0.9):
        centre = withers.lerp(poll, u) + Vector((0, 0, -D * 0.25 * (1 - u)))
        body_rings.append((centre, neck_r * (1.25 - 0.3 * u), neck_r * (1.35 - 0.35 * u)))
    body_rings.append((poll + ahead * (S * 0.3), neck_r * 0.7, neck_r * 0.7))
    body = c.loft("body", body_rings, fur, sides=22)
    parts.append((body, ["hips", "chest", "neck"]))

    # Head: the skull, the cheeks, the stop under the brow, the muzzle; the jaw under it.
    m = p.muzzle
    head = c.loft("head", [
        (along(-0.02, 0.15), S * 0.45, S * 0.45),
        (along(0.10, 0.30), S * 0.95, S * 0.90),
        (along(0.28, 0.22), S * 1.06, S * 0.90),
        (along(0.44, 0.04), S * 0.78 * m, S * 0.66 * m),
        (along(0.62, -0.08), S * 0.56 * m, S * 0.48 * m),
        (along(0.82, -0.12), S * 0.44 * m, S * 0.38 * m),
        (along(0.96, -0.10), S * 0.32 * m, S * 0.30 * m),
        (along(1.02, -0.08), S * 0.10, S * 0.10),
    ], fur, sides=20)
    parts.append((head, ["neck", "head"]))

    under = coat.get("pale", fur)
    jaw = c.loft("jaw", [
        (along(0.24, -0.38), S * 0.58, S * 0.22),
        (along(0.48, -0.50), S * 0.46 * m, S * 0.20 * m),
        (along(0.76, -0.53), S * 0.34 * m, S * 0.16 * m),
        (along(0.92, -0.50), S * 0.22 * m, S * 0.11 * m),
        (along(0.95, -0.50), S * 0.05, S * 0.05),
    ], under, sides=16)
    parts.append((jaw, ["jaw"]))

    # The nose: broad at the top, a dark button.
    nose_mat = coat.get("nose", fur)
    parts.append((c.blob("nose", (S * 0.24 * m, S * 0.18, S * 0.15), nose_mat, along(1.0, 0.04)), ["head"]))

    # Inside the mouth, hidden until the jaw drops; the dark line of the lips round it.
    if "mouth" in coat:
        parts.append((c.loft("mouth", [(along(0.34, -0.42), S * 0.34, S * 0.10), (along(0.62, -0.46), S * 0.30 * m, S * 0.08), (along(0.9, -0.44), S * 0.16 * m, S * 0.05)], coat["mouth"], sides=10), ["head"]))
    if "lip" in coat:
        for side in (1, -1):
            line = [(along(0.96, -0.36, 0.14 * side), S * 0.035, S * 0.035),
                     (along(0.78, -0.43, 0.36 * m * side), S * 0.04, S * 0.04),
                     (along(0.52, -0.44, 0.50 * m * side), S * 0.045, S * 0.045),
                     (along(0.36, -0.38, 0.58 * side), S * 0.03, S * 0.03)]
            parts.append((c.loft(f"lip{side}", line, coat["lip"], sides=6), ["head"]))
    if "tooth" in coat:
        for side in (1, -1):
            for i, u in enumerate((0.56, 0.68)):
                root = along(u, -0.40, (0.40 - 0.08 * i) * m * side)
                parts.append((tuft(f"tooth_up{side}{i}", root, root - up * S * 0.12, S * 0.045, coat["tooth"]), ["head"]))
                low = along(u + 0.06, -0.42, (0.36 - 0.08 * i) * m * side)
                parts.append((tuft(f"tooth_low{side}{i}", low - up * S * 0.08, low + up * S * 0.05, S * 0.045, coat["tooth"]), ["jaw"]))

    # The eyes, and a brow over each.
    white = coat.get("glint") or t.material("eye_glint", (1.0, 1.0, 1.0), rough=0.1, glow=0.6)
    rim = coat.get("rim") or t.material("eye_rim", (0.03, 0.03, 0.03), rough=0.5)
    pupil = coat.get("pupil") or t.material("beast_pupil", (0.02, 0.02, 0.02), rough=0.2)
    for side in (1, -1):
        centre, look, r = eye(made, side, coat.get("eye", nose_mat), rim, pupil, white, p.eye, p.eyes_aside)
        if p.brow > 0:
            brow = c.blob(f"brow{side}", (r * 1.35 * p.brow, r * 0.9, r * 0.55 * p.brow), coat.get("dark", fur), centre + up * r * 1.05 + Vector((side * r * 0.1, 0, 0)) - ahead * r * 0.2)
            # Lower at the inner corner, so the look is a scowl rather than surprise.
            c.turned(brow, centre, ahead, -14 * side * p.brow)
            parts.append((brow, ["head"]))

    # Ears, pointed or round, leaning back and out, with the inside of each paler.
    inner = coat.get("inner", under)
    for side in (1, -1):
        base = along(0.12, 0.72, 0.55 * side)
        if p.ear_round:
            tip = base + up * p.ear + Vector((0.3 * p.ear * side, 0.2 * p.ear, 0))
            ear = c.loft(f"ear{side}", [(base, p.ear_width * 0.6, p.ear_width * 0.25), (base.lerp(tip, 0.5), p.ear_width, p.ear_width * 0.22), (tip, p.ear_width * 0.5, p.ear_width * 0.15)], fur, sides=14)
            cup = [(q - Vector((0, p.ear_width * 0.12, 0)), w * 0.78, h * 0.5) for q, w, h in ((base.lerp(tip, 0.2), p.ear_width * 0.6, p.ear_width * 0.2), (base.lerp(tip, 0.55), p.ear_width * 0.9, p.ear_width * 0.2), (base.lerp(tip, 0.9), p.ear_width * 0.45, p.ear_width * 0.12))]
        else:
            tip = base + up * p.ear + Vector((0.18 * p.ear * side, 0.22 * p.ear, 0))
            ear = c.loft(f"ear{side}", [(base, p.ear_width, p.ear_width * 0.40), (base.lerp(tip, 0.5), p.ear_width * 0.72, p.ear_width * 0.28), (tip, p.ear_width * 0.05, p.ear_width * 0.04)], fur, sides=12)
            cup = [(q - Vector((0, p.ear_width * 0.2, 0)), w, h) for q, w, h in ((base.lerp(tip, 0.08), p.ear_width * 0.7, p.ear_width * 0.16), (base.lerp(tip, 0.5), p.ear_width * 0.48, p.ear_width * 0.12), (base.lerp(tip, 0.85), p.ear_width * 0.08, p.ear_width * 0.04))]
        parts.append((ear, ["head"]))
        parts.append((c.loft(f"ear_in{side}", cup, inner, sides=10), ["head"]))

    # Legs, each running on into its paw.
    k = p.leg
    pad = coat.get("pad", coat.get("paw", fur))
    claw = coat.get("claw") or t.material("claw", (0.10, 0.09, 0.08), rough=0.4)
    for side in (1, -1):
        name_side = "L" if side > 0 else "R"
        f = [Vector((q.x * side, q.y, q.z)) for q in front]
        leg = c.loft(f"front{side}", [
            (Vector((f[0].x * 0.4, f[0].y, f[0].z + D * 0.02)), G * 0.30 * k, G * 0.36 * k),
            (f[0], G * 0.36 * k, G * 0.44 * k),
            (f[1], G * 0.30 * k, G * 0.34 * k),
            (f[1].lerp(f[2], 0.55), G * 0.22 * k, G * 0.24 * k),
            (f[2], G * 0.20 * k, G * 0.21 * k),
        ], fur, sides=14)
        parts.append((leg, ["chest", f"front.{name_side}.upper", f"front.{name_side}.lower", f"front.{name_side}.paw"]))
        for piece in paw(f"front_paw{side}", f[2], f[3], G, k, coat.get("paw", fur), pad, claw, p.toes):
            parts.append((piece, [f"front.{name_side}.lower", f"front.{name_side}.paw"] if piece.name == f"front_paw{side}" else [f"front.{name_side}.paw"]))

        b = [Vector((q.x * side, q.y, q.z)) for q in back]
        leg = c.loft(f"back{side}", [
            (Vector((b[0].x * 0.4, b[0].y, b[0].z + D * 0.02)), G * 0.36 * k * hq, G * 0.44 * k * hq),
            (b[0], G * 0.44 * k * hq, G * 0.54 * k * hq),
            (b[0].lerp(b[1], 0.55), G * 0.40 * k * hq, G * 0.48 * k * hq),
            (b[1], G * 0.30 * k, G * 0.34 * k),
            (b[1].lerp(b[2], 0.5), G * 0.24 * k, G * 0.26 * k),
            (b[2], G * 0.20 * k, G * 0.23 * k),
        ], fur, sides=14)
        parts.append((leg, ["hips", f"back.{name_side}.upper", f"back.{name_side}.lower", f"back.{name_side}.paw"]))
        for piece in paw(f"back_paw{side}", b[2], b[3], G, k, coat.get("paw", fur), pad, claw, p.toes):
            parts.append((piece, [f"back.{name_side}.lower", f"back.{name_side}.paw"] if piece.name == f"back_paw{side}" else [f"back.{name_side}.paw"]))

    # Tail: a brush, or a rodent's whip.
    tails = [f"tail{i + 1}" for i in range(len(tail_points) - 1)]
    if p.tail_thin:
        rings = [(q, p.tail_bush * (1 - 0.8 * i / 4), p.tail_bush * (1 - 0.8 * i / 4)) for i, q in enumerate(tail_points)]
        rings.append((tail_points[-1] + (tail_points[-1] - tail_points[-2]) * 0.1, p.tail_bush * 0.1, p.tail_bush * 0.1))
        tail = c.loft("tail", rings, inner, sides=10)
    else:
        widths = (0.45, 0.85, 1.0, 0.8, 0.35)
        rings = [(q, p.tail_bush * wd, p.tail_bush * wd) for q, wd in zip(tail_points, widths)]
        rings.append((tail_points[-1] + (tail_points[-1] - tail_points[-2]) * 0.25, p.tail_bush * 0.05, p.tail_bush * 0.05))
        tail = c.loft("tail", rings, fur, sides=14)
    parts.append((tail, ["hips", *tails]))

    # The coat, by where a face lies rather than which way it points, so each colour ends in
    # a clean line: a darker saddle along the back and up the neck, a pale belly and throat,
    # the underside of the muzzle pale.
    if "dark" in coat:
        c.paint(body, coat["dark"], lambda q, n: q.z > z + D * 0.62 and n.z > 0.15)
        c.paint(tail, coat["dark"], lambda q, n: n.z > 0.35)
        c.paint(head, coat["dark"], lambda q, n: lift_of(q) > 0.55 and (q - poll).dot(ahead) < p.head * 0.36)
    if "pale" in coat:
        c.paint(body, coat["pale"], lambda q, n: q.z < z - D * 0.42 or (q.y < -L * 0.95 and q.z < withers.z + D * 0.2 and n.y < 0.2 and abs(n.x) < 0.75))
        c.paint(head, coat["pale"], lambda q, n: lift_of(q) < -0.28)

    made.anchors.update({"poll": poll, "withers": withers, "tail": tail_points, "body": body, "head": head,
                         "tail_obj": tail, "legs_front": front, "legs_back": back, "lift_of": lift_of, "mid": mid})
    return made
