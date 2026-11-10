using Descender.Save;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// The run this test is part of is muted: <see cref="MuteDuringTests"/> turned the mute on before
    /// the first test, and the listener is at zero while the tests play.
    /// </summary>
    public class MuteDuringTestsTests
    {
        [Test]
        public void TheRunIsMuted()
        {
            Assert.That(SettingsApplier.Muted, Is.True, "MuteDuringTests did not run before the tests");
            Assert.That(AudioListener.volume, Is.EqualTo(0f));
        }

        [Test]
        public void TheRunHasNoHitStops()
        {
            Assert.That(Fx.Feedback.HitStopOff, Is.True, "MuteDuringTests did not run before the tests");
            var before = Time.timeScale;
            Fx.Feedback.HitStop(0.1f);
            Assert.That(Time.timeScale, Is.EqualTo(before), "a hit-stop slowed the run");
            Assert.That(Fx.Feedback.IsHitStopped, Is.False);
        }
    }
}
