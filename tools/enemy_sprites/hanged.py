"""Hanged thing: a frozen traitor on a rope under the ceilings of Treachery.

It hangs still but for a sway, lets go when the hero walks under it, and
climbs back up. The rope is drawn by the game; the strip is the body, its
origin at the feet, the rope's knot at the top of the head."""
from pix import Canvas, compose, pt, add, tint

W, H = 40, 64
SKIN, SKIN_S, SKIN_D = (150, 166, 190), (104, 120, 150), (70, 84, 112)
RAG, RAG_S = (52, 56, 72), (34, 36, 50)
FROST = (226, 240, 255)
ROPE = (120, 100, 70)
EYE = (210, 240, 255)
EDGE = (24, 16, 32)

## Where the rope's knot is in the frame, so the game can draw the rope to it.
KNOT = (20, 6)


def limb2(c, a, ang1, ang2, l1, l2, w, col, edge=None):
    m = pt(a, ang1, l1)
    e = pt(m, ang2, l2)
    c.limb(a, m, w, col, edge)
    c.limb(m, e, w - 1, col, edge)
    return e


def frame(p):
    body, fx = Canvas(W, H), Canvas(W, H)
    sway = p.get("sway", 0)
    # The body hangs from the knot: the neck is just under it, the hips
    # further down and the feet below, each swung a little further.
    knot = KNOT
    neck = add(knot, sway * 0.4, 7)
    hip = add(neck, sway * 1.2, 20)
    tilt = p.get("tilt", 25)
    # legs, the far one first
    for k, (ang, ang2) in enumerate(((p["lb"], p["lb2"]), (p["lf"], p["lf2"]))):
        col = SKIN_S if k == 0 else SKIN
        foot = limb2(body, add(hip, -1 + k * 2, 0), ang, ang2, 12, 12, 3, col, EDGE if k else None)
        body.line(foot, add(foot, 3 if k else -3, 0), 2, col)
    # the rags: a torn shirt and the torso under it
    body.poly([add(neck, -5, 0), add(neck, 5, 0), add(hip, 5, 2), add(hip, -6, 3)], RAG)
    body.poly([add(neck, -5, 0), add(neck, 0, 0), add(hip, -2, 3), add(hip, -6, 3)], RAG_S)
    for k in range(3):
        body.line(add(hip, -5 + k * 4, 2), add(hip, -5 + k * 4, 5 + (k % 2) * 2), 1, RAG_S)
    # arms, hanging
    for k, (ang, ang2) in enumerate(((p["ab"], p["ab2"]), (p["af"], p["af2"]))):
        col = SKIN_S if k == 0 else SKIN
        hand = limb2(body, add(neck, -4 + k * 8, 2), ang, ang2, 11, 10, 2, col, EDGE if k else None)
        for d in (-20, 0, 20):
            body.line(hand, pt(hand, ang2 + d, 2.5), 1, col)
    # the head, tilted on the rope
    hx, hy = add(neck, 0, -3)
    body.ellipse(hx + tilt * 0.08, hy, 4, 5, SKIN)
    body.ellipse(hx + tilt * 0.08 + 1, hy - 1, 2, 3, SKIN_S)
    body.line(add(knot, 0, 0), add(knot, 0, 4), 2, ROPE)
    body.line(add(knot, -3, 3), add(knot, 3, 3), 1, ROPE)
    if p.get("face") == "dead":
        body.line((hx - 2, hy), (hx + 2, hy), 1, SKIN_D)
    else:
        body.dot(hx - 1, hy, EYE)
        body.dot(hx + 2, hy, EYE)
        if p.get("face") == "open":
            body.rect(hx - 1, hy + 2, hx + 2, hy + 3, EDGE)
            fx.glow(hx - 1, hy, 1, EYE, 90)
            fx.glow(hx + 2, hy, 1, EYE, 90)
        else:
            body.line((hx - 1, hy + 3), (hx + 2, hy + 3), 1, SKIN_D)
    # frost on the shoulders and the hands
    for q in (add(neck, -5, 1), add(neck, 5, 1), add(hip, -4, 3), add(hip, 4, 4)):
        fx.blend(q[0], q[1], FROST, 190)
        fx.blend(q[0] + 1, q[1], FROST, 90)
    img = compose(W, H, None, body, fx)
    if p.get("hurt"):
        img = tint(img, (255, 255, 255), 0.35)
    return img


def poses():
    base = dict(lb=5, lb2=-5, lf=-5, lf2=8, ab=10, ab2=-5, af=-8, af2=6)
    anims = {}
    anims["idle"] = ([dict(base, sway=[0, 1, 2, 1, 0, -1, -2, -1][i], tilt=25 + [0, 2, 4, 2, 0, -2, -4, -2][i])
                      for i in range(8)], 6)
    # The drop: the arms come out, the eyes open, the legs splay.
    anims["attack"] = ([
        dict(base, sway=0, tilt=10, ab=40, ab2=20, af=-40, af2=-20, lb=15, lb2=0, lf=-15, lf2=5, face="open"),
        dict(base, sway=0, tilt=0, ab=90, ab2=70, af=-90, af2=-70, lb=35, lb2=-10, lf=-35, lf2=20, face="open"),
    ], 8)
    flinch = dict(base, sway=3, tilt=40, ab=30, ab2=40, af=-20, af2=-30, face="open", hurt=True)
    anims["hurt"] = ([flinch, dict(flinch, sway=1, tilt=30, hurt=False)], 8)
    # Death: the rope's hold goes; it folds at the knees and sits in a heap.
    anims["death"] = ([
        dict(flinch, sway=0, tilt=50),
        dict(base, sway=0, tilt=60, ab=60, ab2=30, af=-60, af2=-30, lb=40, lb2=-60, lf=-40, lf2=60,
             face="open"),
        dict(base, sway=0, tilt=80, ab=80, ab2=20, af=-80, af2=-20, lb=70, lb2=-100, lf=-70, lf2=100,
             face="dead"),
        dict(base, sway=0, tilt=90, ab=85, ab2=10, af=-85, af2=-10, lb=80, lb2=-110, lf=-80, lf2=110,
             face="dead"),
    ], 8)
    return anims


def build():
    return {k: ([frame(p) for p in ps], fps) for k, (ps, fps) in poses().items()}
