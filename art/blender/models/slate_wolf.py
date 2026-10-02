"""Lupo d'Ardesia — act 2, the Stonewood (A2-01): a wolf the wood turned with it. Heavier than the grey wolf,
its coat the colour of slate, plates of split stone standing along its back, crystal grown through at the
shoulders, and the same crystal where its eyes were."""

from mathutils import Vector

import beast
import temins as t
import wolf

LOOKS = beast.Proportions(
    height=0.80, half_length=0.43, girth=0.20, depth=0.25, leg=1.15,
    neck_angle=54, head_tilt=10, head=0.36, skull=0.12, brow=1.5,
    snarl=0.9, lid=0.7, ears_back=0.5, hackles=0, tail_droop=55, tail_bush=0.07,
)

STYLE = {"stride": 30, "run_frames": 15, "bob": 0.04, "flex": 6, "lunge": 0.24, "bite": 36, "tail": 6, "side": 0.21}


def stone_parts(made, slab, crystal, plates=6, spikes=3):
    """Slabs of stone standing along the spine, rooted in the body, and crystal through each shoulder."""
    withers, rump, S = made.anchors["withers"], made.spec["rump"], made.anchors["S"]

    for i in range(plates):
        u = 0.02 + i / (plates - 1) * 0.86
        centre = withers.lerp(rump, u)
        tall = (0.30 - 0.16 * abs(u - 0.35)) * (1.0 if i % 2 == 0 else 0.72)
        root = centre + Vector((0, 0, -0.04))
        tip = centre + Vector((0, 0.07 + 0.04 * u, 0.10 + tall))
        made.add(beast.tuft(f"slab{i}", root, tip, S * 1.25, slab, sides=6, flat=0.34, across=(1, 0, 0)), ["chest", "hips"])

    for side in (1, -1):
        for i in range(spikes):
            root = withers + Vector((side * 0.11, 0.03 + 0.07 * i, -0.02))
            tip = root + Vector((side * (0.20 - 0.04 * i), 0.05, 0.22 - 0.05 * i))
            made.add(beast.tuft(f"crystal{side}{i}", root, tip, S * 0.42, crystal, sides=6, flat=1.0), ["chest"])


def build():
    colours = wolf.coat(fur=(0.17, 0.18, 0.20), dark=(0.07, 0.075, 0.085), pale=(0.36, 0.38, 0.40),
                        eye=(0.25, 0.95, 0.85), eye_glow=2.0, prefix="slate_wolf")
    slab = t.material("slate_slab", (0.11, 0.12, 0.13), rough=0.75)
    crystal = t.material("stone_crystal", (0.25, 0.90, 0.82), glow=1.2, rough=0.15)

    made = beast.make("slate_wolf", LOOKS, colours)
    stone_parts(made, slab, crystal)
    wolf.ruff(made, slab, count=3, length=0.7, thick=0.30)
    beast.fangs(made, colours["tooth"], made.anchors["S"] * 0.32, spread=0.27)
    made.finish(STYLE)

    return {"folder": "creatures", "name": "slate_wolf"}
