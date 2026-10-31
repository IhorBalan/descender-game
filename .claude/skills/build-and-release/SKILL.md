---
name: build-and-release
description: Building and releasing the Unity game: Player settings, scenes in build, the release workflow that builds Linux and Windows through GameCI on a v* tag and attaches zips to a GitHub release, versioning, running a build, and the licence secrets it needs. Use when touching build settings, the release workflow, or cutting a release.
---

# Build and release

## Player settings

Set by `Descender > Setup > Render pipeline and settings`
(`ProjectBootstrap`) and kept in `ProjectSettings/ProjectSettings.asset`:
product `Descender`, default 1280 x 720 (4x of 320 x 180), `Fullscreen
Window`, resizable, `Color Space: Gamma` (pixel art, no HDR), `Run In
Background` off, `Scripting Backend: Mono` (IL2CPP later if a platform
needs it), `Api Compatibility: .NET Standard 2.1`, `Managed Stripping:
Low` (Newtonsoft reflection). Splash off.

`File > Build Profiles`: `Main` first, then every region scene; a region
not in the list cannot be loaded additively. The EditMode test
`BuildScenesTests` checks every `Assets/Scenes/Regions/*.unity` is in
`EditorBuildSettings.scenes`.

## Local build

`File > Build Profiles > Windows/Linux > Build`, or:

```
Unity -batchmode -quit -projectPath . -buildLinux64Player build/StandaloneLinux64/descender -logFile -
Unity -batchmode -quit -projectPath . -buildWindows64Player build/StandaloneWindows64/descender.exe -logFile -
```

Run the Linux build with `./build/StandaloneLinux64/descender` (`-screen-
fullscreen 0` for a window). Builds, `Library/`, `Logs/`, `Temp/` and
`UserSettings/` are gitignored.

## Release workflow

`.github/workflows/release.yml` runs on a tag matching `v*`:

1. `build` matrix: `game-ci/unity-builder@v4` for `StandaloneLinux64` and
   `StandaloneWindows64`, `buildName: descender`, `versioning: Tag` (the
   tag is the `Application.version`), `Library/` cached per platform.
2. Zips `build/<platform>` to `descender-linux.zip` and
   `descender-windows.zip`.
3. `release`: `softprops/action-gh-release@v2` creates the release named
   after the tag with generated notes and both zips.

Cutting a release: merge to `main`, then

```
git tag v0.4.0 && git push origin v0.4.0
```

Tags are `vMAJOR.MINOR.PATCH`. The Godot releases before the move keep
their tags; the first Unity release is `v1.0.0`.

## Secrets

Same as the tests: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` in the
repository secrets (see `unity-testing-and-ci`). A build job that fails
in `Activate` is missing them.

## Checklist before a tag

- `main` green (format, EditMode, PlayMode).
- `ProjectSettings/ProjectVersion.txt` matches the editor version GameCI
  will pull (it reads this file; a `6000.3.x` version has a GameCI image).
- Every region scene in Build Profiles.
- `docs/` changelog updated if there is one.
- Saves from the previous release load (the save tests load a version-1
  file).
