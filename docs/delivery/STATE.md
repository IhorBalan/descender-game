# Delivery state

The shared state of the delivery pipeline. Project memory is off for this
project, so the coordinator and the threads keep it here. The coordinator
owns `roadmap`, `backlog`, `decisions` and `playtest`; threads report
changes to it rather than editing those sections directly. A thread that
changes how work is verified updates `verification` in its own pull request.

Task statuses: `proposed`, `approved`, `rejected`, `in progress`,
`in review`, `merging`, `done`, `parked`, `needs local`, `waiting on me`.

## Roadmap

Drafted 2026-11-06 from `main` after PR #136, `docs/design/paradise.md`, the
story script, the enemy, economy, upgrades and progression designs, the
October plans and the 33 open GitHub issues. The full draft with its
reasoning is in the project files (`planning/roadmap.md`). Ihor corrects it.

**Current milestone: M1, Paradise greybox end to end.**

Principles: one region to production before the next; small milestones,
each playable; the prototype stays playable in `Main.unity` until a
production region replaces it; enemies after the map in each region.

### M1. Paradise greybox end to end (current)

Goal: all seven zones exist and the region plays west to east in one run.

- Greybox zones 3 to 7 with `ParadiseLayout` and `ParadiseGreybox`: all
  paths, the 15 save shrines, the encounter spaces, the Warden arena, the
  Low Gate's outer door (the drop into Ruined Earth comes with the Warden
  fight in M4).
- Place the Heaven 1 and Heaven 2 beats in the new region: the `opening`
  and `gate` triggers, Mereth and Ithuriel, the seven story items, the
  feather (zone 3) and the light dash (zone 5), the Making-house trigger.
- The construction-rule checker: an EditMode test that walks each zone's
  main path and fails on a gap, step or fall outside the tables in
  `paradise.md` section 2, and on a missing shrine spacing.
- A scripted playtest that runs the whole region's main path.
- Done when: a 20-minute straight run from zone 1 to the Low Gate's outer
  door works, every beat fires, the checker is green, and a run is on the
  playtest list.

### M2. Paradise art, zones 3 and 4

The Great Vines and the Tallied Fields dressed to the bar zones 1 and 2 set;
zones 1 and 2 revisited only where M1 moved them. Done when: a screenshot per
zone in `docs/screenshots/paradise/` and a play-through with no readability
complaint on hazards or ledges.

### M3. Paradise art, zones 5 to 7

The Terraces, the Making-house Approach and the Low Gate; per-zone URP
Volumes blended at the borders; the zone 6 machinery (moving platforms,
steam vents, timed shutters) and the zone 7 wind, built static in M1; music
and ambience hooks that thin from zone 5 on. Done when: the region reads as
one place from gold to steel blue and the frame budgets (300 draw calls, 12
lights, 2,000 particles) hold.

### M4. Paradise enemies and the Warden

Angel guards and watchers in the encounter spaces per `enemies.md`, the
thorn and light hazards, drops per the economy; the Warden of the Low Gate
fight with its lines and the drop through the gate; the prototype's upgrades
and power-ups placed per `economy.md` and `upgrades.md`. Done when: the
region plays with combat from start to Warden on each difficulty and the
enemy data tests cover the new placements.

### M5. Paradise polish and the first Unity build

Feel and audio passes, a timed full run (target 20 minutes), a performance
pass against the budgets, `Main.unity` booting into the new Paradise with
the prototype Paradise retired, an in-game version string, and a `v0.4.0`
playtest build on GitHub (needs the Unity licence secrets in CI, which is
Ihor's to provide; a playtest build, not a release to players). Done when:
Ihor has played a build of Paradise end to end.

### M6. Ruined Earth: design, then build

M6a: `docs/design/ruined_earth.md` in the shape of `paradise.md`, deciding
what of the hub prototype (dialogue, quests, camp, shop) carries over.
M6b: greybox, beats and NPCs. M6c: art and polish. Enemies light by design.

### M7. Purgatory

Design doc, greybox with the Ferryman and the shaft of names, art, polish.

### M8 onward. The nine circles, two at a time

Following the script's pairing and issues #39 to #43: the Shallows and the
Winds, the Mire and the Furnace, the Seal, the Glass and the Archive, the
Gate Below and the Floor. Each pair is a design-then-build milestone with
its bodies from `enemies.md` and its boss. The clock ending and the Gardener
fight (#20, #31) come last.

### Cross-cutting, folded into milestones as it fits

Unity tests in CI (needs the licence secrets or a self-hosted runner);
Godot removal and a README rewrite after M5; the open October design items
(Vials and Church Iron placements, Vael's memory shop, credits text); issue
hygiene as each production region lands.

## Backlog

| Task | Title | Status | Priority | Depends on | Attempts | Thread | PR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| T-0001 | Paradise construction-rule checker | done | 1 | none | 1 | T-0001 implementation | #139 |
| T-0002 | The dialogue pipeline works in Unity: triggers, NPCs and relics talk | done | 2 | none | 1 | T-0002 implementation | #140 |
| T-0003 | Beats of zones 1 and 2 as layout placements | in review | 3 | T-0001, T-0002 | 1 | T-0003 implementation | |
| T-0004 | Greybox zones 3 and 4, the Great Vines and the Tallied Fields | approved | 4 | T-0001, T-0003 | 0 | | |
| T-0005 | Greybox zones 5 to 7, the Terraces, the Making-house Approach and the Low Gate | approved | 5 | T-0004 | 0 | | |

Full task text: project files, `planning/m1-tasks.md` (revision 2). Playtest:
T-0001 no, T-0002 to T-0005 yes. T-0001 and T-0002 run in parallel (disjoint
files); the rest are sequential because they share `ParadiseLayout.cs`, the
zone scenes, `ParadiseWorld.asset` and the build settings.

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

### D-0002: region-by-region roadmap, Paradise first

- **Decided**: the roadmap takes Paradise from greybox to a playable Unity
  build (M1 to M5) before any other region is touched. The current
  milestone is M1, Paradise greybox end to end. Heaven 2 stays inside
  Paradise as zones 5 to 7, and Paradise ends at the Low Gate dropping into
  Ruined Earth, as the world does today.
- **Why**: `docs/design/paradise.md` section 7 already orders the work this
  way and the tech base is built for it; one finished region sets the
  quality bar, tools and budgets the other eleven reuse; the greybox is the
  smallest step that makes the whole region playable and testable; and the
  prototype keeps the full story walkable throughout.
- **Alternative**: breadth first, dressing all twelve prototype regions to a
  middling standard, or building enemies and combat depth before more map.
  Rejected because it spreads effort before the bar is known.
- **Undo**: reorder the roadmap; nothing in code depends on the order. Zones
  are separate scenes, so partial Paradise work survives a reorder.

### D-0003: verification runs on the owner's Windows PC through Remote Control

- **Decided**: see `verification`. Threads verify on the Windows PC
  (Be_Quiet_PC, `D:\Dev\descender-game`; the owner moved from his Mac on
  2026-11-06). Cloud threads cannot run the Unity tests
  or, with the current network policy, the CSharpier check; the owner chose
  Remote Control sessions on his machine over widening the cloud network.
- **Alternative**: a cloud environment allowing the .NET and NuGet hosts
  so threads could at least run the format check. Declined by the owner.
- **Undo**: create that environment and move the format check back into
  cloud threads; the Unity tests still need a machine with the editor.

### D-0004: the drop into Ruined Earth comes with the Warden (M4), not M1

- **Decided**: M1's run ends at the Low Gate's outer door, where the `gate`
  trigger plays and a closed floor with a marker says where the drop goes.
- **Why**: the design drops the hero after the boss fight; a cross-world
  hand-off needs the streamer to switch atlases and the save to record its
  world (a save-format change with a tested migration), none of which the
  greybox needs to play end to end.
- **Alternative**: build the hand-off now in T-0005.
- **Undo**: add the hand-off as a task when M4 starts; nothing in M1 blocks it.

### D-0005: Paradise beats are layout data with an idempotent sync

- **Decided**: story triggers, NPCs, relics and feathers are declared per zone
  in `ParadiseLayout` and built as `WorldPlacement`s; `Descender > Paradise >
  Sync placements from layout` adds or updates them by id in an existing scene
  and touches nothing else, so dressed zones keep their art.
- **Why**: "Build new zones" skips an existing scene and "Rebuild zone" loses
  hand edits; a by-id sync is the only way into zones 1 and 2, and it is what
  `docs/ARCHITECTURE.md` means by idempotent tools.
- **Alternative**: place the beats by hand in each scene.
- **Undo**: hand placements still work; the sync only ever adds or moves its own ids.

### D-0006: shrine spacing is "at most 600 tiles, at least 15 shrines"

- **Decided**: the construction-rule checker enforces a 600-tile maximum between
  consecutive save shrines (and from the start to the first); the region holds at
  least 15 shrines (16 planned). This deviates from `docs/design/paradise.md`
  section 2 ("every 400 to 600 tiles, 15 in total").
- **Why**: zones 1 and 2 as built space theirs at 290 to 384, so a 400 minimum
  fails the design's own first zones; the maximum is what protects the player;
  a fixed 15 forced gaps of 750 and 800 in zones 6 and 7.
- **Alternative**: re-space zones 6 and 7 to hold exactly 15 (loses the shrine
  before the Warden arena).
- **Undo**: change the two constants in `ParadiseRules` and the layout tests.

### D-0007: zone 6 machinery and zone 7 wind are static in M1, built in M3

- **Decided**: the greybox builds the surfaces the moving platforms, steam
  vents, timed shutters and wind will occupy as plain decks with markers, so
  the main path is complete and checkable; the mechanics are M3 scope, with
  the art of zones 5 to 7.
- **Why**: they are new runtime mechanics the run does not need; M3 dresses
  those surfaces anyway.
- **Alternative**: build them in M1 (T-0005 grows past one reviewable change).
- **Undo**: move the line from M3 to a new M1 task.

## Verification

What a task must pass before it merges, and how a thread runs it.

### The checks

| Check | Where it runs | Needs Unity | Command |
| --- | --- | --- | --- |
| EditMode tests (79 on `main` after the single-launch hook) | pre-push hook, developer machine | yes | `sh tools/hooks/pre-push` (or `Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/pre-push-EditMode.xml -logFile Logs/pre-push-EditMode.log`) |
| PlayMode tests (6) | pre-push hook, developer machine | yes, with a graphics device (no `-nographics`) | the hook runs both in one editor launch; alone, the EditMode command with `-testPlatform PlayMode` |
| C# format | GitHub Actions `Tests / C# format` on every PR and push to `main` | no | `csharpier check Assets` (CSharpier 1.0.3, needs the .NET SDK) |
| Claude review | GitHub Actions `Claude Code Review / review` on every non-draft PR | no | governed by `REVIEW.md` |

The pre-push hook is installed per clone with `git config core.hooksPath
tools/hooks`. It finds the editor named in `ProjectSettings/ProjectVersion.txt`
(6000.6.8f1) under the Unity Hub folder, or at `UNITY_EDITOR`. It refuses to
run while the same project folder is open in the editor (`Temp/UnityLockfile`).
Never skip it with `SKIP_UNITY_TESTS=1` or `--no-verify`.

The hook prints its phase, the time elapsed and the previous run's duration,
and the editor log is `Logs/pre-push.log` in the checkout being pushed. To
watch a run from another terminal: `Get-Content <checkout>\Logs\pre-push.log
-Wait -Tail 20` (PowerShell) or `tail -f`. `Logs/pre-push-status.txt` holds
the current test count (`PlayMode 4/6`). A run with no log or progress change
for 10 minutes is stopped and fails.

Merges do not deploy: only a `v*` tag releases, and tagging needs the owner.

### Where threads run them

- **Cloud threads cannot verify.** The cloud container has no Unity editor
  and its network policy blocks the .NET SDK and NuGet hosts, so neither the
  Unity tests nor CSharpier run there (checked 2026-11-06). A cloud thread
  may still write code and docs, but it must say in the pull request that
  the changes were not run in Unity and must not push without the hook
  having run somewhere.
- **Implementation threads run as Remote Control sessions on the owner's
  Windows PC** (Be_Quiet_PC), in `D:\Dev\descender-game`,
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
   `git -C D:\Dev\descender-game worktree add ../descender-task-T-XXXX -b task/T-XXXX origin/main`.
   A worktree is a separate Unity project folder with its own `Library/`,
   `Temp/` and `Logs/` (all gitignored), so it has its own lock file and the
   editor being open on the main checkout does not block it. Remove it when
   the PR is merged: `git worktree remove ../descender-task-T-XXXX`.
2. **The first Unity run in a new worktree imports the whole project**
   (several minutes on the Mac; about a minute on the Windows PC, where the
   whole hook takes 1m07s cold and 19 seconds warm; a few GB in `Library/`).
   Reuse a worktree for a task's fix rounds rather than recreating it.
3. **Unity batchmode instances can run concurrently on different project
   folders**, but each is heavy on CPU and memory. Run at most one Unity
   test run at a time on the machine: take a lock before the hook, for
   example `mkdir "$TEMP/descender-unity.lock"` and remove it after, or simply
   push one task at a time. Two instances must never open the same folder.
4. **Shared files**: two tasks must not both touch the same scene, prefab,
   ScriptableObject, project setting or `Packages/manifest.json`; the
   coordinator sequences those tasks.
5. **Hook installation is per checkout**, not per worktree: `core.hooksPath`
   is in the shared `.git/config`, so setting it once in the main checkout
   covers every worktree. Check with `git config core.hooksPath` before the
   first push from a worktree. When the value is an absolute path (as on the
   Windows PC, `D:\Dev\descender-game\tools\hooks`), every worktree runs the
   main checkout's copy of the hook against its own files, so a change to the
   hook takes effect only once the main checkout has pulled it, and a task
   branch cannot loosen its own gate.
6. **Do not copy `Library/` from another checkout to save the first import.**
   It was tried on the Windows PC: the copied script-to-class mapping was
   wrong until `Library/Bee`, `ScriptAssemblies`, `ArtifactDB`,
   `SourceAssetDB` and `Artifacts` were deleted, which is the import itself.
   The first run costs a minute or two there, so the copy is not worth the
   risk of a test run against a stale import.

## Playtest

Open items for the owner to play in the editor. Remove an item when it has
been played and its feedback is in.

- **T-0002, the dialogue pipeline (PR #140).** In `Main.unity`, after New
  Game: the opening lines play at the start, the gate lines play at the goal
  gate, Old Pell talks in the hub, and Tobiah's token can be examined (its
  lines play and it is gone afterwards).
- **T-0003, the beats of zones 1 and 2.** In `ParadiseMain.unity`, after New
  Game: the opening lines play as Ansel wakes, Mereth stands by her tree in
  the Orchard and talks (and says something else when asked again), and
  Tobiah's token in the root hollow below her can be examined, reads its
  lines and is gone (`item:token` set). No white, pink or orange beat flags
  remain at those three spots.
