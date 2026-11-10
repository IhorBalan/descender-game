using System.Collections.Generic;
using System.IO;
using Descender.EditorTools.Setup;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// What a build needs to boot to the title: Main.unity first in the build
    /// settings, and the UI input map with the actions the EventSystem is wired to.
    /// </summary>
    public class BootTests
    {
        [Test]
        public void MainSceneIsFirstInTheBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Length, Is.GreaterThan(0), "the build settings list no scene");
            Assert.That(scenes[0].path, Is.EqualTo(MainSceneSetup.ScenePath));
            Assert.That(scenes[0].enabled, Is.True);
        }

        [Test]
        public void PlayFromMainLeavesTheTestRunnerSceneAlone()
        {
            // The PlayMode test run boots from Assets/InitTestScene<guid>.unity; Play from Main
            // must not swap that for the game's entry scene, or the run hangs.
            Assert.That(PlayFromMain.IsTestRunnerScene("Assets/InitTestScene53412698-01fe.unity"), Is.True);
            Assert.That(PlayFromMain.IsTestRunnerScene(PlayFromMain.MainScenePath), Is.False);
            Assert.That(PlayFromMain.IsTestRunnerScene(PlayFromMain.ParadiseEntryPath), Is.False);
            Assert.That(PlayFromMain.IsTestRunnerScene(""), Is.False);
            Assert.That(PlayFromMain.IsTestRunnerScene(null), Is.False);
        }

        [Test]
        public void MainSceneSetupKeepsTheParadiseScenesInTheBuild()
        {
            var before = EditorBuildSettings.scenes;
            try
            {
                MainSceneSetup.SetBuildScenes();
                var after = EditorBuildSettings.scenes;
                Assert.That(after[0].path, Is.EqualTo(MainSceneSetup.ScenePath), "Main must stay first");
                var listed = new List<string>();
                foreach (var scene in after)
                {
                    listed.Add(scene.path);
                }
                var paradise = Directory.GetFiles(MainSceneSetup.ParadiseSceneFolder, "*.unity");
                Assert.That(paradise.Length, Is.GreaterThan(0), "no scene under " + MainSceneSetup.ParadiseSceneFolder);
                foreach (var file in paradise)
                {
                    Assert.That(listed, Does.Contain(file.Replace('\\', '/')), "dropped from the build settings");
                }
                // The committed list is already what the setup makes: running it changes nothing.
                var was = new List<string>();
                foreach (var scene in before)
                {
                    was.Add(scene.path);
                }
                Assert.That(listed, Is.EqualTo(was), "the setup should leave EditorBuildSettings.asset as it is");
            }
            finally
            {
                EditorBuildSettings.scenes = before;
            }
        }

        [Test]
        public void UiMapHasTheActionsTheEventSystemBindsTo()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(MainSceneSetup.ActionsPath);
            Assert.That(actions, Is.Not.Null, MainSceneSetup.ActionsPath + " is missing");
            var ui = actions.FindActionMap("UI", true);
            foreach (var name in new[] { "Navigate", "Submit", "Cancel", "Point", "Click" })
            {
                Assert.That(ui.FindAction(name), Is.Not.Null, $"the UI map has no '{name}' action");
            }
            Assert.That(actions.FindActionMap("Player", true).FindAction("Pause"), Is.Not.Null);
        }

        [Test]
        public void TheLanternFruitLookAndSoundsAreWholeAndTheEntryScenesGiveItToTheLab()
        {
            const string path = "Assets/Data/Enemies/LanternFruitLook.asset";
            var look = AssetDatabase.LoadAssetAtPath<Enemies.LanternFruitLook>(path);
            Assert.That(look, Is.Not.Null, path);
            Assert.That(look.Hang, Is.Not.Null);
            Assert.That(look.Ripe, Is.Not.Null);
            foreach (var frames in new[] { look.Burst, look.Pop, look.Grow })
            {
                Assert.That(frames, Is.Not.Empty);
                Assert.That(frames, Has.None.Null);
            }
            var library = AssetDatabase.LoadAssetAtPath<Audio.SfxLibrary>("Assets/Data/SfxLibrary.asset");
            Assert.That(library.Find("fruit_ripen"), Is.Not.Null, "no sound for it ripening");
            Assert.That(library.Find("fruit_burst"), Is.Not.Null, "no sound for its burst");
            var guid = AssetDatabase.AssetPathToGUID(path);
            foreach (var scene in new[] { "Assets/Scenes/Main.unity", "Assets/Scenes/Paradise/ParadiseMain.unity" })
            {
                Assert.That(
                    File.ReadAllText(scene),
                    Does.Contain("fruitLook: {fileID: 11400000, guid: " + guid),
                    scene
                );
            }
        }

        [Test]
        public void ABuildStartedWithTheLabOptionOpensThatStation()
        {
            Assert.That(Game.DevStart.TryLabFromArguments(new[] { "Descender.exe", "-lab", "Fruit" }, out var named));
            Assert.That(named, Is.EqualTo("fruit"), "the word after it, in lower case");
            Assert.That(Game.DevStart.TryLabFromArguments(new[] { "Descender.exe", "-lab" }, out var last));
            Assert.That(last, Is.EqualTo(Game.DevStart.FirstStation), "nothing after it is the first station");
            Assert.That(Game.DevStart.TryLabFromArguments(new[] { "x.exe", "-LAB", "-screen-width" }, out var option));
            Assert.That(option, Is.EqualTo(Game.DevStart.FirstStation), "another option after it is not a station");
            Assert.That(Game.DevStart.TryLabFromArguments(new[] { "Descender.exe" }, out _), Is.False);
            Assert.That(Game.DevStart.TryLabFromArguments(null, out _), Is.False);
        }
    }
}
