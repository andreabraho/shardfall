"""The suits and headpieces of the Archer, the Asura and the Dragonseer (CLS-04, 05, 06 — 2026-10-02).

"Their armour in the style of theirs" (your call; the analysis is docs/09-archer-asura-dragonseer.md):
light leather and cloth, feathers and fur for the Archer, never plate; black plate with flared
shoulders, blades, ribs and violet light for the Asura ("much more arcane and darker", your call);
mantles, a stole, a wide sash, beads and hanging seals for the Dragonseer, never metal but a clasp.

Redrawn the same day at your call ("more their own, and better made"): each suit is its own
function now and has a shape of its own — a straw cape, a rib cage, a feathered collar — where
all seven of a class were one function with other colours before. What they share is in the
first half of this file: curved patches that follow the body, pointed scales in rows, capes with
a cut hem, tubes through points (horns, ribs, ribbons, cords), blades cut from an outline.

All worn over the class's own clothes, as the Ninja's are (visuals.json "own_clothes"), each
piece riding one bone. The Archer carries a quiver across the back, so nothing of the Archer's
hangs there above the belt. `build(name)` makes the one called `name`.

Angles are in degrees round the body: 0 the front (-Y), 90 the left (+X), 180 the back.
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import armourkit as a
import ninjakit as n
import temins as t

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BODIES = os.path.join(ROOT, "game", "assets", "temins", "characters")
WEARER = {
    "archer": os.path.join(BODIES, "archer_woman_body.glb"),
    "asura": os.path.join(BODIES, "asura_woman_body.glb"),
    "seer": os.path.join(BODIES, "seer_woman_body.glb"),
    "warrior": os.path.join(BODIES, "woman_body.glb"),
    "ninja": os.path.join(BODIES, "ninja_woman_body.glb"),
}

RX, RY = n.RX, n.RY
Y0 = 0.02          # how far back the middle of the chest is
ARM_Z = 1.11       # the height of the arm in the rest pose


def m(name, colour=None, metal=0.0, rough=0.7, glow=0.0):
    return t.material(name, colour, metal=metal, rough=rough, glow=glow)


# ------------------------------------------------------------------ the pieces

def at(rx, ry, angle, z, y=Y0, k=1.0):
    """The point at `angle` on the body's outline, at height z."""
    r = math.radians(angle)
    return (rx * k * math.sin(r), y - ry * k * math.cos(r), z)


def mirror(obj):
    """The same piece on the other side of the body — turned pieces too, which armourkit's cannot."""
    copy = obj.copy()
    copy.data = obj.data.copy()
    bpy.context.collection.objects.link(copy)
    copy.data.transform(Matrix.Scale(-1, 4, (1, 0, 0)) @ obj.matrix_basis)
    copy.matrix_basis = Matrix.Identity(4)
    copy.data.flip_normals()
    return copy


def solid(obj, thick):
    """Thickness outwards, so a patch never sinks into what it lies on."""
    mod = obj.modifiers.new("Solid", "SOLIDIFY")
    mod.thickness = thick
    mod.offset = 1
    return obj


def patch(name, rx, ry, z0, z1, mat, a0=-180, a1=180, top=1.0, y=Y0, cols=None, rows=3, thick=0.028, hem=None, span=(0.0, 1.0), bulge=0.0):
    """
    A curved sheet following the body from angle a0 to a1 and from z1 down to z0: a plate, a flap,
    a mantle. `top` scales its upper edge (below 1 it flares downwards, above 1 it opens upwards);
    `hem(s)` lengthens the lower edge, s from -1 to 1 across it (from 0 to 1 round a whole ring);
    `span` keeps only part of its height, for an edging in another colour; `bulge` swells its middle.
    """
    closed = a1 - a0 >= 360
    cols = cols or max(2, int(abs(a1 - a0) / 9))
    count = cols if closed else cols + 1
    bm = bmesh.new()
    grid = []

    for r in range(rows + 1):
        v = span[0] + (span[1] - span[0]) * r / rows
        row = []
        for c in range(count):
            u = c / cols
            drop = hem(u if closed else u * 2 - 1) if hem is not None else 0.0
            z = z1 + (z0 - drop - z1) * v
            k = top + (1 - top) * v + bulge * math.sin(math.pi * v)
            row.append(bm.verts.new(at(rx, ry, a0 + (a1 - a0) * u, z, y, k)))
        grid.append(row)

    for r in range(rows):
        for c in range(cols):
            d = (c + 1) % count
            bm.faces.new((grid[r][c], grid[r][d], grid[r + 1][d], grid[r + 1][c]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return solid(t.mesh_object(name, bm, mat), thick)


def edged(name, rx, ry, z0, z1, mat, trim, edge=0.14, **kw):
    """A patch and its lower edge in another colour."""
    thick, rows = kw.pop("thick", 0.028), kw.pop("rows", 3)
    return [patch(name, rx, ry, z0, z1, mat, thick=thick, rows=rows, **kw),
            patch(name + "_edge", rx + 0.004, ry + 0.004, z0, z1, trim, thick=thick + 0.006, span=(1 - edge, 1.0), rows=1, **kw)]


def pointed(depth):
    return lambda s: depth * (1 - abs(s))


def swallow(depth):
    return lambda s: depth * abs(s)


def zigzag(count, depth):
    return lambda s: depth * abs(((s * count) % 1.0) * 2 - 1)


def ragged(seed, depth):
    def hem(s):
        k = math.sin(s * 37.0 + seed * 1.7) * 0.5 + math.sin(s * 91.0 + seed) * 0.5
        return depth * (0.5 + 0.5 * k)
    return hem


def leaves(name, rx, ry, z, mat, count, height=0.12, a0=-180, a1=180, y=Y0, lean=0.06, point=0.5, offset=0.0, overlap=1.25, thick=0.016, frame=None):
    """
    A row of pointed scales — or leaves, or feathers — hanging round the body at height z, each
    standing out a little at its tip. `frame` moves the whole row elsewhere (round an arm).
    """
    bm = bmesh.new()
    closed = a1 - a0 >= 360
    step = (a1 - a0) / count
    half = step * overlap / 2

    for i in range(count):
        mid = a0 + step * (i + 0.5 + offset) if closed else a0 + step * (i + 0.5)
        top, low, tip = z + height / 2, z - height / 2 + height * point, z - height / 2
        k0, k1 = 1.0, 1.0 + lean
        km = 1.0 + lean * (1 - point)
        v = [bm.verts.new(p) for p in (
            at(rx, ry, mid - half, top, y, k0), at(rx, ry, mid, top, y, k0), at(rx, ry, mid + half, top, y, k0),
            at(rx, ry, mid - half, low, y, km), at(rx, ry, mid, tip, y, k1), at(rx, ry, mid + half, low, y, km))]
        bm.faces.new((v[0], v[1], v[4], v[3]))
        bm.faces.new((v[1], v[2], v[5], v[4]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    if frame is not None:
        bmesh.ops.transform(bm, matrix=frame, verts=bm.verts)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    return solid(t.mesh_object(name, bm, mat), thick)


def arm_frame(x):
    """Turns a row round the body into a row round the left arm at `x`, tips towards the hand, 90 the top."""
    return Matrix(((0, 0, -1, x), (0, 1, 0, 0), (1, 0, 0, ARM_Z), (0, 0, 0, 1)))


def arm_leaves(name, mat, rows, x0=0.30, step=0.085, r=0.20, count=7, height=0.13, shrink=0.012, alt=None, **kw):
    """Rows of leaves down the left upper arm from the shoulder, over its top and sides."""
    parts = []
    for i in range(rows):
        radius = r - shrink * i
        parts.append(leaves(f"{name}_{i}", radius, radius, 0.0, alt if alt is not None and i % 2 else mat, count, height=height,
                            a0=-40, a1=220, y=0.0, frame=arm_frame(x0 + step * i), **kw))
    return parts


def curve(points, steps=5):
    """A smooth line through the points (Catmull-Rom)."""
    p = [Vector(q) for q in points]
    p = [p[0] * 2 - p[1]] + p + [p[-1] * 2 - p[-2]]
    out = []
    for i in range(1, len(p) - 2):
        for s in range(steps):
            u = s / steps
            out.append(0.5 * ((2 * p[i]) + (-p[i - 1] + p[i + 1]) * u + (2 * p[i - 1] - 5 * p[i] + 4 * p[i + 1] - p[i + 2]) * u * u
                              + (-p[i - 1] + 3 * p[i] - 3 * p[i + 1] + p[i + 2]) * u ** 3))
    out.append(p[-2])
    return out


def tube(name, points, radii, mat, sides=8, flat=1.0, closed=False, ref=None):
    """
    A tube through the points, `radii` one number or (start, end): a horn, a rib, a cord. `flat`
    below 1 presses it into a ribbon; `closed` joins its ends into a hoop.
    """
    pts = [Vector(p) for p in points]
    count = len(pts)
    if isinstance(radii, (int, float)):
        radii = (radii, radii)
    bm = bmesh.new()
    rings = []

    for i, p in enumerate(pts):
        if closed:
            tan = pts[(i + 1) % count] - pts[i - 1]
        else:
            tan = pts[min(i + 1, count - 1)] - pts[max(i - 1, 0)]
        tan.normalize()
        side = Vector(ref) if ref is not None else (Vector((0, 0, 1)) if abs(tan.z) < 0.9 else Vector((0, 1, 0)))
        n1 = tan.cross(side).normalized()
        n2 = n1.cross(tan).normalized()
        r = radii[0] + (radii[1] - radii[0]) * (i / max(1, count - 1))
        r = max(r, 0.003)
        rings.append([bm.verts.new(p + r * (math.cos(2 * math.pi * j / sides) * n1 + flat * math.sin(2 * math.pi * j / sides) * n2))
                      for j in range(sides)])

    pairs = list(zip(rings, rings[1:])) + ([(rings[-1], rings[0])] if closed else [])
    for lower, upper in pairs:
        for j in range(sides):
            k = (j + 1) % sides
            bm.faces.new((lower[j], lower[k], upper[k], upper[j]))

    if not closed:
        bm.faces.new(rings[0])
        bm.faces.new(rings[-1])

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return t.mesh_object(name, bm, mat)


def hoop(name, centre, radius, minor, mat, right=(1, 0, 0), up=(0, 0, 1), segments=28, sides=8, flat=1.0):
    """A ring standing in the plane of `right` and `up`."""
    c, r, u = Vector(centre), Vector(right).normalized(), Vector(up).normalized()
    pts = [c + radius * (math.cos(2 * math.pi * i / segments) * r + math.sin(2 * math.pi * i / segments) * u) for i in range(segments)]
    return tube(name, pts, minor, mat, sides=sides, closed=True, flat=flat, ref=tuple(r.cross(u)))


def fin(name, outline, thick, mat, origin, right=(1, 0, 0), up=(0, 0, 1), edge=0.0, bevel=0.005):
    """A blade cut from an outline (across, along), stood at `origin` with `right` across and `up` along."""
    obj = t.profile_solid(name, outline, thick, mat, bevel=bevel, edge=edge)
    r, u = Vector(right).normalized(), Vector(up).normalized()
    nrm = r.cross(u).normalized()
    u = nrm.cross(r).normalized()
    frame = Matrix((r, u, nrm)).transposed().to_4x4()
    frame.translation = Vector(origin)
    obj.data.transform(frame)
    return obj


def feather(name, base, tip, width, mat, spine=None, face=(0, -1, 0)):
    """A feather from `base` to `tip`, its flat turned to `face`; `spine` a quill down its middle."""
    b, e = Vector(base), Vector(tip)
    along = e - b
    length = along.length
    right = along.cross(Vector(face)).normalized()
    w = width / 2
    outline = [(0, 0), (w * 0.55, length * 0.12), (w, length * 0.45), (w * 0.8, length * 0.78), (0, length),
               (-w * 0.8, length * 0.78), (-w, length * 0.45), (-w * 0.55, length * 0.12)]
    parts = [fin(name, outline, 0.018, mat, b, right=right, up=along, bevel=0.004)]
    if spine is not None:
        parts.append(tube(name + "_quill", [b, b + along * 0.92], (0.012, 0.004), spine, sides=5))
    return parts


def blade(name, origin, length, width, mat, right=(1, 0, 0), up=(0, 0, 1), hook=0.35, thick=0.035):
    """A swept blade standing on a shoulder or a back: wide at the root, hooked at the tip."""
    w = width
    outline = [(-w * 0.5, 0), (w * 0.5, 0), (w * 0.62, length * 0.30), (w * (0.45 + hook), length), (w * 0.05, length * 0.55), (-w * 0.35, length * 0.22)]
    return fin(name, outline, thick, mat, origin, right=right, up=up, edge=min(w * 0.18, 0.02))


def beads(name, points, radius, mat, big=None, alt=None):
    """A ball at each point: a string of beads. `big` (index, radius, material) makes one larger;
    `alt` (every, material) gives every so-manieth another material."""
    parts = []
    for i, p in enumerate(points):
        if big is not None and i == big[0]:
            parts.append(t.sphere(f"{name}_big", big[1], big[2], at=tuple(p), subdivisions=2))
        else:
            parts.append(t.sphere(f"{name}_{i}", radius, alt[1] if alt is not None and i % alt[0] == 0 else mat, at=tuple(p), subdivisions=1))
    return parts


def necklace(name, mat, rx=0.36, ry=0.40, z_back=1.20, z_front=1.02, count=15, radius=0.026, spread=115, big=None, alt=None, y=Y0):
    """Beads hung round the neck, low at the front; only what shows before the hair."""
    pts = []
    for i in range(count):
        ang = -spread + 2 * spread * i / (count - 1)
        z = z_back + (z_front - z_back) * (0.5 + 0.5 * math.cos(math.radians(ang * 180 / spread)))
        pts.append(at(rx, ry, ang, z, y))
    return beads(name, pts, radius, mat, big=big, alt=alt)


def cloak(name, mat, top=(1.19, 0.385, 0.30), bottom=(0.45, 0.58, 0.42), curl=0.10, hem=None, cols=16, rows=8, thick=0.024, span=(0.0, 1.0)):
    """A cape down the back as armourkit's — (z, y, half width) at its top and bottom — with a cut hem."""
    bm = bmesh.new()
    grid = []
    for r in range(rows + 1):
        v = span[0] + (span[1] - span[0]) * r / rows
        row = []
        for c in range(cols + 1):
            u = c / cols * 2 - 1
            drop = hem(u) if hem is not None else 0.0
            z = top[0] + (bottom[0] - drop - top[0]) * v
            yy = top[1] + (bottom[1] - top[1]) * v
            half = top[2] + (bottom[2] - top[2]) * v
            row.append(bm.verts.new((u * half, yy - curl * u * u, z)))
        grid.append(row)
    for r in range(rows):
        for c in range(cols):
            bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = t.mesh_object(name, bm, mat)
    obj.modifiers.new("Solid", "SOLIDIFY").thickness = thick
    return obj


def seal(name, where, ink, paper, size=(0.085, 0.17), angle=0.0):
    """A paper seal hanging flat at `where`, facing out at `angle`, a mark of ink on it."""
    parts = [t.box(name, (size[0], 0.012, size[1]), paper, bevel=0.003),
             t.box(name + "_mark", (size[0] * 0.36, 0.018, size[1] * 0.55), ink),
             t.box(name + "_bar", (size[0] * 0.7, 0.018, size[1] * 0.07), ink, at=(0, 0, size[1] * 0.36))]
    frame = Matrix.Translation(where) @ Matrix.Rotation(math.radians(angle), 4, "Z")
    for p in parts:
        p.data.transform(frame)
    return parts


def breast(angle, z, out=0.012, grow=0.0):
    """The point at `angle` and height z on the surface of a vest, which narrows to the shoulders."""
    k = 1.0 if z < 0.985 else 1.0 - 0.10 * (z - 0.985) / 0.225
    return at(RX + grow + out, RY + grow + out, angle, z, Y0, k)


def stud_row(name, rx, ry, z, mat, count, a0=-180, a1=180, radius=0.02, y=Y0):
    return [t.sphere(f"{name}_{i}", radius, mat, at=at(rx, ry, a0 + (a1 - a0) * (i + 0.5) / count, z, y), subdivisions=1) for i in range(count)]


class Suit:
    """What a suit is made of, bone by bone."""

    def __init__(self, name, wearer, head=False):
        self.name, self.wearer, self.head, self.bones = name, wearer, head, {}

    def add(self, bone, parts, only=None):
        """`only` "m" or "w": a piece of the full cut alone, or of the women's (visuals.json "cut")."""
        key = bone if only is None else f"{bone}__{only}"
        self.bones.setdefault(key, []).extend(parts if isinstance(parts, (list, tuple)) else [parts])

    def both(self, bone, parts, only=None):
        """Built for the left, worn on both."""
        self.add(bone + ".l", parts, only)
        self.add(bone + ".r", [mirror(p) for p in parts], only)

    def right(self, bone, parts):
        """Built for the left, worn on the right only."""
        self.add(bone + ".r", [mirror(p) for p in parts])
        for p in parts:
            bpy.data.objects.remove(p, do_unlink=True)

    def done(self):
        focus = ((0, 0, 1.75), 0.8) if self.head else ((0, 0, 0.75), 0.95)
        return a.finish(self.name, self.bones, underlay=False, reference=WEARER[self.wearer], replaces=[], focus=focus)


# The women's cut (2026-10-02, at your call: "the women a little more uncovered"): where the men wear
# a vest from the belly to the shoulders, the women wear a band round the chest between these
# heights, the shoulders and the belly bare. What lay on the vest above or below is cut again for it.
BUST = (0.86, 1.065)


def bust(s, mat, trim=None, grow=0.0):
    """The band round the chest of the women's cut, edged in `trim` above and below."""
    rx, ry = RX + grow, RY + grow
    s.add("spine", a.band("bust_low", rx, ry, BUST[0], 0.99, mat, y=Y0), "w")
    s.add("chest", a.band("bust_top", rx, ry, 0.985, BUST[1], mat, top=0.965, y=Y0), "w")
    if trim is not None:
        s.add("spine", a.band("bust_hem", rx + 0.007, ry + 0.007, BUST[0] - 0.005, BUST[0] + 0.028, trim, y=Y0, bevel=0.006), "w")
        s.add("chest", a.band("bust_rim", (rx + 0.007) * 0.965, (ry + 0.007) * 0.965, BUST[1] - 0.026, BUST[1] + 0.004, trim, y=Y0, bevel=0.006), "w")


def vest(s, mat, hem=None, grow=0.0):
    low, high = n.vest(mat, hem=hem, grow=grow)
    s.add("spine", low, "m")
    s.add("chest", high, "m")
    bust(s, mat, hem, grow)


# ------------------------------------------------------------------ the Archer

def quiver_strap(s, leather, buckle=None):
    """The quiver's strap, from the right hip to the left shoulder, front and back."""
    for y, tag in ((-RY - 0.014, "front"), (RY + 0.045, "back")):
        s.add("chest", n.strip(f"strap_{tag}", (-0.27, y, 0.86), (0.22, y * 0.93, 1.19), 0.075, leather, thick=0.03))
    if buckle is not None:
        s.add("chest", t.cylinder("strap_buckle", 0.05, 0.03, buckle, at=(-0.02, -RY - 0.03, 1.03), sides=12, axis="Y", bevel=0.006))


def lacing(s, cord, slit):
    """A laced opening down the front of a jerkin."""
    front = Y0 - RY - 0.008
    s.add("chest", n.strip("slit", (0, front, 1.00), (0, front + 0.012, 1.19), 0.05, slit, thick=0.012), "m")
    for i in range(3):
        z = 1.02 + 0.055 * i
        for side in (-1, 1):
            s.add("chest", a.rod(f"lace_{i}_{side}", (side * 0.045, front - 0.008, z), (-side * 0.045, front - 0.008, z + 0.045), 0.009, cord, sides=5), "m")
    # The women's: one lace across the band.
    for side in (-1, 1):
        s.add("chest", a.rod(f"lace_w_{side}", (side * 0.045, front - 0.008, 1.0), (-side * 0.045, front - 0.008, 1.045), 0.009, cord, sides=5), "w")


def plastron(mat, rim, side=-1, z0=0.99, z1=1.19, depth=0.06):
    """The guard an archer wears over the breast the string passes: on the right, with `side` -1."""
    a0, a1 = (-78, -6) if side < 0 else (6, 78)
    return [patch("plastron_rim", RX + 0.006, RY + 0.006, z0 - 0.014, z1 + 0.012, rim, a0=a0 - 4, a1=a1 + 4, hem=pointed(depth), top=0.93, thick=0.014, rows=2),
            patch("plastron", RX + 0.016, RY + 0.016, z0, z1, mat, a0=a0, a1=a1, hem=pointed(depth), top=0.93, thick=0.018, rows=2)]


def hip_cape(mat, trim=None, z1=0.63, z0=0.22, a0=100, a1=260, hem=None, flare=0.86, rx=0.455, ry=0.405, name="hip_cape"):
    """A cape hanging from the belt at the back, below the quiver."""
    if trim is None:
        return [patch(name, rx, ry, z0, z1, mat, a0=a0, a1=a1, top=flare, hem=hem, y=0.0, rows=5, thick=0.022)]
    return edged(name, rx, ry, z0, z1, mat, trim, a0=a0, a1=a1, top=flare, hem=hem, y=0.0, rows=5, thick=0.022, edge=0.12)


def fur_ring(name, rx, ry, z0, z1, mat, count=14, depth=0.06, flare=0.9, y=Y0, **kw):
    """A band of fur: a ring cut into tufts along its lower edge."""
    return patch(name, rx, ry, z0, z1, mat, top=flare, hem=zigzag(count, depth), cols=count * 2, rows=1, thick=0.04, y=y, **kw)


def arm_fur(name, x, r, mat, width=0.07, count=8):
    """A cuff of fur round the left arm at `x`."""
    return leaves(name, r, r, 0.0, mat, count, height=width, a0=-180, a1=180, y=0.0, lean=0.10, point=0.6, thick=0.03, frame=arm_frame(x))


def trail_jerkin(name):
    """Giubba del Sentiero: a laced linen jerkin, a green scarf, the strap and a pouch. What one sets out in."""
    s = Suit(name, "archer")
    linen, hide, leather = m("trail_linen", (0.56, 0.46, 0.28)), m("leather_dark"), m("leather")
    green = m("trail_green", (0.14, 0.30, 0.13))
    brass = m("trail_brass", (0.62, 0.46, 0.18), metal=0.6, rough=0.45)

    vest(s, linen, hem=leather)
    lacing(s, hide, hide)
    quiver_strap(s, hide, buckle=brass)
    s.add("chest", n.collar(green))
    s.add("chest", n.strip("scarf_end", (0.20, -0.27, 1.17), (0.25, -0.33, 0.98), 0.09, green))

    s.add("hips", n.sash(leather, knot=False))
    s.add("hips", [t.box("pouch", (0.13, 0.07, 0.13), leather, at=(-0.30, -0.30, 0.56), bevel=0.02, segments=2),
                   t.box("pouch_flap", (0.14, 0.075, 0.05), hide, at=(-0.30, -0.305, 0.61), bevel=0.015),
                   t.sphere("pouch_stud", 0.016, brass, at=(-0.30, -0.345, 0.59), subdivisions=1)])

    s.add("lowerarm.l", n.bracer(leather, hide))
    s.right("lowerarm", n.wraps(linen))
    s.both("lowerleg", n.shin(linen, turns=4))
    return s.done()


def hunters_hide(name):
    """Pelle del Cacciatore: hide and fur — a fur collar, bone toggles, fur at the wrists and shins, a tail at the belt."""
    s = Suit(name, "archer")
    hide, dark = m("hunter_hide", (0.36, 0.22, 0.12)), m("leather_dark")
    fur = m("hunter_fur", (0.66, 0.58, 0.44), rough=0.95)
    fur_dark = m("hunter_fur_dark", (0.30, 0.22, 0.15), rough=0.95)
    bone = m("bone")

    vest(s, hide, hem=dark)
    quiver_strap(s, dark)
    s.add("chest", [fur_ring("fur_collar", 0.36, 0.33, 1.10, 1.24, fur, count=12, depth=0.07, flare=0.72),
                    fur_ring("fur_collar_low", 0.40, 0.36, 1.06, 1.15, fur_dark, count=12, depth=0.06, flare=0.85)])

    # Bone toggles down the front, and teeth on a cord.
    front = Y0 - RY - 0.012
    for i in range(3):
        s.add("spine" if i == 0 else "chest", tube(f"toggle_{i}", [(-0.07, front, 0.93 + 0.07 * i), (0.07, front, 0.945 + 0.07 * i)], (0.016, 0.012), bone, sides=6))

    s.add("hips", n.sash(dark, knot=False))
    s.add("hips", fur_ring("fur_belt", 0.43, 0.37, 0.50, 0.61, fur, count=14, depth=0.07, flare=0.95, y=0.0))
    tail = curve([(0.36, -0.20, 0.58), (0.43, -0.22, 0.44), (0.44, -0.20, 0.28), (0.40, -0.18, 0.16)])
    s.add("hips", [tube("tail", tail, (0.05, 0.07), fur_dark, sides=8), t.sphere("tail_tip", 0.065, fur, at=(0.40, -0.18, 0.13), subdivisions=2, squash=(1, 1, 1.3))])

    s.add("upperarm.l", n.pad(hide, rim=dark, size=0.165) + [arm_fur("pad_fur", 0.50, 0.185, fur, width=0.09, count=9)])
    s.add("lowerarm.l", n.bracer(hide, dark) + [arm_fur("bracer_fur", 0.53, 0.168, fur)])
    s.right("lowerarm", n.wraps(hide) + [arm_fur("wrap_fur", 0.53, 0.165, fur)])
    shin_fur = fur_ring("shin_fur", 0.15, 0.15, 0.27, 0.34, fur, count=7, depth=0.05, flare=0.95, y=0.005)
    shin_fur.data.transform(Matrix.Translation((n.LEG, 0, 0)))
    s.both("lowerleg", n.shin(hide, trim=dark) + [shin_fur])
    return s.done()


def fletchers_scales(name):
    """Scaglie del Frecciaio: feathers for scales — rows of green and bronze leaves on the chest, down the bow arm, round the hips."""
    s = Suit(name, "archer")
    green = m("fletcher_green", (0.06, 0.20, 0.08))
    leaf = m("fletcher_leaf", (0.09, 0.30, 0.12), metal=0.25, rough=0.5)
    bronze = m("fletcher_bronze", (0.66, 0.42, 0.16), metal=0.65, rough=0.4)
    hide = m("leather_dark")

    vest(s, green, hem=bronze)
    for row in range(3):
        k = 1 - 0.03 * row
        s.add("chest", leaves(f"chest_leaf_{row}", (RX + 0.012) * k, (RY + 0.012) * k, 1.03 + 0.065 * row, bronze if row == 1 else leaf, 14, height=0.13, offset=row * 0.5, lean=0.05), "m")
    for row in range(2):
        s.add("chest" if row else "spine", leaves(f"bust_leaf_{row}", RX + 0.012, RY + 0.012, 0.935 + 0.065 * row, bronze if row else leaf, 14, height=0.13, offset=row * 0.5, lean=0.05), "w")
    quiver_strap(s, hide, buckle=bronze)
    s.add("chest", n.collar(green))

    s.add("hips", n.sash(hide, knot=False))
    for row in range(2):
        s.add("hips", leaves(f"skirt_{row}", 0.44 - 0.012 * row, 0.385 - 0.012 * row, 0.50 + 0.10 * row, leaf if row else bronze, 12, height=0.19, y=0.0, offset=row * 0.5, lean=0.10, point=0.6, thick=0.02))

    s.add("upperarm.l", arm_leaves("spaulder", leaf, 3, alt=bronze, r=0.205))
    s.add("lowerarm.l", n.bracer(hide, bronze, plate=bronze))
    s.right("lowerarm", n.wraps(green))
    s.both("lowerleg", n.shin(hide, trim=bronze, plate=bronze))
    return s.done()


def ridge_ranger(name):
    """Guardiacresta: the mountain ranger's — a deep cowl, a harness of belts and pouches, iron on the bow arm, a grey cape from the belt."""
    s = Suit(name, "archer")
    grey = m("ranger_grey", (0.15, 0.19, 0.16))
    cowl = m("ranger_cowl", (0.07, 0.11, 0.08))
    hide, leather = m("leather_dark"), m("leather")
    iron = m("ranger_iron", (0.34, 0.36, 0.40), metal=0.65, rough=0.42)

    vest(s, grey, hem=hide)
    quiver_strap(s, hide, buckle=iron)
    # The cowl: a thick roll at the neck, its edge falling on the chest in a point, the hood lying on the left shoulder.
    s.add("chest", [a.roll("cowl", 0.33, 0.31, 1.10, 1.26, cowl, y=0.0),
                    patch("cowl_fall", RX + 0.02, RY + 0.02, 1.07, 1.20, cowl, a0=-70, a1=70, top=0.80, hem=pointed(0.11), rows=2, thick=0.03),
                    t.sphere("hood", 0.15, cowl, at=(0.24, 0.30, 1.19), subdivisions=2, squash=(1.0, 0.8, 0.7))])
    # A second belt across the belly, pouches on it.
    s.add("spine", [a.band("belly_belt", RX + 0.01, RY + 0.01, 0.88, 0.94, leather, y=Y0, bevel=0.008),
                    t.box("belly_buckle", (0.09, 0.03, 0.08), iron, at=(0.0, Y0 - RY - 0.015, 0.91), bevel=0.01)])
    for x in (-0.22, 0.22):
        s.add("spine", [t.box("belly_pouch", (0.11, 0.06, 0.11), hide, at=(x, Y0 - RY * 0.86 - 0.03, 0.91), bevel=0.02, segments=2)])

    s.add("hips", n.sash(hide, knot=False))
    s.add("hips", hip_cape(grey, trim=cowl, z0=0.20, hem=swallow(0.10)))
    for side in (-1, 1):
        s.add("hips", patch(f"hip_flap_{side}", 0.44, 0.38, 0.40, 0.62, hide, a0=side * 90 - 32, a1=side * 90 + 32, hem=pointed(0.07), top=0.94, y=0.0, rows=2))

    # Iron on the bow arm: two lames on the shoulder, splints on the forearm.
    s.add("upperarm.l", n.pad(iron, rim=hide, size=0.175) + [
        t.sphere("pad_low", 0.15, iron, at=(0.50, 0.0, 1.15), subdivisions=2, squash=(1.0, 0.95, 0.55)),
        t.sphere("pad_low_rim", 0.155, hide, at=(0.50, 0.0, 1.13), subdivisions=2, squash=(1.0, 0.95, 0.33))])
    s.add("lowerarm.l", n.bracer(hide, iron) + [
        t.box(f"splint_{i}", (0.20, 0.035, 0.03), iron, at=(0.63, -0.06 + 0.06 * i, ARM_Z + 0.150), bevel=0.01) for i in range(3)])
    s.right("lowerarm", n.wraps(grey))
    s.both("lowerleg", n.shin(hide, trim=iron, plate=iron) + [
        t.sphere("knee", 0.075, iron, at=(n.LEG, -0.125, 0.34), subdivisions=2, squash=(1, 0.6, 1))])
    return s.done()


def wardens_mantle(name):
    """Manto del Custode: the wardens' archers' — deep blue, a mantle over the bow shoulder clasped in silver, a silver guard on the breast, a swallow-tailed cape from the belt."""
    s = Suit(name, "archer")
    blue = m("warden_archer_blue", (0.08, 0.15, 0.36))
    night = m("warden_archer_night", (0.05, 0.08, 0.20))
    silver = m("warden_archer_silver", (0.80, 0.83, 0.90), metal=0.8, rough=0.28)
    hide = m("leather_dark")
    ice = m("warden_ice", (0.55, 0.85, 1.0), glow=1.0, rough=0.2)

    vest(s, blue, hem=silver)
    quiver_strap(s, hide, buckle=silver)
    s.add("chest", plastron(silver, night), "m")
    s.add("chest", plastron(silver, night, z0=0.93, z1=1.055, depth=0.05), "w")
    s.add("chest", n.collar(night))
    # The wardens' star at the throat.
    for cut, throat in (("m", (0.0, Y0 - RY * 0.80 - 0.02, 1.215)), ("w", (0.0, -0.275, 1.19))):
        for i in range(4):
            r = math.radians(90 * i)
            s.add("chest", a.spike(f"star_{i}", throat, (throat[0] + 0.07 * math.sin(r), throat[1] - 0.004, throat[2] + 0.07 * math.cos(r)), 0.026, silver, sides=4), cut)
        s.add("chest", t.sphere("star_stone", 0.03, ice, at=(throat[0], throat[1] - 0.02, throat[2]), subdivisions=1), cut)

    s.add("hips", n.sash(night, knot=False))
    s.add("hips", [a.band("belt_silver", 0.412, 0.352, 0.635, 0.67, silver, y=0.0, bevel=0.006)])
    s.add("hips", hip_cape(blue, trim=silver, z0=0.16, hem=swallow(0.14), flare=0.82))
    for side in (-1, 1):
        s.add("hips", edged(f"hip_flap_{side}", 0.44, 0.38, 0.40, 0.62, blue, silver, a0=side * 62 - 30, a1=side * 62 + 30, hem=pointed(0.08), top=0.94, y=0.0, rows=2, edge=0.2))

    # The mantle: cloth over the bow shoulder to the elbow, cut to a point, a silver hem, a clasp.
    mantle = []
    for i, (x, r, mat) in enumerate(((0.26, 0.215, blue), (0.345, 0.205, night), (0.43, 0.195, blue))):
        mantle.append(leaves(f"mantle_{i}", r, r, 0.0, mat, 5, height=0.17, a0=-45, a1=225, y=0.0, lean=0.05, point=0.35, overlap=1.3, thick=0.022, frame=arm_frame(x)))
    mantle.append(leaves("mantle_hem", 0.19, 0.19, 0.0, silver, 5, height=0.06, a0=-45, a1=225, y=0.0, lean=0.05, point=0.6, overlap=1.3, thick=0.026, frame=arm_frame(0.50)))
    mantle.append(t.sphere("mantle_clasp", 0.05, silver, at=(0.27, -0.17, 1.22), subdivisions=2))
    s.add("upperarm.l", mantle)
    s.right("upperarm", n.pad(night, rim=silver, size=0.15))

    s.add("lowerarm.l", n.bracer(hide, silver, plate=silver))
    s.right("lowerarm", n.bracer(night, silver))
    s.both("lowerleg", n.shin(hide, trim=silver, plate=silver))
    return s.done()


def ashen_stalker(name):
    """Predatore di Cenere: burnt black and torn — a mask of cloth to the chin, tatters from the shoulder and the belt, antler on the bow shoulder, embers in the seams."""
    s = Suit(name, "archer")
    ash = m("ash_archer_cloth", (0.07, 0.07, 0.08))
    soot = m("ash_archer_soot", (0.15, 0.14, 0.15))
    iron = m("ash_archer_iron", (0.20, 0.20, 0.23), metal=0.65, rough=0.4)
    bone = m("ash_bone", (0.62, 0.58, 0.50))
    ember = m("class_ember", (1.0, 0.42, 0.08), glow=1.3)
    hide = m("leather_dark")

    vest(s, ash, hem=soot)
    for row in range(2):
        k = 1 - 0.03 * row
        s.add("chest", leaves(f"chest_scale_{row}", (RX + 0.012) * k, (RY + 0.012) * k, 1.06 + 0.07 * row, soot if row else iron, 12, height=0.13, offset=row * 0.5, lean=0.05, a0=-100, a1=100), "m")
        s.add("chest" if row else "spine", leaves(f"bust_scale_{row}", RX + 0.012, RY + 0.012, 0.935 + 0.065 * row, soot if row else iron, 12, height=0.13, offset=row * 0.5, lean=0.05, a0=-100, a1=100), "w")
    quiver_strap(s, hide)
    s.add("chest", [a.roll("mask", 0.34, 0.33, 1.10, 1.30, ash, y=-0.02),
                    t.sphere("clasp", 0.045, ember, at=(-0.02, -RY - 0.035, 1.03), subdivisions=1, squash=(1, 0.6, 1.3))])
    # Embers in the seams: a line of light under the vest's hem.
    s.add("spine", a.band("ember_seam", RX + 0.004, RY + 0.004, 0.845, 0.858, ember, y=Y0, bevel=0))

    s.add("hips", n.sash(soot, knot=False))
    s.add("hips", hip_cape(ash, z0=0.20, hem=ragged(3, 0.20), flare=0.84))
    s.add("hips", hip_cape(soot, z0=0.34, hem=ragged(11, 0.14), flare=0.88, rx=0.47, ry=0.42, a0=70, a1=290, name="hip_cape_over"))

    # Tatters over the bow shoulder, and a fork of antler standing on it.
    rags = []
    for i, (x, r, mat) in enumerate(((0.28, 0.21, ash), (0.38, 0.20, soot))):
        rags.append(patch(f"rag_{i}", r, r, -0.20, 0.0, mat, a0=-50, a1=230, hem=ragged(5 + i * 4, 0.10), y=0.0, rows=2, thick=0.02))
        rags[-1].data.transform(arm_frame(x))
    prong = curve([(0.36, 0.02, 1.27), (0.40, 0.04, 1.40), (0.37, 0.07, 1.52), (0.31, 0.08, 1.60)])
    rags += [tube("antler", prong, (0.04, 0.008), bone, sides=6),
             tube("antler_tine", curve([(0.40, 0.04, 1.40), (0.46, 0.00, 1.46), (0.49, -0.03, 1.55)]), (0.028, 0.006), bone, sides=5),
             tube("antler_tine_b", curve([(0.38, 0.06, 1.49), (0.43, 0.10, 1.54), (0.45, 0.12, 1.62)]), (0.022, 0.006), bone, sides=5)]
    s.add("upperarm.l", n.pad(iron, rim=soot, size=0.16) + rags)
    s.right("upperarm", n.pad(iron, rim=soot, size=0.15))

    s.add("lowerarm.l", n.bracer(hide, iron, plate=iron) + [a.sleeve("bracer_ember", 0.615, 0.635, 0.152, 0.151, ember, bevel=0)])
    s.right("lowerarm", n.wraps(ash))
    s.both("lowerleg", n.shin(hide, trim=iron, plate=iron))
    return s.done()


def kiln_plume(name):
    """Piuma della Fornace: the firebird's — a collar of gold feathers, feathers down both arms, a tail of long red plumes tipped with fire."""
    s = Suit(name, "archer")
    red = m("plume_red", (0.42, 0.05, 0.04))
    flame = m("plume_flame", (0.70, 0.16, 0.05))
    gold = m("plume_gold", (0.95, 0.66, 0.18), metal=0.8, rough=0.3)
    ember = m("class_ember", (1.0, 0.42, 0.08), glow=1.3)
    hide = m("leather_dark")

    vest(s, red, hem=gold)
    quiver_strap(s, hide, buckle=gold)
    s.add("chest", plastron(gold, red), "m")
    s.add("chest", plastron(gold, red, z0=0.93, z1=1.055, depth=0.05), "w")
    # The collar: two rows of feathers spread over the shoulders.
    s.add("chest", [leaves("collar_low", 0.40, 0.36, 1.15, flame, 14, height=0.17, lean=0.16, point=0.6, thick=0.02, a0=-150, a1=150),
                    leaves("collar_high", 0.35, 0.32, 1.20, gold, 12, height=0.14, lean=0.14, point=0.6, offset=0.5, thick=0.02, a0=-150, a1=150),
                    t.sphere("heart", 0.055, ember, at=(0.0, Y0 - RY * 0.78 - 0.035, 1.17), subdivisions=2, squash=(1, 0.6, 1.25)),
                    hoop("heart_ring", (0.0, Y0 - RY * 0.78 - 0.03, 1.17), 0.07, 0.014, gold, right=(1, 0, 0), up=(0, 0, 1), segments=14, sides=6)])

    s.add("hips", n.sash(red, knot=False))
    s.add("hips", [a.band("belt_gold", 0.412, 0.352, 0.635, 0.67, gold, y=0.0, bevel=0.006)])
    # The tail: five long plumes fanned from the small of the back, fire at their tips.
    for i in range(5):
        ang = 180 + (i - 2) * 26
        base = Vector(at(0.40, 0.36, ang, 0.62, 0.0))
        reach = 0.52 - 0.07 * abs(i - 2)
        tip = Vector(at(0.40 + reach * 0.75, 0.36 + reach * 0.75, 180 + (i - 2) * 34, 0.62 - reach * 0.80, 0.0))
        face = Vector((-math.sin(math.radians(ang)), math.cos(math.radians(ang)), 0.6))
        s.add("hips", feather(f"plume_{i}", base, tip, 0.17, flame if i % 2 else red, spine=gold, face=face))
        s.add("hips", t.sphere(f"plume_eye_{i}", 0.04, ember, at=tuple(base + (tip - base) * 0.80 + face.normalized() * 0.012), subdivisions=1, squash=(1, 1, 1.4)))
    for side in (-1, 1):
        s.add("hips", edged(f"hip_flap_{side}", 0.44, 0.38, 0.40, 0.62, red, gold, a0=side * 62 - 30, a1=side * 62 + 30, hem=pointed(0.08), top=0.94, y=0.0, rows=2, edge=0.2))

    s.both("upperarm", arm_leaves("wing", flame, 3, alt=gold, r=0.21, height=0.15))
    s.add("lowerarm.l", n.bracer(hide, gold, plate=gold) + [
        blade("bracer_wing", (0.56, 0.0, ARM_Z + 0.13), 0.24, 0.10, gold, right=(1, 0, 0), up=(0.35, 0, 1), hook=0.5, thick=0.025)])
    s.right("lowerarm", n.bracer(red, gold))
    s.both("lowerleg", n.shin(hide, trim=gold, plate=gold))
    return s.done()


def archer_head(name, band, feather_mat=None, feathers=0, plate=None, antlers=None, ends=None, gem=None, quill=None, wings=None):
    """A band round the brow; `feathers` standing behind the left ear; `plate` over the brow;
    `antlers` branching from each temple; `ends` the band's tails; `wings` a spread of feathers swept back at both temples."""
    rx, ry, y = n.BROW
    s = Suit(name, "archer", head=True)
    s.add("head", n.headband(band, ends=ends))

    if plate is not None:
        s.add("head", n.brow_plate(plate, width=44))

    for i in range(feathers):
        base = (rx - 0.01, 0.10 + 0.07 * i, 1.90)
        tip = (rx + 0.07 + 0.03 * i, 0.30 + 0.12 * i, 2.26 + 0.07 * i)
        s.add("head", feather(f"feather_{i}", base, tip, 0.13, feather_mat, spine=quill, face=(1, 0.2, 0)))

    if wings is not None:
        for side in (-1, 1):
            for i in range(3):
                base = (side * (rx + 0.005), -0.10 + 0.05 * i, 1.89)
                tip = (side * (rx + 0.10 + 0.02 * i), 0.22 + 0.10 * i, 2.04 + 0.13 * i)
                s.add("head", feather(f"wing_{side}_{i}", base, tip, 0.12, wings, spine=quill, face=(side, 0.15, 0.1)))

    if antlers is not None:
        left = [tube("antler", curve([(rx - 0.03, 0.04, 1.90), (rx + 0.08, 0.07, 2.08), (rx + 0.12, 0.10, 2.26), (rx + 0.06, 0.12, 2.42)]), (0.05, 0.01), antlers, sides=7),
                tube("tine_a", curve([(rx + 0.07, 0.07, 2.06), (rx + 0.19, 0.03, 2.14), (rx + 0.24, 0.00, 2.27)]), (0.032, 0.007), antlers, sides=6),
                tube("tine_b", curve([(rx + 0.12, 0.10, 2.24), (rx + 0.22, 0.14, 2.32), (rx + 0.25, 0.15, 2.44)]), (0.028, 0.007), antlers, sides=6),
                tube("tine_c", curve([(rx + 0.03, 0.05, 1.98), (rx + 0.06, -0.08, 2.05), (rx + 0.07, -0.14, 2.15)]), (0.026, 0.007), antlers, sides=6)]
        s.add("head", left + [mirror(p) for p in left])

    if gem is not None:
        s.add("head", t.sphere("gem", 0.05, gem, at=(0, y - ry - 0.03, 1.895), subdivisions=1, squash=(1, 0.7, 1.2)))

    return s.done()


# ------------------------------------------------------------------ the Asura

def cuirass(s, steel, trim, keel=False, lames=None, grow=0.0):
    """A dark breastplate edged in `trim`; `keel` a ridge down its front; `lames` (a, b) bands over the belly in turn."""
    if lames is None:
        vest(s, steel, hem=trim, grow=grow)
    else:
        s.add("chest", a.band("breast", RX + grow, RY + grow, 0.985, 1.21, steel, top=0.90, y=Y0), "m")
        for i in range(3):
            z1 = 0.995 - 0.052 * i
            s.add("spine", a.band(f"lame_{i}", RX + grow + 0.006 - 0.004 * i, RY + grow + 0.006 - 0.004 * i, z1 - 0.062, z1, lames[i % 2], y=Y0, bevel=0.008), "m")
        s.add("spine", a.band("lame_hem", RX + grow + 0.004, RY + grow + 0.004, 0.835, 0.862, trim, y=Y0, bevel=0.006), "m")
        bust(s, steel, trim, grow)
    s.add("chest", a.band("breast_rim", (RX + grow) * 0.92, (RY + grow) * 0.92, 1.175, 1.215, trim, y=Y0, bevel=0.006), "m")
    if keel:
        front = Y0 - RY - grow
        s.add("chest", fin("keel", [(-0.035, 0), (0.035, 0), (0.05, 0.10), (0.0, 0.19), (-0.05, 0.10)], 0.05, steel, (0, front - 0.012, 1.0), right=(1, 0, 0), up=(0, 0.04, 1), edge=0.02), "m")
        s.add("chest", fin("keel_w", [(-0.03, 0), (0.03, 0), (0.04, 0.05), (0.0, 0.10), (-0.04, 0.05)], 0.045, steel, (0, front - 0.012, 0.95), right=(1, 0, 0), up=(0, 0.0, 1), edge=0.018), "w")


def heart(s, gem, ring, z=1.09, size=0.062):
    """The stone on the breast, in its setting: at `z` on the full cut, on the band of the women's."""
    for cut, height in (("m", z), ("w", min(z, 0.995))):
        front = breast(0, height, 0.0)[1]
        s.add("chest", [t.sphere("heart", size, gem, at=(0, front - 0.02, height), subdivisions=2, squash=(1, 0.6, 1.25)),
                        hoop("heart_ring", (0, front - 0.012, height), size * 1.25, 0.016, ring, segments=14, sides=6)], cut)


def pauldron(steel, trim, size=0.19, lame=False, blades=0, spikes=0, horn=None, rune=None, blade_mat=None, reach=0.26):
    """A flared guard for the left shoulder: `lame` a second plate under it, `blades` swept blades
    standing on it, `spikes` cones, `horn` a curved horn, `rune` a lit stone."""
    parts = n.pad(steel, rim=trim, size=size, flat=0.7)
    if lame:
        parts += [t.sphere("pad_low", size * 0.86, steel, at=(0.50, 0.0, 1.15), subdivisions=2, squash=(1.0, 0.95, 0.55)),
                  t.sphere("pad_low_rim", size * 0.89, trim, at=(0.50, 0.0, 1.13), subdivisions=2, squash=(1.0, 0.95, 0.33))]
    for i in range(blades):
        y = (i - (blades - 1) / 2) * 0.115
        length = reach * (1.0 - 0.18 * abs(i - (blades - 1) / 2))
        parts.append(blade(f"pad_blade_{i}", (0.42, y, 1.26), length, 0.13, blade_mat or trim, right=(1, 0, 0), up=(-0.18, y * 0.9, 1)))
    for i in range(spikes):
        ang = math.radians(-50 + 100 * i / max(1, spikes - 1)) if spikes > 1 else 0.0
        base = (0.43, 0.13 * math.sin(ang), 1.25 + 0.03 * math.cos(ang))
        parts.append(a.spike(f"pad_spike_{i}", base, (0.47, 0.30 * math.sin(ang), 1.40 + 0.06 * math.cos(ang)), 0.045, trim, sides=6))
    if horn is not None:
        parts.append(tube("pad_horn", curve([(0.44, 0.0, 1.28), (0.50, -0.02, 1.44), (0.44, -0.06, 1.60), (0.34, -0.10, 1.66)]), (0.062, 0.008), horn, sides=8))
    if rune is not None:
        parts.append(t.sphere("pad_rune", 0.04, rune, at=(0.47, -0.125, 1.235), subdivisions=1))
    return parts


def faulds(mat, trim, rows=1, count=6, z=0.60, drop=0.17):
    """Plates hanging from the belt, curved round the hips, each cut to a point."""
    parts = []
    for row in range(rows):
        k = 1 + 0.03 * (rows - 1 - row)
        parts.append(leaves(f"fauld_{row}", 0.435 * k, 0.375 * k, z - drop * (0.5 + 0.55 * row), mat, count, height=drop, y=0.0, offset=row * 0.5, lean=0.07, point=0.3, overlap=1.06, thick=0.03))
        parts.append(leaves(f"fauld_tip_{row}", 0.44 * k, 0.38 * k, z - drop * (0.5 + 0.55 * row) - drop * 0.36, trim, count, height=drop * 0.3, y=0.0, offset=row * 0.5, lean=0.03, point=0.95, overlap=1.06, thick=0.034))
    return parts


def rune_band(name, x, r, mat):
    """A ring of light round the left forearm."""
    return a.sleeve(name, x, x + 0.022, r, r, mat, bevel=0)


def branded_wraps(name):
    """Bende del Marchio: black bindings and paper wards — what holds the mark in. Rings of light on the marked arm."""
    s = Suit(name, "asura")
    black = m("brand_cloth", (0.045, 0.04, 0.06))
    shroud = m("brand_wine", (0.24, 0.03, 0.06))
    steel = m("asura_steel", (0.07, 0.06, 0.10), metal=0.7, rough=0.34)
    rune = m("brand_iron", (0.24, 0.22, 0.26), metal=0.65, rough=0.4)
    violet = m("class_violet", (0.62, 0.30, 1.0), glow=1.1)
    paper, ink = m("ward_paper", (0.70, 0.66, 0.56)), violet

    s.add("chest", n.binding(black, z0=0.99, z1=1.20, count=3), "m")
    s.add("spine", n.binding(shroud, z0=0.85, z1=0.98, count=2), "m")
    s.add("chest", n.binding(black, z0=0.99, z1=1.07, count=1), "w")
    s.add("spine", n.binding(black, z0=0.87, z1=0.985, count=2), "w")
    # Wards pasted over the bindings.
    s.add("chest", seal("ward_a", breast(-20, 1.09, 0.02), ink, paper, size=(0.08, 0.19), angle=-20), "m")
    s.add("chest", seal("ward_b", breast(16, 1.06, 0.02), ink, paper, size=(0.08, 0.17), angle=16), "m")
    s.add("chest", seal("ward_c", breast(200, 1.08, 0.03), ink, paper, size=(0.08, 0.19), angle=200), "m")
    s.add("chest", seal("ward_a", breast(-20, 0.98, 0.02), ink, paper, size=(0.075, 0.16), angle=-20), "w")
    s.add("chest", seal("ward_b", breast(16, 0.965, 0.02), ink, paper, size=(0.075, 0.15), angle=16), "w")
    s.add("chest", n.collar(shroud))

    s.add("hips", n.sash(shroud, tail=black, lengths=(0.36, 0.26)))

    s.add("upperarm.l", pauldron(steel, rune, size=0.165, rune=violet))
    s.add("lowerarm.l", n.wraps(black) + [rune_band(f"mark_{i}", 0.53 + 0.075 * i, 0.156, violet) for i in range(3)])
    s.right("lowerarm", n.wraps(black))
    s.both("lowerleg", n.shin(black, turns=4))
    return s.done()


def bloodied_leathers(name):
    """Cuoio Insanguinato: black leather under crossed belts and an iron ring, studs on the shoulders, a torn cape."""
    s = Suit(name, "asura")
    hide = m("blood_hide", (0.06, 0.05, 0.065))
    shroud = m("blood_red", (0.36, 0.02, 0.03))
    iron = m("blood_iron", (0.30, 0.28, 0.30), metal=0.7, rough=0.38)
    wine = m("blood_wine", (0.20, 0.02, 0.03))
    violet = m("class_blood", (1.0, 0.14, 0.08), glow=1.1)

    vest(s, hide, hem=wine)
    s.add("chest", n.straps(wine, width=0.07))
    centre = (0.0, Y0 - RY - 0.03, 1.025)
    s.add("chest", [hoop("harness_ring", centre, 0.07, 0.02, iron, segments=16, sides=6),
                    t.sphere("harness_stone", 0.04, violet, at=centre, subdivisions=1, squash=(1, 0.6, 1))])
    s.add("chest", n.collar(shroud))
    s.add("spine", cloak("cape", shroud, top=(0.99, 0.40, 0.26), bottom=(0.62, 0.50, 0.34), curl=0.08, hem=ragged(2, 0.16), rows=5))
    s.add("chest", t.box("cape_knot", (0.36, 0.05, 0.06), wine, at=(0, 0.39, 1.02), bevel=0.02))

    s.add("hips", n.sash(shroud, knot=False))
    s.add("hips", faulds(hide, iron, count=6, drop=0.16))

    shoulder = pauldron(hide, iron, size=0.175)
    shoulder += [t.sphere(f"stud_{i}", 0.03, iron, at=(0.41 + 0.10 * math.cos(math.radians(60 * i)), 0.10 * math.sin(math.radians(60 * i)), 1.285 - 0.02 * abs(math.cos(math.radians(60 * i)))), subdivisions=1)
                 for i in range(6)]
    s.both("upperarm", shoulder)
    s.add("lowerarm.l", n.bracer(hide, iron) + [rune_band("mark", 0.62, 0.153, violet)])
    s.right("lowerarm", n.bracer(hide, iron))
    s.both("lowerleg", n.shin(hide, trim=iron))
    return s.done()


def hexscale_hauberk(name):
    """Usbergo Squamastregata: scales from throat to thigh, dark violet and black in turn, a blade on each shoulder."""
    s = Suit(name, "asura")
    steel = m("asura_steel", (0.07, 0.06, 0.10), metal=0.7, rough=0.34)
    scale = m("hex_scale", (0.02, 0.22, 0.20), metal=0.55, rough=0.4)
    rune = m("hex_verdigris", (0.16, 0.44, 0.36), metal=0.55, rough=0.4)
    shroud = m("hex_teal", (0.02, 0.13, 0.14))
    violet = m("hex_venom", (0.30, 1.0, 0.55), glow=1.0)

    vest(s, steel, hem=rune)
    for row in range(5):
        z = 0.89 + 0.062 * row
        k = 1.0 if z < 1.0 else 1 - 0.035 * (row - 2)
        s.add("spine" if z < 0.985 else "chest", leaves(f"scale_{row}", (RX + 0.012) * k, (RY + 0.012) * k, z, scale if row % 2 else steel, 16, height=0.115, offset=row * 0.5, lean=0.045), None if row < 3 else "m")
    heart(s, violet, rune, z=1.12)
    s.add("chest", a.band("gorget", 0.29, 0.27, 1.17, 1.25, steel, y=0.0, top=0.9, bevel=0.01))
    s.add("spine", cloak("cape", shroud, top=(0.99, 0.41, 0.28), bottom=(0.52, 0.53, 0.36), curl=0.09, hem=pointed(0.12), rows=6))
    s.add("chest", t.box("cape_knot", (0.38, 0.05, 0.06), rune, at=(0, 0.395, 1.02), bevel=0.02))

    s.add("hips", n.sash(shroud, knot=False))
    for row in range(2):
        s.add("hips", leaves(f"skirt_{row}", 0.44 - 0.01 * row, 0.385 - 0.01 * row, 0.47 + 0.09 * row, steel if row else scale, 12, height=0.16, y=0.0, offset=row * 0.5, lean=0.09, point=0.5, thick=0.024))

    s.both("upperarm", pauldron(steel, rune, size=0.185, blades=1, rune=violet, blade_mat=rune))
    s.both("lowerarm", n.bracer(steel, rune, plate=rune))
    s.both("lowerleg", n.shin(steel, trim=rune, plate=steel))
    return s.done()


def ridge_dreadmail(name):
    """Cotta del Terrore: black plate with a keel, lames down the belly, a gorget, two-plated shoulders with a pair of blades each."""
    s = Suit(name, "asura")
    steel = m("dread_steel", (0.035, 0.055, 0.12), metal=0.75, rough=0.3)
    dull = m("dread_iron", (0.09, 0.13, 0.24), metal=0.65, rough=0.4)
    edge = m("dread_edge", (0.44, 0.52, 0.68), metal=0.75, rough=0.3)
    shroud = m("dread_midnight", (0.03, 0.06, 0.24))
    violet = m("dread_ice", (0.45, 0.78, 1.0), glow=1.1)

    cuirass(s, steel, edge, keel=True, lames=(steel, dull))
    s.add("chest", [a.band("gorget", 0.30, 0.28, 1.16, 1.28, steel, y=0.0, top=0.9, bevel=0.01),
                    a.band("gorget_rim", 0.28, 0.26, 1.265, 1.295, edge, y=0.0, bevel=0.005)])
    for side in (-1, 1):
        s.add("chest", t.sphere(f"eye_{side}", 0.035, violet, at=(side * 0.15, Y0 - RY * 0.95 - 0.02, 1.13), subdivisions=1, squash=(1.3, 0.6, 0.7)), "m")
        s.add("chest", t.sphere(f"eye_{side}", 0.035, violet, at=(side * 0.15, Y0 - RY * 0.985 - 0.02, 1.02), subdivisions=1, squash=(1.3, 0.6, 0.7)), "w")
    s.add("spine", cloak("cape", shroud, top=(0.99, 0.42, 0.30), bottom=(0.44, 0.56, 0.40), curl=0.10, hem=zigzag(3, 0.12), rows=7))
    s.add("chest", t.box("cape_bar", (0.42, 0.05, 0.06), edge, at=(0, 0.40, 1.02), bevel=0.02))

    s.add("hips", n.sash(shroud, knot=False))
    s.add("hips", faulds(steel, edge, rows=2, count=6, drop=0.15))

    s.both("upperarm", pauldron(steel, edge, size=0.195, lame=True, blades=2, rune=violet, reach=0.24))
    s.both("lowerarm", n.bracer(steel, edge, plate=dull) + [blade("bracer_blade", (0.55, 0.0, ARM_Z + 0.13), 0.20, 0.09, edge, right=(-1, 0, 0), up=(-0.5, 0, 1), hook=0.4, thick=0.028)])
    s.both("lowerleg", n.shin(steel, trim=edge, plate=steel) + [a.spike("knee", (n.LEG, -0.13, 0.31), (n.LEG, -0.22, 0.42), 0.045, edge, sides=5)])
    return s.done()


def high_collar(steel, trim, z0=1.17, z1=1.50, spread=62, open_=1.22):
    """A collar standing behind the neck, open at the front, flaring upwards."""
    parts = [patch("collar", 0.36, 0.34, z0, z1, steel, a0=180 - spread - 60, a1=180 + spread + 60, top=open_, y=0.06, rows=3, thick=0.03),
             patch("collar_rim", 0.365, 0.345, z0, z1, trim, a0=180 - spread - 60, a1=180 + spread + 60, top=open_, y=0.06, rows=1, thick=0.036, span=(0.0, 0.12))]
    return parts


def wardenbane_plate(name):
    """Piastre Sterminacustodi: black and old gold — a collar standing behind the head, great layered shoulders, gold chevrons on the breast, a cape hemmed in gold."""
    s = Suit(name, "asura")
    steel = m("asura_steel", (0.07, 0.06, 0.10), metal=0.7, rough=0.34)
    gold = m("bane_gold", (0.52, 0.36, 0.12), metal=0.8, rough=0.35)
    shroud = m("bane_crimson", (0.32, 0.02, 0.04))
    violet = m("class_violet", (0.62, 0.30, 1.0), glow=1.1)

    cuirass(s, steel, gold, lames=(steel, steel), grow=0.004)
    front = lambda ang, z: breast(ang, z, 0.016, 0.004)
    for i in range(2):
        for side in (-1, 1):
            s.add("chest", tube(f"chevron_{i}_{side}", [front(side * 58 * (1 - j / 5), 1.19 - 0.05 * i - 0.10 * j / 5) for j in range(6)], 0.016, gold, sides=6, flat=0.6), "m")
    heart(s, violet, gold, z=1.03, size=0.055)
    s.add("chest", high_collar(steel, gold, z1=1.52))
    s.add("spine", [cloak("cape", shroud, top=(0.99, 0.43, 0.32), bottom=(0.34, 0.58, 0.44), curl=0.10, hem=swallow(0.10), rows=8),
                    cloak("cape_hem", gold, top=(0.99, 0.436, 0.325), bottom=(0.34, 0.586, 0.446), curl=0.10, hem=swallow(0.10), rows=1, span=(0.9, 1.0), thick=0.03)])
    s.add("chest", t.box("cape_bar", (0.46, 0.05, 0.06), gold, at=(0, 0.41, 1.02), bevel=0.02))

    s.add("hips", n.sash(shroud, knot=False))
    s.add("hips", faulds(steel, gold, rows=2, count=6, drop=0.16))
    s.add("hips", edged("tabard", 0.45, 0.39, 0.30, 0.62, shroud, gold, a0=-22, a1=22, hem=pointed(0.10), top=0.95, y=0.0, rows=3, edge=0.16))

    s.both("upperarm", pauldron(steel, gold, size=0.215, lame=True, blades=1, spikes=2, rune=violet, blade_mat=gold, reach=0.30))
    s.both("lowerarm", n.bracer(steel, gold, plate=gold) + [t.sphere("bracer_rune", 0.032, violet, at=(0.63, -0.02, ARM_Z + 0.165), subdivisions=1)])
    s.both("lowerleg", n.shin(steel, trim=gold, plate=steel) + [t.sphere("knee", 0.07, gold, at=(n.LEG, -0.125, 0.33), subdivisions=2, squash=(1, 0.6, 1))])
    return s.done()


def ashen_tyrant(name):
    """Tiranno di Cenere: a cage of ashen ribs over black plate, a heart of light behind them, spines down the back, horned shoulders."""
    s = Suit(name, "asura")
    steel = m("tyrant_steel", (0.05, 0.05, 0.06), metal=0.7, rough=0.34)
    ash = m("tyrant_bone", (0.56, 0.54, 0.52), rough=0.6)
    shroud = m("tyrant_ash", (0.17, 0.16, 0.16))
    rune = m("tyrant_iron", (0.22, 0.21, 0.22), metal=0.65, rough=0.4)
    violet = m("tyrant_ember", (1.0, 0.22, 0.06), glow=1.3)

    cuirass(s, steel, rune, lames=(steel, m("tyrant_dull", (0.10, 0.09, 0.12), metal=0.6, rough=0.45)))
    # The ribs: four a side, from the spine round to the breastbone, and the breastbone itself.
    for i in range(4):
        z = 1.165 - 0.058 * i
        for side in (-1, 1):
            pts = [breast(side * (150 - 138 * j / 9), z - 0.05 * (j / 9) ** 2, 0.018) for j in range(10)]
            s.add("chest", tube(f"rib_{i}_{side}", pts, (0.014, 0.024), ash, sides=6), "m" if i < 2 else None)
    front = Y0 - (RY + 0.02) * 0.95
    s.add("chest", tube("breastbone", [(0, front - 0.01, 1.20), (0, front - 0.02, 1.08), (0, front - 0.012, 0.94)], (0.034, 0.02), ash, sides=6), "m")
    s.add("chest", tube("breastbone_w", [(0, front - 0.03, 1.075), (0, front - 0.03, 1.0), (0, front - 0.022, 0.92)], (0.03, 0.02), ash, sides=6), "w")
    s.add("chest", t.sphere("heart", 0.07, violet, at=(-0.10, Y0 - RY * 0.955, 1.03), subdivisions=2, squash=(1, 0.5, 1.1)))
    s.add("chest", high_collar(steel, ash, z1=1.44, open_=1.16))
    # Spines down the back, over the cape's root.
    for i in range(4):
        z = 1.15 - 0.085 * i
        s.add("chest" if z > 0.99 else "spine", a.spike(f"spine_{i}", (0, RY + 0.0, z), (0, RY + 0.17 - 0.02 * i, z + 0.07), 0.04, ash, sides=5))
    for side in (-1, 1):
        s.add("spine", cloak(f"cape_{side}", shroud, top=(0.98, 0.42, 0.14), bottom=(0.28, 0.58, 0.20), curl=0.04, hem=ragged(4 + side, 0.22), rows=7, cols=8))
        for obj in s.bones["spine"][-1:]:
            obj.data.transform(Matrix.Translation((side * 0.18, 0, 0)))

    s.add("hips", n.sash(shroud, knot=False))
    s.add("hips", faulds(steel, ash, rows=2, count=6, drop=0.16))
    # A skull's worth of teeth along the belt.
    s.add("hips", [a.spike(f"tooth_{i}", at(0.415, 0.355, -50 + 20 * i, 0.60, 0.0), at(0.43, 0.37, -50 + 20 * i, 0.51, 0.0), 0.03, ash, sides=4) for i in range(6)])

    s.both("upperarm", pauldron(steel, ash, size=0.21, lame=True, horn=ash, spikes=3, rune=violet))
    s.both("lowerarm", n.bracer(steel, rune, plate=steel) + [tube(f"arm_rib_{i}", [(0.55 + 0.08 * i, -0.10, ARM_Z + 0.11), (0.56 + 0.08 * i, 0.0, ARM_Z + 0.17), (0.55 + 0.08 * i, 0.10, ARM_Z + 0.11)], 0.014, ash, sides=5) for i in range(3)])
    s.both("lowerleg", n.shin(steel, trim=ash, plate=steel) + [a.spike("knee", (n.LEG, -0.13, 0.30), (n.LEG, -0.24, 0.44), 0.05, ash, sides=5)])
    return s.done()


def kiln_fiend(name):
    """Demone della Fornace: the last of them — violet-black plate lined with gold, a sigil lit on the breast, horned shoulders, and three blades of light spread like a wing behind each."""
    s = Suit(name, "asura")
    steel = m("fiend_steel", (0.10, 0.05, 0.16), metal=0.75, rough=0.3)
    dark = m("fiend_dark", (0.05, 0.03, 0.08), metal=0.7, rough=0.35)
    gold = m("fiend_gold", (0.92, 0.64, 0.18), metal=0.8, rough=0.3)
    cloth = m("fiend_cloth", (0.26, 0.05, 0.36))
    violet = m("class_violet", (0.62, 0.30, 1.0), glow=1.3)
    light = m("fiend_light", (0.56, 0.22, 1.0), glow=1.2)

    cuirass(s, steel, gold, keel=False, lames=(steel, dark), grow=0.004)
    # The sigil: a ring on the breast, a line down from it, two lines out to the shoulders.
    surface = lambda ang, z: breast(ang, z, 0.014, 0.004)
    heart(s, violet, gold, z=1.10, size=0.06)
    for side in (-1, 1):
        s.add("chest", tube(f"sigil_{side}", [surface(side * (12 + 46 * j / 5), 1.115 + 0.075 * j / 5) for j in range(6)], 0.011, light, sides=5), "m")
        s.add("chest", tube(f"sigil_low_{side}", [surface(side * (10 + 30 * j / 4), 1.07 - 0.05 * j / 4) for j in range(5)], 0.010, light, sides=5), "m")
        s.add("chest", tube(f"sigil_w_{side}", [surface(side * (12 + 40 * j / 4), 0.995 + 0.05 * j / 4) for j in range(5)], 0.010, light, sides=5), "w")
    s.add("spine", tube("sigil_stem", [at(RX + 0.016, RY + 0.016, 0, 0.99 - 0.14 * j / 3, Y0) for j in range(4)], 0.010, light, sides=5))
    s.add("chest", high_collar(steel, gold, z1=1.50, open_=1.20))

    # The wing: three blades behind each shoulder, rising out past the head, light along them.
    wing = []
    for i in range(3):
        root = Vector((0.28 + 0.03 * i, RY + 0.10, 1.13 - 0.10 * i))
        up = Vector((0.62 + 0.14 * i, 0.20, 0.78 - 0.30 * i)).normalized()
        length = 0.86 - 0.09 * i
        across = up.cross(Vector((0, 1, 0))).normalized()
        wing.append(blade(f"wing_{i}", root, length, 0.20, dark, right=-across, up=up, hook=0.25, thick=0.04))
        wing.append(tube(f"wing_light_{i}", [root + up * length * 0.30, root + up * length * 0.94 - across * 0.03], (0.03, 0.008), light, sides=6, flat=0.5, ref=(0, 1, 0)))
    s.add("chest", wing + [mirror(p) for p in wing])
    s.add("spine", [cloak("cape", cloth, top=(0.99, 0.43, 0.30), bottom=(0.30, 0.58, 0.44), curl=0.10, hem=zigzag(2.5, 0.16), rows=8),
                    cloak("cape_hem", gold, top=(0.99, 0.436, 0.305), bottom=(0.30, 0.586, 0.446), curl=0.10, hem=zigzag(2.5, 0.16), rows=1, span=(0.92, 1.0), thick=0.03)])

    s.add("hips", n.sash(cloth, knot=False))
    s.add("hips", [a.band("belt_gold", 0.412, 0.352, 0.635, 0.67, gold, y=0.0, bevel=0.006)])
    s.add("hips", faulds(steel, gold, rows=2, count=6, drop=0.17))
    s.add("hips", edged("tabard", 0.455, 0.395, 0.28, 0.62, cloth, gold, a0=-22, a1=22, hem=pointed(0.12), top=0.95, y=0.0, rows=3, edge=0.16))

    s.both("upperarm", pauldron(steel, gold, size=0.22, lame=True, horn=gold, blades=1, rune=violet, blade_mat=dark, reach=0.28))
    s.both("lowerarm", n.bracer(steel, gold, plate=gold) + [rune_band("bracer_light", 0.62, 0.153, light),
                                                           blade("bracer_blade", (0.55, 0.0, ARM_Z + 0.13), 0.22, 0.09, gold, right=(-1, 0, 0), up=(-0.5, 0, 1), hook=0.4, thick=0.028)])
    s.both("lowerleg", n.shin(steel, trim=gold, plate=steel) + [a.spike("knee", (n.LEG, -0.13, 0.30), (n.LEG, -0.24, 0.44), 0.05, gold, sides=5),
                                                               t.cylinder("shin_light", 0.136, 0.016, light, at=(n.LEG, 0.005, 0.205), sides=18)])
    return s.done()


def asura_head(name, band, trim=None, horn=None, horn_size=(0.12, 0.30), crest=None, crown=0, gem=None, ends=None, curl=0.10):
    """A dark band round the brow; `horn` a curved horn at each temple, `horn_size` how far out and up,
    `curl` how far its tip comes forward; `crest` a blade over the brow; `crown` so many spikes round the band."""
    rx, ry, y = n.BROW
    s = Suit(name, "asura", head=True)
    s.add("head", n.headband(band, ends=ends))

    if trim is not None:
        s.add("head", [a.band("rim_low", rx + 0.008, ry + 0.008, 1.845, 1.862, trim, sides=40, y=y, bevel=0.004),
                       a.band("rim_high", rx + 0.008, ry + 0.008, 1.925, 1.942, trim, sides=40, y=y, bevel=0.004),
                       n.brow_plate(band, z0=1.84, z1=1.99, width=38)])

    if horn is not None:
        out, up = horn_size
        left = [tube("horn", curve([(rx - 0.04, 0.10, 1.90), (rx + out * 0.75, 0.12, 1.94 + up * 0.25), (rx + out, 0.08 - curl * 0.4, 1.90 + up * 0.72), (rx + out * 0.55, 0.04 - curl, 1.90 + up)], steps=6),
                     (0.075, 0.008), horn, sides=8),
                hoop("horn_ring", (rx + out * 0.42, 0.115, 1.92 + up * 0.13), 0.066, 0.014, trim or band, right=(0, 1, 0), up=(-0.5, 0, 1), segments=12, sides=5)]
        s.add("head", left + [mirror(p) for p in left])

    front = y - ry - 0.02
    if crest is not None:
        s.add("head", fin("crest", [(-0.05, 0), (0.05, 0), (0.07, 0.14), (0.0, 0.40), (-0.07, 0.14)], 0.04, crest, (0, front, 1.92), right=(1, 0, 0), up=(0, -0.10, 1), edge=0.02))

    for i in range(crown):
        ang = -75 + 150 * i / max(1, crown - 1)
        r = math.radians(ang)
        base = ((rx + 0.005) * math.sin(r), y - (ry + 0.005) * math.cos(r), 1.93)
        tip = ((rx + 0.07) * math.sin(r), y - (ry + 0.07) * math.cos(r), 2.10 + 0.10 * math.cos(r * 1.2))
        s.add("head", a.spike(f"crown_{i}", base, tip, 0.04, trim or band, sides=4))

    if gem is not None:
        s.add("head", t.sphere("gem", 0.055, gem, at=(0, y - ry - 0.04, 1.90), subdivisions=1, squash=(1, 0.7, 1.2)))

    return s.done()


# ------------------------------------------------------------------ the Dragonseer

MX, MY = RX + 0.035, RY + 0.035     # the mantle's half width and depth
WY = 0.335                          # how far forward the women's chest stands, bare of the mantle
PAPER = (0.93, 0.88, 0.70)


def mantle(s, cloth, trim, z0=1.02, hem=None, layers=1, edge=0.16, flare=0.84):
    """The mantle over the shoulders, `layers` deep, its lower edge in `trim`."""
    for cut, low in (("m", z0), ("w", z0 + 0.07)):
        for i in range(layers):
            grow, lift = 0.018 * (layers - 1 - i), (0.075 if cut == "m" else 0.05) * i
            s.add("chest", edged(f"mantle_{i}", MX + grow, MY + grow, low + lift, 1.225, cloth, trim, top=flare - 0.05 * i, hem=hem, cols=32, rows=3, edge=edge, thick=0.03), cut)


def stole(s, mat, tip, length=0.86, width=0.12):
    """Two bands from the collar down the front, hanging free below the sash."""
    for cut, deep in (("m", MY), ("w", WY)):
        for side in (-1, 1):
            s.add("spine", [n.strip(f"stole_{side}", (side * 0.13, -deep - 0.012, 1.14), (side * 0.15, -deep - 0.03, length), width, mat, thick=0.026),
                            n.strip(f"stole_tip_{side}", (side * 0.15, -deep - 0.032, length + 0.045), (side * 0.15, -deep - 0.032, length), width + 0.015, tip, thick=0.032)], cut)


def cuffs(s, cloth, trim, r=0.185, bell=0.0):
    """Wide sleeves' ends over the forearms; `bell` opens them further at the wrist."""
    cuff = [a.sleeve("cuff", 0.56, 0.74, 0.150, r + bell, cloth), a.sleeve("cuff_rim", 0.715, 0.745, r + bell + 0.007, r + bell + 0.007, trim, bevel=0)]
    s.both("lowerarm", cuff)


def seals(s, angles, z=0.50):
    """Paper seals hung from the sash on cords, one at each angle round the hips."""
    paper, ink = m("seal_paper", PAPER), m("seal_ink", (0.55, 0.10, 0.08))
    for i, angle in enumerate(angles):
        where = at(0.455, 0.405, angle, z - 0.02 * (i % 2), 0.0)
        s.add("hips", seal(f"seal_{i}", where, ink, paper, angle=angle))
        s.add("hips", a.rod(f"seal_cord_{i}", (where[0], where[1], where[2] + 0.085), at(0.42, 0.36, angle, 0.63, 0.0), 0.006, ink, sides=4))


def tabard(mat, trim, z0=0.26, z1=0.62, half=24, hem=None, back=False, rx=0.47, ry=0.42):
    """A panel hanging from the sash at the front (or the back), over the robe."""
    mid = 180 if back else 0
    return edged("tabard_back" if back else "tabard", rx, ry, z0, z1, mat, trim, a0=mid - half, a1=mid + half, hem=hem, top=0.90, y=0.0, rows=4, edge=0.12)


def sun(name, centre, radius, mat, rays=8, ray=0.09, right=(1, 0, 0), up=(0, 0, 1), disc=True):
    """A disc with rays, standing in the plane of `right` and `up`."""
    c, r, u = Vector(centre), Vector(right).normalized(), Vector(up).normalized()
    nrm = r.cross(u).normalized()
    parts = []
    if disc:
        obj = t.cylinder(name, radius, 0.03, mat, sides=20)
        frame = Matrix((r, u, nrm)).transposed().to_4x4()
        frame.translation = c
        obj.data.transform(frame)
        parts.append(obj)
    for i in range(rays):
        d = math.cos(2 * math.pi * i / rays) * r + math.sin(2 * math.pi * i / rays) * u
        parts.append(a.spike(f"{name}_ray_{i}", tuple(c + d * radius * 0.85), tuple(c + d * (radius + ray)), 0.03, mat, sides=4))
    return parts


def pilgrims_mantle(name):
    """Manto del Pellegrino: undyed linen, a rope for a belt, wooden beads, a gourd at the hip. A pilgrim's."""
    s = Suit(name, "seer")
    linen = m("pilgrim_linen", (0.62, 0.56, 0.42))
    hemp = m("pilgrim_hemp", (0.40, 0.30, 0.16))
    wood = m("pilgrim_wood", (0.30, 0.17, 0.08))
    gourd = m("pilgrim_gourd", (0.66, 0.50, 0.22), rough=0.5)

    mantle(s, linen, hemp, z0=1.06, edge=0.12)
    s.add("chest", n.collar(linen))
    s.add("chest", necklace("beads", wood, z_front=0.99, ry=0.405, count=15, radius=0.028, big=(7, 0.045, gourd)), "m")
    s.add("chest", necklace("beads", wood, z_front=0.99, ry=WY + 0.03, rx=0.33, count=15, radius=0.028, big=(7, 0.045, gourd)), "w")

    s.add("hips", [a.roll("rope_a", 0.415, 0.355, 0.62, 0.665, hemp, y=0.0), a.roll("rope_b", 0.415, 0.355, 0.665, 0.71, hemp, y=0.0),
                   t.sphere("rope_knot", 0.06, hemp, at=(0.20, -0.335, 0.665), subdivisions=2)])
    for i in range(2):
        s.add("hips", tube(f"rope_end_{i}", curve([(0.20, -0.36, 0.65), (0.22 + 0.05 * i, -0.40, 0.50), (0.21 + 0.08 * i, -0.40, 0.34 + 0.06 * i)]), 0.02, hemp, sides=6))
    s.add("hips", [t.sphere("gourd_low", 0.085, gourd, at=(-0.40, -0.16, 0.47), subdivisions=2), t.sphere("gourd_high", 0.055, gourd, at=(-0.40, -0.16, 0.57), subdivisions=2),
                   t.cylinder("gourd_stop", 0.018, 0.04, wood, at=(-0.40, -0.16, 0.63), sides=6)])
    s.add("hips", tabard(linen, hemp, z0=0.34, half=18))
    return s.done()


def reed_mantle(name):
    """Manto di Giunco: a rain cape of reeds — three shaggy tiers over the shoulders, a skirt of them at the hips, jade cord."""
    s = Suit(name, "seer")
    reed = m("reed_straw", (0.60, 0.50, 0.20), rough=0.9)
    reed_dark = m("reed_dark", (0.40, 0.36, 0.12), rough=0.9)
    jade = m("seer_jade", (0.10, 0.42, 0.34))
    linen = m("pilgrim_linen", (0.62, 0.56, 0.42))

    for i in range(3):
        k = 1 + 0.07 * (2 - i)
        s.add("chest", patch(f"reeds_{i}", MX * k, MY * k, 1.02 + 0.065 * i, 1.235, reed if i % 2 == 0 else reed_dark, top=0.74 - 0.04 * i, hem=zigzag(16, 0.075), cols=32, rows=2, thick=0.03), "m" if i == 0 else None)
    s.add("chest", [a.roll("reed_neck", 0.30, 0.28, 1.19, 1.25, jade, y=0.0),
                    t.sphere("reed_knot", 0.045, jade, at=(0.0, -0.285, 1.21), subdivisions=2)])
    for i in range(2):
        s.add("chest", tube(f"neck_cord_{i}", curve([(0.0, -0.30, 1.20), (-0.04 + 0.08 * i, -0.42, 1.12), (-0.06 + 0.12 * i, -0.44, 1.00)]), 0.012, jade, sides=5))

    s.add("hips", n.sash(jade, tail=linen, z0=0.60, z1=0.74, lengths=(0.34, 0.26)))
    for i in range(2):
        s.add("hips", patch(f"reed_skirt_{i}", 0.46 + 0.02 * (1 - i), 0.41 + 0.02 * (1 - i), 0.40 + 0.09 * i, 0.62, reed_dark if i == 0 else reed, top=0.88, hem=zigzag(18, 0.08), cols=36, rows=2, thick=0.026, y=0.0))
    seals(s, [-38], z=0.42)
    cuff = patch("reed_cuff", 0.17, 0.17, -0.12, 0.0, reed, top=0.9, hem=zigzag(8, 0.05), cols=16, rows=1, thick=0.024, y=0.0)
    cuff.data.transform(arm_frame(0.56))
    s.both("lowerarm", [cuff])
    return s.done()


def jadescale_vestment(name):
    """Veste Squamagiada: white, with a cape of jade scales over the shoulders, a jade ring on the breast, a stole, bell sleeves."""
    s = Suit(name, "seer")
    white = m("seer_white", (0.82, 0.80, 0.72))
    jade = m("seer_jade", (0.10, 0.42, 0.34))
    scale = m("jade_scale", (0.16, 0.56, 0.44), metal=0.3, rough=0.4)
    scale_pale = m("jade_scale_pale", (0.46, 0.76, 0.62), metal=0.3, rough=0.4)
    bronze = m("seer_bronze", (0.62, 0.42, 0.18), metal=0.6, rough=0.45)

    mantle(s, white, jade, z0=1.00)
    for row in range(3):
        k = 1.0 - 0.045 * row
        s.add("chest", leaves(f"jade_{row}", (MX + 0.016) * k, (MY + 0.016) * k, 1.07 + 0.058 * row, scale_pale if row == 1 else scale, 16, height=0.115, offset=row * 0.5, lean=0.05), "m" if row == 0 else None)
    s.add("chest", n.collar(jade))
    stole(s, jade, bronze)
    # The jade ring, hung on a cord at the breast.
    for cut, deep in (("m", MY), ("w", WY)):
        ring_at = (0.0, -deep - 0.06, 0.98)
        s.add("spine", [hoop("jade_ring", ring_at, 0.065, 0.024, scale_pale, segments=18, sides=8, flat=0.6),
                        a.rod("jade_cord", (0.0, -deep - 0.05, 1.045), (0.0, -deep - 0.03, 1.12), 0.008, bronze, sides=4),
                        tube("jade_tassel", [(0.0, -deep - 0.06, 0.915), (0.0, -deep - 0.065, 0.80)], (0.02, 0.03), jade, sides=6)], cut)

    s.add("hips", n.sash(jade, tail=white, z0=0.60, z1=0.76, lengths=(0.36, 0.28)))
    s.add("hips", a.roll("cord", 0.418, 0.358, 0.665, 0.70, bronze, y=0.0))
    seals(s, [-52, -30, 52])
    cuffs(s, white, jade, bell=0.015)
    return s.done()


def ridge_oracle(name):
    """Oracolo della Cresta: the mountain oracle's — fur at the shoulders, a bronze mirror on the breast, streamers of three colours down the back, bells at the belt."""
    s = Suit(name, "seer")
    grey = m("oracle_grey", (0.40, 0.44, 0.48))
    blue = m("oracle_blue", (0.10, 0.20, 0.40))
    fur = m("oracle_fur", (0.80, 0.78, 0.72), rough=0.95)
    bronze = m("oracle_bronze", (0.70, 0.48, 0.18), metal=0.75, rough=0.3)
    red, yellow = m("oracle_red", (0.55, 0.08, 0.06)), m("oracle_yellow", (0.74, 0.56, 0.12))

    mantle(s, grey, blue, z0=1.01)
    s.add("chest", [fur_ring("fur", 0.40, 0.37, 1.13, 1.26, fur, count=14, depth=0.07, flare=0.70)])
    # The mirror: a bright disc on a chain of beads.
    for cut, deep in (("m", MY), ("w", WY)):
        mirror_at = (0.0, -deep - 0.04, 1.02 if cut == "m" else 0.98)
        s.add("chest", necklace("chain", bronze, z_front=1.06 if cut == "m" else 1.02, ry=deep + 0.012, rx=0.36 if cut == "m" else 0.32, count=13, radius=0.02, spread=100), cut)
        s.add("chest", [t.cylinder("mirror", 0.085, 0.025, m("oracle_mirror", (0.90, 0.80, 0.50), metal=0.9, rough=0.12), at=mirror_at, sides=20, axis="Y", bevel=0.006),
                        hoop("mirror_rim", mirror_at, 0.088, 0.016, bronze, segments=18, sides=6)], cut)
    # Streamers down the back, from under the mantle.
    for i, mat in enumerate((red, yellow, blue, red, yellow)):
        x = (i - 2) * 0.115
        s.add("spine", n.strip(f"streamer_{i}", (x, MY + 0.03, 1.03), (x * 1.5, MY + 0.17, 0.42 + 0.07 * abs(i - 2)), 0.085, mat, thick=0.02))
    stole(s, m("seer_white", (0.82, 0.80, 0.72)), blue, length=0.88)

    s.add("hips", n.sash(blue, tail=red, z0=0.60, z1=0.76, lengths=(0.36, 0.28)))
    s.add("hips", a.roll("cord", 0.418, 0.358, 0.665, 0.70, bronze, y=0.0))
    for i, angle in enumerate((-66, -40, 40, 66)):
        p = at(0.44, 0.39, angle, 0.53, 0.0)
        s.add("hips", [t.sphere(f"bell_{i}", 0.04, bronze, at=p, subdivisions=2, squash=(1, 1, 1.15)),
                       a.rod(f"bell_cord_{i}", p, (p[0] * 0.95, p[1] * 0.95, 0.63), 0.006, red, sides=4)])
    seals(s, [-12, 12])
    s.both("upperarm", n.pad(blue, rim=bronze, size=0.17, flat=0.45) + [arm_fur("cap_fur", 0.50, 0.185, fur, width=0.08, count=9)])
    cuffs(s, grey, blue)
    return s.done()


def wardens_benediction(name):
    """Benedizione del Custode: white and gold over blue — a mantle in two tiers scalloped in gold, the sun on a blue panel down the front, a wheel of gold rays on the back."""
    s = Suit(name, "seer")
    white = m("bene_white", (0.86, 0.85, 0.80))
    blue = m("bene_blue", (0.07, 0.14, 0.40))
    gold = m("bene_gold", (0.95, 0.68, 0.20), metal=0.8, rough=0.3)
    ice = m("warden_ice", (0.55, 0.85, 1.0), glow=1.0, rough=0.2)

    mantle(s, white, gold, z0=0.99, hem=lambda u: 0.035 * abs(math.sin(math.pi * 10 * u)), layers=2, edge=0.2)
    s.add("chest", n.collar(blue))
    s.add("chest", [t.sphere("clasp", 0.05, ice, at=(0, -RY * 0.86, 1.215), subdivisions=2, squash=(1, 0.7, 1.2)),
                    hoop("clasp_ring", (0, -RY * 0.86 + 0.01, 1.215), 0.06, 0.014, gold, segments=14, sides=6)])
    stole(s, blue, gold, length=0.84)
    # The ring behind the shoulders, rays on it.
    back = (0.0, MY + 0.075, 0.87)
    s.add("spine", sun("wheel", back, 0.10, gold, rays=12, ray=0.11, right=(1, 0, 0), up=(0, 0, 1)))
    s.add("spine", [hoop("wheel_ring", back, 0.165, 0.016, gold, segments=24, sides=6),
                    t.sphere("wheel_stone", 0.045, ice, at=(back[0], back[1] + 0.02, back[2]), subdivisions=2, squash=(1, 0.6, 1))])

    s.add("hips", n.sash(blue, tail=gold, z0=0.60, z1=0.76, lengths=(0.36, 0.28)))
    s.add("hips", a.roll("cord", 0.418, 0.358, 0.665, 0.70, gold, y=0.0))
    s.add("hips", tabard(blue, gold, z0=0.24, half=26, hem=pointed(0.08)))
    s.add("hips", sun("sun", (0.0, -0.435, 0.44), 0.065, gold, rays=8, ray=0.06, up=(0, 0.08, 1)))
    s.add("hips", tabard(white, gold, z0=0.30, half=24, back=True))
    s.both("upperarm", n.pad(white, rim=gold, size=0.18, flat=0.5) + [leaves("cap_points", 0.19, 0.19, 0.0, gold, 6, height=0.09, a0=-40, a1=220, y=0.0, lean=0.04, point=0.7, thick=0.02, frame=arm_frame(0.53))])
    cuffs(s, white, gold, bell=0.02)
    return s.done()


def ashen_vigil(name):
    """Veglia di Cenere: the watcher's by the dead fire — charred robes torn at every hem, a censer still burning at the hip, beads of ember, burnt prayers down the back."""
    s = Suit(name, "seer")
    char = m("vigil_char", (0.09, 0.09, 0.10))
    grey = m("vigil_grey", (0.24, 0.23, 0.25))
    rust = m("vigil_rust", (0.42, 0.14, 0.04))
    iron = m("vigil_iron", (0.16, 0.16, 0.18), metal=0.6, rough=0.45)
    ember = m("class_ember", (1.0, 0.42, 0.08), glow=1.3)
    coal = m("vigil_coal", (1.0, 0.30, 0.05), glow=0.8)

    s.add("chest", patch("mantle_low", MX + 0.022, MY + 0.022, 0.98, 1.225, grey, top=0.80, hem=ragged(3, 0.10), cols=36, rows=3, thick=0.03), "m")
    s.add("chest", patch("mantle_high", MX + 0.036, MY + 0.036, 1.07, 1.23, char, top=0.76, hem=ragged(8, 0.08), cols=36, rows=2, thick=0.03))
    s.add("chest", patch("mantle_glow", MX + 0.012, MY + 0.012, 0.99, 1.02, coal, top=1.0, hem=ragged(3, 0.07), cols=36, rows=1, thick=0.012), "m")
    s.add("chest", [a.roll("cowl", 0.32, 0.30, 1.13, 1.27, char, y=0.0)])
    s.add("chest", necklace("embers", iron, z_front=0.97, ry=MY + 0.05, rx=0.38, count=13, radius=0.03, spread=105, big=(6, 0.05, ember), alt=(3, ember)), "m")
    s.add("chest", necklace("embers", iron, z_front=0.96, ry=WY + 0.03, rx=0.33, count=13, radius=0.03, spread=105, big=(6, 0.05, ember), alt=(3, ember)), "w")
    stole(s, char, rust, length=0.80, width=0.11)
    # Burnt prayers: long seals down the back, their ends alight.
    for i in range(3):
        x = (i - 1) * 0.17
        z0 = 0.50 + 0.08 * abs(i - 1)
        s.add("spine", [n.strip(f"prayer_{i}", (x, MY + 0.03, 1.02), (x * 1.3, MY + 0.15, z0), 0.10, m("vigil_paper", (0.46, 0.42, 0.34)), thick=0.016),
                        n.strip(f"prayer_end_{i}", (x * 1.3 - 0.0, MY + 0.142, z0 + 0.05), (x * 1.3, MY + 0.152, z0 - 0.01), 0.105, coal, thick=0.02)])

    s.add("hips", n.sash(char, tail=rust, z0=0.60, z1=0.76, lengths=(0.36, 0.28)))
    s.add("hips", a.roll("cord", 0.418, 0.358, 0.665, 0.70, rust, y=0.0))
    s.add("hips", patch("skirt", 0.47, 0.42, 0.40, 0.62, grey, top=0.88, hem=ragged(6, 0.16), cols=36, rows=3, thick=0.026, y=0.0))
    # The censer: an iron cage on a chain, the coal alight inside.
    cx, cy, cz = -0.42, -0.20, 0.36
    s.add("hips", [t.sphere("censer_coal", 0.06, ember, at=(cx, cy, cz), subdivisions=2),
                   hoop("censer_a", (cx, cy, cz), 0.078, 0.012, iron, right=(1, 0, 0), up=(0, 0, 1), segments=14, sides=5),
                   hoop("censer_b", (cx, cy, cz), 0.078, 0.012, iron, right=(0, 1, 0), up=(0, 0, 1), segments=14, sides=5),
                   hoop("censer_c", (cx, cy, cz), 0.078, 0.012, iron, right=(1, 0, 0), up=(0, 1, 0), segments=14, sides=5),
                   a.spike("censer_cap", (cx, cy, cz + 0.07), (cx, cy, cz + 0.14), 0.04, iron, sides=6),
                   a.rod("censer_chain", (cx, cy, cz + 0.12), (cx + 0.02, cy, 0.64), 0.008, iron, sides=4)])
    cap = patch("cap", 0.20, 0.20, -0.24, 0.0, char, a0=-50, a1=230, hem=ragged(9, 0.10), y=0.0, rows=2, thick=0.022)
    cap.data.transform(arm_frame(0.27))
    s.both("upperarm", [cap])
    cuffs(s, grey, rust)
    return s.done()


def kiln_dragon(name):
    """Drago della Fornace: jade and gold — a cape of gold dragon scales, antlers of gold on the shoulders, the pearl at the breast, a red sash of heaven flying behind."""
    s = Suit(name, "seer")
    jade = m("dragon_jade", (0.06, 0.36, 0.28))
    red = m("dragon_red", (0.50, 0.06, 0.04))
    gold = m("dragon_gold", (0.95, 0.66, 0.18), metal=0.8, rough=0.3)
    gold_dark = m("dragon_gold_dark", (0.70, 0.42, 0.10), metal=0.8, rough=0.35)
    pearl = m("dragon_pearl", (0.40, 1.0, 0.78), glow=1.4)

    mantle(s, jade, gold, z0=0.99, edge=0.14)
    for row in range(4):
        k = 1.0 - 0.042 * row
        s.add("chest", leaves(f"dragon_{row}", (MX + 0.016) * k, (MY + 0.016) * k, 1.055 + 0.052 * row, gold_dark if row % 2 else gold, 16, height=0.11, offset=row * 0.5, lean=0.06), "m" if row == 0 else None)
    s.add("chest", n.collar(red))
    # The pearl, held in a claw of gold.
    for cut, pearl_at in (("m", (0.0, -MY - 0.045, 1.04)), ("w", (0.0, -WY - 0.05, 0.99))):
        s.add("chest", [t.sphere("pearl", 0.07, pearl, at=pearl_at, subdivisions=2)], cut)
        for i in range(4):
            r = math.radians(45 + 90 * i)
            root = (pearl_at[0] + 0.085 * math.cos(r), pearl_at[1] + 0.04, pearl_at[2] + 0.085 * math.sin(r))
            tip = (pearl_at[0] + 0.035 * math.cos(r), pearl_at[1] - 0.065, pearl_at[2] + 0.035 * math.sin(r))
            s.add("chest", tube(f"claw_{i}", curve([root, ((root[0] + tip[0]) / 2 * 1.5, pearl_at[1] - 0.05, pearl_at[2] + (root[2] + tip[2] - 2 * pearl_at[2]) / 2 * 1.5), tip]), (0.018, 0.005), gold, sides=5), cut)
    stole(s, red, gold, length=0.82)

    # The sash of heaven: a red ribbon in an arc behind the head, its ends flying out below the arms.
    arc = curve([(-0.92, 0.36, 0.52), (-0.76, 0.34, 0.70), (-0.76, 0.52, 1.06), (-0.52, 0.70, 1.50), (0.0, 0.76, 1.68), (0.52, 0.70, 1.50), (0.76, 0.52, 1.06), (0.76, 0.34, 0.70), (0.92, 0.36, 0.52)], steps=6)
    s.add("chest", tube("heaven", arc, 0.058, red, sides=6, flat=0.18, ref=(0, 1, 0.25)))
    for side in (-1, 1):
        s.add("chest", t.sphere(f"heaven_end_{side}", 0.04, gold, at=(side * 0.93, 0.36, 0.50), subdivisions=1, squash=(1, 1, 1.5)))

    s.add("hips", n.sash(red, tail=gold, z0=0.60, z1=0.76, lengths=(0.38, 0.30)))
    s.add("hips", a.roll("cord", 0.418, 0.358, 0.665, 0.70, gold, y=0.0))
    s.add("hips", tabard(jade, gold, z0=0.24, half=26, hem=pointed(0.09)))
    for row in range(3):
        s.add("hips", leaves(f"tabard_scale_{row}", 0.478, 0.428, 0.36 + 0.075 * row, gold if row % 2 else gold_dark, 3, height=0.11, a0=-20, a1=20, y=0.0, lean=0.02, offset=0))
    seals(s, [-62, -44, 44, 62], z=0.48)

    shoulder = n.pad(gold, rim=gold_dark, size=0.175, flat=0.5)
    shoulder += [tube("antler", curve([(0.42, 0.0, 1.27), (0.47, 0.05, 1.42), (0.42, 0.12, 1.56), (0.33, 0.18, 1.62)]), (0.05, 0.008), gold, sides=7),
                 tube("antler_tine", curve([(0.46, 0.04, 1.40), (0.54, 0.02, 1.46), (0.57, 0.0, 1.56)]), (0.03, 0.006), gold, sides=6),
                 arm_leaves("cap_scale", gold_dark, 1, x0=0.49, r=0.185, count=6, height=0.10)[0]]
    s.both("upperarm", shoulder)
    cuffs(s, jade, gold, bell=0.025)
    for bone in ("lowerarm.l", "lowerarm.r"):
        sign = 1 if bone.endswith("l") else -1
        s.add(bone, t.cylinder("cuff_pearl", 0.026, 0.02, pearl, at=(sign * 0.65, 0.0, ARM_Z + 0.175), sides=10))
    return s.done()


def seer_head(name, band, trim=None, ends=None, gem=None, disc=None, pendants=None, halo=None, hang=False, rays=0, horns=None):
    """A fillet round the brow; `ends` its tails; `gem` a stone on the brow; `disc` a sun standing
    over it with `rays`; `pendants` beads hanging at each temple; `halo` a ring behind the head;
    `hang` a paper seal from the knot; `horns` a small pair of antlers."""
    rx, ry, y = n.BROW
    s = Suit(name, "seer", head=True)
    s.add("head", n.headband(band, ends=ends))

    if trim is not None:
        s.add("head", [a.band("rim_low", rx + 0.008, ry + 0.008, 1.845, 1.862, trim, sides=40, y=y, bevel=0.004),
                       a.band("rim_high", rx + 0.008, ry + 0.008, 1.925, 1.942, trim, sides=40, y=y, bevel=0.004)])

    front = y - ry - 0.03

    if gem is not None:
        s.add("head", t.sphere("gem", 0.055, gem, at=(0, front, 1.895), subdivisions=2, squash=(1, 0.7, 1.2)))

    if disc is not None:
        s.add("head", sun("disc", (0, front + 0.005, 2.09), 0.11, disc, rays=rays, ray=0.07, right=(1, 0, 0), up=(0, 0, 1)))
        s.add("head", a.spike("disc_stem", (0, front, 1.93), (0, front, 2.02), 0.035, trim or band, sides=4))

    if pendants is not None:
        for side in (-1, 1):
            x = side * (rx + 0.014)
            s.add("head", a.rod(f"pendant_cord_{side}", (x, -0.02, 1.86), (x, -0.02, 1.58), 0.008, trim or band, sides=4))
            s.add("head", beads(f"pendant_{side}", [(x, -0.02, 1.78 - 0.085 * i) for i in range(3)], 0.036, pendants))

    if halo is not None:
        centre = (0, ry + 0.10, 2.04)
        s.add("head", [hoop("halo", centre, 0.47, 0.03, halo, segments=36, sides=8), hoop("halo_inner", centre, 0.39, 0.012, halo, segments=30, sides=5)])
        for i in range(9):
            r = math.radians(-80 + 20 * i)
            d = Vector((math.sin(r), 0, math.cos(r)))
            s.add("head", a.spike(f"halo_ray_{i}", tuple(Vector(centre) + d * 0.48), tuple(Vector(centre) + d * (0.62 if i % 2 == 0 else 0.56)), 0.03, halo, sides=4))

    if horns is not None:
        left = [tube("horn", curve([(rx - 0.03, 0.06, 1.91), (rx + 0.07, 0.10, 2.06), (rx + 0.06, 0.16, 2.22), (rx - 0.02, 0.22, 2.32)]), (0.045, 0.008), horns, sides=7),
                tube("horn_tine", curve([(rx + 0.06, 0.10, 2.05), (rx + 0.15, 0.08, 2.12), (rx + 0.18, 0.07, 2.22)]), (0.028, 0.006), horns, sides=6)]
        s.add("head", left + [mirror(p) for p in left])

    if hang:
        s.add("head", seal("seal", (-0.40, 0.50, 1.62), m("seal_ink", (0.55, 0.10, 0.08)), m("seal_paper", PAPER), size=(0.09, 0.18), angle=150))

    return s.done()


# ------------------------------------------------------------------ the pieces by name

def abyss_plume(name):
    """Giaco dell'Abisso (P5, 2026-10-07): the carrion bird's — black leather under a collar of black feathers with bone quills,
    a crimson eye on the breast in a ring of bone, a tail of black plumes with bone shafts, bone talons on the bow arm."""
    s = Suit(name, "archer")
    black = m("abyss_leather", (0.05, 0.04, 0.06))
    plume = m("abyss_plume", (0.09, 0.05, 0.11))
    bone = m("bone")
    glow = m("abyss_glow", (0.95, 0.10, 0.38), glow=1.3)

    vest(s, black, hem=bone)
    quiver_strap(s, m("leather_dark"), buckle=bone)
    s.add("chest", plastron(plume, black), "m")
    s.add("chest", plastron(plume, black, z0=0.93, z1=1.055, depth=0.05), "w")
    s.add("chest", [leaves("collar_low", 0.40, 0.36, 1.15, plume, 14, height=0.18, lean=0.16, point=0.7, thick=0.02, a0=-150, a1=150),
                    leaves("collar_high", 0.35, 0.32, 1.20, black, 12, height=0.14, lean=0.14, point=0.7, offset=0.5, thick=0.02, a0=-150, a1=150),
                    t.sphere("eye", 0.05, glow, at=(0.0, Y0 - RY * 0.78 - 0.035, 1.17), subdivisions=2, squash=(1, 0.6, 1.25)),
                    hoop("eye_ring", (0.0, Y0 - RY * 0.78 - 0.03, 1.17), 0.07, 0.016, bone, right=(1, 0, 0), up=(0, 0, 1), segments=14, sides=6)])

    s.add("hips", n.sash(black, knot=False))
    s.add("hips", [a.band("belt_bone", 0.412, 0.352, 0.635, 0.67, bone, y=0.0, bevel=0.006)])
    for i in range(5):
        ang = 180 + (i - 2) * 26
        base = Vector(at(0.40, 0.36, ang, 0.62, 0.0))
        reach = 0.56 - 0.07 * abs(i - 2)
        tip = Vector(at(0.40 + reach * 0.75, 0.36 + reach * 0.75, 180 + (i - 2) * 34, 0.62 - reach * 0.80, 0.0))
        face = Vector((-math.sin(math.radians(ang)), math.cos(math.radians(ang)), 0.6))
        s.add("hips", feather(f"plume_{i}", base, tip, 0.16, plume if i % 2 else black, spine=bone, face=face))
    for side in (-1, 1):
        s.add("hips", edged(f"hip_flap_{side}", 0.44, 0.38, 0.40, 0.62, black, bone, a0=side * 62 - 30, a1=side * 62 + 30, hem=ragged(3, 0.10), top=0.94, y=0.0, rows=2, edge=0.2))

    s.both("upperarm", arm_leaves("wing", plume, 3, alt=black, r=0.21, height=0.16))
    talons = [a.spike(f"talon_{i}", (0.60 + 0.05 * i, -0.10, ARM_Z + 0.10), (0.66 + 0.05 * i, -0.22, ARM_Z + 0.22), 0.02, bone, sides=5) for i in range(3)]
    s.add("lowerarm.l", n.bracer(black, bone, plate=bone) + talons)
    s.right("lowerarm", n.bracer(plume, bone))
    s.both("lowerleg", n.shin(black, trim=bone, plate=plume))
    return s.done()


def abyss_fiend(name):
    """Armatura Infernale dell'Abisso (P5, 2026-10-07): the Asura's last — black plate cracked with crimson, a ribcage of bone
    over the breast, and behind each shoulder a wing of bone ribs with the light between them."""
    s = Suit(name, "asura")
    steel = m("abyss_plate", (0.05, 0.04, 0.07), metal=0.75, rough=0.3)
    dark = m("abyss_dark", (0.10, 0.06, 0.12), metal=0.6, rough=0.4)
    cloth = m("abyss_cloth", (0.22, 0.03, 0.10))
    glow = m("abyss_glow", (0.95, 0.10, 0.38), glow=1.3)
    bone = m("bone")

    cuirass(s, steel, dark, keel=False, lames=(steel, dark), grow=0.004)
    surface = lambda ang, z: breast(ang, z, 0.014, 0.004)
    heart(s, glow, bone, z=1.10, size=0.055)
    for side in (-1, 1):
        for j in range(3):
            z = 1.16 - 0.06 * j
            s.add("chest", tube(f"rib_{side}_{j}", [surface(side * (14 + 9 * i), z - 0.004 * i * i) for i in range(5)], 0.013, bone, sides=6, flat=0.5), "m")
        s.add("chest", tube(f"rib_w_{side}", [surface(side * (14 + 9 * i), 1.0 - 0.004 * i * i) for i in range(4)], 0.011, bone, sides=6, flat=0.5), "w")
    s.add("chest", high_collar(steel, dark, z1=1.50, open_=1.20))

    # The wing: four ribs of bone behind each shoulder, rising past the head, a thread of light down each.
    wing = []
    for i in range(4):
        root = Vector((0.26 + 0.03 * i, RY + 0.10, 1.15 - 0.08 * i))
        up = Vector((0.60 + 0.15 * i, 0.22, 0.80 - 0.26 * i)).normalized()
        length = 0.82 - 0.08 * i
        across = up.cross(Vector((0, 1, 0))).normalized()
        wing.append(tube(f"wing_rib_{i}", [root, root + up * length * 0.55 - across * 0.04, root + up * length], (0.035, 0.008), bone, sides=7, ref=(0, 1, 0)))
        wing.append(tube(f"wing_light_{i}", [root + up * length * 0.25, root + up * length * 0.85 - across * 0.02], (0.016, 0.004), glow, sides=5, ref=(0, 1, 0)))
    s.add("chest", wing + [mirror(p) for p in wing])
    s.add("spine", [cloak("cape", cloth, top=(0.99, 0.43, 0.30), bottom=(0.30, 0.58, 0.44), curl=0.10, hem=ragged(4, 0.20), rows=8)])

    s.add("hips", n.sash(cloth, knot=False))
    s.add("hips", [a.band("belt_bone", 0.412, 0.352, 0.635, 0.67, bone, y=0.0, bevel=0.006)])
    s.add("hips", faulds(steel, dark, rows=2, count=6, drop=0.17))

    s.both("upperarm", pauldron(steel, dark, size=0.22, lame=True, horn=bone, rune=glow, reach=0.28))
    s.both("lowerarm", n.bracer(steel, dark, plate=bone) + [rune_band("bracer_light", 0.62, 0.153, glow)])
    s.both("lowerleg", n.shin(steel, trim=dark, plate=steel) + [a.spike("knee", (n.LEG, -0.13, 0.30), (n.LEG, -0.25, 0.45), 0.05, bone, sides=6),
                                                               t.cylinder("shin_light", 0.136, 0.016, glow, at=(n.LEG, 0.005, 0.205), sides=18)])
    return s.done()


def abyss_dragon(name):
    """Veste dell'Abisso (P5, 2026-10-07): the Dragonseer's last — black silk under a mantle of bone dragon scales, a crimson pearl
    held in claws of bone at the breast, bone horns on the shoulders, a black stole hemmed in crimson."""
    s = Suit(name, "seer")
    silk = m("abyss_silk", (0.05, 0.03, 0.06))
    crimson = m("abyss_cloth", (0.22, 0.03, 0.10))
    bone = m("bone")
    bone_dark = m("abyss_bone_dark", (0.40, 0.36, 0.34), rough=0.6)
    pearl = m("abyss_glow", (0.95, 0.10, 0.38), glow=1.4)

    mantle(s, silk, bone, z0=0.99, edge=0.14)
    for row in range(4):
        k = 1.0 - 0.042 * row
        s.add("chest", leaves(f"scale_{row}", (MX + 0.016) * k, (MY + 0.016) * k, 1.055 + 0.052 * row, bone_dark if row % 2 else bone, 16, height=0.11, offset=row * 0.5, lean=0.06), "m" if row == 0 else None)
    s.add("chest", n.collar(crimson))
    for cut, pearl_at in (("m", (0.0, -MY - 0.045, 1.04)), ("w", (0.0, -WY - 0.05, 0.99))):
        s.add("chest", [t.sphere("pearl", 0.07, pearl, at=pearl_at, subdivisions=2)], cut)
        for i in range(4):
            r = math.radians(45 + 90 * i)
            root = (pearl_at[0] + 0.085 * math.cos(r), pearl_at[1] + 0.04, pearl_at[2] + 0.085 * math.sin(r))
            tip = (pearl_at[0] + 0.035 * math.cos(r), pearl_at[1] - 0.065, pearl_at[2] + 0.035 * math.sin(r))
            s.add("chest", tube(f"claw_{i}", curve([root, ((root[0] + tip[0]) / 2 * 1.5, pearl_at[1] - 0.05, pearl_at[2] + (root[2] + tip[2] - 2 * pearl_at[2]) / 2 * 1.5), tip]), (0.018, 0.005), bone, sides=5), cut)
    stole(s, silk, crimson, length=0.82)

    s.add("hips", n.sash(crimson, tail=bone, z0=0.60, z1=0.76, lengths=(0.38, 0.30)))
    s.add("hips", a.roll("cord", 0.418, 0.358, 0.665, 0.70, bone, y=0.0))
    s.add("hips", tabard(silk, crimson, z0=0.24, half=26, hem=ragged(3, 0.09)))
    for row in range(3):
        s.add("hips", leaves(f"tabard_scale_{row}", 0.478, 0.428, 0.36 + 0.075 * row, bone if row % 2 else bone_dark, 3, height=0.11, a0=-20, a1=20, y=0.0, lean=0.02, offset=0))

    shoulder = n.pad(silk, rim=bone, size=0.175, flat=0.5)
    shoulder += [tube("horn", curve([(0.42, 0.0, 1.27), (0.50, 0.02, 1.40), (0.52, 0.10, 1.54), (0.46, 0.16, 1.62)]), (0.05, 0.008), bone, sides=7),
                 arm_leaves("cap_scale", bone_dark, 1, x0=0.49, r=0.185, count=6, height=0.10)[0]]
    s.both("upperarm", shoulder)
    cuffs(s, silk, bone, bell=0.025)
    return s.done()


def build(name):
    silver = m("class_silver", (0.80, 0.82, 0.86), metal=0.7, rough=0.3)
    iron = m("iron", metal=0.5, rough=0.55)
    bone = m("bone")
    gold = m("gold", metal=0.75, rough=0.35)
    hide = m("leather_dark")
    ember = m("class_ember", (1.0, 0.42, 0.08), glow=1.3)
    violet = m("class_violet", (0.62, 0.30, 1.0), glow=1.1)
    ash_iron = m("ash_archer_iron", (0.20, 0.20, 0.23), metal=0.65, rough=0.4)

    suits = {
        "trail_jerkin": trail_jerkin, "hunters_hide": hunters_hide, "fletchers_scales": fletchers_scales, "ridge_ranger": ridge_ranger,
        "wardens_mantle": wardens_mantle, "ashen_stalker": ashen_stalker, "kiln_plume": kiln_plume,
        "branded_wraps": branded_wraps, "bloodied_leathers": bloodied_leathers, "hexscale_hauberk": hexscale_hauberk, "ridge_dreadmail": ridge_dreadmail,
        "wardenbane_plate": wardenbane_plate, "ashen_tyrant": ashen_tyrant, "kiln_fiend": kiln_fiend,
        "pilgrims_mantle": pilgrims_mantle, "reed_mantle": reed_mantle, "jadescale_vestment": jadescale_vestment, "ridge_oracle": ridge_oracle,
        "wardens_benediction": wardens_benediction, "ashen_vigil": ashen_vigil, "kiln_dragon": kiln_dragon,
        "abyss_plume": abyss_plume, "abyss_fiend": abyss_fiend, "abyss_dragon": abyss_dragon,
    }
    if name in suits:
        return suits[name](name)

    green = m("trail_green", (0.14, 0.30, 0.13))
    black = m("asura_steel", (0.07, 0.06, 0.10), metal=0.7, rough=0.34)
    rune = m("asura_rune", (0.42, 0.16, 0.62), metal=0.5, rough=0.4)
    shroud = m("asura_shroud", (0.20, 0.05, 0.32))
    tyrant = m("tyrant_steel", (0.05, 0.05, 0.06), metal=0.7, rough=0.34)
    ash = m("tyrant_bone", (0.56, 0.54, 0.52), rough=0.6)
    bane_gold = m("bane_gold", (0.52, 0.36, 0.12), metal=0.8, rough=0.35)
    linen = m("pilgrim_linen", (0.62, 0.56, 0.42))
    white, jade = m("seer_white", (0.82, 0.80, 0.72)), m("seer_jade", (0.10, 0.42, 0.34))
    jade_stone = m("jade_stone", (0.20, 0.70, 0.52), rough=0.3)
    bene_blue, bene_gold = m("bene_blue", (0.07, 0.14, 0.40)), m("bene_gold", (0.95, 0.68, 0.20), metal=0.8, rough=0.3)
    char, rust = m("vigil_char", (0.09, 0.09, 0.10)), m("vigil_rust", (0.42, 0.14, 0.04))

    mire_teal = m("mire_teal", (0.04, 0.17, 0.19))
    verdigris = m("mire_verdigris", (0.16, 0.44, 0.36), metal=0.6, rough=0.4)
    pearl = m("mire_pearl", (0.92, 0.95, 0.90), glow=0.5, rough=0.2)

    heads = {
        "trail_band": lambda: archer_head(name, green, feather_mat=m("feather_brown", (0.36, 0.22, 0.10)), feathers=1, ends=green, quill=bone),
        "ironfeather_band": lambda: archer_head(name, hide, feather_mat=m("feather_grey", (0.50, 0.53, 0.58), metal=0.5, rough=0.4), feathers=2, plate=iron, quill=iron),
        "wardens_plume": lambda: archer_head(name, m("warden_archer_blue", (0.08, 0.15, 0.36)), plate=silver, wings=silver, quill=m("warden_archer_night", (0.05, 0.08, 0.20)),
                                             gem=m("warden_ice", (0.55, 0.85, 1.0), glow=1.0, rough=0.2)),
        "ashen_antlers": lambda: archer_head(name, m("ash_archer_cloth", (0.07, 0.07, 0.08)), antlers=m("ash_bone", (0.62, 0.58, 0.50)), plate=ash_iron, gem=ember),

        "brand_band": lambda: asura_head(name, m("brand_cloth", (0.045, 0.04, 0.06)), ends=m("brand_wine", (0.24, 0.03, 0.06)), gem=violet),
        "iron_horns": lambda: asura_head(name, black, trim=m("hex_verdigris", (0.16, 0.44, 0.36), metal=0.55, rough=0.4), horn=black, horn_size=(0.13, 0.26),
                                         gem=m("hex_venom", (0.30, 1.0, 0.55), glow=1.0), curl=0.06),
        "wardenbane_crest": lambda: asura_head(name, black, trim=bane_gold, horn=black, horn_size=(0.19, 0.36), crest=bane_gold, gem=violet, curl=0.14),
        "ashen_crown": lambda: asura_head(name, tyrant, trim=m("tyrant_iron", (0.22, 0.21, 0.22), metal=0.65, rough=0.4), horn=ash, horn_size=(0.26, 0.50), crown=5,
                                          gem=m("tyrant_ember", (1.0, 0.22, 0.06), glow=1.3), curl=0.22),

        "pilgrims_fillet": lambda: seer_head(name, linen, ends=linen, hang=True),
        "jade_fillet": lambda: seer_head(name, white, trim=jade, ends=jade, gem=jade_stone, pendants=jade_stone),
        "wardens_diadem": lambda: seer_head(name, bene_blue, trim=bene_gold, disc=bene_gold, rays=8, pendants=m("warden_ice", (0.55, 0.85, 1.0), glow=1.0, rough=0.2)),
        "ashen_halo": lambda: seer_head(name, char, trim=rust, gem=ember, pendants=ember, halo=m("vigil_halo", (1.0, 0.36, 0.06), glow=0.9)),

        # Act 2, the Drowned Mire (A2-03): verdigris, deep teal, coral and pearl — what the marsh kept.
        "drowned_hood": lambda: archer_head(name, mire_teal, plate=verdigris, wings=m("mire_kelp", (0.10, 0.30, 0.16)), quill=verdigris, gem=pearl, ends=mire_teal),
        "drowned_horns": lambda: asura_head(name, m("mire_deep", (0.03, 0.07, 0.09)), trim=verdigris, horn=m("mire_coral", (0.80, 0.30, 0.26), rough=0.6), horn_size=(0.22, 0.44), crown=7,
                                            gem=pearl, curl=0.16),
        "drowned_tiara": lambda: seer_head(name, mire_teal, trim=verdigris, gem=pearl, disc=verdigris, rays=7, pendants=pearl,
                                           halo=m("mire_halo", (0.25, 0.95, 0.80), glow=0.8)),

        # The abyss set (P5, 2026-10-07): black, bone and crimson light.
        "abyss_cowl": lambda: archer_head(name, m("abyss_leather", (0.05, 0.04, 0.06)), feather_mat=m("abyss_plume", (0.09, 0.05, 0.11)), feathers=2, plate=bone,
                                          antlers=bone, quill=bone, gem=m("abyss_glow", (0.95, 0.10, 0.38), glow=1.3)),
        "abyss_horns": lambda: asura_head(name, m("abyss_plate", (0.05, 0.04, 0.07), metal=0.75, rough=0.3), trim=bone, horn=bone, horn_size=(0.24, 0.48), crown=5,
                                          gem=m("abyss_glow", (0.95, 0.10, 0.38), glow=1.3), curl=0.20),
        "abyss_diadem": lambda: seer_head(name, m("abyss_silk", (0.05, 0.03, 0.06)), trim=bone, gem=m("abyss_glow", (0.95, 0.10, 0.38), glow=1.3), disc=bone, rays=9,
                                          pendants=m("abyss_glow", (0.95, 0.10, 0.38), glow=1.3), horns=bone),
    }
    return heads[name]()
