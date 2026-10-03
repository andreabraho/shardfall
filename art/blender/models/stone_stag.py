"""Il Cervo di Pietra — the Stonewood's boss (A2-01): a great stag the wood turned and did not stop. Granite for a
coat, slabs of slate over the shoulders and down the spine, antlers of crystal as wide as it is long, and the
crystal's light where its eyes were. It holds its head low, antlers first."""

from mathutils import Vector

import beast
import temins as t
import slate_wolf
import wolf

LOOKS = beast.Proportions(
    height=1.30, half_length=0.62, girth=0.25, depth=0.34, leg=1.0,
    neck=0.52, neck_angle=58, head=0.46, skull=0.125, muzzle=0.95, head_tilt=16,
    eye=1.0, eyes_aside=0.6, brow=1.5,
    ear=0.21, ear_width=0.07, tail=0.16, tail_bush=0.05, tail_droop=60, haunch=0.95, toes=2,
    snarl=0.35, lid=0.7, ears_back=0.3, hackles=0,
)

STYLE = {"stride": 30, "run_frames": 16, "bob": 0.05, "flex": 5, "lunge": 0.34, "bite": 14, "tail": 4, "side": 0.26}


def antler(made, side, crystal, dark):
    """One antler: a beam sweeping up, out and back from the skull, and five tines off it."""
    along, S = made.anchors["along"], made.anchors["S"]
    root = along(0.06, 0.80, 0.55 * side)
    beam = [root,
            root + Vector((0.12 * side, 0.10, 0.24)),
            root + Vector((0.30 * side, 0.26, 0.46)),
            root + Vector((0.46 * side, 0.30, 0.72)),
            root + Vector((0.48 * side, 0.18, 0.98))]
    widths = [0.060, 0.055, 0.048, 0.038, 0.012]

    made.add(beast.c.loft(f"beam{side}", [(p, w, w) for p, w in zip(beam, widths)], crystal, sides=10), ["head"])
    made.add(beast.c.blob(f"burr{side}", (0.075, 0.075, 0.05), dark, root), ["head"])

    tines = [(beam[1], Vector((0.10 * side, -0.30, 0.20)), 0.045),   # the brow tine, forward: what it gores with
             (beam[1], Vector((0.22 * side, -0.16, 0.30)), 0.040),
             (beam[2], Vector((0.26 * side, -0.14, 0.30)), 0.040),
             (beam[3], Vector((0.24 * side, -0.02, 0.24)), 0.034),
             (beam[3], Vector((0.02 * side, 0.22, 0.22)), 0.030)]

    for i, (start, reach, width) in enumerate(tines):
        made.add(beast.tuft(f"tine{side}{i}", start, start + reach, width * 2, crystal, sides=7, flat=1.0), ["head"])


def build():
    colours = wolf.coat(fur=(0.21, 0.22, 0.23), dark=(0.08, 0.085, 0.095), pale=(0.42, 0.43, 0.44),
                        eye=(0.25, 0.95, 0.85), eye_glow=2.5, prefix="stone_stag")
    slab = t.material("slate_slab", (0.11, 0.12, 0.13), rough=0.75)
    crystal = t.material("stag_crystal", (0.25, 0.90, 0.82), glow=1.5, rough=0.15)

    made = beast.make("stone_stag", LOOKS, colours)

    slate_wolf.stone_parts(made, slab, crystal, plates=8, spikes=4)

    for side in (1, -1):
        antler(made, side, crystal, slab)

    # A beard of split stone under the jaw, and a mantle of slabs over the neck.
    along, S = made.anchors["along"], made.anchors["S"]
    withers, poll = made.anchors["withers"], made.anchors["poll"]

    for i in range(3):
        root = along(0.30 + 0.14 * i, -0.75, 0.0)
        made.add(beast.tuft(f"beard{i}", root, root + Vector((0, 0.04, -0.20 + 0.03 * i)), S * 0.7, slab, sides=6, flat=0.4), ["head"])

    for i in range(4):
        centre = poll.lerp(withers, 0.2 + 0.2 * i) + Vector((0, 0, 0.06))
        made.add(beast.tuft(f"mantle{i}", centre + Vector((0, 0, -0.05)), centre + Vector((0, 0.10, 0.17)), S * 1.1, slab, sides=6, flat=0.34, across=(1, 0, 0)), ["neck", "chest"])

    made.finish(STYLE)

    return {"folder": "creatures", "name": "stone_stag"}
