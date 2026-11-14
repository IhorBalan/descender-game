using Descender.Save;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The master volume the applier puts on the listener: the settings' value normally, and zero
    /// for as long as the test mute is on, even when the settings are applied again meanwhile.
    /// </summary>
    public class SettingsApplierTests
    {
        private bool mutedBefore;
        private float volumeBefore;

        [SetUp]
        public void Remember()
        {
            mutedBefore = SettingsApplier.Muted;
            volumeBefore = AudioListener.volume;
        }

        [TearDown]
        public void Restore()
        {
            SettingsApplier.Muted = mutedBefore;
            AudioListener.volume = volumeBefore;
        }

        [Test]
        public void MutedKeepsTheListenerSilentThroughAnApply()
        {
            var settings = new SettingsData { volume = 0.8f };

            SettingsApplier.Muted = true;
            Assert.That(AudioListener.volume, Is.EqualTo(0f), "turning the mute on silences the listener at once");
            SettingsApplier.Apply(settings);
            Assert.That(AudioListener.volume, Is.EqualTo(0f), "applying the settings while muted must stay silent");
            Assert.That(SettingsApplier.MasterVolume(settings), Is.EqualTo(0f));

            SettingsApplier.Muted = false;
            SettingsApplier.Apply(settings);
            var expected = Application.isBatchMode ? 0f : 0.8f;
            Assert.That(
                AudioListener.volume,
                Is.EqualTo(expected).Within(0.001f),
                "unmuted, the settings win again, except in a batch-mode run"
            );
            Assert.That(SettingsApplier.MasterVolume(settings), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void SilentGivesZeroAndOtherwiseTheSettingsVolume()
        {
            var settings = new SettingsData { volume = 0.8f };

            Assert.That(SettingsApplier.MasterVolume(settings, silent: true), Is.EqualTo(0f));
            Assert.That(SettingsApplier.MasterVolume(settings, silent: false), Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void ABatchModeRunStaysSilentWhenUnmuted()
        {
            if (!Application.isBatchMode)
            {
                Assert.Ignore("Only a command-line run is silenced this way; the pre-push hook runs this one.");
            }
            SettingsApplier.Muted = false;
            SettingsApplier.Apply(new SettingsData { volume = 1f });
            Assert.That(AudioListener.volume, Is.EqualTo(0f));
        }
    }
}
