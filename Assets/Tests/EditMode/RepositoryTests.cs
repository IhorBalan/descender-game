using System.Collections.Generic;
using System.IO;
using System.Linq;
using Descender.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Descender.Tests
{
    /// <summary>
    /// What the repository as a whole must hold for the Unity project to stand on its own:
    /// no C# line, comment included, names a file of the Godot project the game was ported from, the Godot
    /// project itself is gone from the tree, and the twelve prototype region scenes carry everything
    /// they reference. The roots, exclusions and patterns are constants so later checks on the
    /// repository can widen or share them.
    /// </summary>
    public class RepositoryTests
    {
        /// <summary>
        /// The Godot project's files and folders at the repository root, deleted with the project.
        /// The last commit that held them is tagged `godot-last`.
        /// </summary>
        private static readonly string[] GodotRootEntries =
        {
            "project.godot",
            "export_presets.cfg",
            "default_bus_layout.tres",
            ".gdlintrc",
            "addons",
            "dialogue",
            "godot_assets",
            "levels",
            "resources",
            "scenes",
            "scripts",
            "tests",
        };

        /// <summary>No tracked file anywhere in the repository may end with one of these.</summary>
        private static readonly string[] GodotFileSuffixes = { ".tscn", ".tres", ".gd", ".import", ".gdignore" };

        /// <summary>
        /// Folders the tree walk skips when `git` is not on the path: what `.gitignore` names, so an
        /// editor's or a build's untracked output cannot fail the test.
        /// </summary>
        private static readonly string[] UntrackedFolders =
        {
            ".git",
            ".vs",
            "Library",
            "Temp",
            "Obj",
            "Build",
            "Builds",
            "Logs",
            "UserSettings",
            "MemoryCaptures",
            "Recordings",
        };

        /// <summary>The folders whose C# is scanned, from the project root.</summary>
        private static readonly string[] ScannedRoots = { "Assets/Scripts", "Assets/Tests" };

        /// <summary>Files the scan leaves out: this one, which has to spell the patterns.</summary>
        private static readonly string[] ExcludedFiles = { "Assets/Tests/EditMode/RepositoryTests.cs" };

        /// <summary>
        /// What no line, code or comment, may contain: Godot paths and formats, and a path that
        /// leaves Assets/. A `.gd` script name may still be cited as the origin of a port.
        /// </summary>
        private static readonly string[] ForbiddenInCode =
        {
            "res://",
            ".tscn",
            ".tres",
            ".gdshader",
            "project.godot",
            "godot_assets/",
            "import_godot",
            "../",
        };

        /// <summary>The prototype regions sit directly in this folder; the rebuilt Paradise is in a subfolder and is not one of them.</summary>
        private const string RegionSceneFolder = "Assets/Scenes";

        private const string RegionScenePattern = "Region_*.unity";

        private const int RegionSceneCount = 12;

        /// <summary>Every type a WorldPlacement may have.</summary>
        private static readonly string[] PlacementTypes =
        {
            "start",
            "save",
            "ability",
            "item",
            "upgrade",
            "power",
            "enemy",
            "npc",
            "trigger",
        };

        /// <summary>The types that must find a prefab in the catalog.</summary>
        private static readonly string[] PlacementTypesWithPrefab =
        {
            "save",
            "ability",
            "item",
            "upgrade",
            "power",
            "enemy",
        };

        /// <summary>How many problems a failure message lists before it stops.</summary>
        private const int MaxListed = 20;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [Test]
        public void UnityCodeNamesNoGodotPath()
        {
            var offenders = new List<string>();
            var scanned = 0;
            foreach (var root in ScannedRoots)
            {
                var folder = Path.Combine(ProjectRoot, root);
                Assert.That(Directory.Exists(folder), Is.True, root + " is missing");
                foreach (var file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                {
                    var relative = file.Substring(ProjectRoot.Length + 1).Replace('\\', '/');
                    if (ExcludedFiles.Contains(relative))
                    {
                        continue;
                    }
                    scanned++;
                    var lines = File.ReadAllLines(file);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        foreach (var pattern in ForbiddenInCode)
                        {
                            if (lines[i].Contains(pattern))
                            {
                                offenders.Add($"{relative}:{i + 1} contains \"{pattern}\"");
                            }
                        }
                    }
                }
            }

            Assert.That(scanned, Is.GreaterThan(0), "no C# file was scanned");
            Assert.That(offenders, Is.Empty, Listed("C# names a Godot path or leaves Assets/", offenders));
        }

        [Test]
        public void TheRepositoryHoldsNoGodotProject()
        {
            var problems = new List<string>();
            foreach (var entry in GodotRootEntries)
            {
                var path = Path.Combine(ProjectRoot, entry);
                if (File.Exists(path) || Directory.Exists(path))
                {
                    problems.Add(entry + " exists at the repository root");
                }
            }

            var files = TrackedFiles();
            Assert.That(files.Count, Is.GreaterThan(0), "no repository file was listed");
            foreach (var file in files)
            {
                foreach (var suffix in GodotFileSuffixes)
                {
                    if (file.EndsWith(suffix, System.StringComparison.Ordinal))
                    {
                        problems.Add(file);
                    }
                }
            }

            Assert.That(problems, Is.Empty, Listed("the repository still holds Godot files", problems));
        }

        /// <summary>
        /// Every tracked file, relative to the repository root with forward slashes: `git ls-files`
        /// when git is on the path, else a walk of the tree that skips the untracked folders.
        /// </summary>
        private static List<string> TrackedFiles()
        {
            var listed = GitLsFiles();
            if (listed != null)
            {
                return listed;
            }
            var files = new List<string>();
            Walk(ProjectRoot, files);
            return files;
        }

        private static List<string> GitLsFiles()
        {
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo("git", "ls-files -z")
                {
                    WorkingDirectory = ProjectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using (var process = System.Diagnostics.Process.Start(info))
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        return null;
                    }
                    return output.Split('\0').Where(path => path.Length > 0).ToList();
                }
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private static void Walk(string folder, List<string> files)
        {
            foreach (var child in Directory.GetDirectories(folder))
            {
                if (!UntrackedFolders.Contains(Path.GetFileName(child)))
                {
                    Walk(child, files);
                }
            }
            foreach (var file in Directory.GetFiles(folder))
            {
                files.Add(file.Substring(ProjectRoot.Length + 1).Replace('\\', '/'));
            }
        }

        [Test]
        public void RegionScenesAreSelfContained()
        {
            var scenes = Directory
                .GetFiles(
                    Path.Combine(ProjectRoot, RegionSceneFolder),
                    RegionScenePattern,
                    SearchOption.TopDirectoryOnly
                )
                .Select(file => RegionSceneFolder + "/" + Path.GetFileName(file))
                .OrderBy(path => path, System.StringComparer.Ordinal)
                .ToArray();
            Assert.That(
                scenes.Length,
                Is.EqualTo(RegionSceneCount),
                "the prototype region scenes in " + RegionSceneFolder
            );

            var problems = new List<string>();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var opened = default(Scene);
            try
            {
                foreach (var path in scenes)
                {
                    opened = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    Check(opened, Path.GetFileNameWithoutExtension(path), problems);
                    EditorSceneManager.CloseScene(opened, true);
                    opened = default;
                }
            }
            finally
            {
                if (opened.IsValid() && opened.isLoaded)
                {
                    EditorSceneManager.CloseScene(opened, true);
                }
                // A batch run starts in an untitled scene, which has no setup to restore.
                if (setup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }

            Assert.That(problems, Is.Empty, Listed("region scenes reference something missing", problems));
        }

        /// <summary>Adds what an open region scene lacks to `problems`, each line starting with the scene's name.</summary>
        private static void Check(Scene scene, string sceneName, List<string> problems)
        {
            var objects = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();
            if (objects.Length == 0)
            {
                problems.Add($"{sceneName}: the scene is empty");
                return;
            }

            foreach (var gameObject in objects)
            {
                var missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missing > 0)
                {
                    problems.Add($"{sceneName}: {PathOf(gameObject)} has {missing} missing script(s)");
                }
            }

            // The hero drops through a ledge by turning its layer off, which an effector's own mask overrides.
            foreach (var effector in objects.SelectMany(o => o.GetComponents<PlatformEffector2D>()))
            {
                if (effector.useColliderMask)
                {
                    problems.Add($"{sceneName}: {PathOf(effector.gameObject)} uses its own collider mask");
                }
            }

            var tiles = objects.SelectMany(o => o.GetComponents<RegionTiles>()).ToArray();
            if (tiles.Length == 0)
            {
                problems.Add($"{sceneName}: no RegionTiles");
            }
            foreach (var regionTiles in tiles)
            {
                var at = $"{sceneName}: {PathOf(regionTiles.gameObject)}";
                var tileSet = regionTiles.TileSet;
                if (tileSet == null)
                {
                    problems.Add($"{at} has no RegionTileSet");
                    continue;
                }
                if (tileSet.sprites == null || tileSet.sprites.Length == 0)
                {
                    problems.Add($"{at}: {tileSet.name} has no atlas sprites");
                }
                else if (tileSet.sprites.Any(sprite => sprite == null))
                {
                    problems.Add($"{at}: {tileSet.name} has a missing atlas sprite");
                }
                if (tileSet.cells == null)
                {
                    problems.Add($"{at}: {tileSet.name} has no cell data");
                }
                else if (tileSet.Data.Layers.Count == 0)
                {
                    problems.Add($"{at}: {tileSet.name}'s cell data holds no layer");
                }
            }

            foreach (var layer in objects.SelectMany(o => o.GetComponents<BackdropLayer>()))
            {
                if (layer.GetComponent<SpriteRenderer>().sprite == null)
                {
                    problems.Add($"{sceneName}: backdrop {PathOf(layer.gameObject)} has no sprite");
                }
            }

            foreach (var prop in objects.SelectMany(o => o.GetComponents<WorldProp>()))
            {
                var renderers = prop.GetComponentsInChildren<SpriteRenderer>(true);
                if (renderers.Length == 0 || renderers.Any(renderer => renderer.sprite == null))
                {
                    problems.Add($"{sceneName}: prop {PathOf(prop.gameObject)} has no sprite");
                }
            }

            var placements = objects.SelectMany(o => o.GetComponents<WorldPlacement>()).ToArray();
            if (placements.Length == 0)
            {
                problems.Add($"{sceneName}: no WorldPlacement");
            }
            foreach (var placement in placements)
            {
                var at = $"{sceneName}: placement {placement.Id} ({placement.Type})";
                var catalog =
                    new SerializedObject(placement).FindProperty("catalog").objectReferenceValue as PlacementCatalog;
                if (catalog == null)
                {
                    problems.Add($"{at} has no catalog");
                }
                if (!PlacementTypes.Contains(placement.Type))
                {
                    problems.Add($"{at} has an unknown type");
                    continue;
                }
                // An enemy is a foe made from the profile the catalog names for its character.
                if (
                    catalog != null
                    && PlacementTypesWithPrefab.Contains(placement.Type)
                    && catalog.Resolve(placement.Type, placement.Character, placement.Dialogue.Length > 0) == null
                    && (placement.Type != "enemy" || catalog.FoeFor(placement.Character) == null)
                )
                {
                    problems.Add($"{at} finds nothing for \"{placement.Character}\" in {catalog.name}");
                }
            }
        }

        private static string PathOf(GameObject gameObject)
        {
            var path = gameObject.name;
            for (var parent = gameObject.transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }
            return path;
        }

        private static string Listed(string what, List<string> problems)
        {
            var shown = string.Join("\n", problems.Take(MaxListed));
            var more = problems.Count > MaxListed ? $"\n... and {problems.Count - MaxListed} more" : "";
            return $"{what} ({problems.Count}):\n{shown}{more}";
        }
    }
}
