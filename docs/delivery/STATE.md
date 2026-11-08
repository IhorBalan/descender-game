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

### MG. Unity-only repository (done 2026-11-08)

Prove the Unity assets are complete and self-contained, point the remaining
tools at `Assets/`, delete the Godot project (`project.godot`, `scenes/`,
`scripts/`, `levels/`, `resources/`, `godot_assets/`, `addons/`,
`dialogue/`, `tests/`, the Godot tool scripts and importers), and rewrite
the README and architecture docs for one project. Done when: the
repository tests `TheRepositoryHoldsNoGodotProject` and
`UnityCodeNamesNoGodotPath` are green, all Unity tests pass, and the docs
describe a Unity-only repository. Done: T-0007 to T-0010 merged (#148 to
#151); the Godot prototype is in history at the tag `godot-last`
(24035cf). Still open, Ihor's: the GitHub repository description and
topics say Godot.

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

Unity tests in CI (needs the licence secrets or a self-hosted runner); the
open October design items
(Vials and Church Iron placements, Vael's memory shop, credits text); issue
hygiene as each production region lands.

## Backlog

| Task | Title | Status | Priority | Depends on | Attempts | Thread | PR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| T-0001 | Paradise construction-rule checker | done | 1 | none | 1 | T-0001 implementation | #139 |
| T-0002 | The dialogue pipeline works in Unity: triggers, NPCs and relics talk | done | 2 | none | 1 | T-0002 implementation | #140 |
| T-0003 | Beats of zones 1 and 2 as layout placements | done | 3 | T-0001, T-0002 | 1 | T-0003 implementation | #145 |
| T-0004 | Greybox zones 3 and 4, the Great Vines and the Tallied Fields | done | 4 | T-0001, T-0003 | 1 | Work through the roadmap tasks | #152 |
| T-0005 | Greybox zones 5 to 7, the Terraces, the Making-house Approach and the Low Gate | done | 5 | T-0004 | 1 | Work through the roadmap tasks | #153 |

| T-0006 | Prove the Unity port is complete and reads nothing from Godot | done | 6 | none | 1 | T-0006 implementation | #146 |
| T-0007 | The Python tools stand without the Godot tree | done | 7 | none | 1 | Continue the Godot removal milestone | #148 |
| T-0008 | Godot-format code and reference copies leave Assets/ | done | 8 | T-0006, T-0007 | 1 | Continue the Godot removal milestone | #149 |
| T-0009 | Delete the Godot project | done | 9 | T-0008 | 1 | Continue the Godot removal milestone | #150 |
| T-0010 | The docs describe one Unity project | done | 10 | T-0009 | 1 | Continue the Godot removal milestone | #151 |
| T-0011 | The scripted run of the Paradise main path | in review | 11 | T-0005 | 1 | Work through the roadmap tasks | #154 |

Full task text: project files, `planning/m1-tasks.md` (revision 3, T-0011
added 2026-11-08) and `planning/godot-removal-tasks.md` (revision 2, MG).
Playtest: T-0001 no, T-0002 to T-0005 yes, T-0006 to T-0011 no. T-0001 and T-0002 run in parallel (disjoint
files); the rest are sequential because they share `ParadiseLayout.cs`, the
zone scenes, `ParadiseWorld.asset` and the build settings. T-0006 and
T-0007 run in parallel with M1 (disjoint files); T-0008, T-0009 and T-0010
follow in order.

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

### D-0008: Godot is removed now, alongside M1, not after M5

- **Decided**: milestone MG runs now in parallel with M1 and finishes before
  M2 starts. Deletion is one squash commit after the Unity assets are proven
  complete and self-contained and the tools no longer read the Godot tree.
  No Ihor sign-off is needed: everything deleted stays in git history.
- **Why**: Ihor asked for a Unity-only repository; nothing in Unity reads a
  Godot file (checked by T-0006), so the reference has no remaining technical
  use; the Godot files were last changed on 2026-10-31 and the two trees have
  already diverged (the rebuilt Paradise exists only in Unity), so the
  "behavioural reference" is ageing; two projects in one root confuse agents
  and the review (case-collision rules, `.gdignore`, "do not edit the Godot
  files"); and MG touches no file M1 touches, so it costs M1 nothing.
- **Alternative**: keep the Godot tree until M5 as the roadmap draft had it,
  or move it to a `godot-reference` branch or a separate repository.
  Rejected: a branch is as recoverable as history and adds a thing to
  maintain; a separate repository needs Ihor to create it.
- **Undo**: `git revert` the T-0009 squash commit (and T-0010's if the docs
  should follow); the files come back with their history. The last commit
  with the whole Godot project is the parent of T-0009's merge; T-0009 tags
  it `godot-last` so it is easy to find.

### D-0009: the generated region scenes are hand-owned Unity assets from now on

- **Decided**: `Assets/Scenes/Region_<key>.unity`, `Assets/Data/World.asset`,
  `Assets/Data/Regions/Tiles/*` and `Assets/Prefabs/Props/**` stop being
  generator output. The importer is deleted; a change to a prototype region
  is made in Unity (or by a Unity editor script), like any other scene.
- **Why**: the generator's only input was the Godot scenes; keeping it means
  keeping them. The prototype regions are retired one by one as production
  regions land (M5 retires Paradise), so they will see few edits.
- **Alternative**: keep `import_godot_regions.py` and the Godot scenes as the
  level source until every region is rebuilt. Rejected: that is the
  "after M5 (or later)" plan Ihor just asked to end.
- **Undo**: restore the importer and the Godot scenes from `godot-last` and
  run it; the GUIDs are derived from paths, so the output lands on the same
  ids.

### D-0010: checkpoints are numbered by zone, then west to east

- **Decided** (2026-11-08, coordinator, folded into T-0005): `Sync world from
  scenes` and `ParadiseGreybox.CheckpointOrder` number the save shrines zone
  by zone (the zones' order in `ParadiseLayout.All()`), west to east within a
  zone, instead of top to bottom then left to right.
- **Why**: the y-then-x order interleaved every new zone's shrines with the
  older ones, so building zones 3 and 4 (#152) renumbered the shrines of
  zones 1 and 2 in their scenes, and every later zone would have done the
  same to every scene before it and to the checkpoint index saved in a slot.
  By zone, a new zone only appends.
- **Alternative**: keep the world order and accept the churn in older
  scenes. Rejected: the task rule "scene diffs of earlier zones are empty"
  is worth more than a top-to-bottom numbering nothing reads.
- **Undo**: restore the sort in `SyncWorld` and `CheckpointOrder` and run the
  sync; saves made in the meantime point at other shrines.

## Verification

What a task must pass before it merges, and how a thread runs it.

### The checks

| Check | Where it runs | Needs Unity | Command |
| --- | --- | --- | --- |
| EditMode tests (120 on `main` after T-0011) | pre-push hook, developer machine | yes | `sh tools/hooks/pre-push` (or `Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/pre-push-EditMode.xml -logFile Logs/pre-push-EditMode.log`) |
| PlayMode tests (17 after T-0011; `ParadiseRunTests` runs the whole region and takes minutes, logging its progress every two game minutes so the hook's stall limit is not hit) | pre-push hook, developer machine | yes, with a graphics device (no `-nographics`) | the hook runs both in one editor launch; alone, the EditMode command with `-testPlatform PlayMode` |
| C# format | GitHub Actions `Tests / C# format` on every PR and push to `main` | no | `csharpier check Assets` (CSharpier 1.0.3, needs the .NET SDK) |
| Claude review | GitHub Actions `Claude Code Review / review` on every non-draft PR | no | governed by `REVIEW.md` |
| Python tools | GitHub Actions `Tests / Python tools` on every PR and push to `main` | no | `python3 -I tools/unity/check_tools.py` (every `tools/` module imports and names no Godot path) and `python3 -I tools/unity/build_prefabs.py && git diff --exit-code --stat` (the committed prefabs and assets are the script's own output) |

The pre-push hook is installed per clone with `git config core.hooksPath
tools/hooks`. It finds the editor named in `ProjectSettings/ProjectVersion.txt`
(6000.6.8f1) under the Unity Hub folder, or at `UNITY_EDITOR`. It refuses to
run while the same project folder is open in the editor (`Temp/UnityLockfile`).
Never skip it with `SKIP_UNITY_TESTS=1` or `--no-verify`. Test runs make no
sound: the PlayMode assembly's `MuteDuringTests` callback mutes the listener
for the whole run.

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
  repeats it. The Python tool checks run in CI (`Tests / Python tools`, with
  Pillow and numpy installed on the runner), so the Windows PC needs no Python.
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
- **T-0004, zones 3 and 4.** In `ParadiseMain.unity`, after New Game: run
  from the start through the Great Vines to the Tallied Fields without a
  jump the hero cannot make (the branch runs and the trunk climbs by pod);
  the thorns on the moss floors hurt and are the only red thing; the feather
  at the top of the Crown Vine grants Wings and the descent off the top into
  zone 4 flows on double jumps; the 7-tile stream gaps need Wings; the
  ledger page in the keeper's hut can be examined; Ithuriel talks by the
  Field Shrine (and says something else when asked again); the shrines at
  about x 1,750, 2,250, 2,580, 3,150 and 3,650 save.
- **T-0005, zones 5 to 7, the whole run.** In `ParadiseMain.unity`, after
  New Game: a straight run from the start to the Low Gate's outer door,
  timed against the 20-minute target (7,500 tiles at 6.25 tiles/s is 20
  minutes of running); the terraces climb in steps the hero makes; the two
  light barriers on the shortcut ledges stop him until the light dash at x
  4,700, after which he dashes through one and across the 9-tile gaps; the
  tally stone, prayer card and glove can be examined; the `makinghouse`
  lines play where the door comes into view; the basin shard is in the
  drain below the pipes; the cliff ledges descend without a killing fall;
  the `gate` lines play at the outer door and the floor there is closed.
