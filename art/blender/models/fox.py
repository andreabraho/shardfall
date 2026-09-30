"""Volpe Rossa — slight and low, a narrow muzzle, tall ears, black socks and a white-tipped brush."""

import beast
import temins as t

LOOKS = beast.Proportions(
    height=0.46, half_length=0.29, girth=0.115, depth=0.135, leg=0.85,
    neck=0.20, neck_angle=40, head=0.27, skull=0.085, muzzle=0.78, head_tilt=4,
    eye=1.1, eyes_aside=0.4, brow=0.6,
    ear=0.13, ear_width=0.050, tail=0.50, tail_bush=0.09, tail_droop=22, haunch=1.0,
)

STYLE = {"stride": 36, "run_frames": 12, "bob": 0.035, "flex": 8, "lunge": 0.16, "bite": 34, "tail": 16, "side": 0.13}


def build():
    fur = t.material("fox_fur", (0.80, 0.34, 0.09), rough=0.9)
    white = t.material("fox_white", (0.93, 0.89, 0.82), rough=0.9)
    black = t.material("fox_black", (0.09, 0.07, 0.07), rough=0.9)
    colours = {
        "fur": fur, "pale": white, "paw": black, "pad": black, "inner": white, "lip": black,
        "nose": t.material("beast_nose", (0.04, 0.04, 0.05), rough=0.3),
        "mouth": t.material("beast_mouth", (0.32, 0.07, 0.08), rough=0.5),
        "tooth": t.material("tooth", (0.92, 0.89, 0.80), rough=0.4),
        "eye": t.material("fox_eye", (0.98, 0.72, 0.18), rough=0.2, glow=0.35),
    }
    made = beast.make("fox", LOOKS, colours)
    H = LOOKS.height

    # Black stockings up the legs, the backs of the ears black, a white tip to the brush.
    for obj, _ in made.parts:
        if obj.name.startswith(("front", "back")):
            beast.c.paint(obj, black, lambda q, n: q.z < H * 0.42)
        if obj.name.startswith("ear") and not obj.name.startswith("ear_in"):
            beast.c.paint(obj, black, lambda q, n: True)
    tip = made.anchors["tail"][-2]
    beast.c.paint(made.anchors["tail_obj"], white, lambda q, n: q.y > tip.y - 0.02)
    # White cheeks, running back from the muzzle.
    beast.c.paint(made.anchors["head"], white, lambda q, n: abs(n.x) > 0.45 and made.anchors["lift_of"](q) < 0.1)

    beast.whiskers(made, black, count=2, length=1.3)
    made.finish(STYLE)
    return {"folder": "creatures", "name": "fox"}
