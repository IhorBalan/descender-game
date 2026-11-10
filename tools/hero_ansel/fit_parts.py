"""Fits the cut parts of the new Ansel to his approved proportions and writes the rig layout.

The parts sheet draws each part at its own scale, so every part is measured here against the
approved full-body picture (the "reference": 1024 x 1536, y down) and resized to match. Writes
Assets/Art/Paradise/Ansel/ansel_*.png and ansel_rig.json, which ParadiseHero builds the rig from.

    python tools/hero_ansel/cut_parts.py    # parts_sheet.png -> one PNG per part
    python tools/hero_ansel/fit_parts.py    # those parts -> Assets/Art/Paradise/Ansel

Then Descender > Paradise > Build hero rig in Unity.

Needs numpy, Pillow and OpenCV.
"""
import json
import os
import sys

try:
    import cv2
except ImportError:  # OpenCV is only needed to run this script, not to import it (check_tools.py).
    cv2 = None
import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise", "Ansel")

GROUND = 1455.0  # the soles, on the reference
AXIS = 585.0  # the body's axis, on the reference
REF_PER_UNIT = 860.0  # reference pixels in one world unit: he stands 1.63 units tall
ART_SCALE = 0.5  # written pixels per reference pixel
PPU = REF_PER_UNIT * ART_SCALE

# part: (x scale, y scale, a point on the part, where that point sits on the reference)
FIT = {
    "arm": (1.0, 1.15, (55, 35), (550, 320)),
    "leg": (1.0, 1.31, (100, 60), (565, 680)),
    "torso": (0.9, 1.15, (90, 10), (585, 205)),
    "head": (0.88, 0.88, (105, 190), (578, 205)),
    "hood": (1.0, 1.0, (228, 135), (617, 322)),
    "cloak": (1.16, 1.16, (410, 95), (640, 320)),
    "sword": (0.87, 0.87, (67, 64), (557, 820)),
}

FAR_ARM = 55.0  # how far behind the near shoulder the far one is, along x
FAR_LEG = 45.0


def on_ref(part, x, y):
    sx, sy, (ax, ay), (rx, ry) = FIT[part]
    return rx + (x - ax) * sx, ry + (y - ay) * sy


def unit(x, y):
    return round((x - AXIS) / REF_PER_UNIT, 5), round((GROUND - y) / REF_PER_UNIT, 5)


def resize_premultiplied(im, size):
    """Resizes through premultiplied alpha, so no dark or grey fringe comes in from the empty pixels."""
    a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255.0
    a[..., :3] *= a[..., 3:]
    small = cv2.resize(a, size, interpolation=cv2.INTER_AREA)
    alpha = np.maximum(small[..., 3:], 1e-4)
    small[..., :3] = np.clip(small[..., :3] / alpha, 0, 1)
    return Image.fromarray((small * 255.0 + 0.5).astype(np.uint8), "RGBA")


def bone(name, parent, at, tip):
    x, y = unit(*at)
    tx, ty = unit(*tip)
    return {"name": name, "parent": parent, "x": x, "y": y, "tipX": tx, "tipY": ty}


def skeleton():
    shoulder = on_ref("arm", 55, 35)
    elbow = on_ref("arm", 45, 235)
    hand = on_ref("arm", 62, 470)
    fingertips = on_ref("arm", 65, 518)
    hip = on_ref("leg", 100, 60)
    knee = on_ref("leg", 78, 320)
    ankle = on_ref("leg", 58, 585)
    toe = on_ref("leg", 140, 635)
    bones = [
        bone("Hips", "", (AXIS, 680), (AXIS, 560)),
        bone("Spine", "Hips", (AXIS, 560), (AXIS, 420)),
        bone("Chest", "Spine", (AXIS, 420), (578, 215)),
        bone("Head", "Chest", (578, 205), (578, 60)),
    ]

    def shifted(p, dx):
        return p[0] + dx, p[1]

    for suffix, dx in (("Front", 0.0), ("Back", FAR_ARM)):
        bones.append(bone("UpperArm" + suffix, "Chest", shifted(shoulder, dx), shifted(elbow, dx)))
        bones.append(bone("ForeArm" + suffix, "UpperArm" + suffix, shifted(elbow, dx), shifted(fingertips, dx)))
    bones.append(bone("Sword", "ForeArmFront", hand, (hand[0] + 300, hand[1])))
    for suffix, dx in (("Front", 0.0), ("Back", FAR_LEG)):
        bones.append(bone("Thigh" + suffix, "Hips", shifted(hip, dx), shifted(knee, dx)))
        bones.append(bone("Shin" + suffix, "Thigh" + suffix, shifted(knee, dx), shifted(ankle, dx)))
        bones.append(bone("Boot" + suffix, "Shin" + suffix, shifted(ankle, dx), shifted(toe, dx)))
    # The cloak: three chains fanning from the shoulders to the hem, in the cloak's own pixels.
    chains = [((250, 40), (40, 930)), ((320, 40), (230, 940)), ((400, 60), (440, 800))]
    for c, (top, bottom) in enumerate(chains):
        joints = [on_ref("cloak", top[0] + (bottom[0] - top[0]) * s / 5, top[1] + (bottom[1] - top[1]) * s / 5) for s in range(6)]
        for s in range(5):
            bones.append(bone(f"Cloak{c}_{s}", "Chest" if s == 0 else f"Cloak{c}_{s - 1}", joints[s], joints[s + 1]))
    return bones


def origin(part, size, dx=0.0):
    """The world position, on the rig, of the bottom-left corner of a part."""
    sx, sy, (ax, ay), (rx, ry) = FIT[part]
    left = rx - ax * sx + dx
    bottom = ry - ay * sy + size[1] * sy
    return unit(left, bottom)


def main():
    if cv2 is None:
        sys.exit("fit_parts.py needs OpenCV.")
    parts_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "ai_art", "out", "hero_ansel_parts")
    os.makedirs(OUT, exist_ok=True)
    sizes = {}
    for name, (sx, sy, _, _) in FIT.items():
        im = Image.open(os.path.join(parts_dir, name + ".png"))
        sizes[name] = im.size
        written = (round(im.width * sx * ART_SCALE), round(im.height * sy * ART_SCALE))
        resize_premultiplied(im, written).save(os.path.join(OUT, f"ansel_{name}.png"), optimize=True)
        print(name, im.size, "->", written)

    def entry(name, file, order, skinned, bones, grid=(0, 0), far=False, dx=0.0, attach=""):
        x, y = origin(file, sizes[file], dx)
        return {"name": name, "file": f"ansel_{file}", "order": order, "skinned": skinned, "far": far, "bones": bones,
                "cols": grid[0], "rows": grid[1], "originX": x, "originY": y, "bone": attach}

    cloak_bones = ["Chest"] + [f"Cloak{c}_{s}" for c in range(3) for s in range(5)]
    parts = [
        entry("Cloak", "cloak", 1, True, cloak_bones, (12, 24)),
        entry("ArmBack", "arm", 3, True, ["UpperArmBack", "ForeArmBack"], (6, 20), far=True, dx=FAR_ARM),
        entry("LegBack", "leg", 5, True, ["ThighBack", "ShinBack", "BootBack"], (8, 26), far=True, dx=FAR_LEG),
        entry("LegFront", "leg", 7, True, ["ThighFront", "ShinFront", "BootFront"], (8, 26)),
        entry("Torso", "torso", 10, True, ["Hips", "Spine", "Chest"], (8, 18)),
        entry("Head", "head", 12, False, [], attach="Head"),
        entry("Sword", "sword", 14, False, [], attach="Sword"),
        entry("ArmFront", "arm", 15, True, ["UpperArmFront", "ForeArmFront"], (6, 20)),
        entry("Hood", "hood", 16, False, [], attach="Chest"),
    ]
    with open(os.path.join(OUT, "ansel_rig.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"generator": "tools/hero_ansel/fit_parts.py", "ppu": PPU, "skeleton": skeleton(), "parts": parts}, f, indent=1)
        f.write("\n")


if __name__ == "__main__":
    main()
