"""Four-legged creatures: a body lofted from rings, a skeleton, and the clips the game plays (PIP-03).

A creature is drawn as tubes (loft) whose rings follow its spine, legs, neck and tail, so the
body is one smooth shape rather than a pile of balls. Every part is weighted to the bones it
lies along (bind), the nearest the most, so the body bends at its joints instead of breaking.

The clips are what ModelAnimator looks for: Idle, Run, Attack, HitReact and Death. They are
built as poses — how far each bone turns about the world's axes — keyed a frame at a time, so
a clip is a few lines of numbers rather than hand-placed keys.

Blender's axes: Z is up and the creature faces -Y, as every Temins model does; left is +X.
"""

import math

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector

import temins as t

FPS = 24


# --- Shape -------------------------------------------------------------------------------


def smooth(rings, per=3):
    """More rings between the given ones, on a curve through them (Catmull-Rom)."""
    if len(rings) < 3:
        return rings

    out = []
    padded = [rings[0], *rings, rings[-1]]
    for i in range(1, len(padded) - 2):
        p0, p1, p2, p3 = padded[i - 1], padded[i], padded[i + 1], padded[i + 2]
        for s in range(per):
            u = s / per
            out.append(tuple(_catmull(p0[k], p1[k], p2[k], p3[k], u) for k in range(len(p1))))
    out.append(rings[-1])
    return out


def _catmull(a, b, c, d, u):
    if isinstance(a, (tuple, list, Vector)):
        return Vector(tuple(_catmull(a[i], b[i], c[i], d[i], u) for i in range(3)))
    return 0.5 * ((2 * b) + (-a + c) * u + (2 * a - 5 * b + 4 * c - d) * u * u + (-a + 3 * b - 3 * c + d) * u ** 3)


def loft(name, rings, mat, sides=16, per=2, across=(1, 0, 0)):
    """
    A tube through `rings` — (centre, half width, half height) — closed at both ends. The
    width lies along `across` (X unless told otherwise) where it can, the height across it,
    so a body's rings stand upright and a leg's face forward.
    """
    rings = smooth([(Vector(c), w, h) for c, w, h in rings], per)
    bm = bmesh.new()
    loops = []
    for i, (c, w, h) in enumerate(rings):
        a = rings[max(i - 1, 0)][0]
        b = rings[min(i + 1, len(rings) - 1)][0]
        along = (b - a).normalized()
        side = Vector(across).normalized()
        side = side - along * side.dot(along)
        if side.length < 1e-4:
            side = Vector((0, 1, 0)) - along * along.y
        side.normalize()
        up = along.cross(side).normalized()
        loop = []
        for k in range(sides):
            angle = 2 * math.pi * k / sides
            loop.append(bm.verts.new(c + side * math.cos(angle) * w + up * math.sin(angle) * h))
        loops.append(loop)

    for a, b in zip(loops, loops[1:]):
        for k in range(sides):
            bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k]))

    for loop, (c, _, _) in ((loops[0], rings[0]), (loops[-1], rings[-1])):
        centre = bm.verts.new(c)
        for k in range(sides):
            bm.faces.new((loop[k], loop[(k + 1) % sides], centre))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return t.mesh_object(name, bm, mat, smooth=True)


def blob(name, radii, mat, at, subdivisions=3):
    """An egg: a paw, an eye, a nose, a cheek."""
    return t.sphere(name, 1.0, mat, at=at, subdivisions=subdivisions, squash=radii, smooth=True)


def turned(obj, pivot, axis, degrees):
    """Turns a part's mesh about `pivot`, in place: a blob laid along a slope."""
    pivot = Vector(pivot)
    turn = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(degrees), 4, Vector(axis)) @ Matrix.Translation(-pivot)
    obj.data.transform(turn)
    return obj


def paint(obj, mat, where):
    """Gives `mat` to every face whose centre and normal satisfy where(centre, normal)."""
    if mat.name not in obj.data.materials:
        obj.data.materials.append(mat)
    index = list(obj.data.materials).index(mat)
    for poly in obj.data.polygons:
        if where(obj.matrix_world @ poly.center, obj.matrix_world.to_3x3() @ poly.normal):
            poly.material_index = index


def mirror(point):
    return Vector((-point[0], point[1], point[2]))


# --- Skeleton ----------------------------------------------------------------------------


class Rig:
    """Bones named, placed and parented, made into an armature by build()."""

    def __init__(self):
        self.bones = {}

    def add(self, name, head, tail, parent=None):
        self.bones[name] = (Vector(head), Vector(tail), parent)
        return name

    def chain(self, names, points, parent):
        for name, a, b in zip(names, points, points[1:]):
            parent = self.add(name, a, b, parent)
        return parent

    def build(self, name):
        data = bpy.data.armatures.new(name)
        arm = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(arm)
        bpy.context.view_layer.objects.active = arm
        arm.select_set(True)
        bpy.ops.object.mode_set(mode="EDIT")
        for bone, (head, tail, _) in self.bones.items():
            edit = data.edit_bones.new(bone)
            edit.head, edit.tail = head, tail
        for bone, (_, _, parent) in self.bones.items():
            if parent:
                data.edit_bones[bone].parent = data.edit_bones[parent]
        bpy.ops.object.mode_set(mode="OBJECT")
        for pose in arm.pose.bones:
            pose.rotation_mode = "QUATERNION"
        return arm

    def segment(self, bone):
        head, tail, _ = self.bones[bone]
        return head, tail


def weigh(obj, rig, bones):
    """Weights each vertex to the nearest of `bones`, shared where two meet."""
    for bone in bones:
        if bone not in obj.vertex_groups:
            obj.vertex_groups.new(name=bone)

    for v in obj.data.vertices:
        p = obj.matrix_world @ v.co
        near = []
        for bone in bones:
            a, b = rig.segment(bone)
            ab = b - a
            u = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
            d = (p - (a + ab * u)).length
            near.append((1.0 / max(d, 0.01) ** 4, bone))
        near.sort(reverse=True)
        near = near[:3]
        total = sum(w for w, _ in near)
        for w, bone in near:
            if w / total > 0.03:
                obj.vertex_groups[bone].add([v.index], w / total, "REPLACE")


def bind(name, arm, rig, parts, soften_at=75):
    """
    Joins the parts — (object, bones it lies along) — into one skinned mesh on the armature.
    """
    for obj, bones in parts:
        weigh(obj, rig, bones)
    body = t.join(name, [obj for obj, _ in parts])
    t.soften(body, angle=soften_at)
    body.parent = arm
    body.modifiers.new("Armature", "ARMATURE").object = arm
    return body


# --- Clips -------------------------------------------------------------------------------


def _quaternion(bone, pose):
    """A pose's turns about the world's X, Y and Z, as the bone's own rotation."""
    rest = bone.bone.matrix_local.to_3x3().inverted()
    q = Quaternion()
    for key, axis in (("rz", (0, 0, 1)), ("ry", (0, 1, 0)), ("rx", (1, 0, 0))):
        if pose.get(key):
            q = q @ Quaternion((rest @ Vector(axis)).normalized(), math.radians(pose[key]))
    return q


def clip(arm, name, frames, pose_at):
    """
    Keys the armature a frame at a time from pose_at(u), u from 0 to 1 over `frames`, and
    files the result as an action of its own. A pose maps a bone to its turns (rx, ry, rz, in
    degrees about the world's axes) and its shift (move, in metres along them).
    """
    arm.animation_data_create()
    action = bpy.data.actions.new(name)
    arm.animation_data.action = action

    for f in range(frames + 1):
        pose = pose_at(f / frames)
        for bone in arm.pose.bones:
            own = pose.get(bone.name, {})
            bone.rotation_quaternion = _quaternion(bone, own)
            move = Vector(own.get("move", (0, 0, 0)))
            bone.location = bone.bone.matrix_local.to_3x3().inverted() @ move
            bone.keyframe_insert("rotation_quaternion", frame=f)
            bone.keyframe_insert("location", frame=f)

    track = arm.animation_data.nla_tracks.new()
    track.name = name
    track.strips.new(name, 0, action)
    arm.animation_data.action = None
    action.use_fake_user = True
    return action


def ease(u):
    return u * u * (3 - 2 * u)


def keyed(keys):
    """
    A pose_at from key poses at given times: [(u, pose), ...], eased from each to the next.
    Channels a key leaves out are at rest.
    """
    def at(u):
        for (u0, a), (u1, b) in zip(keys, keys[1:]):
            if u0 <= u <= u1:
                return blend(a, b, ease((u - u0) / max(u1 - u0, 1e-6)))
        return keys[-1][1]
    return at


def blend(a, b, s):
    out = {}
    for bone in set(a) | set(b):
        pa, pb = a.get(bone, {}), b.get(bone, {})
        channels = {}
        for key in set(pa) | set(pb):
            if key == "move":
                va, vb = Vector(pa.get("move", (0, 0, 0))), Vector(pb.get("move", (0, 0, 0)))
                channels["move"] = tuple(va.lerp(vb, s))
            else:
                channels[key] = pa.get(key, 0.0) * (1 - s) + pb.get(key, 0.0) * s
        out[bone] = channels
    return out


def add(*poses):
    """Poses layered: turns and shifts summed."""
    out = {}
    for pose in poses:
        for bone, channels in pose.items():
            mine = out.setdefault(bone, {})
            for key, value in channels.items():
                if key == "move":
                    mine["move"] = tuple(Vector(mine.get("move", (0, 0, 0))) + Vector(value))
                else:
                    mine[key] = mine.get(key, 0.0) + value
    return out


# --- A four-legged body --------------------------------------------------------------------

LEGS = ("front.L", "front.R", "back.L", "back.R")


def leg_bones(leg):
    return [f"{leg}.upper", f"{leg}.lower", f"{leg}.paw"]


def quadruped_rig(spec):
    """
    The skeleton every four-legged creature shares: a body bone at the middle carrying the
    hips and the chest; the neck, head and jaw off the chest; the tail off the hips; a leg of
    three bones at each corner.
    """
    rig = Rig()
    mid = Vector(spec["mid"])
    rig.add("root", (0, mid.y, 0), (0, mid.y, 0.2 * mid.z))
    rig.add("body", mid + Vector((0, 0.03, 0)), mid - Vector((0, 0.03, 0)), "root")
    rig.add("hips", mid, spec["rump"], "body")
    rig.add("chest", mid, spec["withers"], "body")
    rig.add("neck", spec["withers"], spec["poll"], "chest")
    rig.add("head", spec["poll"], spec["nose"], "neck")
    rig.add("jaw", spec["jaw"][0], spec["jaw"][1], "head")
    rig.chain([f"tail{i + 1}" for i in range(len(spec["tail"]) - 1)], [Vector(p) for p in spec["tail"]], "hips")
    for leg in LEGS:
        side = 1 if leg.endswith(".L") else -1
        points = [Vector((p[0] * side, p[1], p[2])) for p in spec[leg[:-2]]]
        rig.chain(leg_bones(leg), points, "chest" if leg.startswith("front") else "hips")
    return rig


def quadruped_clips(arm, spec, style):
    """
    Idle, Run, Attack, HitReact and Death for a four-legged creature. `style` tunes them:
    stride (degrees a leg swings), run_frames (a stride, in frames), bob (metres the body
    rises), flex (degrees the back bends), bite (degrees the jaw opens), lunge (metres the
    body throws forward), tail (degrees the tail sways), side (how far the body lies from the
    ground once fallen).
    """
    tails = [b.name for b in arm.pose.bones if b.name.startswith("tail")]
    stride = style.get("stride", 34)
    flex = style.get("flex", 6)
    bob = style.get("bob", 0.04)
    tail = style.get("tail", 12)
    lunge = style.get("lunge", 0.2)
    bite = style.get("bite", 32)
    s = math.sin
    tau = 2 * math.pi

    def tail_wave(u, amount, key="rz", speed=1):
        return {b: {key: amount * s(tau * (u * speed - i * 0.12))} for i, b in enumerate(tails)}

    # Idle: breathing, a slow look round, the jaw working now and then. The tail is held still
    # and low, only rising and falling a little with the breath: swung side to side it read as
    # a dog wagging for its owner (2026-09-30, at your call).
    def idle(u):
        return add(
            {
                "chest": {"rx": 1.5 * s(tau * u)},
                "hips": {"rx": -0.8 * s(tau * u)},
                "neck": {"rx": -1.5 * s(tau * u), "rz": 6 * s(tau * u)},
                "head": {"rx": 3 * s(2 * tau * u), "rz": 4 * s(tau * u + 0.6)},
                "jaw": {"rx": 3 * max(0.0, s(2 * tau * u))},
            },
            {b: {"rx": 1.5 * s(tau * u - i * 0.3)} for i, b in enumerate(tails)},
        )

    # Run: a bound — the hind legs drive together, the fore legs reach together — with the
    # back flexing and the body rising between strides.
    def run(u):
        pose = {}
        for leg in LEGS:
            fore = leg.startswith("front")
            lag = 0.06 if leg.endswith(".R") else 0.0
            phase = tau * (u + lag + (0.5 if fore else 0.0))
            swing = stride * s(phase)
            fold = max(0.0, -math.cos(phase))
            upper, lower, paw = leg_bones(leg)
            if fore:
                pose[upper] = {"rx": swing}
                pose[lower] = {"rx": 55 * fold}
                pose[paw] = {"rx": 30 * fold - swing * 0.4}
            else:
                pose[upper] = {"rx": swing}
                pose[lower] = {"rx": -40 * fold}
                pose[paw] = {"rx": 25 * fold - swing * 0.3}
        pose["body"] = {"move": (0, 0, bob * s(tau * u + 1.2))}
        pose["hips"] = {"rx": flex * s(tau * u)}
        pose["chest"] = {"rx": -flex * s(tau * u)}
        pose["neck"] = {"rx": flex * 0.8 * s(tau * u) - 8}
        pose["head"] = {"rx": 4}
        return add(pose, tail_wave(u, tail * 0.8, "rx", 1))

    # Attack: gathers back, throws itself forward with the jaws open, snaps them shut.
    rest = {}
    gather = {
        "body": {"move": (0, lunge * 0.3, -0.03)},
        "hips": {"rx": 4}, "chest": {"rx": 6},
        "neck": {"rx": 10}, "head": {"rx": 6}, "jaw": {"rx": 8},
        "front.L.upper": {"rx": 10}, "front.R.upper": {"rx": 10},
        "back.L.upper": {"rx": -10}, "back.R.upper": {"rx": -10},
    }
    strike = {
        "body": {"move": (0, -lunge, 0.02)},
        "hips": {"rx": -3}, "chest": {"rx": -8},
        "neck": {"rx": -18}, "head": {"rx": -6}, "jaw": {"rx": bite},
        "front.L.upper": {"rx": -32}, "front.R.upper": {"rx": -28},
        "front.L.lower": {"rx": 20}, "front.R.lower": {"rx": 16},
        "back.L.upper": {"rx": 22}, "back.R.upper": {"rx": 20},
    }
    snap = {**strike, "jaw": {"rx": -2}, "head": {"rx": -10}}
    attack = keyed([(0, rest), (0.3, gather), (0.45, strike), (0.55, snap), (1.0, rest)])

    # HitReact: flinches back from the blow and settles.
    flinch = {
        "body": {"move": (0, 0.05 * spec["length"], 0)},
        "chest": {"rx": 8, "ry": 4}, "neck": {"rx": 14}, "head": {"rx": 8, "rz": 10},
        "jaw": {"rx": 14},
    }
    hurt = keyed([(0, rest), (0.3, flinch), (1.0, rest)])

    # Death: staggers, then rolls onto its side with its legs gone slack.
    side = style.get("side", 0.2)
    stagger = {
        "body": {"move": (0, 0, -0.05)},
        "neck": {"rx": 12}, "head": {"rx": 10}, "jaw": {"rx": 18},
        "front.L.upper": {"rx": -10}, "front.R.upper": {"rx": 8},
        "front.L.lower": {"rx": 30},
    }
    fallen = {
        "root": {"ry": 88, "move": (0, 0, side)},
        "neck": {"rx": 14, "rz": -8}, "head": {"rx": 16, "ry": -10}, "jaw": {"rx": 22},
        "front.L.upper": {"rx": -24}, "front.R.upper": {"rx": 10},
        "front.L.lower": {"rx": 36}, "front.R.lower": {"rx": 20},
        "back.L.upper": {"rx": 18}, "back.R.upper": {"rx": -8},
        "back.L.lower": {"rx": -20}, "back.R.lower": {"rx": -10},
        **{b: {"rz": 8 + 4 * i} for i, b in enumerate(tails)},
    }
    settled = {**fallen, "root": {"ry": 90, "move": (0, 0, side * 0.96)}}
    death = keyed([(0, rest), (0.25, stagger), (0.7, fallen), (0.82, {**fallen, "root": {"ry": 93, "move": (0, 0, side * 1.05)}}), (1.0, settled)])

    frames = style.get("run_frames", 14)
    clip(arm, "Idle", 48, idle)
    clip(arm, "Run", frames, run)
    clip(arm, "Attack", style.get("attack_frames", 18), attack)
    clip(arm, "HitReact", 10, hurt)
    clip(arm, "Death", 30, death)
    bpy.context.scene.render.fps = FPS
