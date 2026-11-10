"""Fits the cut parts of the new Ansel to his approved proportions and writes the rig layout.

The parts sheet draws each part at its own scale, so every part is measured here against the
approved full-body picture (reference.png: 1024 x 1536, y down) and resized to match. Writes
Assets/Art/Paradise/Ansel/ansel_*.png, a normal map ansel_*_n.png for each (so the scene's
lights shape him), and ansel_rig.json, which ParadiseHero builds the rig from.

The far arm and leg are their own pictures, not the near ones tinted: a little smaller, cooler,
darker and softer, as limbs on the far side of a body read.

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

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "ai_art"))
import normals  # noqa: E402

OUT = os.path.join(HERE, "..", "..", "Assets", "Art", "Paradise", "Ansel")
DEFAULT_PARTS = os.path.join(HERE, "..", "ai_art", "out", "hero_ansel_parts")

GROUND = 1455.0  # the soles, on the reference
AXIS = 585.0  # the body's axis, on the reference
REF_PER_UNIT = 860.0  # reference pixels in one world unit: he stands 1.63 units tall
ART_SCALE = 0.5  # written pixels per reference pixel
PPU = REF_PER_UNIT * ART_SCALE

FAR_ARM = 55.0  # how far behind the near shoulder the far one is, along x
FAR_LEG = 45.0
FAR_ARM_SCALE = 0.94  # the far limbs are a little smaller, as further away
FAR_LEG_SCALE = 0.97

# How much broader than drawn: a sturdier build through the chest, arms and legs, widened
# across (not lengthened), so he keeps his height and his joints stay where they were.
BULK_TORSO = 1.16
BULK_ARM = 1.2
BULK_LEG = 1.16
BULK_CLOAK = 1.06

# Empty pixels written around every part, so the silhouette drawn behind him (an ink line and
# a halo of light, Descender/Hero Silhouette) has room outside the paint.
PAD = 40

# part: (source picture, x scale, y scale, a point on the part, where that point sits on the reference)
FIT = {
    "arm": ("arm", BULK_ARM, 1.15, (55, 35), (550, 320)),
    "arm_far": ("arm", BULK_ARM * FAR_ARM_SCALE, 1.15 * FAR_ARM_SCALE, (55, 35), (550 + FAR_ARM, 320)),
    "leg": ("leg", BULK_LEG, 1.31, (100, 60), (565, 680)),
    "leg_far": ("leg", BULK_LEG * FAR_LEG_SCALE, 1.31 * FAR_LEG_SCALE, (100, 60), (565 + FAR_LEG, 680)),
    "torso": ("torso", 0.9 * BULK_TORSO, 1.15, (90, 10), (585, 205)),
    "head": ("head", 0.88, 0.88, (105, 190), (578, 205)),
    "hood": ("hood", 1.0, 1.0, (228, 135), (617, 322)),
    "cloak": ("cloak", 1.16 * BULK_CLOAK, 1.16, (410, 95), (640, 320)),
    "sword": ("sword", 0.87, 0.87, (67, 64), (557, 820)),
}

# Normal map settings per part, in the terms of tools/ai_art/normals.py.
LIMB = dict(relief=(2, 6, 0), bevel=5, dome=0, wrap=(False, False), lathe=True)
SHAPE = {
    "arm": LIMB,
    "arm_far": LIMB,
    "leg": LIMB,
    "leg_far": LIMB,
    "torso": dict(relief=(2, 7, 4), bevel=5, dome=0, wrap=(False, False), lathe=True),
    "head": dict(relief=(2, 5, 0), bevel=4, dome=16, wrap=(False, False)),
    "hood": dict(relief=(2, 8, 8), bevel=5, dome=8, wrap=(False, False)),
    "cloak": dict(relief=(2, 8, 10), bevel=4, dome=0, wrap=(False, False)),
    "sword": dict(relief=(1, 3, 0), bevel=2, dome=0, wrap=(False, False), lathe=True),
}


def on_ref(part, x, y):
    _, sx, sy, (ax, ay), (rx, ry) = FIT[part]
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


def far_side(im):
    """A limb on the far side of the body: darker and cooler, flatter in contrast, a touch soft."""
    a = np.asarray(im).astype(np.float32) / 255.0
    rgb = a[..., :3]
    luma = (rgb * np.array([0.3, 0.59, 0.11])).sum(axis=2, keepdims=True)
    rgb = luma + (rgb - luma) * 0.8  # less saturated
    rgb = 0.5 + (rgb - 0.5) * 0.85  # less contrast
    rgb = rgb * np.array([0.66, 0.68, 0.80]) + np.array([0.02, 0.025, 0.05])
    rgb = cv2.GaussianBlur(np.clip(rgb, 0, 1), (0, 0), 0.6)
    out = np.dstack([rgb, a[..., 3:]])
    return Image.fromarray((np.clip(out, 0, 1) * 255.0 + 0.5).astype(np.uint8), "RGBA")


def bone(name, parent, at, tip):
    x, y = unit(*at)
    tx, ty = unit(*tip)
    return {"name": name, "parent": parent, "x": x, "y": y, "tipX": tx, "tipY": ty}


def skeleton():
    bones = [
        bone("Hips", "", (AXIS, 680), (AXIS, 560)),
        bone("Spine", "Hips", (AXIS, 560), (AXIS, 420)),
        bone("Chest", "Spine", (AXIS, 420), (578, 215)),
        bone("Head", "Chest", on_ref("head", 105, 190), on_ref("head", 125, 40)),
        # Hair: two locks that sway on their own, the back tufts and the top.
        bone("HairBack", "Head", on_ref("head", 62, 78), on_ref("head", 8, 118)),
        bone("HairTop", "Head", on_ref("head", 140, 32), on_ref("head", 60, 6)),
        # The hood, thrown back over the shoulders, jostles on its own bone.
        bone("Hood", "Chest", on_ref("hood", 190, 100), on_ref("hood", 40, 180)),
        # The tunic's front and back tails swing below the belt.
        bone("TailBack", "Hips", on_ref("torso", 60, 330), on_ref("torso", 55, 505)),
        bone("TailFront", "Hips", on_ref("torso", 195, 330), on_ref("torso", 205, 500)),
    ]
    for suffix, part in (("Front", "arm"), ("Back", "arm_far")):
        bones.append(bone("UpperArm" + suffix, "Chest", on_ref(part, 55, 35), on_ref(part, 45, 235)))
        bones.append(bone("ForeArm" + suffix, "UpperArm" + suffix, on_ref(part, 45, 235), on_ref(part, 52, 420)))
        bones.append(bone("Hand" + suffix, "ForeArm" + suffix, on_ref(part, 52, 420), on_ref(part, 65, 518)))
    grip = on_ref("arm", 62, 470)
    bones.append(bone("Sword", "HandFront", grip, (grip[0] + 300, grip[1])))
    for suffix, part in (("Front", "leg"), ("Back", "leg_far")):
        bones.append(bone("Thigh" + suffix, "Hips", on_ref(part, 100, 60), on_ref(part, 78, 320)))
        bones.append(bone("Shin" + suffix, "Thigh" + suffix, on_ref(part, 78, 320), on_ref(part, 58, 585)))
        bones.append(bone("Boot" + suffix, "Shin" + suffix, on_ref(part, 58, 585), on_ref(part, 140, 635)))
    # The cloak: three chains fanning from the shoulders to the hem, in the cloak's own pixels.
    chains = [((250, 40), (40, 930)), ((320, 40), (230, 940)), ((400, 60), (440, 800))]
    for c, (top, bottom) in enumerate(chains):
        joints = [on_ref("cloak", top[0] + (bottom[0] - top[0]) * s / 5, top[1] + (bottom[1] - top[1]) * s / 5) for s in range(6)]
        for s in range(5):
            bones.append(bone(f"Cloak{c}_{s}", "Chest" if s == 0 else f"Cloak{c}_{s - 1}", joints[s], joints[s + 1]))
    return bones


def origin(part, size):
    """The world position, on the rig, of the bottom-left corner of a part (its padding included)."""
    _, sx, sy, (ax, ay), (rx, ry) = FIT[part]
    pad = PAD / ART_SCALE
    return unit(rx - ax * sx - pad, ry - ay * sy + size[1] * sy + pad)


def padded(im):
    """The part with PAD empty pixels on every side."""
    out = Image.new("RGBA", (im.width + 2 * PAD, im.height + 2 * PAD), (0, 0, 0, 0))
    out.paste(im, (PAD, PAD))
    return out


def main():
    if cv2 is None:
        sys.exit("fit_parts.py needs OpenCV.")
    parts_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_PARTS
    os.makedirs(OUT, exist_ok=True)
    sizes = {}
    for name, (source, sx, sy, _, _) in FIT.items():
        im = Image.open(os.path.join(parts_dir, source + ".png"))
        sizes[name] = im.size
        written = (round(im.width * sx * ART_SCALE), round(im.height * sy * ART_SCALE))
        art = resize_premultiplied(im, written)
        if name.endswith("_far"):
            art = far_side(art)
        art = padded(art)
        art.save(os.path.join(OUT, f"ansel_{name}.png"), optimize=True)
        normal = normals.normal_map(np.asarray(art), SHAPE[name])
        Image.fromarray(normal, "RGB").save(os.path.join(OUT, f"ansel_{name}_n.png"), optimize=True)
        print(name, im.size, "->", written)

    def entry(name, file, order, skinned, bones, grid=(0, 0), attach=""):
        x, y = origin(file, sizes[file])
        return {"name": name, "file": f"ansel_{file}", "order": order, "skinned": skinned, "bones": bones,
                "cols": grid[0], "rows": grid[1], "originX": x, "originY": y, "bone": attach}

    cloak_bones = ["Chest"] + [f"Cloak{c}_{s}" for c in range(3) for s in range(5)]
    parts = [
        entry("Cloak", "cloak", 1, True, cloak_bones, (12, 24)),
        entry("ArmBack", "arm_far", 3, True, ["UpperArmBack", "ForeArmBack", "HandBack"], (6, 22)),
        entry("LegBack", "leg_far", 5, True, ["ThighBack", "ShinBack", "BootBack"], (8, 26)),
        entry("LegFront", "leg", 7, True, ["ThighFront", "ShinFront", "BootFront"], (8, 26)),
        entry("Torso", "torso", 10, True, ["Hips", "Spine", "Chest", "TailBack", "TailFront"], (10, 20)),
        entry("Head", "head", 12, True, ["Head", "HairBack", "HairTop"], (12, 12)),
        entry("Sword", "sword", 14, False, [], attach="Sword"),
        entry("ArmFront", "arm", 15, True, ["UpperArmFront", "ForeArmFront", "HandFront"], (6, 22)),
        entry("Hood", "hood", 16, False, [], attach="Hood"),
    ]
    with open(os.path.join(OUT, "ansel_rig.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump({"generator": "tools/hero_ansel/fit_parts.py", "ppu": PPU, "skeleton": skeleton(), "parts": parts}, f, indent=1)
        f.write("\n")


if __name__ == "__main__":
    main()
