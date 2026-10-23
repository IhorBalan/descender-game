"""Angel Guard: Heaven's soldier. Faceless gold mask, full halo, spear.

From the art thread's prototype rig, with the hurt and death strips added."""
import math
from pix import Canvas, compose, pt, add, tint

W, H = 96, 64
ARMOR, ARMOR_S, ARMOR_D = (238, 240, 250), (176, 182, 210), (130, 134, 170)
GOLD, GOLD_S, GOLD_H = (242, 194, 72), (186, 128, 44), (255, 236, 160)
CLOTH, CLOTH_S = (66, 116, 220), (44, 74, 160)
SHAFT = (150, 100, 60)
WING, WING_S, WING_D = (252, 252, 255), (200, 212, 240), (160, 172, 214)
LIGHT = (255, 250, 210)
HALO_GLOW, HALO_DOT, HALO_BRIGHT = (255, 236, 150), (255, 220, 100), (255, 252, 230)
EDGE = (24, 16, 32)


def wing(c, root, ang, fall, big, col_a, col_b):
    bone = 11 * big
    wrist = pt(root, ang, bone)
    lens = [8, 11, 14, 18, 22]
    for i in reversed(range(5)):
        b = pt(root, ang, bone * i / 4)
        a = fall + (4 - i) * 8
        c.limb(b, pt(b, a, lens[i] * big), 3, col_a if i % 2 else col_b, None, 2)
    c.limb(root, wrist, 4, col_b, None, 3)


def leg(c, hip, thigh, shin, armor, cloth, edge=None):
    knee = pt(hip, thigh, 9)
    ankle = pt(knee, shin, 8)
    c.limb(hip, knee, 4, cloth, edge)
    c.limb(knee, ankle, 4, armor, edge)
    c.limb(ankle, pt(ankle, shin + 90, 4), 3, armor, edge)
    c.stamp(*knee, 3, GOLD if edge else GOLD_S)


def arm(c, sh, up, low, armor, edge=None):
    el = pt(sh, up, 7)
    hand = pt(el, low, 6)
    c.limb(sh, el, 3, armor, edge)
    c.limb(el, hand, 3, armor, edge)
    c.stamp(*hand, 3, GOLD if edge else GOLD_S)
    return el, hand


def spear(c, fx, hand, ang, glow=0):
    tip = pt(hand, ang, 24)
    butt = pt(hand, ang + 180, 12)
    c.line(butt, tip, 2, SHAFT)
    c.line(pt(hand, ang, 3), pt(hand, ang, 5), 2, GOLD)
    head_base = pt(hand, ang, 22)
    point = pt(hand, ang, 31)
    l, r = pt(head_base, ang + 90, 2.5), pt(head_base, ang - 90, 2.5)
    c.poly([l, point, r, pt(head_base, ang + 180, 1)], GOLD)
    c.line(head_base, pt(hand, ang, 29), 1, GOLD_H)
    if glow:
        fx.glow(point[0], point[1], 2, LIGHT, glow)


def torso(c, hip, neck):
    hx, hy = hip
    nx, ny = neck
    # tabard skirt
    c.poly([(hx - 5, hy - 1), (hx + 5, hy - 1), (hx + 6, hy + 7), (hx - 6, hy + 7)], CLOTH_S)
    c.poly([(hx - 1, hy - 1), (hx + 5, hy - 1), (hx + 6, hy + 7), (hx + 1, hy + 7)], CLOTH)
    c.line((hx - 6, hy + 7), (hx + 6, hy + 7), 1, GOLD)
    # breastplate
    c.poly([(hx - 4, hy), (hx + 4, hy), (nx + 5, ny + 2), (nx - 5, ny + 2)], ARMOR_S)
    c.poly([(hx - 1, hy), (hx + 4, hy), (nx + 5, ny + 2), (nx, ny + 2)], ARMOR)
    c.line((nx + 1, ny + 4), (hx + 1, hy - 2), 1, GOLD)  # centre ridge
    c.line((hx - 4, hy), (hx + 4, hy), 2, GOLD_S)


def head(c, fx, hc, flicker, hurt, out=False):
    x, y = round(hc[0]), round(hc[1])
    c.rect(x - 2, y + 3, x + 2, y + 5, ARMOR_S)  # gorget
    c.rect(x - 3, y - 3, x + 3, y + 3, GOLD_S)
    c.rect(x - 2, y - 4, x + 2, y + 3, GOLD)
    c.rect(x - 1, y - 3, x + 3, y + 2, GOLD)
    c.rect(x + 1, y - 3, x + 2, y - 2, GOLD_H)
    c.line((x - 3, y - 4), (x - 8, y - 7), 2, CLOTH)  # plume
    c.line((x - 3, y - 3), (x - 9, y - 4), 2, CLOTH_S)
    slit = (255, 120, 120) if hurt else LIGHT
    if out:
        slit = GOLD_S
    c.line((x + 1, y - 1), (x + 3, y - 1), 1, slit)
    c.line((x + 2, y - 1), (x + 2, y + 2), 1, slit)
    if not out:
        fx.glow(x + 3, y, 2, slit, 70 + flicker * 30)


def halo(fx, hc, t, dim=False):
    cx, cy = hc[0] - 1, hc[1] - 8
    for k in range(28):
        a = 2 * math.pi * k / 28
        x, y = cx + 6 * math.cos(a), cy + 2 * math.sin(a)
        fx.glow(x, y, 1, HALO_GLOW, 20 if dim else 50)
    for k in range(28):
        a = 2 * math.pi * k / 28
        x, y = cx + 6 * math.cos(a), cy + 2 * math.sin(a)
        bright = (k + t * 3) % 9 == 0
        fx.dot(x, y, HALO_BRIGHT if bright else HALO_DOT)


def frame(p):
    body, fx, back = Canvas(W, H), Canvas(W, H), Canvas(W, H)
    hip = (36 + p.get("dx", 0), 41 + p.get("bob", 0))
    lean = p.get("lean", 0)
    neck = pt(hip, 180 - lean, 13)
    sh = add(neck, 0, 3)
    hc = add(pt(neck, 180 - lean * 0.5, 4), 1, 0)
    root = add(neck, -3, 3)

    wing(body, add(root, -3, -1), p["wing"] - 8, p["fall"] - 6, 1.0, WING_S, WING_D)
    arm(body, add(sh, -2, 0), p["aub"], p["alb"], ARMOR_D)
    leg(body, add(hip, -2, 4), p["tb"], p["sb"], ARMOR_D, CLOTH_S)
    wing(body, root, p["wing"], p["fall"], 1.08, WING, WING_S)
    torso(body, hip, neck)
    leg(body, add(hip, 2, 4), p["tf"], p["sf"], ARMOR, CLOTH, EDGE)
    head(body, fx, hc, p.get("t", 0) % 2, p.get("hurt"), p.get("out"))
    c_sh = add(sh, 1, 0)
    el, hand = arm(body, c_sh, p["auf"], p["alf"], ARMOR, EDGE)
    body.ellipse(c_sh[0], c_sh[1], 3, 2, GOLD)  # pauldron
    body.dot(c_sh[0] + 1, c_sh[1] - 1, GOLD_H)
    spear(body, fx, hand, p["sp"], p.get("glow", 0))
    if not p.get("out"):
        halo(fx, hc, p.get("t", 0), p.get("dim"))
    if p.get("streak"):
        tip = pt(hand, p["sp"], 31)
        for k in range(1, 16):
            q = pt(tip, p["sp"] + 180, k * 1.4)
            back.blend(q[0], q[1] - 1, LIGHT, 200 - k * 12)
            back.blend(q[0], q[1] + 1, LIGHT, 200 - k * 12)
    img = compose(W, H, back, body, fx)
    if p.get("hurt"):
        img = tint(img, (255, 80, 90), 0.25)
    return img


def poses():
    base = dict(tf=6, sf=-2, tb=-8, sb=2, auf=20, alf=100, aub=-10, alb=15, sp=170, wing=-150, fall=-20)
    anims = {}
    anims["idle"] = ([dict(base, t=i, bob=[0, 0, 1, 1][i], wing=-150 + [0, -2, -4, -2][i],
                           fall=-20 + [0, 2, 4, 2][i]) for i in range(4)], 5)
    walk = []
    for i in range(4):
        ph = i / 4 * 2 * math.pi
        s, cc = math.sin(ph), math.cos(ph)
        walk.append(dict(base, t=i, tf=18 * s, sf=18 * s - 12 * max(0, -cc) - 4, tb=-18 * s,
                         sb=-18 * s - 12 * max(0, cc) - 4, bob=0 if abs(s) > 0.5 else -1,
                         aub=-12 * s - 6, lean=3))
    anims["walk"] = (walk, 6)
    anims["attack"] = ([
        dict(base, t=0, lean=-4, tf=14, sf=-2, tb=-16, sb=4, auf=-80, alf=95, aub=10, alb=40, sp=95, wing=-160, fall=-15, glow=90),
        dict(base, t=1, lean=12, dx=4, tf=30, sf=4, tb=-26, sb=8, auf=88, alf=92, aub=-30, alb=-10, sp=92, wing=-125, fall=-40,
             glow=140, streak=True),
        dict(base, t=2, lean=10, dx=4, tf=30, sf=4, tb=-26, sb=8, auf=86, alf=90, aub=-30, alb=-10, sp=91, wing=-130, fall=-35, glow=60),
        dict(base, t=3, lean=2, dx=1, tf=10, sf=-2, tb=-12, sb=2, auf=0, alf=100, aub=-10, alb=15, sp=150, wing=-145, fall=-22),
    ], 10)
    flinch = dict(base, lean=-12, dx=-3, tf=-6, sf=-8, tb=-24, sb=-4, auf=-30, alf=60, aub=-50, alb=-30, sp=190,
                  wing=-165, fall=-10, hurt=True)
    anims["hurt"] = ([flinch, dict(flinch, lean=-5, dx=-2, auf=-10, alf=80, aub=-30, alb=-10, sp=180, hurt=False)], 8)
    # Death: the spear drops, it goes down on one knee, tips forward and
    # lies face down with the wings spread on the ground; the halo goes out.
    anims["death"] = ([
        dict(flinch, lean=-18, dx=-4, auf=-60, alf=40, sp=230, wing=-175, fall=0),
        dict(base, lean=10, dx=-2, bob=7, tf=55, sf=-70, tb=-30, sb=-60, auf=30, alf=60, aub=-20, alb=10, sp=120,
             wing=-150, fall=10, dim=True),
        dict(base, lean=60, dx=-3, bob=13, tf=95, sf=-110, tb=-70, sb=-95, auf=100, alf=110, aub=-70, alb=-40, sp=100,
             wing=-120, fall=30, dim=True),
        dict(base, lean=88, dx=-5, bob=17, tf=110, sf=-125, tb=-95, sb=-110, auf=120, alf=135, aub=-100, alb=-60, sp=95,
             wing=-100, fall=45, dim=True, out=True),
    ], 8)
    return anims


def build():
    return {k: ([frame(p) for p in ps], fps) for k, (ps, fps) in poses().items()}
