"""Split the 168x168 SpriteCook hero (hero_base.png) into movable layers for cut-out animation."""
import os

import numpy as np
from PIL import Image, ImageDraw

SRC = Image.open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "hero_base.png")).convert("RGBA")
A = np.array(SRC).astype(int)
H, W = A.shape[:2]
R, G, B, AL = A[..., 0], A[..., 1], A[..., 2], A[..., 3]
OPAQUE = AL > 0
DARK = (R + G + B) < 130
YY, XX = np.mgrid[0:H, 0:W]


def poly(pts):
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).polygon(pts, fill=1)
    return np.array(m).astype(bool)


def grow(mask, n=1):
    m = mask.copy()
    for _ in range(n):
        g = m.copy()
        g[1:] |= m[:-1]; g[:-1] |= m[1:]; g[:, 1:] |= m[:, :-1]; g[:, :-1] |= m[:, 1:]
        m = g
    return m


warm = (R > B + 25)
cool = ~warm

# Wing: inside a hand-drawn region, cool colours and outline only.
wing_region = poly([(18, 86), (22, 64), (28, 48), (44, 33), (60, 31), (68, 36), (73, 44), (76, 51), (77, 56),
                    (71, 60), (66, 64), (62, 72), (60, 84), (56, 92), (48, 97), (28, 97)])
WING = wing_region & OPAQUE & (cool | DARK)

# Halo: gold pixels in the top band, plus the dark outline hugging them (but not the hair).
gold = (R > 170) & (G > 110) & (B < 120) & (R > B + 70)
halo_gold = gold & (YY <= 23)
HALO = OPAQUE & (halo_gold | (DARK & grow(halo_gold, 1) & (YY <= 23) & ~((YY > 13) & (XX > 74) & (XX < 112))))
HALO |= OPAQUE & halo_gold

# Forearm + hand + sword: rotates about the elbow for attacks.
arm_region = poly([(56, 83), (73, 83), (77, 97), (85, 99), (86, 108), (148, 124), (148, 133), (138, 135),
                   (84, 124), (80, 123), (76, 122), (68, 118), (60, 113), (50, 108), (50, 99), (56, 97)])
cream = (R > 200) & (G > 200) & (B > 150) & ~((B > R - 10))  # tunic cloth
tunic_gold = gold & (YY < 112) & (XX > 82)
pants = (B >= R) & (R + G + B < 330) & ~DARK & (XX < 100) & (YY > 108) & ~poly([(84, 106), (148, 122), (148, 134), (84, 126)])
ARM = arm_region & OPAQUE & ~cream & ~tunic_gold & ~pants & ~WING
blade_zone = poly([(78, 103), (152, 119), (152, 138), (78, 127)])
blade = blade_zone & OPAQUE & ~DARK & (G >= R) & ~warm & (XX >= 80)
ARM &= (XX < 82) | blade
ARM |= blade | (OPAQUE & DARK & grow(blade, 1) & blade_zone) | (blade_zone & OPAQUE & (XX >= 118))

# Legs (below the tunic), split at the gap between them.
legs_region = (YY >= 117) & ~ARM
BACK_LEG = legs_region & OPAQUE & (XX < 85)
FRONT_LEG = legs_region & OPAQUE & (XX >= 85)

BODY = OPAQUE & ~WING & ~HALO & ~ARM & ~BACK_LEG & ~FRONT_LEG
HEAD = BODY & (YY < 57)


def layer(mask, img=None):
    arr = np.array(SRC if img is None else img).copy()
    arr[~mask] = 0
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


def inpaint(img, hole):
    """Fill hole pixels from opaque neighbours, onion-peel style."""
    a = np.array(img).astype(int)
    todo = hole & (a[..., 3] == 0)
    while todo.any():
        filled = False
        for y, x in zip(*np.nonzero(todo)):
            nb = []
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < H and 0 <= xx < W and a[yy, xx, 3] > 0 and not todo[yy, xx]:
                    nb.append(tuple(a[yy, xx]))
            if len(nb) >= 2 or (nb and not filled):
                a[y, x] = max(set(nb), key=nb.count)
                todo[y, x] = False
                filled = True
        if not filled:
            break
    return Image.fromarray(a.astype(np.uint8), "RGBA")


if __name__ == "__main__":
    vis = np.zeros((H, W, 4), np.uint8)
    for m, c in ((BODY, (240, 230, 200)), (WING, (80, 160, 255)), (HALO, (255, 200, 0)), (ARM, (255, 60, 60)),
                 (BACK_LEG, (120, 60, 200)), (FRONT_LEG, (60, 200, 120)), (HEAD, (250, 150, 150))):
        vis[m] = (*c, 255)
    Image.fromarray(vis).resize((W * 4, H * 4), Image.NEAREST).save("masks.png")
