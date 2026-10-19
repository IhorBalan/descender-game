# Review instructions

These rules apply to the automatic Claude review on every pull request
(`.github/workflows/claude-code-review.yml`). Keep this file short: every line
here changes what the reviewer posts.

## Focus areas

Look for problems a player or the next contributor would actually hit:

- Gameplay logic: player and enemy state machines, timers, input handling,
  damage, death and respawn, checkpoints, level flow.
- Physics and collisions: layers and masks, `Area2D` overlaps, `_process` vs
  `_physics_process`, anything that can make the hero stuck, fall through, or
  take double damage.
- Signals and node wiring: missing connections, wrong node paths, nodes used
  after `queue_free()`.
- Scenes and resources: broken `ext_resource` paths, missing nodes, uid
  mismatches, renamed files that something still references.
- Crashes: null access, unchecked dictionary or array lookups, bad casts.
- CI and tests: workflows or `tests/` changes that would pass while broken.

## Severity

- **Blocking**: a bug that breaks gameplay, crashes, corrupts a scene or
  resource, or makes CI pass when it shouldn't. Explain the concrete path to
  the failure.
- **Nit**: everything else (style, naming, typing, duplication, refactors,
  micro-performance). Report at most **3** nits per review. If there are more,
  say "plus N smaller items" in the summary instead of listing them.

Don't report per-frame polling, missing type hints or duplicated code as
blocking at this project's scale. Don't report anything you can't tie to a
specific line in the diff.

## Do not review

- `assets/**` image files and every `*.import` and `*.uid` file (generated
  by Godot)
- `docs/screenshots/**`
- `art/**` and other generated placeholder art
- Formatting and whitespace

## Follow-up reviews

The first review of a PR (opened, reopened or marked ready) covers the whole
diff. Every later review, triggered by a new push, is a follow-up:

- Look only at the commits pushed since the last review.
- Report **blocking** findings only. No new nits.
- Never repeat a point from an earlier review, including ones the author
  fixed, replied to, or deferred to a follow-up.
- If there is nothing blocking, say so in one line.
