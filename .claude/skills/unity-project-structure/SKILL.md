---
name: unity-project-structure
description: Where things live in the Unity project (Assets/Scripts, Assets/Scenes, Assets/Prefabs, Assets/Data, Assets/Art), the assembly definitions, namespaces, static state classes, scenes vs prefabs, and what the leftover Godot files are for. Use when adding a script, scene, prefab, asset or folder, or when unsure where something goes.
---

# Unity project structure

The Unity project lives at the repository root (`Assets/`, `Packages/`,
`ProjectSettings/`), Unity 6.3 LTS (`ProjectSettings/ProjectVersion.txt`).
The Godot project it is ported from still sits beside it (`scenes/`,
`scripts/`, `levels/`, `resources/`, `project.godot`) as the reference for
behaviour and numbers until the port is complete; each Unity folder carries
a `.gdignore` so Godot ignores it. Do not edit the Godot files unless the
task is about the port's reference, and never build new features there.

## Folders

```
Assets/
  Scenes/            Main.unity (persistent: camera, HUD, audio, systems), one .unity per region
  Scripts/
    Runtime/         Descender.Runtime.asmdef; one folder per area:
      Player/        PlayerController, PlayerTuning, abilities
      Enemies/       Walker, Flyer, Boss, their *Profile ScriptableObjects
      World/         RegionLook, WorldProp, WorldRoom, WorldDoor, BackdropLayer, region streaming
      Story/         StoryFlags, DialogueData, Quests
      Economy/       Items, Inventory, Shops, Skills, LootTable
      Save/          SaveData, SaveSlots, SaveManager, SettingsData
      UI/            HUD, menus, dialogue box, prompts
      Audio/         Music, Sfx
      Input/         InputRouter (owns the Descender.inputactions asset)
    Editor/          Descender.Editor.asmdef; menu items, importers, inspectors
  Tests/EditMode/    Descender.Tests.EditMode.asmdef (data, logic, importer tests)
  Tests/PlayMode/    Descender.Tests.PlayMode.asmdef (scene smoke tests)
  Prefabs/           Player, Enemies/, Props/<region>/, UI/
  Data/              Enemies/*.asset, Bosses/*.asset, Regions/*.asset, dialogue/*.json
  Art/               Sprites/<character>/, Props/<region>/, Tiles/, Regions/, UI/, Fonts/
  Audio/             music/, sfx/
  Settings/          URP asset, 2D Renderer, Descender.inputactions, Materials/, Audio mixer
  Shaders/
```

A new file goes in the folder of the area it belongs to; make a new folder
only when an area has five or more files. Every file under `Assets/` has a
`.meta` beside it; commit the two together and never regenerate a `.meta`
of a file that already has one (its GUID is what scenes and prefabs point
at).

## Assemblies

Four `.asmdef`s, and no scripts outside them:

- `Descender.Runtime` (root namespace `Descender`): game code. References
  Input System, URP runtime, TextMeshPro, 2D Tilemap Extras, Newtonsoft.
- `Descender.Editor` (Editor platform only): tools and importers. May
  reference the runtime; the runtime never references it.
- `Descender.Tests.EditMode` and `Descender.Tests.PlayMode`: see
  `unity-testing-and-ci`.

Adding a package type to runtime code means adding its assembly to the
asmdef's `references`, not an `#if`. Keep `UnityEditor` usage in
`Assets/Scripts/Editor` or under `#if UNITY_EDITOR` for the rare
`[ExecuteAlways]` helper.

## Namespaces and names

Namespace follows the folder: `Descender.Player`, `Descender.World`,
`Descender.EditorTools.Importers` (the editor root is `EditorTools` so it
never collides with `UnityEditor`). One public type per file, file named
after the type. MonoBehaviours are `sealed` unless designed for inheritance.

## State that outlives a scene

The Godot autoloads became static classes with explicit `Reset()`:
`StoryFlags`, `Inventory`, `Skills`, `SaveManager`. They hold no Unity
objects, so they are testable in EditMode and survive scene loads. Anything
that needs a Unity object (audio, camera, HUD) is a MonoBehaviour in
`Main.unity`, which is loaded once; regions are loaded additively with
`SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive)` and unloaded
when the hero leaves. Do not add `DontDestroyOnLoad` singletons; put the
object in `Main.unity`.

A new game calls every static `Reset()` through `SaveManager.StartNewGame`;
when adding static state, add it there and to `SaveData`.

## Scenes vs prefabs

- A region is a scene: tilemaps, lights, props, rooms, doors, placements.
- Anything that appears more than once is a prefab (`Assets/Prefabs/`),
  placed in scenes as prefab instances; edit the prefab, not the instance,
  unless the difference is intentional and local.
- Enemy and region numbers are ScriptableObjects under `Assets/Data/`
  (`WalkerProfile`, `FlyerProfile`, `BossProfile`, `RegionLook`), generated
  from the Godot `.tres` files by `tools/unity/import_godot_assets.py`.
  Edit the `.asset` in the inspector; the importer does not overwrite them.

## Units

One Unity unit is one 16 px tile (sprites import at 16 PPU for level art,
64 PPU for painted props and backdrops, which were drawn at 4x). Tuning
constants in `PlayerTuning` and the profiles stay in level pixels, as in
Godot, and are divided by `PlayerTuning.PixelsPerUnit` where they meet a
Unity transform or Rigidbody2D. Y points up in Unity; the Godot numbers
with Y down (gravity, jump velocity) flip sign at the same place.

## Things that are not here

The web map editor and its Firestore bridge were dropped with the move;
nothing under `Assets/` talks to the network, and no replacement should be
added without a plan in `docs/`.
