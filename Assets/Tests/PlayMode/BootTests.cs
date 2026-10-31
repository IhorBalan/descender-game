using System.Collections;
using Descender.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// Main.unity boots to the title: the flow, the input router and an EventSystem
    /// on the Input System exist after one frame, and nothing errors on the way.
    /// </summary>
    public class BootTests
    {
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator MainSceneBootsToTheTitle()
        {
            Save.SaveManager.UseDirectory(System.IO.Path.Combine(Application.temporaryCachePath, "boot_tests"));
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            Assert.That(GameFlow.Instance, Is.Not.Null, "Main.unity has no GameFlow");
            Assert.That(GameFlow.Instance.Current, Is.EqualTo(GameFlow.Phase.Title));
            Assert.That(Input.InputRouter.Instance, Is.Not.Null, "Main.unity has no InputRouter");
            Assert.That(World.WorldStreamer.Instance, Is.Not.Null, "Main.unity has no WorldStreamer");
            Assert.That(Audio.Music.Instance, Is.Not.Null, "Main.unity has no Music");
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            Assert.That(eventSystem, Is.Not.Null, "no EventSystem was made");
            Assert.That(eventSystem.TryGetComponent<InputSystemUIInputModule>(out _), Is.True);
            Assert.That(Audio.Music.Instance.Playing, Is.EqualTo("title"));
        }
    }
}
