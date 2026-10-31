#!/usr/bin/env python3
"""Turn the Godot region scenes into Unity region scenes.

    python3 -I tools/unity/import_godot_regions.py

Reads scenes/world/world.tscn (the regions and their bands), each region's
scenes/world/<nn>_<key>.tscn and the TileSet levels/world/world_tiles.tres, and
writes, with deterministic GUIDs and file ids so every run agrees:

  Assets/Data/Regions/Tiles/<key>_cells.txt     the four layers' cells and the tile kinds (RegionTileData)
  Assets/Data/Regions/Tiles/<key>_tiles.asset   a RegionTileSet: the atlas's 256 sprites, the cells, the materials
  Assets/Scenes/Region_<key>.unity              RegionRoot at the band's corner; Tiles (RegionTiles, RegionFringes),
                                                Backdrop/* (BackdropLayer), Fx/* (RegionParticles), Lights/*
                                                (WorldLight), Rooms/* (WorldRoom), Doors/* (WorldDoor),
                                                Placements/* (WorldPlacement, LightBarrier), Props/* (prefabs)
  Assets/Prefabs/Props/<key>/<name>.prefab      a WorldProp with its lit Art sprite and, when it shines, its Glow
  Assets/Data/World.asset                       the WorldAtlas: bands, start, checkpoints in save order, abilities
  Assets/Data/Placements.asset                  an empty PlacementCatalog, filled in Unity by Descender > Setup

The tiles themselves are not painted into the scenes: RegionTiles builds the
Tilemaps from the cell data when a region loads, so 200k cells cost nothing in
the repo. Godot's level pixels become Unity units at 16 a unit, Y flipped; a
region's children stay in level coordinates from the region's own corner, the
RegionRoot carrying the band's offset. Run again after a Godot scene changes;
every output is rewritten (the scenes are generated, not hand-edited).
"""
from __future__ import annotations

import base64
import os
import re
import struct
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import import_godot_assets as assets  # noqa: E402

ROOT = assets.ROOT
NAMESPACE = assets.NAMESPACE
PPU = 16.0  # level pixels a unit
ART_TILE = 64  # pixels a tile the painted art is drawn at
ATLAS_COLUMNS = 16
SHEET_TILES = 256

LIT_MATERIAL = "{fileID: 2100000, guid: a97c105638bdf8b4a8650670310a4cd3, type: 2}"  # URP Sprite-Lit-Default
UNLIT_MATERIAL = "{fileID: 2100000, guid: 9dfc825aed78fcd4ba02077103263b40, type: 2}"  # URP Sprite-Unlit-Default

LAYER_NAMES = ["Background", "Decor", "Terrain", "Glow"]
ROOM_DEFAULT_SIZE = (480.0, 272.0)
LIGHT_TEXTURE_SIZE = 128

SCRIPTS = {
    name: assets.guid_for(f"Assets/Scripts/Runtime/World/{name}.cs")
    for name in [
        "RegionRoot",
        "RegionTiles",
        "RegionTileSet",
        "RegionFringes",
        "RegionParticles",
        "BackdropLayer",
        "WorldLight",
        "WorldRoom",
        "WorldDoor",
        "WorldPlacement",
        "LightBarrier",
        "WorldProp",
        "WorldAtlas",
        "PlacementCatalog",
    ]
}

PROP_ORDER = -50
BACKDROP_ORDER = -1000
FRONT_ORDER = 500


# --- Godot text scene parsing -------------------------------------------------


class Node:
    def __init__(self, name: str, kind: str, parent: str | None, instance: str | None):
        self.name = name
        self.type = kind
        self.parent = parent
        self.instance = instance
        self.props: dict = {}

    def path(self) -> str:
        if self.parent is None:
            return "."
        if self.parent == ".":
            return self.name
        return self.parent + "/" + self.name

    def get(self, key, default=None):
        return self.props.get(key, default)


def parse_scene(text: str) -> tuple[dict, list[Node]]:
    """The ext_resources by id and the nodes, with values parsed."""
    ext: dict = {}
    nodes: list[Node] = []
    current: Node | None = None
    key = None
    buffer = ""
    for line in text.splitlines():
        if key is None and line.startswith("["):
            current = None
            header = line.strip()[1:-1]
            if header.startswith("ext_resource "):
                attrs = dict(re.findall(r'(\w+)="([^"]*)"', header))
                ext[attrs["id"]] = attrs
            elif header.startswith("node "):
                attrs = dict(re.findall(r'(\w+)="([^"]*)"', header))
                instance = re.search(r'instance=ExtResource\("([^"]+)"\)', header)
                current = Node(attrs["name"], attrs.get("type", ""), attrs.get("parent"), instance.group(1) if instance else None)
                nodes.append(current)
            continue
        if current is None:
            continue
        if key is None:
            if " = " not in line:
                continue
            key, buffer = line.split(" = ", 1)
            key = key.strip()
        else:
            buffer += "\n" + line
        if buffer.count("(") == buffer.count(")") and buffer.count("[") == buffer.count("]"):
            current.props[key] = parse_value(buffer.strip())
            key = None
            buffer = ""
    return ext, nodes


def parse_value(token: str):
    if token in ("true", "false"):
        return token == "true"
    if token.startswith('"'):
        return token[1:-1]
    if token.startswith("PackedStringArray("):
        return re.findall(r'"((?:[^"\\]|\\.)*)"', token)
    if token.startswith("PackedByteArray("):
        return base64.b64decode(token[len('PackedByteArray("') : -2])
    if token.startswith("Vector2("):
        return tuple(float(v) for v in token[8:-1].split(","))
    if token.startswith("Color("):
        return tuple(float(v) for v in token[6:-1].split(","))
    if token.startswith("ExtResource(") or token.startswith("SubResource("):
        return token.split('"')[1]
    if token.startswith("Array["):
        return re.findall(r'ExtResource\("([^"]+)"\)', token)
    try:
        return float(token) if "." in token else int(token)
    except ValueError:
        return token


def tile_cells(data: bytes) -> list[tuple[int, int, int, int, int]]:
    """Godot TileMapLayer tile_map_data: (x, y, source, atlas_x, atlas_y) per cell."""
    cells = []
    count = (len(data) - 2) // 12
    for i in range(count):
        x, y, source, ax, ay, _alt = struct.unpack_from("<hhhhhh", data, 2 + i * 12)
        cells.append((x, y, source, ax, ay))
    return cells


# --- The TileSet: which source is which region, the kind of each tile -----------


def read_tile_set() -> tuple[dict[int, str], dict[int, list[int]]]:
    """Source id -> region key, and source id -> kind per atlas index."""
    text = open(os.path.join(ROOT, "levels", "world", "world_tiles.tres")).read()
    ext = {}
    for m in re.finditer(r'\[ext_resource type="Texture2D" path="res://levels/world/tiles/([a-z_]+)\.png" id="([^"]+)"\]', text):
        ext[m.group(2)] = m.group(1)
    canvas = {}
    for m in re.finditer(r'\[sub_resource type="CanvasTexture" id="([^"]+)"\]\ndiffuse_texture = ExtResource\("([^"]+)"\)', text):
        canvas[m.group(1)] = ext[m.group(2)]
    sources_by_sub = {}
    kinds_by_sub = {}
    for m in re.finditer(r'\[sub_resource type="TileSetAtlasSource" id="([^"]+)"\]\n(.*?)(?=\n\[|\Z)', text, re.S):
        sub_id, body = m.group(1), m.group(2)
        texture = re.search(r'texture = SubResource\("([^"]+)"\)', body).group(1)
        sources_by_sub[sub_id] = canvas[texture]
        kinds = [0] * SHEET_TILES
        for k in re.finditer(r"^(\d+):(\d+)/0/custom_data_0 = (\d+)$", body, re.M):
            ax, ay, kind = int(k.group(1)), int(k.group(2)), int(k.group(3))
            kinds[ay * ATLAS_COLUMNS + ax] = kind
        kinds_by_sub[sub_id] = kinds
    keys: dict[int, str] = {}
    kinds_by_source: dict[int, list[int]] = {}
    for m in re.finditer(r'^sources/(\d+) = SubResource\("([^"]+)"\)$', text, re.M):
        source = int(m.group(1))
        keys[source] = sources_by_sub[m.group(2)]
        kinds_by_source[source] = kinds_by_sub[m.group(2)]
    return keys, kinds_by_source


# --- Unity YAML ------------------------------------------------------------------


def fid(*parts) -> int:
    """A stable positive file id for an object of a scene or prefab."""
    return int(uuid.uuid5(NAMESPACE, "fileid:" + "|".join(str(p) for p in parts)).int % (2**62)) + 1


def num(value) -> str:
    return assets.yaml_number(value)


def vec2(x, y) -> str:
    return "{x: %s, y: %s}" % (num(x), num(y))


def vec3(x, y, z=0) -> str:
    return "{x: %s, y: %s, z: %s}" % (num(x), num(y), num(z))


def color(c) -> str:
    return assets.yaml_color(c)


def ystr(value: str) -> str:
    """A string value as Unity writes it: bare, or quoted when YAML would misread it."""
    if value == "":
        return ""
    if re.match(r"^[A-Za-z0-9_][A-Za-z0-9_ .()/:-]*$", value) and not value.endswith(" ") and ": " not in value and value not in ("true", "false", "null", "yes", "no"):
        if value[0] not in "-?:,[]{}#&*!|>'\"%@`":
            return value
    return "'" + value.replace("'", "''") + "'"


def strings(values: list[str], indent: str) -> str:
    if not values:
        return " []\n"
    return "\n" + "".join(f"{indent}- {ystr(v)}\n" for v in values)


def sprite_ref(png_rel: str) -> str:
    return "{fileID: 21300000, guid: %s, type: 3}" % assets.guid_for(png_rel)


def asset_ref(rel: str, file_id: int = 11400000) -> str:
    return "{fileID: %d, guid: %s, type: 2}" % (file_id, assets.guid_for(rel))


YAML_HEAD = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"


class Doc:
    """The objects of one scene or prefab file."""

    def __init__(self, tag: str):
        self.tag = tag
        self.chunks: list[str] = []
        self.roots: list[int] = []

    def id(self, *parts) -> int:
        return fid(self.tag, *parts)

    def game_object(self, path: str, name: str, components: list[int], layer: int = 0, active: bool = True) -> int:
        go = self.id("go", path)
        lines = [
            f"--- !u!1 &{go}",
            "GameObject:",
            "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}",
            "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}",
            "  serializedVersion: 6",
            "  m_Component:",
        ]
        for component in components:
            lines.append(f"  - component: {{fileID: {component}}}")
        lines += [
            f"  m_Layer: {layer}",
            f"  m_Name: {ystr(name)}",
            "  m_TagString: Untagged",
            "  m_Icon: {fileID: 0}",
            "  m_NavMeshLayer: 0",
            "  m_StaticEditorFlags: 0",
            f"  m_IsActive: {1 if active else 0}",
        ]
        self.chunks.append("\n".join(lines) + "\n")
        return go

    def transform(self, path: str, parent_path: str | None, position=(0, 0, 0), scale=(1, 1, 1), children: list[int] | None = None) -> int:
        tr = self.id("tr", path)
        go = self.id("go", path)
        father = self.id("tr", parent_path) if parent_path is not None else 0
        lines = [
            f"--- !u!4 &{tr}",
            "Transform:",
            "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}",
            "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}",
            f"  m_GameObject: {{fileID: {go}}}",
            "  serializedVersion: 2",
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
            f"  m_LocalPosition: {vec3(*position)}",
            f"  m_LocalScale: {vec3(*scale)}",
            "  m_ConstrainProportionsScale: 0",
            "  m_Children:" + (" []" if not children else ""),
        ]
        for child in children or []:
            lines.append(f"  - {{fileID: {child}}}")
        lines += [
            f"  m_Father: {{fileID: {father}}}",
            "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",
        ]
        self.chunks.append("\n".join(lines) + "\n")
        if parent_path is None:
            self.roots.append(tr)
        return tr

    def behaviour(self, path: str, script: str, fields: str, key: str = "") -> int:
        comp = self.id("mb", script, path, key)
        go = self.id("go", path)
        text = (
            f"--- !u!114 &{comp}\n"
            "MonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: {go}}}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {SCRIPTS[script]}, type: 3}}\n"
            "  m_Name: \n"
            "  m_EditorClassIdentifier: \n"
        )
        self.chunks.append(text + fields)
        return comp

    def sprite_renderer(self, path: str, sprite: str, material: str, order: int, tint=(1, 1, 1, 1), flip_x=False) -> int:
        comp = self.id("sr", path)
        go = self.id("go", path)
        text = (
            f"--- !u!212 &{comp}\n"
            "SpriteRenderer:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: {go}}}\n"
            "  m_Enabled: 1\n"
            "  m_CastShadows: 0\n"
            "  m_ReceiveShadows: 0\n"
            "  m_DynamicOccludee: 1\n"
            "  m_StaticShadowCaster: 0\n"
            "  m_MotionVectors: 1\n"
            "  m_LightProbeUsage: 1\n"
            "  m_ReflectionProbeUsage: 1\n"
            "  m_RayTracingMode: 0\n"
            "  m_RayTraceProcedural: 0\n"
            "  m_RenderingLayerMask: 1\n"
            "  m_RendererPriority: 0\n"
            "  m_Materials:\n"
            f"  - {material}\n"
            "  m_StaticBatchInfo:\n"
            "    firstSubMesh: 0\n"
            "    subMeshCount: 0\n"
            "  m_StaticBatchRoot: {fileID: 0}\n"
            "  m_ProbeAnchor: {fileID: 0}\n"
            "  m_LightProbeVolumeOverride: {fileID: 0}\n"
            "  m_ScaleInLightmap: 1\n"
            "  m_ReceiveGI: 1\n"
            "  m_PreserveUVs: 0\n"
            "  m_IgnoreNormalsForChartDetection: 0\n"
            "  m_ImportantGI: 0\n"
            "  m_StitchLightmapSeams: 1\n"
            "  m_SelectedEditorRenderState: 0\n"
            "  m_MinimumChartSize: 4\n"
            "  m_AutoUVMaxDistance: 0.5\n"
            "  m_AutoUVMaxAngle: 89\n"
            "  m_LightmapParameters: {fileID: 0}\n"
            "  m_SortingLayerID: 0\n"
            "  m_SortingLayer: 0\n"
            f"  m_SortingOrder: {order}\n"
            f"  m_Sprite: {sprite}\n"
            f"  m_Color: {color(tint)}\n"
            f"  m_FlipX: {1 if flip_x else 0}\n"
            "  m_FlipY: 0\n"
            "  m_DrawMode: 0\n"
            "  m_Size: {x: 1, y: 1}\n"
            "  m_AdaptiveModeThreshold: 0.5\n"
            "  m_SpriteTileMode: 0\n"
            "  m_WasSpriteAssigned: 1\n"
            "  m_MaskInteraction: 0\n"
            "  m_SpriteSortPoint: 0\n"
        )
        self.chunks.append(text)
        return comp

    def prefab_instance(self, path: str, parent_path: str, prefab_rel: str, prefab_tag: str, name: str, position, flip: bool) -> int:
        """A prop placed from its prefab; returns the stripped transform id for the parent's children."""
        inst = self.id("pi", path)
        guid = assets.guid_for(prefab_rel)
        prefab_tr = fid(prefab_tag, "tr", ".")
        prefab_go = fid(prefab_tag, "go", ".")
        stripped = (prefab_tr ^ inst) & 0x7FFFFFFFFFFFFFFF
        if stripped == 0:
            stripped = 1
        mods = []

        def mod(target, prop, value):
            mods.append(
                f"    - target: {{fileID: {target}, guid: {guid}, type: 3}}\n"
                f"      propertyPath: {prop}\n"
                f"      value: {value}\n"
                "      objectReference: {fileID: 0}\n"
            )

        mod(prefab_tr, "m_LocalPosition.x", num(position[0]))
        mod(prefab_tr, "m_LocalPosition.y", num(position[1]))
        mod(prefab_tr, "m_LocalPosition.z", "0")
        mod(prefab_tr, "m_LocalRotation.w", "1")
        mod(prefab_tr, "m_LocalRotation.x", "0")
        mod(prefab_tr, "m_LocalRotation.y", "0")
        mod(prefab_tr, "m_LocalRotation.z", "0")
        mod(prefab_tr, "m_LocalEulerAnglesHint.x", "0")
        mod(prefab_tr, "m_LocalEulerAnglesHint.y", "0")
        mod(prefab_tr, "m_LocalEulerAnglesHint.z", "0")
        if flip:
            mod(prefab_tr, "m_LocalScale.x", "-1")
        mod(prefab_go, "m_Name", ystr(name))
        text = (
            f"--- !u!1001 &{inst}\n"
            "PrefabInstance:\n"
            "  m_ObjectHideFlags: 0\n"
            "  serializedVersion: 2\n"
            "  m_Modification:\n"
            "    serializedVersion: 3\n"
            f"    m_TransformParent: {{fileID: {self.id('tr', parent_path)}}}\n"
            "    m_Modifications:\n" + "".join(mods) + "    m_RemovedComponents: []\n"
            "    m_RemovedGameObjects: []\n"
            "    m_AddedGameObjects: []\n"
            "    m_AddedComponents: []\n"
            f"  m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}\n"
            f"--- !u!4 &{stripped} stripped\n"
            "Transform:\n"
            f"  m_CorrespondingSourceObject: {{fileID: {prefab_tr}, guid: {guid}, type: 3}}\n"
            f"  m_PrefabInstance: {{fileID: {inst}}}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
        )
        self.chunks.append(text)
        return stripped

    def scene_text(self) -> str:
        roots = "".join(f"  - {{fileID: {r}}}\n" for r in self.roots)
        return (
            YAML_HEAD
            + "".join(self.chunks)
            + "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n"
            + roots
        )

    def prefab_text(self) -> str:
        return YAML_HEAD + "".join(self.chunks)


def write(rel: str, text: str) -> None:
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", newline="\n") as handle:
        handle.write(text)


def write_meta(rel: str, text: str) -> None:
    assets.write_if_missing(os.path.join(ROOT, rel + ".meta"), text)


def prefab_meta(guid: str) -> str:
    return assets.META_HEAD % guid + "PrefabImporter:\n  externalObjects: {}\n" + assets.META_TAIL


# --- Units ---------------------------------------------------------------------


def to_units(px: float) -> float:
    return round(px / PPU, 5)


def local(position) -> tuple[float, float]:
    """A Godot position in level pixels (Y down) as region-local units (Y up)."""
    return to_units(position[0]), -to_units(position[1])


def image_units(png_rel: str) -> tuple[float, float]:
    w, h = assets.image_size(os.path.join(ROOT, png_rel))
    return w / ART_TILE, h / ART_TILE


# --- Regions ------------------------------------------------------------------------


class Region:
    def __init__(self, key: str, scene_rel: str, node: Node, top_px: float):
        self.key = key
        self.scene_rel = scene_rel
        self.node = node
        self.top_px = top_px
        self.name = str(node.get("region_name", key))
        self.subtitle = str(node.get("subtitle", ""))
        self.rows = int(node.get("rows", 34))
        self.ext: dict = {}
        self.nodes: list[Node] = []

    @property
    def top_row(self) -> int:
        return int(round(self.top_px / PPU))

    def children(self, parent: str) -> list[Node]:
        return [n for n in self.nodes if n.parent == parent]

    def one(self, path: str) -> Node | None:
        for n in self.nodes:
            if n.path() == path:
                return n
        return None


def read_world() -> tuple[list[Region], list[str], int]:
    text = open(os.path.join(ROOT, "scenes", "world", "world.tscn")).read()
    ext, nodes = parse_scene(text)
    world = nodes[0]
    abilities = list(world.get("abilities", []))
    width = int(world.get("width", 480))
    regions = []
    for node in nodes[1:]:
        if node.instance is None:
            continue
        scene_path = ext[node.instance]["path"].replace("res://", "")
        key = re.sub(r"^\d+_", "", os.path.basename(scene_path)[:-5])
        y = float(node.get("position", (0.0, 0.0))[1])
        region = Region(key, scene_path, node, y)
        own_ext, own_nodes = parse_scene(open(os.path.join(ROOT, scene_path)).read())
        region.ext = own_ext
        region.nodes = own_nodes
        root = own_nodes[0]
        # The world scene's overrides win over the region's own values.
        for k in ("region_name", "subtitle", "rows"):
            if k not in node.props and k in root.props:
                node.props[k] = root.props[k]
        region.name = str(node.get("region_name", key))
        region.subtitle = str(node.get("subtitle", ""))
        region.rows = int(node.get("rows", int(root.get("rows", 34))))
        regions.append(region)
    regions.sort(key=lambda r: r.top_px)
    return regions, abilities, width


def res_path(region: Region, ext_id: str) -> str:
    return region.ext[ext_id]["path"].replace("res://", "")


def region_png(region: Region, ext_id: str) -> str:
    """godot_assets/regions/<key>/x.png -> Assets/Art/Regions/<key>/x.png."""
    path = res_path(region, ext_id)
    return path.replace("godot_assets/regions/", "Assets/Art/Regions/")


# --- Tiles -----------------------------------------------------------------------


def write_tiles(region: Region, source_keys: dict[int, str], kinds_by_source: dict[int, list[int]]) -> tuple[str, str]:
    layers = {}
    kinds = None
    for name in LAYER_NAMES:
        node = region.one("Tiles/" + name)
        cells = tile_cells(node.get("tile_map_data", b"\x00\x00")) if node else []
        triples = []
        for x, y, source, ax, ay in cells:
            if source_keys.get(source) != region.key:
                raise SystemExit(f"{region.key}: a {name} cell uses source {source} ({source_keys.get(source)}), not its own atlas")
            if kinds is None:
                kinds = kinds_by_source[source]
            triples.append((x, y, ay * ATLAS_COLUMNS + ax))
        layers[name] = triples
    if kinds is None:
        kinds = [0] * SHEET_TILES
    lines = ["descender-tiles 1"]
    lines.append("kinds %d %s" % (len(kinds), base64.b64encode(struct.pack("<%dh" % len(kinds), *kinds)).decode()))
    for name in LAYER_NAMES:
        triples = layers[name]
        packed = b"".join(struct.pack("<hhh", *t) for t in triples)
        lines.append("layer %s %d %s" % (name, len(triples), base64.b64encode(packed).decode()))
    cells_rel = f"Assets/Data/Regions/Tiles/{region.key}_cells.txt"
    write(cells_rel, "\n".join(lines) + "\n")

    sheet_rel = f"Assets/Art/Tiles/{region.key}.png"
    sprites = "".join(
        "  - {fileID: %d, guid: %s, type: 3}\n" % (assets.file_id_for(f"{region.key}_{i}"), assets.guid_for(sheet_rel))
        for i in range(SHEET_TILES)
    )
    asset_rel = f"Assets/Data/Regions/Tiles/{region.key}_tiles.asset"
    text = (
        YAML_HEAD + "--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPTS['RegionTileSet']}, type: 3}}\n"
        f"  m_Name: {region.key}_tiles\n  m_EditorClassIdentifier: \n"
        "  sprites:\n" + sprites + f"  cells: {{fileID: 4900000, guid: {assets.guid_for(cells_rel)}, type: 3}}\n"
        f"  litMaterial: {LIT_MATERIAL}\n"
        f"  glowMaterial: {UNLIT_MATERIAL}\n"
    )
    write(asset_rel, text)
    return cells_rel, asset_rel


# --- Props ------------------------------------------------------------------------


def prop_prefab(region: Region, scene_rel: str) -> tuple[str, str]:
    """Writes the prefab of a prop scene (scenes/props/<key>/<name>.tscn); returns (prefab rel, tag)."""
    name = os.path.basename(scene_rel)[:-5]
    prefab_rel = f"Assets/Prefabs/Props/{region.key}/{name}.prefab"
    tag = "prefab:" + prefab_rel
    if prefab_rel in PREFABS_WRITTEN:
        return prefab_rel, tag
    PREFABS_WRITTEN.add(prefab_rel)
    art_rel = f"Assets/Art/Props/{region.key}/{name}.png"
    glow_rel = f"Assets/Art/Props/{region.key}/{name}_glow.png"
    if not os.path.exists(os.path.join(ROOT, art_rel)):
        raise SystemExit(f"prop {name}: no art at {art_rel}")
    has_glow = os.path.exists(os.path.join(ROOT, glow_rel))
    doc = Doc(tag)
    title = " ".join(w[:1].upper() + w[1:] for w in name.split("_"))
    children = [doc.id("tr", "Art")] + ([doc.id("tr", "Glow")] if has_glow else [])
    doc.game_object(".", title, [doc.id("tr", "."), doc.id("mb", "WorldProp", ".", "")])
    doc.transform(".", None, children=children)
    doc.behaviour(".", "WorldProp", "")
    doc.game_object("Art", "Art", [doc.id("tr", "Art"), doc.id("sr", "Art")])
    doc.transform("Art", ".")
    doc.sprite_renderer("Art", sprite_ref(art_rel), LIT_MATERIAL, PROP_ORDER)
    if has_glow:
        doc.game_object("Glow", "Glow", [doc.id("tr", "Glow"), doc.id("sr", "Glow")])
        doc.transform("Glow", ".")
        doc.sprite_renderer("Glow", sprite_ref(glow_rel), UNLIT_MATERIAL, PROP_ORDER + 1)
    write(prefab_rel, doc.prefab_text())
    write_meta(prefab_rel, prefab_meta(assets.guid_for(prefab_rel)))
    return prefab_rel, tag


PREFABS_WRITTEN: set[str] = set()


# --- The scene ------------------------------------------------------------------


def field_str(indent: str, name: str, value: str) -> str:
    return f"{indent}{name}: {ystr(value)}\n"


def write_scene(
    region: Region,
    regions: list[Region],
    abilities: list[str],
    width: int,
    tiles_asset_rel: str,
    checkpoint_index: dict[tuple[str, str], int],
) -> str:
    key = region.key
    doc = Doc("scene:" + key)
    root = "."
    I = "  "

    # The root: RegionRoot at the band's corner.
    groups = ["Tiles", "Backdrop", "Fx", "Lights", "Rooms", "Doors", "Placements", "Props"]
    doc.game_object(root, f"Region_{key}", [doc.id("tr", root), doc.id("mb", "RegionRoot", root, "")])
    doc.transform(root, None, position=(0, -region.top_row, 0), children=[doc.id("tr", g) for g in groups])
    look_rel = f"Assets/Data/Regions/{key}.asset"
    if not os.path.exists(os.path.join(ROOT, look_rel)):
        raise SystemExit(f"{key}: no RegionLook at {look_rel}")
    doc.behaviour(
        root,
        "RegionRoot",
        field_str(I, "key", key)
        + field_str(I, "displayName", region.name)
        + field_str(I, "subtitle", region.subtitle)
        + f"{I}look: {asset_ref(look_rel)}\n"
        + f"{I}topRow: {region.top_row}\n"
        + f"{I}rows: {region.rows}\n"
        + f"{I}tiles: {{fileID: {doc.id('mb', 'RegionTiles', 'Tiles', '')}}}\n"
        + f"{I}backdrop: {{fileID: {doc.id('tr', 'Backdrop')}}}\n",
    )

    # Tiles: RegionTiles and the fringes.
    fringes = region.one("Fringes")
    tiles_components = [doc.id("tr", "Tiles"), doc.id("mb", "RegionTiles", "Tiles", "")]
    if fringes:
        tiles_components.append(doc.id("mb", "RegionFringes", "Tiles", ""))
    doc.game_object("Tiles", "Tiles", tiles_components)
    doc.transform("Tiles", root)
    doc.behaviour(
        "Tiles",
        "RegionTiles",
        f"{I}tileSet: {asset_ref(tiles_asset_rel)}\n{I}width: {width}\n{I}rows: {region.rows}\n",
    )
    if fringes:

        def side(name: str) -> str:
            refs = fringes.get(name, [])
            if not refs:
                return f"{I}{name}: []\n"
            return f"{I}{name}:\n" + "".join(f"{I}- {sprite_ref(region_png(region, r))}\n" for r in refs)

        doc.behaviour(
            "Tiles",
            "RegionFringes",
            side("top")
            + side("bottom")
            + side("left")
            + side("right")
            + f"{I}topChance: {num(fringes.get('top_chance', 1.0))}\n"
            + f"{I}bottomChance: {num(fringes.get('bottom_chance', 1.0))}\n"
            + f"{I}leftChance: {num(fringes.get('left_chance', 1.0))}\n"
            + f"{I}rightChance: {num(fringes.get('right_chance', 1.0))}\n"
            + f"{I}material: {LIT_MATERIAL}\n"
            + f"{I}tiles: {{fileID: {doc.id('mb', 'RegionTiles', 'Tiles', '')}}}\n",
        )

    # Backdrop: a sprite per layer, back to front.
    layers = region.children("Backdrop")
    doc.game_object("Backdrop", "Backdrop", [doc.id("tr", "Backdrop")])
    doc.transform("Backdrop", root, children=[doc.id("tr", n.path()) for n in layers])
    for index, layer in enumerate(layers):
        path = layer.path()
        png = region_png(region, layer.get("texture"))
        w, h = image_units(png)
        x, y = local(layer.get("position", (0.0, 0.0)))
        front = bool(layer.get("front", False))
        cover = bool(layer.get("cover", False))
        lit = bool(layer.get("lit", False))
        tint = layer.get("tint", (1.0, 1.0, 1.0, 1.0))
        doc.game_object(path, layer.name, [doc.id("tr", path), doc.id("sr", path), doc.id("mb", "BackdropLayer", path, "")])
        # The sprite's centre: the Godot layer draws its texture's top-left at the anchor.
        doc.transform(path, "Backdrop", position=(x + w / 2, y - h / 2, 0))
        order = FRONT_ORDER + index if front else BACKDROP_ORDER + index
        doc.sprite_renderer(path, sprite_ref(png), LIT_MATERIAL if lit else UNLIT_MATERIAL, order, tint=tint)
        scroll = layer.get("scroll", (1.0, 1.0))
        drift = layer.get("drift", (0.0, 0.0))
        sway = layer.get("sway", (0.0, 0.0))
        doc.behaviour(
            path,
            "BackdropLayer",
            f"{I}scroll: {vec2(*scroll)}\n{I}cover: {1 if cover else 0}\n{I}front: {1 if front else 0}\n"
            f"{I}drift: {vec2(*drift)}\n{I}sway: {vec2(*sway)}\n{I}repeatY: {1 if layer.get('repeat_y', False) else 0}\n",
        )

    # Fx: the drifting particles.
    fx = region.children("Fx")
    doc.game_object("Fx", "Fx", [doc.id("tr", "Fx")])
    doc.transform("Fx", root, children=[doc.id("tr", n.path()) for n in fx])
    for node in fx:
        path = node.path()
        png = region_png(region, node.get("texture"))
        doc.game_object(path, node.name, [doc.id("tr", path), doc.id("mb", "RegionParticles", path, "")])
        doc.transform(path, "Fx")
        direction = node.get("direction", (0.0, 1.0))
        speed = node.get("speed", (5.0, 15.0))
        gravity = node.get("gravity", (0.0, 0.0))
        size = node.get("size", (1.0, 1.0))
        doc.behaviour(
            path,
            "RegionParticles",
            f"{I}sprite: {sprite_ref(png)}\n{I}amount: {int(node.get('amount', 30))}\n"
            f"{I}color: {color(node.get('color', (1.0, 1.0, 1.0, 1.0)))}\n{I}lifetime: {num(node.get('lifetime', 10.0))}\n"
            f"{I}direction: {vec2(*direction)}\n{I}spread: {num(node.get('spread', 25.0))}\n{I}speed: {vec2(*speed)}\n"
            f"{I}gravity: {vec2(*gravity)}\n{I}size: {vec2(*size)}\n{I}spin: {num(node.get('spin', 0.0))}\n"
            f"{I}sway: {num(node.get('sway', 0.0))}\n{I}material: {UNLIT_MATERIAL}\n",
        )

    # Lights.
    lights = region.children("Lights")
    doc.game_object("Lights", "Lights", [doc.id("tr", "Lights")])
    doc.transform("Lights", root, children=[doc.id("tr", n.path()) for n in lights])
    for node in lights:
        path = node.path()
        doc.game_object(path, node.name, [doc.id("tr", path), doc.id("mb", "WorldLight", path, "")])
        doc.transform(path, "Lights", position=(*local(node.get("position", (0.0, 0.0))), 0))
        radius = float(node.get("texture_scale", 1.0)) * LIGHT_TEXTURE_SIZE / 2.0
        doc.behaviour(
            path,
            "WorldLight",
            f"{I}color: {color(node.get('color', (1.0, 1.0, 1.0, 1.0)))}\n{I}energy: {num(node.get('energy', 1.0))}\n"
            f"{I}radiusPixels: {num(radius)}\n{I}flicker: {1 if node.get('flicker', False) else 0}\n"
            f"{I}activeDistance: 40\n{I}checkEvery: 0.25\n",
        )

    # Rooms.
    rooms = region.children("Rooms")
    doc.game_object("Rooms", "Rooms", [doc.id("tr", "Rooms")])
    doc.transform("Rooms", root, children=[doc.id("tr", n.path()) for n in rooms])
    for node in rooms:
        path = node.path()
        size = node.get("size", ROOM_DEFAULT_SIZE)
        doc.game_object(path, node.name, [doc.id("tr", path), doc.id("mb", "WorldRoom", path, "")])
        doc.transform(path, "Rooms", position=(*local(node.get("position", (0.0, 0.0))), 0))
        doc.behaviour(
            path,
            "WorldRoom",
            f"{I}id: {int(node.get('id', 0))}\n"
            + field_str(I, "kind", str(node.get("kind", "normal")))
            + f"{I}area: {int(node.get('area', 0))}\n{I}depth: {int(node.get('depth', 0))}\n"
            + field_str(I, "roomName", str(node.get("room_name", "")))
            + field_str(I, "ability", str(node.get("ability", "")))
            + f"{I}size: {vec2(to_units(size[0]), to_units(size[1]))}\n",
        )

    # Doors.
    doors = region.children("Doors")
    doc.game_object("Doors", "Doors", [doc.id("tr", "Doors")])
    doc.transform("Doors", root, children=[doc.id("tr", n.path()) for n in doors])
    barrier_sprite = sprite_ref("Assets/Art/Sprites/light_barrier.png")
    for node in doors:
        path = node.path()
        lock_region = int(node.get("lock_region", 0))
        lock_key = regions[lock_region].key if 0 <= lock_region < len(regions) else ""
        doc.game_object(path, node.name, [doc.id("tr", path), doc.id("mb", "WorldDoor", path, "")])
        doc.transform(path, "Doors", position=(*local(node.get("position", (0.0, 0.0))), 0))
        doc.behaviour(
            path,
            "WorldDoor",
            f"{I}id: {int(node.get('id', 0))}\n{I}roomA: {int(node.get('room_a', 0))}\n{I}roomB: {int(node.get('room_b', 0))}\n"
            + field_str(I, "dir", str(node.get("dir", "h")))
            + field_str(I, "lockKind", str(node.get("lock_kind", "")))
            + f"{I}lockAbility: {int(node.get('lock_ability', 0))}\n"
            + field_str(I, "lockRegion", lock_key if node.get("lock_kind", "") == "boss" else "")
            + f"{I}secret: {1 if node.get('secret', False) else 0}\n"
            + f"{I}sealSprite: {barrier_sprite}\n{I}sealMaterial: {UNLIT_MATERIAL}\n",
        )

    # Placements.
    placements = region.children("Placements")
    doc.game_object("Placements", "Placements", [doc.id("tr", "Placements")])
    doc.transform("Placements", root, children=[doc.id("tr", n.path()) for n in placements])
    catalog_ref = asset_ref("Assets/Data/Placements.asset")
    for node in placements:
        path = node.path()
        kind = str(node.get("type", "enemy"))
        if kind == "barrier":
            doc.game_object(path, node.name, [doc.id("tr", path), doc.id("mb", "LightBarrier", path, "")])
            doc.transform(path, "Placements", position=(*local(node.get("position", (0.0, 0.0))), 0))
            doc.behaviour(
                path,
                "LightBarrier",
                f"{I}height: {int(node.get('height', 1))}\n{I}sprite: {barrier_sprite}\n{I}material: {UNLIT_MATERIAL}\n",
            )
            continue
        ability = str(node.get("ability", ""))
        ability_index = abilities.index(ability) if ability in abilities else -1
        doc.game_object(path, node.name, [doc.id("tr", path), doc.id("mb", "WorldPlacement", path, "")])
        doc.transform(path, "Placements", position=(*local(node.get("position", (0.0, 0.0))), 0))
        doc.behaviour(
            path,
            "WorldPlacement",
            field_str(I, "id", f"{key}/{node.name}")
            + field_str(I, "type", kind)
            + f"{I}catalog: {catalog_ref}\n"
            + field_str(I, "ability", ability)
            + f"{I}abilityIndex: {ability_index}\n"
            + f"{I}checkpointIndex: {checkpoint_index.get((key, node.name), -1)}\n"
            + field_str(I, "placementName", str(node.get("placement_name", "")))
            + field_str(I, "character", str(node.get("character", "")))
            + f"{I}tint: {color(node.get('tint', (1.0, 1.0, 1.0, 1.0)))}\n"
            + f"{I}facesLeft: {1 if node.get('faces_left', False) else 0}\n"
            + f"{I}dialogue:" + strings(list(node.get("dialogue", [])), I)
            + f"{I}rotate:" + strings(list(node.get("rotate", [])), I)
            + field_str(I, "unless", str(node.get("unless", "")))
            + field_str(I, "shop", str(node.get("shop", "")))
            + field_str(I, "item", str(node.get("item", "")))
            + field_str(I, "barks", str(node.get("barks", "")))
            + f"{I}passive: {1 if node.get('passive', False) else 0}\n"
            + field_str(I, "behaviour", str(node.get("behaviour", "")))
            + field_str(I, "requires", str(node.get("requires", "")))
            + field_str(I, "upgrade", str(node.get("upgrade", "")))
            + field_str(I, "power", str(node.get("power", ""))),
        )

    # Props: prefab instances.
    props = region.children("Props")
    prop_children = []
    for node in props:
        if node.instance is None:
            continue
        prefab_rel, tag = prop_prefab(region, res_path(region, node.instance))
        scale = node.get("scale", (1.0, 1.0))
        prop_children.append(
            doc.prefab_instance(node.path(), "Props", prefab_rel, tag, node.name, local(node.get("position", (0.0, 0.0))), scale[0] < 0)
        )
    doc.game_object("Props", "Props", [doc.id("tr", "Props")])
    doc.transform("Props", root, children=prop_children)

    scene_rel = f"Assets/Scenes/Region_{key}.unity"
    write(scene_rel, doc.scene_text())
    write_meta(scene_rel, assets.default_meta(assets.guid_for(scene_rel)))
    return scene_rel


# --- The atlas and the catalog ------------------------------------------------------


def checkpoints_in_order(regions: list[Region]) -> list[tuple[Region, Node]]:
    """Every save placement, top to bottom then left to right over the world (level_builder.gd, _above_or_left)."""
    found = []
    for region in regions:
        for node in region.children("Placements"):
            if node.get("type", "enemy") == "save":
                x, y = node.get("position", (0.0, 0.0))
                found.append((y + region.top_px, x, region, node))
    found.sort(key=lambda f: (f[0], f[1]))
    return [(r, n) for _, _, r, n in found]


def write_atlas(regions: list[Region], abilities: list[str], width: int, checkpoints: list[tuple[Region, Node]]) -> None:
    start_region = None
    start = (0.0, 0.0)
    for region in regions:
        for node in region.children("Placements"):
            if node.get("type", "enemy") == "start":
                start_region = region
                x, y = node.get("position", (0.0, 0.0))
                start = (to_units(x), -to_units(y + region.top_px))
    if start_region is None:
        raise SystemExit("no start placement in the world")
    bands = "".join(
        f"  - key: {r.key}\n    displayName: {ystr(r.name)}\n    subtitle: {ystr(r.subtitle)}\n"
        f"    topRow: {r.top_row}\n    rows: {r.rows}\n"
        for r in regions
    )
    points = ""
    for region, node in checkpoints:
        x, y = node.get("position", (0.0, 0.0))
        points += f"  - region: {region.key}\n    position: {vec2(to_units(x), -to_units(y + region.top_px))}\n"
    text = (
        YAML_HEAD + "--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPTS['WorldAtlas']}, type: 3}}\n"
        "  m_Name: World\n  m_EditorClassIdentifier: \n"
        "  regions:\n" + bands + f"  width: {width}\n"
        f"  startRegion: {start_region.key}\n  start: {vec2(*start)}\n"
        "  checkpoints:\n" + points + "  abilities:" + strings(abilities, "  ")
    )
    write("Assets/Data/World.asset", text)


def write_catalog() -> None:
    rel = "Assets/Data/Placements.asset"
    if os.path.exists(os.path.join(ROOT, rel)):
        return  # Unity fills and owns it from then on.
    text = (
        YAML_HEAD + "--- !u!114 &11400000\nMonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPTS['PlacementCatalog']}, type: 3}}\n"
        "  m_Name: Placements\n  m_EditorClassIdentifier: \n"
        "  entries: []\n"
    )
    write(rel, text)


def check_scripts() -> None:
    for name, guid in SCRIPTS.items():
        meta = os.path.join(ROOT, "Assets", "Scripts", "Runtime", "World", name + ".cs.meta")
        if os.path.exists(meta):
            found = re.search(r"^guid: ([0-9a-f]+)", open(meta).read(), re.M).group(1)
            if found != guid:
                raise SystemExit(f"{name}.cs.meta has guid {found}, the path scheme gives {guid}; the scenes would not find it")


def main() -> int:
    check_scripts()
    source_keys, kinds_by_source = read_tile_set()
    regions, abilities, width = read_world()
    checkpoints = checkpoints_in_order(regions)
    index = {(r.key, n.name): i for i, (r, n) in enumerate(checkpoints)}
    write_catalog()
    scenes = []
    for region in regions:
        _cells, tiles_asset = write_tiles(region, source_keys, kinds_by_source)
        scenes.append(write_scene(region, regions, abilities, width, tiles_asset, index))
    write_atlas(regions, abilities, width, checkpoints)
    metas = assets.write_metas()
    print(
        f"{len(scenes)} region scenes, {len(PREFABS_WRITTEN)} prop prefabs, {len(checkpoints)} checkpoints, "
        f"{metas} new .meta files"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
