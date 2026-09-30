"""Lupo Grigio — a lean grey wolf, head low, hackles up and lips drawn back off its teeth: a darker
saddle, a pale throat, a ruff swept back from the cheeks, eyes glaring amber under a scowl."""

from mathutils import Vector

import beast
import temins as t

LOOKS = beast.Proportions(
    neck_angle=52, head_tilt=9, brow=1.2,
    snarl=0.8, lid=0.6, ears_back=0.35, hackles=7,
)

STYLE = {"stride": 34, "run_frames": 14, "bob": 0.05, "flex": 7, "lunge": 0.22, "bite": 34, "tail": 12, "side": 0.19}


def coat(fur=(0.21, 0.21, 0.23), dark=(0.10, 0.10, 0.11), pale=(0.50, 0.48, 0.45), eye=(1.0, 0.60, 0.10), eye_glow=1.3, prefix="wolf"):
    return {
        "fur": t.material(f"{prefix}_fur", fur, rough=0.9),
        "dark": t.material(f"{prefix}_saddle", dark, rough=0.9),
        "pale": t.material(f"{prefix}_throat", pale, rough=0.9),
        "nose": t.material("beast_nose", (0.04, 0.04, 0.05), rough=0.3),
        "lip": t.material("beast_lip", (0.04, 0.03, 0.03), rough=0.5),
        "mouth": t.material("beast_mouth", (0.32, 0.07, 0.08), rough=0.5),
        "tooth": t.material("tooth", (0.92, 0.89, 0.80), rough=0.4),
        "eye": t.material(f"{prefix}_eye", eye, rough=0.2, glow=eye_glow),
        "paw": t.material(f"{prefix}_paw", tuple(x * 0.8 for x in fur), rough=0.9),
        "pad": t.material(f"{prefix}_toe", tuple(x * 0.65 for x in fur), rough=0.9),
        "inner": t.material(f"{prefix}_inner", tuple(min(1.0, x * 1.25) for x in pale), rough=0.9),
    }


def lock(name, root, tip, width, mat, around):
    """A flat lock of fur lying along the body: wide across `around`, thin through it."""
    return beast.tuft(name, root, tip, width, mat, sides=8, flat=0.32, across=around)


def ruff(made, mat, count=4, length=1.0, thick=0.34):
    """Flat locks of fur swept back from the cheeks, down the sides of the neck."""
    along, S, up = made.anchors["along"], made.anchors["S"], made.anchors["up"]
    for side in (1, -1):
        for i in range(count):
            root = along(0.22 + 0.03 * i, 0.10 - 0.26 * i, 0.78 * side)
            tip = root + Vector((0.30 * S * side, 1.35 * S, -0.40 * S)) * length
            made.add(lock(f"ruff{side}{i}", root, tip, S * thick, mat, up), ["neck", "head"])


def build():
    colours = coat()
    made = beast.make("wolf", LOOKS, colours)
    ruff(made, colours["fur"], length=0.8)
    beast.fangs(made, colours["tooth"], made.anchors["S"] * 0.28)
    made.finish(STYLE)
    return {"folder": "creatures", "name": "wolf"}
