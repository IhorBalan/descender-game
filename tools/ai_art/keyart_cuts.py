"""Cut backdrop pieces out of the chosen key art (tools/ai_art/out/keyart) with the
Scenario background remover, one paid call per piece, cached in out/keyart_cuts.
The pieces are painted by the same model in the same style as the kit, so the
backdrops built from them (keyart_backdrop.py) match it without new generations.

  python tools/ai_art/keyart_cuts.py            cut every piece not cut yet
"""
import json
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
import scenario_gen as s  # noqa: E402

HERE = os.path.dirname(__file__)
KEYART = os.path.join(HERE, "out", "keyart")
OUT = os.path.join(HERE, "out", "keyart_cuts")

# name: (key art, box in its 2752x1536 pixels)
PIECES = {
    "tree_k2": (2, (1660, 0, 2620, 800)),
    "mount_k1": (1, (1027, 225, 1840, 663)),
    "mount_k2": (2, (1043, 289, 1669, 621)),
    "mount_k3": (3, (1188, 353, 1819, 600)),
    "isle_k4_temple": (4, (942, 118, 1391, 899)),
    "isle_k1_temple": (1, (1990, 364, 2752, 942)),
    "isle_k3_aqueduct": (3, (1102, 578, 1626, 920)),
    "isle_k3_temple": (3, (2215, 578, 2752, 1006)),
    "isle_k4_aqueduct": (4, (1284, 492, 2033, 888)),
    "ruin_k4_arches": (4, (2365, 524, 2752, 1070)),
    "ruin_k1_gazebo": (1, (1043, 712, 1214, 958)),
    "fore_k1_right": (1, (2097, 920, 2752, 1536)),
    "fore_k3_left": (3, (0, 1027, 770, 1536)),
}


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    only = sys.argv[1:]
    for name, (art, box) in PIECES.items():
        if only and name not in only:
            continue
        dest = os.path.join(OUT, f"{name}_cut.png")
        if os.path.exists(dest):
            continue
        crop = Image.open(os.path.join(KEYART, f"keyart_{art}.png")).convert("RGB").crop(box)
        crop.save(os.path.join(OUT, f"{name}_src.png"))
        try:
            asset = s.upload(crop, name)
            out = s.run_job(s.BACKGROUND_REMOVER, {"image": asset, "hdBackgroundRemoval": True})
            s.download(out[0]).save(dest)
            print(f"{name}: cut")
        except Exception as error:  # noqa: BLE001
            print(f"{name}: failed: {error}")
            if "429" in str(error):
                break


if __name__ == "__main__":
    main()
