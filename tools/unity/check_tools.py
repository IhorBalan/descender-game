#!/usr/bin/env python3
"""Checks that the Python tools stand on the Unity project alone.

    python3 tools/unity/check_tools.py      # Python 3 with Pillow and numpy

Imports every module under tools/ (so a broken import, a missing helper or a
stray side effect at import time fails here, not on the next art run) and
then fails on any trace of the old Godot tree: a module-level constant whose
value holds one of the forbidden fragments, or the fragment anywhere in the
module's source text (a path built inside a function would slip past the
constant scan). Needs no Unity; GitHub Actions runs it on every pull request
(`Tests / Python tools`).

Each folder's modules are imported with that folder on sys.path, the way the
scripts run themselves, and unloaded afterwards so same-named helpers in two
folders (pix.py, export.py) never shadow each other. Nothing is written: the
modules are only imported, never run.
"""
from __future__ import annotations

import importlib.util
import os
import sys
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)

# The two one-way importers read the Godot tree by design and go with it.
EXCLUDED = {
    os.path.join(HERE, "import_godot_assets.py"),
    os.path.join(HERE, "import_godot_regions.py"),
    os.path.abspath(__file__),  # this list itself
}

# Fragments of a path into the Godot project. Spelled in pieces so the
# source scan does not trip over its own list.
FORBIDDEN = [
    "godot" + "_assets",
    "levels" + "/",
    "scenes" + "/",
    "resources" + "/",
    "scripts" + "/",
    "res" + "://",
    "." + "tres",
    "import_godot" + "_assets",
    "sprite" + "_frames",
]


def modules() -> list[str]:
    found = []
    for dirpath, dirnames, filenames in os.walk(TOOLS):
        dirnames[:] = sorted(d for d in dirnames if not d.startswith((".", "__")) and d not in ("out", "backup"))
        for name in sorted(filenames):
            if name.endswith(".py"):
                path = os.path.join(dirpath, name)
                if path not in EXCLUDED:
                    found.append(path)
    return found


def strings_in(value) -> list[str]:
    """Every string reachable through a module-level value, lists and dicts included."""
    if isinstance(value, str):
        return [value]
    if isinstance(value, (list, tuple, set, frozenset)):
        return [s for item in value for s in strings_in(item)]
    if isinstance(value, dict):
        return [s for pair in value.items() for item in pair for s in strings_in(item)]
    return []


def forbidden_in(text: str) -> list[str]:
    return [fragment for fragment in FORBIDDEN if fragment in text]


def check_folder(folder: str, paths: list[str]) -> list[str]:
    problems = []
    before_modules = set(sys.modules)
    sys.path.insert(0, folder)
    try:
        for path in paths:
            relative = os.path.relpath(path, TOOLS).replace(os.sep, "/")
            with open(path, encoding="utf-8") as handle:
                source = handle.read()
            for fragment in forbidden_in(source):
                problems.append(f"{relative}: source mentions {fragment!r}")
            name = "tools_check_" + relative[:-3].replace("/", "_")
            spec = importlib.util.spec_from_file_location(name, path)
            module = importlib.util.module_from_spec(spec)
            try:
                sys.modules[name] = module
                spec.loader.exec_module(module)
            except BaseException as error:  # noqa: BLE001 - a SystemExit at import is a failure too
                problems.append(f"{relative}: import failed: {type(error).__name__}: {error}")
                traceback.print_exc(limit=1, file=sys.stderr)
                continue
            for attr, value in sorted(vars(module).items()):
                if attr.startswith("__"):
                    continue
                for text in strings_in(value):
                    for fragment in forbidden_in(text):
                        problems.append(f"{relative}: {attr} holds {fragment!r}: {text!r}")
    finally:
        sys.path.remove(folder)
        # Unload what came from tools/ so the next folder's pix.py or export.py
        # is its own; third-party modules stay (numpy cannot be loaded twice).
        for name in set(sys.modules) - before_modules:
            if is_under_tools(getattr(sys.modules[name], "__file__", None)):
                del sys.modules[name]
    return problems


def is_under_tools(path: str | None) -> bool:
    return bool(path) and os.path.abspath(path).startswith(TOOLS + os.sep)


def main() -> int:
    paths = modules()
    by_folder: dict[str, list[str]] = {}
    for path in paths:
        by_folder.setdefault(os.path.dirname(path), []).append(path)
    problems = []
    for folder in sorted(by_folder):
        problems += check_folder(folder, by_folder[folder])
    print(f"checked {len(paths)} modules in {len(by_folder)} folders")
    for problem in problems:
        print("FAIL", problem)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
