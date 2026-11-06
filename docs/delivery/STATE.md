# Delivery state

The shared state of the delivery pipeline. Project memory is off for this
project, so the coordinator and the threads keep it here. The coordinator
owns `roadmap`, `backlog`, `decisions` and `playtest`; threads report
changes to it rather than editing those sections directly. A thread that
changes how work is verified updates `verification` in its own pull request.

Task statuses: `proposed`, `approved`, `rejected`, `in progress`,
`in review`, `merging`, `done`, `parked`, `needs local`, `waiting on me`.

## Roadmap

_Empty. The first Planning thread drafts it from `docs/design/`,
`docs/story/` and the code._

## Backlog

| Task | Title | Status | Priority | Depends on | Attempts | Thread | PR |
| --- | --- | --- | --- | --- | --- | --- | --- |

## Decisions

### D-0001: `main` is the integration branch

- **Decided**: task pull requests merge into `main`, the default branch.
  Merges are squash merges (the repository allows only squash).
- **Why**: `main` has no branch protection or rulesets and nothing deploys
  from it. The only deployment is `release.yml`, which runs on a `v*` tag,
  so merging to `main` never reaches players and the owner's standing
  approval covers it. A separate integration branch would add a second
  merge with no gate behind it.
- **Alternative**: a `develop` branch merged into `main` per milestone.
  Rejected as a second step that verifies nothing new.
- **Undo**: create the integration branch from `main`, point task branches
  at it in the Implementation stage, and change this entry.

### D-0002: verification runs on the owner's Mac through Remote Control

- **Decided**: see `verification`. Cloud threads cannot run the Unity tests
  or, with the current network policy, the CSharpier check; the owner chose
  Remote Control sessions on his machine over widening the cloud network.
- **Alternative**: a cloud environment allowing the .NET and NuGet hosts
  so threads could at least run the format check. Declined by the owner.
- **Undo**: create that environment and move the format check back into
  cloud threads; the Unity tests still need a machine with the editor.

## Verification

What a task must pass before it merges, and how a thread runs it.

### The checks

| Check | Where it runs | Needs Unity | Command |
| --- | --- | --- | --- |
| EditMode tests (76 on `main` at d49d0d6) | pre-push hook, developer machine | yes | `tools/hooks/pre-push` (or `Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/pre-push-EditMode.xml -logFile Logs/pre-push-EditMode.log`) |
| PlayMode tests (6 on `main` at d49d0d6) | pre-push hook, developer machine | yes, with a graphics device (no `-nographics`) | same with `-testPlatform PlayMode` |
| C# format | GitHub Actions `Tests / C# format` on every PR and push to `main` | no | `csharpier check Assets` (CSharpier 1.0.3, needs the .NET SDK) |
| Claude review | GitHub Actions `Claude Code Review / review` on every non-draft PR | no | governed by `REVIEW.md` |

The pre-push hook is installed per clone with `git config core.hooksPath
tools/hooks`. It finds the editor named in `ProjectSettings/ProjectVersion.txt`
(6000.6.8f1) under the Unity Hub folder, or at `UNITY_EDITOR`. It refuses to
run while the same project folder is open in the editor (`Temp/UnityLockfile`).
Never skip it with `SKIP_UNITY_TESTS=1` or `--no-verify`.

Merges do not deploy: only a `v*` tag releases, and tagging needs the owner.

### Where threads run them

- **Cloud threads cannot verify.** The cloud container has no Unity editor
  and its network policy blocks the .NET SDK and NuGet hosts, so neither the
  Unity tests nor CSharpier run there (checked 2026-11-06). A cloud thread
  may still write code and docs, but it must say in the pull request that
  the changes were not run in Unity and must not push without the hook
  having run somewhere.
- **Implementation threads run as Remote Control sessions on the owner's
  Mac**, in `/Users/ihorbalanpersonal/Documents/Projects/descender-game`,
  where Unity 6000.6.8f1 is installed and the pre-push hook runs fully.
  Install CSharpier there once (`dotnet tool install --global csharpier
  --version 1.0.3`) and run `csharpier check Assets` before pushing; CI
  repeats it.
- **Pull request description** includes the hook's summary lines
  (`pre-push: EditMode passed (N tests).` and `pre-push: PlayMode passed
  (N tests).`) and the CSharpier result, so the Claude review can check them.

### Running several threads on one machine

The checkout at `.../descender-game` is the owner's working copy and may be
open in the Unity editor. Threads do not work in it directly:

1. **One git worktree per task**, in a sibling folder:
   `git -C /Users/ihorbalanpersonal/Documents/Projects/descender-game worktree add ../descender-task-T-XXXX -b task/T-XXXX origin/main`.
   A worktree is a separate Unity project folder with its own `Library/`,
   `Temp/` and `Logs/` (all gitignored), so it has its own lock file and the
   editor being open on the main checkout does not block it. Remove it when
   the PR is merged: `git worktree remove ../descender-task-T-XXXX`.
2. **The first Unity run in a new worktree imports the whole project**
   (several minutes, a few GB in `Library/`). Later runs take about 20
   seconds. Reuse a worktree for a task's fix rounds rather than recreating
   it.
3. **Unity batchmode instances can run concurrently on different project
   folders**, but each is heavy on CPU and memory. Run at most one Unity
   test run at a time on the machine: take a lock before the hook, for
   example `mkdir /tmp/descender-unity.lock` and remove it after, or simply
   push one task at a time. Two instances must never open the same folder.
4. **Shared files**: two tasks must not both touch the same scene, prefab,
   ScriptableObject, project setting or `Packages/manifest.json`; the
   coordinator sequences those tasks.
5. **Hook installation is per checkout**, not per worktree: `core.hooksPath`
   is in the shared `.git/config`, so setting it once in the main checkout
   covers every worktree. Check with `git config core.hooksPath` before the
   first push from a worktree.

## Playtest

_Nothing to play yet from the pipeline. The current build is `main` at
d49d0d6 (PR #136)._
