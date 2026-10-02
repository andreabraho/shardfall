"""Spada di Bronzo — the Bronze Greatsword cut down to one hand (swordkit.variant)."""

import swordkit as k


def build():
    return k.variant("bronze_greatsword", "bronze_sword", great=False)
