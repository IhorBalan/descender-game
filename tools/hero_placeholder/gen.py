#!/usr/bin/env python3
"""Draw the placeholder hero's animation strips into godot_assets/sprites/hero/.

The hero is a simple figure built from a few limbs, drawn pose by pose at
game scale so every frame fits the hero's collision box (Player.tscn: 14x32
level pixels, the origin at the feet). The body of every frame fills the box
from the top of the head to the soles, so what the player sees standing on a
ledge, pressed to a wall or landing is what the physics has. Only the sword
and the wings reach outside it.

    python3 tools/hero_placeholder/gen.py            # write the strips
    python3 tools/hero_placeholder/gen.py --sheet out.png   # and a contact sheet

The script rewrites the hero entry of godot_assets/sprites/sprites.json and checks
every frame against the box (see check_frame). Needs Python 3 with Pillow.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, "tools"))
import sprite_frames  # noqa: E402

SPRITES = os.path.join(ROOT, "godot_assets", "sprites")
OUT_DIR = os.path.join(SPRITES, "hero")
SPEC_PATH = os.path.join(SPRITES, "sprites.json")

FRAME_W, FRAME_H = 64, 48
ORIGIN = (32, 44)  # the feet, in the frame
# The collision box in level pixels around the origin (Player.tscn).
BOX_W, BOX_H = 14, 32
BOX = (-BOX_W // 2, -BOX_H, BOX_W // 2, 0)  # x0, y0, x1, y1 (exclusive)

SKIN = (236, 196, 160, 255)
HAIR = (66, 44, 56, 255)
TUNIC = (232, 226, 210, 255)
TUNIC_SHADE = (200, 190, 172, 255)
BELT = (128, 86, 52, 255)
PANTS = (76, 76, 108, 255)
BOOT = (96, 64, 44, 255)
SWORD = (204, 210, 222, 255)
SWORD_EDGE = (244, 248, 252, 255)
HILT = (150, 108, 64, 255)
WING = (224, 238, 252, 255)
WING_SHADE = (176, 200, 228, 255)
GLOW = (255, 232, 150, 255)
OUTLINE = (30, 24, 40, 255)
EYE = (30, 24, 40, 255)

LEG_W = 3
ARM_W = 2
SWORD_LEN = 12


class Pose:
    """One frame of the figure, facing right, in level pixels from the feet.

    Points are (x, y) with y negative upwards. Legs and arms are joint chains
    (hip/shoulder -> knee/elbow -> foot/hand). The sword hangs from the front
    hand at an angle in degrees: 0 points forward, 90 down, -90 up.
    """

    def __init__(self, **kw):
        self.hip = (0, -13)
        self.shoulder = (0, -23)
        self.head = (1, -27)
        self.back_leg = ((-2, -7), (-2, -2))
        self.front_leg = ((2, -7), (2, -2))
        self.back_arm = ((-2, -18), (-1, -14))
        self.front_arm = ((3, -18), (4, -14))
        self.sword = 65  # degrees, or None
        self.sword_hand = "front"
        self.wings = None  # None, "open", "up", "down"
        self.glow = ()  # extra bright dots (x, y)
        self.lying = None  # a death frame drawn on the side: (x, y) of the hips
        self.eye = True
        self.arms = True
        for key, value in kw.items():
            if not hasattr(self, key):
                raise KeyError(key)
            setattr(self, key, value)


def _px(p):
    return (ORIGIN[0] + p[0], ORIGIN[1] + p[1])


def _line(draw, a, b, color, width):
    draw.line([_px(a), _px(b)], fill=color, width=width)
    # Round the joints so bends don't leave notches.
    r = width // 2
    for p in (a, b):
        x, y = _px(p)
        draw.ellipse((x - r, y - r, x + r, y + r), fill=color)


def _leg(draw, hip, leg, boot=True):
    knee, foot = leg
    _line(draw, hip, knee, PANTS, LEG_W)
    _line(draw, knee, (foot[0], foot[1] - 1), PANTS, LEG_W)
    if boot:
        # The sole is the row above the foot point; the outline is the last.
        x, y = _px(foot)
        draw.rectangle((x - 1, y - 2, x + 2, y), fill=BOOT)


def _arm(draw, shoulder, arm, sleeve=True):
    elbow, hand = arm
    _line(draw, shoulder, elbow, TUNIC_SHADE if sleeve else SKIN, ARM_W)
    _line(draw, elbow, hand, SKIN, ARM_W)
    x, y = _px(hand)
    draw.rectangle((x - 1, y - 1, x + 1, y + 1), fill=SKIN)


def _sword(draw, hand, angle):
    a = math.radians(angle)
    dx, dy = math.cos(a), math.sin(a)
    hx, hy = hand
    # The grip sits in the hand, the blade runs from the guard.
    guard = (hx + dx * 2, hy + dy * 2)
    tip = (hx + dx * (2 + SWORD_LEN), hy + dy * (2 + SWORD_LEN))
    _line(draw, (hx - dx * 2, hy - dy * 2), guard, HILT, 2)
    gx, gy = _px(guard)
    draw.rectangle((gx - 1, gy - 1, gx + 1, gy + 1), fill=HILT)
    draw.line([_px(guard), _px(tip)], fill=SWORD, width=2)
    draw.line([_px(guard), _px(tip)], fill=SWORD_EDGE, width=1)


def _wings(draw, shoulder, kind):
    sx, sy = shoulder
    if kind == "open":
        pts = [(sx - 2, sy), (sx - 14, sy - 7), (sx - 16, sy - 2), (sx - 9, sy + 1), (sx - 2, sy + 4)]
    elif kind == "up":
        pts = [(sx - 2, sy), (sx - 10, sy - 12), (sx - 14, sy - 9), (sx - 7, sy + 1), (sx - 2, sy + 4)]
    elif kind == "down":
        pts = [(sx - 2, sy), (sx - 12, sy + 2), (sx - 14, sy + 8), (sx - 8, sy + 5), (sx - 2, sy + 4)]
    else:
        return
    draw.polygon([_px(p) for p in pts], fill=WING, outline=WING_SHADE)
    # Feather lines.
    draw.line([_px((sx - 3, sy + 1)), _px(pts[2])], fill=WING_SHADE, width=1)


def draw_pose(pose: Pose) -> Image.Image:
    img = Image.new("RGBA", (FRAME_W, FRAME_H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    if pose.lying is not None:
        _draw_lying(draw, pose)
    else:
        _draw_standing(draw, pose)
    return _outlined(img)


def _draw_standing(draw, pose):
    hip, sh, head = pose.hip, pose.shoulder, pose.head
    if pose.wings:
        _wings(draw, sh, pose.wings)
    back_hand = pose.back_arm[1]
    front_hand = pose.front_arm[1]
    if pose.sword is not None and pose.sword_hand == "back":
        _sword(draw, back_hand, pose.sword)
    if pose.arms:
        _arm(draw, sh, pose.back_arm)
    _leg(draw, (hip[0] - 1, hip[1]), pose.back_leg)
    # Torso: shoulders wider than the hips, a belt at the waist.
    draw.polygon(
        [_px((sh[0] - 4, sh[1] - 1)), _px((sh[0] + 4, sh[1] - 1)), _px((hip[0] + 4, hip[1] + 1)), _px((hip[0] - 4, hip[1] + 1))],
        fill=TUNIC,
    )
    draw.line([_px((hip[0] - 3, hip[1] - 1)), _px((hip[0] + 3, hip[1] - 1))], fill=BELT, width=1)
    draw.line([_px((sh[0] - 3, sh[1] + 2)), _px((hip[0] - 3, hip[1] - 2))], fill=TUNIC_SHADE, width=1)
    _leg(draw, (hip[0] + 1, hip[1]), pose.front_leg)
    # Head: a round face under a cap of hair, one eye towards the front.
    hx, hy = _px(head)
    draw.ellipse((hx - 4, hy - 4, hx + 4, hy + 4), fill=SKIN)
    draw.chord((hx - 4, hy - 4, hx + 4, hy + 4), 190, 350, fill=HAIR)
    draw.rectangle((hx - 4, hy - 2, hx - 2, hy + 1), fill=HAIR)
    if pose.eye:
        draw.point((hx + 2, hy), fill=EYE)
    if pose.arms:
        _arm(draw, sh, pose.front_arm)
    if pose.sword is not None and pose.sword_hand == "front":
        _sword(draw, front_hand, pose.sword)
    for g in pose.glow:
        x, y = _px(g)
        draw.rectangle((x - 1, y - 1, x + 1, y + 1), fill=GLOW)


def _draw_lying(draw, pose):
    # On his back, head to the rear: the body runs along the ground.
    x, y = pose.lying
    draw.rectangle(_px((x - 10, y - 5)) + _px((x + 1, y - 1)), fill=TUNIC)
    draw.line([_px((x - 9, y - 5)), _px((x - 9, y - 1))], fill=BELT, width=1)
    draw.rectangle(_px((x - 9, y - 7)) + _px((x + 1, y - 5)), fill=TUNIC_SHADE)
    draw.rectangle(_px((x + 1, y - 4)) + _px((x + 11, y - 1)), fill=PANTS)
    draw.rectangle(_px((x + 10, y - 6)) + _px((x + 12, y - 1)), fill=BOOT)
    hx, hy = _px((x - 14, y - 4))
    draw.ellipse((hx - 4, hy - 4, hx + 4, hy + 4), fill=SKIN)
    draw.chord((hx - 4, hy - 4, hx + 4, hy + 4), 120, 280, fill=HAIR)
    draw.rectangle(_px((x - 6, y - 8)) + _px((x - 4, y - 5)), fill=SKIN)
    draw.line([_px((x + 2, y - 1)), _px((x + 14, y - 1))], fill=SWORD, width=2)
    draw.line([_px((x + 2, y - 1)), _px((x + 14, y - 1))], fill=SWORD_EDGE, width=1)


def _outlined(img: Image.Image) -> Image.Image:
    """A one pixel dark outline around everything drawn."""
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    alpha = img.getchannel("A")
    mask = Image.new("L", img.size, 0)
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        shifted = Image.new("L", img.size, 0)
        shifted.paste(alpha, (dx, dy))
        mask = Image.eval(Image.composite(shifted, mask, shifted), lambda v: 255 if v else 0)
    line = Image.new("RGBA", img.size, OUTLINE)
    out.paste(line, (0, 0), mask)
    out.alpha_composite(img)
    return out


# --- Poses ---------------------------------------------------------------


def stand(bob=0, **kw):
    """The standing pose with the body raised by `bob` (negative lowers it)."""
    b = -bob
    base = dict(
        hip=(0, -13 + b),
        shoulder=(0, -23 + b),
        head=(1, -27 + b),
        back_arm=((-2, -18 + b), (-1, -14 + b)),
        front_arm=((3, -18 + b), (4, -14 + b)),
    )
    base.update(kw)
    return Pose(**base)


def idle_frames():
    frames = []
    for i in range(6):
        # A slow breath: the chest and head settle a pixel and come back.
        down = 1 if i in (2, 3) else 0
        p = Pose(
            shoulder=(0, -23 + down),
            head=(1, -27 + down),
            back_arm=((-2, -18 + down), (-1, -14 + down)),
            front_arm=((3, -18 + down), (4, -14 + down)),
            sword=65 + (3 if i in (3, 4) else 0),
        )
        frames.append(p)
    return frames


def run_frames():
    frames = []
    n = 8
    for i in range(n):
        t = i / n * math.tau
        # Two strides a cycle: each foot plants ahead, drives back under the
        # hips and swings forward again through the air, knee up.
        def leg(phase):
            s = math.sin(t + phase)
            c = math.cos(t + phase)
            foot_x = 5 * s
            swinging = c > 0  # moving forward
            lift = 5 * c if swinging else 0.0
            knee_x = 3 * s + (2 if swinging else 1)
            knee_y = -8 - lift * 0.6
            return ((knee_x, knee_y), (foot_x, -2 - lift))

        # The body dips a pixel as each foot plants.
        bob = 1 if i % 4 in (1, 2) else 0
        front = leg(0.0)
        back = leg(math.pi)
        swing = math.sin(t)
        p = Pose(
            hip=(0, -13 + bob),
            shoulder=(2, -23 + bob),
            head=(3, -27 + bob),
            back_leg=back,
            front_leg=front,
            # Arms pump against the legs; the sword rides low in the back
            # hand so the legs stay readable.
            back_arm=((-1 - 2 * swing, -18 + bob), (-2 - 3 * swing, -14 + bob)),
            front_arm=((4 + 2 * swing, -18 + bob), (5 + 3 * swing, -15 + bob)),
            sword=140 + 10 * swing,
            sword_hand="back",
        )
        frames.append(p)
    return frames


def skid_frames():
    # Feet forward, weight back: the stop before a turn.
    return [
        Pose(
            hip=(-1, -13),
            shoulder=(-3, -23),
            head=(-2, -27),
            back_leg=((-3, -8), (-5, -2)),
            front_leg=((2, -8), (4, -2)),
            back_arm=((-5, -20), (-6, -16)),
            front_arm=((0, -19), (1, -15)),
            sword=120,
        )
    ]


def jump_frames():
    launch = Pose(
        shoulder=(0, -23),
        head=(1, -27),
        back_leg=((-2, -8), (-3, -2)),
        front_leg=((2, -8), (3, -2)),
        back_arm=((-3, -26), (-4, -30)),
        front_arm=((4, -26), (5, -30)),
        sword=-100,
    )
    rise = Pose(
        hip=(0, -14),
        back_leg=((-1, -9), (-3, -2)),
        front_leg=((4, -8), (3, -2)),
        back_arm=((-3, -25), (-4, -29)),
        front_arm=((4, -21), (6, -24)),
        sword=-60,
    )
    apex = Pose(
        hip=(0, -14),
        back_leg=((-2, -8), (-3, -2)),
        front_leg=((3, -8), (3, -2)),
        back_arm=((-4, -21), (-6, -19)),
        front_arm=((4, -20), (6, -18)),
        sword=40,
    )
    return [launch, rise, apex]


def fall_frames():
    a = Pose(
        back_leg=((-3, -8), (-4, -2)),
        front_leg=((3, -8), (4, -2)),
        back_arm=((-4, -26), (-5, -30)),
        front_arm=((4, -25), (6, -29)),
        sword=-70,
    )
    b = Pose(
        back_leg=((-2, -8), (-4, -2)),
        front_leg=((3, -8), (5, -2)),
        back_arm=((-4, -25), (-6, -28)),
        front_arm=((5, -24), (6, -27)),
        sword=-85,
    )
    return [a, b]


def land_frames():
    deep = Pose(
        hip=(0, -9),
        shoulder=(1, -18),
        head=(2, -22),
        back_leg=((-4, -6), (-4, -2)),
        front_leg=((5, -6), (4, -2)),
        back_arm=((-3, -14), (-4, -10)),
        front_arm=((5, -14), (6, -10)),
        sword=50,
    )
    half = Pose(
        hip=(0, -11),
        shoulder=(0, -21),
        head=(1, -25),
        back_leg=((-3, -7), (-3, -2)),
        front_leg=((4, -7), (3, -2)),
        back_arm=((-2, -17), (-2, -13)),
        front_arm=((4, -17), (5, -13)),
        sword=60,
    )
    return [deep, half]


def dash_frames():
    frames = []
    for i in range(2):
        frames.append(
            Pose(
                hip=(0, -13),
                shoulder=(2, -22),
                head=(3, -26),
                back_leg=((-3, -8), (-5, -2)),
                front_leg=((3, -8), (4, -2)),
                back_arm=((0, -19), (-3, -17)),
                front_arm=((7, -19), (8, -15)),
                sword=160 if i == 0 else 165,
                sword_hand="back",
                eye=True,
            )
        )
    return frames


def wall_frames():
    # Facing the wall (the sprite is flipped towards it): both hands on it,
    # feet against it, sliding.
    frames = []
    for i in range(2):
        d = i
        frames.append(
            Pose(
                hip=(-1, -13),
                shoulder=(0, -23),
                head=(1, -27),
                back_leg=((2, -9 + d), (5, -3)),
                front_leg=((3, -7 - d), (5, -2)),
                back_arm=((3, -25), (6, -27 + d)),
                front_arm=((4, -20), (6, -19 - d)),
                sword=None,
            )
        )
    return frames


def glide_frames():
    frames = []
    for i, wing in enumerate(("open", "up")):
        frames.append(
            Pose(
                hip=(0, -13),
                shoulder=(0, -23),
                head=(1, -27),
                back_leg=((-2, -8), (-3, -2)),
                front_leg=((2, -7), (3, -2)),
                back_arm=((-5, -23), (-7, -22)),
                front_arm=((5, -23), (7, -22)),
                sword=None,
                wings=wing,
            )
        )
    return frames


def double_jump_frames():
    frames = []
    for wing in ("up", "open", "down", "open"):
        frames.append(
            Pose(
                hip=(0, -14),
                back_leg=((-1, -9), (-3, -2)),
                front_leg=((4, -8), (3, -2)),
                back_arm=((-3, -25), (-4, -29)),
                front_arm=((4, -21), (6, -24)),
                sword=-60,
                wings=wing,
            )
        )
    return frames


def attack_frames():
    # Wind up over the shoulder (0-1), the swing comes over and through
    # (2-4, the hitbox window), the follow-through (5).
    hip = (0, -13)
    return [
        Pose(hip=hip, shoulder=(-1, -23), head=(0, -27), back_leg=((-3, -8), (-4, -2)), front_leg=((2, -8), (3, -2)),
             front_arm=((1, -26), (-1, -30)), back_arm=((-3, -19), (-3, -15)), sword=-150),
        Pose(hip=hip, shoulder=(-1, -23), head=(0, -27), back_leg=((-3, -8), (-4, -2)), front_leg=((2, -8), (3, -2)),
             front_arm=((2, -27), (0, -31)), back_arm=((-3, -19), (-3, -15)), sword=-120),
        Pose(hip=hip, shoulder=(1, -23), head=(2, -27), back_leg=((-2, -8), (-4, -2)), front_leg=((3, -8), (4, -2)),
             front_arm=((5, -25), (8, -27)), back_arm=((-3, -19), (-4, -15)), sword=-40),
        Pose(hip=hip, shoulder=(2, -23), head=(3, -27), back_leg=((-2, -8), (-4, -2)), front_leg=((4, -8), (5, -2)),
             front_arm=((6, -21), (9, -20)), back_arm=((-3, -19), (-4, -15)), sword=5),
        Pose(hip=hip, shoulder=(2, -23), head=(3, -27), back_leg=((-2, -8), (-4, -2)), front_leg=((4, -8), (5, -2)),
             front_arm=((6, -19), (8, -15)), back_arm=((-3, -19), (-4, -15)), sword=45),
        Pose(hip=hip, shoulder=(1, -23), head=(2, -27), back_leg=((-2, -8), (-3, -2)), front_leg=((3, -8), (4, -2)),
             front_arm=((5, -18), (6, -14)), back_arm=((-3, -19), (-3, -15)), sword=70),
    ]


def cast_frames():
    hip = (0, -13)
    return [
        Pose(hip=hip, shoulder=(-1, -23), head=(0, -27), back_leg=((-3, -8), (-4, -2)), front_leg=((2, -8), (3, -2)),
             back_arm=((-4, -21), (-6, -24)), front_arm=((2, -19), (1, -15)), sword=75),
        Pose(hip=hip, shoulder=(1, -23), head=(2, -27), back_leg=((-3, -8), (-4, -2)), front_leg=((3, -8), (4, -2)),
             back_arm=((-3, -19), (-3, -15)), front_arm=((5, -23), (9, -22)), sword=None, glow=((11, -22),)),
        Pose(hip=hip, shoulder=(1, -23), head=(2, -27), back_leg=((-3, -8), (-4, -2)), front_leg=((3, -8), (4, -2)),
             back_arm=((-3, -19), (-3, -15)), front_arm=((5, -23), (9, -22)), sword=None, glow=((12, -22), (10, -24))),
    ]


def slam_frames():
    dive = Pose(
        hip=(0, -13),
        shoulder=(0, -23),
        head=(1, -27),
        back_leg=((-2, -8), (-3, -2)),
        front_leg=((2, -8), (3, -2)),
        back_arm=((-4, -21), (-5, -25)),
        front_arm=((5, -20), (6, -16)),
        sword=88,
    )
    landed = Pose(
        hip=(0, -9),
        shoulder=(2, -18),
        head=(3, -22),
        back_leg=((-4, -6), (-4, -2)),
        front_leg=((5, -6), (4, -2)),
        back_arm=((-2, -14), (-3, -10)),
        front_arm=((6, -13), (8, -9)),
        sword=70,
    )
    return [dive, landed]


def hurt_frames():
    a = Pose(
        hip=(0, -13),
        shoulder=(-2, -23),
        head=(-2, -27),
        back_leg=((-2, -8), (-3, -2)),
        front_leg=((3, -8), (4, -2)),
        back_arm=((-5, -22), (-7, -25)),
        front_arm=((2, -22), (4, -25)),
        sword=-30,
        eye=False,
    )
    b = Pose(
        hip=(0, -13),
        shoulder=(-3, -22),
        head=(-3, -26),
        back_leg=((-3, -8), (-4, -2)),
        front_leg=((3, -8), (5, -2)),
        back_arm=((-6, -21), (-8, -23)),
        front_arm=((1, -22), (3, -25)),
        sword=-20,
        eye=False,
    )
    return [a, b]


def heal_frames():
    # Kneeling with a hand on the heart; the light gathers in it.
    frames = []
    for i in range(4):
        glow = [(1, -19)] if i in (1, 2) else []
        if i == 2:
            glow += [(4, -21), (-2, -22)]
        frames.append(
            Pose(
                hip=(0, -9),
                shoulder=(1, -19),
                head=(2, -23),
                back_leg=((-4, -5), (-5, -2)),
                front_leg=((4, -6), (4, -2)),
                back_arm=((-3, -15), (-3, -11)),
                front_arm=((4, -16), (2, -19)),
                sword=None,
                glow=tuple(glow),
                eye=i not in (1, 2),
            )
        )
    return frames


def death_frames():
    stagger = Pose(
        hip=(0, -13),
        shoulder=(-2, -23),
        head=(-2, -27),
        back_leg=((-3, -8), (-4, -2)),
        front_leg=((3, -8), (5, -2)),
        back_arm=((-5, -22), (-7, -25)),
        front_arm=((2, -22), (4, -25)),
        sword=-30,
        eye=False,
    )
    knees = Pose(
        hip=(-1, -9),
        shoulder=(-2, -19),
        head=(-2, -23),
        back_leg=((-5, -4), (-6, -2)),
        front_leg=((3, -5), (4, -2)),
        back_arm=((-4, -15), (-5, -11)),
        front_arm=((1, -15), (2, -11)),
        sword=80,
        eye=False,
    )
    falling = Pose(
        hip=(-3, -6),
        shoulder=(-9, -11),
        head=(-12, -13),
        back_leg=((0, -4), (3, -2)),
        front_leg=((2, -4), (5, -2)),
        back_arm=((-10, -8), (-12, -4)),
        front_arm=((-6, -8), (-7, -4)),
        sword=None,
        eye=False,
    )
    lying = Pose(lying=(0, 0), eye=False)
    return [stagger, knees, falling, lying, lying]


# name -> (frames, fps, loop)
ANIMATIONS = {
    "idle": (idle_frames, 6, True),
    "run": (run_frames, 12, True),
    "skid": (skid_frames, 1, False),
    "jump": (jump_frames, 8, False),
    "fall": (fall_frames, 6, True),
    "land": (land_frames, 15, False),
    "dash": (dash_frames, 12, True),
    "wall": (wall_frames, 6, True),
    "glide": (glide_frames, 8, True),
    "double_jump": (double_jump_frames, 20, False),
    "attack": (attack_frames, 21, False),
    "cast": (cast_frames, 14, False),
    "slam": (slam_frames, 1, False),
    "hurt": (hurt_frames, 10, False),
    "heal": (heal_frames, 8, True),
    "death": (death_frames, 8, False),
}
# Frames where the body is meant to be below the top of the box (crouched,
# kneeling, down) or past its sides (lying): the fill check is relaxed.
LOW = {"land", "slam", "heal", "death", "hurt"}
## A breath may settle the head a pixel; a lean may put it this far past a side.
HEAD_SLACK = 1
SIDE_SLACK = 2


def body_mask(pose: Pose) -> Image.Image:
    """The frame's head, torso and legs: what should fill the box. The arms,
    sword, wings and glow reach out of it by design."""
    bare = Pose(**{k: v for k, v in vars(pose).items()})
    bare.sword = None
    bare.wings = None
    bare.glow = ()
    bare.arms = False
    return draw_pose(bare).getchannel("A")


def check_frame(name: str, index: int, pose: Pose) -> list[str]:
    """Complaints about a frame whose body does not fit the collision box."""
    if pose.lying is not None:
        return []
    bbox = body_mask(pose).getbbox()
    x0, y0, x1, y1 = (bbox[0] - ORIGIN[0], bbox[1] - ORIGIN[1], bbox[2] - ORIGIN[0], bbox[3] - ORIGIN[1])
    problems = []
    where = "%s[%d]" % (name, index)
    if y1 != BOX[3]:
        problems.append("%s: the feet end at y=%d, not at the floor (0)" % (where, y1))
    if name not in LOW and not BOX[1] <= y0 <= BOX[1] + HEAD_SLACK:
        problems.append("%s: the head is at y=%d, not at the top of the box (%d)" % (where, y0, BOX[1]))
    if name in LOW and y0 < BOX[1]:
        problems.append("%s: the head is at y=%d, above the box (%d)" % (where, y0, BOX[1]))
    if name != "death" and (x0 < BOX[0] - SIDE_SLACK or x1 > BOX[2] + SIDE_SLACK):
        problems.append("%s: the body spans x=%d..%d, outside the box (%d..%d)" % (where, x0, x1, BOX[0], BOX[2]))
    return problems


def build():
    strips = {}
    problems = []
    for name, (make, fps, loop) in ANIMATIONS.items():
        poses = make()
        frames = [draw_pose(p) for p in poses]
        for i, p in enumerate(poses):
            problems += check_frame(name, i, p)
        strip = Image.new("RGBA", (FRAME_W * len(frames), FRAME_H), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            strip.paste(f, (i * FRAME_W, 0))
        strips[name] = (strip, len(frames), fps, loop)
    return strips, problems


def write(strips):
    os.makedirs(OUT_DIR, exist_ok=True)
    for old in os.listdir(OUT_DIR):
        if old.endswith(".png"):
            os.remove(os.path.join(OUT_DIR, old))
    for name, (strip, _, _, _) in strips.items():
        strip.save(os.path.join(OUT_DIR, "hero_%s.png" % name))
    with open(SPEC_PATH) as f:
        spec = json.load(f)
    spec["hero"] = {
        "frame_size": [FRAME_W, FRAME_H],
        "origin": list(ORIGIN),
        "anchor": "feet",
        "animations": {
            name: {"file": "hero_%s.png" % name, "frames": count, "fps": fps, "loop": loop}
            for name, (_, count, fps, loop) in strips.items()
        },
    }
    with open(SPEC_PATH, "w") as f:
        json.dump(spec, f, indent=2)
        f.write("\n")
    sprite_frames.write(["hero"])


def contact_sheet(strips, path, scale=4):
    """Every strip, scaled up, with the collision box drawn over each frame."""
    rows = len(strips)
    width = max(s.width for s, _, _, _ in strips.values())
    label_w = 0
    sheet = Image.new("RGBA", ((width + label_w) * scale, rows * (FRAME_H + 2) * scale), (40, 44, 52, 255))
    draw = ImageDraw.Draw(sheet)
    y = 0
    for name, (strip, count, _, _) in strips.items():
        big = strip.resize((strip.width * scale, strip.height * scale), Image.NEAREST)
        sheet.alpha_composite(big, (0, y * scale))
        for i in range(count):
            x0 = (i * FRAME_W + ORIGIN[0] + BOX[0]) * scale
            y0 = (y + ORIGIN[1] + BOX[1]) * scale
            x1 = (i * FRAME_W + ORIGIN[0] + BOX[2]) * scale - 1
            y1 = (y + ORIGIN[1] + BOX[3]) * scale - 1
            draw.rectangle((x0, y0, x1, y1), outline=(60, 255, 90, 255), width=1)
        draw.text((2, y * scale + 2), name, fill=(255, 255, 255, 255))
        y += FRAME_H + 2
    sheet.save(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--sheet", help="also write a contact sheet with the collision box drawn on every frame")
    parser.add_argument("--check", action="store_true", help="only check the frames against the box; write nothing")
    args = parser.parse_args()
    strips, problems = build()
    for p in problems:
        print("warning:", p)
    if not args.check:
        write(strips)
        print("wrote %d strips to %s" % (len(strips), OUT_DIR))
    if args.sheet:
        contact_sheet(strips, args.sheet)
        print("wrote", args.sheet)
    return 1 if problems else 0


if __name__ == "__main__":
    raise SystemExit(main())
