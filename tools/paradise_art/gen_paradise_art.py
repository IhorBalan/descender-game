#!/usr/bin/env python3
"""Generate the Paradise region's art (zones 1 and 2) from code.

    python tools/paradise_art/gen_paradise_art.py [z1 z2 garden common] [--no-preview]

Writes PNGs (RGBA, straight alpha) under Assets/Art/Paradise/, a
manifest.json describing every file, and mock screenshots under
tools/paradise_art/previews/. Deterministic: fixed seeds throughout.
With no zone arguments everything is generated.
"""
from __future__ import annotations

import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import pa_common  # noqa: E402
import pa_z1  # noqa: E402
from pa_core import Registry  # noqa: E402

ZONES = {"z1": pa_z1}
import pa_z2  # noqa: E402
import pa_garden  # noqa: E402

ZONES["z2"] = pa_z2
ZONES["garden"] = pa_garden
import pa_hero  # noqa: E402
ZONES["hero"] = pa_hero  # Ansel cutout rig parts -> Assets/Art/Paradise/Hero/


def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    want = args or ["common", *ZONES]  # "garden" = the Celestial Garden kit
    reg = Registry()
    t0 = time.time()
    if "common" in want or not args:
        print("common particles")
        pa_common.build(reg)
    for z in want:
        if z in ZONES:
            print(f"zone {z}")
            ZONES[z].build(reg)
    reg.write_manifest()
    if "--no-preview" not in argv:
        for z in want:
            if z in ZONES:
                ZONES[z].preview()
    print(f"done in {time.time() - t0:.1f}s")


if __name__ == "__main__":
    main(sys.argv[1:])
