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
    ear: float = 0.13             # ear length
    ear_width: float = 0.045
    ear_round: bool = False       # a rodent's round ears rather than a dog's points
    tail: float = 0.44            # tail length
    tail_bush: float = 0.08       # the tail's widest half-width
    tail_droop: float = 45.0      # degrees the tail hangs below level
    tail_thin: bool = False       # a rodent's thin tail rather than a brush
    haunch: float = 1.0           # the hind quarters, as a share of the chest


class Made:
    def __init__(self, name, rig, arm, spec, parts, anchors):
        self.name, self.rig, self.arm, self.spec, self.parts = name, rig, arm, spec, parts
        self.anchors = anchors

    def add(self, obj, bones):
        self.parts.append((obj, bones))

    def finish(self, style):
        c.bind(self.name, self.arm, self.rig, self.parts)
        c.quadruped_clips(self.arm, self.spec, style)


def tuft(name, root, tip, width, mat, sides=6):
    """A lock of fur: a cone from `root` to `tip`."""
    root, tip = Vector(root), Vector(tip)
    return c.loft(name, [(root, width, width * 0.7), (root.lerp(tip, 0.45), width * 0.75, width * 0.5), (tip, width * 0.06, width * 0.05)], mat, sides=sides, per=2)


def fangs(made, mat, length, spread=0.28, at=0.86, lift=-0.36):
    """Two teeth down from the upper jaw, the canines that show when it bites."""
    along, S, up = made.anchors["along"], made.anchors["S"], made.anchors["up"]
    for side in (1, -1):
        root = along(at, lift, spread * side)
        made.add(tuft(f"fang{side}", root, root - up * length, S * 0.07, mat, sides=6), ["head"])


def whiskers(made, mat, count=3, length=1.6):
    """Thin whiskers fanned out from the sides of the muzzle."""
    along, S = made.anchors["along"], made.anchors["S"]
    for side in (1, -1):
        for i in range(count):
            root = along(0.86, -0.1 - i * 0.08, 0.30 * side)
            tip = root + Vector((side * S * length, S * 0.5 * length, S * (0.25 - 0.25 * i) * length))
            made.add(tuft(f"whisker{side}{i}", root, tip, S * 0.022, mat, sides=4), ["head"])


def make(name, p, coat):
    """
    Lays the beast out and lofts it. `coat` names its materials: fur, and optionally dark
    (the saddle along the back), pale (belly and throat), nose, eye, paw, inner (inside the
    ears and the tail's underside).
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

    # Head: skull, cheeks, the stop, the muzzle; the jaw under it.
    m = p.muzzle
    head = c.loft("head", [
        (along(-0.02, 0.15), S * 0.45, S * 0.45),
        (along(0.10, 0.30), S * 0.95, S * 0.90),
        (along(0.30, 0.20), S * 1.05, S * 0.88),
        (along(0.48, 0.02), S * 0.72 * m, S * 0.62 * m),
        (along(0.70, -0.10), S * 0.52 * m, S * 0.44 * m),
        (along(0.90, -0.12), S * 0.40 * m, S * 0.34 * m),
        (along(1.00, -0.10), S * 0.26 * m, S * 0.24 * m),
        (along(1.03, -0.10), S * 0.06, S * 0.06),
    ], fur, sides=18)
    parts.append((head, ["neck", "head"]))

    jaw = c.loft("jaw", [
        (along(0.26, -0.40), S * 0.55, S * 0.20),
        (along(0.50, -0.52), S * 0.46 * m, S * 0.20 * m),
        (along(0.78, -0.55), S * 0.34 * m, S * 0.16 * m),
        (along(0.94, -0.52), S * 0.20 * m, S * 0.10 * m),
        (along(0.97, -0.52), S * 0.04, S * 0.04),
    ], coat.get("pale", fur), sides=14)
    parts.append((jaw, ["jaw"]))

    nose_mat = coat.get("nose", fur)
    parts.append((c.blob("nose", (S * 0.24, S * 0.20, S * 0.18), nose_mat, along(1.02, 0.08)), ["head"]))
    mouth = coat.get("mouth")
    if mouth:
        parts.append((c.loft("mouth", [(along(0.35, -0.36), S * 0.45, S * 0.06), (along(0.9, -0.40), S * 0.25, S * 0.05)], mouth, sides=10), ["jaw"]))

    eye_mat = coat.get("eye", nose_mat)
    for side in (1, -1):
        eye = along(0.40, 0.42, 0.60 * side)
        parts.append((c.blob(f"eye{side}", (S * 0.16, S * 0.18, S * 0.16), eye_mat, eye), ["head"]))
        if "pupil" in coat:
            parts.append((c.blob(f"pupil{side}", (S * 0.07, S * 0.09, S * 0.11), coat["pupil"], eye + Vector((side * S * 0.12, -S * 0.08, 0))), ["head"]))

    # Ears, pointed or round, leaning back and out.
    for side in (1, -1):
        base = along(0.12, 0.72, 0.55 * side)
        if p.ear_round:
            tip = base + up * p.ear + Vector((0.3 * p.ear * side, 0.2 * p.ear, 0))
            ear = c.loft(f"ear{side}", [(base, p.ear_width * 0.6, p.ear_width * 0.25), (base.lerp(tip, 0.5), p.ear_width, p.ear_width * 0.22), (tip, p.ear_width * 0.5, p.ear_width * 0.15)], coat.get("inner", fur), sides=12)
        else:
            tip = base + up * p.ear + Vector((0.18 * p.ear * side, 0.22 * p.ear, 0))
            ear = c.loft(f"ear{side}", [(base, p.ear_width, p.ear_width * 0.40), (base.lerp(tip, 0.5), p.ear_width * 0.72, p.ear_width * 0.28), (tip, p.ear_width * 0.05, p.ear_width * 0.04)], fur, sides=10)
            if "inner" in coat:
                c.paint(ear, coat["inner"], lambda q, n, s=side: n.y < -0.5)
        parts.append((ear, ["head"]))

    # Legs, a paw at the foot of each.
    k = p.leg
    for side in (1, -1):
        f = [Vector((q.x * side, q.y, q.z)) for q in front]
        leg = c.loft(f"front{side}", [
            (Vector((f[0].x * 0.4, f[0].y, f[0].z + D * 0.02)), G * 0.30 * k, G * 0.36 * k),
            (f[0], G * 0.36 * k, G * 0.44 * k),
            (f[1], G * 0.30 * k, G * 0.34 * k),
            (f[1].lerp(f[2], 0.55), G * 0.22 * k, G * 0.24 * k),
            (f[2], G * 0.20 * k, G * 0.21 * k),
            (f[2].lerp(f[3], 0.5), G * 0.21 * k, G * 0.22 * k),
        ], fur, sides=14)
        name_side = "L" if side > 0 else "R"
        parts.append((leg, ["chest", f"front.{name_side}.upper", f"front.{name_side}.lower", f"front.{name_side}.paw"]))

        b = [Vector((q.x * side, q.y, q.z)) for q in back]
        leg = c.loft(f"back{side}", [
            (Vector((b[0].x * 0.4, b[0].y, b[0].z + D * 0.02)), G * 0.36 * k * hq, G * 0.44 * k * hq),
            (b[0], G * 0.44 * k * hq, G * 0.54 * k * hq),
            (b[0].lerp(b[1], 0.55), G * 0.40 * k * hq, G * 0.48 * k * hq),
            (b[1], G * 0.30 * k, G * 0.34 * k),
            (b[1].lerp(b[2], 0.5), G * 0.24 * k, G * 0.26 * k),
            (b[2], G * 0.20 * k, G * 0.23 * k),
            (b[2].lerp(b[3], 0.55), G * 0.20 * k, G * 0.21 * k),
        ], fur, sides=14)
        parts.append((leg, ["hips", f"back.{name_side}.upper", f"back.{name_side}.lower", f"back.{name_side}.paw"]))

        for label, foot in (("front", f[3]), ("back", b[3])):
            paw = c.blob(f"{label}paw{side}", (G * 0.25 * k, G * 0.34 * k, G * 0.16 * k), coat.get("paw", fur), foot + Vector((0, -G * 0.06, G * 0.14 * k)))
            parts.append((paw, [f"{label}.{name_side}.paw"]))

    # Tail: a brush, or a rodent's whip.
    tails = [f"tail{i + 1}" for i in range(len(tail_points) - 1)]
    if p.tail_thin:
        rings = [(q, p.tail_bush * (1 - 0.8 * i / 4), p.tail_bush * (1 - 0.8 * i / 4)) for i, q in enumerate(tail_points)]
        rings.append((tail_points[-1] + (tail_points[-1] - tail_points[-2]) * 0.1, p.tail_bush * 0.1, p.tail_bush * 0.1))
        tail = c.loft("tail", rings, coat.get("inner", fur), sides=10)
    else:
        widths = (0.45, 0.85, 1.0, 0.8, 0.35)
        rings = [(q, p.tail_bush * wd, p.tail_bush * wd) for q, wd in zip(tail_points, widths)]
        rings.append((tail_points[-1] + (tail_points[-1] - tail_points[-2]) * 0.25, p.tail_bush * 0.05, p.tail_bush * 0.05))
        tail = c.loft("tail", rings, fur, sides=14)
    parts.append((tail, ["hips", *tails]))

    # The coat: darker along the back, paler underneath.
    if "dark" in coat:
        for obj in (body, head, tail):
            c.paint(obj, coat["dark"], lambda q, n: n.z > 0.62 and q.z > z)
    if "pale" in coat:
        c.paint(body, coat["pale"], lambda q, n: n.z < -0.30 or (n.y < -0.55 and q.y < -L * 0.9 and n.z < 0.3))
        c.paint(head, coat["pale"], lambda q, n: n.z < -0.25)

    anchors = {"along": along, "S": S, "up": up, "ahead": ahead, "poll": poll, "withers": withers,
               "tail": tail_points, "body": body, "head": head, "tail_obj": tail, "legs_front": front, "legs_back": back}
    return Made(name, rig, arm, spec, parts, anchors)
