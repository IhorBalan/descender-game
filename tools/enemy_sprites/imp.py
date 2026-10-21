"""Imp: small, fast Hell demon. Big horned head, bat wings, spade tail, claws.

From the art thread's prototype rig, with the hurt and death strips added."""
import math
from pix import Canvas, compose, pt, add, tint

W, H = 56, 40
SKIN, SKIN_S, SKIN_D = (226, 62, 58), (164, 36, 50), (120, 24, 44)
BELLY = (250, 140, 90)
HORN, HORN_S = (246, 228, 190), (196, 170, 130)
WING, WING_S = (110, 44, 96), (76, 28, 70)
EYE = (255, 230, 60)
EDGE = (24, 16, 32)


def leg(c, hip, thigh, shin, col, edge=None):
    knee = pt(hip, thigh, 4)
    hock = pt(knee, shin, 4)
    foot = pt(hock, shin * 0.3, 2)
    c.limb(hip, knee, 3, col, edge)
    c.limb(knee, hock, 2, col, edge)
    c.limb(hock, foot, 2, col, edge)
    c.limb(foot, add(foot, 3, 0), 1, HORN_S, edge)


def arm(c, sh, up, low, col, edge=None):
    el = pt(sh, up, 4)
    hand = pt(el, low, 4)
    c.limb(sh, el, 2, col, edge)
    c.limb(el, hand, 2, col, edge)
    for d in (-25, 0, 25):
        c.line(hand, pt(hand, low + d, 2.5), 1, HORN)
    return hand


def wing(c, root, ang, open_):
    tip = pt(root, ang, 9 + open_ * 3)
    mid = pt(root, ang + 50 + open_ * 10, 7 + open_ * 2)
    low = pt(root, ang + 95, 5)
    c.poly([root, tip, add(mid, 1, 1), mid, low], WING_S)
    c.poly([root, tip, mid], WING)
    c.line(root, tip, 1, SKIN_D)
    c.line(tip, mid, 1, SKIN_D)


def head(c, fx, hc, mode):
    x, y = round(hc[0]), round(hc[1])
    c.ellipse(x, y, 5, 4, SKIN)
    c.rect(x - 4, y + 1, x + 4, y + 3, SKIN_S)
    c.ellipse(x + 1, y - 1, 3, 2, SKIN)
    # horns curling back
    c.line((x - 1, y - 3), (x - 4, y - 7), 2, HORN, 1)
    c.line((x - 4, y - 7), (x - 7, y - 7), 1, HORN_S)
    c.line((x + 2, y - 3), (x + 1, y - 7), 2, HORN, 1)
    if mode == "dead":
        c.line((x + 1, y - 1), (x + 5, y - 1), 1, SKIN_D)
    else:
        c.dot(x + 4, y - 1, EYE)
        c.dot(x + 2, y - 1, EYE)
        fx.blend(x + 4, y - 1, EYE, 80)
    if mode == "grin":
        c.line((x, y + 2), (x + 5, y + 2), 1, EDGE)
        c.dot(x + 1, y + 2, HORN)
        c.dot(x + 3, y + 2, HORN)
    elif mode == "roar":
        c.rect(x + 1, y + 1, x + 5, y + 3, EDGE)
        c.dot(x + 2, y + 1, HORN)
        c.dot(x + 4, y + 1, HORN)
    else:
        c.line((x + 1, y + 2), (x + 4, y + 1), 1, EDGE)


def tail(c, hip, sway):
    pts = [add(hip, -2, 0), add(hip, -6, -1 + sway), add(hip, -9, -4 + sway), add(hip, -10, -8 + sway)]
    for a, b in zip(pts, pts[1:]):
        c.line(a, b, 1, SKIN_S)
    tx, ty = pts[-1]
    c.poly([(tx - 2, ty), (tx, ty - 3), (tx + 2, ty), (tx, ty + 1)], SKIN_D)


def frame(p):
    body, fx, back = Canvas(W, H), Canvas(W, H), Canvas(W, H)
    hip = (24 + p.get("dx", 0), 29 + p.get("bob", 0))
    lean = p.get("lean", 25)
    neck = pt(hip, 180 - lean, 6)
    hc = add(neck, 3, -3)
    tail(body, hip, p.get("tail", 0))
    wing(body, add(neck, -3, 1), p.get("wing", -140), p.get("open", 0))
    arm(body, add(neck, -1, 1), p["aub"], p["alb"], SKIN_D)
    leg(body, add(hip, -1, 0), p["tb"], p["sb"], SKIN_D)
    body.ellipse((hip[0] + neck[0]) / 2, (hip[1] + neck[1]) / 2 + 1, 3.5, 4, SKIN)
    body.ellipse((hip[0] + neck[0]) / 2 + 1.5, (hip[1] + neck[1]) / 2 + 2, 1.5, 2.5, BELLY)
    leg(body, add(hip, 1, 0), p["tf"], p["sf"], SKIN, EDGE)
    head(body, fx, hc, p.get("face", "grin"))
    hand = arm(body, add(neck, 1, 2), p["auf"], p["alf"], SKIN, EDGE)
    if p.get("claw"):
        for k in range(3):
            a = pt(hand, 150, 3 + k * 2)
            b = pt(hand, 30, 3 + k * 2)
            for i in range(9):
                q = (a[0] + (b[0] - a[0]) * i / 8 + 3, a[1] + (b[1] - a[1]) * i / 8 - k)
                back.blend(q[0], q[1], (255, 200, 120), 200 - k * 50)
    img = compose(W, H, back, body, fx)
    if p.get("hurt"):
        img = tint(img, (255, 255, 255), 0.35)
    return img


def poses():
    base = dict(tf=40, sf=-30, tb=10, sb=-40, auf=40, alf=90, aub=0, alb=40)
    anims = {}
    anims["idle"] = ([dict(base, bob=[0, 1, 1, 0][i], wing=-140 + [0, 15, 25, 15][i], open=[0, 1, 2, 1][i],
                           tail=[0, 1, 2, 1][i], alf=90 - 10 * [0, 1, 1, 0][i]) for i in range(4)], 8)
    run = []
    for i in range(4):
        s = math.sin(i / 4 * 2 * math.pi)
        run.append(dict(base, lean=40, tf=40 + 35 * s, sf=-20 + 20 * s, tb=40 - 35 * s, sb=-20 - 20 * s,
                        bob=[0, -2, 0, -2][i], wing=-120 + 20 * (i % 2), open=1 + i % 2, tail=-1 + i % 3,
                        auf=60, alf=120, aub=-40, alb=10))
    anims["run"] = (run, 12)
    anims["attack"] = ([
        dict(base, lean=10, bob=2, tf=60, sf=-40, tb=30, sb=-60, auf=-60, alf=-20, wing=-160, face="roar"),
        dict(base, lean=60, bob=-5, dx=6, tf=-30, sf=-40, tb=-50, sb=-30, auf=120, alf=110, wing=-100, open=3,
             face="roar", claw=True),
        dict(base, lean=50, bob=-2, dx=10, tf=30, sf=-20, tb=-20, sb=-40, auf=70, alf=40, wing=-110, open=2, face="roar"),
        dict(base, lean=30, bob=1, dx=10, tf=45, sf=-35, tb=15, sb=-45, wing=-140, open=1),
    ], 12)
    flinch = dict(base, lean=-10, dx=-2, tf=20, sf=-10, tb=-10, sb=-20, auf=-80, alf=-40, aub=-90, alb=-60,
                  wing=-170, face="roar", hurt=True)
    anims["hurt"] = ([flinch, dict(flinch, lean=0, dx=-1, auf=-40, alf=-10, aub=-50, alb=-30, wing=-160, hurt=False)], 8)
    # Death: thrown back, the knees go, it crumples onto its side and lies still.
    anims["death"] = ([
        dict(flinch, lean=-25, dx=-3, bob=-1, open=3, wing=-180, tail=2),
        dict(base, lean=-5, dx=-4, bob=5, tf=70, sf=-100, tb=-60, sb=-70, auf=-30, alf=20, aub=-60, alb=-20,
             wing=-150, open=1, face="roar", tail=1),
        dict(base, lean=55, dx=-6, bob=9, tf=100, sf=-120, tb=-80, sb=-90, auf=60, alf=120, aub=-80, alb=-40,
             wing=-120, open=0, face="roar", tail=0),
        dict(base, lean=88, dx=-8, bob=10, tf=110, sf=-130, tb=-95, sb=-100, auf=95, alf=130, aub=-85, alb=-60,
             wing=-110, open=0, face="dead", tail=-1),
    ], 10)
    return anims


def build():
    return {k: ([frame(p) for p in ps], fps) for k, (ps, fps) in poses().items()}
