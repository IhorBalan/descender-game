using System.Collections;
using Descender.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The dialogue pipeline is wired: both entry scenes boot with a runner and a
    /// box and the level's dialogue loaded, and an interactable is heard by the
    /// runner exactly while it is enabled, whichever of the two came first.
    /// </summary>
    public class DialogueFlowTests
    {
        [UnityTest]
        public IEnumerator AnInteractableIsHeardWhileItIsEnabled()
        {
            var rig = DialogueRig.Make();
            var early = new GameObject("Early").AddComponent<Npc>();
            try
            {
                Assert.That(rig.Runner.Hears(early), Is.True, "enabled after the runner: it registers itself");
                early.gameObject.SetActive(false);
                Assert.That(rig.Runner.Hears(early), Is.False, "disabled: forgotten");
                early.gameObject.SetActive(true);
                Assert.That(rig.Runner.Hears(early), Is.True);

                // A runner that starts after the things of a scene listens to them too.
                rig.Runner.enabled = false;
                Assert.That(DialogueRunner.Current, Is.Null);
                Assert.That(rig.Runner.Hears(early), Is.False);
                rig.Runner.enabled = true;
                Assert.That(DialogueRunner.Current, Is.SameAs(rig.Runner));
                Assert.That(rig.Runner.Hears(early), Is.True, "enabled before the runner: the runner picks it up");

                Object.Destroy(early.gameObject);
                yield return null;
                Assert.That(Interactable.Enabled, Has.No.Member(early), "a destroyed interactable leaves the list");
            }
            finally
            {
                if (early != null)
                {
                    Object.Destroy(early.gameObject);
                }
                rig.Dispose();
            }
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator EntrySceneOfTheNewParadiseHasARunnerAndABox()
        {
            yield return HasARunnerAndABox("ParadiseMain");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator MainSceneHasARunnerAndABox()
        {
            yield return HasARunnerAndABox("Main");
        }

        private static IEnumerator HasARunnerAndABox(string scene)
        {
            Save.SaveManager.UseDirectory(System.IO.Path.Combine(Application.temporaryCachePath, "boot_tests"));
            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
            var runner = Object.FindAnyObjectByType<DialogueRunner>();
            Assert.That(runner, Is.Not.Null, $"{scene}.unity has no DialogueRunner");
            Assert.That(Object.FindAnyObjectByType<DialogueBox>(), Is.Not.Null, $"{scene}.unity has no DialogueBox");
            Assert.That(DialogueRunner.Current, Is.SameAs(runner));
            Assert.That(runner.Dialogue, Is.Not.Null, "the runner loaded no dialogue file");
            Assert.That(runner.Dialogue.HasEntry("opening"), Is.True, "the runner does not hold the opening entry");
        }
    }
}
