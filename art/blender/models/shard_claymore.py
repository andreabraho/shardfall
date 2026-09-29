"""Claymore del Frammento — a steel frame holding a blade of violet crystal, crystal shards at the guard."""

import random

import swordkit as k
import temins as t


def build():
    steel = t.material("steel_dark", metal=0.6, rough=0.4)
    crystal = t.material("crystal", (0.55, 0.28, 1.0), glow=0.7, rough=0.15)
    gold = t.material("gold", metal=0.8, rough=0.3)
    leather = t.material("leather_dark")

    outline = k.mirrored([(0.16, 0.0), (0.17, 1.3), (0.14, 1.65), (0.0, 1.98)])
    core = [(x * 0.7, y * 0.97 + 0.03) for x, y in outline]

    parts = [
        k.blade(outline, 0.08, steel, edge=0.03),
        k.blade(core, 0.13, crystal, edge=0.05, name="core"),
        k.crossguard(0.34, 0.08, 0.12, gold, droop=0.02, flare=0.5, at=0.03),
        *k.grip(0.40, 0.046, leather, gold, wraps=4),
        *k.pommel("facet", 0.08, crystal, at=-0.45),
    ]

    # Shards of the crystal growing out of the guard.
    rng = random.Random(5)
    for i, (x, tilt) in enumerate(((-0.30, 0.5), (-0.2, 0.25), (0.2, -0.25), (0.30, -0.5))):
        shard = t.sphere(f"shard_{i}", 0.04, crystal, subdivisions=0, squash=(0.8, 0.8, 2.6))
        shard.rotation_euler = (rng.uniform(-0.3, 0.3), tilt, 0)
        shard.location = (x, 0, 0.12)
        parts.append(shard)

    return k.finish("shard_claymore", parts)
