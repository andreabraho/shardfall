"""Spadone d'Acciaio — the Steel Sword grown to two hands (swordkit.variant)."""

import swordkit as k


def build():
    return k.variant("steel_sword", "steel_greatsword", great=True)
