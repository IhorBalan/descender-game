"""The garden's near backdrop: the depth between the walk and the far distance.

The play layer is crisp and the painted distance is hazed, and between the two there was
only air. This layer fills it with what the garden is made of, built from the marble kit
itself: tiers of aqueduct arches, terraces with balustrades and blossom, and stacks of rock with
ruins on them, all standing in their own band of cloud. It is baked (a backdrop takes no
2D light), so each piece is lit here through its normal map by the same sun as the play
layer, then hazed toward the air colour and softened a little, more so near its foot.

The layer is 8192 x 1152, four screens of one screen's height, and wraps sideways. Output
goes to out/Backdrop_Garden, where `scenario_gen.py apply` copies it into the project.

  python tools/ai_art/near_backdrop.py
"""
import glob
import math
import os
import random

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

HERE = os.path.dirname(__file__)
ART = os.path.join(HERE, "..", "..", "Assets", "Art", "Paradise")
KIT = os.path.join(ART, "Kit")
NORMALS = os.path.join(ART, "Normals")
BACKDROPS = os.path.join(ART, "Backdrop_Garden")
OUT = os.path.join(HERE, "out", "Backdrop_Garden")
W, H = 8192, 1152

# As SunAndClouds and ParadiseGarden set them: toward the sun, the sky's and the sun's colours.
SUN = np.array((0.52, 0.6, 0.61), dtype=np.float32)
SUN /= np.linalg.norm(SUN)
SKY_LIGHT = np.array((0.88, 0.93, 1.0), dtype=np.float32)
SUN_LIGHT = np.array((1.0, 0.95, 0.84), dtype=np.float32)
SUN_SHARE = 0.42
AIR = np.array((196, 214, 232), dtype=np.float32) / 255.0

_lit = {}


def lit_piece(name: str) -> Image.Image:
    """A kit piece, lit by the sun through its normal map (flat surfaces come out as painted)."""
    if name in _lit:
        return _lit[name]
    path = glob.glob(os.path.join(KIT, "*", name + ".png"))[0]
    img = Image.open(path).convert("RGBA")
    rgba = np.asarray(img).astype(np.float32) / 255.0
    map_path = os.path.join(NORMALS, name + "_n.png")
    if os.path.exists(map_path):
        normal = np.asarray(Image.open(map_path).convert("RGB").resize(img.size, Image.BILINEAR))
        normal = normal.astype(np.float32) / 127.5 - 1.0
        facing = np.clip((normal * SUN).sum(axis=2), 0.0, None) / SUN[2]
        if name == "rock_fill":
            # At this size its full relief would read as engraved lines.
            facing = 1.0 + (facing - 1.0) * 0.45
    else:
        facing = np.ones(img.size[::-1], dtype=np.float32)
    light = SKY_LIGHT * (1.0 - SUN_SHARE) + SUN_LIGHT * SUN_SHARE * facing[..., None]
    out = rgba.copy()
    out[..., :3] = np.clip(rgba[..., :3] * light, 0.0, 1.0)
    _lit[name] = Image.fromarray((out * 255.0 + 0.5).astype(np.uint8), "RGBA")
    return _lit[name]


def piece(name: str, scale: float, flip: bool = False) -> Image.Image:
    img = lit_piece(name)
    out = img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))), Image.LANCZOS)
    return out.transpose(Image.FLIP_LEFT_RIGHT) if flip else out


def over(canvas: Image.Image, img: Image.Image, x: float, y: float) -> None:
    """Composites `img` with its top left at (x, y), clipped to the canvas."""
    x, y = round(x), round(y)
    box = (max(0, -x), max(0, -y), min(img.width, canvas.width - x), min(img.height, canvas.height - y))
    if box[2] > box[0] and box[3] > box[1]:
        canvas.alpha_composite(img.crop(box), (max(0, x), max(0, y)))


def tiled(name: str, scale: float, width: int, height: int | None = None) -> Image.Image:
    """A piece repeated across `width` (and down `height`, when given)."""
    tile = piece(name, scale)
    out = Image.new("RGBA", (width, height or tile.height), (0, 0, 0, 0))
    for y in range(0, out.height, tile.height):
        for x in range(0, width, tile.width):
            over(out, tile, x, y)
    return out


def aqueduct(arches: int, tiers: int, scale: float, broken: bool, rng: random.Random) -> Image.Image:
    """Tiers of arches, one on another, the piers in line; one end can have fallen away."""
    arch = piece("arch_span", scale)
    step_x, step_y = arch.width - 2, arch.height - round(12 * scale)
    out = Image.new("RGBA", (step_x * arches + 2, step_y * tiers + 12), (0, 0, 0, 0))
    for tier in reversed(range(tiers)):
        for i in range(arches):
            over(out, arch.transpose(Image.FLIP_LEFT_RIGHT) if i % 2 else arch, i * step_x, tier * step_y)
    for i in range(arches):
        if rng.random() < 0.7:
            name = rng.choice(("ivy_curtain", "wisteria_drape", "ivy_drape_a", "ivy_drape_c"))
            drape = piece(name, scale * rng.uniform(0.55, 0.8), rng.random() < 0.5)
            over(out, drape, (i + rng.uniform(0.0, 0.5)) * step_x, -drape.height * 0.02 + 8 * scale)
    if not broken:
        return out
    # The break: a rough line down the last arch, lower toward the outer edge.
    mask = Image.new("L", out.size, 255)
    points = [(out.width, 0)]
    y, inner = 0.0, out.width - step_x * rng.uniform(0.35, 0.6)
    while y < out.height:
        points.append((inner + rng.uniform(-34, 34) * (1.0 + scale), y))
        y += rng.uniform(18, 52)
        inner += rng.uniform(2, 16)
    points.append((out.width, out.height))
    ImageDraw.Draw(mask).polygon(points, fill=0)
    out.putalpha(ImageChops.multiply(out.getchannel("A"), mask.filter(ImageFilter.GaussianBlur(1.0))))
    return out


def planted(out: Image.Image, names, x0: float, x1: float, ground: float, scale: float, rng: random.Random) -> None:
    """Stands a row of plants or ruins on a level line."""
    x = x0
    while x < x1:
        name = rng.choice(names)
        p = piece(name, scale * rng.uniform(0.85, 1.15), rng.random() < 0.5)
        over(out, p, x - p.width / 2, ground - p.height + p.height * 0.03)
        x += p.width * rng.uniform(0.75, 1.5)


def wander(n: int, rng: random.Random, waves: float) -> list:
    """A rough line of n values around 0, about -1..1: a few octaves of sine at random phases."""
    octaves = [(waves * 2**i, 0.5**i, rng.uniform(0.0, 6.28)) for i in range(4)]
    return [sum(a * math.sin(f * 6.28 * k / n + p) for f, a, p in octaves) / 1.875 for k in range(n)]


def stack(width: int, height: int, scale: float, rng: random.Random) -> Image.Image:
    """A pillar of rock out of the clouds: ragged sides, widening down, grass and ruins on a rough top."""
    room = round(720 * scale)
    out = Image.new("RGBA", (width, room + height), (0, 0, 0, 0))
    left_edge, right_edge = wander(height, rng, 2.6), wander(height, rng, 2.2)
    lefts = [width * (0.2 - 0.17 * (y / height)) + width * 0.05 * left_edge[y] for y in range(height)]
    rights = [width * (0.8 + 0.17 * (y / height)) + width * 0.05 * right_edge[y] for y in range(height)]
    x0, x1 = lefts[0], rights[0]
    surface = wander(width, rng, 2.0)

    def top(x: float) -> float:
        return room + height * 0.035 * (surface[min(width - 1, max(0, round(x)))] + 1.0)

    outline = [(x, top(x)) for x in range(round(x0), round(x1), 3)]
    outline += [(rights[y], room + y) for y in range(round(top(x1)) - room, height, 3)]
    outline += [(lefts[y], room + y) for y in range(height - 1, round(top(x0)) - room, -3)]
    mask = Image.new("L", out.size, 0)
    ImageDraw.Draw(mask).polygon(outline, fill=255)
    body = Image.new("RGBA", out.size, (0, 0, 0, 0))
    body.paste(tiled("rock_fill", scale * 0.7, width, height), (0, room))
    body.putalpha(ImageChops.multiply(body.getchannel("A"), mask.filter(ImageFilter.GaussianBlur(1.2))))

    span = x1 - x0
    tree = piece("blossom_tree", scale * 0.5, rng.random() < 0.5)
    at = x0 + span * rng.uniform(0.3, 0.6)
    over(out, tree, at - tree.width / 2, top(at) - tree.height * 0.97)
    ruin = piece("broken_column", scale * 0.7, rng.random() < 0.5)
    at = x0 + span * rng.uniform(0.68, 0.85)
    over(out, ruin, at - ruin.width / 2, top(at) - ruin.height * 0.96)
    out.alpha_composite(body)
    lip = tiled("grass_lip", scale * 0.7, width)
    pivot = lip.height * 0.42
    for x in range(round(x0) + 4, round(x1) - 4, 3):
        over(out, lip.crop((x, 0, x + 3, lip.height)), x, top(x + 1) - pivot)
    # Ivy down the sides: it hides the rim and breaks the long straight falls of rock.
    for u in (rng.uniform(0.05, 0.2), rng.uniform(0.4, 0.6), rng.uniform(0.8, 0.95)):
        ivy = piece(rng.choice(("ivy_drape_a", "ivy_drape_b", "ivy_drape_c")), scale * rng.uniform(0.7, 1.0))
        at = x0 + span * u
        over(out, ivy, at - ivy.width / 2, top(at) - ivy.height * 0.04)
    return out


def terrace(width: int, wall: int, rock: int, scale: float, rng: random.Random) -> Image.Image:
    """A built terrace on a stack of rock: blossom behind a balustrade, a coping, a short wall."""
    margin = round(width * 0.14)
    room = round(920 * scale)
    cap = tiled("wall_cap", scale, width)
    rail = tiled("balustrade", scale, width)
    out = Image.new("RGBA", (width + margin * 2, room + cap.height + wall + rock), (0, 0, 0, 0))
    ground = room + round(cap.height * 0.2)
    planted(out, ["blossom_tree"], margin + width * 0.2, margin + width * 0.9, ground, scale * 0.55, rng)
    planted(out, ["hedge_flowers"], margin + width * 0.1, margin + width * 0.9, ground, scale * 0.8, rng)
    over(out, rail, margin, ground - rail.height)

    # The rock it stands on, ragged and widening down, reaching up behind the wall.
    start = room + cap.height + wall // 2
    height = out.height - start
    left_edge, right_edge = wander(height, rng, 2.4), wander(height, rng, 2.0)
    lefts = [margin * (1.0 - y / height) + width * 0.03 * left_edge[y] + width * 0.04 for y in range(height)]
    rights = [out.width - lefts[y] + width * 0.03 * right_edge[y] for y in range(height)]
    outline = [(lefts[y], start + y) for y in range(0, height, 3)]
    outline += [(rights[y], start + y) for y in range(height - 1, 0, -3)]
    mask = Image.new("L", out.size, 0)
    ImageDraw.Draw(mask).polygon(outline, fill=255)
    body = Image.new("RGBA", out.size, (0, 0, 0, 0))
    body.paste(tiled("rock_fill", scale * 0.7, out.width, height), (0, start))
    body.putalpha(ImageChops.multiply(body.getchannel("A"), mask.filter(ImageFilter.GaussianBlur(1.2))))
    out.alpha_composite(body)

    # The wall sinks into the rock along a rough line, and ivy hides what is left of the join.
    block = tiled("wall_block", scale * 0.75, width, wall)
    foot = wander(width, rng, 3.0)
    wall_mask = Image.new("L", block.size, 0)
    ImageDraw.Draw(wall_mask).polygon(
        [(x, wall * (0.78 + 0.16 * foot[x])) for x in range(0, width, 3)] + [(width, 0), (0, 0)], fill=255
    )
    block.putalpha(ImageChops.multiply(block.getchannel("A"), wall_mask.filter(ImageFilter.GaussianBlur(1.5))))
    over(out, block, margin, room + cap.height - 8)
    over(out, cap, margin, room)
    for u in (rng.uniform(0.04, 0.16), rng.uniform(0.35, 0.6), rng.uniform(0.84, 0.96)):
        ivy = piece(rng.choice(("ivy_curtain", "wisteria_drape", "ivy_drape_b")), scale * rng.uniform(0.65, 0.95))
        over(out, ivy, margin + width * u - ivy.width / 2, room + cap.height * 0.4)
    return out


def hazed(img: Image.Image, top: float, bottom: float, soften: float) -> Image.Image:
    """Toward the air by `top` at its head and `bottom` at its foot, greyer as it goes, slightly soft."""
    a = np.asarray(img).astype(np.float32) / 255.0
    t = np.linspace(0.0, 1.0, img.height, dtype=np.float32).reshape(-1, 1) ** 1.4
    amount = (top + (bottom - top) * t)[..., None]
    grey = a[..., :3].mean(axis=2, keepdims=True)
    colour = a[..., :3] + (grey - a[..., :3]) * amount * 0.7
    a[..., :3] = colour * (1.0 - amount) + AIR * amount
    # Blurred with its alpha premultiplied, so no dark fringe comes in from the empty pixels.
    premultiplied = np.dstack((a[..., :3] * a[..., 3:], a[..., 3]))
    out = Image.fromarray((premultiplied * 255.0 + 0.5).astype(np.uint8), "RGBA")
    out = out.filter(ImageFilter.GaussianBlur(soften))
    blurred = np.asarray(out).astype(np.float32) / 255.0
    alpha = blurred[..., 3:]
    blurred[..., :3] = np.clip(blurred[..., :3] / np.maximum(alpha, 1e-3), 0.0, 1.0)
    return Image.fromarray((blurred * 255.0 + 0.5).astype(np.uint8), "RGBA")


def stand(canvas: Image.Image, img: Image.Image, x: float, base: float, top: float, bottom: float, soften: float) -> None:
    """Stands a group on `base`, centred at x, hazed, wrapping around the layer's seam."""
    done = hazed(img, top, bottom, soften)
    left, y = x - done.width / 2, base - done.height
    for dx in (-W, 0, W):
        over(canvas, done, left + dx, y)


def near() -> Image.Image:
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    rng = random.Random(23)
    far_row = dict(top=0.5, bottom=0.86, soften=1.7)
    near_row = dict(top=0.34, bottom=0.8, soften=1.1)
    # Everything carries on below the screen, so no foot ever shows where the cloud thins.
    # A row further back, so even this layer has depth of its own.
    stand(canvas, aqueduct(6, 4, 0.23, False, rng), 2500, 1230, **far_row)
    stand(canvas, stack(330, 640, 0.3, rng), 4750, 1230, **far_row)
    stand(canvas, aqueduct(5, 4, 0.23, True, rng), 6600, 1230, **far_row)
    # The main row: a few shapes with open sky between them.
    stand(canvas, aqueduct(3, 3, 0.36, True, rng), 800, 1270, **near_row)
    stand(canvas, stack(520, 800, 0.42, rng), 3550, 1300, **near_row)
    stand(canvas, stack(360, 610, 0.34, rng), 3980, 1290, **near_row)
    stand(canvas, terrace(700, 130, 330, 0.36, rng), 5650, 1290, **near_row)
    stand(canvas, aqueduct(2, 3, 0.36, False, rng), 7450, 1270, **near_row)
    # Its own band of cloud, hiding every foot.
    clouds = Image.open(os.path.join(BACKDROPS, "cloud_sea_2.png")).convert("RGBA")
    clouds = ImageChops.offset(clouds, 2300, 0)
    over(canvas, hazed(clouds, 0.04, 0.1, 0.6), 0, 896)
    return canvas


def main() -> None:
    os.makedirs(OUT, exist_ok=True)
    img = near()
    img.save(os.path.join(OUT, "garden_bg4_near.png"))
    print(f"built garden_bg4_near {img.size}")


if __name__ == "__main__":
    main()
