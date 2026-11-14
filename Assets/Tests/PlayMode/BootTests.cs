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
    /// on the Input System exist after one frame, so does the dev console (tests run as
    /// a debug build), and nothing errors on the way.
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
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            Assert.That(eventSystem, Is.Not.Null, "no EventSystem was made");
            Assert.That(eventSystem.TryGetComponent<InputSystemUIInputModule>(out _), Is.True);
            Assert.That(Audio.Music.Instance.Playing, Is.EqualTo("title"));
            Assert.That(Object.FindAnyObjectByType<UI.DevConsole>(), Is.Not.Null, "no dev console was made");
        }

        /// <summary>
        /// The lab is a throwaway game: it opens from the title with the hero in the
        /// station asked for, its saves go to a scratch folder, and leaving it points the
        /// saves back where they were.
        /// </summary>
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TheLabOpensFromTheTitleAndGivesTheSavesBack()
        {
            var saves = System.IO.Path.Combine(Application.temporaryCachePath, "boot_tests");
            Save.SaveManager.UseDirectory(saves);
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            var flow = GameFlow.Instance;
            Assert.That(flow.Current, Is.EqualTo(GameFlow.Phase.Title));

            flow.StartLab("jump");
            // The way in fades the screen on real time, so the wait is in seconds, not frames.
            var giveUpAt = Time.realtimeSinceStartup + 20f;
            while (flow.Current != GameFlow.Phase.Playing && Time.realtimeSinceStartup < giveUpAt)
            {
                yield return null;
            }

            Assert.That(flow.Current, Is.EqualTo(GameFlow.Phase.Playing), "the lab did not open");
            Assert.That(flow.InLab, Is.True);
            Assert.That(flow.Hero, Is.Not.Null);
            Assert.That(Lab.LabRoot.Current.Station.id, Is.EqualTo("jump"));
            Assert.That(Save.SaveManager.Slots.Directory, Is.Not.EqualTo(saves), "the lab saves to a scratch folder");

            flow.QuitToTitle();
            yield return null;

            Assert.That(flow.Current, Is.EqualTo(GameFlow.Phase.Title));
            Assert.That(flow.InLab, Is.False, "the lab is taken down");
            Assert.That(flow.Hero, Is.Null);
            Assert.That(Save.SaveManager.Slots.Directory, Is.EqualTo(saves), "the saves are back where they were");
        }
    }
}
