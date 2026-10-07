"""What act 2's two-legged bosses wear (A2-01): pieces of ours hung on act 1's bodies by a bone (`attachments` on a
visual). Each is built round the origin in units of the thing it sits on — a head of radius 1, shoulders 1 to each
side — and the visual says how big that is on the creature.

`build(name)` makes one of: king_crown, king_mantle, warlord_helm, warlord_pauldrons.
"""

import math

import armourkit as a
import temins as t


def _ring(count, radius, z, start=0.0):
    for i in range(count):
        rad = start + i / count * math.tau
        yield i, math.cos(rad) * radius, math.sin(rad) * radius, rad


def king_crown():
    """A tall crown of green bronze, coral grown through its points, pearls round the band, weed hanging behind."""
    bronze = t.material("mire_crown_bronze", (0.07, 0.24, 0.20), metal=0.65, rough=0.38)
    deep = t.material("mire_deep_bronze", (0.025, 0.09, 0.085), metal=0.55, rough=0.45)
    coral = t.material("mire_coral", (0.80, 0.30, 0.26), rough=0.6)
    pearl = t.material("mire_pearl", (0.92, 0.95, 0.90), glow=0.6, rough=0.2)
    light = t.material("bell_light", (0.35, 1.0, 0.62), glow=1.6, rough=0.2)
    kelp = t.material("mire_kelp", (0.10, 0.30, 0.16), rough=0.8)

    parts = [a.band("band", 1.06, 1.06, 0.0, 0.42, bronze, sides=36, top=1.04),
             a.band("rim_low", 1.10, 1.10, -0.04, 0.06, deep, sides=36),
             a.band("rim_high", 1.13, 1.13, 0.38, 0.48, deep, sides=36)]

    for i, x, y, rad in _ring(8, 1.10, 0.45):
        tall = 1.25 if i % 2 == 0 else 0.7
        parts.append(a.spike(f"point_{i}", (x, y, 0.42), (x * 1.18, y * 1.18, 0.42 + tall), 0.22, bronze, sides=6))
        if i % 2 == 0:
            parts.append(a.spike(f"coral_{i}", (x * 1.05, y * 1.05, 0.42 + tall * 0.55), (x * 1.42, y * 1.42, 0.42 + tall * 1.25), 0.09, coral, sides=6))
        parts.append(t.sphere(f"pearl_{i}", 0.13, pearl, at=(x * 1.02, y * 1.02, 0.21), subdivisions=2))

    # The stone over the brow: the marsh's own light.
    parts.append(t.sphere("stone", 0.2, light, at=(0, -1.12, 0.24), subdivisions=2, squash=(1, 0.6, 1.3)))

    # Weed hanging from the back of the band.
    for i, x in enumerate((-0.6, -0.3, 0.0, 0.3, 0.6)):
        y = math.sqrt(max(0.0, 1.12 ** 2 - x ** 2))
        parts.append(a.spike(f"weed_{i}", (x, y, 0.05), (x * 1.1, y * 1.15, -1.1 - 0.25 * (i % 2)), 0.12, kelp, sides=5))

    return parts


def king_mantle():
    """A collar of weed and chain over the shoulders: fronds falling to the chest and down the back, pearls on the chain."""
    kelp = t.material("mire_kelp", (0.10, 0.30, 0.16), rough=0.8)
    dark = t.material("mire_kelp_dark", (0.04, 0.14, 0.09), rough=0.85)
    deep = t.material("mire_deep_bronze", (0.025, 0.09, 0.085), metal=0.55, rough=0.45)
    pearl = t.material("mire_pearl", (0.92, 0.95, 0.90), glow=0.6, rough=0.2)

    parts = [a.band("collar", 0.62, 0.50, -0.05, 0.16, deep, sides=32, top=0.86)]

    for i, x, y, rad in _ring(14, 1.0, 0.0, start=0.2):
        long = 0.75 + 0.3 * (i % 3)
        parts.append(a.spike(f"frond_{i}", (x * 0.55, y * 0.45, 0.10), (x * 1.12, y * 0.92, -long), 0.2, kelp if i % 2 else dark, sides=5))

    for i, x, y, rad in _ring(10, 0.74, 0.0):
        parts.append(t.sphere(f"pearl_{i}", 0.085, pearl, at=(x, y * 0.82, -0.02 - 0.1 * abs(math.sin(rad))), subdivisions=2))

    return parts


def warlord_helm():
    """A cap of black iron over the skull, two great horns, a crest of standing flame, fire in the seam."""
    iron = t.material("cinder_iron", (0.07, 0.06, 0.06), metal=0.55, rough=0.5)
    horn = t.material("warlord_horn", (0.22, 0.16, 0.11), rough=0.6)
    lava = t.material("lava", (1.0, 0.36, 0.06), glow=1.7, rough=0.4)
    hot = t.material("white_heat", (1.0, 0.78, 0.42), glow=2.0, rough=0.3)

    parts = [t.sphere("cap", 1.08, iron, at=(0, 0.05, 0.25), subdivisions=3, squash=(1.0, 1.0, 0.72)),
             a.band("brow", 1.10, 1.10, 0.10, 0.30, iron, sides=36),
             a.band("seam", 1.115, 1.115, 0.17, 0.23, lava, sides=36),
             t.box("nasal", (0.22, 0.12, 0.7), iron, at=(0, -1.07, -0.1), bevel=0.04)]

    for side in (1, -1):
        parts.append(a.spike(f"horn_a{side}", (side * 0.95, 0.0, 0.45), (side * 1.9, -0.1, 0.9), 0.3, horn, sides=10))
        parts.append(a.spike(f"horn_b{side}", (side * 1.75, -0.08, 0.82), (side * 2.05, -0.5, 1.9), 0.2, horn, sides=10))

    for i in range(5):
        y = -0.7 + i * 0.35
        tall = 1.2 - 0.18 * abs(i - 1.5)
        parts.append(a.spike(f"flame_{i}", (0, y, 0.85), (0, y + 0.25, 0.85 + tall), 0.2, hot if i % 2 else lava, sides=6))

    return parts


def warlord_pauldrons():
    """A plate of black iron on each shoulder, three spikes standing on it, fire under its rim; a chain of skulls across the chest."""
    iron = t.material("cinder_iron", (0.07, 0.06, 0.06), metal=0.55, rough=0.5)
    lava = t.material("lava", (1.0, 0.36, 0.06), glow=1.7, rough=0.4)
    bone = t.material("bone", rough=0.7)

    parts = []

    for side in (1, -1):
        x = side * 1.05
        parts.append(t.sphere(f"plate{side}", 0.62, iron, at=(x, 0, 0.1), subdivisions=3, squash=(1.1, 1.0, 0.6)))
        parts.append(t.sphere(f"rim{side}", 0.66, lava, at=(x, 0, 0.0), subdivisions=2, squash=(1.1, 1.0, 0.22)))
        parts.append(t.sphere(f"under{side}", 0.6, iron, at=(x, 0, -0.1), subdivisions=2, squash=(1.05, 0.95, 0.3)))

        for i, (dx, dy, tall) in enumerate(((0.0, 0.0, 0.95), (0.3, 0.22, 0.7), (0.3, -0.22, 0.7))):
            base = (x + side * dx, dy, 0.3)
            parts.append(a.spike(f"spike{side}{i}", base, (base[0] + side * 0.25, dy * 1.4, 0.3 + tall), 0.16, iron, sides=7))

    for i in range(5):
        x = -0.6 + i * 0.3
        parts.append(t.sphere(f"skull_{i}", 0.13, bone, at=(x, -0.62, -0.25 - 0.12 * (1 - abs(i - 2) / 2)), subdivisions=2, squash=(1, 0.9, 1.1)))

    return parts


# -- The Abyss Crypts' bosses (P5, 2026-10-07): black iron, bone and a crimson light. --

def _abyss():
    return (t.material("abyss_plate", (0.05, 0.04, 0.07), metal=0.75, rough=0.3),
            t.material("abyss_glow", (0.95, 0.10, 0.38), glow=1.8, rough=0.4),
            t.material("bone", rough=0.7))


def jailer_mask():
    """The Jailer's head: a cage of black iron bars over the skull, a slit of light where the eyes are, a ring on top."""
    iron, glow, bone = _abyss()
    parts = [a.band("brow", 1.08, 1.08, 0.0, 0.26, iron, sides=36),
             a.band("jaw", 1.06, 1.06, -0.75, -0.55, iron, sides=36),
             t.box("slit", (1.0, 0.12, 0.12), glow, at=(0, -1.06, 0.06), bevel=0.03)]
    for i, x, y, rad in _ring(10, 1.08, 0.0):
        parts.append(a.rod(f"bar_{i}", (x, y, -0.7), (x * 0.35, y * 0.35, 1.05), 0.07, iron, sides=8))
    parts.append(a.band("crown_ring", 0.42, 0.42, 1.0, 1.12, iron, sides=24))
    return parts


def jailer_chains():
    """Chains across the Jailer's chest and a great ring of keys at its belt-side, the keys of bone."""
    iron, glow, bone = _abyss()
    parts = []
    for side in (-1, 1):
        for i in range(9):
            u = i / 8
            x, z = side * (0.9 - 1.6 * u), 0.3 - 0.9 * u
            parts.append(t.cylinder(f"link_{side}_{i}", 0.11, 0.05, iron, at=(x, -0.62, z), sides=12, axis="Y", bevel=0.01))
    parts.append(t.cylinder("key_ring", 0.32, 0.06, iron, at=(0.7, -0.62, -0.75), sides=24, axis="Y", bevel=0.01))
    for k in range(4):
        rad = math.radians(200 + k * 40)
        root = (0.7 + 0.3 * math.cos(rad), -0.62, -0.75 + 0.3 * math.sin(rad))
        tip = (0.7 + 0.75 * math.cos(rad), -0.62, -0.75 + 0.75 * math.sin(rad))
        parts.append(a.rod(f"key_{k}", root, tip, 0.05, bone, sides=6))
    parts.append(t.sphere("lock_light", 0.12, glow, at=(0.7, -0.66, -0.75), subdivisions=2))
    return parts


def ferryman_hood():
    """A deep hood of black cloth over the Ferryman's head, two crimson eyes inside, a rim of bone coins round its edge."""
    iron, glow, bone = _abyss()
    cloth = t.material("abyss_cloth_black", (0.04, 0.02, 0.05), rough=0.95)
    parts = [t.sphere("hood", 1.25, cloth, at=(0, 0.15, 0.25), subdivisions=3, squash=(1.0, 1.05, 1.15)),
             a.spike("peak", (0, 0.35, 1.2), (0, 0.95, 2.0), 0.55, cloth, sides=12),
             t.sphere("void", 0.9, t.material("crypt_void", (0.01, 0.0, 0.02), rough=1.0), at=(0, -0.45, 0.05), subdivisions=2, squash=(0.9, 0.5, 1.0))]
    for side in (-1, 1):
        parts.append(t.sphere(f"eye_{side}", 0.13, glow, at=(side * 0.32, -0.9, 0.15), subdivisions=2))
    for i in range(9):
        rad = math.radians(-160 + i * 40)
        parts.append(t.cylinder(f"coin_{i}", 0.16, 0.05, bone, at=(math.cos(rad) * 1.15, math.sin(rad) * 1.15 - 0.2, -0.75), sides=16, axis="Y"))
    return parts


def ferryman_collar():
    """A collar of bone over the shoulders, ribs reaching down the chest, a crimson lantern hanging at the front."""
    iron, glow, bone = _abyss()
    parts = [a.band("collar", 0.62, 0.50, -0.05, 0.18, iron, sides=32, top=0.86)]
    for i, x, y, rad in _ring(12, 1.0, 0.0, start=0.25):
        parts.append(a.spike(f"rib_{i}", (x * 0.55, y * 0.45, 0.10), (x * 1.05, y * 0.85, -0.65 - 0.2 * (i % 2)), 0.1, bone, sides=6))
    parts.append(a.rod("lantern_chain", (0, -0.55, 0.0), (0, -0.62, -0.55), 0.03, iron, sides=6))
    parts.append(t.cylinder("lantern", 0.18, 0.3, iron, at=(0, -0.62, -0.75), sides=8, bevel=0.02))
    parts.append(t.sphere("lantern_light", 0.13, glow, at=(0, -0.62, -0.75), subdivisions=2))
    return parts


def crypt_crown():
    """The Lord of the Crypt's crown: a band of black iron, great horns of bone sweeping up and back, a crimson eye on the brow."""
    iron, glow, bone = _abyss()
    parts = [a.band("band", 1.08, 1.08, 0.0, 0.36, iron, sides=36),
             a.band("rim", 1.12, 1.12, 0.30, 0.40, iron, sides=36),
             t.sphere("eye", 0.22, glow, at=(0, -1.12, 0.2), subdivisions=2, squash=(1, 0.6, 1.3))]
    for side in (1, -1):
        parts.append(a.spike(f"horn_a{side}", (side * 0.9, 0.0, 0.3), (side * 1.7, 0.3, 1.2), 0.3, bone, sides=10))
        parts.append(a.spike(f"horn_b{side}", (side * 1.6, 0.28, 1.1), (side * 1.5, 1.0, 2.3), 0.2, bone, sides=10))
    for i, x, y, rad in _ring(7, 1.1, 0.4, start=math.radians(-130)):
        if abs(math.degrees(rad) + 90) < 20:
            continue
        parts.append(a.spike(f"point_{i}", (x, y, 0.38), (x * 1.08, y * 1.08, 1.0), 0.14, iron, sides=6))
    return parts


def crypt_mantle():
    """Over the Lord's shoulders: two great plates of black iron with bone spines, and a necklace of skulls with crimson eyes."""
    iron, glow, bone = _abyss()
    parts = []
    for side in (1, -1):
        x = side * 1.05
        parts.append(t.sphere(f"plate{side}", 0.66, iron, at=(x, 0, 0.1), subdivisions=3, squash=(1.1, 1.0, 0.6)))
        parts.append(t.sphere(f"rim{side}", 0.7, glow, at=(x, 0, 0.0), subdivisions=2, squash=(1.1, 1.0, 0.18)))
        for i, (dx, dy, tall) in enumerate(((0.0, 0.0, 1.1), (0.3, 0.24, 0.8), (0.3, -0.24, 0.8), (-0.3, 0.0, 0.6))):
            base = (x + side * dx, dy, 0.3)
            parts.append(a.spike(f"spine{side}{i}", base, (base[0] + side * 0.3, dy * 1.4, 0.3 + tall), 0.15, bone, sides=7))
    for i in range(5):
        x = -0.6 + i * 0.3
        z = -0.25 - 0.12 * (1 - abs(i - 2) / 2)
        parts.append(t.sphere(f"skull_{i}", 0.14, bone, at=(x, -0.62, z), subdivisions=2, squash=(1, 0.9, 1.1)))
        parts.append(t.sphere(f"socket_{i}", 0.04, glow, at=(x, -0.75, z + 0.02), subdivisions=1))
    return parts


PIECES = {"king_crown": king_crown, "king_mantle": king_mantle, "warlord_helm": warlord_helm, "warlord_pauldrons": warlord_pauldrons,
          "jailer_mask": jailer_mask, "jailer_chains": jailer_chains, "ferryman_hood": ferryman_hood, "ferryman_collar": ferryman_collar,
          "crypt_crown": crypt_crown, "crypt_mantle": crypt_mantle}


def build(name):
    obj = t.join(name, PIECES[name]())
    t.soften(obj, 40)

    return {"folder": "creatures", "name": name}
