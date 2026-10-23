"""Shade: a forgotten soul of Limbo that drifts down through the circles.

A cowl of grey-blue nothing with two pale eyes, trailing to tatters. It
drifts, then flares and lunges; killed, it unravels and goes out."""
import math
from pix import Canvas, compose, add, tint

W, H = 48, 48
C = (24, 22)
COWL, COWL_S, COWL_D = (96, 108, 138), (66, 76, 104), (42, 48, 72)
INNER = (30, 32, 50)
EYE, EYE_FLARE = (214, 226, 250), (255, 160, 140)
MIST = (150, 170, 210)
EDGE = (24, 16, 32)


def cowl(c, fx, p):
    cx, cy = C[0] + p.get("dx", 0), C[1] + p.get("bob", 0)
    thin = p.get("thin", 0)
    rx, ry = max(1, 9 - thin), 11 + thin
    flare = p.get("flare", 0)
    # the hood: a teardrop, wide at the brow, drawn in below the eyes
    c.ellipse(cx, cy - 2, rx, ry * 0.7, COWL_S)
    c.ellipse(cx + 1, cy - 4, rx - 2, ry * 0.5, COWL)
    c.poly([(cx - rx, cy), (cx + rx, cy), (cx + rx * 0.5, cy + ry), (cx - rx * 0.5, cy + ry)], COWL_S)
    c.poly([(cx - rx + 2, cy), (cx + rx - 1, cy), (cx + rx * 0.3, cy + ry - 2), (cx - rx * 0.3, cy + ry - 2)], COWL_D)
    # the hollow where a face was
    if thin < 4:
        c.ellipse(cx + 2, cy - 1, max(1, rx - 4), 4, INNER)
    # the tatters, swaying
    sway = p.get("sway", 0)
    for k in range(5):
        tx = cx - rx * 0.5 + k * rx * 0.25
        ln = 6 + (k * 5) % 7 + thin * 2
        c.line((tx, cy + ry - 2), (tx + sway + (k % 2) * 2 - 1, cy + ry - 2 + ln), 2, COWL_D if k % 2 else COWL_S, 1)
    if p.get("out"):
        return
    # the eyes: two pale points, red-white when it flares
    col = EYE_FLARE if flare else EYE
    for ex in (cx - 1, cx + 4):
        c.dot(ex, cy - 2, col)
        c.dot(ex + 1, cy - 2, col)
        fx.glow(ex, cy - 2, 2 + flare, col, 70 + flare * 40)
    if flare:
        # the mouth it does not have opens
        c.line((cx, cy + 2), (cx + 5, cy + 2 + flare), 1 + flare // 2, EYE_FLARE)


def frame(p):
    body, fx, back = Canvas(W, H), Canvas(W, H), Canvas(W, H)
    cx, cy = C[0] + p.get("dx", 0), C[1] + p.get("bob", 0)
    if not p.get("out"):
        back.glow(cx, cy, 11, MIST, 30 + p.get("flare", 0) * 15)
    cowl(body, fx, p)
    # motes of what it was, drifting off it
    t = p.get("t", 0)
    for k in range(3):
        a = t * 0.9 + k * 2.1
        back.blend(cx + math.cos(a) * 13, cy + math.sin(a) * 9 + 4, MIST, 110 - k * 30)
    img = compose(W, H, back, body, fx)
    if p.get("hurt"):
        img = tint(img, (255, 255, 255), 0.3)
    return img


def poses():
    anims = {}
    anims["idle"] = ([dict(t=i, bob=[0, -1, -1, 0, 1, 1][i], sway=[0, 1, 2, 1, 0, -1][i]) for i in range(6)], 8)
    anims["attack"] = ([
        dict(t=0, flare=1, bob=-1, sway=-2),
        dict(t=1, flare=2, dx=3, bob=0, sway=-3),
        dict(t=2, flare=3, dx=6, bob=1, sway=-4),
        dict(t=3, flare=1, dx=3, sway=-2),
    ], 10)
    anims["hurt"] = ([dict(t=0, dx=-2, sway=3, thin=1, hurt=True), dict(t=1, dx=-1, sway=2)], 8)
    # Death: it stretches thin, the eyes go out, and it unravels upward.
    anims["death"] = ([
        dict(t=0, thin=1, sway=3, hurt=True),
        dict(t=1, thin=2, bob=-2, sway=4),
        dict(t=2, thin=4, bob=-5, sway=5, out=True),
        dict(t=3, thin=6, bob=-9, sway=6, out=True),
    ], 8)
    return anims


def build():
    return {k: ([frame(p) for p in ps], fps) for k, (ps, fps) in poses().items()}
