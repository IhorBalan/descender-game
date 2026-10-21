"""The people the hero talks to, drawn with the prototype rig (see pix.py).

One rig draws every human NPC; a character is a dictionary of colours,
build and props. Each has an `idle` loop (breathing, a small fidget) and a
`talk` loop (nodding, mouth, a hand gesture) that the game plays while the
dialogue box is open.

    python3 tools/npc_sprites/export.py        # writes the strips and sprites.json
    python3 tools/npc_sprites/export.py --preview  # also writes a preview sheet
"""
import math

from pix import Canvas, compose, pt, add

W, H = 64, 48
EDGE = (24, 16, 32)
SKIN_OLD, SKIN_OLD_S = (226, 190, 160), (184, 140, 112)
SKIN, SKIN_S = (242, 198, 160), (204, 146, 116)
SKIN_PALE, SKIN_PALE_S = (236, 222, 214), (186, 168, 168)
MOUTH = (120, 50, 60)
PAPER, PAPER_S = (244, 238, 220), (196, 186, 160)
WOOD, WOOD_S = (150, 104, 60), (96, 64, 40)
IRON, IRON_H = (110, 112, 126), (170, 172, 188)

CHARACTERS = {
    # Heaven 1: the gathered gardener who keeps tallies.
    "mereth": dict(
        skin=SKIN_OLD, skin_s=SKIN_OLD_S, hair=(214, 210, 206), hair_h=(240, 238, 236),
        hair_style="bun", top=(118, 92, 128), top_s=(80, 60, 92), trim=(196, 180, 150),
        dress=True, sleeves=True, lean=8, torso=10, legs=(7, 6), prop="stick", eyes=(60, 40, 50),
    ),
    # Earth hub: the one-handed blacksmith.
    "pell": dict(
        skin=SKIN, skin_s=SKIN_S, hair=(96, 78, 70), hair_h=(130, 110, 100), hair_style="bald_beard",
        top=(120, 76, 48), top_s=(78, 48, 30), trim=(60, 44, 36), apron=(140, 120, 100),
        pants=(70, 62, 60), pants_s=(48, 42, 42), boots=(56, 40, 32), torso=12, legs=(8, 7),
        broad=True, prop="hammer", stump=True, eyes=(40, 30, 40),
    ),
    # Earth hub: the child who draws what she sees.
    "hanne": dict(
        skin=SKIN, skin_s=SKIN_S, hair=(190, 120, 60), hair_h=(230, 170, 90), hair_style="pigtails",
        top=(200, 70, 80), top_s=(140, 44, 58), trim=(250, 230, 200), pants=(90, 100, 140),
        pants_s=(60, 68, 100), boots=(70, 50, 40), torso=7, legs=(5, 4), child=True,
        prop="paper", eyes=(50, 70, 110),
    ),
    # Earth hub: the last soldier of a dead company.
    "dace": dict(
        skin=SKIN, skin_s=SKIN_S, hair=(70, 60, 56), hair_h=(100, 90, 84), hair_style="helmet",
        top=(96, 104, 76), top_s=(62, 68, 48), trim=(170, 150, 90), pants=(80, 84, 64),
        pants_s=(54, 56, 42), boots=(50, 40, 34), torso=12, legs=(8, 7), prop="rifle",
        eyes=(40, 40, 50),
    ),
    # Violence: Thorne, the lad in the red scarf, half turned. Dace's build
    # with the colour gone out of it, no rifle, and the scarf.
    "thorne": dict(
        skin=(196, 186, 190), skin_s=(146, 136, 146), hair=(60, 54, 56), hair_h=(88, 80, 82),
        hair_style="helmet", top=(78, 82, 70), top_s=(50, 54, 44), trim=(120, 110, 80),
        pants=(66, 68, 58), pants_s=(44, 46, 38), boots=(44, 36, 32), torso=12, legs=(8, 7),
        scarf=(200, 48, 52), scarf_s=(140, 30, 36), prop="hands", eyes=(230, 200, 120),
    ),
    # Earth hub: the nun who stopped praying.
    "ava": dict(
        skin=SKIN_PALE, skin_s=SKIN_PALE_S, hair=(30, 28, 40), hair_h=(60, 56, 76),
        hair_style="veil", top=(36, 32, 48), top_s=(22, 20, 32), trim=(240, 240, 236),
        dress=True, sleeves=True, torso=11, legs=(8, 7), prop="hands", eyes=(50, 40, 60),
    ),
    # Purgatory: the soul who refused both the Basin and the fall. Hooded,
    # grey, leaning on a pole.
    "ferryman": dict(
        skin=(200, 196, 200), skin_s=(150, 146, 156), hair=(50, 48, 60), hair_h=(84, 80, 96),
        hair_style="veil", top=(62, 60, 74), top_s=(38, 36, 48), trim=(96, 92, 108),
        dress=True, sleeves=True, lean=6, torso=11, legs=(8, 7), prop="stick", eyes=(230, 226, 200),
    ),
    # Lust: the demon captain who served under the Lowest. Broad, red,
    # helmeted, a blade held low.
    "gorrow": dict(
        skin=(160, 62, 56), skin_s=(110, 38, 40), hair=(44, 30, 36), hair_h=(90, 64, 70),
        hair_style="helmet", top=(70, 44, 48), top_s=(44, 26, 30), trim=(150, 120, 60),
        pants=(50, 36, 40), pants_s=(34, 24, 28), boots=(30, 22, 24), torso=13, legs=(9, 8),
        broad=True, prop="hammer", eyes=(240, 200, 90),
    ),
    # Greed: the Bargainer, who buys memories. Thin, violet, bearded, hands
    # out for the trade.
    "vael": dict(
        skin=(146, 100, 170), skin_s=(100, 62, 124), hair=(60, 30, 80), hair_h=(120, 80, 150),
        hair_style="bald_beard", top=(84, 60, 110), top_s=(54, 36, 74), trim=(200, 170, 90),
        dress=True, sleeves=True, torso=12, legs=(8, 7), prop="hands", eyes=(240, 220, 120),
    ),
}


def leg(c, hip, thigh, shin, lengths, pants, boot, edge=None):
    t, s = lengths
    knee = pt(hip, thigh, t)
    ankle = pt(knee, shin, s)
    c.limb(hip, knee, 3, pants, edge)
    c.limb(knee, ankle, 3, pants, edge)
    c.limb(pt(knee, shin, s - 2), ankle, 3, boot, edge)
    toe = pt(ankle, shin + 90, 3)
    c.limb(ankle, toe, 2, boot, edge)


def arm(c, sh, up, low, sleeve, skin, edge=None, stump=False):
    el = pt(sh, up, 5)
    c.limb(sh, el, 3, sleeve, edge)
    if stump:
        end = pt(el, low, 3)
        c.limb(el, end, 3, sleeve, edge)
        c.stamp(end[0], end[1], 3, PAPER_S)  # the bandage
        return end
    hand = pt(el, low, 5)
    c.limb(el, pt(el, low, 3), 3, sleeve, edge)
    c.limb(pt(el, low, 3), hand, 2, skin, edge)
    return hand


def dress(c, hip, hem_y, col, col_s, trim):
    hx, hy = hip
    c.poly([(hx - 4, hy - 1), (hx + 4, hy - 1), (hx + 8, hem_y), (hx - 8, hem_y)], col_s)
    c.poly([(hx - 3, hy - 1), (hx + 3, hy - 1), (hx + 6, hem_y - 1), (hx - 6, hem_y - 1)], col)
    c.line((hx - 8, hem_y), (hx + 8, hem_y), 1, trim)
    c.line((hx + 1, hy + 2), (hx + 3, hem_y - 2), 1, col_s)
    # Feet showing under the hem.
    c.rect(hx - 4, hem_y + 1, hx - 2, hem_y + 2, EDGE)
    c.rect(hx + 2, hem_y + 1, hx + 4, hem_y + 2, EDGE)


def scarf(c, neck, ch, t=0):
    """A scarf wound at the neck, its tail down the back, lifting a little."""
    nx, ny = round(neck[0]), round(neck[1])
    col, col_s = ch["scarf"], ch["scarf_s"]
    c.rect(nx - 4, ny + 1, nx + 3, ny + 3, col)
    c.line((nx - 4, ny + 3), (nx + 3, ny + 3), 1, col_s)
    tail = [(nx - 4, ny + 3), (nx - 6, ny + 8 - (t % 2)), (nx - 5, ny + 13 - (t % 2))]
    c.line(tail[0], tail[1], 2, col)
    c.line(tail[1], tail[2], 2, col_s)


def torso(c, hip, neck, ch, broad=False):
    hx, hy = hip
    nx, ny = neck
    w = 5 if broad else 4
    c.poly([(hx - w, hy + 1), (hx + w, hy + 1), (nx + w - 1, ny + 1), (nx - w + 1, ny + 1)], ch["top_s"])
    c.poly([(hx - w + 2, hy + 1), (hx + w, hy + 1), (nx + w - 1, ny + 1), (nx - w + 3, ny + 1)], ch["top"])
    if ch.get("apron"):
        c.poly([(hx - 3, hy + 2), (hx + 3, hy + 2), (nx + 2, ny + 3), (nx - 2, ny + 3)], ch["apron"])
        c.line((nx - 1, ny + 3), (nx - 2, ny + 1), 1, ch["trim"])
    c.line((hx - w, hy + 1), (hx + w, hy + 1), 1, ch["trim"])
    c.line((nx - 1, ny + 1), (nx + 2, ny + 1), 1, ch["trim"])


def head(c, fx, hc, ch, talk=False, nod=0):
    x, y = round(hc[0]), round(hc[1]) + nod
    skin, skin_s = ch["skin"], ch["skin_s"]
    hair, hair_h = ch["hair"], ch["hair_h"]
    style = ch["hair_style"]
    c.rect(x - 3, y - 3, x + 3, y + 3, skin)
    c.rect(x - 2, y - 4, x + 2, y + 4, skin)
    c.rect(x - 3, y + 2, x - 1, y + 3, skin_s)
    if style == "bun":
        c.rect(x - 3, y - 4, x + 2, y - 3, hair)
        c.rect(x - 4, y - 3, x - 2, y, hair)
        c.rect(x - 6, y - 3, x - 4, y - 1, hair)  # the bun
        c.dot(x - 5, y - 3, hair_h)
        c.dot(x, y - 4, hair_h)
    elif style == "bald_beard":
        c.rect(x - 4, y - 2, x - 3, y + 1, hair)  # hair at the temple only
        c.rect(x - 2, y + 3, x + 3, y + 5, hair)  # beard
        c.rect(x - 1, y + 4, x + 2, y + 6, hair)
        c.dot(x + 1, y + 5, hair_h)
        c.dot(x - 1, y - 4, skin_s)
    elif style == "pigtails":
        c.rect(x - 3, y - 4, x + 2, y - 2, hair)
        c.rect(x - 4, y - 3, x - 3, y - 1, hair)
        c.rect(x - 6, y - 1, x - 5, y + 3, hair)  # back pigtail
        c.rect(x + 3, y - 1, x + 4, y + 3, hair)  # front pigtail
        c.dot(x - 6, y + 3, hair_h)
        c.dot(x + 4, y + 3, hair_h)
        c.dot(x, y - 4, hair_h)
    elif style == "helmet":
        c.rect(x - 4, y - 5, x + 3, y - 2, hair)
        c.rect(x - 5, y - 2, x + 4, y - 1, hair)
        c.dot(x + 4, y - 1, hair_h)
        c.line((x - 4, y - 4), (x + 2, y - 4), 1, hair_h)
    elif style == "veil":
        c.rect(x - 4, y - 5, x + 3, y - 3, ch["trim"])  # the wimple's white band
        c.rect(x - 4, y - 2, x - 3, y + 4, ch["trim"])
        c.rect(x - 6, y - 5, x - 4, y + 5, hair)  # the veil behind
        c.rect(x - 5, y - 6, x + 2, y - 5, hair)
    c.dot(x - 1, y + 1, skin_s)  # ear
    c.dot(x + 2, y, ch["eyes"])
    c.dot(x + 3, y + 1, skin_s)  # nose
    if talk:
        c.dot(x + 2, y + 3, MOUTH)
    else:
        c.dot(x + 2, y + 3, skin_s)
    if style == "bald_beard" and talk:
        c.dot(x + 2, y + 3, MOUTH)


def prop(c, fx, kind, hand, ch, t=0, talk=False):
    if kind == "hammer":
        head_ = pt(hand, 0, 9)
        c.limb(hand, head_, 2, WOOD, None)
        c.rect(head_[0] - 3, head_[1] - 1, head_[0] + 2, head_[1] + 2, IRON)
        c.dot(head_[0] - 2, head_[1] - 1, IRON_H)
    elif kind == "stick":
        top = pt(hand, 180, 7)
        c.limb(pt(hand, 0, 9), top, 2, WOOD, None)
        for k in range(4):
            c.dot(top[0] + 1, top[1] + 2 + k * 2, WOOD_S)
    elif kind == "paper":
        x, y = round(hand[0]), round(hand[1])
        c.rect(x - 1, y - 6, x + 6, y + 1, PAPER)
        c.rect(x + 6, y - 6, x + 6, y + 1, PAPER_S)
        # Hanne's drawing: the tally mark.
        for k in range(3):
            c.dot(x + 1 + k * 2, y - 3, EDGE)
        c.line((x, y - 1), (x + 5, y - 2), 1, EDGE)
    elif kind == "rifle":
        # Slung across the back: a diagonal behind the body.
        a = add(hand, -9, -16)
        b = add(hand, 2, 4)
        c.limb(a, b, 2, WOOD, None)
        c.line(a, pt(a, 180 + 30, 4), 2, IRON)
        c.dot(b[0], b[1], IRON_H)
    elif kind == "hands":
        c.stamp(hand[0] - 1, hand[1], 3, ch["skin"])
        c.dot(hand[0], hand[1] - 1, ch["skin_s"])


def frame(ch, p):
    body, fx, back = Canvas(W, H), Canvas(W, H), Canvas(W, H)
    bob = p.get("bob", 0)
    lean = ch.get("lean", 0) + p.get("lean", 0)
    legs = ch["legs"]
    feet_y = 45
    hip = (32 + p.get("dx", 0), feet_y - (legs[0] + legs[1]) + bob)
    neck = pt(hip, 180 - lean, ch["torso"])
    sh = add(neck, 0, 2)
    hc = add(pt(neck, 180 - lean * 0.6, 4), 1, 0)
    skin, skin_s = ch["skin"], ch["skin_s"]
    sleeve = ch["top"] if ch.get("sleeves") else skin
    sleeve_s = ch["top_s"] if ch.get("sleeves") else skin_s
    talk = p.get("talk", False)

    if ch.get("prop") == "rifle":
        prop(back, fx, "rifle", add(sh, 2, 2), ch)
    arm(body, add(sh, -1, 0), p["aub"], p["alb"], sleeve_s, skin_s)
    if ch.get("dress"):
        dress(body, hip, feet_y - 1 + bob, ch["top"], ch["top_s"], ch["trim"])
    else:
        leg(body, add(hip, -1, 1), p["tb"], p["sb"], legs, ch["pants_s"], ch["boots"])
    torso(body, hip, neck, ch, ch.get("broad", False))
    if ch.get("scarf"):
        scarf(body, neck, ch, p.get("t", 0))
    if not ch.get("dress"):
        leg(body, add(hip, 1, 1), p["tf"], p["sf"], legs, ch["pants"], ch["boots"], EDGE)
    head(body, fx, hc, ch, talk, p.get("nod", 0))
    hand = arm(body, add(sh, 1, 0), p["auf"], p["alf"], sleeve, skin, EDGE, ch.get("stump", False))
    if ch.get("stump"):
        # The hammer is in the other hand, held low across the body.
        other = arm(body, add(sh, -2, 1), p["aub"] + 20, p["alb"] + 30, sleeve_s, skin_s, EDGE)
        prop(body, fx, "hammer", other, ch, p.get("t", 0), talk)
    elif ch.get("prop") and ch["prop"] != "rifle":
        prop(body, fx, ch["prop"], hand, ch, p.get("t", 0), talk)
    return compose(W, H, back, body, fx)


def poses(ch):
    base = dict(tf=8, sf=-4, tb=-8, sb=2, auf=10, alf=40, aub=-12, alb=10)
    if ch.get("prop") == "hands":
        base.update(auf=25, alf=85, aub=20, alb=80)
    if ch.get("prop") == "paper":
        base.update(auf=20, alf=100)
    if ch.get("prop") == "stick":
        base.update(auf=5, alf=20)
    anims = {}
    idle = []
    for i in range(4):
        b = [0, 0, 1, 1][i]
        idle.append(dict(base, bob=b, t=i, alf=base["alf"] - b * 4, aub=base["aub"] - b * 3))
    anims["idle"] = (idle, 5)
    talk = []
    for i in range(4):
        nod = [0, 1, 0, 0][i]
        open_ = i in (1, 2)
        talk.append(dict(base, bob=[0, 1, 0, 0][i], t=i, talk=open_, nod=nod,
                         auf=base["auf"] + [0, 25, 35, 15][i], alf=base["alf"] + [0, 20, 30, 10][i],
                         lean=[0, 2, 2, 0][i]))
    anims["talk"] = (talk, 6)
    return anims


def build(name):
    ch = CHARACTERS[name]
    return {k: ([frame(ch, p) for p in ps], fps) for k, (ps, fps) in poses(ch).items()}
