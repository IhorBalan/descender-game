"""Foreground foliage silhouettes keyed out of the key art's dark corner leaves
(they are much darker than the bright clouds behind them, so a luminance key
separates them without a paid background remover). Output: out/keyart_cuts/fg_*.png.

  python tools/ai_art/keyart_foliage.py
"""
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter

HERE = os.path.dirname(__file__)
KEYART = os.path.join(HERE, "out", "keyart")
OUT = os.path.join(HERE, "out", "keyart_cuts")

# name: (key art, box in its 2752x1536 pixels, the region the foreground leaves fill as a
# polygon in 0..1 of the box: anything of the scene behind outside it is dropped)
PIECES = {
    "fg_leaves_a": (1, (2080, 900, 2752, 1536), [(0, 0.66), (0.52, 0.52), (0.64, 0.22), (0.82, 0), (1, 0), (1, 1), (0, 1)]),
    "fg_leaves_b": (3, (0, 1000, 800, 1536), [(0, 0), (0.24, 0), (0.36, 0.42), (0.6, 0.62), (1, 0.68), (1, 1), (0, 1)]),
    "fg_leaves_d": (4, (2200, 950, 2752, 1536), [(0, 0.48), (0.5, 0.42), (0.64, 0.14), (0.86, 0), (1, 0), (1, 1), (0, 1)]),
}


def key(img: Image.Image, low: int = 80, high: int = 125) -> Image.Image:
    """
    Opaque where dark, clear where bright, soft between; then only the dark regions that
    reach the bottom edge are kept (the leaves rising into the frame), dropping the
    shadows and greenery of the scene behind them.
    """
    rgb = img.convert("RGB")
    lum = rgb.convert("L").filter(ImageFilter.GaussianBlur(1))
    soft = lum.point(lambda v: 255 if v <= low else 0 if v >= high else round(255 * (high - v) / (high - low)))
    solid = lum.point(lambda v: 255 if v < (low + high) // 2 else 0)
    w, h = solid.size
    for x in range(0, w, 2):
        if solid.getpixel((x, h - 1)) == 255:
            ImageDraw.floodfill(solid, (x, h - 1), 128)
    reach = solid.point(lambda v: 255 if v == 128 else 0).filter(ImageFilter.MaxFilter(5))
    alpha = ImageChops.multiply(soft, reach).filter(ImageFilter.GaussianBlur(0.8))
    out = rgb.copy()
    out.putalpha(alpha)
    return out


def main() -> None:
    for name, (art, box, region) in PIECES.items():
        src = Image.open(os.path.join(KEYART, f"keyart_{art}.png")).crop(box)
        cut = key(src)
        keep = Image.new("L", cut.size, 0)
        ImageDraw.Draw(keep).polygon([(x * cut.width, y * cut.height) for x, y in region], fill=255)
        keep = keep.filter(ImageFilter.GaussianBlur(12))
        # The crop cut leaves at the box's sides and top: fade those edges, not the bottom.
        edge = Image.new("L", cut.size, 0)
        ImageDraw.Draw(edge).rectangle((28, 28, cut.width - 29, cut.height + 40), fill=255)
        keep = ImageChops.multiply(keep, edge.filter(ImageFilter.GaussianBlur(14)))
        cut.putalpha(ImageChops.multiply(cut.getchannel("A"), keep))
        cut = cut.crop(cut.getchannel("A").getbbox())
        cut.save(os.path.join(OUT, f"{name}.png"))
        print(name, cut.size)


if __name__ == "__main__":
    main()
