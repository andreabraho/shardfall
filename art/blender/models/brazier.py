"""A brazier: an iron bowl of embers on three legs (PIP-02 test piece). About 1 m tall."""

import math

import temins as t


def build():
    iron = t.material("iron", metal=0.5, rough=0.55)
    rust = t.material("bronze", rough=0.6)
    coal = t.material("coal", (0.08, 0.07, 0.07), rough=0.9)
    ember = t.material("ember", glow=1.8, rough=0.5)

    parts = []

    # The bowl: wide at the lip, a rim band of bronze.
    parts.append(t.cylinder("bowl", 0.22, 0.24, iron, at=(0, 0, 0.86), sides=10, radius_top=0.40, bevel=0.012))
    parts.append(t.cylinder("rim", 0.42, 0.05, rust, at=(0, 0, 0.99), sides=10, bevel=0.01))
    parts.append(t.cylinder("neck", 0.10, 0.10, iron, at=(0, 0, 0.70), sides=8, radius_top=0.2))

    # Three legs splaying out to feet.
    for i in range(3):
        a = math.radians(90 + i * 120)
        foot = (math.cos(a) * 0.36, math.sin(a) * 0.36)
        top = (math.cos(a) * 0.12, math.sin(a) * 0.12)
        leg = t.box(f"leg_{i}", (0.05, 0.05, 0.78), iron, at=(0, 0, 0), bevel=0.01)
        # Lean the leg from its top at the neck to its foot on the ground.
        dx, dy = foot[0] - top[0], foot[1] - top[1]
        tilt = math.atan2(math.hypot(dx, dy), 0.70)
        leg.rotation_euler = (0, -tilt, a)
        leg.location = ((foot[0] + top[0]) / 2, (foot[1] + top[1]) / 2, 0.37)
        parts.append(leg)
        parts.append(t.box(f"foot_{i}", (0.1, 0.1, 0.04), rust, at=(foot[0], foot[1], 0.02), bevel=0.01))

    # Coals heaped in the bowl, the ones on top glowing.
    import random
    rng = random.Random(3)
    for i in range(16):
        r = rng.uniform(0, 0.3)
        a = rng.uniform(0, 6.283)
        z = 0.97 + (0.3 - r) * 0.35 + rng.uniform(-0.02, 0.03)
        parts.append(t.rock(f"coal_{i}", rng.uniform(0.05, 0.08), ember if rng.random() < 0.55 else coal,
                            at=(math.cos(a) * r, math.sin(a) * r, z), seed=40 + i, subdivisions=0, roughness=0.25))

    t.join("brazier", parts)

    return {"folder": "props", "name": "brazier"}
