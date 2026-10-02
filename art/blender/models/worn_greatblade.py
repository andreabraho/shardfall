"""Spadone Consunto — the Worn Blade grown to two hands (swordkit.variant)."""

import swordkit as k


def build():
    return k.variant("worn_blade", "worn_greatblade", great=True)
