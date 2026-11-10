"""Gilded Roman numerals, for the numbers cut into the coping under the orchard's trees.

The orchard is a little too tidy (docs/design/paradise.md, zone 2): every tree is numbered. Under
each tree of the orchard rows its number is cut into the coping's plain marble band and gilded,
like the garden's other trim. ParadiseGarden sets the numerals in a row, so four letters serve
every number. Each is drawn as strokes with serifs at four times size and reduced, gold lit along
its upper right (the sun is high on the right), with a fine dark line where it meets the stone.

  Kit/Architecture/numeral_I.png, numeral_V.png, numeral_X.png, numeral_L.png

  python tools/ai_art/tree_numerals.py
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

try:
    import cv2
except ImportError:  # OpenCV is only needed to run this script, not to import it (check_tools.py).
    cv2 = None

FOLDER = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise", "Kit", "Architecture")
# A numeral's height and stroke, in pixels at 128 a unit, its width by letter, and the empty
# margin round it (ParadiseGarden.NumeralMargin: the letters are set edge to edge less this).
HEIGHT = 30
STROKE = 5
WIDTHS = {"I": 12, "V": 26, "X": 26, "L": 22}
MARGIN = 2
GOLD = np.array((0.86, 0.68, 0.34), np.float32)
GOLD_LIGHT = np.array((1.0, 0.9, 0.6), np.float32)
CUT = np.array((0.32, 0.24, 0.14), np.float32)


def numeral(letter: str) -> np.ndarray:
    k = 4
    w, h, pad, s = WIDTHS[letter] * k, HEIGHT * k, MARGIN * k, STROKE * k
    img = Image.new("L", (w + 2 * pad, h + 2 * pad), 0)
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = pad, pad, pad + w, pad + h
    foot = s * 0.45
    if letter == "I":
        cx = (x0 + x1) / 2
        d.rectangle((cx - s / 2, y0, cx + s / 2, y1), fill=255)
        d.rectangle((x0, y0, x1, y0 + foot), fill=255)
        d.rectangle((x0, y1 - foot, x1, y1), fill=255)
    elif letter == "V":
        d.line((x0 + s * 0.8, y0, (x0 + x1) / 2, y1 - s * 0.2), fill=255, width=int(s))
        d.line((x1 - s * 0.6, y0, (x0 + x1) / 2, y1 - s * 0.2), fill=255, width=int(s * 0.6))
        d.rectangle((x0, y0, x0 + s * 1.9, y0 + foot), fill=255)
        d.rectangle((x1 - s * 1.6, y0, x1, y0 + foot), fill=255)
    elif letter == "X":
        d.line((x0 + s * 0.8, y0, x1 - s * 0.8, y1), fill=255, width=int(s))
        d.line((x1 - s * 0.6, y0, x0 + s * 0.6, y1), fill=255, width=int(s * 0.6))
        for y in (y0, y1 - foot):
            d.rectangle((x0, y, x0 + s * 1.9, y + foot), fill=255)
            d.rectangle((x1 - s * 1.9, y, x1, y + foot), fill=255)
    elif letter == "L":
        d.rectangle((x0 + s * 0.6, y0, x0 + s * 1.6, y1), fill=255)
        d.rectangle((x0, y0, x0 + s * 2.2, y0 + foot), fill=255)
        d.rectangle((x0, y1 - s * 0.9, x1, y1), fill=255)
        d.rectangle((x1 - s * 0.45, y1 - s * 1.8, x1, y1), fill=255)
    mask = np.asarray(img.filter(ImageFilter.GaussianBlur(k * 0.4)), dtype=np.float32) / 255.0
    small = cv2.resize(mask, (img.width // k, img.height // k), interpolation=cv2.INTER_AREA)
    gy, gx = np.gradient(cv2.GaussianBlur(small, (0, 0), 0.8))
    light = np.clip(-gx * 1.5 + gy * 1.5, -1.0, 1.0)[..., None]
    rgb = GOLD[None, None, :] + (GOLD_LIGHT - GOLD)[None, None, :] * np.clip(light, 0.0, 1.0)
    rgb *= 1.0 - 0.35 * np.clip(-light, 0.0, 1.0)
    outline = np.clip(cv2.dilate(small, np.ones((3, 3), np.uint8)) - small, 0.0, 1.0)[..., None]
    out = np.zeros(small.shape + (4,), np.float32)
    out[..., :3] = rgb * (1.0 - outline) + CUT * outline
    out[..., 3] = np.clip(small + outline[..., 0] * 0.6, 0.0, 1.0)
    return out


def main() -> None:
    for letter in WIDTHS:
        img = numeral(letter)
        Image.fromarray((np.clip(img, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), "RGBA").save(
            os.path.join(FOLDER, f"numeral_{letter}.png"), optimize=True
        )
    print(f"numerals {''.join(WIDTHS)} at {HEIGHT} px")


if __name__ == "__main__":
    main()
