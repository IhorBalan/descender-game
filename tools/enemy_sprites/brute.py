"""Brute: hulking Hell demon with molten cracks and a huge cleaver.

From the art thread's prototype rig, with the hurt and death strips added."""
import math
from pix import Canvas, compose, pt, add, tint

W, H = 112, 96
SKIN, SKIN_S, SKIN_D = (132, 44, 70), (94, 28, 56), (64, 20, 44)
SKIN_H = (170, 66, 86)
MAGMA, MAGMA_H = (255, 128, 36), (255, 220, 110)
HORN, HORN_S = (60, 50, 56), (36, 30, 38)
IRON, IRON_S, IRON_H = (92, 92, 108), (58, 58, 72), (150, 150, 168)
EDGE_RED = (230, 60, 40)
CLOTH = (52, 40, 40)
EDGE = (24, 16, 32)


def leg(c, hip, thigh, shin, col, edge=None):
    knee = pt(hip, thigh, 9)
    ankle = pt(knee, shin, 9)
    c.limb(hip, knee, 7, col, edge)
    c.limb(knee, ankle, 6, col, edge)
    c.limb(ankle, add(ankle, 5, 1), 4, col, edge)
    return knee


def arm(c, sh, up, low, col, edge=None):
    el = pt(sh, up, 10)
    hand = pt(el, low, 10)
    c.limb(sh, el, 7, col, edge, 6)
    c.limb(el, hand, 6, col, edge, 5)
    c.stamp(*hand, 5, col)
    return el, hand


def cleaver(c, fx, hand, ang, hot=0):
    c.line(pt(hand, ang + 180, 4), pt(hand, ang, 4), 3, CLOTH)
    b0 = pt(hand, ang, 4)
    b1 = pt(hand, ang, 28)
    off = 90
    quad = [b0, b1, pt(b1, ang + off, 10), pt(b0, ang + off, 8)]
    c.poly(quad, IRON)
    c.poly([b0, b1, pt(b1, ang + off, 3), pt(b0, ang + off, 3)], IRON_S)
    c.line(pt(b1, ang + off, 10), pt(b0, ang + off, 8), 1, EDGE_RED)
    c.dot(*pt(pt(hand, ang, 10), ang + off, 5), IRON_H)
    c.dot(*pt(pt(hand, ang, 18), ang + off, 6), IRON_H)
    if hot:
        for k in range(10):
            q = pt(pt(b0, ang + off, 8), ang, k * 2.4)
            fx.glow(q[0], q[1], 1, MAGMA, hot)


def cracks(c, fx, pts, t):
    for a, b in zip(pts, pts[1:]):
        c.line(a, b, 1, MAGMA)
    for i, q in enumerate(pts):
        if (i + t) % 3 == 0:
            c.dot(*q, MAGMA_H)
            fx.glow(q[0], q[1], 1, MAGMA, 70)


def frame(p):
    body, fx, back = Canvas(W, H), Canvas(W, H), Canvas(W, H)
    t = p.get("t", 0)
    hip = (44 + p.get("dx", 0), 70 + p.get("bob", 0))
    lean = p.get("lean", 20)
    neck = pt(hip, 180 - lean, 18)
    sh = add(neck, 1, 3)

    arm(body, add(sh, -6, 0), p["aub"], p["alb"], SKIN_D)
    leg(body, add(hip, -3, 2), p["tb"], p["sb"], SKIN_D)
    # loincloth
    body.poly([add(hip, -8, -1), add(hip, 7, -1), add(hip, 6, 8), add(hip, -6, 9)], CLOTH)
    leg(body, add(hip, 4, 2), p["tf"], p["sf"], SKIN_S, EDGE)
    # hulking torso: narrow hips, huge shoulders, hump
    hx, hy = hip
    nx, ny = neck
    body.poly([(hx - 7, hy), (hx + 7, hy), (nx + 11, ny + 4), (nx + 4, ny - 2), (nx - 8, ny - 1), (nx - 12, ny + 6)], SKIN_S)
    body.poly([(hx - 2, hy), (hx + 7, hy), (nx + 11, ny + 4), (nx + 4, ny - 2), (nx - 4, ny - 1)], SKIN)
    body.ellipse(nx + 5, ny + 9, 4, 3, SKIN_H)  # pec highlight
    body.line((hx - 7, hy), (hx + 7, hy), 2, SKIN_D)
    cracks(body, fx, [(nx - 6, ny + 4), (nx - 3, ny + 8), (nx - 4, ny + 12), (nx - 1, ny + 15), (hx, hy - 3)], t)
    cracks(body, fx, [(nx + 2, ny + 1), (nx + 4, ny + 4), (nx + 8, ny + 6)], t + 1)
    el, hand = arm(body, add(sh, 4, 1), p["auf"], p["alf"], SKIN, EDGE)
    body.ellipse(sh[0] + 4, sh[1] + 1, 5, 4, SKIN)
    body.ellipse(sh[0] + 5, sh[1], 2, 1, SKIN_H)
    cracks(body, fx, [add(sh, 4, 4), pt(sh, p["auf"], 6)], t)
    # small head sunk forward between the shoulders
    hc = add(neck, 11, -1)
    x, y = round(hc[0]), round(hc[1])
    body.rect(x - 4, y - 4, x + 5, y + 5, EDGE)
    body.rect(x - 3, y - 3, x + 4, y + 4, SKIN)
    body.rect(x - 2, y - 3, x + 3, y - 2, SKIN_H)
    body.rect(x + 1, y + 2, x + 5, y + 4, SKIN_S)  # jaw
    body.line((x - 1, y - 3), (x - 6, y - 8), 3, HORN, 2)
    body.line((x - 6, y - 8), (x - 3, y - 12), 2, HORN, 1)
    body.line((x - 3, y - 12), (x - 1, y - 13), 1, HORN_S)
    body.line((x + 1, y - 3), (x + 3, y - 8), 2, HORN_S, 2)
    body.line((x + 3, y - 8), (x + 6, y - 10), 2, HORN_S, 1)
    body.dot(x + 5, y + 2, (240, 230, 200))  # tusk
    body.dot(x + 5, y + 1, (240, 230, 200))
    eye = (255, 255, 255) if p.get("hurt") else MAGMA_H
    if p.get("dead"):
        eye = SKIN_D
    body.line((x + 2, y), (x + 4, y), 1, eye)
    if not p.get("dead"):
        fx.glow(x + 4, y, 2, MAGMA, 90)

    if p.get("dropped"):
        # The cleaver lies on the ground behind it.
        cleaver(body, fx, (hip[0] - 36, 90), 92, 0)
    else:
        cleaver(body, fx, hand, p["cl"], p.get("hot", 0))
    if p.get("impact"):
        gx = hip[0] + 10 if p.get("dead") else hand[0] + 14
        for i in range(-16, 17):
            hgt = max(0, 7 - abs(i) // 2 + (i * 7 + t) % 3)
            for k in range(hgt):
                col = MAGMA_H if k < 2 else MAGMA
                back.blend(gx + i, 92 - k, col, 220 - k * 22)
        for i in range(8):
            a = -70 + i * 20
            q = pt((gx, 90), a + 180, 10 + (i % 3) * 3)
            back.stamp(q[0], q[1], 2, (110, 90, 90))
    img = compose(W, H, back, body, fx)
    if p.get("hurt"):
        img = tint(img, (255, 255, 255), 0.3)
    return img


def poses():
    base = dict(tf=12, sf=-6, tb=-12, sb=4, auf=30, alf=70, aub=-5, alb=25, cl=120)
    anims = {}
    anims["idle"] = ([dict(base, t=i, bob=[0, 0, 1, 1][i], alf=70 - [0, 0, 6, 6][i], cl=120 - [0, 0, 6, 6][i])
                      for i in range(4)], 5)
    walk = []
    for i in range(4):
        s = math.sin(i / 4 * 2 * math.pi)
        cc = math.cos(i / 4 * 2 * math.pi)
        walk.append(dict(base, t=i, tf=16 * s, sf=16 * s - 14 * max(0, -cc) - 4, tb=-16 * s,
                         sb=-16 * s - 14 * max(0, cc) - 4, bob=1 if abs(s) > 0.5 else 0, lean=24,
                         aub=-15 * s, auf=30 + 8 * s))
    anims["walk"] = (walk, 5)
    anims["attack"] = ([
        dict(base, t=0, lean=6, auf=-150, alf=-170, aub=-150, alb=-170, cl=-160, tf=14, sf=-4, tb=-14, sb=4),
        dict(base, t=1, lean=-2, bob=-1, auf=-165, alf=-185, aub=-160, alb=-180, cl=-150, tf=14, sf=-4, tb=-14, sb=4, hot=60),
        dict(base, t=2, lean=40, bob=4, dx=4, auf=80, alf=60, aub=60, alb=40, cl=20, tf=40, sf=-10, tb=-30, sb=10, hot=120, impact=True),
        dict(base, t=3, lean=38, bob=4, dx=4, auf=75, alf=55, aub=55, alb=40, cl=25, tf=40, sf=-10, tb=-30, sb=10, impact=True),
        dict(base, t=0, lean=24, bob=1, dx=2, auf=40, alf=60, cl=90),
    ], 8)
    flinch = dict(base, lean=6, dx=-3, auf=-10, alf=30, aub=-30, alb=-10, cl=150, hurt=True)
    anims["hurt"] = ([flinch, dict(flinch, lean=12, dx=-2, auf=10, alf=50, aub=-15, alb=10, cl=135, hurt=False)], 8)
    # Death: it staggers, drops to its knees, the cleaver falls, and it
    # pitches forward onto its face and shakes the ground.
    anims["death"] = ([
        dict(flinch, t=0, lean=-6, dx=-4, auf=-40, alf=0, aub=-50, alb=-30, cl=170),
        dict(base, t=1, lean=14, dx=-2, bob=10, tf=70, sf=-100, tb=-50, sb=-90, auf=20, alf=40, aub=-20, alb=0, cl=120),
        dict(base, t=2, lean=55, dx=0, bob=16, tf=100, sf=-125, tb=-80, sb=-110, auf=90, alf=80, aub=-60, alb=-30,
             cl=60, dropped=True),
        dict(base, t=3, lean=86, dx=2, bob=22, tf=115, sf=-135, tb=-100, sb=-120, auf=120, alf=120, aub=-90, alb=-60,
             cl=10, dropped=True, impact=True, dead=True),
        dict(base, t=0, lean=86, dx=2, bob=22, tf=115, sf=-135, tb=-100, sb=-120, auf=120, alf=120, aub=-90, alb=-60,
             cl=10, dropped=True, dead=True),
    ], 8)
    return anims


def build():
    return {k: ([frame(p) for p in ps], fps) for k, (ps, fps) in poses().items()}
