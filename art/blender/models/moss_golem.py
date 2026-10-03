"""Golem di Muschio — the Stonewood's shielder (A2-01): a golem of ours (`golemkit`), a hunched heap of grey
stone with a lamp of green in its eyes, grown over with the wood — moss on its head and shoulders and hanging
from its chest, pale lichen in crusts, and the Stonewood's crystal breaking out of its hump."""

import armourkit as a
import golemkit
import temins as t


def build():
    stone = t.material("golem_stone", (0.22, 0.23, 0.22), rough=0.85)
    dark = t.material("golem_stone_dark", (0.12, 0.13, 0.13), rough=0.9)
    eyes = t.material("golem_eyes", (0.45, 1.0, 0.55), glow=2.2, rough=0.2)
    moss = t.material("golem_moss", (0.20, 0.34, 0.12), rough=0.95)
    deep = t.material("golem_moss_dark", (0.09, 0.18, 0.07), rough=0.95)
    lichen = t.material("golem_lichen", (0.50, 0.56, 0.40), rough=0.95)
    crystal = t.material("stag_crystal", (0.25, 0.90, 0.82), glow=1.5, rough=0.15)

    arm = golemkit.skeleton("moss_golem")
    golemkit.body(arm, stone, dark, eyes)

    for side in (1, -1):
        lr = "L" if side > 0 else "R"
        golemkit.grow(arm, t.sphere(f"moss_shoulder{side}", 0.3, moss, at=(side * 0.86, 0.05, 2.8), subdivisions=2, squash=(1.15, 1.2, 0.5)), f"upper_arm.{lr}")
        golemkit.grow(arm, t.sphere(f"lichen_chest{side}", 0.2, lichen, at=(side * 0.42, -0.6, 2.12), subdivisions=2, squash=(1.1, 0.15, 0.9)), "spine.003")
        golemkit.grow(arm, t.sphere(f"lichen_thigh{side}", 0.14, lichen, at=(side * 0.46, -0.31, 0.98), subdivisions=2, squash=(1.1, 0.15, 0.9)), f"thigh.{lr}")

        for i, x in enumerate((0.3, 0.6)):
            golemkit.grow(arm, a.spike(f"strand{side}{i}", (side * x, -0.54, 1.95), (side * (x + 0.04), -0.6, 1.45 - 0.15 * i), 0.10, deep if i else moss, sides=5), "spine.003")

    golemkit.grow(arm, t.sphere("moss_head", 0.22, moss, at=(0, -0.44, 3.02), subdivisions=2, squash=(1.2, 1.15, 0.5)), "spine.006")

    for i, (root, tip, width) in enumerate((((0.25, 0.6, 2.55), (0.55, 1.25, 3.15), 0.19), ((-0.22, 0.62, 2.45), (-0.5, 1.2, 2.95), 0.17),
                                            ((0.0, 0.75, 2.2), (0.05, 1.3, 2.45), 0.15))):
        golemkit.grow(arm, a.spike(f"crystal{i}", root, tip, width, crystal, sides=6), "spine.003")

    golemkit.animate(arm)

    return {"folder": "creatures", "name": "moss_golem", "views": 4}
