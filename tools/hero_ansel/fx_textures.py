"""The textures of Ansel's effects: soft light, dust, the sword's arc, a shockwave ring, a streak.

Painted by numbers in the soft style of the Paradise art (no hard pixels), white so the game
tints them. Written to Assets/Art/Paradise/Ansel/Fx.

    python tools/hero_ansel/fx_textures.py

Needs numpy and Pillow.
"""
import os

import numpy as np
from PIL import Image, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Art", "Paradise", "Ansel", "Fx")


def save(name, alpha, glow=None):
    """White texture with the given alpha; glow tints the core a little warmer for light."""
    h, w = alpha.shape
    rgb = np.ones((h, w, 3), np.float32)
    if glow is not None:
        rgb = rgb * (1.0 - glow[..., None] * np.array([0.0, 0.05, 0.18]))
    out = np.dstack([rgb, np.clip(alpha, 0, 1)])
    Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"), optimize=True)


def grid(n):
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    return (x + 0.5) / n * 2 - 1, (y + 0.5) / n * 2 - 1


def noise(n, scale, seed):
    """Soft value noise: random cells, upscaled smoothly."""
    rng = np.random.default_rng(seed)
    cells = rng.random((max(2, n // scale), max(2, n // scale))).astype(np.float32)
    return np.asarray(Image.fromarray((cells * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC)).astype(np.float32) / 255


def soft():
    x, y = grid(64)
    r = np.sqrt(x * x + y * y)
    a = np.clip(1 - r, 0, 1) ** 2.2
    save("fx_soft", a, glow=a)


def dust():
    n = 128
    x, y = grid(n)
    r = np.sqrt(x * x + y * y)
    base = np.clip(1 - r * 1.15, 0, 1) ** 1.4
    cloud = 0.55 * noise(n, 16, 1) + 0.3 * noise(n, 6, 2) + 0.15 * noise(n, 3, 3)
    a = np.clip(base * (0.35 + cloud) - 0.1, 0, 1)
    a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2))).astype(np.float32) / 255
    save("fx_dust", a * 0.9)


def slash():
    """A crescent: a thick, bright leading edge thinning to a tail, along a quarter circle."""
    n = 256
    x, y = grid(n)
    r = np.sqrt(x * x + y * y)
    angle = np.degrees(np.arctan2(-y, x))  # 0 to the right, 90 up
    along = np.clip((angle + 60) / 150, 0, 1)  # 0 at the tail, 1 at the head
    inside = (angle > -60) & (angle < 90)
    width = 0.04 + 0.16 * along
    band = np.clip(1 - np.abs(r - 0.78) / width, 0, 1) ** 1.5
    a = np.where(inside, band * (along ** 0.8), 0)
    a = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255
    save("fx_slash", a, glow=a)


def ring():
    x, y = grid(256)
    r = np.sqrt(x * x + y * y)
    a = np.clip(1 - np.abs(r - 0.82) / 0.12, 0, 1) ** 2 + 0.25 * np.clip(1 - r, 0, 1) ** 3
    save("fx_ring", np.clip(a, 0, 1), glow=np.clip(a, 0, 1))


def streak():
    w, h = 128, 32
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u = (x + 0.5) / w
    v = (y + 0.5) / h * 2 - 1
    a = np.clip(1 - np.abs(v) * 1.6, 0, 1) ** 2 * np.sin(np.pi * u) ** 0.7 * (0.3 + 0.7 * u)
    save("fx_streak", a, glow=a)


def main():
    os.makedirs(OUT, exist_ok=True)
    for make in (soft, dust, slash, ring, streak):
        make()
        print(make.__name__)


if __name__ == "__main__":
    main()
