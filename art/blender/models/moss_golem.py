"""Golem di Muschio — the Stonewood's shielder (A2-01): the LOWPO Rock Golem (CC0) with clips of ours
(`golemkit`), grown over with the wood — moss on its head and shoulders and hanging from its chest, pale lichen
in crusts, and the Stonewood's crystal breaking out of its back."""

import armourkit as a
import golemkit
import temins as t


def build():
    arm = golemkit.load("Rock_Golem")
    golemkit.animate(arm)

    moss = t.material("golem_moss", (0.20, 0.34, 0.12), rough=0.95)
    deep = t.material("golem_moss_dark", (0.09, 0.18, 0.07), rough=0.95)
    lichen = t.material("golem_lichen", (0.50, 0.56, 0.40), rough=0.95)
    crystal = t.material("stag_crystal", (0.25, 0.90, 0.82), glow=1.5, rough=0.15)

    # In the pose it was made in: arms out to the sides, +X its left, -Y its front.
    for side in (1, -1):
        golemkit.grow(arm, t.sphere(f"moss_shoulder{side}", 0.40, moss, at=(side * 0.80, 0.05, 2.80), subdivisions=2, squash=(1.2, 1.3, 0.38)), "spine.003")
        golemkit.grow(arm, t.sphere(f"lichen_chest{side}", 0.2, lichen, at=(side * 0.45, -0.47, 2.15), subdivisions=2, squash=(1.1, 0.15, 0.9)), "spine.003")
        golemkit.grow(arm, t.sphere(f"lichen_thigh{side}", 0.15, lichen, at=(side * 0.55, -0.26, 1.05), subdivisions=2, squash=(1.1, 0.15, 0.9)), f"thigh.{'L' if side > 0 else 'R'}")

        for i, x in enumerate((0.35, 0.62)):
            golemkit.grow(arm, a.spike(f"strand{side}{i}", (side * x, -0.50, 2.55), (side * (x + 0.04), -0.58, 1.95 - 0.15 * i), 0.10, deep if i else moss, sides=5), "spine.003")

    golemkit.grow(arm, t.sphere("moss_head", 0.36, moss, at=(0, 0.05, 3.48), subdivisions=2, squash=(1.2, 1.25, 0.4)), "spine.006")

    for i, (root, tip, width) in enumerate((((0.30, 0.30, 2.40), (0.62, 1.20, 2.95), 0.19), ((-0.25, 0.30, 2.30), (-0.52, 1.10, 2.75), 0.17),
                                            ((0.0, 0.32, 2.05), (0.05, 1.05, 2.30), 0.15))):
        golemkit.grow(arm, a.spike(f"crystal{i}", root, tip, width, crystal, sides=6), "spine.003")

    return {"assets": "lowpo", "folder": "golem", "name": "moss_golem", "views": 4}
