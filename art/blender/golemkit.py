"""Act 2's golem (2026-10-03): a body of stones of ours, each carried whole by one bone, and the clips that move
it — keyed here by *aiming* each limb: a bone is told which way to point in the model's space (+X its left, -Y its
front, +Z up) and the rotation that does it is worked out through its parents. A stone body moves stiffly, so a
handful of key poses eased into one another is all it needs.

`skeleton()` makes the bones (named as Blender's human metarig names them), `body(arm, ...)` lays the stones on
them, `animate(arm)` keys the clips the game looks for (Idle, Walk, Run, Attack, Spellcasting, HitReact, Death),
and `grow(arm, ...)` weighs any further piece to one bone.
"""

import math

import bpy
from mathutils import Quaternion, Vector

from creaturekit import Rig
import temins as t


def skeleton(name="golem"):
    """A hunched thing three metres tall: hips at 1.25, a chest that leans over them, the head low and forward
    between the shoulders, arms long enough that the fists hang by the knees."""
    rig = Rig()
    rig.add("spine", (0, 0, 1.25), (0, 0, 1.45))
    rig.chain(["spine.001", "spine.002", "spine.003", "spine.004", "spine.005", "spine.006"],
              [(0, 0, 1.45), (0, 0, 1.68), (0, 0.04, 1.95), (0, 0.12, 2.55), (0, -0.04, 2.62), (0, -0.2, 2.6), (0, -0.4, 2.95)], "spine")

    for side, s in (("L", 1), ("R", -1)):
        rig.add(f"shoulder.{side}", (0.1 * s, 0.08, 2.45), (0.85 * s, 0.08, 2.48), "spine.003")
        rig.chain([f"upper_arm.{side}", f"forearm.{side}", f"hand.{side}"],
                  [(0.85 * s, 0.08, 2.48), (1.02 * s, -0.02, 1.82), (1.08 * s, -0.16, 1.18), (1.1 * s, -0.24, 0.78)], f"shoulder.{side}")
        rig.chain([f"thigh.{side}", f"shin.{side}", f"foot.{side}"],
                  [(0.4 * s, 0.0, 1.25), (0.48 * s, -0.06, 0.68), (0.52 * s, 0.02, 0.14), (0.54 * s, -0.3, 0.05)], "spine")

    return rig.build(name)


def body(arm, stone, dark, eyes):
    """The stones: a great boulder of a chest and a hump behind it, a small head sunk forward, heavy fists."""
    rock = t.rock

    def side(s):
        return "L" if s > 0 else "R"

    grow(arm, rock("pelvis", 0.42, dark, at=(0, 0.02, 1.22), seed=11, squash=(1.5, 1.05, 0.75)), "spine")
    grow(arm, rock("belly", 0.42, stone, at=(0, -0.02, 1.62), seed=12, squash=(1.25, 1.0, 0.85)), "spine.002")
    grow(arm, rock("chest", 0.62, stone, at=(0, 0.04, 2.08), seed=13, squash=(1.5, 1.0, 0.92)), "spine.003")
    grow(arm, rock("hump", 0.5, dark, at=(0, 0.4, 2.55), seed=14, squash=(1.35, 0.9, 0.8)), "spine.003")
    grow(arm, rock("head", 0.3, stone, at=(0, -0.46, 2.8), seed=15, squash=(1.05, 1.0, 0.9)), "spine.006")
    grow(arm, rock("brow", 0.2, dark, at=(0, -0.62, 2.9), seed=16, squash=(1.6, 0.7, 0.45)), "spine.006")

    for s in (1, -1):
        grow(arm, t.sphere(f"eye{s}", 0.065, eyes, at=(0.12 * s, -0.74, 2.8), subdivisions=2, squash=(1.3, 0.6, 0.8)), "spine.006")
        grow(arm, rock(f"shoulder{s}", 0.46, dark, at=(0.88 * s, 0.06, 2.5), seed=20 + s, squash=(1.1, 1.05, 0.9)), f"upper_arm.{side(s)}")
        grow(arm, rock(f"upper{s}", 0.3, stone, at=(0.98 * s, 0.02, 2.0), seed=22 + s, squash=(1.0, 1.0, 1.25)), f"upper_arm.{side(s)}")
        grow(arm, rock(f"fore{s}", 0.36, stone, at=(1.06 * s, -0.1, 1.45), seed=24 + s, squash=(1.0, 1.0, 1.3)), f"forearm.{side(s)}")
        grow(arm, rock(f"fist{s}", 0.42, dark, at=(1.1 * s, -0.22, 0.88), seed=26 + s, squash=(1.05, 1.1, 1.0)), f"hand.{side(s)}")
        grow(arm, rock(f"thigh{s}", 0.32, stone, at=(0.45 * s, -0.03, 0.95), seed=28 + s, squash=(1.0, 1.0, 1.2)), f"thigh.{side(s)}")
        grow(arm, rock(f"shin{s}", 0.3, stone, at=(0.5 * s, -0.02, 0.42), seed=30 + s, squash=(1.0, 1.0, 1.25)), f"shin.{side(s)}")
        grow(arm, rock(f"foot{s}", 0.28, dark, at=(0.53 * s, -0.16, 0.1), seed=32 + s, squash=(1.05, 1.5, 0.5)), f"foot.{side(s)}")


def grow(arm, obj, bone):
    """A piece carried by one bone: skinned to it whole."""
    group = obj.vertex_groups.new(name=bone)
    group.add([v.index for v in obj.data.vertices], 1.0, "REPLACE")
    obj.parent = arm
    obj.modifiers.new("Armature", "ARMATURE").object = arm
    return obj


# --- Posing by aim -----------------------------------------------------------------------


def _turn(degrees):
    rx, ry, rz = degrees
    return (Quaternion((0, 0, 1), math.radians(rz)) @ Quaternion((0, 1, 0), math.radians(ry))
            @ Quaternion((1, 0, 0), math.radians(rx)))


def _depth(bone):
    return len(bone.parent_recursive)


def apply(arm, pose):
    """
    Sets every bone from a pose: {bone: {"aim": (x, y, z), "turn": (rx, ry, rz), "move": (x, y, z)}}. An aim points the
    bone that way; a turn (degrees about the model's axes) is laid over whatever it inherits; a move shifts the root.
    A bone the pose leaves out goes where its parent takes it.
    """
    delta = {}

    for bone in sorted(arm.data.bones, key=_depth):
        spec = pose.get(bone.name, {})
        inherited = delta[bone.parent.name] if bone.parent else Quaternion()
        rest = bone.matrix_local.to_quaternion()
        mine = inherited

        if "aim" in spec:
            now = inherited @ (bone.tail_local - bone.head_local).normalized()
            mine = now.rotation_difference(Vector(spec["aim"]).normalized()) @ inherited

        if "turn" in spec:
            mine = _turn(spec["turn"]) @ mine

        delta[bone.name] = mine
        posed = arm.pose.bones[bone.name]
        posed.rotation_mode = "QUATERNION"
        posed.rotation_quaternion = rest.inverted() @ inherited.inverted() @ mine @ rest
        posed.location = rest.inverted() @ Vector(spec.get("move", (0, 0, 0)))


def clip(arm, name, frames, pose_at):
    """Keys pose_at(u), u from 0 to 1 over `frames`, as an action of its own on a track of its own."""
    arm.animation_data_create()
    action = bpy.data.actions.new(name)
    arm.animation_data.action = action
    last = {}

    for f in range(frames + 1):
        apply(arm, pose_at(f / frames))

        for bone in arm.pose.bones:
            # One way round: a quaternion and its negative are the same turn, but not to the interpolation.
            if bone.name in last and last[bone.name].dot(bone.rotation_quaternion) < 0:
                bone.rotation_quaternion = -bone.rotation_quaternion
            last[bone.name] = bone.rotation_quaternion.copy()
            bone.keyframe_insert("rotation_quaternion", frame=f)
            bone.keyframe_insert("location", frame=f)

    track = arm.animation_data.nla_tracks.new()
    track.name = name
    track.strips.new(name, 0, action)
    arm.animation_data.action = None
    action.use_fake_user = True
    return action


def _ease(u):
    return u * u * (3 - 2 * u)


def _mix(a, b, s):
    out = {}
    for bone in set(a) | set(b):
        pa, pb = a.get(bone, {}), b.get(bone, {})
        mixed = {}
        for key in set(pa) | set(pb):
            # A turn or a move left out is none; an aim left out is the other pose's, so nothing snaps.
            zero = pb.get(key) if key == "aim" else (0, 0, 0)
            va = Vector(pa.get(key, zero))
            vb = Vector(pb.get(key, pa.get(key) if key == "aim" else (0, 0, 0)))
            mixed[key] = tuple(va.lerp(vb, s))
        out[bone] = mixed
    return out


def keyed(keys):
    """A pose_at from key poses [(u, pose), ...], eased from each to the next."""
    def at(u):
        for (u0, a), (u1, b) in zip(keys, keys[1:]):
            if u0 <= u <= u1:
                return _mix(a, b, _ease((u - u0) / max(u1 - u0, 1e-6)))
        return keys[-1][1]
    return at


def merge(*poses):
    """Poses laid over one another, the later winning bone by bone and channel by channel."""
    out = {}
    for pose in poses:
        for bone, channels in pose.items():
            out.setdefault(bone, {}).update(channels)
    return out


def _both(left, aim):
    """A limb's aim for each side: given for the left (+X), mirrored for the right."""
    x, y, z = aim
    return {f"{left}.L": {"aim": (x, y, z)}, f"{left}.R": {"aim": (-x, y, z)}}


def arms(upper, fore, hand=None, right=None):
    """Both arms, the right mirroring the left unless it is given its own (upper, fore, hand)."""
    pose = merge(_both("upper_arm", upper), _both("forearm", fore), _both("hand", hand or fore))
    if right:
        u, f, h = right
        pose.update({"upper_arm.R": {"aim": u}, "forearm.R": {"aim": f}, "hand.R": {"aim": h or f}})
    return pose


# Standing: arms hanging heavy and a little forward, as a thing of stone holds them; legs as they were made.
LEGS = merge(_both("thigh", (0.36, -0.10, -1)), _both("shin", (0.23, 0.11, -1)))
STAND = merge(arms((0.32, -0.06, -1), (0.14, -0.28, -1), (0.10, -0.22, -1)), LEGS)


def _legs(swing, lift, side=1.0):
    """One leg's aims for a stride: swing > 0 forward, lift bending the knee back."""
    return {"aim": (0.30 * side, -swing, -1)}, {"aim": (0.22 * side, -swing * 0.5 + lift, -1)}


def stride(stretch, bob, lean, arm_swing):
    """A walk or a run: a full cycle of both legs, arms against them, the body rolling and dipping with each step."""
    def at(u):
        p = u * math.tau
        pose = {}
        for side, name, phase in ((1.0, "L", 0.0), (-1.0, "R", math.pi)):
            swing = stretch * math.sin(p + phase)
            lift = 0.6 * max(0.0, math.cos(p + phase)) * stretch / 0.42
            thigh, shin = _legs(swing, lift, side)
            pose[f"thigh.{name}"], pose[f"shin.{name}"] = thigh, shin
            back = arm_swing * math.sin(p + phase)
            pose[f"upper_arm.{name}"] = {"aim": (0.32 * side, -0.06 + back, -1)}
            pose[f"forearm.{name}"] = {"aim": (0.14 * side, -0.28 + back * 0.6, -1)}
            pose[f"hand.{name}"] = {"aim": (0.10 * side, -0.22 + back * 0.6, -1)}
        pose["spine"] = {"move": (0, 0, -bob * (1 - math.cos(2 * p)) / 2), "turn": (0, 3 * math.sin(p), 0)}
        pose["spine.001"] = {"turn": (lean, 0, 0)}
        pose["spine.002"] = {"turn": (0, 0, 6 * math.sin(p))}
        return pose
    return at


def idle(u):
    p = u * math.tau
    return merge(arms((0.32 + 0.03 * math.sin(p), -0.06, -1), (0.14, -0.28 - 0.04 * math.sin(p), -1), (0.10, -0.22, -1)),
                 {"spine": {"move": (0, 0, -0.02 * (1 - math.cos(p)))}, "spine.003": {"turn": (2 * math.sin(p), 0, 0)}})


RAISED = merge(arms((0.35, 0.18, 1), (0.12, 0.40, 1), (0.08, 0.45, 1)),
               {"spine.001": {"turn": (-10, 0, 0)}, "spine.003": {"turn": (-6, 0, 0)}})

SLAMMED = merge(arms((0.25, -1, -0.45), (0.10, -0.55, -1), (0.06, -0.30, -1)),
                {"spine": {"move": (0, 0, -0.14)}, "spine.001": {"turn": (22, 0, 0)}, "spine.003": {"turn": (6, 0, 0)},
                 "thigh.L": {"aim": (0.30, -0.25, -1)}, "thigh.R": {"aim": (-0.30, -0.25, -1)},
                 "shin.L": {"aim": (0.22, 0.20, -1)}, "shin.R": {"aim": (-0.22, 0.20, -1)}})

CALLING = merge(arms((0.75, -0.25, 1), (0.35, -0.35, 1), (0.25, -0.30, 1)),
                {"spine.001": {"turn": (-8, 0, 0)}, "spine.003": {"turn": (-6, 0, 0)}})

STRUCK = merge(arms((0.40, 0.25, -1), (0.20, 0.0, -1), (0.12, 0.05, -1)),
               {"spine.001": {"turn": (-14, 0, 0)}, "spine.003": {"turn": (-8, 0, 0)}})

STAGGER = merge(arms((0.45, 0.10, -1), (0.25, -0.10, -1)),
                {"spine": {"move": (0, 0, -0.22)}, "spine.003": {"turn": (-12, 0, 0)},
                 "thigh.L": {"aim": (0.30, -0.55, -1)}, "thigh.R": {"aim": (-0.30, -0.45, -1)},
                 "shin.L": {"aim": (0.22, 0.35, -1)}, "shin.R": {"aim": (-0.22, 0.30, -1)}})

# Toppled on its face like a statue off its plinth: turned about the hips, laid on the ground, arms flung out.
FALLEN = merge(arms((0.8, -0.55, -0.05), (0.6, -0.8, -0.1), (0.5, -0.8, -0.15)),
               {"spine": {"turn": (88, 0, 0), "move": (0, -0.05, -0.82)},
                # The legs go over with the body: where the standing aims land once turned 88 degrees forward.
                "thigh.L": {"aim": (0.36, 1.0, -0.13)}, "thigh.R": {"aim": (-0.36, 1.0, -0.13)},
                "shin.L": {"aim": (0.23, 1.0, 0.07)}, "shin.R": {"aim": (-0.23, 1.0, 0.07)}})


def animate(arm):
    clip(arm, "Idle", 48, idle)
    clip(arm, "Walk", 36, stride(0.40, 0.05, 4, 0.30))
    clip(arm, "Run", 24, stride(0.62, 0.10, 12, 0.55))
    clip(arm, "Attack", 30, keyed([(0.0, STAND), (0.40, RAISED), (0.55, SLAMMED), (0.78, SLAMMED), (1.0, STAND)]))
    clip(arm, "Spellcasting", 36, lambda u: merge(CALLING, {"spine.002": {"turn": (0, 0, 2 * math.sin(u * math.tau * 3))}}))
    clip(arm, "HitReact", 18, keyed([(0.0, STAND), (0.3, STRUCK), (1.0, STAND)]))
    clip(arm, "Death", 44, keyed([(0.0, STAND), (0.25, STAGGER), (0.8, FALLEN), (0.9, merge(FALLEN, {"spine": {"turn": (84, 0, 0), "move": (0, -0.05, -0.76)}})), (1.0, FALLEN)]))
