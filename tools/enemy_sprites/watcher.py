"""Watcher: a flying 'biblically accurate' angel. One great eye, a turning ring, six wings.

From the art thread's prototype rig, with the hurt and death strips added."""
import math
from pix import Canvas, compose, pt, tint

W, H = 72, 60
C = (26, 23)
WING, WING_S, WING_D = (252, 252, 255), (204, 214, 242), (164, 176, 218)
GOLD, GOLD_S, GOLD_H = (242, 194, 72), (186, 128, 44), (255, 236, 160)
SCLERA, SCLERA_S = (250, 246, 236), (210, 200, 190)
IRIS, IRIS_H = (70, 150, 240), (150, 210, 255)
RED, RED_H = (240, 70, 60), (255, 190, 120)
PUPIL = (24, 16, 32)
LIGHT = (255, 250, 210)


def wings(c, flap):
    for ang, ln, col in [(-150, 22, WING_D), (150, 22, WING_D), (-105, 25, WING_S), (105, 25, WING_S),
                         (-50, 19, WING_S), (50, 19, WING_S)]:
        sgn = 1 if ang > 0 else -1
        a0 = ang + sgn * flap
        for k, d in enumerate((-12, -4, 4, 12)):
            a = a0 + d * sgn
            end = pt(C, a, ln - abs(d) // 3)
            c.limb(pt(C, a, 5), end, 4, col if k % 2 else WING, None, 2)


def ring_pts(phase, front):
    rx, ry = 14, 5 + 6 * abs(math.sin(phase))
    tilt = math.radians(20)
    out = []
    for k in range(48):
        a = 2 * math.pi * k / 48
        x, y = rx * math.cos(a), ry * math.sin(a)
        xr = x * math.cos(tilt) - y * math.sin(tilt)
        yr = x * math.sin(tilt) + y * math.cos(tilt)
        is_front = math.sin(a) > 0
        if is_front == front:
            out.append((C[0] + xr, C[1] + yr, k))
    return out


def ring(c, phase, front, t, broken=0):
    pts = ring_pts(phase, front)
    if broken:
        # The two halves drop apart and tilt as they fall.
        pts = [(x + (broken if x > C[0] else -broken) * 0.6, y + broken + (x - C[0]) * 0.15 * (1 if x > C[0] else -1), k)
               for x, y, k in pts]
    for x, y, k in pts:
        c.stamp(x, y, 2, GOLD if front else GOLD_S)
    for x, y, k in pts:
        if (k + t * 2) % 8 == 0:  # little eyes riding the ring
            c.dot(x, y, SCLERA)
            c.dot(x + 1, y, PUPIL)
        elif front and k % 4 == 1:
            c.dot(x, y - 1, GOLD_H)


def eye(c, fx, look, mode):
    cx, cy = C
    open_ = {"open": 5, "wide": 6, "shut": 1}[mode[0]]
    c.ellipse(cx, cy, 9, open_ + 1, PUPIL)
    c.ellipse(cx, cy, 8, open_, SCLERA_S)
    c.ellipse(cx + 0.5, cy - 0.5, 7, max(1, open_ - 1), SCLERA)
    if open_ > 1:
        iris, hi = (RED, RED_H) if mode[1] else (IRIS, IRIS_H)
        ix = cx + look
        c.ellipse(ix, cy, 3, min(3, open_ - 1), iris)
        c.dot(ix - 1, cy - 1, hi)
        c.rect(ix, cy - 1, ix + 1, cy + 1, PUPIL) if mode[0] != "wide" else c.dot(ix + 1, cy, PUPIL)
        if mode[1]:
            fx.glow(ix + 1, cy, 4, RED_H, 120)
    else:
        c.line((cx - 6, cy), (cx + 6, cy), 1, SCLERA_S)


def frame(p):
    body, fx, back = Canvas(W, H), Canvas(W, H), Canvas(W, H)
    global C
    C = (30, 29 + p.get("bob", 0))
    if not p.get("dim"):
        back.glow(C[0], C[1], 10, LIGHT, 45)
    wings(body, p.get("flap", 0))
    ring(body, p["phase"], False, p["t"], p.get("broken", 0))
    eye(body, fx, p.get("look", 1), p.get("mode", ("open", False)))
    ring(body, p["phase"], True, p["t"], p.get("broken", 0))
    if p.get("beam"):
        for x in range(C[0] + 6, W):
            for dy, a in ((0, 255), (-1, 200), (1, 200), (-2, 90), (2, 90)):
                fx.blend(x, C[1] + dy, (255, 255, 240) if abs(dy) < 1 else RED_H, a)
    img = compose(W, H, back, body, fx)
    if p.get("hurt"):
        img = tint(img, (255, 80, 90), 0.25)
    return img


def poses():
    anims = {}
    anims["idle"] = ([dict(t=i, phase=i * math.pi / 6, flap=[0, 8, 14, 8, 0, -6][i], bob=[0, -1, -1, 0, 1, 1][i],
                           look=[1, 1, 2, 2, 1, 0][i]) for i in range(6)], 8)
    anims["attack"] = ([
        dict(t=0, phase=0.3, flap=10, look=2, mode=("wide", True)),
        dict(t=1, phase=0.8, flap=16, look=3, mode=("wide", True)),
        dict(t=2, phase=1.3, flap=-4, look=3, mode=("wide", True), beam=True),
        dict(t=3, phase=1.8, flap=-6, look=3, mode=("wide", True), beam=True),
        dict(t=4, phase=2.3, flap=4, look=1, mode=("open", False)),
    ], 10)
    anims["hurt"] = ([dict(t=0, phase=0.5, flap=-10, mode=("shut", False), hurt=True),
                      dict(t=1, phase=0.7, flap=-4, look=0, mode=("open", False))], 8)
    # Death: the eye shuts, the wings fold, the ring breaks and falls away,
    # and the eye sinks, dark.
    anims["death"] = ([
        dict(t=0, phase=0.5, flap=-14, mode=("shut", False), hurt=True),
        dict(t=1, phase=0.9, flap=-26, mode=("shut", False), broken=4, dim=True),
        dict(t=2, phase=1.2, flap=-38, bob=5, mode=("shut", False), broken=10, dim=True),
        dict(t=3, phase=1.4, flap=-48, bob=11, mode=("shut", False), broken=18, dim=True),
    ], 8)
    return anims


def build():
    return {k: ([frame(p) for p in ps], fps) for k, (ps, fps) in poses().items()}
