"""Spada del Cacciatore — the Hunter's Greatsword cut down to one hand (swordkit.variant)."""

import swordkit as k


def build():
    return k.variant("hunters_greatsword", "hunters_sword", great=False)
