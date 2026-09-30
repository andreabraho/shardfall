"""Giubba Imbottita — the first coat: quilted cloth in padded rolls, a rope belt, a short skirt, puffed sleeves."""

import armourkit as a
import temins as t


def build():
    cloth = t.material("cloth_beige", (0.70, 0.60, 0.44), rough=0.9)
    brown = t.material("cloth_brown", (0.45, 0.33, 0.22), rough=0.9)
    wood = t.material("wood")

    chest = [
        a.roll("roll_0", 0.43, 0.40, 0.97, 1.08, cloth),
        a.roll("roll_1", 0.425, 0.395, 1.08, 1.19, cloth),
        a.roll("roll_2", 0.415, 0.39, 1.19, 1.29, cloth, top=0.86),
        a.band("collar", 0.30, 0.29, 1.26, 1.33, brown, y=0.05),
        t.sphere("button_0", 0.025, wood, at=(0, -0.40, 1.19), subdivisions=1),
        t.sphere("button_1", 0.025, wood, at=(0, -0.41, 1.05), subdivisions=1),
    ]

    spine = [a.roll(f"roll_{i}", 0.425, 0.395, 0.64 + i * 0.112, 0.752 + i * 0.112, cloth) for i in range(3)]
    spine += [t.sphere(f"button_{i}", 0.025, wood, at=(0, -0.405, 0.70 + i * 0.112), subdivisions=1) for i in range(3)]

    hips = [
        a.band("rope", 0.42, 0.395, 0.575, 0.635, brown, y=0.03, bevel=0.02),
        a.band("skirt", 0.47, 0.44, 0.45, 0.58, cloth, top=0.9, y=0.03),
    ]

    # Puffed sleeves on the upper arms, a roll at the shoulder.
    left = [
        a.sleeve("sleeve", 0.12, 0.42, 0.165, 0.15, cloth, bevel=0.03),
        t.sphere("puff", 0.17, cloth, at=(0.30, 0.0, 1.19), subdivisions=2, squash=(1.1, 1.0, 0.75)),
    ]
    right = [a.mirror_x(p) for p in left]

    return a.finish("padded_coat", {"chest": chest, "spine": spine, "hips": hips, "upperarm.l": left, "upperarm.r": right})
