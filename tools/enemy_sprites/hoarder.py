"""Hoarder: the miser of Greed. A hunched demon under a sack of what it took.

Shuffles bent double under the sack, throws from it, and dies spilling it."""
import math
from pix import Canvas, compose, pt, add, tint

W, H = 64, 48
SKIN, SKIN_S, SKIN_D = (120, 108, 92), (84, 74, 64), (54, 46, 42)
SACK, SACK_S, SACK_D = (138, 96, 56), (100, 66, 40), (66, 42, 28)
ROPE = (190, 160, 110)
COIN, COIN_H = (242, 194, 72), (255, 236, 160)
EYE = (255, 200, 60)
HORN = (200, 190, 170)
EDGE = (24, 16, 32)


def leg(c, hip, thigh, shin, col, edge=None):
    knee = pt(hip, thigh, 6)
    ankle = pt(knee, shin, 6)
    c.limb(hip, knee, 3, col, edge)
    c.limb(knee, ankle, 3, col, edge)
    c.limb(ankle, add(ankle, 4, 0), 2, col, edge)


def arm(c, sh, up, low, col, edge=None):
    el = pt(sh, up, 6)
    hand = pt(el, low, 6)
    c.limb(sh, el, 3, col, edge)
    c.limb(el, hand, 2, col, edge)
    for d in (-30, 0, 30):
        c.line(hand, pt(hand, low + d, 2.5), 1, HORN)
    return hand


def sack(c, fx, back, hunch, spill):
    """The sack on its back, drawn behind the body; `spill` opens it."""
    x, y = back
    rx, ry = 11 - spill, 9 - spill * 0.5
    c.ellipse(x, y, rx, ry, SACK_S)
    c.ellipse(x + 1, y - 1, rx - 2, ry - 2, SACK)
    c.line((x - rx + 2, y + 2), (x + 2, y + ry - 1), 1, SACK_D)
    for k in range(3):
        c.dot(x - 3 + k * 3, y - ry + 2 + (k % 2), SACK_D)
    # the neck of the sack, tied with rope, over the shoulder
    knot = (x + rx - 3, y - ry + 1)
    c.line(knot, add(knot, 3, 3), 3, SACK_S)
    c.line(add(knot, -1, 1), add(knot, 3, 2), 1, ROPE)
    # glints: what is inside shows through the weave
    for k in range(3):
        gx, gy = x - 4 + k * 4, y + 2 - (k % 2) * 3
        c.dot(gx, gy, COIN)
        fx.blend(gx, gy, COIN_H, 60 + hunch * 5)
    if spill:
        for k in range(spill * 3):
            cx = x - 10 + k * 3 + (k % 3)
            cy = 44 - (k % 2) * (3 - spill) - (k * 7 % 3)
            c.dot(cx, cy, COIN if k % 2 else COIN_H)


def head(c, fx, hc, mode):
    x, y = round(hc[0]), round(hc[1])
    c.ellipse(x, y, 4, 4, SKIN)
    c.rect(x - 3, y + 1, x + 3, y + 3, SKIN_S)
    # a long jaw, horns swept back
    c.poly([(x + 1, y + 1), (x + 7, y + 3), (x + 1, y + 4)], SKIN_S)
    c.line((x - 2, y - 3), (x - 6, y - 6), 2, HORN, 1)
    c.line((x + 1, y - 4), (x, y - 8), 2, HORN, 1)
    if mode == "dead":
        c.line((x + 1, y - 1), (x + 4, y - 1), 1, SKIN_D)
        return
    c.dot(x + 3, y - 1, EYE)
    fx.blend(x + 3, y - 1, EYE, 90)
    if mode == "snarl":
        c.line((x + 2, y + 2), (x + 6, y + 3), 1, EDGE)
        c.dot(x + 3, y + 2, HORN)
        c.dot(x + 5, y + 3, HORN)


def frame(p):
    body, fx, back = Canvas(W, H), Canvas(W, H), Canvas(W, H)
    hip = (28 + p.get("dx", 0), 36 + p.get("bob", 0))
    lean = p.get("lean", 45)  # bent double under the load
    neck = pt(hip, 180 - lean, 11)
    hc = add(pt(neck, 180 - lean - 20, 3), 2, 0)
    back_c = add(pt(hip, 180 - lean, 7), -6, -5)
    sack(back, fx, back_c, p.get("hunch", 0), p.get("spill", 0))
    leg(body, add(hip, -1, 0), p["tb"], p["sb"], SKIN_D)
    arm(body, add(neck, -2, 1), p["aub"], p["alb"], SKIN_D)
    # the torso: a ridged back under the sack, a thin belly
    body.limb(hip, neck, 7, SKIN, None, 5)
    body.line(add(hip, 1, -2), add(neck, 2, 0), 1, SKIN_S)
    for k in range(3):
        q = pt(hip, 180 - lean, 3 + k * 3)
        body.dot(q[0] - 2, q[1] - 2, SKIN_S)
    leg(body, add(hip, 1, 0), p["tf"], p["sf"], SKIN, EDGE)
    head(body, fx, hc, p.get("face", "calm"))
    hand = arm(body, add(neck, 1, 2), p["auf"], p["alf"], SKIN, EDGE)
    if p.get("coin"):
        # a coin leaves the hand: the throw
        for k in range(3):
            q = add(hand, 4 + k * 4, -3 - k)
            back.dot(q[0], q[1], COIN)
            fx.blend(q[0] + 1, q[1], COIN_H, 150 - k * 40)
    img = compose(W, H, back, body, fx)
    if p.get("hurt"):
        img = tint(img, (255, 255, 255), 0.35)
    return img


def poses():
    base = dict(tf=15, sf=-20, tb=-5, sb=-10, auf=70, alf=120, aub=60, alb=110)
    anims = {}
    anims["idle"] = ([dict(base, bob=[0, 0, 1, 1][i], hunch=i % 2, lean=45 + [0, 1, 2, 1][i],
                           alf=120 - 6 * [0, 1, 1, 0][i]) for i in range(4)], 5)
    walk = []
    for i in range(4):
        s = math.sin(i / 4 * 2 * math.pi)
        walk.append(dict(base, lean=50, tf=20 + 30 * s, sf=-25 + 15 * s, tb=20 - 30 * s, sb=-25 - 15 * s,
                         bob=[0, -1, 0, -1][i], auf=70 + 10 * s, aub=60 - 10 * s))
    anims["walk"] = (walk, 6)
    anims["attack"] = ([
        dict(base, lean=30, dx=-2, bob=1, auf=-100, alf=-120, aub=40, alb=100, face="snarl"),
        dict(base, lean=55, dx=5, bob=-2, tf=40, sf=-30, tb=-30, sb=10, auf=110, alf=95, aub=50, alb=100,
             face="snarl", coin=True),
        dict(base, lean=50, dx=6, bob=0, tf=35, sf=-30, tb=-25, sb=5, auf=95, alf=100, face="snarl"),
        dict(base, lean=45, dx=2, auf=80, alf=115),
    ], 10)
    flinch = dict(base, lean=20, dx=-3, tf=0, sf=-10, tb=-20, sb=-10, auf=-60, alf=-30, aub=-20, alb=20,
                  face="snarl", hurt=True)
    anims["hurt"] = ([flinch, dict(flinch, lean=30, dx=-2, auf=-20, alf=40, hurt=False)], 8)
    # Death: it folds under the sack, goes down on its face and the sack
    # bursts, what it took rolling out across the floor.
    anims["death"] = ([
        dict(flinch, lean=10, dx=-4, bob=-1),
        dict(base, lean=60, dx=-3, bob=5, tf=60, sf=-80, tb=-50, sb=-60, auf=60, alf=100, aub=30, alb=80,
             face="snarl", spill=1),
        dict(base, lean=85, dx=-5, bob=9, tf=95, sf=-110, tb=-80, sb=-95, auf=100, alf=120, aub=80, alb=100,
             face="dead", spill=2),
        dict(base, lean=90, dx=-6, bob=10, tf=100, sf=-115, tb=-85, sb=-100, auf=110, alf=130, aub=90, alb=110,
             face="dead", spill=3),
    ], 8)
    return anims


def build():
    return {k: ([frame(p) for p in ps], fps) for k, (ps, fps) in poses().items()}
