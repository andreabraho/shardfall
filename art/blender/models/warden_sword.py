"""Spada del Guardiano — the Warden Greatsword cut down to one hand (swordkit.variant)."""

import swordkit as k


def build():
    return k.variant("warden_greatsword", "warden_sword", great=False)
