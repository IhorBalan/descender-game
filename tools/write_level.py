#!/usr/bin/env python3
"""One-off helper that wrote the first draft of levels/level_01.txt.

The text file is the source of truth and is meant to be edited by hand;
this script only exists so the first layout had exact column counts.
Run it to print the level with a column ruler:

    python3 tools/write_level.py            # print levels/level_01.txt with a ruler
    python3 tools/write_level.py --write    # regenerate the draft layout
"""
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEVEL = os.path.join(ROOT, "levels", "level_01.txt")


def row(*runs: tuple[str, int]) -> str:
    return "".join(ch * n for ch, n in runs)


def draft() -> list[str]:
    W = 94  # inner width; column 0 and 95 are walls
    wall = ("#", 1)
    rows = [
        row(("#", 96)),
        row(wall, (".", W), wall),
        row(wall, (".", 72), ("G", 1), (".", 21), wall),
        row(wall, (".", 70), ("#", 11), (".", 13), wall),
        row(wall, (".", 32), ("#", 5), (".", 24), ("f", 1), (".", 32), wall),
        row(wall, (".", 39), ("f", 1), (".", 54), wall),
        row(wall, (".", W), wall),
        row(wall, (".", 83), ("#", 5), (".", 6), wall),
        row(wall, (".", 14), ("#", 4), (".", 9), ("#", 4), (".", 63), wall),
        row(wall, (".", W), wall),
        row(wall, (".", 56), ("C", 1), (".", 25), ("w", 1), (".", 11), wall),
        row(wall, (".", 5), ("P", 1), (".", 19), ("w", 1), (".", 23), ("w", 1),
            (".", 3), ("#", 5), (".", 14), ("g", 1), (".", 3), ("w", 1), (".", 2), ("#", 16)),
        row(("#", 19), (".", 3), ("#", 17), (".", 7), ("#", 17), (".", 7), ("#", 26)),
        row(("#", 19), ("^", 3), ("#", 17), ("^", 7), ("#", 17), ("^", 7), ("#", 26)),
        row(("#", 96)),
    ]
    for i, r in enumerate(rows):
        assert len(r) == 96, (i, len(r))
    return rows


def show(rows: list[str]) -> None:
    width = max(len(r) for r in rows)
    print("    " + "".join(str((c // 10) % 10) if c % 10 == 0 else " " for c in range(width)))
    print("    " + "".join(str(c % 10) for c in range(width)))
    for y, r in enumerate(rows):
        print(f"{y:>3} {r}")


if __name__ == "__main__":
    if "--write" in sys.argv:
        with open(LEVEL, "w") as f:
            f.write("; Descender - level 01 (Heaven, outskirts). See scripts/level_builder.gd for the legend.\n")
            f.write("\n".join(draft()) + "\n")
        print("wrote", LEVEL)
    with open(LEVEL) as f:
        show([line.rstrip("\n") for line in f if not line.startswith(";")])
