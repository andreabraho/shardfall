"""The Statue of Souls — the Abyss Crypts' first floor (P5, 2026-10-07): a hooded keeper of black stone on a plinth,
holding out an empty cup where the Soul Key is set. Until then a faint light stirs in the cup; the floor seals the
statue behind a barrier while the key is missing.

The game stretches it to the visual's height.
"""

import math

import armourkit as a
import temins as t


def build():
    stone = t.material("crypt_stone", (0.16, 0.13, 0.20), rough=0.85)
    dark = t.material("crypt_leaf", (0.07, 0.06, 0.09), rough=0.8)
    seal = t.material("abyss_seal", (0.62, 0.30, 1.0), glow=1.4, rough=0.3)

    pieces = [
        # The plinth, two steps.
        t.box("plinth_low", (1.5, 1.5, 0.3), stone, at=(0, 0, 0.15), bevel=0.05, segments=3),
        t.box("plinth_high", (1.15, 1.15, 0.35), stone, at=(0, 0, 0.47), bevel=0.05, segments=3),
        # The robe: a long cone from the plinth to the shoulders, a little wider at the hem.
        t.cylinder("robe", 0.5, 1.7, dark, at=(0, 0, 1.5), sides=24, radius_top=0.28, smooth=True),
        # The hood, deep and pointed, its opening toward -Y.
        t.sphere("hood", 0.3, dark, at=(0, 0.02, 2.5), subdivisions=3, squash=(1.0, 1.05, 1.15)),
        a.spike("hood_tip", (0, 0.08, 2.66), (0, 0.22, 2.98), 0.13, dark, sides=12),
        t.sphere("face", 0.2, t.material("crypt_void", (0.01, 0.0, 0.02), rough=1.0), at=(0, -0.14, 2.46), subdivisions=2, squash=(0.9, 0.5, 1.0)),
        # Eyes in the hood.
        t.sphere("eye_l", 0.035, seal, at=(-0.07, -0.25, 2.5), subdivisions=2),
        t.sphere("eye_r", 0.035, seal, at=(0.07, -0.25, 2.5), subdivisions=2),
        # Arms out in front, holding the cup.
        a.rod("arm_l", (-0.3, -0.05, 2.15), (-0.14, -0.5, 1.85), 0.09, dark, sides=12),
        a.rod("arm_r", (0.3, -0.05, 2.15), (0.14, -0.5, 1.85), 0.09, dark, sides=12),
        t.cylinder("cup", 0.2, 0.16, stone, at=(0, -0.55, 1.88), sides=24, radius_top=0.26, smooth=True),
        t.sphere("cup_light", 0.12, seal, at=(0, -0.55, 1.98), subdivisions=2, squash=(1, 1, 0.5)),
    ]

    # Runes round the plinth.
    for k in range(8):
        rad = k / 8 * math.tau
        x, y = math.cos(rad) * 0.76, math.sin(rad) * 0.76
        pieces.append(t.box(f"rune_{k}", (0.12, 0.02, 0.18), seal, at=(x, y, 0.15), bevel=0.01))
        pieces[-1].rotation_euler[2] = rad + math.pi / 2

    obj = t.join("soul_statue", pieces)
    t.soften(obj, 40)

    return {"folder": "crypts", "name": "soul_statue"}
