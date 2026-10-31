#!/usr/bin/env python3
"""Writes the hero, enemy and pickup prefabs, and the small data assets they
reference (the hit-flash material, the SFX library, the hero tuning), as Unity
YAML, so a cloud session without an editor can keep them in step with the code.

Safe to run again: every file is rewritten from what is in the repo, with
deterministic GUIDs and fileIDs (uuid5 of the asset path, as
import_godot_assets.py does), so references elsewhere never break. Run from
anywhere; then let Unity import, or run import_godot_assets.py for the .meta
files of anything new.

    python3 tools/unity/build_prefabs.py
"""

from __future__ import annotations

import json
import os
import re
import sys
import uuid

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import import_godot_assets as importer  # noqa: E402

ROOT = importer.ROOT
ASSETS = importer.ASSETS
PPU = 16.0

SPRITE_LIT_DEFAULT = "a97c105638bdf8b4a8650670310a4cd3"
FLASH_SHADER = "Assets/Shaders/SpriteFlash.shader"
FLASH_MATERIAL = "Assets/Settings/Materials/Sprite Flash.mat"
SFX_LIBRARY = "Assets/Data/SfxLibrary.asset"
PLAYER_TUNING = "Assets/Data/PlayerTuning.asset"

SCRIPTS = "Assets/Scripts/Runtime"

# Which behaviour each enemy character gets, and its visual sorting order.
WALKERS = ["imp", "angel_guard", "brute", "slug", "hoarder", "burned", "hound"]
FLYERS = ["watcher", "shade", "siren"]
HANGERS = ["hanged"]
BOSS_BODIES = ["angel_guard", "brute", "imp", "hero"]


def guid(path: str) -> str:
    return importer.guid_for(path)


def file_id(path: str, name: str) -> int:
    """A stable positive int64 for an object inside a prefab or asset."""
    return int(uuid.uuid5(importer.NAMESPACE, f"fileid:{path}:{name}").int % (2**62)) + 1


def script_ref(relative_script: str) -> str:
    return f"{{fileID: 11500000, guid: {guid(relative_script)}, type: 3}}"


def script(name: str) -> str:
    """The script reference for a runtime class by its folder/name under Assets/Scripts/Runtime."""
    return script_ref(f"{SCRIPTS}/{name}.cs")


def num(value: float) -> str:
    return importer.yaml_number(value)


# --- sprites -----------------------------------------------------------------


def sprite_ids(texture_relative: str) -> dict[str, int]:
    """Sprite name -> internal fileID, read back from the texture's .meta."""
    meta_path = os.path.join(ROOT, texture_relative + ".meta")
    with open(meta_path) as handle:
        text = handle.read()
    ids: dict[str, int] = {}
    for match in re.finditer(r"213: (-?\d+)\n\s+second: (\S+)", text):
        ids[match.group(2)] = int(match.group(1))
    if not ids:
        # A single-sprite texture: Unity's fixed id for the one sprite.
        stem = os.path.splitext(os.path.basename(texture_relative))[0]
        ids[stem] = 21300000
    return ids


def sprite_ref(texture_relative: str, name: str) -> str:
    ids = sprite_ids(texture_relative)
    if name not in ids:
        if len(ids) == 1:
            name = next(iter(ids))
        else:
            raise KeyError(f"{name} is not a sprite of {texture_relative}: {sorted(ids)}")
    return f"{{fileID: {ids[name]}, guid: {guid(texture_relative)}, type: 3}}"


def standalone_sprite(stem: str, frame: int | None = None) -> str:
    texture = f"Assets/Art/Sprites/{stem}.png"
    return sprite_ref(texture, f"{stem}_{frame}" if frame is not None else stem)


def strips_yaml(character: str, indent: int = 2) -> str:
    """The SpriteAnimator strips of a character from godot_assets/sprites/sprites.json."""
    spec = importer.sprite_spec()[character]
    pad = " " * indent
    lines = []
    for anim_name, anim in spec["animations"].items():
        texture = f"Assets/Art/Sprites/{character}/{anim['file']}"
        lines.append(f"{pad}- name: {anim_name}")
        lines.append(f"{pad}  fps: {num(anim.get('fps', 8))}")
        lines.append(f"{pad}  loop: {1 if anim.get('loop', False) else 0}")
        lines.append(f"{pad}  frames:")
        for i in range(int(anim["frames"])):
            lines.append(f"{pad}  - {sprite_ref(texture, f'{character}_{anim_name}_{i}')}")
    return "\n".join(lines) + "\n"


def first_frame(character: str, anim: str = "idle") -> str:
    spec = importer.sprite_spec()[character]
    texture = f"Assets/Art/Sprites/{character}/{spec['animations'][anim]['file']}"
    return sprite_ref(texture, f"{character}_{anim}_0")


# --- prefab building blocks --------------------------------------------------

HEADER = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"


class Prefab:
    """Collects the objects of one prefab and writes them in Unity's order."""

    def __init__(self, relative: str):
        self.relative = relative
        self.blocks: list[str] = []

    def fid(self, name: str) -> int:
        return file_id(self.relative, name)

    def game_object(self, name: str, layer: int, components: list[int], obj_name: str | None = None) -> int:
        fid = self.fid(f"go:{name}")
        comps = "".join(f"  - component: {{fileID: {c}}}\n" for c in components)
        self.blocks.append(
            f"--- !u!1 &{fid}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n"
            f"  m_Component:\n{comps}  m_Layer: {layer}\n  m_Name: {obj_name or name}\n  m_TagString: Untagged\n"
            f"  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
        )
        return fid

    def transform(self, name: str, parent: int, children: list[int], scale: float = 1.0, position=(0.0, 0.0)) -> int:
        fid = self.fid(f"tr:{name}")
        kids = "".join(f"  - {{fileID: {c}}}\n" for c in children) if children else ""
        self.blocks.append(
            f"--- !u!4 &{fid}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {self.fid('go:' + name)}}}\n"
            f"  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n"
            f"  m_LocalPosition: {{x: {num(position[0])}, y: {num(position[1])}, z: 0}}\n"
            f"  m_LocalScale: {{x: {num(scale)}, y: {num(scale)}, z: 1}}\n"
            f"  m_ConstrainProportionsScale: 0\n  m_Children:{' []' if not children else ''}\n{kids}"
            f"  m_Father: {{fileID: {parent}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
        )
        return fid

    def rigidbody(self, name: str, body_type: int = 0, continuous: bool = True) -> int:
        fid = self.fid(f"rb:{name}")
        self.blocks.append(
            f"--- !u!50 &{fid}\nRigidbody2D:\n  serializedVersion: 5\n  m_ObjectHideFlags: 0\n"
            f"  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n"
            f"  m_GameObject: {{fileID: {self.fid('go:' + name)}}}\n  m_BodyType: {body_type}\n  m_Simulated: 1\n"
            f"  m_UseFullKinematicContacts: 0\n  m_UseAutoMass: 0\n  m_Mass: 1\n  m_LinearDamping: 0\n"
            f"  m_AngularDamping: 0.05\n  m_GravityScale: 0\n  m_Material: {{fileID: 0}}\n  m_IncludeLayers:\n"
            f"    serializedVersion: 2\n    m_Bits: 0\n  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n"
            f"  m_Interpolate: 1\n  m_SleepingMode: 1\n  m_CollisionDetection: {1 if continuous else 0}\n  m_Constraints: 4\n"
        )
        return fid

    def box(self, name: str, size_px: tuple[float, float], offset_px: tuple[float, float], trigger: bool = False) -> int:
        fid = self.fid(f"box:{name}")
        self.blocks.append(
            f"--- !u!61 &{fid}\nBoxCollider2D:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {self.fid('go:' + name)}}}\n"
            f"  m_Enabled: 1\n  serializedVersion: 3\n  m_Density: 1\n  m_Material: {{fileID: 0}}\n  m_IncludeLayers:\n"
            f"    serializedVersion: 2\n    m_Bits: 0\n  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n"
            f"  m_LayerOverridePriority: 0\n  m_ForceSendLayers:\n    serializedVersion: 2\n    m_Bits: 4294967295\n"
            f"  m_ForceReceiveLayers:\n    serializedVersion: 2\n    m_Bits: 4294967295\n  m_ContactCaptureLayers:\n"
            f"    serializedVersion: 2\n    m_Bits: 4294967295\n  m_CallbackLayers:\n    serializedVersion: 2\n"
            f"    m_Bits: 4294967295\n  m_IsTrigger: {1 if trigger else 0}\n  m_UsedByEffector: 0\n  m_CompositeOperation: 0\n"
            f"  m_CompositeOrder: 0\n  m_Offset: {{x: {num(offset_px[0] / PPU)}, y: {num(offset_px[1] / PPU)}}}\n"
            f"  m_SpriteTilingProperty:\n    border: {{x: 0, y: 0, z: 0, w: 0}}\n    pivot: {{x: 0.5, y: 0.5}}\n"
            f"    oldSize: {{x: 1, y: 1}}\n    newSize: {{x: 1, y: 1}}\n    adaptiveTilingThreshold: 0.5\n"
            f"    drawMode: 0\n    adaptiveTiling: 0\n  m_AutoTiling: 0\n  m_Size: {{x: {num(size_px[0] / PPU)}, y: {num(size_px[1] / PPU)}}}\n"
            f"  m_EdgeRadius: 0\n"
        )
        return fid

    def sprite_renderer(self, name: str, sprite: str, order: int) -> int:
        fid = self.fid(f"sr:{name}")
        self.blocks.append(
            f"--- !u!212 &{fid}\nSpriteRenderer:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {self.fid('go:' + name)}}}\n"
            f"  m_Enabled: 1\n  m_CastShadows: 0\n  m_ReceiveShadows: 0\n  m_DynamicOccludee: 1\n  m_StaticShadowCaster: 0\n"
            f"  m_MotionVectors: 1\n  m_LightProbeUsage: 1\n  m_ReflectionProbeUsage: 1\n  m_RayTracingMode: 0\n"
            f"  m_RayTraceProcedural: 0\n  m_RayTracingAccelStructBuildFlagsOverride: 0\n  m_RayTracingAccelStructBuildFlags: 1\n"
            f"  m_SmallMeshCulling: 1\n  m_RenderingLayerMask: 1\n  m_RendererPriority: 0\n  m_Materials:\n"
            f"  - {{fileID: 2100000, guid: {SPRITE_LIT_DEFAULT}, type: 2}}\n  m_StaticBatchInfo:\n    firstSubMesh: 0\n"
            f"    subMeshCount: 0\n  m_StaticBatchRoot: {{fileID: 0}}\n  m_ProbeAnchor: {{fileID: 0}}\n"
            f"  m_LightProbeVolumeOverride: {{fileID: 0}}\n  m_ScaleInLightmap: 1\n  m_ReceiveGI: 1\n  m_PreserveUVs: 0\n"
            f"  m_IgnoreNormalsForChartDetection: 0\n  m_ImportantGI: 0\n  m_StitchLightmapSeams: 1\n"
            f"  m_SelectedEditorRenderState: 0\n  m_MinimumChartSize: 4\n  m_AutoUVMaxDistance: 0.5\n  m_AutoUVMaxAngle: 89\n"
            f"  m_LightmapParameters: {{fileID: 0}}\n  m_SortingLayerID: 0\n  m_SortingLayer: 0\n  m_SortingOrder: {order}\n"
            f"  m_Sprite: {sprite}\n  m_Color: {{r: 1, g: 1, b: 1, a: 1}}\n  m_FlipX: 0\n  m_FlipY: 0\n"
            f"  m_DrawMode: 0\n  m_Size: {{x: 1, y: 1}}\n  m_AdaptiveModeThreshold: 0.5\n  m_SpriteTileMode: 0\n"
            f"  m_WasSpriteAssigned: 1\n  m_MaskInteraction: 0\n  m_SpriteSortPoint: 0\n"
        )
        return fid

    def behaviour(self, name: str, key: str, script_ref_text: str, fields: str) -> int:
        fid = self.fid(f"mb:{name}:{key}")
        self.blocks.append(
            f"--- !u!114 &{fid}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {self.fid('go:' + name)}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {script_ref_text}\n  m_Name: \n"
            f"  m_EditorClassIdentifier: \n{fields}"
        )
        return fid

    def write(self) -> str:
        path = os.path.join(ROOT, self.relative)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w") as handle:
            handle.write(HEADER + "".join(self.blocks))
        return self.relative


def animated_body(
    prefab: Prefab,
    name: str,
    layer: int,
    character: str,
    order: int,
    body_px: tuple[float, float],
    components: list[tuple[str, str, str]],
    scale: float = 1.0,
    strips: str | None = None,
    first: str | None = None,
) -> None:
    """A root with a Rigidbody2D and a box at its feet, a Visual child with the
    character's strips, and the given MonoBehaviours (key, script, fields with
    {sprite} and {renderer} placeholders) on the root, plus a HitFlash."""
    visual = f"{name}/Visual"
    rb = prefab.rigidbody(name)
    box = prefab.box(name, body_px, (0.0, body_px[1] / 2.0))
    sr = prefab.sprite_renderer(visual, first or first_frame(character), order)
    animator = prefab.behaviour(
        visual,
        "SpriteAnimator",
        script("Fx/SpriteAnimator"),
        f"  strips:\n{strips if strips is not None else strips_yaml(character)}  startAnimation: idle\n",
    )
    root_components = [prefab.fid(f"tr:{name}"), rb, box]
    for key, script_text, fields in components:
        root_components.append(
            prefab.behaviour(
                name,
                key,
                script_text,
                fields.replace("{sprite}", f"{{fileID: {animator}}}").replace("{renderer}", f"{{fileID: {sr}}}"),
            )
        )
    root_components.append(
        prefab.behaviour(
            name,
            "HitFlash",
            script("Fx/HitFlash"),
            f"  renderers:\n  - {{fileID: {sr}}}\n  flashMaterial: {{fileID: 2100000, guid: {guid(FLASH_MATERIAL)}, type: 2}}\n",
        )
    )
    prefab.game_object(name, layer, root_components, obj_name=name.split("/")[-1])
    prefab.transform(name, 0, [prefab.fid(f"tr:{visual}")])
    prefab.game_object(visual, layer, [prefab.fid(f"tr:{visual}"), sr, animator], obj_name="Visual")
    prefab.transform(visual, prefab.fid(f"tr:{name}"), [], scale=scale)


LAYER_PLAYER = 9
LAYER_ENEMY = 10
LAYER_HAZARD = 13
LAYER_PICKUP = 14


def build_player() -> str:
    prefab = Prefab("Assets/Prefabs/Player.prefab")
    animated_body(
        prefab,
        "Player",
        LAYER_PLAYER,
        "hero",
        10,
        (14.0, 32.0),
        [
            (
                "PlayerController",
                script("Player/PlayerController"),
                f"  tuning: {{fileID: 11400000, guid: {guid(PLAYER_TUNING)}, type: 2}}\n  sprite: {{sprite}}\n"
                "  boltPrefab: {fileID: 0}\n  autoRespawn: 1\n",
            ),
            (
                "PlayerSprite",
                script("Player/PlayerSprite"),
                f"  controller: {{fileID: {prefab.fid('mb:Player:PlayerController')}}}\n  sprite: {{sprite}}\n",
            ),
        ],
    )
    return prefab.write()


def enemy_box(character: str) -> tuple[float, float]:
    """The body of a walker from its profile asset, or a size by the strips for the rest."""
    profile = os.path.join(ASSETS, "Data", "Enemies", f"{character}.asset")
    if os.path.exists(profile):
        with open(profile) as handle:
            match = re.search(r"body: \{x: ([\d.]+), y: ([\d.]+)\}", handle.read())
        if match:
            return float(match.group(1)), float(match.group(2))
    return 16.0, 16.0


def build_enemy(character: str) -> str:
    prefab = Prefab(f"Assets/Prefabs/Enemies/{character}.prefab")
    profile_ref = f"{{fileID: 11400000, guid: {guid(f'Assets/Data/Enemies/{character}.asset')}, type: 2}}"
    if character in FLYERS:
        components = [
            (
                "Flyer",
                script("Enemies/Flyer"),
                f"  profile: {profile_ref}\n  sprite: {{sprite}}\n  contactDamage: 1\n"
                "  bodySize: {x: 16, y: 16}\n  dive: 0\n",
            )
        ]
        body = (16.0, 16.0)
    elif character in HANGERS:
        components = [("Hanger", script("Enemies/Hanger"), "  sprite: {sprite}\n  maxHealth: 3\n")]
        body = (16.0, 44.0)
    else:
        components = [
            (
                "Walker",
                script("Enemies/Walker"),
                f"  profile: {profile_ref}\n  sprite: {{sprite}}\n  contactDamage: 1\n  behaviour: 0\n"
                "  barks: \n  passive: 0\n",
            )
        ]
        body = enemy_box(character)
    animated_body(prefab, character, LAYER_ENEMY, character, 5, body, components)
    return prefab.write()


def build_boss() -> str:
    prefab = Prefab("Assets/Prefabs/Enemies/Boss.prefab")
    wardrobe = "  wardrobe:\n"
    for character in BOSS_BODIES:
        wardrobe += f"  - character: {character}\n    strips:\n" + strips_yaml(character, indent=4)
    imp_ref = f"{{fileID: {file_id('Assets/Prefabs/Enemies/imp.prefab', 'mb:imp:Walker')}, guid: {guid('Assets/Prefabs/Enemies/imp.prefab')}, type: 3}}"
    animated_body(
        prefab,
        "Boss",
        LAYER_ENEMY,
        "angel_guard",
        6,
        (32.0, 80.0),
        [
            (
                "Boss",
                script("Enemies/Boss"),
                "  profile: {fileID: 0}\n  sprite: {sprite}\n" + wardrobe + f"  impPrefab: {imp_ref}\n"
                "  arena:\n    serializedVersion: 2\n    x: -8\n    y: -4\n    width: 16\n    height: 8\n",
            )
        ],
        strips=strips_yaml("angel_guard"),
    )
    return prefab.write()


def simple_pickup(relative: str, name: str, layer: int, key: str, script_text: str, fields: str, sprite: str | None, order: int = 5, box=None) -> str:
    """A pickup: a trigger box, its behaviour, and (when it has one) a Sprite child."""
    prefab = Prefab(relative)
    components = [prefab.fid(f"tr:{name}")]
    if box is not None:
        components.append(prefab.box(name, box[0], box[1], trigger=True))
    children = []
    sr_text = "{fileID: 0}"
    if sprite is not None:
        visual = f"{name}/Sprite"
        sr = prefab.sprite_renderer(visual, sprite, order)
        prefab.game_object(visual, layer, [prefab.fid(f"tr:{visual}"), sr], obj_name="Sprite")
        children.append(prefab.fid(f"tr:{visual}"))
        sr_text = f"{{fileID: {sr}}}"
    components.append(prefab.behaviour(name, key, script_text, fields.replace("{renderer}", sr_text)))
    prefab.game_object(name, layer, components)
    prefab.transform(name, 0, children)
    if sprite is not None:
        visual = f"{name}/Sprite"
        lift = {"Checkpoint": 0.0, "AbilityPickup": 8.0}.get(name, 16.0)
        scale = 1.0 if name in ("Checkpoint", "AbilityPickup") else 2.0
        prefab.transform(visual, prefab.fid(f"tr:{name}"), [], scale=scale, position=(0.0, lift / PPU))
    return prefab.write()


def build_pickups() -> list[str]:
    written = []
    written.append(
        simple_pickup(
            "Assets/Prefabs/Pickups/Checkpoint.prefab",
            "Checkpoint",
            LAYER_PICKUP,
            "Checkpoint",
            script("Pickups/Checkpoint"),
            "  index: 0\n  sprite: {renderer}\n  frames:\n"
            f"  - {standalone_sprite('checkpoint', 0)}\n  - {standalone_sprite('checkpoint', 1)}\n",
            standalone_sprite("checkpoint", 0),
            order=-1,
            box=((24.0, 32.0), (0.0, 16.0)),
        )
    )
    written.append(
        simple_pickup(
            "Assets/Prefabs/Pickups/AbilityPickup.prefab",
            "AbilityPickup",
            LAYER_PICKUP,
            "AbilityPickup",
            script("Pickups/AbilityPickup"),
            "  ability: 0\n  sprite: {renderer}\n",
            standalone_sprite("feather"),
            box=((16.0, 16.0), (0.0, 8.0)),
        )
    )
    written.append(
        simple_pickup(
            "Assets/Prefabs/Pickups/Upgrade.prefab",
            "Upgrade",
            LAYER_PICKUP,
            "UpgradePickup",
            script("Pickups/UpgradePickup"),
            "  kind: 0\n  flag: \n  sprites:\n"
            f"  - {standalone_sprite('upgrade_heart')}\n  - {standalone_sprite('upgrade_vial')}\n  - {standalone_sprite('upgrade_iron')}\n",
            standalone_sprite("upgrade_heart"),
            box=((20.0, 28.0), (0.0, 14.0)),
        )
    )
    written.append(
        simple_pickup(
            "Assets/Prefabs/Pickups/PowerUp.prefab",
            "PowerUp",
            LAYER_PICKUP,
            "PowerUp",
            script("Pickups/PowerUp"),
            "  power: 0\n  sprites:\n"
            f"  - {standalone_sprite('power_halo')}\n  - {standalone_sprite('power_blade')}\n  - {standalone_sprite('power_zeal')}\n",
            standalone_sprite("power_halo"),
            box=((20.0, 28.0), (0.0, 14.0)),
        )
    )
    written.append(
        simple_pickup(
            "Assets/Prefabs/Pickups/LootDrop.prefab",
            "LootDrop",
            LAYER_PICKUP,
            "LootDrop",
            script("Pickups/LootDrop"),
            "  itemId: \n",
            None,
            box=((20.0, 24.0), (0.0, 10.0)),
        )
    )
    written.append(
        simple_pickup("Assets/Prefabs/Pickups/LightDrop.prefab", "LightDrop", LAYER_PICKUP, "LightDrop", script("Pickups/LightDrop"), "", None)
    )
    written.append(
        simple_pickup("Assets/Prefabs/Pickups/HealMote.prefab", "HealMote", LAYER_PICKUP, "HealMote", script("Pickups/HealMote"), "", None)
    )
    written.append(
        simple_pickup(
            "Assets/Prefabs/Pickups/Hazard.prefab",
            "Hazard",
            LAYER_HAZARD,
            "Hazard",
            script("Pickups/Hazard"),
            "  damage: 1\n",
            None,
            box=((16.0, 8.0), (8.0, 4.0)),
        )
    )
    return written


# --- data assets -------------------------------------------------------------


def write_text(relative: str, text: str) -> str:
    path = os.path.join(ROOT, relative)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as handle:
        handle.write(text)
    return relative


def build_material() -> str:
    return write_text(
        FLASH_MATERIAL,
        HEADER
        + "--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
        f"  m_Name: Sprite Flash\n  m_Shader: {{fileID: 4800000, guid: {guid(FLASH_SHADER)}, type: 3}}\n"
        "  m_Parent: {fileID: 0}\n  m_ModifiedSerializedProperties: 0\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n"
        "  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 0\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n"
        "  stringTagMap: {}\n  disabledShaderPasses: []\n  m_LockedProperties: \n  m_SavedProperties:\n"
        "    serializedVersion: 3\n    m_TexEnvs:\n    - _MainTex:\n        m_Texture: {fileID: 0}\n"
        "        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}\n    m_Ints: []\n    m_Floats: []\n"
        "    m_Colors:\n    - _Color: {r: 1, g: 1, b: 1, a: 1}\n  m_BuildTextureStacks: []\n",
    )


def build_sfx_library() -> str:
    folder = os.path.join(ASSETS, "Audio", "sfx")
    entries = ""
    for filename in sorted(os.listdir(folder)):
        if not filename.endswith((".wav", ".ogg")):
            continue
        name = os.path.splitext(filename)[0]
        clip_guid = guid(f"Assets/Audio/sfx/{filename}")
        entries += f"  - name: {name}\n    clip: {{fileID: 8300000, guid: {clip_guid}, type: 3}}\n"
    return write_text(
        SFX_LIBRARY,
        HEADER
        + "--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n"
        f"  m_EditorHideFlags: 0\n  m_Script: {script('Audio/SfxLibrary')}\n  m_Name: SfxLibrary\n"
        f"  m_EditorClassIdentifier: \n  entries:\n{entries}",
    )


def build_player_tuning() -> str:
    """The hero's numbers at the script's defaults: fields left out of the YAML keep
    their initializers, so this asset follows PlayerTuning.cs without repeating it."""
    return write_text(
        PLAYER_TUNING,
        HEADER
        + "--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n"
        f"  m_EditorHideFlags: 0\n  m_Script: {script('Player/PlayerTuning')}\n  m_Name: PlayerTuning\n"
        "  m_EditorClassIdentifier: \n",
    )


def main() -> int:
    written = [build_material(), build_sfx_library(), build_player_tuning(), build_player()]
    written += [build_enemy(character) for character in WALKERS + FLYERS + HANGERS]
    written.append(build_boss())
    written += build_pickups()
    metas = importer.write_metas()
    print(f"wrote {len(written)} assets and {metas} .meta files")
    for relative in written:
        print("  " + relative)
    return 0


if __name__ == "__main__":
    sys.exit(main())
