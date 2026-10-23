"""Siren: the winged want of Lust. Velvet skin, bat wings, a face that smiles.

Hangs in the air with the wings beating slow, comes at the hero fast when
it sees him, and the smile shows its teeth. Dies with the wings folding."""
import math
from pix import Canvas, compose, pt, add, tint

W, H = 64, 48
C = (32, 24)
SKIN, SKIN_S, SKIN_D = (214, 120, 150), (166, 78, 118), (112, 46, 84)
HAIR, HAIR_S = (70, 22, 60), (44, 12, 40)
WING, WING_S, WING_D = (120, 44, 110), (84, 28, 84), (54, 18, 58)
EYE = (255, 230, 120)
TOOTH = (246, 236, 220)
VELVET = (200, 80, 140)
EDGE = (24, 16, 32)


def wing(c, root, ang, spread, col, col_s, side):
    """A bat wing from `root`; `side` is -1 for the far wing, 1 for the near."""
    bone = 10 + spread * 1.5
    wrist = pt(root, ang, bone)
    fingers = [pt(wrist, ang - 40 * side - k * 25 * side + spread * 4 * side, 6 + k * 2) for k in range(3)]
    low = pt(root, ang - 110 * side, 6)
    pts = [root, wrist] + fingers + [low]
    c.poly(pts, col_s)
    c.poly([root, wrist, fingers[0], fingers[1]], col)
    c.limb(root, wrist, 2, col_s)
    for f in fingers:
        c.line(wrist, f, 1, WING_D)


def body(c, fx, p):
    cx, cy = C[0] + p.get("dx", 0), C[1] + p.get("bob", 0)
    lean = p.get("lean", 0)
    hip = (cx - 2, cy + 8)
    neck = pt(hip, 180 - lean, 11)
    hc = add(pt(neck, 180 - lean, 3), 1, -1)
    # the legs trail behind, bent, tipped with claws
    for off, col in ((-2, SKIN_D), (1, SKIN)):
        knee = pt(add(hip, off, 0), 150 + p.get("legs", 0), 7)
        foot = pt(knee, 60, 6)
        c.limb(add(hip, off, 0), knee, 3, col, EDGE if off > 0 else None)
        c.limb(knee, foot, 2, col, EDGE if off > 0 else None)
        c.dot(foot[0] + 1, foot[1] + 1, TOOTH)
    # the torso, a velvet wrap about the hips
    c.limb(hip, neck, 6, SKIN, None, 4)
    c.ellipse(hip[0], hip[1] - 1, 4, 3, VELVET)
    c.line(add(hip, -3, -4), add(neck, -2, 2), 1, SKIN_S)
    # the arms reach when it comes
    reach = p.get("reach", 0)
    for off, col in ((-1, SKIN_D), (1, SKIN)):
        sh = add(neck, off, 2)
        el = pt(sh, 60 + reach * 30 + off * 10, 6)
        hand = pt(el, 100 + reach * 10, 6)
        c.limb(sh, el, 2, col, EDGE if off > 0 else None)
        c.limb(el, hand, 2, col, EDGE if off > 0 else None)
        for d in (-25, 0, 25):
            c.line(hand, pt(hand, 100 + reach * 10 + d, 2.5), 1, TOOTH)
    # the head: long hair blown back, a face that smiles
    x, y = round(hc[0]), round(hc[1])
    c.poly([(x - 2, y - 5), (x - 12 - p.get("hair", 0), y - 2), (x - 11, y + 6), (x - 2, y + 4)], HAIR_S)
    c.poly([(x - 1, y - 5), (x - 9 - p.get("hair", 0), y - 1), (x - 7, y + 4), (x, y + 3)], HAIR)
    c.ellipse(x + 1, y, 4, 4, SKIN)
    c.ellipse(x + 2, y + 1, 3, 2, SKIN_S)
    c.line((x - 1, y - 5), (x - 3, y - 9), 2, SKIN_D, 1)  # a horn through the hair
    c.line((x + 3, y - 4), (x + 4, y - 8), 2, SKIN_D, 1)
    if p.get("dead"):
        c.line((x + 2, y - 1), (x + 5, y - 1), 1, SKIN_D)
        return
    c.dot(x + 3, y - 1, EYE)
    c.dot(x + 4, y - 1, EYE)
    fx.blend(x + 4, y - 1, EYE, 90)
    if p.get("grin"):
        c.rect(x + 1, y + 2, x + 5, y + 3, EDGE)
        for k in range(3):
            c.dot(x + 1 + k * 2, y + 2, TOOTH)
    else:
        c.line((x + 2, y + 2), (x + 5, y + 2), 1, SKIN_D)
        c.dot(x + 5, y + 1, TOOTH)


def frame(p):
    body_c, fx, back = Canvas(W, H), Canvas(W, H), Canvas(W, H)
    cx, cy = C[0] + p.get("dx", 0), C[1] + p.get("bob", 0)
    flap = p.get("flap", 0)
    spread = p.get("spread", 1)
    root_far = (cx - 4, cy + 1)
    root_near = (cx - 1, cy)
    wing(back, root_far, -140 + flap, spread, WING_S, WING_D, -1)
    body(body_c, fx, p)
    wing(fx, root_near, -150 + flap, spread, WING, WING_S, 1)
    if p.get("glow"):
        back.glow(cx, cy, 10, VELVET, 25)
    img = compose(W, H, back, body_c, fx)
    if p.get("hurt"):
        img = tint(img, (255, 255, 255), 0.3)
    return img


def poses():
    anims = {}
    anims["idle"] = ([dict(bob=[0, -1, -2, -1, 0, 1][i], flap=[20, 0, -20, -30, -20, 0][i],
                           spread=[1, 2, 2, 1, 0, 0][i], hair=[0, 1, 2, 2, 1, 0][i], legs=[0, 5, 10, 5, 0, -5][i],
                           glow=True) for i in range(6)], 8)
    anims["attack"] = ([
        dict(bob=-2, flap=-30, spread=2, lean=-10, reach=1, grin=True, hair=3, glow=True),
        dict(bob=0, flap=10, spread=2, dx=4, lean=20, reach=2, grin=True, hair=4, legs=15, glow=True),
        dict(bob=1, flap=25, spread=1, dx=7, lean=25, reach=2, grin=True, hair=4, legs=20),
        dict(bob=0, flap=0, spread=1, dx=3, lean=5, reach=1, grin=True, hair=2),
    ], 10)
    anims["hurt"] = ([dict(dx=-3, flap=-35, spread=2, lean=-20, grin=True, hair=-2, hurt=True),
                      dict(dx=-2, flap=-20, spread=1, lean=-10, hair=-1)], 8)
    # Death: the wings fold about it and it drops, the smile gone.
    anims["death"] = ([
        dict(dx=-2, flap=-40, spread=2, lean=-25, grin=True, hair=-2, hurt=True),
        dict(dx=-2, bob=3, flap=20, spread=0, lean=10, hair=0, legs=20),
        dict(dx=-3, bob=8, flap=50, spread=0, lean=50, hair=2, legs=40, dead=True),
        dict(dx=-3, bob=13, flap=70, spread=0, lean=85, hair=3, legs=60, dead=True),
    ], 8)
    return anims


def build():
    return {k: ([frame(p) for p in ps], fps) for k, (ps, fps) in poses().items()}
