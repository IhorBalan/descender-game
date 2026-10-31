---
name: input-system-and-ui
description: Input through the Input System (Descender.inputactions, the generated DescenderInput wrapper, Player and UI maps, rebinding with PerformInteractiveRebinding and saving overrides as JSON, button prompt glyphs), and screens with uGUI and TextMeshPro (HUD, menus, dialogue box, pausing). Use when touching input, bindings, menus, HUD, dialogue UI, pause, or prompts.
---

# Input System and UI

## Actions

`Assets/Settings/Descender.inputactions` has two maps, `Player` (MoveLeft,
MoveRight, MoveDown, Jump, Attack, Dash, Cast, Heal, Interact, Pause,
Restart, Console) and `UI` (Navigate, Submit, Cancel, PageLeft,
PageRight), with `Keyboard` and `Gamepad` control schemes. No C# wrapper is
generated (`generateWrapperCode: 0`); actions are found by name. Action
names match the Godot InputMap names in PascalCase;
`SettingsData.Rebindable` lists the ones a player can rebind.

One `InputRouter` in `Main.unity` holds the asset (`[SerializeField]
InputActionAsset`), looks the actions up once in `Awake` with
`FindActionMap("Player", true).FindAction("Jump", true)`, enables `Player`
in play and `UI` in menus (never both), and exposes typed accessors:

```csharp
public bool JumpPressed => jump.WasPressedThisFrame();
public bool JumpHeld => jump.IsPressed();
public float MoveX => (moveRight.IsPressed() ? 1f : 0f) - (moveLeft.IsPressed() ? 1f : 0f);
```

Gameplay reads these in `Update` and buffers for `FixedUpdate`
(`character-movement-2d`). Project Settings > Input System Package:
`Update Mode: Process Events In Dynamic Update`, `Active Input Handling:
Input System Package (New)` only (no legacy `Input.GetKey`).

## Rebinding

The Controls screen calls:

```csharp
action.Disable();
rebind = action.PerformInteractiveRebinding(bindingIndex)
    .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta")
    .WithCancelingThrough("<Keyboard>/escape")
    .OnMatchWaitForAnother(0.1f)
    .OnComplete(op => { op.Dispose(); action.Enable(); Save(); })
    .OnCancel(op => { op.Dispose(); action.Enable(); })
    .Start();
```

Pick the binding index by control scheme (`action.bindings[i].groups`
contains "Keyboard" or "Gamepad") so each column rebinds its own device.
After a change `SettingsData.bindings = actions.SaveBindingOverridesAsJson()`
and `SettingsData.Save()`; on boot `actions.LoadBindingOverridesFromJson(settings.bindings)`.
"Reset to defaults" is `actions.RemoveAllBindingOverrides()`.
Display names come from `action.GetBindingDisplayString(index, out device, out controlPath)`.

## Prompts

`Prompt` (a `TMP_Text` + `Image`) shows a glyph for an action: the
`PromptGlyphs` ScriptableObject maps control paths (`buttonSouth`,
`leftShoulder`, `space`...) to sprites from `Assets/Art/UI/Prompts`, per
`SettingsData.prompts` layout (Auto, Xbox, PlayStation, Nintendo,
Keyboard). Auto uses `Gamepad.current?.name` (DualShock/Switch Pro) and
falls back to keyboard when `InputRouter.LastDevice` is a keyboard;
`InputSystem.onActionChange` updates `LastDevice`.

## UI stack

uGUI with TextMeshPro, one `Canvas` per screen under `UI` in
`Main.unity`: `Screen Space - Camera` rendering through the pixel-perfect
camera, `Canvas Scaler: Scale With Screen Size`, reference 320 x 180,
`Pixel Perfect` on. Fonts: `Assets/Art/Fonts/*.ttf` converted to TMP font
assets with `Sampling Point Size` equal to the pixel font size, `Padding
1`, `Render Mode: SMOOTH` off (`RASTER`), `Atlas: 512`. `TMP_Text` with
`Font Size` the pixel size, no auto size.

Screens are prefabs (`Assets/Prefabs/UI/`) with a `Screen` MonoBehaviour
(`Open()`, `Close()`, `OnCancel()`), pushed and popped by `UiStack`,
which also swaps the input map and sets `Time.timeScale = 0` while a
pausing screen is open. Navigation: Unity `Selectable` navigation set to
Explicit on the menu buttons; `UiStack` calls
`EventSystem.current.SetSelectedGameObject(first)` on open. The
`EventSystem` uses `InputSystemUIInputModule` bound to the `UI` map.

HUD elements: hearts, flasks, light, charges are prefabs updated by events
(`Player.HealthChanged`), never polled in `Update`. Dialogue box types
`SettingsData.CharsPerSecond(textSpeed)` characters a second through
`TMP_Text.maxVisibleCharacters` with a coroutine; choices are buttons made
from a prefab.

## Pause and focus

`Pause` toggles on the Pause action from the `Player` map, sets
`Time.timeScale = 0`, `AudioListener.pause = true` (music keeps playing on
its own `ignoreListenerPause` source), opens the pause screen. Losing
window focus (`OnApplicationFocus(false)`) pauses in a build, not in the
editor.

## Text and localisation

All player-visible strings are in code constants or data JSON for now;
there is no localisation table. Keep them out of scenes so they can be
collected later.
