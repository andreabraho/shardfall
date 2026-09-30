"""Chiamatombe — a dark blade with violet runes, a guard of ribs, a pommel of bone."""

import swordkit as k
import temins as t


def build():
    dark = t.material("grave_steel", (0.25, 0.23, 0.28), metal=0.5, rough=0.4)
    bone = t.material("bone", rough=0.7)
    violet = t.material("violet", glow=1.2, rough=0.3)
    leather = t.material("leather_dark")

    parts = [
        k.blade(k.mirrored([(0.15, 0.0), (0.16, 1.2), (0.15, 1.55), (0.08, 1.80), (0.0, 1.97)]), 0.11, dark),
        t.box("spine", (0.14, 0.13, 0.09), bone, at=(0, 0, 0.04), bevel=0.025),
        *k.grip(0.40, 0.045, leather, bone, wraps=4),
        *k.pommel("cup", 0.08, bone, at=-0.45, gem=violet),
    ]

    # Ribs curling up round the blade's foot, three to a side.
    for side in (False, True):
        for i, reach in enumerate((0.34, 0.28, 0.22)):
            rib = t.profile_solid(f"rib_{side}_{i}", [
                (0.0, -0.02), (reach * 0.6, -0.03), (reach, 0.06), (reach * 0.95, 0.16),
                (reach * 0.85, 0.10), (reach * 0.55, 0.03), (0.0, 0.03),
            ], 0.05, bone, bevel=0.012, segments=3)
            parts.append(k.stand(rib, y=(i - 1) * 0.035, z=0.02 - i * 0.03, mirror=side))

    # Runes up the blade.
    for i, y in enumerate((0.35, 0.65, 0.95, 1.25)):
        s = 0.04 - i * 0.004
        parts.append(k.stand(t.profile_solid(f"rune_{i}", [(0, y - s * 1.8), (s, y), (0, y + s * 1.8), (-s, y)], 0.116, violet)))

    return k.finish("gravecaller", parts)
