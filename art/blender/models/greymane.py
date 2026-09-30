"""Grigiocrine — the old alpha: heavier than his pack, charcoal-coated, a great silver mane over
his shoulders, red eyes, a torn ear and scars across the muzzle."""

import math

from mathutils import Vector

import beast
import temins as t
import wolf

LOOKS = beast.Proportions(
    height=0.80, half_length=0.42, girth=0.20, depth=0.26, leg=1.12,
    neck=0.30, neck_angle=34, head=0.36, skull=0.125, muzzle=1.08, head_tilt=10,
    ear=0.12, ear_width=0.05, tail=0.46, tail_bush=0.095, tail_droop=38, haunch=1.0,
)

STYLE = {"stride": 30, "run_frames": 15, "bob": 0.05, "flex": 6, "lunge": 0.26, "bite": 38, "tail": 9, "side": 0.23}


def build():
    colours = wolf.coat(fur=(0.11, 0.11, 0.12), dark=(0.05, 0.05, 0.06), pale=(0.34, 0.33, 0.32),
                        eye=(1.0, 0.16, 0.08), eye_glow=1.6, prefix="greymane")
    silver = t.material("greymane_mane", (0.36, 0.36, 0.39), rough=0.85)
    made = beast.make("greymane", LOOKS, colours)
    along, S = made.anchors["along"], made.anchors["S"]
    withers, poll = made.anchors["withers"], made.anchors["poll"]

    # The mane: rows of long locks from the back of the skull down over the shoulders, swept
    # back and out, longest at the withers.
    for row in range(6):
        u = row / 5
        centre = poll.lerp(withers + Vector((0, 0.12, 0)), u)
        reach = 0.14 + 0.08 * (1 - abs(u - 0.6))
        for k in range(-2, 3):
            angle = k * 0.55 + (0.2 if row % 2 else 0.0)
            out = Vector((0.9 * math.sin(angle), 0.0, math.cos(angle)))
            root = centre + out * (LOOKS.girth * (0.45 + 0.25 * u))
            tip = root + out * reach * 0.22 + Vector((0, reach, -0.03 - 0.02 * abs(k)))
            made.add(beast.tuft(f"mane{row}{k}", root, tip, 0.075, silver, sides=8),
                     ["neck", "chest"] if u > 0.5 else ["neck", "head"])

    # Cheek ruff in silver, heavier than a pack wolf's.
    wolf.ruff(made, silver, count=4, length=1.3, thick=0.40)
    beast.fangs(made, colours["tooth"], S * 0.30, spread=0.30)

    # Three pale scars raked across the left of the muzzle.
    scar = t.material("greymane_scar", (0.62, 0.50, 0.48), rough=0.8)
    for i in range(3):
        a = along(0.55 + i * 0.08, 0.55, 0.58)
        b = along(0.63 + i * 0.08, -0.10, 0.62)
        made.add(beast.c.loft(f"scar{i}", [(a, S * 0.03, S * 0.03), (b, S * 0.03, S * 0.03)], scar, sides=6, per=1), ["head"])

    made.finish(STYLE)
    return {"folder": "creatures", "name": "greymane"}
