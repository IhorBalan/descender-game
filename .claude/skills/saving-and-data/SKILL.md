---
name: saving-and-data
description: Saving and data in this project: the three save slots as JSON under Application.persistentDataPath/saves, the SaveData shape kept compatible with the Godot saves, SettingsData and its JSON file, Newtonsoft for JSON, static state classes with Reset, ScriptableObject profiles under Assets/Data, and the dialogue JSON. Use when touching saves, settings, items, flags, profiles or data files.
---

# Saving and data

## Where files go

`Application.persistentDataPath` (`~/.config/unity3d/Descender/Descender`
on Linux, `%USERPROFILE%\AppData\LocalLow\Descender\Descender` on Windows):

- `saves/slot_1.json` .. `slot_3.json` by `SaveSlots`
- `settings.json` by `SettingsData.Save`

Tests use `SaveManager.UseDirectory(tempDir)` and `SettingsData.Load(path)`
so nothing touches the real folder. Never write anywhere else; never use
`PlayerPrefs` (it is a registry on Windows and has no slots).

## Save shape

`SaveData.ToJson()` writes version 1 with the same keys as the Godot game
(`version, slot, difficulty, hero.{position, region, hearts, flasks},
abilities, flags, inventory, skills, checkpoint, play_time, saved_at`),
so saves from the Godot build load. `SaveData.FromJson` reads missing keys
as defaults and never throws on a well-formed file; a malformed file is
reported through `SaveSlots.Warning` and treated as empty. A new field:

1. Add to `SaveData`, with a default.
2. Write it in `ToJson`, read it with a default in `FromJson`.
3. Add a case to `SaveTests` that loads a version-1 file without the key.
4. Bump `Version` only when an old file can no longer be read correctly.

Static state (`StoryFlags`, `Inventory`, `Skills`) serialises itself
(`ToJson`/`FromJson` on each) and `SaveManager.Write` composes them. All
JSON goes through Newtonsoft (`JObject`, `JsonConvert` with
`Formatting.Indented`), not `JsonUtility`, which cannot do dictionaries.

## Settings

`SettingsData` is a `[Serializable]` class with public fields, clamped by
`Clamped()` on load; `Settings.Apply()` pushes it to the mixer, window
(`Screen.SetResolution`, `Screen.fullScreenMode`), `QualitySettings.vSyncCount`,
input binding overrides and the HUD. Saved on every change (small file).

## Static state

A static class per Godot autoload, no Unity objects inside, a `Reset()`
each, `SaveManager.StartNewGame` calls all of them. They raise plain C#
events (`StoryFlags.Changed`) that MonoBehaviours subscribe to in
`OnEnable` and unsubscribe in `OnDisable`. Tests call `Reset()` in
`[SetUp]`.

## Data tables

- Items, shops, quests, skills: code tables (`Items.Ordered`,
  `Shops.List`...) ported from the Godot scripts; ids are strings.
- Enemy and boss numbers: `WalkerProfile`, `FlyerProfile`, `BossProfile`
  ScriptableObjects in `Assets/Data/Enemies|Bosses`, referenced by the
  prefab's `[SerializeField] profile`. `LootTable` is a `[Serializable]`
  struct inside them with `Roll(chance, between)` taking the random
  functions so tests are deterministic.
- Region looks: `RegionLook` in `Assets/Data/Regions`.
- Dialogue: `Assets/Data/dialogue/world_1.json`, read by `DialogueData`;
  flags, `then` targets, items and quests are validated by
  `DialogueFileTests` in EditMode.

`tools/unity/import_godot_assets.py` generated the `.asset` files from the
`.tres` ones; they are now the source of truth and are edited in the
inspector. The script never overwrites an existing `.asset`.

## Creating a profile

`Create > Descender > Walker Profile` (`[CreateAssetMenu]` on the class),
name it after the enemy id, fill the numbers in level pixels, point the
prefab at it. Validation that would have been a Godot `_get_configuration_
warnings` goes in `OnValidate` (clamp, warn) and in an EditMode test that
loads every profile with `AssetDatabase.FindAssets("t:WalkerProfile")`.
