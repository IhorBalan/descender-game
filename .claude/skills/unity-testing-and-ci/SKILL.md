---
name: unity-testing-and-ci
description: Tests and CI for the Unity project: the Unity Test Framework EditMode tests (data, logic, importers) and PlayMode tests (scene smoke), what to test where, running them in the editor and from the command line, the GitHub Actions workflows (CSharpier check, GameCI test runner, release builds), the Unity licence secrets, and the Claude review bot. Use when adding or fixing tests, when CI is red, or when touching .github/workflows or REVIEW.md.
---

# Testing and CI

## Where a test goes

- `Assets/Tests/EditMode/` (`Descender.Tests.EditMode`, Editor only):
  anything with no scene: the static state and data classes, `DialogueData`
  against the real JSON (`DialogueFileTests`), profiles loaded through
  `AssetDatabase`, the editor importers, `TresParser`, save round trips.
  Fast; most tests live here. They may reference `Descender.Editor`.
- `Assets/Tests/PlayMode/` (`Descender.Tests.PlayMode`): tests that need a
  running scene: load `Main` + a region, spawn the hero, step physics,
  check it stands, jumps, takes damage, saves. These are the smoke tests
  the Godot `smoke_test.gd` did. Slow; keep them few and broad.

Both asmdefs set `UNITY_INCLUDE_TESTS`, reference `nunit.framework.dll`,
`UnityEngine.TestRunner`, `UnityEditor.TestRunner` and the runtime
assembly. Test classes are `public`, methods `[Test]` (EditMode) or
`[UnityTest]` returning `IEnumerator` (PlayMode), named
`Method_Scenario_Expectation` or a sentence (`LoadsGodotSave`). Use
`Assert.That(x, Is.EqualTo(y))`; `Is.EqualTo(1f).Within(0.001f)` for
floats.

```csharp
[UnityTest]
public IEnumerator HeroStandsOnTerrain()
{
    yield return SceneManager.LoadSceneAsync("Main");
    yield return SceneManager.LoadSceneAsync("Region_Hollow", LoadSceneMode.Additive);
    var hero = Object.FindFirstObjectByType<PlayerController>();
    yield return new WaitForFixedUpdate();
    for (var i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
    Assert.That(hero.IsGrounded, Is.True);
}
```

PlayMode tests reset statics in `[SetUp]`, use a temp directory for
saves, and never depend on test order. `[Timeout(60000)]` on long ones.
`LogAssert.Expect(LogType.Warning, ...)` when a warning is the expected
outcome; an unexpected error log fails the test.

## Running

Editor: `Window > General > Test Runner`, EditMode or PlayMode tab, Run
All. Command line (what the pre-push hook does):

```
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -logFile -
```

Format: `csharpier check Assets` (`.csharpierrc.yaml`), fix with
`csharpier format Assets`.

### The pre-push hook

The tests gate pushes from the developer's machine, not from CI.
`tools/hooks/pre-push` runs the EditMode then the PlayMode tests with the
editor named in `ProjectSettings/ProjectVersion.txt` (found under the
Unity Hub install folder, or `UNITY_EDITOR`), prints the compile errors or
the failing tests, and refuses the push when either run fails. About 20
seconds with a warm `Library/`. Install it once per clone:

```
git config core.hooksPath tools/hooks
```

The project must not be open in the editor while it runs (the editor holds
`Temp/UnityLockfile`); then run both Test Runner tabs by hand and push with
`SKIP_UNITY_TESTS=1 git push`. `git push --no-verify` also skips it. A
pull request's description says the tests passed locally; the reviewer
trusts that, so do not skip the hook for a change to C#, a scene, a prefab
or data.

## Workflows

`.github/workflows/tests.yml` on pull requests and pushes to `main`:
`format`, `dotnet tool install csharpier` then `csharpier check Assets`.
No Unity runs in CI: the test jobs that used GameCI were removed because
they need a Unity licence and the account's email and password as
repository secrets, which this project does not keep. The pre-push hook
above is the test gate.

`.github/workflows/release.yml` on a `v*` tag: `game-ci/unity-builder@v4`
for StandaloneLinux64 and StandaloneWindows64, zipped and attached to a
GitHub release (see `build-and-release`). This one still needs the Unity
licence secrets below; until they exist, build releases locally.

`.github/workflows/claude-code-review.yml`: the Claude review of every
non-draft PR, governed by `REVIEW.md` at the root (focus areas, severity,
the skip list). Changes to how the bot reviews go in `REVIEW.md`, not the
workflow prompt.

## Secrets

Only the release workflow activates Unity, with repository secrets
`UNITY_LICENSE` (the contents of the `.ulf` file: `Unity -batchmode
-createManualActivationFile` then activate at license.unity3d.com/manual),
`UNITY_EMAIL` and `UNITY_PASSWORD` for the Unity account that owns it. A
Personal licence works. A Unity service account cannot activate a licence
(Unity's licensing backend rejects its tokens), so there is no
password-free route short of a Pro serial. The Claude review needs
`CLAUDE_CODE_OAUTH_TOKEN` or `ANTHROPIC_API_KEY`.

## When CI or the hook is red

- Format: run `csharpier format Assets` and commit.
- The hook cannot find Unity: install the version in
  `ProjectSettings/ProjectVersion.txt` through Unity Hub, or point
  `UNITY_EDITOR` at it.
- The hook says the project is open: close the editor, or run the Test
  Runner by hand and push with `SKIP_UNITY_TESTS=1`.
- Compile error: the log is `Logs/pre-push-<platform>.log`; a `UnityEditor`
  reference outside the Editor asmdef or a missing asmdef reference are
  the usual causes.
- Release activation error: the secrets above.
