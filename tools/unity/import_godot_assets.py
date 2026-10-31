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
import uuid

try:
    from PIL import Image
except ImportError:  # pragma: no cover
    Image = None

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, "Assets")
NAMESPACE = uuid.UUID("5a8f1c1e-2b3d-4c5e-9f70-0de5ce000001")

# Pixels per unit: one tile is one unit. Level pixel art (sprites, UI) is 16 a
# tile; painted art (tiles, props, region sets) is drawn at 64 a tile.
PPU_LEVEL = 16
PPU_PAINTED = 64

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


def guid_for(rel_path: str) -> str:
    return uuid.uuid5(NAMESPACE, rel_path.replace(os.sep, "/")).hex


def file_id_for(name: str) -> int:
    """A stable positive int64 for a sprite's internal id."""
    return int(uuid.uuid5(NAMESPACE, "sprite:" + name).int % (2**62)) + 1


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


# --- Meta files ---------------------------------------------------------------

META_HEAD = "fileFormatVersion: 2\nguid: %s\n"
META_TAIL = "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def folder_meta(guid: str) -> str:
    return META_HEAD % guid + "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n" + META_TAIL


def default_meta(guid: str) -> str:
    return META_HEAD % guid + "DefaultImporter:\n  externalObjects: {}\n" + META_TAIL


def script_meta(guid: str) -> str:
    return (
        META_HEAD % guid
        + "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
        + "  executionOrder: 0\n  icon: {instanceID: 0}\n"
        + META_TAIL
    )


def text_meta(guid: str) -> str:
    return META_HEAD % guid + "TextScriptImporter:\n  externalObjects: {}\n" + META_TAIL


def native_meta(guid: str, main_object: int = 11400000) -> str:
    return META_HEAD % guid + f"NativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: {main_object}\n" + META_TAIL


def prefab_meta(guid: str) -> str:
    return META_HEAD % guid + "PrefabImporter:\n  externalObjects: {}\n" + META_TAIL


def shader_meta(guid: str) -> str:
    return (
        META_HEAD % guid
        + "ShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n"
        + "  preprocessorOverride: 0\n"
        + META_TAIL
    )


def audio_meta(guid: str, loop_stream: bool) -> str:
    return (
        META_HEAD % guid
        + "AudioImporter:\n  externalObjects: {}\n  serializedVersion: 7\n"
        + "  defaultSettings:\n    serializedVersion: 2\n    loadType: %d\n    sampleRateSetting: 0\n"
        % (1 if loop_stream else 0)
        + "    sampleRateOverride: 44100\n    compressionFormat: 1\n    quality: 1\n    conversionMode: 0\n    preloadAudioData: 0\n"
        + "  platformSettingOverrides: {}\n  forceToMono: 0\n  normalize: 1\n  preloadAudioData: 0\n"
        + "  loadInBackground: 0\n  ambisonic: 0\n  3D: 0\n"
        + META_TAIL
    )


def font_meta(guid: str) -> str:
    return (
        META_HEAD % guid
        + "TrueTypeFontImporter:\n  externalObjects: {}\n  serializedVersion: 4\n  fontSize: 16\n"
        + "  forceTextureCase: -2\n  characterSpacing: 0\n  characterPadding: 1\n  includeFontData: 1\n"
        + "  fontNames:\n  - Pirata One\n  fallbackFontReferences: []\n  customCharacters: \n"
        + "  fontRenderingMode: 0\n  ascentCalculationMode: 1\n  useLegacyBoundsCalculation: 0\n"
        + "  shouldRoundAdvanceValue: 1\n"
        + META_TAIL
    )


def sprite_entry(name: str, x: int, y: int, w: int, h: int, pivot: tuple[float, float]) -> str:
    sprite_id = uuid.uuid5(NAMESPACE, "spriteid:" + name).hex
    return (
        "    - serializedVersion: 2\n"
        f"      name: {name}\n"
        "      originalName: \n"
        "      rect:\n        serializedVersion: 2\n"
        f"        x: {x}\n        y: {y}\n        width: {w}\n        height: {h}\n"
        "      alignment: 9\n"
        f"      pivot: {{x: {pivot[0]:.6g}, y: {pivot[1]:.6g}}}\n"
        "      border: {x: 0, y: 0, z: 0, w: 0}\n"
        "      customData: \n      outline: []\n      physicsShape: []\n      tessellationDetail: 0\n"
        "      bones: []\n"
        f"      spriteID: {sprite_id}\n"
        f"      internalID: {file_id_for(name)}\n"
        "      vertices: []\n      indices: \n      edges: []\n      weights: []\n"
    )


def texture_meta(
    guid: str,
    *,
    ppu: int,
    point_filter: bool,
    normal_map: bool = False,
    mipmaps: bool = False,
    pivot: tuple[float, float] = (0.5, 0.5),
    sprites: list[tuple[str, int, int, int, int, tuple[float, float]]] | None = None,
    secondary: list[tuple[str, str]] | None = None,
) -> str:
    """A TextureImporter .meta: a Sprite (2D and UI), single or sliced."""
    multiple = sprites is not None
    lines = [META_HEAD % guid, "TextureImporter:\n"]
    if multiple:
        lines.append("  internalIDToNameTable:\n")
        for name, *_ in sprites:
            lines.append(f"  - first:\n      213: {file_id_for(name)}\n    second: {name}\n")
    else:
        lines.append("  internalIDToNameTable: []\n")
    lines.append(
        "  externalObjects: {}\n  serializedVersion: 13\n"
        "  mipmaps:\n    mipMapMode: 0\n"
        f"    enableMipMap: {1 if mipmaps else 0}\n"
        f"    sRGBTexture: {0 if normal_map else 1}\n"
        f"    linearTexture: {1 if normal_map else 0}\n"
        "    fadeOut: 0\n    borderMipMap: 0\n    mipMapsPreserveCoverage: 0\n"
        "    alphaTestReferenceValue: 0.5\n    mipMapFadeDistanceStart: 1\n    mipMapFadeDistanceEnd: 3\n"
        "  bumpmap:\n    convertToNormalMap: 0\n    externalNormalMap: 0\n    heightScale: 0.25\n"
        "    normalMapFilter: 0\n    flipGreenChannel: 0\n"
        "  isReadable: 0\n  streamingMipmaps: 0\n  streamingMipmapsPriority: 0\n  vTOnly: 0\n"
        "  ignoreMipmapLimit: 0\n  grayScaleToAlpha: 0\n  generateCubemap: 6\n  cubemapConvolution: 0\n"
        "  seamlessCubemap: 0\n  textureFormat: 1\n  maxTextureSize: 4096\n"
        "  textureSettings:\n    serializedVersion: 2\n"
        f"    filterMode: {0 if point_filter else 1}\n"
        "    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n"
        "  nPOTScale: 0\n  lightmap: 0\n  compressionQuality: 50\n"
        f"  spriteMode: {2 if multiple else 1}\n"
        "  spriteExtrude: 1\n  spriteMeshType: 1\n  alignment: 9\n"
        f"  spritePivot: {{x: {pivot[0]:.6g}, y: {pivot[1]:.6g}}}\n"
        f"  spritePixelsToUnits: {ppu}\n"
        "  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n  spriteGenerateFallbackPhysicsShape: 1\n"
        "  alphaUsage: 1\n  alphaIsTransparency: 1\n  spriteTessellationDetail: -1\n"
        f"  textureType: {1 if normal_map else 8}\n"
        "  textureShape: 1\n  singleChannelComponent: 0\n  flipbookRows: 1\n  flipbookColumns: 1\n"
        "  maxTextureSizeSet: 0\n  compressionQualitySet: 0\n  textureFormatSet: 0\n"
        "  ignorePngGamma: 0\n  applyGammaDecoding: 0\n  swizzle: 50462976\n  cookieLightType: 0\n"
        "  platformSettings:\n  - serializedVersion: 4\n    buildTarget: DefaultTexturePlatform\n"
        "    maxTextureSize: 4096\n    resizeAlgorithm: 0\n    textureFormat: -1\n    textureCompression: 0\n"
        "    compressionQuality: 50\n    crunchedCompression: 0\n    allowsAlphaSplitting: 0\n    overridden: 0\n"
        "    ignorePlatformSupport: 0\n    androidETC2FallbackOverride: 0\n    forceMaximumCompressionQuality_BC6H_BC7: 0\n"
        "  spriteSheet:\n    serializedVersion: 2\n"
    )
    if multiple:
        lines.append("    sprites:\n")
        for name, x, y, w, h, piv in sprites:
            lines.append(sprite_entry(name, x, y, w, h, piv))
    else:
        lines.append("    sprites: []\n")
    lines.append(
        "    outline: []\n    customData: \n    physicsShape: []\n    bones: []\n    spriteID: \n"
        "    internalID: 0\n    vertices: []\n    indices: \n    edges: []\n    weights: []\n"
    )
    if secondary:
        lines.append("    secondaryTextures:\n")
        for name, other_guid in secondary:
            lines.append(f"    - name: {name}\n      texture: {{fileID: 2800000, guid: {other_guid}, type: 3}}\n")
    else:
        lines.append("    secondaryTextures: []\n")
    if multiple:
        lines.append("    nameFileIdTable:\n")
        for name, *_ in sprites:
            lines.append(f"      {name}: {file_id_for(name)}\n")
    else:
        lines.append("    nameFileIdTable: {}\n")
    lines.append("    spriteCustomMetadata:\n      entries: []\n  mipmapLimitGroupName: \n  pSDRemoveMatte: 0\n")
    lines.append(META_TAIL)
    return "".join(lines)


def image_size(path: str) -> tuple[int, int]:
    if Image is None:
        raise SystemExit("Pillow is needed to read image sizes: pip install Pillow")
    with Image.open(path) as image:
        return image.size


def rel(path: str) -> str:
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def write_if_missing(meta_path: str, text: str) -> bool:
    if os.path.exists(meta_path):
        return False
    with open(meta_path, "w", newline="\n") as handle:
        handle.write(text)
    return True


def sprite_spec() -> dict:
    with open(os.path.join(ROOT, "godot_assets", "sprites", "sprites.json")) as handle:
        return json.load(handle)


def strip_sprites(character: str, anim_name: str, anim: dict, info: dict, size: tuple[int, int]):
    frame_w, frame_h = (int(v) for v in info["frame_size"])
    origin_x, origin_y = (int(v) for v in info["origin"])
    pivot = (origin_x / frame_w, 1.0 - origin_y / frame_h)
    frames = int(anim["frames"])
    y = size[1] - frame_h
    return [
        (f"{character}_{anim_name}_{i}", i * frame_w, y, frame_w, frame_h, pivot)
        for i in range(frames)
    ]


def grid_sprites(stem: str, size: tuple[int, int], cell: int):
    columns, rows = size[0] // cell, size[1] // cell
    result = []
    for row in range(rows):
        for column in range(columns):
            # Unity's rect y runs from the bottom; the editor's tile ids run from the top.
            result.append((f"{stem}_{row * columns + column}", column * cell, size[1] - (row + 1) * cell, cell, cell, (0.5, 0.5)))
    return result


# Standalone sprites under Assets/Art/Sprites that are horizontal frame strips (Godot's hframes).
STANDALONE_FRAMES = {"checkpoint.png": 2}


def texture_meta_for(path: str) -> str:
    """The meta for a PNG under Assets/, by where it lives."""
    relative = rel(path)
    guid = guid_for(relative)
    name = os.path.basename(path)
    stem = name[:-4]
    folder = os.path.dirname(relative)
    size = image_size(path)
    if relative.startswith("Assets/Art/Sprites/"):
        character = os.path.basename(folder)
        spec = sprite_spec().get(character, {})
        for anim_name, anim in spec.get("animations", {}).items():
            if anim["file"] == name:
                return texture_meta(guid, ppu=PPU_LEVEL, point_filter=True, sprites=strip_sprites(character, anim_name, anim, spec, size))
        if name in STANDALONE_FRAMES:
            frames = STANDALONE_FRAMES[name]
            frame_w = size[0] // frames
            sprites = [(f"{stem}_{i}", i * frame_w, 0, frame_w, size[1], (0.5, 0.0)) for i in range(frames)]
            return texture_meta(guid, ppu=PPU_LEVEL, point_filter=True, sprites=sprites)
        return texture_meta(guid, ppu=PPU_LEVEL, point_filter=True, pivot=(0.5, 0.0))
    if relative.startswith("Assets/Art/Tiles/Sheets/"):
        return texture_meta(guid, ppu=PPU_PAINTED, point_filter=False, normal_map=stem.endswith("_n"))
    if relative.startswith("Assets/Art/Tiles/"):
        if stem.endswith("_n"):
            return texture_meta(guid, ppu=PPU_PAINTED, point_filter=False, normal_map=True)
        normal = os.path.join(os.path.dirname(path), stem + "_n.png")
        secondary = [("_NormalMap", guid_for(rel(normal)))] if os.path.exists(normal) else None
        return texture_meta(guid, ppu=PPU_PAINTED, point_filter=False, sprites=grid_sprites(stem, size, PPU_PAINTED), secondary=secondary)
    if relative.startswith("Assets/Art/Props/"):
        if stem.endswith("_n"):
            return texture_meta(guid, ppu=PPU_PAINTED, point_filter=False, normal_map=True)
        secondary = None
        if not stem.endswith("_glow"):
            normal = os.path.join(os.path.dirname(path), stem + "_n.png")
            if os.path.exists(normal):
                secondary = [("_NormalMap", guid_for(rel(normal)))]
        # A prop is placed by its top-left corner, as the Godot prop scenes were.
        return texture_meta(guid, ppu=PPU_PAINTED, point_filter=False, pivot=(0.0, 1.0), secondary=secondary)
    if relative.startswith("Assets/Art/Regions/"):
        return texture_meta(guid, ppu=PPU_PAINTED, point_filter=False, mipmaps=True)
    return texture_meta(guid, ppu=PPU_LEVEL, point_filter=True)


def write_metas() -> int:
    written = 0
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        dirnames.sort()
        for dirname in dirnames:
            folder = os.path.join(dirpath, dirname)
            written += write_if_missing(folder + ".meta", folder_meta(guid_for(rel(folder))))
        for filename in sorted(filenames):
            if filename.endswith(".meta") or filename == ".gdignore":
                continue
            path = os.path.join(dirpath, filename)
            relative = rel(path)
            guid = guid_for(relative)
            lower = filename.lower()
            if lower.endswith(".cs"):
                text = script_meta(guid)
            elif lower.endswith((".json", ".txt", ".md", ".gdshader")):
                text = text_meta(guid) if not lower.endswith(".gdshader") else default_meta(guid)
            elif lower.endswith(".png"):
                text = texture_meta_for(path)
            elif lower.endswith((".wav", ".ogg")):
                text = audio_meta(guid, loop_stream="/Audio/music/" in relative or "ambient" in lower)
            elif lower.endswith(".ttf"):
                text = font_meta(guid)
            elif lower.endswith(".asset"):
                text = native_meta(guid)
            elif lower.endswith(".mat"):
                text = native_meta(guid, 2100000)
            elif lower.endswith(".prefab"):
                text = prefab_meta(guid)
            elif lower.endswith(".shader"):
                text = shader_meta(guid)
            else:
                text = default_meta(guid)
            written += write_if_missing(path + ".meta", text)
    return written


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


def yaml_number(value) -> str:
    if isinstance(value, bool):
        return "1" if value else "0"
    if isinstance(value, float) and value.is_integer():
        return str(int(value))
    return repr(float(value))


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
