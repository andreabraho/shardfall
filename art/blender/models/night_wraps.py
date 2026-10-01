"""Fasce Notturne — the Ninja's first armour, hardly one: grey bandage wound round the chest,
the forearms and the shins, and a green sash knotted at the hip (CLS-03)."""

import ninjakit as n
import temins as t


def build():
    bandage = t.material("bandage", (0.42, 0.41, 0.38), rough=0.9)
    green = t.material("ninja_green", (0.08, 0.26, 0.15), rough=0.85)

    arm_l, arm_r = n.sides(n.wraps(bandage))
    leg_l, leg_r = n.sides(n.shin(bandage, turns=4))

    return n.finish("night_wraps", {
        "spine": n.binding(bandage),
        "hips": n.sash(green),
        "lowerarm.l": arm_l, "lowerarm.r": arm_r,
        "lowerleg.l": leg_l, "lowerleg.r": leg_r,
    })
