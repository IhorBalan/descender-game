using System.Collections;
using System.IO;
using Descender.Save;
using Descender.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The settings screen opened from the title: it opens on the Game tab with the
    /// focus on Gameplay's first setting; pressing a toggle flips it and shows the new
    /// state; a slider steps by a twentieth; the tabs turn to Controls with its
    /// Movement and Actions sections; Esc goes from the settings to their section and
    /// then back to the title. The settings file is put back as it was afterwards.
    /// </summary>
    public class SettingsScreenTests
    {
        private TitleScreen title;
        private SettingsScreen settings;
        private string savedFile;

        [UnitySetUp]
        public IEnumerator OpenFromTheTitle()
        {
            savedFile = File.Exists(SettingsData.DefaultPath) ? File.ReadAllText(SettingsData.DefaultPath) : null;
            SaveManager.UseDirectory(Path.Combine(Application.temporaryCachePath, "settings_tests"));
            yield return SceneManager.LoadSceneAsync("Main");
            yield return null;
            title = Object.FindAnyObjectByType<TitleScreen>();
            settings = Object.FindAnyObjectByType<SettingsScreen>();
            Assert.That(settings, Is.Not.Null, "Main.unity has no SettingsScreen");
            title.Menu.Items[3].onClick.Invoke();
            yield return null;
            Assert.That(settings.IsOpen, Is.True, "Settings on the title opens the settings");
        }

        [TearDown]
        public void PutTheSettingsBack()
        {
            if (savedFile != null)
            {
                File.WriteAllText(SettingsData.DefaultPath, savedFile);
                SettingsData.Current = SettingsData.Load(SettingsData.DefaultPath);
            }
            else if (File.Exists(SettingsData.DefaultPath))
            {
                File.Delete(SettingsData.DefaultPath);
                SettingsData.Current = new SettingsData();
            }
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator OpensOnGameplayWithTheFocusOnItsFirstSetting()
        {
            Assert.That(settings.TabIndex, Is.EqualTo(0));
            Assert.That(settings.SectionName, Is.EqualTo("Gameplay"));
            Assert.That(settings.Sections.Count, Is.EqualTo(2));
            var first = settings.Rows[0];
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(first.gameObject));
            Assert.That(settings.InfoTitle, Is.EqualTo(first.Label), "the description is the focused setting's");
            Assert.That(settings.Focus.Showing, Is.True);
            yield break;
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator PressingAToggleFlipsItAndShowsIt()
        {
            SettingsRow flashes = null;
            foreach (var row in settings.Rows)
            {
                if (row.Label == "Hit flashes")
                {
                    flashes = row;
                }
            }
            Assert.That(flashes, Is.Not.Null);
            var before = SettingsData.Current.hitFlash;
            flashes.Button.onClick.Invoke();
            yield return null;
            Assert.That(SettingsData.Current.hitFlash, Is.EqualTo(!before));
            Assert.That(flashes.ValueText, Is.EqualTo(before ? "Off" : "On"));
            settings.StepRow(flashes, -1);
            Assert.That(SettingsData.Current.hitFlash, Is.EqualTo(before), "left flips it back");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator ASliderStepsByATwentieth()
        {
            settings.ShowTab(3, true);
            yield return null;
            Assert.That(settings.SectionName, Is.EqualTo("Volume"));
            var music = settings.Rows[1];
            Assert.That(music.Type, Is.EqualTo(SettingsRow.Kind.Slider));
            SettingsData.Current.music = 0.5f;
            settings.StepRow(music, 1);
            Assert.That(SettingsData.Current.music, Is.EqualTo(0.55f).Within(1e-4));
            Assert.That(music.ValueText, Is.EqualTo("55"));
            settings.StepRow(music, -1);
            settings.StepRow(music, -1);
            Assert.That(SettingsData.Current.music, Is.EqualTo(0.45f).Within(1e-4));
            SettingsData.Current.music = 1f;
            settings.StepRow(music, 1);
            Assert.That(SettingsData.Current.music, Is.EqualTo(1f), "it stops at full");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TheTabsTurnAndControlsSplitsIntoMovementAndActions()
        {
            settings.TurnTab(1);
            yield return null;
            Assert.That(settings.TabIndex, Is.EqualTo(1));
            Assert.That(settings.Sections[0].Label, Is.EqualTo("Movement"));
            Assert.That(settings.Sections[1].Label, Is.EqualTo("Actions"));
            Assert.That(settings.Rows[0].Label, Is.EqualTo("Bindings for"));
            var keys = settings.Rows.Count - 1;
            settings.ShowTab(1, false);
            EventSystem.current.SetSelectedGameObject(settings.Sections[1].gameObject);
            settings.OnTop();
            yield return null;
            Assert.That(settings.SectionName, Is.EqualTo("Actions"), "focusing a section shows it");
            Assert.That(
                keys + settings.Rows.Count - 1,
                Is.EqualTo(SettingsData.Rebindable.Length),
                "every binding has a row"
            );
            settings.TurnTab(-2);
            Assert.That(settings.TabIndex, Is.EqualTo(3), "turning wraps round");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator EscGoesFromTheSettingsToTheirSectionThenOut()
        {
            settings.OnCancel();
            yield return null;
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(settings.Sections[0].gameObject));
            settings.OnCancel();
            yield return null;
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(title.Shown, Is.SameAs(title.Menu), "the title's menu is back");
        }
    }
}
