"""Lupo Grigio — a lean grey wolf: a darker saddle, a pale throat, a ruff swept back from the cheeks."""

from mathutils import Vector

import beast
import temins as t

LOOKS = beast.Proportions()

STYLE = {"stride": 34, "run_frames": 14, "bob": 0.05, "flex": 7, "lunge": 0.22, "bite": 34, "tail": 12, "side": 0.19}


def coat(fur=(0.21, 0.21, 0.23), dark=(0.10, 0.10, 0.11), pale=(0.50, 0.48, 0.45), eye=(0.95, 0.62, 0.14), eye_glow=0.4, prefix="wolf"):
    return {
        "fur": t.material(f"{prefix}_fur", fur, rough=0.9),
        "dark": t.material(f"{prefix}_saddle", dark, rough=0.9),
        "pale": t.material(f"{prefix}_throat", pale, rough=0.9),
        "nose": t.material("beast_nose", (0.04, 0.04, 0.05), rough=0.35),
        "mouth": t.material("beast_mouth", (0.35, 0.08, 0.08), rough=0.5),
        "eye": t.material(f"{prefix}_eye", eye, rough=0.2, glow=eye_glow),
        "pupil": t.material("beast_pupil", (0.02, 0.02, 0.02), rough=0.2),
        "paw": t.material(f"{prefix}_paw", tuple(x * 0.7 for x in fur), rough=0.9),
        "inner": t.material(f"{prefix}_inner", (0.12, 0.10, 0.11), rough=0.9),
        "tooth": t.material("tooth", (0.92, 0.89, 0.80), rough=0.4),
    }


def ruff(made, mat, count=4, length=1.0, thick=0.34):
    """Locks of fur swept back from the cheeks, down the sides of the neck."""
    along, S = made.anchors["along"], made.anchors["S"]
    for side in (1, -1):
        for i in range(count):
            root = along(0.20 + 0.04 * i, 0.15 - 0.32 * i, 0.80 * side)
            tip = root + Vector((0.22 * S * side, 1.4 * S, -0.45 * S)) * length
            made.add(beast.tuft(f"ruff{side}{i}", root, tip, S * thick, mat, sides=8), ["neck", "head"])


def build():
    colours = coat()
    made = beast.make("wolf", LOOKS, colours)
    ruff(made, colours["fur"], length=0.8)
    beast.fangs(made, colours["tooth"], made.anchors["S"] * 0.22)
    made.finish(STYLE)
    return {"folder": "creatures", "name": "wolf"}
