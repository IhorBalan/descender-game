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
    }
}
