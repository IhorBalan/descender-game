# Descender

A 2D metroidvania built in Unity 6 at the repository root (`Assets/`,
`Packages/`, `ProjectSettings/`), ported from the Godot project that still
sits beside it as reference. Read `docs/ARCHITECTURE.md` first, then the
skill under `.claude/skills/` for the area you touch. `REVIEW.md` governs the
automatic review of every pull request.

## Delivery pipeline
Follow `docs/ARCHITECTURE.md`. Standing approval from the project owner: agents may merge task pull requests into the integration branch without human review once pre-commit checks have passed, all GitHub checks are green and review findings are resolved.

The integration branch is `main`. Pull requests are squash merged. The
shared state of the pipeline (roadmap, backlog, decisions, how work is
verified, what to playtest) is `docs/delivery/STATE.md`. The pre-commit
checks are the pre-push hook in `tools/hooks/` (Unity EditMode and PlayMode
tests) and `csharpier check Assets`; they run on a developer machine, not in
the cloud, and are never skipped. Only a `v*` tag releases to players, and
that needs the project owner.

## Code review checklist

The Claude review in CI is the standard review. Check that:

* Every criterion is met, and the named test exercises that behavior
* No test was weakened, skipped or deleted, and no assertion is empty
* The change stays in scope and any difference from `Touches` is justified
* It follows `docs/ARCHITECTURE.md`, and is correct, readable and extensible
* Edge cases, null and lifecycle bugs, and integration issues are handled, with no hot-path performance problems
* Each new, moved or deleted asset has a matching `.meta` change, GUIDs are stable and no generated files are committed
* Renamed serialized fields use `FormerlySerializedAs`, and save changes include a tested migration
* Scene and prefab diffs contain only intended changes
* `Packages/manifest.json` adds nothing the project owner has not approved

Return `APPROVE` or `CHANGES_REQUIRED`. List blocking findings with file and
reason, separately from non-blocking notes. Open issues for debt worth
tracking. Block only on real defects, unmet criteria or rule violations.
