"""Lama del Crinale — the Ridge Greatblade cut down to one hand (swordkit.variant)."""

import swordkit as k


def build():
    return k.variant("ridge_greatblade", "ridge_blade", great=False)
