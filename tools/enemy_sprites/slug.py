"""Slug: the devourer of Gluttony. A bloated, wet maw on a crawling gut.

Low and long, it lies like a heap of offal until the hero is close, then
rears and bites. Dies deflating into a puddle."""
import math
from pix import Canvas, compose, add, tint

W, H = 72, 40
FLESH, FLESH_S, FLESH_D = (150, 126, 96), (112, 88, 66), (72, 54, 44)
BELLY, BELLY_H = (190, 162, 128), (214, 194, 164)
GUM, MOUTH = (150, 44, 56), (44, 10, 18)
TOOTH = (236, 226, 200)
EYE, EYE_D = (230, 210, 60), (120, 90, 20)
SLIME = (170, 200, 120)
EDGE = (24, 16, 32)


def body_shape(c, tail, head, p):
    """The gut: a chain of swells from the tail to the head, rippled by `wave`."""
    n = 7
    for i in range(n):
        t = i / (n - 1)
        x = tail[0] + (head[0] - tail[0]) * t
        y = tail[1] + (head[1] - tail[1]) * t
        swell = 4 + 6 * math.sin(math.pi * (0.15 + 0.85 * t)) + p.get("rear", 0) * t * 0.5
        ripple = math.sin(p.get("wave", 0) + i * 1.3) * p.get("ripple", 0)
        c.ellipse(x, y - swell * 0.55 + ripple, swell * 1.3, swell, FLESH_S if i % 2 else FLESH)
    for i in range(n - 1):
        t = i / (n - 1)
        x = tail[0] + (head[0] - tail[0]) * t
        y = tail[1] + (head[1] - tail[1]) * t
        swell = 4 + 6 * math.sin(math.pi * (0.15 + 0.85 * t))
        c.ellipse(x + 1, y - swell * 0.3, swell * 0.9, swell * 0.5, BELLY)
        c.dot(x - 1, y - swell * 1.1, BELLY_H)
    # the slime it drags
    for k in range(4):
        c.dot(tail[0] - 6 - k * 3, tail[1] + 1 - (k % 2), SLIME)


def maw(c, fx, head, p):
    x, y = head
    gape = p.get("gape", 2)
    rear = p.get("rear", 0)
    hy = y - 8 - rear
    # the head swell, a lip that pulls up when it bites
    c.ellipse(x + 2, hy, 10, 8, FLESH)
    c.ellipse(x + 4, hy + 2, 7, 5, BELLY)
    if p.get("dead"):
        c.line((x + 2, hy + 4), (x + 12, hy + 5), 1, MOUTH)
        c.line((x + 4, hy - 2), (x + 7, hy - 2), 1, FLESH_D)
        c.line((x + 1, hy - 3), (x + 3, hy - 1), 1, FLESH_D)
        return
    # the mouth: wide, dark, lined with teeth
    c.poly([(x + 2, hy + 1), (x + 13, hy - 1 - gape), (x + 13, hy + 3 + gape), (x + 2, hy + 5)], GUM)
    c.poly([(x + 4, hy + 2), (x + 12, hy - gape), (x + 12, hy + 2 + gape), (x + 4, hy + 4)], MOUTH)
    for k in range(4):
        tx = x + 5 + k * 2
        c.dot(tx, hy + 1 - gape * (k + 1) / 5, TOOTH)
        c.dot(tx, hy + 3 + gape * (k + 1) / 5, TOOTH)
    if gape >= 4:
        c.dot(x + 9, hy + 2, SLIME)
    # eye stalks
    for sx, ang in ((x - 1, -0.6), (x + 4, -0.3)):
        ex, ey = sx + math.sin(ang) * 4, hy - 7 - math.cos(ang) * 3 + p.get("stalk", 0)
        c.line((sx, hy - 5), (ex, ey), 2, FLESH_S)
        c.dot(ex, ey, EYE)
        c.dot(ex + 1, ey, EYE_D)
        fx.blend(ex, ey - 1, EYE, 60)


def frame(p):
    body, fx = Canvas(W, H), Canvas(W, H)
    dx = p.get("dx", 0)
    bob = p.get("bob", 0)
    flat = p.get("flat", 0)
    tail = (14 + dx, 36 + bob + flat)
    head = (46 + dx, 36 + bob)
    if flat:
        # death: the swells sink and spread
        for i in range(6):
            x = tail[0] + (head[0] - tail[0]) * i / 5
            c_w = 12 - flat
            body.ellipse(x, 36 + flat * 0.4, c_w + 2, max(2, 7 - flat), FLESH_S)
            body.ellipse(x + 1, 35 + flat * 0.4, c_w, max(1, 5 - flat), FLESH)
        for k in range(flat * 3):
            body.dot(10 + k * 2.3, 38, SLIME)
        maw(body, fx, (head[0], head[1] + flat * 0.6), dict(p, rear=-flat))
    else:
        body_shape(body, tail, head, p)
        maw(body, fx, head, p)
    img = compose(W, H, None, body, fx)
    if p.get("hurt"):
        img = tint(img, (255, 255, 255), 0.35)
    return img


def poses():
    anims = {}
    anims["idle"] = ([dict(bob=[0, 0, -1, -1][i], wave=i * 0.8, ripple=0.6, gape=[2, 2, 3, 2][i],
                           stalk=[0, -1, -1, 0][i]) for i in range(4)], 5)
    anims["walk"] = ([dict(wave=i * math.pi / 2, ripple=1.6, dx=[0, 1, 2, 1][i], gape=2) for i in range(4)], 6)
    anims["attack"] = ([
        dict(rear=6, gape=1, dx=-2, stalk=-2),
        dict(rear=9, gape=6, dx=6, stalk=-3),
        dict(rear=5, gape=5, dx=9, stalk=-1),
        dict(rear=1, gape=2, dx=4),
    ], 8)
    anims["hurt"] = ([dict(rear=-2, gape=5, dx=-3, hurt=True), dict(rear=-1, gape=3, dx=-2)], 8)
    anims["death"] = ([
        dict(rear=4, gape=6, dx=-2, hurt=True),
        dict(flat=1, gape=4),
        dict(flat=2, gape=2, dead=True),
        dict(flat=3, dead=True),
    ], 8)
    return anims


def build():
    return {k: ([frame(p) for p in ps], fps) for k, (ps, fps) in poses().items()}
