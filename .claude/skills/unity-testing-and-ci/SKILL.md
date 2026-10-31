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
All. Command line (the same thing CI does):

```
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -logFile -
```

Format: `csharpier check Assets` (`.csharpierrc.yaml`), fix with
`csharpier format Assets`.

## Workflows

`.github/workflows/tests.yml` on pull requests and pushes to `main`:

1. `format`: `dotnet tool install csharpier` then `csharpier check Assets`.
2. `unity-tests` matrix EditMode / PlayMode with `game-ci/unity-test-runner@v4`,
   `Library/` cached by hash of Assets, Packages and ProjectSettings,
   `githubToken` so results appear as a check, `coverageEnabled: false`
   (coverage has crashed PlayMode on Unity 6).

`.github/workflows/release.yml` on a `v*` tag: `game-ci/unity-builder@v4`
for StandaloneLinux64 and StandaloneWindows64, zipped and attached to a
GitHub release (see `build-and-release`).

`.github/workflows/claude-code-review.yml`: the Claude review of every
non-draft PR, governed by `REVIEW.md` at the root (focus areas, severity,
the skip list). Changes to how the bot reviews go in `REVIEW.md`, not the
workflow prompt.

## Secrets

GameCI activates Unity with repository secrets `UNITY_LICENSE` (the
contents of the `.ulf` file: on a machine with Unity Hub, `Unity Hub >
Preferences > Licenses`, or `Unity -batchmode -createManualActivationFile`
then activate at license.unity3d.com/manual), `UNITY_EMAIL` and
`UNITY_PASSWORD` for the Unity account. A Personal licence works for CI.
The Claude review needs `CLAUDE_CODE_OAUTH_TOKEN` or `ANTHROPIC_API_KEY`
as before. Without the Unity secrets the test jobs fail at activation
with "No valid Unity license"; that is the first thing to check when
every test job is red.

## When CI is red

- Format: run `csharpier format Assets` and commit.
- Activation error: secrets (above).
- A test fails only in CI: usually asset import order or a path with
  `Application.dataPath`; load through `AssetDatabase` in EditMode and
  through scenes in PlayMode.
- Compile error in CI only: a `UnityEditor` reference outside the Editor
  asmdef or missing asmdef reference.
- Library cache corrupt after a Unity upgrade: bump the cache key prefix.

Never skip, ignore or `[Explicit]` a test to get green.
