"""Mastino di Brace — act 2, the Cinderlands (A2-01): a lean hound burnt black, the fire it runs on showing
wherever the hide has split — along the spine, in the mouth, at the end of the tail, in the eyes."""

from mathutils import Vector

import beast
import temins as t
import wolf

LOOKS = beast.Proportions(
    height=0.70, half_length=0.40, girth=0.15, depth=0.20, leg=0.92,
    neck_angle=56, head_tilt=11, head=0.35, skull=0.10, muzzle=0.92, brow=1.4,
    snarl=1.0, lid=0.7, ears_back=0.6, hackles=0, ear=0.15, tail=0.42, tail_bush=0.045, tail_droop=20, haunch=0.9,
)

STYLE = {"stride": 38, "run_frames": 12, "bob": 0.05, "flex": 8, "lunge": 0.26, "bite": 36, "tail": 10, "side": 0.17}


def build():
    colours = wolf.coat(fur=(0.045, 0.035, 0.035), dark=(0.02, 0.015, 0.015), pale=(0.16, 0.07, 0.04),
                        eye=(1.0, 0.45, 0.08), eye_glow=2.4, prefix="ember_hound")
    fire = t.material("hound_fire", (1.0, 0.36, 0.06), glow=1.6, rough=0.4)
    heat = t.material("hound_heat", (1.0, 0.70, 0.25), glow=2.0, rough=0.3)

    # The inside of the mouth burns.
    colours["mouth"] = fire

    made = beast.make("ember_hound", LOOKS, colours)
    withers, rump, S = made.anchors["withers"], made.spec["rump"], made.anchors["S"]

    # Flame standing along the spine where the hide has split: tall at the shoulders, guttering toward the rump.
    for i in range(8):
        u = i / 7 * 0.9
        centre = withers.lerp(rump, u)
        tall = 0.20 - 0.14 * u + (0.03 if i % 2 else 0.0)
        root = centre + Vector((0, 0, -0.03))
        tip = centre + Vector((0, 0.09, 0.07 + tall))
        made.add(beast.tuft(f"flame{i}", root, tip, S * 0.55, fire if i % 3 else heat, sides=6, flat=0.45, across=(1, 0, 0)), ["chest", "hips"])

    # Cracks of fire down each flank.
    for side in (1, -1):
        for i in range(3):
            centre = withers.lerp(rump, 0.25 + 0.25 * i) + Vector((side * LOOKS.girth * 0.92, 0, -0.03))
            made.add(beast.tuft(f"crack{side}{i}", centre + Vector((0, -0.05, 0.06)), centre + Vector((side * 0.01, 0.07, -0.10)), S * 0.22, fire,
                                sides=5, flat=0.3, across=(0, 1, 0)), ["chest", "hips"])

    # The end of the tail is alight.
    tail = made.anchors["tail"]
    made.add(beast.tuft("tail_flame", tail[-2], tail[-1] + (tail[-1] - tail[-2]) * 1.4 + Vector((0, 0, 0.06)), S * 0.7, heat, sides=6, flat=0.8), ["hips"])

    beast.fangs(made, colours["tooth"], S * 0.30, spread=0.26)
    made.finish(STYLE)

    return {"folder": "creatures", "name": "ember_hound"}
