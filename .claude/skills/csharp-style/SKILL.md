---
name: csharp-style
description: How C# is written in this repo so it passes the CSharpier check in CI and reads like the existing scripts: naming, fields and serialization, properties, null handling, Unity attributes, doc comments. Use whenever writing or editing a .cs file.
---

# C# style

CI runs `csharpier check Assets` (CSharpier 1.0.x, settings in
`.csharpierrc.yaml`: 120 columns, four spaces). Run it locally before
pushing:

```
dotnet tool install --global csharpier --version 1.0.3
csharpier format Assets      # rewrites
csharpier check Assets       # only reports
```

CSharpier owns layout (braces, wrapping, blank lines). What follows is the
part it does not decide. `.editorconfig` mirrors these for the IDE.

## Layout of a script

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// One paragraph on what this is, which asset configures it and which
    /// prefab or scene uses it. Every public type starts like this.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Walker : MonoBehaviour
    {
        public const float StepPixels = 4f;                 // constants first

        [SerializeField] private WalkerProfile profile;     // inspector fields, camelCase, private
        [SerializeField] private Transform eyes;

        private Rigidbody2D body;                           // runtime state
        private State state = State.Patrol;

        public event System.Action<Walker> Died;            // events, past tense

        public bool IsAlive => state != State.Dead;         // properties

        private void Awake() { ... }                        // Unity messages in call order:
        private void OnEnable() { ... }                     // Awake, OnEnable, Start, FixedUpdate,
        private void FixedUpdate() { ... }                  // Update, LateUpdate, OnDisable, OnDestroy

        public void Hit(int damage, Vector2 from) { ... }   // public methods

        private void Die() { ... }                          // private methods

        private enum State { Patrol, Chase, Dead }          // nested types last
    }
}
```

Block-scoped `namespace { }` everywhere, as above (CSharpier keeps either;
the repo uses one). System usings before Unity usings, sorted.

## Naming

- Types, methods, properties, events, constants and enum members:
  `PascalCase`. Private fields and locals: `camelCase`, no underscore
  prefix. Serialized private fields are still `camelCase` (Unity shows them
  nicified).
- Booleans read as a question: `IsAlive`, `canDash`, `wasGrounded`.
- Unity messages are `private` and never `public` or `virtual` unless a
  base class really needs it.
- Static data holders mirror the Godot autoloads: `StoryFlags.Set(...)`,
  `Inventory.Add(...)`. Name the method by what it does, not `Get`/`Set`
  prefixes on properties.

## Fields and serialization

- Inspector-visible data is `[SerializeField] private`, never `public`.
  Code that needs to read it gets a property.
- Renaming a serialized field needs `[FormerlySerializedAs("oldName")]`
  (in `UnityEngine.Serialization`) or every prefab loses the value.
- Tuning numbers live in a `ScriptableObject` (`PlayerTuning`,
  `WalkerProfile`) or a `const`, not in scattered `[SerializeField]`s on
  the behaviour. Use `[Range]`, `[Min]`, `[Tooltip]` on tuning fields.
- `[Header("...")]` groups a long inspector; `[field: SerializeField]` on
  an auto-property is fine when you want a read-only property with
  inspector data.
- Cache components in `Awake` with `GetComponent`; never call it in
  `Update`/`FixedUpdate`.

## Properties, var, expression bodies

- `var` when the type is on the right or obvious; the explicit type for
  numeric literals where the width matters (`float speed = 3f`, not
  `var speed = 3f` is also fine; what matters is `f` on float literals).
- Expression-bodied members for one-liners, blocks otherwise. No
  `=> { ... }` multi-statement lambdas as event handlers in
  MonoBehaviours; name the method so it can be unsubscribed.
- Prefer `readonly` on fields set only in the constructor or at
  declaration. Struct for small value types (`DialogueLine`, `LootDrop`).

## Null and Unity objects

- Unity objects: `if (target == null)` or `if (target)` are both correct
  (the overload sees destroyed objects). Do not use `?.` or `??` on
  `UnityEngine.Object`: they skip the lifetime check and keep a destroyed
  reference alive-looking.
- Plain C# objects: `?.`, `??`, `is null` are fine and preferred.
- `TryGetComponent` over `GetComponent` + null check.

## Collections and allocation

- Pre-size lists and reuse them in hot paths; `List<T>` over arrays when
  the size varies, arrays for fixed tables (`Items.Ordered`).
- No LINQ, `string.Format`, interpolation or `foreach` over an
  `IEnumerable` interface in `Update`/`FixedUpdate` paths of the hero,
  enemies, particles or streaming. Use it freely in editor code, tests,
  menus and one-shot setup.
- `Physics2D` queries use the non-allocating overloads with a cached
  `ContactFilter2D` and results array/list.

## Comments

- `///` XML summary on every public type, and on public members whose
  name does not say it all. One paragraph, present tense, says what and
  why; the Godot file it ports when behaviour should match it
  (`// Port of scripts/enemies/walker.gd`).
- Inline comments explain intent or a non-obvious number, not what the
  line does. Reference the pixel value when a unit value appears:
  `// 72 px jump height`.
- No commented-out code, no `TODO` without an owner and a line of context.

## Strings, enums, logging

- Ids that cross into data (item ids, flags, dialogue ids) are
  `string`s matched ordinally, as in the JSON. Enum for closed sets the
  code owns (`ItemSlot`, `Ability`, `Difficulty`).
- `Debug.Log` only in editor tools and setup; gameplay never logs per
  frame. `Debug.LogWarning` for data that is wrong but survivable,
  `Debug.LogError` for a bug.
- Use `StringComparison.Ordinal` for every `string.Compare`/`StartsWith`
  on ids, and `CultureInfo.InvariantCulture` when formatting numbers for
  data.
