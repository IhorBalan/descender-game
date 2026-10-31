---
name: editor-tooling
description: Editor scripts in this project: menu items under Descender/, the importers (sprite animations, prop prefabs, .tres profiles via the Python script), ProjectBootstrap, AssetDatabase and PrefabUtility usage, custom inspectors and property drawers, OnValidate, and the deterministic .meta GUID scheme used by tools/unity. Use when writing anything under Assets/Scripts/Editor or tools/unity, or when generating assets.
---

# Editor tooling

All editor code is in `Assets/Scripts/Editor` (`Descender.Editor`,
namespace `Descender.EditorTools.*`), never in a runtime folder. Menu
items live under `Descender/`:

| Menu | Class | Does |
|---|---|---|
| Setup / Render pipeline and settings | `ProjectBootstrap` | URP asset + 2D Renderer, assign to Graphics/Quality, physics 60 Hz, player settings, layers |
| Import / Sprite animations | `SpriteAnimationImporter` | clips + animator per character from `sprites.json` |
| Import / Prop prefabs | `PropPrefabImporter` | prefab per prop PNG triplet |
| Import / Apply pixel art import | `PixelArtImport` | texture settings on selected textures |

Every item is idempotent: running it twice changes nothing the second
time, and it never overwrites an asset a person has edited (it loads and
updates the fields it owns). Say what it did in one `Debug.Log` at the
end.

## Generating assets

```csharp
var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
// ... change it ...
EditorUtility.SetDirty(clip);
AssetDatabase.SaveAssets();          // once, at the end of the whole operation
```

- Folders: `AssetDatabase.IsValidFolder` then `CreateFolder`
  (`Directory.CreateDirectory` + `Refresh` is also fine for a tree).
- Prefabs: build the hierarchy in memory, `PrefabUtility.SaveAsPrefabAsset`,
  `Object.DestroyImmediate` the temporary. To edit an existing prefab,
  `LoadPrefabContents` / `SaveAsPrefabAsset` / `UnloadPrefabContents`.
- Sprites of a texture: `AssetDatabase.LoadAllAssetsAtPath(texturePath)`
  filtered to `Sprite`, sorted by the `_<index>` suffix.
- Many changes: wrap in `AssetDatabase.StartAssetEditing()` /
  `StopAssetEditing()` in a `try/finally`, then one `SaveAssets`.
- Settings assets: `SerializedObject` on the asset from
  `AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]`,
  `ApplyModifiedProperties`.

## The Python importers

`tools/unity/import_godot_assets.py` (run with `python3 -I`) copies the
Godot art, audio and data into `Assets/` and writes `.meta` files with
import settings and sprite slicing from `sprites.json`, plus `.asset`
ScriptableObjects from the `.tres` profiles. It exists because the port
runs without a Unity editor available; once Unity has opened the project
these files are ordinary assets.

GUIDs are deterministic: `uuid5(NAMESPACE, "Assets/relative/path").hex`
with the namespace `5a8f1c1e-2b3d-4c5e-9f70-0de5ce000001`. Script `.asset`
files reference their class by the GUID computed from the script path the
same way, so a `.cs` **must keep its path** or every `.asset` pointing at
it loses its script (Unity shows "Missing (Mono Script)"). Moving a script:
update `SCRIPT_GUIDS` in the Python and the `m_Script` lines, or do the
move inside Unity, which rewrites the references. The script never
overwrites a `.meta` or `.asset` that exists.

`TresParser` (editor, tested) reads the Godot `.tres` text format for
anything that still needs to pull numbers out of the old resources.

## Inspectors

- Prefer `[Header]`, `[Tooltip]`, `[Range]`, `[Min]` and `OnValidate` over
  a custom editor. `OnValidate` clamps and warns; it must not create
  assets or touch other objects.
- A `PropertyDrawer` for small structs that are edited often
  (`LootTable`), a custom `Editor` only for a workflow (a button that
  regenerates something). Custom editors use `serializedObject` and
  `PropertyField`, never direct field writes (undo breaks).
- `[CreateAssetMenu(menuName = "Descender/Walker Profile")]` on every
  ScriptableObject type.
- Scene helpers that must run in the editor (`BackdropLayer` preview) use
  `[ExecuteAlways]` guarded by `Application.isPlaying` checks, not
  `[ExecuteInEditMode]`.

## Gizmos

`OnDrawGizmosSelected` for room bounds, door targets, light ranges,
enemy patrol extents; pixel-unit conversions through
`PlayerTuning.PixelsPerUnit`. Keep gizmos cheap and selected-only.

## Testing editor code

Importers are tested in EditMode (`Descender.Tests.EditMode` references
`Descender.Editor`): generate into `Assets/Tests/Temp/` and delete in
`[TearDown]` with `AssetDatabase.DeleteAsset`. Pure helpers
(`PropPrefabImporter.ToTitle`, `TresParser`) are tested directly.
