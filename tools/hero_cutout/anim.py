"""Cut-out animation of the SpriteCook hero: layers are moved/rotated per frame."""
import math
import numpy as np
from PIL import Image
import layers as L

FW, FH = 216, 176          # frame size
OX, OY = 24, 6             # where the 168x168 source sits in the frame
ELBOW = (66, 86)
WING_ROOT = (74, 58)
HIPS = {"back": (71, 118), "front": (99, 118)}
EYE = (96, 41)

# Fill the torso where the forearm used to cover it, so the arm can move away cleanly.
_hole = L.ARM & L.poly([(68, 84), (84, 84), (86, 122), (68, 122)])


def _patch(img, hole, off=13):
    """Fill the hole by copying the tunic/pants texture from just to the right."""
    a = np.array(img)
    src = np.array(L.SRC)
    for y, x in zip(*np.nonzero(hole)):
        for k in (off, off + 4, off + 8):
            if x + k < a.shape[1] and not L.ARM[y, x + k] and src[y, x + k, 3]:
                a[y, x] = src[y, x + k]
                break
    return Image.fromarray(a)


BASE = _patch(L.layer(L.BODY), _hole)


def _edge(img, region):
    a = np.array(img)
    al = a[..., 3] > 0
    left_open = np.zeros_like(al)
    left_open[:, 1:] = ~al[:, :-1]
    m = region & al & left_open
    a[m] = (24, 16, 32, 255)
    return Image.fromarray(a)


BASE = _edge(BASE, _hole)
WING = L.layer(L.WING)
HALO = L.layer(L.HALO)
ARM = L.layer(L.ARM)
BACK_LEG = L.layer(L.BACK_LEG)
FRONT_LEG = L.layer(L.FRONT_LEG)
UPPER = np.zeros_like(L.BODY)
UPPER[:90] = True


def split_base():
    a = np.array(BASE)
    top, bot = a.copy(), a.copy()
    top[90:] = 0
    bot[:90] = 0
    return Image.fromarray(top), Image.fromarray(bot)


BASE_TOP, BASE_BOT = split_base()


def rot(img, ang, pivot, dx=0, dy=0):
    """Rotate about a pivot (degrees, + = counter-clockwise on screen), then offset."""
    out = img.rotate(ang, resample=Image.NEAREST, center=pivot)
    if dx or dy:
        out = shift(out, dx, dy)
    return out


def shift(img, dx, dy):
    out = Image.new("RGBA", img.size)
    out.paste(img, (dx, dy), img)
    return out


def glow(frame, x, y, r, col, a):
    px = frame.load()
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            d = math.hypot(dx, dy)
            if d > r:
                continue
            xx, yy = x + dx, y + dy
            if 0 <= xx < frame.width and 0 <= yy < frame.height:
                k = a * (1 - d / (r + 1)) / 255
                pr, pg, pb, pa = px[xx, yy]
                if pa == 0:
                    px[xx, yy] = (*col, int(255 * k))
                else:
                    px[xx, yy] = (int(pr + (col[0] - pr) * k), int(pg + (col[1] - pg) * k), int(pb + (col[2] - pb) * k), pa)


def slash(frame, cx, cy, a0, a1, r, width, alpha):
    """Light crescent; angles in screen degrees (0 = right, 90 = down)."""
    px = frame.load()
    steps = 120
    for i in range(steps + 1):
        t = i / steps
        a = math.radians(a0 + (a1 - a0) * t)
        th = math.sin(math.pi * t) * width
        for k in range(int(th) + 1):
            x = int(round(cx + (r - k) * math.cos(a)))
            y = int(round(cy + (r - k) * math.sin(a)))
            if 0 <= x < frame.width and 0 <= y < frame.height:
                col = (255, 255, 255) if k < 2 else (140, 235, 255)
                al = int(alpha * (1 - k / (th + 2)) * (0.5 + 0.5 * t))
                pr, pg, pb, pa = px[x, y]
                if al > pa:
                    px[x, y] = (*col, al)


def tint(img, col, k):
    a = np.array(img).astype(float)
    m = a[..., 3] > 0
    a[m, :3] = a[m, :3] + (np.array(col) - a[m, :3]) * k
    return Image.fromarray(a.astype(np.uint8))


def frame(p):
    f = Image.new("RGBA", (FW, FH))
    b = p.get("breath", 0)
    bob = p.get("bob", 0)
    dx = p.get("dx", 0)

    def put(img, ox=0, oy=0):
        f.alpha_composite(img, (OX + dx + ox, OY + bob + oy))

    put(rot(WING, p.get("wing", 0), WING_ROOT), 0, b)
    put(rot(BACK_LEG, p.get("back", 0), HIPS["back"]))
    put(rot(FRONT_LEG, p.get("front", 0), HIPS["front"]))
    put(BASE_BOT)
    put(BASE_TOP, 0, b)
    put(rot(ARM, p.get("arm", 0), ELBOW), 0, b)
    put(HALO, 0, b + p.get("halo", 0))
    glow(f, OX + dx + EYE[0], OY + bob + b + EYE[1], 3, (120, 255, 255), p.get("eye", 90))
    if p.get("slash"):
        a0, a1, al = p["slash"]
        slash(f, OX + dx + ELBOW[0], OY + bob + b + ELBOW[1], a0, a1, 78, 9, al)
    if p.get("lean"):
        f = f.rotate(p["lean"], resample=Image.NEAREST, center=(OX + 85, OY + 166))
    if p.get("hurt"):
        f = tint(f, (255, 70, 80), p["hurt"])
    return f


def poses():
    anims = {}
    idle = []
    for i in range(8):
        ph = i / 8 * 2 * math.pi
        idle.append(dict(breath=1 if math.sin(ph) > 0.3 else 0,
                         wing=-3 * (1 - math.cos(ph)) / 2 * 2,
                         halo=round(-1.5 * math.sin(ph + 1)),
                         eye=70 + 50 * (0.5 + 0.5 * math.sin(ph))))
    anims["idle"] = (idle, 8)

    walk = []
    for i in range(8):
        ph = i / 8 * 2 * math.pi
        s = math.sin(ph)
        walk.append(dict(front=18 * s, back=-18 * s, bob=-2 if abs(s) < 0.4 else 0,
                         wing=-2 - 3 * abs(s), halo=round(math.sin(ph * 2)), arm=-4 * s, eye=90))
    anims["walk"] = (walk, 10)

    anims["attack"] = ([
        dict(arm=60, wing=-4, dx=-1, eye=110),
        dict(arm=118, wing=-8, dx=-3, lean=3, front=-6, back=6, eye=140),
        dict(arm=128, wing=-9, dx=-3, lean=4, front=-8, back=8, eye=160),
        dict(arm=-8, wing=6, dx=6, lean=-4, front=14, back=-10, slash=(-150, 30, 240), eye=200),
        dict(arm=-22, wing=4, dx=7, lean=-4, front=14, back=-10, slash=(-20, 45, 120), eye=150),
        dict(arm=-8, wing=1, dx=3, lean=-1, front=6, back=-4, eye=110),
    ], 14)

    anims["jump"] = ([
        dict(bob=2, front=-20, back=25, wing=6, arm=10, eye=110),
        dict(bob=-6, front=28, back=-6, wing=-16, arm=25, halo=1, eye=110),
        dict(bob=-4, front=10, back=-14, wing=8, arm=15, halo=-1, eye=110),
    ], 8)

    anims["hurt"] = ([
        dict(dx=-4, lean=6, wing=8, arm=20, hurt=0.45, eye=0, halo=1),
        dict(dx=-2, lean=3, wing=4, arm=8, hurt=0.15, eye=40),
    ], 10)
    return anims


def build():
    return {k: ([frame(p) for p in ps], fps) for k, (ps, fps) in poses().items()}


if __name__ == "__main__":
    anims = build()
    rows = []
    for name, (frames, _) in anims.items():
        s = Image.new("RGBA", (FW * len(frames), FH))
        for i, fr in enumerate(frames):
            s.alpha_composite(fr, (i * FW, 0))
        rows.append(s)
    w = max(r.width for r in rows)
    sheet = Image.new("RGBA", (w, FH * len(rows)), (90, 100, 130, 255))
    for i, r in enumerate(rows):
        sheet.alpha_composite(r, (0, i * FH))
    sheet.save("preview.png")
