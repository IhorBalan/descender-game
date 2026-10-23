"""Burned: a sent-down angel of Heresy. The guard's rig, charred.

Ash-grey armour, cloth the colour of dried blood, wings burnt to struts and
a halo that is only an ember now. The same bones as angel.py: the palette
is swapped for the build and put back after."""
import angel

PALETTE = {
    "ARMOR": (112, 104, 110), "ARMOR_S": (74, 66, 74), "ARMOR_D": (48, 42, 52),
    "GOLD": (160, 112, 64), "GOLD_S": (104, 70, 44), "GOLD_H": (214, 160, 90),
    "CLOTH": (124, 32, 36), "CLOTH_S": (72, 18, 24),
    "WING": (82, 70, 76), "WING_S": (56, 46, 54), "WING_D": (36, 30, 38),
    "LIGHT": (255, 140, 70),
    "HALO_GLOW": (255, 120, 50), "HALO_DOT": (220, 90, 40), "HALO_BRIGHT": (255, 200, 120),
}


def build():
    saved = {name: getattr(angel, name) for name in PALETTE}
    try:
        for name, colour in PALETTE.items():
            setattr(angel, name, colour)
        return angel.build()
    finally:
        for name, colour in saved.items():
            setattr(angel, name, colour)
