using System.Collections;
using System.IO;
using Descender.Economy;
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
    /// The pause menu, the camp and the pack in the gilded look: each is scaled
    /// smoothly from 1080 over a veil, opens with the focus on its first item and the
    /// bar under it, the pause menu's quit asks before it quits and forgets the
    /// question when the focus leaves, the camp's note follows the focus, and the
    /// pack lists what is carried with the focused row lit.
    /// </summary>
    public class GildedMenuTests
    {
        [UnitySetUp]
        public IEnumerator LoadMain()
        {
            Save.SaveManager.UseDirectory(Path.Combine(Application.temporaryCachePath, "gilded_menu_tests"));
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            Inventory.Reset();
            Object.FindAnyObjectByType<TitleScreen>().Close();
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator ThePauseMenuOpensOnResumeAndQuitAsksFirst()
        {
            var pause = Object.FindAnyObjectByType<PauseScreen>();
            pause.Open();
            yield return null;
            var scaler = pause.Canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(TitleKit.Width, TitleKit.Height)));
            Assert.That(pause.Menu.Items.Count, Is.EqualTo(8));
            var resume = pause.Menu.Items[0];
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(resume.gameObject));
            Assert.That(pause.Focus.Showing, Is.True);
            Assert.That(pause.Focus.Middle, Is.EqualTo(pause.Menu.MiddleOf(resume.gameObject)));
            Assert.That(pause.StatusText, Does.Contain("0:00"));

            var quit = pause.Menu.Items[7];
            EventSystem.current.SetSelectedGameObject(quit.gameObject);
            yield return null;
            quit.onClick.Invoke();
            Assert.That(pause.IsOpen, Is.True, "the first press only asks");
            Assert.That(
                quit.GetComponent<TitleItem>().Text,
                Is.EqualTo(PauseScreen.QuitConfirmText.ToUpperInvariant())
            );
            EventSystem.current.SetSelectedGameObject(resume.gameObject);
            pause.OnTop();
            Assert.That(
                quit.GetComponent<TitleItem>().Text,
                Is.EqualTo(PauseScreen.QuitText.ToUpperInvariant()),
                "moving away forgets the question"
            );
            pause.Close();
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TheCampsNoteFollowsTheFocus()
        {
            var camp = new GameObject("Camp").AddComponent<CampScreen>();
            camp.Open(null, 1, 4);
            yield return null;
            Assert.That(camp.Menu.Items.Count, Is.EqualTo(5));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(camp.Menu.Items[0].gameObject));
            yield return null;
            StringAssert.Contains("What he carries", camp.NoteText);
            EventSystem.current.SetSelectedGameObject(camp.Menu.Items[4].gameObject);
            yield return null;
            Assert.That(camp.NoteText, Is.EqualTo("Back to the descent."));
            camp.Close();
            Object.Destroy(camp.gameObject);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator ThePackListsWhatIsCarriedWithTheFocusedRowLit()
        {
            foreach (var ware in Shops.List[0].Stock)
            {
                Inventory.Add(ware.Item);
            }
            var pack = new GameObject("Pack").AddComponent<InventoryScreen>();
            pack.Open();
            yield return null;
            var lines = pack.Lines();
            Assert.That(lines.Count, Is.EqualTo(Shops.List[0].Stock.Length));
            StringAssert.Contains(Items.DisplayName(Shops.List[0].Stock[0].Item), lines[0]);
            var first = pack.RowsShown[0];
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(first.gameObject));
            Assert.That(first.GetComponent<UiButtonLook>().Lit, Is.True);
            Assert.That(
                first.transform.Find("Bar").GetComponent<Image>().enabled,
                Is.True,
                "the focused row's bar shows"
            );
            var second = pack.RowsShown[1];
            Assert.That(second.transform.Find("Bar").GetComponent<Image>().enabled, Is.False);
            pack.Close();
            Object.Destroy(pack.gameObject);
        }
    }
}
