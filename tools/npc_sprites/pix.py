"""Tiny pixel-art rig helpers: forward-kinematics limbs, outline pass, sheets."""
import math
from PIL import Image, ImageDraw

OUTLINE = (24, 16, 32, 255)


def rgba(c, a=255):
    return c if len(c) == 4 else (c[0], c[1], c[2], a)


def pt(p, ang, ln):
    """Angle in degrees: 0 = straight down, +90 = forward (+x), 180 = up."""
    r = math.radians(ang)
    return (p[0] + ln * math.sin(r), p[1] + ln * math.cos(r))


def add(p, dx, dy):
    return (p[0] + dx, p[1] + dy)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        self.px = self.img.load()
        self.d = ImageDraw.Draw(self.img)

    def dot(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            self.px[x, y] = rgba(c)

    def blend(self, x, y, c, a):
        x, y = int(round(x)), int(round(y))
        if not (0 <= x < self.w and 0 <= y < self.h):
            return
        r, g, b, oa = self.px[x, y]
        if oa == 0:
            self.px[x, y] = (c[0], c[1], c[2], a)
        else:
            t = a / 255
            self.px[x, y] = (int(r + (c[0] - r) * t), int(g + (c[1] - g) * t),
                             int(b + (c[2] - b) * t), max(oa, a))

    def stamp(self, x, y, w, c):
        ix = int(round(x)) - w // 2
        iy = int(round(y)) - w // 2
        for dx in range(w):
            for dy in range(w):
                self.dot(ix + dx, iy + dy, c)

    def line(self, p0, p1, w, c, w1=None):
        """Thick line from p0 to p1; w1 tapers the width toward p1."""
        w1 = w if w1 is None else w1
        n = max(2, int(math.hypot(p1[0] - p0[0], p1[1] - p0[1]) * 2) + 1)
        for i in range(n + 1):
            t = i / n
            ww = int(round(w + (w1 - w) * t))
            self.stamp(p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t, max(1, ww), c)

    def limb(self, p0, p1, w, c, edge=None, w1=None):
        if edge:
            self.line(p0, p1, w + 2, edge, None if w1 is None else w1 + 2)
        self.line(p0, p1, w, c, w1)

    def poly(self, pts, c):
        self.d.polygon([(round(x), round(y)) for x, y in pts], fill=rgba(c))

    def ellipse(self, cx, cy, rx, ry, c):
        self.d.ellipse([round(cx - rx), round(cy - ry), round(cx + rx), round(cy + ry)], fill=rgba(c))

    def rect(self, x0, y0, x1, y1, c):
        self.d.rectangle([round(x0), round(y0), round(x1), round(y1)], fill=rgba(c))

    def glow(self, x, y, r, c, a=110):
        for dx in range(-r, r + 1):
            for dy in range(-r, r + 1):
                dd = math.hypot(dx, dy)
                if dd <= r + 0.2:
                    self.blend(x + dx, y + dy, c, int(a * (1 - dd / (r + 1))))


def outlined(body, color=OUTLINE):
    src = body.img
    w, h = src.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sp, op = src.load(), out.load()
    for y in range(h):
        for x in range(w):
            if sp[x, y][3] > 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and sp[nx, ny][3] > 128:
                    op[x, y] = color
                    break
    out.alpha_composite(src)
    return out


def compose(w, h, back_fx=None, body=None, fx=None, outline=OUTLINE):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    if back_fx is not None:
        img.alpha_composite(back_fx.img)
    if body is not None:
        img.alpha_composite(outlined(body, outline))
    if fx is not None:
        img.alpha_composite(fx.img)
    return img


def tint(img, c, amount):
    """Flash a frame toward a colour (used for hurt frames)."""
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = (int(r + (c[0] - r) * amount), int(g + (c[1] - g) * amount),
                            int(b + (c[2] - b) * amount), a)
    return out


def strip(frames):
    w, h = frames[0].size
    s = Image.new("RGBA", (w * len(frames), h), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        s.alpha_composite(f, (i * w, 0))
    return s


def scale(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)
