# Architecture

How the Unity port of Descender is put together, and the conventions every
change follows so the game reads as one codebase. The detailed, per-topic
rules live in the project skills under `.claude/skills/`; this document is
the map, and names the skill to read before touching an area.

## The game

A side-view 2D metroidvania: Ansel, a man with half-finished angel powers,
fights his way down from Heaven through Purgatory and the nine circles of
Hell. The world is one connected vertical map rendered at 320 x 180 and
scaled by whole numbers. The design lives in `docs/design/` (economy,
enemies, progression, upgrades, the Paradise rebuild) and the story in
`docs/story/`.

## Repository

The Unity 6 project (`Assets/`, `Packages/`, `ProjectSettings/`) sits at the
repository root and is the whole game. `tools/` holds what works on it from
outside the editor: `tools/unity/build_prefabs.py` (the hero, enemy and
pickup prefabs and their small data assets as Unity YAML), `unity_meta.py`
(the shared `.meta` writers and sprite slicing), `check_tools.py` (the CI
guard that every tool imports and names no Godot path), the art and sound
generators (`gen_*.py`, `menu_textures.py`, `*_sprites/`, `hero_*/`,
`paradise_art/`, `ai_art/`) and the pre-push hook (`tools/hooks/`). The game
was ported from a Godot 4 prototype; that project is in history at the tag
`godot-last` (`git show godot-last:<path>`) and nothing in the tree reads
from it (`RepositoryTests` keeps it so). Rules:

- Never add a top-level folder whose name differs from a Unity one only by
  case (`assets/` and `Assets/` are one folder on a case-insensitive disk).
- The `tools/unity/` scripts write `.meta` files with GUIDs derived from the
  path, so every clone agrees on the ids; never regenerate a `.meta` that
  already exists. `build_prefabs.py` is deterministic: CI checks that its
  output matches the committed assets.

## Layout

```
Assets/
  Scenes/            Main.unity (persistent systems), Region_<key>.unity per region,
                     Paradise/ (the rebuilt Paradise: ParadiseMain + zone scenes)
  Scripts/
    Runtime/         Descender.Runtime.asmdef, one folder per area (below)
    Editor/          Descender.Editor.asmdef: Setup/, Importers/, World/, Paradise/, Build/
  Tests/EditMode/    Descender.Tests.EditMode.asmdef
  Tests/PlayMode/    Descender.Tests.PlayMode.asmdef
  Prefabs/           Player.prefab, Enemies/, Pickups/, Props/, Paradise/
  Data/              Enemies/*.asset, Bosses/*.asset, Regions/*.asset, World.asset,
                     PlayerTuning.asset, Placements*.asset, SfxLibrary.asset, dialogue/*.json
  Art/, Audio/       Imported sprites, tiles, props, backdrops, UI, fonts; music and sfx
  Settings/          URP asset and 2D Renderer, Descender.inputactions, Materials/, Post/, MenuTheme
  Shaders/           Sprite flash and glow, Paradise shaders
  Resources/         UiTheme.asset (the one Resources.Load)
```

A new file goes in the folder of the area it belongs to; make a new folder
only when an area has five or more files. Every file under `Assets/` has a
`.meta` beside it: commit the two together, give a new asset a new GUID,
never change an existing GUID. Skill: `unity-project-structure`.

## Assemblies and namespaces

Four assembly definitions and no scripts outside them:

| Assembly | Root namespace | Role |
| --- | --- | --- |
| `Descender.Runtime` | `Descender` | All game code. References Input System, URP, TextMeshPro, 2D Tilemap Extras, uGUI, Newtonsoft. |
| `Descender.Editor` | `Descender.EditorTools` | Menu items (`Descender > ...`), importers, setup, Paradise build tools. May reference the runtime; the runtime never references it. |
| `Descender.Tests.EditMode` | `Descender.Tests` | Data, logic, save and importer tests. Editor only. |
| `Descender.Tests.PlayMode` | `Descender.Tests` | Scene smoke tests. |

Namespace follows the folder (`Descender.Player`, `Descender.World`,
`Descender.EditorTools.Importers`). One public type per file, named after
the type. MonoBehaviours are `sealed` unless designed for inheritance.
`UnityEditor` is used only in the Editor assembly, or under `#if
UNITY_EDITOR` for a rare `[ExecuteAlways]` helper. A new package type in
runtime code means adding its assembly to the asmdef `references`, not an
`#if`.

## Runtime areas

| Folder | What lives there |
| --- | --- |
| `Game/` | `GameFlow`: the phases (title, playing, paused) and the moves between them; spawns the hero, owns the EventSystem. `DevStart` for "Play from here" and for the lab (in a build, `Descender.exe -lab <station>` opens a station). `GameFlow` also makes the dev console when the scene has none. |
| `Player/` | `PlayerController` (run, jump, dash, sword, heal, abilities), `PlayerSprite`, `PlayerTuning` (ScriptableObject of movement numbers), `IMovingGround` (ground that moves, such as a `Mover`: the hero standing on it is carried at its speed, keeps a share of it when he leaves, and is hurt and put aside when one crushes him), `LiveTuning` (the working copy the dev console's `tune` changes while playing, its fields found by reflection, and the abilities `ability` forces; neither reaches a save), `Power`, `Upgrades`. The ledge grab (`PlayerState.Hang`) is a trial behind a switch: off unless `PlayerTuning.ledgeGrab` or a lab room (`Station.ledgeGrab`, through `GrabsLedges`) turns it on. |
| `Enemies/` | `Walker`, `Flyer`, `Boss`, `Hanger`, `LanternFruit` (a glowing fruit under a branch that shivers, falls and bursts on the hero beneath, pops harmlessly when struck, and grows back; drawn by `LanternFruitView` every rendered frame (a sway on its stem, a shiver and swell as it ripens, a stretch as it falls, cross-faded frames; its burst, `LanternFruitBurst`, is a flash, a ground shockwave, a column of light, motes, peel shards and seeds that land, and a flare with an ember glow), painted from a `LanternFruitLook` (`Assets/Data/Enemies/LanternFruitLook.asset`, sprites under `Assets/Art/Paradise/Enemies/LanternFruit/`) that `GameFlow` hands the lab, else a placeholder circle; a small light either way; in the lab's `fruit` room), their `*Profile` ScriptableObjects, `EnemyPhysics` (shared non-allocating queries), `Kills`, death and health bar visuals. |
| `Combat/` | `IHittable`, `Strike` (one hit as the attacker sends it: sword, slam or bolt, its damage, where from and its push; `Strikes.Find` and `Strikes.Deliver` are how every attack finds its target and hands the strike over, whole to an `IStruck`, as damage to a plain `IHittable`), `Dummy` (takes every hit, never dies, and shows the last strike, the count and the damage a second), `Layers` (collision layers and masks looked up once), `LightBolt`. |
| `Pickups/` | `Pickup` base and the things lying in the world: checkpoints, hazards, feathers, upgrades, power-ups, loot and currency. |
| `World/` | Region streaming (`WorldAtlas`, `WorldStreamer`, `RegionRoot`), tiles (`RegionTileSet`, `RegionTiles`, `RegionTileData`), look and atmosphere (`RegionLook`, `BackdropLayer`, `RegionParticles`, `RegionFringes`), placements (`WorldPlacement`, `PlacementCatalog`, `IPlacementReceiver`), rooms, doors, light barriers. `World/Paradise/` holds the rebuilt Paradise's zone pieces. |
| `Story/` | `Story` (flags), `DialogueData` (the JSON model), `Quests`, `StoryMode` (the story is parked: its triggers, talking NPCs and relics do not spawn; see `docs/story/PARKED.md`). |
| `Economy/` | `Items`, `Inventory`, `Shops`, `Skills`, `LootTable`. |
| `Save/` | `SaveData` (versioned JSON, still loads the Godot version 1 file), `SaveSlots`, `SaveManager`, `SettingsData`, `SettingsApplier`. |
| `UI/` | HUD, title, pause, camp, pack, shop, skills and settings screens, dialogue box, prompts, dev console; `UiKit`/`MenuKit` build uGUI from code in the stone theme. |
| `Input/` | `InputRouter`: the one owner of `Descender.inputactions`. |
| `Audio/` | `Music` (one crossfaded loop), `Sfx` (pooled sources), `SfxLibrary`. |
| `Camera/` | `GameCamera`: look-ahead, room clamp, whole-pixel shake, and the blend between `CameraSettings` (framing, zoom, look-ahead, held axes). `CameraZone`: an area that gives the camera its own settings while the hero stands in it; the smallest zone wins. |
| `Fx/` | `Feedback` (hit-stop, shake, rumble, particles), `HitFlash`, `Tween`, `Shapes`, `Shape` (the placeholder: a labelled square, circle, diamond or triangle for anything without art), `SpriteAnimator`. |
| `Lab/` | The mechanics lab, built from data with no scene and no art: `LabLayout` (the stations and their pieces, sized from `PlayerTuning`), `LabBuilder` (colliders and `Shape`s), `LabRoot` (entering a station; a station that changes in play is built again), `LabPad`. Opened by `GameFlow.StartLab`: the console's `lab`, or Descender > Lab > Play. |
| `Triggers/` | Items, switches, locks and doors as data: `TriggerZone` (fires on enter, leave, interact or hit, once or every time, when its `TriggerNeeds` are met) runs a list of `TriggerAction`s (`TriggerActions.cs`: one small serializable class each, made in one line by `Do`). `Door`, `Mover`, `Crumble` and `Blinker` (platforms that do not stay, both a `TimedPlatform`: one gives way once stood on and comes back, one is solid and gone by the clock; neither comes back on top of the hero, and neither is safe ground), `Climbable` (an area with no collider that the hero takes hold of with up or down and climbs with no gravity; its top is where he steps off, so a ledge or a floor's edge goes there), `Water` (an area with no collider he swims in: with the middle of his body in it he sinks slowly, moves and dashes slower and jump is a stroke; with his head above its top jump is a full jump out), `ForceZone` (an area whose wind, a speed, is added to the hero's own while he is in it: a headwind, a lift, a downdraught, steady or in gusts; the hero keeps the wind's part of his speed apart from his own, and what a wall or the floor takes is taken from the wind's part first), `Keys` (held names that are not pack items), `Notice` (a line on the HUD). A new action is one class; the inspector (`Editor/Triggers`) lists it by itself. |

## Scenes, streaming and state

- `Main.unity` is loaded once and holds everything that outlives a region:
  camera, HUD and menus, audio, `InputRouter`, `GameFlow`, `WorldStreamer`,
  the fade. There are no `DontDestroyOnLoad` singletons; an object that must
  persist goes in `Main.unity`. `Descender > Setup > Main scene` rebuilds
  its wiring.
- The world is one grid of bands (`Assets/Data/World.asset`, a
  `WorldAtlas`). `WorldStreamer` keeps the hero's band and its neighbours
  loaded as additive scenes and unloads the rest. Every region scene must
  be in the build's scene list; an EditMode test checks `Main` is first.
- A region scene holds its `RegionRoot`, tilemaps built at load from its
  `RegionTileSet`, backdrop layers, rooms, doors and `WorldPlacement`s.
  Placements spawn prefabs through the `PlacementCatalog` by type and
  character, unless a story flag or the kill tracker says the thing is gone.
- State that outlives a scene is a static class with an explicit `Reset()`
  and no Unity objects, so it is testable in EditMode: `Story`,
  `Inventory`, `Skills`, `Kills`, `Checkpoint`, `SaveManager`.
  `SaveManager.StartNewGame` resets all of them; new static state is added
  there and to `SaveData`.
- Anything that appears more than once is a prefab under `Assets/Prefabs/`.
  Tuning numbers are ScriptableObjects under `Assets/Data/` or constants,
  never scattered serialized fields.

## Units and numbers

One Unity unit is one 16 px tile. Level art imports at 16 PPU, painted props
and backdrops at 64 PPU. Tuning constants (`PlayerTuning`, the profiles)
stay in level pixels and seconds as in Godot and are divided by
`PlayerTuning.PixelsPerUnit` where they meet a transform or Rigidbody2D.
Y points up, so Godot numbers with Y down flip sign at that point.

## Physics and input

Movement and physics queries run in `FixedUpdate`; input is read in `Update`
through `InputRouter`'s typed accessors and buffered. Collision layers come
from `Layers`, never a magic int. `Physics2D` queries use the non-allocating
overloads with a cached `ContactFilter2D`. Menus switch the input to the UI
map through `UiStack`. Skills: `character-movement-2d`,
`input-system-and-ui`.

## Rendering

URP with the 2D Renderer, `Color Space: Gamma`, a Pixel Perfect Camera at
320 x 180 (not in the rebuilt Paradise, whose painted art is framed by the
camera's own size so camera zones zoom smoothly), one global `Light2D` tinted per region, normal maps as sprite
secondary textures, bloom through the Volume framework in the rebuilt
Paradise. Skill: `urp-2d-lighting-and-camera`, `shaders-and-particles`.

## Data

- Enemy, boss and region numbers are ScriptableObjects under
  `Assets/Data/`; edit the `.asset` in the inspector.
- Dialogue is JSON under `Assets/Data/dialogue/`: entries keyed by id with
  `lines`, the flags they `set`, and `unless` guards. Every id, flag and
  reference must resolve; `DialogueFileTests` checks the real file.
- Saves are JSON files `slot_<n>.json` under `persistentDataPath` (the
  folder is injected so tests use a temporary one). `SaveData.Version` is
  1; a change to the save shape bumps it and adds a tested migration that
  still loads the previous version.
- Ids that cross into data are ordinal `string`s; closed sets the code owns
  are enums (`Ability`, `Difficulty`, `ItemSlot`).

## Code style

CSharpier (1.0.x, `.csharpierrc.yaml`: 120 columns, four spaces) owns
layout and runs in CI. Beyond that: block-scoped namespaces, System usings
before Unity, `[SerializeField] private` camelCase fields with a property
for readers, `[FormerlySerializedAs]` on every renamed serialized field,
`///` summary on every public type, `TryGetComponent` over `GetComponent`
plus null check, no `?.` or `??` on `UnityEngine.Object`. No LINQ, string
building or `new` collections in the per-frame paths of the hero, enemies,
particles or streaming. Skill: `csharp-style`, `performance-2d`.

## Tests and verification

- EditMode (`Assets/Tests/EditMode/`): data, static state, save round
  trips, importers, anything without a scene. Fast; most tests live here.
- PlayMode (`Assets/Tests/PlayMode/`): a floor made in code, the hero and
  an imp stepped through `FixedUpdate`; few and broad. Tests reset statics
  in `[SetUp]` and never depend on order.
- Both run on a developer machine from the pre-push hook
  (`tools/hooks/pre-push`, installed with `git config core.hooksPath
  tools/hooks`), which needs the editor named in
  `ProjectSettings/ProjectVersion.txt`. CI runs only `csharpier check
  Assets` and the Claude review (`REVIEW.md`); it has no Unity licence.
- A change that was not run in Unity says so in its pull request.

Skill: `unity-testing-and-ci`. Delivery rules for agents: `CLAUDE.md`.

## Editor tooling

Everything under `Descender >` in the menu bar: project bootstrap, main
scene setup, UI theme, placement catalog, the importers, "Play from Main
scene", "Play from here", region tile preview, and the Paradise greybox
and art builders. Tools are idempotent and never overwrite a hand-edited
asset. Skill: `editor-tooling`.

## Build and release

`release.yml` builds Linux and Windows through GameCI and publishes a
GitHub release only when a `v*` tag is pushed. Merging to `main` deploys
nothing. Tagging is a release to players and needs the project owner.
Skill: `build-and-release`.
