"""Grigiocrine — the old alpha: heavier than his pack, charcoal-coated, a great silver mane over
his shoulders, red eyes under a heavy brow, and scars raked across the muzzle."""

import math

from mathutils import Vector

import beast
import temins as t
import wolf

LOOKS = beast.Proportions(
    height=0.80, half_length=0.42, girth=0.20, depth=0.26, leg=1.12,
    neck=0.30, neck_angle=34, head=0.36, skull=0.125, muzzle=1.08, head_tilt=10,
    eye=1.05, eyes_aside=0.35, brow=1.4,
    ear=0.12, ear_width=0.05, tail=0.46, tail_bush=0.095, tail_droop=38, haunch=1.0,
)

STYLE = {"stride": 30, "run_frames": 15, "bob": 0.05, "flex": 6, "lunge": 0.26, "bite": 38, "tail": 9, "side": 0.23}


def build():
    colours = wolf.coat(fur=(0.11, 0.11, 0.12), dark=(0.05, 0.05, 0.06), pale=(0.34, 0.33, 0.32),
                        eye=(1.0, 0.16, 0.08), eye_glow=2.0, prefix="greymane")
    silver = t.material("greymane_mane", (0.23, 0.23, 0.25), rough=0.85)
    made = beast.make("greymane", LOOKS, colours)
    along, S = made.anchors["along"], made.anchors["S"]
    withers, poll = made.anchors["withers"], made.anchors["poll"]

    # The mane: a heavy collar of fur round the neck and over the shoulders, fuller than the
    # neck under it, ending in a fringe of locks that falls back over the withers.
    end = withers + Vector((0, 0.16, 0.0))
    collar = beast.c.loft("mane", [
        (poll + Vector((0, 0.02, -0.02)), 0.09, 0.09),
        (poll.lerp(withers, 0.30) + Vector((0, 0, 0.01)), 0.155, 0.155),
        (poll.lerp(withers, 0.65) + Vector((0, 0, 0.02)), 0.19, 0.185),
        (withers + Vector((0, 0.04, 0.02)), 0.185, 0.17),
        (end, 0.10, 0.09),
    ], silver, sides=22)
    made.add(collar, ["neck", "chest"])
    for row, (u, reach) in enumerate(((0.55, 0.12), (0.95, 0.18))):
        centre = poll.lerp(withers, u) + Vector((0, 0, 0.03))
        for k in range(-4, 5):
            angle = k * 0.36 + (0.18 if row else 0.0)
            out = Vector((math.sin(angle), 0.0, math.cos(angle)))
            root = centre + out * 0.14
            tip = root + out * reach * 0.35 + Vector((0, reach, -0.05 - 0.015 * abs(k)))
            made.add(wolf.lock(f"mane{row}{k}", root, tip, 0.075, silver, out.cross(Vector((0, 1, 0)))), ["neck", "chest"])

    wolf.ruff(made, silver, count=4, length=1.25, thick=0.40)
    beast.fangs(made, colours["tooth"], S * 0.30, spread=0.28)

    # Three scars raked down the left of the muzzle: long thin weals laid on the fur.
    scar = t.material("greymane_scar", (0.55, 0.36, 0.34), rough=0.8)
    for i in range(3):
        centre = along(0.55 + i * 0.09, 0.05, 0.60 - i * 0.07)
        weal = beast.c.blob(f"scar{i}", (S * 0.05, S * 0.035, S * 0.30), scar, centre)
        beast.c.turned(weal, centre, (1, 0, 0), 28)
        made.add(weal, ["head"])

    made.finish(STYLE)
    return {"folder": "creatures", "name": "greymane"}
