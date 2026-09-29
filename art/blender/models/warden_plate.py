"""Piastre del Guardiano — the mountain gate's plate: blued steel edged in gold, a blue tabard, an ice-blue stone on the breast."""

import armourkit as a
import temins as t


def build():
    steel = t.material("warden_steel", (0.55, 0.66, 0.86), metal=0.7, rough=0.3)
    dark = t.material("blue_groove", (0.30, 0.37, 0.50), metal=0.6, rough=0.4)
    gold = t.material("gold", metal=0.8, rough=0.3)
    cloth = t.material("cloth_blue", (0.20, 0.28, 0.50), rough=0.8)
    belt = t.material("leather_dark")
    ice = t.material("ice", (0.55, 0.85, 1.0), glow=1.0, rough=0.2)

    # The breastplate on the chest: narrowing to the shoulders, a ridge down its front,
    # gold at its hem and a gorget at the neck.
    chest = [
        a.band("breast", 0.43, 0.40, 0.97, 1.29, steel, top=0.84, y=0.03),
        a.band("hem", 0.445, 0.415, 0.955, 0.99, gold, y=0.03),
        a.band("gorget", 0.31, 0.30, 1.26, 1.33, gold, y=0.05),
        t.box("ridge", (0.07, 0.05, 0.28), steel, at=(0, -0.37, 1.13), bevel=0.02),
        t.sphere("stone", 0.05, ice, at=(0, -0.405, 1.15), subdivisions=0, squash=(1, 0.7, 1.3)),
    ]

    # Three lames down the belly.
    spine = [
        a.band(f"lame_{i}", 0.425 - i * 0.003, 0.395 - i * 0.003, 0.87 - i * 0.105, 0.975 - i * 0.105,
               steel if i % 2 == 0 else dark, y=0.03)
        for i in range(3)
    ]

    # Its own cape, clear of the plates, deep blue with a gold hem: the Knight's own is
    # hidden under a modelled armour, since it would pass through the breastplate.
    spine += [
        a.cape("cape", t.material("cape_blue", (0.09, 0.13, 0.32), rough=0.9)),
        a.cape("hem", gold, top=(0.36, 0.556, 0.487), bottom=(0.28, 0.572, 0.50), rows=1, thickness=0.03),
    ]

    # The belt with its buckle, the tassets and the tabard hanging from it.
    hips = [
        a.band("belt", 0.415, 0.39, 0.575, 0.645, belt, y=0.03),
        t.box("buckle", (0.1, 0.03, 0.08), gold, at=(0, -0.37, 0.61), bevel=0.01),
        t.box("tabard", (0.16, 0.02, 0.30), cloth, at=(0, -0.385, 0.43), bevel=0.005),
    ]
    for side in (-1, 1):
        hips.append(t.box(f"tasset_front_{side}", (0.16, 0.04, 0.2), steel, at=(side * 0.2, -0.36, 0.49), bevel=0.015))
        hips.append(t.box(f"tasset_side_{side}", (0.04, 0.2, 0.19), steel, at=(side * 0.405, 0.02, 0.49), bevel=0.015))

    # A pauldron on each shoulder: a dome, a lame under it, a gold rim and rivets.
    left = [
        t.sphere("dome", 0.2, steel, at=(0.34, 0.0, 1.22), subdivisions=2, squash=(1.1, 0.95, 0.72)),
        t.sphere("lame", 0.17, dark, at=(0.42, 0.0, 1.14), subdivisions=2, squash=(0.9, 0.95, 0.72)),
        t.sphere("rim", 0.205, gold, at=(0.34, 0.0, 1.195), subdivisions=2, squash=(1.1, 0.96, 0.45)),
        t.sphere("rivet", 0.025, gold, at=(0.36, 0.0, 1.365), subdivisions=1),
    ]
    right = [a.mirror_x(p) for p in left]

    # Bracers on the forearms, flaring to the elbow, a gold band at the wrist.
    bracer_l = [
        t.cylinder("bracer", 0.15, 0.24, steel, at=(0.62, 0.0, 1.105), sides=8, radius_top=0.135, axis="X", bevel=0.01),
        t.cylinder("cuff", 0.142, 0.035, gold, at=(0.745, 0.0, 1.105), sides=8, axis="X"),
    ]
    bracer_r = [a.mirror_x(p) for p in bracer_l]

    return a.finish("warden_plate", {
        "chest": chest,
        "spine": spine,
        "hips": hips,
        "upperarm.l": left,
        "upperarm.r": right,
        "lowerarm.l": bracer_l,
        "lowerarm.r": bracer_r,
    })
