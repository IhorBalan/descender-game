#!/usr/bin/env python3
"""Carry the Godot project's art, audio and data into the Unity project.

    python3 tools/unity/import_godot_assets.py

Copies the PNGs, sounds and JSON under godot_assets/ and levels/ into Assets/Art,
Assets/Audio and Assets/Data, turns the enemy, boss and region .tres
resources into ScriptableObject .asset files, and writes a .meta beside
every file and folder under Assets/ that has none, with a GUID derived from
the path, so every clone and every run agrees on the ids. Sprite strips are
sliced in their .meta from godot_assets/sprites/sprites.json (one sprite a frame,
pivot at the strip's origin), tile atlases into their 64 px grid, and a
prop's normal map is attached to its art as the `_NormalMap` secondary
texture, so Unity needs no click to show them lit.

A .meta that already exists is left alone: Unity owns it from then on.
Needs only the standard library (and Pillow, for image sizes).
"""
from __future__ import annotations

import json
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from unity_meta import (  # noqa: E402,F401
    ASSETS,
    NAMESPACE,
    ROOT,
    guid_for,
    sprite_spec,
    write_metas,
    yaml_number,
)

COPIES = [
    ("godot_assets/sprites", "Assets/Art/Sprites", (".png", ".json")),
    ("godot_assets/props", "Assets/Art/Props", (".png", ".json")),
    ("godot_assets/regions", "Assets/Art/Regions", (".png", ".json")),
    ("levels/world/tiles", "Assets/Art/Tiles", (".png",)),
    ("levels/world", "Assets/Art/Tiles/Sheets", (".png",)),
    ("godot_assets/ui/theme", "Assets/Art/UI/Theme", (".png",)),
    ("godot_assets/ui/prompts", "Assets/Art/UI/Prompts", (".png",)),
    ("godot_assets/fonts", "Assets/Art/Fonts", (".ttf", ".txt")),
    ("godot_assets/audio", "Assets/Audio", (".wav", ".ogg")),
    ("godot_assets/shaders", "Assets/Shaders/Godot", (".gdshader",)),
    ("dialogue", "Assets/Data/dialogue", (".json",)),
]



def copy_tree(src_rel: str, dst_rel: str, exts: tuple[str, ...]) -> int:
    src = os.path.join(ROOT, src_rel)
    dst = os.path.join(ROOT, dst_rel)
    count = 0
    if not os.path.isdir(src):
        return 0
    for entry in sorted(os.listdir(src)):
        path = os.path.join(src, entry)
        if os.path.isdir(path):
            if src_rel == "levels/world" and entry == "tiles":
                continue
            count += copy_tree(os.path.join(src_rel, entry), os.path.join(dst_rel, entry), exts)
            continue
        if not entry.lower().endswith(exts):
            continue
        os.makedirs(dst, exist_ok=True)
        target = os.path.join(dst, entry)
        if not os.path.exists(target) or os.path.getsize(target) != os.path.getsize(path):
            shutil.copy2(path, target)
            count += 1
    return count



# --- Godot resources into ScriptableObjects ----------------------------------

SCRIPT_GUIDS = {
    "WalkerProfile": guid_for("Assets/Scripts/Runtime/Enemies/WalkerProfile.cs"),
    "FlyerProfile": guid_for("Assets/Scripts/Runtime/Enemies/FlyerProfile.cs"),
    "BossProfile": guid_for("Assets/Scripts/Runtime/Enemies/BossProfile.cs"),
    "RegionLook": guid_for("Assets/Scripts/Runtime/World/RegionLook.cs"),
}


def parse_tres(text: str) -> tuple[str, dict]:
    """The script_class and the [resource] values of a .tres, enough for ours."""
    script_class = ""
    if 'script_class="' in text:
        script_class = text.split('script_class="', 1)[1].split('"', 1)[0]
    body = text.split("[resource]", 1)[1] if "[resource]" in text else ""
    values: dict = {}
    key = None
    buffer = ""
    for line in body.splitlines():
        if key is None:
            if " = " not in line:
                continue
            key, buffer = line.split(" = ", 1)
            key = key.strip()
        else:
            buffer += "\n" + line
        if buffer.count("{") == buffer.count("}") and buffer.count("[") == buffer.count("]"):
            values[key] = parse_value(buffer.strip())
            key = None
            buffer = ""
    return script_class, values


def parse_value(token: str):
    if token in ("true", "false"):
        return token == "true"
    if token.startswith('"'):
        return token[1:-1]
    if token.startswith("{") or token.startswith("["):
        return json.loads(token)
    if token.startswith("Vector2("):
        return ("Vector2", [float(v) for v in token[8:-1].split(",")])
    if token.startswith("Color("):
        return ("Color", [float(v) for v in token[6:-1].split(",")])
    if token.startswith("ExtResource"):
        return None
    try:
        return float(token)
    except ValueError:
        return token


def yaml_vector(values, keys="xy") -> str:
    return "{" + ", ".join(f"{k}: {yaml_number(v)}" for k, v in zip(keys, values)) + "}"


def yaml_color(values) -> str:
    rgba = list(values) + [1.0] * (4 - len(values))
    return "{r: %s, g: %s, b: %s, a: %s}" % tuple(yaml_number(v) for v in rgba)


def loot_yaml(loot: dict | None) -> str:
    loot = loot or {}
    light = loot.get("light", [0, 0])
    return (
        "  loot:\n"
        f"    LightMin: {int(light[0])}\n    LightMax: {int(light[1])}\n"
        f"    BoneChance: {yaml_number(loot.get('bone_chance', 0.0))}\n"
        f"    Bones: {int(loot.get('bones', 0))}\n"
        f"    FlaskChance: {yaml_number(loot.get('flask_chance', 0.0))}\n"
        f"    Unique: {loot.get('unique', '')}\n"
        f"    Xp: {int(loot.get('xp', 0))}\n"
    )


def asset_head(script_class: str, name: str) -> str:
    return (
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUIDS[script_class]}, type: 3}}\n"
        f"  m_Name: {name}\n  m_EditorClassIdentifier: \n"
    )


POWER_KINDS = {"halo": 0, "blade": 1, "zeal": 2}


def walker_asset(name: str, v: dict) -> str:
    def num(key, default):
        return yaml_number(v.get(key, default))

    return (
        asset_head("WalkerProfile", name)
        + f"  body: {yaml_vector(v.get('body', ('Vector2', [18, 24]))[1])}\n"
        + f"  speed: {num('speed', 32)}\n  health: {int(v.get('health', 3))}\n"
        + f"  walkAnim: {v.get('walk_anim', 'run')}\n"
        + f"  facesLeft: {yaml_number(v.get('faces_left', False))}\n  armour: {yaml_number(v.get('armour', False))}\n"
        + f"  attackRange: {num('attack_range', 30)}\n  reach: {yaml_vector(v.get('reach', ('Vector2', [22, 20]))[1])}\n"
        + f"  windup: {num('windup', 0.35)}\n  strike: {num('strike', 0.15)}\n  recover: {num('recover', 0.45)}\n"
        + f"  cooldown: {num('cooldown', 0.8)}\n  attackDamage: {int(v.get('attack_damage', 1))}\n"
        + f"  dropChance: {num('drop_chance', 0.35)}\n  power: {int(v.get('power', 1))}\n"
        + f"  powerChance: {num('power_chance', 0.1)}\n"
        + loot_yaml(v.get("loot"))
        + f"  puff: {yaml_color(v.get('puff', ('Color', [0.55, 0.15, 0.15]))[1])}\n"
    )


def flyer_asset(name: str, v: dict) -> str:
    def num(key, default):
        return yaml_number(v.get(key, default))

    return (
        asset_head("FlyerProfile", name)
        + f"  health: {int(v.get('health', 2))}\n  hover: {num('hover', 25)}\n  chase: {num('chase', 55)}\n"
        + f"  aggro: {num('aggro', 110)}\n  dropChance: {num('drop_chance', 0.5)}\n"
        + loot_yaml(v.get("loot"))
        + f"  puff: {yaml_color(v.get('puff', ('Color', [1.0, 0.95, 0.8]))[1])}\n"
    )


def boss_asset(name: str, v: dict) -> str:
    def num(key, default):
        return yaml_number(v.get(key, default))

    return (
        asset_head("BossProfile", name)
        + f"  displayName: {v.get('display_name', '')}\n  character: {v.get('character', 'angel_guard')}\n"
        + f"  walkAnim: {v.get('walk_anim', 'walk')}\n  scale: {num('scale', 1.5)}\n"
        + f"  tint: {yaml_color(v.get('tint', ('Color', [1, 1, 1, 1]))[1])}\n"
        + f"  facesLeft: {yaml_number(v.get('faces_left', False))}\n"
        + f"  body: {yaml_vector(v.get('body', ('Vector2', [32, 80]))[1])}\n"
        + f"  health: {int(v.get('health', 24))}\n  speed: {num('speed', 34)}\n"
        + f"  contactDamage: {int(v.get('contact_damage', 1))}\n  third: {v.get('third', '')}\n"
        + f"  attackRange: {num('attack_range', 60)}\n  reach: {yaml_vector(v.get('reach', ('Vector2', [52, 60]))[1])}\n"
        + f"  windup: {num('windup', 0.55)}\n  strike: {num('strike', 0.2)}\n  recover: {num('recover', 0.6)}\n"
        + f"  cooldown: {num('cooldown', 1.0)}\n  attackDamage: {int(v.get('attack_damage', 2))}\n"
        + f"  chargeSpeed: {num('charge_speed', 260)}\n  chargeWindup: {num('charge_windup', 0.45)}\n"
        + f"  chargeCooldown: {num('charge_cooldown', 2.0)}\n  motes: {int(v.get('motes', 2))}\n"
        + loot_yaml(v.get("loot"))
    )


def region_asset(name: str, v: dict) -> str:
    return (
        asset_head("RegionLook", name)
        + f"  ambient: {yaml_color(v.get('ambient', ('Color', [0.76, 0.8, 0.9]))[1])}\n"
        + f"  ownSky: {yaml_number(v.get('own_sky', False))}\n"
        + f"  skyTop: {yaml_color(v.get('sky_top', ('Color', [0.4, 0.6, 0.9]))[1])}\n"
        + f"  skyBottom: {yaml_color(v.get('sky_bottom', ('Color', [0.8, 0.85, 0.95]))[1])}\n"
        + f"  cave: {yaml_number(v.get('cave', False))}\n"
        + f"  light: {yaml_color(v.get('light', ('Color', [0, 0, 0, 0]))[1])}\n"
        + f"  tint: {yaml_color(v.get('tint', ('Color', [1, 1, 1, 1]))[1])}\n"
        + f"  contrast: {yaml_number(v.get('contrast', 1.0))}\n  saturation: {yaml_number(v.get('saturation', 1.0))}\n"
        + f"  music: {v.get('music', '')}\n"
    )


CONVERTERS = {
    ("resources/enemies", "WalkerProfile"): ("Assets/Data/Enemies", walker_asset),
    ("resources/enemies", "FlyerProfile"): ("Assets/Data/Enemies", flyer_asset),
    ("resources/bosses", "BossProfile"): ("Assets/Data/Bosses", boss_asset),
    ("resources/regions", "RegionLook"): ("Assets/Data/Regions", region_asset),
}


def convert_resources() -> int:
    count = 0
    for folder in ("resources/enemies", "resources/bosses", "resources/regions"):
        source = os.path.join(ROOT, folder)
        if not os.path.isdir(source):
            continue
        for entry in sorted(os.listdir(source)):
            if not entry.endswith(".tres"):
                continue
            with open(os.path.join(source, entry)) as handle:
                script_class, values = parse_tres(handle.read())
            target = CONVERTERS.get((folder, script_class))
            if target is None:
                print(f"skip {folder}/{entry}: {script_class or 'no script class'}")
                continue
            out_dir, writer = target
            os.makedirs(os.path.join(ROOT, out_dir), exist_ok=True)
            name = entry[:-5]
            path = os.path.join(ROOT, out_dir, name + ".asset")
            text = writer(name, values)
            if not os.path.exists(path) or open(path).read() != text:
                with open(path, "w", newline="\n") as handle:
                    handle.write(text)
                count += 1
    return count


def main() -> int:
    copied = sum(copy_tree(*spec) for spec in COPIES)
    converted = convert_resources()
    metas = write_metas()
    print(f"copied {copied} files, converted {converted} resources, wrote {metas} .meta files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
