"""Spadone di Ferro — the Iron Sword grown to two hands (swordkit.variant)."""

import swordkit as k


def build():
    return k.variant("iron_sword", "iron_greatsword", great=True)
