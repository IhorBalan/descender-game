"""Hound: a lean four-legged Hell dog for Violence. Low, long, all jaw.

Gallops, lunges with the jaw open, and dies rolled onto its side."""
import math
from pix import Canvas, compose, pt, add, tint

W, H = 56, 32
SKIN, SKIN_S, SKIN_D = (118, 40, 52), (82, 26, 40), (52, 16, 30)
BELLY = (150, 70, 70)
BONE = (236, 220, 196)
EYE = (255, 150, 40)
EDGE = (24, 16, 32)


def leg(c, hip, thigh, shin, col, edge=None, back=False):
    knee = pt(hip, thigh, 6)
    hock = pt(knee, shin, 6)
    paw = pt(hock, 90 if not back else 80, 3)
    c.limb(hip, knee, 3 if back else 2, col, edge)
    c.limb(knee, hock, 2, col, edge)
    c.limb(hock, paw, 2, col, edge)
    for k in (-1, 1):
        c.dot(paw[0] + 2, paw[1] + k * 0 + 1, BONE)


def head(c, fx, neck, ang, mode):
    skull = pt(neck, ang, 7)
    jaw = pt(skull, ang + 10, 8)
    c.limb(neck, skull, 5, SKIN, EDGE, 4)
    c.limb(skull, jaw, 4, SKIN, EDGE, 2)
    # ears laid back, or up when it lunges
    ear = pt(skull, 180 + (20 if mode == "snarl" else -30), 5)
    c.line(skull, ear, 2, SKIN_S)
    c.line(skull, add(ear, -2, 1), 1, SKIN_D)
    if mode == "dead":
        c.line(add(skull, 0, -1), add(skull, 3, -1), 1, SKIN_D)
        c.line(add(jaw, -2, 1), add(jaw, 2, 2), 1, SKIN_D)
        return
    ex, ey = add(skull, 2, -1)
    c.dot(ex, ey, EYE)
    fx.blend(ex + 1, ey, EYE, 90)
    fx.blend(ex, ey - 1, EYE, 60)
    if mode == "snarl":
        low = pt(skull, ang + 60, 8)
        c.limb(skull, low, 3, SKIN_S, EDGE, 2)
        for k in range(3):
            c.dot(*add(pt(skull, ang + 10, 3 + k * 2), 0, 1), BONE)
            c.dot(*add(pt(skull, ang + 60, 3 + k * 2), 0, -1), BONE)
    else:
        c.line(add(jaw, -4, 1), add(jaw, 0, 1), 1, SKIN_D)
        c.dot(*add(jaw, -1, 2), BONE)


def tail(c, hip, ang):
    a = pt(hip, ang, 5)
    b = pt(a, ang - 40, 5)
    c.line(hip, a, 2, SKIN_S)
    c.line(a, b, 1, SKIN_D)


def frame(p):
    body, fx = Canvas(W, H), Canvas(W, H)
    dx, bob = p.get("dx", 0), p.get("bob", 0)
    hip = (18 + dx, 20 + bob)
    sh = (33 + dx, 19 + bob + p.get("dip", 0))
    tail(body, add(hip, -2, -1), p.get("tail", -140))
    leg(body, add(hip, 1, 1), p["tb2"], p["sb2"], SKIN_D, None, True)
    leg(body, add(sh, -1, 1), p["tf2"], p["sf2"], SKIN_D)
    # the torso: a ridge of shoulder and hip, lower belly
    body.poly([add(hip, -3, -4), add(sh, 3, -5), add(sh, 3, 3), add(hip, -2, 4)], SKIN)
    body.poly([add(hip, 0, 1), add(sh, 1, 0), add(sh, 1, 3), add(hip, 0, 4)], BELLY)
    body.line(add(hip, -2, -3), add(sh, 2, -4), 1, SKIN_S)
    for k in range(3):
        body.line(add(hip, 2 + k * 4, -2), add(hip, 2 + k * 4, 2), 1, SKIN_S)
    leg(body, add(hip, 0, 1), p["tb"], p["sb"], SKIN, EDGE, True)
    leg(body, add(sh, 0, 1), p["tf"], p["sf"], SKIN, EDGE)
    head(body, fx, add(sh, 2, -3), p.get("head", 70), p.get("face", "calm"))
    img = compose(W, H, None, body, fx)
    if p.get("hurt"):
        img = tint(img, (255, 255, 255), 0.35)
    return img


def poses():
    base = dict(tf=10, sf=-10, tf2=-10, sf2=10, tb=-20, sb=30, tb2=15, sb2=10)
    anims = {}
    anims["idle"] = ([dict(base, bob=[0, 0, 1, 1][i], tail=-140 + [0, 10, 20, 10][i],
                           head=70 + [0, 0, 5, 5][i]) for i in range(4)], 6)
    run = []
    for i in range(4):
        s = math.sin(i / 4 * 2 * math.pi)
        c = math.cos(i / 4 * 2 * math.pi)
        run.append(dict(base, tf=20 + 45 * s, sf=-20 + 25 * s, tf2=20 - 45 * s, sf2=-20 - 25 * s,
                        tb=-30 - 40 * c, sb=30 + 30 * c, tb2=-30 + 40 * c, sb2=30 - 30 * c,
                        bob=[-2, 0, 1, 0][i], dip=[1, -1, 0, 1][i], tail=-120 + 15 * s, head=80))
    anims["run"] = (run, 12)
    anims["attack"] = ([
        dict(base, dx=-2, bob=2, tf=30, sf=-50, tf2=30, sf2=-50, tb=-50, sb=70, tb2=-40, sb2=60,
             head=40, face="snarl", tail=-170),
        dict(base, dx=6, bob=-4, dip=-1, tf=80, sf=-20, tf2=70, sf2=-10, tb=-70, sb=40, tb2=-60, sb2=30,
             head=95, face="snarl", tail=-100),
        dict(base, dx=8, bob=0, tf=10, sf=-10, tf2=0, sf2=0, tb=-10, sb=20, tb2=0, sb2=10,
             head=85, face="snarl", tail=-120),
    ], 12)
    flinch = dict(base, dx=-2, bob=1, tf=-30, sf=40, tf2=-20, sf2=30, tb=10, sb=10, tb2=20, sb2=0,
                  head=30, face="snarl", tail=-180, hurt=True)
    anims["hurt"] = ([flinch, dict(flinch, dx=-1, head=50, hurt=False)], 8)
    # Death: the legs go, it drops on its chest, rolls onto its side, still.
    anims["death"] = ([
        dict(flinch, bob=2, head=20),
        dict(base, dx=-2, bob=5, tf=60, sf=-60, tf2=50, sf2=-50, tb=-60, sb=80, tb2=-50, sb2=70,
             head=100, face="snarl", tail=-160),
        dict(base, dx=-3, bob=8, tf=85, sf=-90, tf2=80, sf2=-80, tb=-85, sb=100, tb2=-80, sb2=95,
             head=110, face="dead", tail=-150),
        dict(base, dx=-4, bob=9, tf=90, sf=-100, tf2=85, sf2=-95, tb=-90, sb=110, tb2=-88, sb2=105,
             head=115, face="dead", tail=-145),
    ], 10)
    return anims


def build():
    return {k: ([frame(p) for p in ps], fps) for k, (ps, fps) in poses().items()}
