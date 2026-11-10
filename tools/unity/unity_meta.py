#!/usr/bin/env python3
"""The Unity-side helpers the Python tools share: where the project is, the
path-derived GUIDs, the `.meta` writers and the sprite-sheet slicing.

Every `.meta` this module writes gets a GUID derived from the asset's path
(uuid5 under one namespace), so every clone and every run agrees on the ids.
A `.meta` that already exists is left alone: Unity owns it from then on.
`write_metas()` walks `Assets/` and writes the missing ones; `ensure_meta()`
does the same for one file a generator has just written, with the folders
above it. Sprite strips are sliced from `Assets/Art/Sprites/sprites.json`
(one sprite a frame, pivot at the strip's origin) and tile atlases into their
64 px grid; a prop's normal map is attached to its art as the `_NormalMap`
secondary texture.

Needs only the standard library (and Pillow, for image sizes). Not a script:
`build_prefabs.py` and the art and sound generators import it.
"""
from __future__ import annotations

import json
import os
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


def guid_for(rel_path: str) -> str:
    return uuid.uuid5(NAMESPACE, rel_path.replace(os.sep, "/")).hex


def file_id_for(name: str) -> int:
    """A stable positive int64 for a sprite's internal id."""
    return int(uuid.uuid5(NAMESPACE, "sprite:" + name).int % (2**62)) + 1


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
    with open(os.path.join(ASSETS, "Art", "Sprites", "sprites.json")) as handle:
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
    if relative.startswith("Assets/Art/UI/Title/"):
        # The title screen's art is drawn smooth at twice its 1920x1080 size (tools/title_textures.py);
        # mipmaps keep it clean where the screen shows it smaller.
        return texture_meta(guid, ppu=100, point_filter=False, mipmaps=True)
    if relative.startswith("Assets/Art/Regions/"):
        return texture_meta(guid, ppu=PPU_PAINTED, point_filter=False, mipmaps=True)
    return texture_meta(guid, ppu=PPU_LEVEL, point_filter=True)


def meta_text_for(path: str) -> str:
    """The .meta text for a file under Assets/, by its extension and location."""
    relative = rel(path)
    guid = guid_for(relative)
    lower = os.path.basename(path).lower()
    if lower.endswith(".cs"):
        return script_meta(guid)
    if lower.endswith((".json", ".txt", ".md", ".gdshader")):
        return text_meta(guid) if not lower.endswith(".gdshader") else default_meta(guid)
    if lower.endswith(".png"):
        return texture_meta_for(path)
    if lower.endswith((".wav", ".ogg")):
        return audio_meta(guid, loop_stream="/Audio/music/" in relative or "ambient" in lower)
    if lower.endswith(".ttf"):
        return font_meta(guid)
    if lower.endswith(".asset"):
        return native_meta(guid)
    if lower.endswith(".mat"):
        return native_meta(guid, 2100000)
    if lower.endswith(".prefab"):
        return prefab_meta(guid)
    if lower.endswith(".shader"):
        return shader_meta(guid)
    return default_meta(guid)


def write_metas() -> int:
    written = 0
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        # Paradise is authored in Unity (docs/design/paradise.md): Unity writes its
        # .meta files with the import settings it needs, never this script.
        dirnames[:] = sorted(d for d in dirnames if d != "Paradise")
        for dirname in dirnames:
            folder = os.path.join(dirpath, dirname)
            written += write_if_missing(folder + ".meta", folder_meta(guid_for(rel(folder))))
        for filename in sorted(filenames):
            if filename.endswith(".meta") or filename == ".gdignore":
                continue
            path = os.path.join(dirpath, filename)
            written += write_if_missing(path + ".meta", meta_text_for(path))
    return written


def inside_assets(path: str) -> bool:
    """Whether a path lies under Assets/, where a .meta means something."""
    path = os.path.abspath(path)
    return path == ASSETS or path.startswith(ASSETS + os.sep)


def ensure_meta(path: str) -> int:
    """Writes the .meta of one file a generator has just written, and of every
    folder between it and Assets/, where they are missing. A file outside
    Assets/ (a scratch `--out`) gets none. Returns how many were written."""
    if not inside_assets(path):
        return 0
    written = 0
    folder = os.path.dirname(os.path.abspath(path))
    while folder != ASSETS:
        written += write_if_missing(folder + ".meta", folder_meta(guid_for(rel(folder))))
        folder = os.path.dirname(folder)
    return written + write_if_missing(os.path.abspath(path) + ".meta", meta_text_for(path))


def yaml_number(value) -> str:
    if isinstance(value, bool):
        return "1" if value else "0"
    if isinstance(value, float) and value.is_integer():
        return str(int(value))
    return repr(float(value))


