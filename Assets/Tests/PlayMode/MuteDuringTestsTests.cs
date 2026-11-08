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
    }
}
