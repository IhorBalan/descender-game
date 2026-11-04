"""Celestial Garden: builds the Paradise kit + backdrops and the previews.

Kit -> Assets/Art/Paradise/Kit/, backdrops -> Assets/Art/Paradise/Backdrop_Garden/.
v2: 4-screen-wide far/midfar/mid/cloud layers, natural cliff rock, noble
ashlar, wall cap, lusher foliage, blossom tree, hedge, wisteria, loggia frame,
waterfall spout.
"""
from __future__ import annotations

import math

import pa_garden_arch as A
import pa_garden_env as E
import pa_garden_ground as G
import pa_garden_nature as N
import pa_garden_plants as P
import pa_garden_wide as WIDE
from pa_core import F32, Layer, gblur, hexc, lerp, np
from pa_garden_paint import SKY

ZONE = "garden"
KA, KN, KW, BG = "Kit/Architecture", "Kit/Nature", "Kit/Water", "Backdrop_Garden"


def make_kit():
    k = {}
    k["column_shaft"] = A.column_shaft()
    k["column_base"] = A.column_base()
    k["column_capital"] = A.column_capital()
    k["arch_span"], k["arch_span_meta"] = A.arch_span(6)
    k["arch_span_wide"], k["arch_span_wide_meta"] = A.arch_span(10, 2111)
    k["deck_top"], _ = A.deck_top()
    k["balustrade"], _ = A.balustrade()
    k["wall_block"] = G.wall_block()
    k["wall_cap"], k["wall_cap_meta"] = G.wall_cap()
    k["banner"], k["banner_pivot"] = A.banner()
    k["lantern"], k["lantern_glow"], _ = A.lantern()
    k["broken_column"], _ = A.broken_column()
    k["fallen_arch_fragment"], _ = A.fallen_arch_fragment()
    k["waterfall_spout"], k["waterfall_spout_meta"] = G.waterfall_spout()
    k["rock_fill"] = G.rock_fill()
    k["grass_lip"], _ = N.grass_lip()
    k["island_bottom"], _ = N.island_bottom()
    for i, v in enumerate("abc"):
        k[f"ivy_drape_{v}"], _ = P.ivy_drape(i)
        k[f"lily_cluster_{v}"], _ = P.lily_cluster(i)
    k["ivy_curtain"], _ = P.ivy_curtain()
    k["vine_hanging"], _ = P.vine_hanging()
    k["flower_bush"], _ = P.flower_bush()
    k["fern"], _ = P.fern()
    k["grass_tuft_strip"], _ = N.grass_tuft_strip()
    k["blossom_tree"], _ = P.blossom_tree()
    k["hedge_flowers"], _ = P.hedge_flowers()
    k["wisteria_drape"], _ = P.wisteria_drape()
    k["waterfall_tex"] = N.waterfall_tex()
    k["waterfall_foam"], _ = N.waterfall_foam()
    k["mist_soft"], _ = N.mist_soft()
    k["loggia_frame"], k["loggia_meta"] = G.loggia_frame(k)
    return k


def build(reg):
    print("  garden kit")
    k = make_kit()
    S = reg.save
    z = ZONE
    S(k["column_shaft"], f"{KA}/column_shaft.png", "Fluted marble column shaft, repeats vertically (1 x 4 units)", z,
      pivot=(0.5, 0.0), tile=("y",), extra={"shaftWidthPx": 54, "attach": "stack on column_base top edge; capital "
                                            "sits on the top edge"})
    S(k["column_base"], f"{KA}/column_base.png", "Column base: plinth with torus mouldings, gold fillet", z,
      pivot=(0.5, 0.0), extra={"topAttachRow": 0, "attach": "pivot = bottom centre on the ground; shaft starts at "
                               "the top edge (row 0), centred"})
    S(k["column_capital"], f"{KA}/column_capital.png", "Corinthian-ish capital: acanthus curls, volutes, gold", z,
      pivot=(0.5, 0.0), extra={"bottomAttachRow": 63, "springingRow": 0,
                               "attach": "pivot = bottom centre on the shaft top; top edge (row 0) is the arch "
                                         "springing line"})
    for key, desc in (("arch_span", "Round arch between column centres 6 units apart"),
                      ("arch_span_wide", "Round arch between column centres 10 units apart")):
        lay, meta = k[key], k[key + "_meta"]
        S(lay, f"{KA}/{key}.png", desc + ": voussoirs, gold keystone, entablature, grassy top", z,
          pivot=(meta["leftColumnCentreX"] / lay.w, 0.0),
          extra={"walkableRow": meta["walkableRow"], "springingRow": lay.h,
                 "leftColumnCentreX": meta["leftColumnCentreX"], "rightColumnCentreX": meta["rightColumnCentreX"],
                 "columnSpacingPx": meta["columnSpacingPx"], "archInnerRadiusPx": meta["archInnerRadiusPx"],
                 "keystoneCentrePx": meta["keystoneCentre"],
                 "attach": "pivot = left column centre on the springing line (bottom edge) = top of that "
                           "column's capital; right column centre is columnSpacingPx to the right. Neighbouring "
                           "spans share a column and overlap by 102 px."})
    S(k["deck_top"], f"{KA}/deck_top.png", "Marble walkway/entablature strip with mossy, flowery top", z,
      pivot=(0.5, 1 - 18 / 77), tile=("x",), extra={"walkableRow": 18})
    S(k["balustrade"], f"{KA}/balustrade.png", "Balustrade for terrace edges (decor in front of the walkway)", z,
      pivot=(0.5, 0.0), tile=("x",), extra={"railTopRow": 0, "footRow": 77,
                                            "attach": "foot (bottom edge) on the walkable line"})
    S(k["wall_block"], f"{KA}/wall_block.png",
      "Noble weathered marble ashlar: 1-3 unit blocks, staggered courses, moss and flowers in some joints (8x8)", z,
      tile=("x", "y"))
    S(k["wall_cap"], f"{KA}/wall_cap.png",
      "Marble coping/cornice topping a terrace wall, grass and flowers spilling over", z,
      pivot=(0.5, 1 - 34 / 96), tile=("x",), extra={"walkableRow": 34,
                                                     "attach": "rows below ~70 overlap the top of wall_block"})
    S(k["banner"], f"{KA}/banner.png", "Cream banner with gold sun emblem on a gold rod; hangs from the rod", z,
      pivot=k["banner_pivot"], extra={"hangRow": 6})
    S(k["lantern"], f"{KA}/lantern.png", "Small hanging gold lantern; hangs from the top", z, pivot=(0.5, 1.0))
    S(k["lantern_glow"], f"{KA}/lantern_glow.png", "Emissive lantern glass", z, pivot=(0.5, 1.0), blend="additive")
    S(k["broken_column"], f"{KA}/broken_column.png", "Ruin: broken column on its base, mossy", z, pivot=(0.5, 0.0))
    S(k["fallen_arch_fragment"], f"{KA}/fallen_arch_fragment.png", "Ruin: fallen arch ring segment in the grass", z,
      pivot=(0.5, 0.0))
    sm = k["waterfall_spout_meta"]
    S(k["waterfall_spout"], f"{KA}/waterfall_spout.png",
      "Carved marble channel mouth set into a wall; water leaves over its lip", z, pivot=(0.0, 0.5),
      extra={"lipPointPx": sm["lipPoint"], "attach": "pivot = left edge middle, set against the wall face; start the "
                                                     "waterfall at lipPointPx (from the top-left)"})
    lm = k["loggia_meta"]
    S(k["loggia_frame"], f"{KA}/loggia_frame.png",
      "Foreground framing piece for the screen's left edge: column + arch + entablature, ivy and wisteria", z,
      pivot=(0.5, 0.0), extra={"columnCentreX": lm["columnCentreX"], "springingRow": lm["springingRow"]})
    S(k["rock_fill"], f"{KN}/rock_fill.png",
      "Natural mossy cliff rock: fractured faces, soft cracks, moss, ferns and ivy in crevices (8x8)", z,
      tile=("x", "y"))
    S(k["grass_lip"], f"{KN}/grass_lip.png", "Lush grass/moss top lip with lilies and blossoms", z,
      pivot=(0.5, 1 - 51 / 128), tile=("x",), extra={"walkableRow": 51})
    S(k["island_bottom"], f"{KN}/island_bottom.png",
      "Rocky underside with hanging roots/moss (rock body on top, hangs downward)", z, pivot=(0.5, 1 - 8 / 160),
      tile=("x",), extra={"attachRow": 8})
    for v in "abc":
        S(k[f"ivy_drape_{v}"], f"{KN}/ivy_drape_{v}.png", f"Ivy drape variant {v}; hangs from the top edge", z,
          pivot=(0.5, 1.0))
        S(k[f"lily_cluster_{v}"], f"{KN}/lily_cluster_{v}.png", f"White lily cluster variant {v} (2.4 x 1.8 units)",
          z, pivot=(0.5, 0.0))
    S(k["ivy_curtain"], f"{KN}/ivy_curtain.png", "Wide ivy curtain; hangs from the top edge", z, pivot=(0.5, 1.0))
    S(k["vine_hanging"], f"{KN}/vine_hanging.png", "Hanging vine with blossoms", z, pivot=(0.5, 1.0))
    S(k["flower_bush"], f"{KN}/flower_bush.png", "Rounded bush with many white blossoms", z, pivot=(0.5, 0.0))
    S(k["fern"], f"{KN}/fern.png", "Fern with overlapping curled fronds", z, pivot=(0.5, 0.0))
    S(k["grass_tuft_strip"], f"{KN}/grass_tuft_strip.png", "Grass tufts strip (foreground/edge dressing)", z,
      pivot=(0.5, 0.0), tile=("x",))
    S(k["blossom_tree"], f"{KN}/blossom_tree.png", "White-blossom orchard tree with a few golden fruit (8 x 10)", z,
      pivot=(0.5, 0.0))
    S(k["hedge_flowers"], f"{KN}/hedge_flowers.png", "Low flowering hedge for terrace edges", z, pivot=(0.5, 0.0),
      tile=("x",))
    S(k["wisteria_drape"], f"{KN}/wisteria_drape.png", "White / pale-gold wisteria curtain to hang from arches", z,
      pivot=(0.5, 1.0))
    S(k["waterfall_tex"], f"{KW}/waterfall_tex.png", "Falling water source for a scrolling shader", z,
      pivot=(0.5, 1.0), tile=("y",))
    S(k["waterfall_foam"], f"{KW}/waterfall_foam.png", "Splash / mist puff at a waterfall foot", z)
    S(k["mist_soft"], f"{KW}/mist_soft.png", "Soft white mist blob", z)
    print("  garden backdrops")
    for i in range(3):
        lay, piv = N.cloud_sea(i, w=8192)
        k[f"cloud_sea_{i}"] = lay
        S(lay, f"{BG}/cloud_sea_{i}.png", ["Cloud sea, back (hazier)", "Cloud sea, middle", "Cloud sea, front "
                                           "(brightest)"][i] + "; 4 screens wide, varied", z, pivot=piv, tile=("x",),
          extra={"layer": i})
    tree, tglow, piv = N.golden_tree()
    k["golden_tree"], k["golden_tree_glow"] = tree, tglow
    S(tree, f"{BG}/golden_tree.png", "Landmark: huge tree with a white-gold crown on its own floating island", z,
      pivot=piv)
    S(tglow, f"{BG}/golden_tree_glow.png", "Emissive crown of the golden tree", z, pivot=piv, blend="additive")
    k["bg_sky"] = E.sky()
    S(k["bg_sky"], f"{BG}/garden_bg0_sky.png", "Sky: deep blue to pale horizon, cumulus, sun upper right, god-rays",
      z, tile=("x",), extra={"layer": 0, "suggestedParallax": 0.98})
    WIDE.set_rock_texture(k["rock_fill"].straight()[0])
    print("  wide far")
    k["bg_far"] = WIDE.far_wide()
    S(k["bg_far"], f"{BG}/garden_bg1_far.png", "Far: varied snowy mountain range in blue haze (4 screens wide)", z,
      tile=("x",), extra={"layer": 1, "screens": 4, "scrollFraction": 0.08})
    print("  wide midfar")
    k["bg_midfar"] = WIDE.midfar_wide()
    S(k["bg_midfar"], f"{BG}/garden_bg2_midfar.png",
      "Midfar: chain of floating islands; ring gate, temple cluster and tall spire each once (4 screens wide)", z,
      tile=("x",), extra={"layer": 2, "screens": 4, "scrollFraction": 0.2})
    print("  wide mid")
    k["bg_mid"] = WIDE.mid_wide(k)
    S(k["bg_mid"], f"{BG}/garden_bg3_mid.png",
      "Mid: nearer cliffs, arcades, trees, waterfalls, breaks of open sky (4 screens wide)", z, tile=("x",),
      extra={"layer": 3, "screens": 4, "scrollFraction": 0.4})
    global _KIT
    _KIT = k


_KIT = None


# ================================================================== previews

def preview():
    from pa_common import PH, PW, bloom, hero, particles, save_preview
    k = _KIT
    lay = Layer(PW, PH)
    gl = Layer(PW, PH)

    def put(key, x, y, s=1.0, mode="over", amount=1.0, anchor="topleft", hz=0.0, flip=False, dark=0.0):
        src = k[key] if isinstance(key, str) else key
        rgb, a = E.scaled(src, s) if s != 1 else src.straight()
        if flip:
            rgb, a = rgb[:, ::-1], a[:, ::-1]
        if hz:
            rgb = lerp(rgb, SKY["haze"], hz)
        if dark:
            rgb = rgb * (1 - dark)
        h, w = a.shape
        if anchor == "bottom":
            x, y = x - w / 2, y - h
        elif anchor == "top":
            x = x - w / 2
        if mode == "add":
            gl.blit(rgb * a[..., None] * amount, a * amount, x, y, premult=True, mode="add")
        else:
            lay.blit(rgb, a * amount, x, y)
        return w, h

    cache = {}

    def backdrop(key, offset, dy=0):
        if key not in cache:
            cache[key] = E.scaled(k[key], 0.625)
        rgb, a = cache[key]
        if rgb.shape[1] < PW:   # sky: stretched to the view
            rgb = np.tile(rgb, (1, 2, 1))
            a = np.tile(a, (1, 2))
        rgb = np.roll(rgb, -int(offset), 1)[:, :PW]
        a = np.roll(a, -int(offset), 1)[:, :PW]
        lay.blit(rgb, a, 0, dy)

    def strip(key, x0, x1, y_top):
        rgb, a = k[key].straight()
        w = a.shape[1]
        xx = x0
        while xx < x1:
            cw = int(min(w, x1 - xx))
            lay.blit(rgb[:, :cw], a[:, :cw], xx, y_top)
            xx += cw

    def fill(key, x0, x1, y0, y1, off=(0, 0)):
        rgb, a = k[key].straight()
        Y, X = np.mgrid[0:PH, 0:PW]
        m = ((X >= x0) & (X < x1) & (Y >= y0) & (Y < y1)).astype(F32)
        lay.blit(rgb[(Y + off[1]) % rgb.shape[0], (X + off[0]) % rgb.shape[1]], m, 0, 0)

    # camera at world x = 2900 px (play layer); backdrops scroll at their fractions
    camx = 2900
    backdrop("bg_sky", 0)
    backdrop("bg_far", camx * 0.08 * 0.625 * 4)
    backdrop("bg_midfar", camx * 0.2 * 0.625 * 4)
    put("golden_tree", 640, 268, 0.16, anchor="bottom", hz=0.3)
    put("golden_tree_glow", 640, 268, 0.16, mode="add", anchor="bottom", amount=0.18)
    backdrop("bg_mid", camx * 0.4 * 0.625 * 4)
    put("cloud_sea_0", 640, PH + 90, 0.625, anchor="bottom")
    # ---- arcade bridge on the right
    walk = 400
    arch = k["arch_span"]
    arch_top = walk - 16
    spring = arch_top + arch.h
    cols = [800, 1184]
    for cx in cols:
        y = spring + 64
        while y < PH:
            put("column_shaft", cx, y, anchor="top")
            y += 256
        put("column_capital", cx, spring, anchor="top")
    for cx in cols:
        put("arch_span", cx - 51, arch_top)
    put("wisteria_drape", 992, arch_top + 64, anchor="top")
    put("ivy_drape_a", 900, arch_top + 64, anchor="top")
    put("lantern", 1060, spring - 141 - 4, anchor="top")
    put("lantern_glow", 1060, spring - 141 - 4, mode="add", anchor="top", amount=0.9)
    put("ivy_drape_c", 1184 + 70, arch_top + 64, anchor="top")
    # ---- terrace: marble ashlar wall, coping, hedge, blossom tree, spout + waterfall
    tw = 600
    fill("wall_block", 0, tw, walk + 30, PH, (40, 0))
    put("ivy_curtain", 420, walk + 44, 0.8, anchor="top")
    put("blossom_tree", 300, walk + 4, 0.85, anchor="bottom")
    put("hedge_flowers", 160, walk + 6, anchor="bottom")
    put("hedge_flowers", 480, walk + 6, 0.8, anchor="bottom")
    strip("wall_cap", 0, tw, walk - 34)
    sm = k["waterfall_spout_meta"]
    spx, spy = tw - 4, walk + 120
    put("waterfall_spout", spx, spy - 32)
    lipx, lipy = spx + sm["lipPoint"][0], spy - 32 + sm["lipPoint"][1]
    y = lipy
    while y < PH:
        put("waterfall_tex", lipx + 8, y, 0.22, anchor="top", amount=0.95)
        y += int(512 * 0.22)
    put("mist_soft", lipx + 8, PH - 120, 1.0, anchor="top")
    put("lily_cluster_a", 520, walk + 3, anchor="bottom")
    put("fern", 90, walk + 4, anchor="bottom")
    put("lily_cluster_b", 900, walk + 3, anchor="bottom")
    put("flower_bush", 1080, walk + 6, 0.85, anchor="bottom")
    hero(lay, 470, walk + 2)
    # ---- loggia framing the left edge (foreground)
    put("loggia_frame", -40, PH + 30, anchor="bottom", dark=0.06)
    put("cloud_sea_2", 640, PH + 200, 0.625, anchor="bottom")
    particles(gl, "Common/Particles/pollen.png", [(700, 250), (300, 300), (1000, 200)], 16, 120, (0.5, 1.0),
              hexc("fff2c8"), 0.5, 31)
    bloom(lay, gl.rgb, 0.6)
    out = save_preview(lay, "garden_preview.png")
    wide_preview()
    return out


def wide_preview(scale=0.25):
    """The 4-screen-wide layers stacked as composited, plus each one alone."""
    from pa_common import save_preview
    k = _KIT
    Wd = int(8192 * scale)
    Hd = int(1152 * scale)
    sky_rgb, sky_a = E.scaled(k["bg_sky"], scale * 4 / 4)
    sky_rgb = np.tile(sky_rgb, (1, 4, 1))[:, :Wd]
    rows = []
    comp = Layer(Wd, Hd)
    comp.blit(sky_rgb, np.ones(sky_rgb.shape[:2], F32), 0, 0)
    layers = ["bg_far", "bg_midfar", "bg_mid"]
    for key in layers:
        rgb, a = E.scaled(k[key], scale)
        comp.blit(rgb, a, 0, 0)
    for key, yb in (("cloud_sea_0", Hd + 30), ("cloud_sea_2", Hd + 60)):
        rgb, a = E.scaled(k[key], scale)
        comp.blit(rgb, a, 0, yb - a.shape[0])
    rows.append(comp)
    for key in layers + ["cloud_sea_0", "cloud_sea_2"]:
        L = Layer(Wd, Hd)
        L.blit(np.ones((Hd, Wd, 3), F32) * hexc("7fa8d8"), np.ones((Hd, Wd), F32), 0, 0)
        rgb, a = E.scaled(k[key], scale)
        L.blit(rgb, a, 0, 0)
        rows.append(L)
    out = Layer(Wd, Hd * len(rows))
    for i, r in enumerate(rows):
        rgb, a = r.straight()
        out.blit(rgb, a, 0, i * Hd)
    save_preview(out, "garden_wide_preview.png")
