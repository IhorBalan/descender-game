# Project skills

Skills Claude Code loads automatically when working in this repo. Each
folder holds a `SKILL.md` whose frontmatter `description` says when it
applies. They distil the Unity 6.3 LTS manual, the URP 17, Input System 1.20,
2D Tilemap Extras 6 and Test Framework 1.6 package docs, and GameCI's docs
into how *this* project does things, so a change fits the code already here.

The Godot skills these replace were removed when the game moved to Unity;
the Godot files still in the repo are reference for the port and are not
touched by any skill.

| Skill | Use it for |
|---|---|
| `unity-project-structure` | Where things live under Assets/, asmdefs, namespaces, static state, scenes vs prefabs, what the Godot files are for |
| `csharp-style` | Writing C# that passes CSharpier and reads like the rest of the repo: fields, properties, serialization, naming |
| `character-movement-2d` | Rigidbody2D + collider movement, FixedUpdate vs Update, layers and masks, hitboxes, hit-stop and feel |
| `urp-2d-lighting-and-camera` | The 2D Renderer, Light2D, normal maps as secondary textures, Pixel Perfect Camera, bloom, parallax backdrops |
| `tilemaps-and-rule-tiles` | Tilemap and Tilemap Renderer, Rule Tiles, Tilemap Collider 2D + Composite, the region scenes |
| `sprites-and-animation` | Sprite import, slicing, Animation Clips and Animator, SpriteRenderer flips, tweens by coroutine |
| `input-system-and-ui` | Input System actions, rebinding and saving overrides, uGUI + TextMeshPro screens, pausing, prompts |
| `audio` | AudioSource pools, the Audio Mixer groups, music crossfades, looping |
| `saving-and-data` | Save slots, SettingsData, persistentDataPath, JSON with Newtonsoft, ScriptableObject profiles |
| `unity-testing-and-ci` | EditMode and PlayMode tests, what to test where, GameCI, the licence secrets, CSharpier |
| `shaders-and-particles` | Shader Graph for sprites, material property blocks, Particle System for ambience |
| `build-and-release` | Player settings, the release workflow, tags that release, running a build |
| `editor-tooling` | Menu items, importers, AssetDatabase, custom inspectors, the Godot-to-Unity import scripts |
| `performance-2d` | Profiler, draw calls and batching, physics cost, allocations, what not to optimise |
