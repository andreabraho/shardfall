"""Ratto dei Campi — a rat the size of a dog: hunched, round-eared, yellow teeth, a long bare tail."""

from mathutils import Vector

import beast
import temins as t

LOOKS = beast.Proportions(
    height=0.32, half_length=0.24, girth=0.14, depth=0.15, leg=0.62,
    neck=0.08, neck_angle=62, head=0.23, skull=0.088, muzzle=0.72, head_tilt=14,
    ear=0.065, ear_width=0.055, ear_round=True,
    tail=0.62, tail_bush=0.026, tail_droop=6, tail_thin=True, haunch=1.2,
)

STYLE = {"stride": 42, "run_frames": 8, "bob": 0.03, "flex": 11, "lunge": 0.12, "bite": 40, "tail": 20, "side": 0.14}


def build():
    fur = t.material("rat_fur", (0.33, 0.27, 0.22), rough=0.95)
    pink = t.material("rat_skin", (0.80, 0.52, 0.50), rough=0.7)
    colours = {
        "fur": fur,
        "pale": t.material("rat_belly", (0.58, 0.50, 0.42), rough=0.95),
        "paw": pink, "inner": pink,
        "nose": t.material("rat_nose", (0.72, 0.40, 0.42), rough=0.5),
        "mouth": t.material("beast_mouth", (0.35, 0.08, 0.08), rough=0.5),
        "eye": t.material("rat_eye", (0.05, 0.02, 0.02), rough=0.1),
    }
    made = beast.make("field_rat", LOOKS, colours)
    along, S, up = made.anchors["along"], made.anchors["S"], made.anchors["up"]

    # Two long front teeth, a shade of old ivory.
    teeth = t.material("rat_teeth", (0.90, 0.80, 0.50), rough=0.4)
    for side in (1, -1):
        root = along(0.97, -0.25, 0.10 * side)
        tooth = beast.c.loft(f"tooth{side}", [(root, S * 0.07, S * 0.04), (root - up * S * 0.35, S * 0.06, S * 0.035), (root - up * S * 0.42 + Vector((0, S * 0.03, 0)), S * 0.01, S * 0.01)], teeth, sides=6, per=1)
        made.add(tooth, ["head"])

    # A bristle of fur along the hunched back.
    for i in range(5):
        y = -0.10 + i * 0.07
        root = Vector((0, y, LOOKS.height + 0.005 - abs(y) * 0.12))
        made.add(beast.tuft(f"bristle{i}", root, root + Vector((0, 0.07, 0.04)), 0.022, t.material("rat_bristle", (0.24, 0.20, 0.16), rough=0.95), sides=6), ["hips" if y > 0 else "chest"])

    beast.whiskers(made, t.material("rat_whisker", (0.75, 0.70, 0.62), rough=0.6), count=3, length=1.8)
    made.finish(STYLE)
    return {"folder": "creatures", "name": "field_rat"}
