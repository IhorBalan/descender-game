using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// A backup for <see cref="MuteDuringTests"/> and the batch-mode silence in
    /// <c>SettingsApplier</c>: before the first fixture of this namespace runs, the listener is put
    /// at zero, so no PlayMode test can play through the speakers even if both of those miss.
    /// </summary>
    [SetUpFixture]
    public sealed class PlayModeAudioOff
    {
        [OneTimeSetUp]
        public void Silence()
        {
            AudioListener.volume = 0f;
        }
    }
}
