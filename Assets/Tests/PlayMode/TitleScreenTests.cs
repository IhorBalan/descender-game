using System.Collections;
using Descender.Game;
using Descender.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The title screen in Main.unity: it opens on the main column, scaled smoothly
    /// from its 1920 x 1080 layout, the focus bar sits on New Game, glides to an item the focus moves to
    /// and lands on its middle, and moves with the focus into the slot picker.
    /// </summary>
    public class TitleScreenTests
    {
        private TitleScreen title;

        [UnitySetUp]
        public IEnumerator OpenTheTitle()
        {
            Save.SaveManager.UseDirectory(System.IO.Path.Combine(Application.temporaryCachePath, "title_tests"));
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            title = Object.FindAnyObjectByType<TitleScreen>();
            Assert.That(title, Is.Not.Null, "Main.unity has no TitleScreen");
            Assert.That(GameFlow.Instance.Current, Is.EqualTo(GameFlow.Phase.Title));
            yield return null;
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator OpensOnTheMenuScaledSmoothlyFrom1080WithTheBarOnNewGame()
        {
            Assert.That(title.IsOpen, Is.True);
            Assert.That(title.Shown, Is.SameAs(title.Menu));
            Assert.That(title.Canvas.pixelPerfect, Is.False, "the title is drawn smooth, not snapped to pixels");
            var scaler = title.Canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(TitleKit.Width, TitleKit.Height)));
            Assert.That(
                scaler.screenMatchMode,
                Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand),
                "all of it stays in view"
            );
            Assert.That(TitleKit.Font, Is.Not.Null, "the title font came from the theme");

            var newGame = title.Menu.Items[0].gameObject;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(newGame));
            Assert.That(title.Focus.Showing, Is.True);
            Assert.That(title.Focus.Middle, Is.EqualTo(title.Menu.MiddleOf(newGame)));
            yield break;
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TheBarGlidesToTheFocusedItemAndLandsOnIt()
        {
            var from = title.Focus.Middle;
            var settings = title.Menu.Items[3].gameObject;
            var target = title.Menu.MiddleOf(settings);
            Assert.That(target, Is.GreaterThan(from));
            EventSystem.current.SetSelectedGameObject(settings);
            var between = false;
            var giveUpAt = Time.realtimeSinceStartup + 3f;
            while (title.Focus.Middle != target && Time.realtimeSinceStartup < giveUpAt)
            {
                yield return null;
                var y = title.Focus.Middle;
                Assert.That(y, Is.InRange(from, target), "the bar never overshoots");
                between |= y > from && y < target;
            }
            Assert.That(between, Is.True, "the bar glides rather than jumps");
            Assert.That(title.Focus.Middle, Is.EqualTo(target), "the bar lands on the item's middle");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator NewGameMovesTheFocusAndTheBarIntoTheSlotPicker()
        {
            title.Menu.Items[0].onClick.Invoke();
            yield return null;
            Assert.That(title.Shown, Is.Not.SameAs(title.Menu));
            Assert.That(title.Menu.Root.gameObject.activeSelf, Is.False, "the menu hides");
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(title.Shown.Contains(selected), Is.True, "the focus is in the slot picker");
            Assert.That(title.Focus.transform.parent, Is.SameAs(title.Shown.Root));
            Assert.That(title.Focus.Showing, Is.True);
            Assert.That(title.Focus.Middle, Is.EqualTo(title.Shown.MiddleOf(selected)));

            title.OnCancel();
            yield return null;
            Assert.That(title.Shown, Is.SameAs(title.Menu), "Cancel goes back to the menu");
        }
    }
}
