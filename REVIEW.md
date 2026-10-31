# Review instructions

These rules apply to the automatic Claude review on every pull request
(`.github/workflows/claude-code-review.yml`). Keep this file short: every line
here changes what the reviewer posts.

The game is a Unity 6 project (C#, URP 2D Renderer, Input System, Tilemaps).
The project's own conventions are the skills under `.claude/skills/`; a
change that breaks one of them is a nit unless it also breaks the game.

## Focus areas

Look for problems a player or the next contributor would actually hit:

- Gameplay logic: the hero's and enemies' state machines, timers, input
  handling, damage, death and respawn, checkpoints, saves, level flow.
- Physics and collisions: layers and masks, trigger vs collider, `Update` vs
  `FixedUpdate` (movement and physics queries belong in `FixedUpdate`; input
  is read in `Update` and buffered), `Rigidbody2D` interpolation, anything
  that can make the hero stuck, fall through a ledge, or take double damage.
- Lifetime and references: `null` after `Destroy`, missing `[SerializeField]`
  references, `GetComponent` or `Find` every frame, coroutines left running
  on a destroyed object, events subscribed and never unsubscribed, static
  state (`StoryFlags`, `Inventory`, `Skills`) not reset on a new game.
- Serialized assets: a `.unity`, `.prefab`, `.asset` or `.meta` change that
  drops a GUID, a `.meta` missing for a new file, a renamed field that loses
  its serialized value (no `[FormerlySerializedAs]`), a script GUID reference
  that no longer resolves.
- Data: `Assets/Data/**` and the dialogue JSON, where every id, flag and
  `then` must resolve (the EditMode data tests check this; a change that
  would make them pass while broken is blocking).
- Allocation in hot paths: LINQ, string concatenation or `new` lists every
  frame in the hero, enemies, tilemap streaming or particles.
- CI and tests: workflow or `Assets/Tests/` changes that would pass while
  broken; tests that depend on asset import order or on the editor's state.

## Severity

- **Blocking**: a bug that breaks gameplay, crashes, corrupts a scene,
  prefab or asset, breaks a save, or makes CI pass when it shouldn't. Explain
  the concrete path to the failure.
- **Nit**: everything else (style, naming, duplication, refactors,
  micro-performance). Report at most **3** nits per review. If there are more,
  say "plus N smaller items" in the summary instead of listing them.

Don't report per-frame polling of input, missing XML doc comments or
duplicated code as blocking at this project's scale. Don't report anything
you can't tie to a specific line in the diff.

## Do not review

- `Assets/Art/**`, `Assets/Audio/**` and every `*.meta` file except for the
  GUID and secondary-texture checks above
- `Library/`, `ProjectSettings/*.asset` churn Unity writes on its own
- `docs/screenshots/**`, `art/**` and other generated placeholder art
- Formatting and whitespace (CSharpier checks it in CI)

## Follow-up reviews

The first review of a PR (opened, reopened or marked ready) covers the whole
diff. Every later review, triggered by a new push, is a follow-up:

- Look only at the commits pushed since the last review.
- Report **blocking** findings only. No new nits.
- Never repeat a point from an earlier review, including ones the author
  fixed, replied to, or deferred to a follow-up.
- If there is nothing blocking, say so in one line.
