using Descender.EditorTools.Build;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// A batch-mode editor run (the pre-push hook, -runTests, -executeMethod) is muted at the editor
    /// level, and the setting the editor had before is the one restored when it quits.
    /// </summary>
    public class BatchModeMuteTests
    {
        [Test]
        public void ABatchModeRunIsMutedAtTheEditorLevel()
        {
            if (!Application.isBatchMode)
            {
                Assert.Ignore("Only a command-line run is muted; the pre-push hook runs this one.");
            }
            Assert.That(EditorUtility.audioMasterMute, Is.True);
        }

        [Test]
        public void TheSettingFoundFirstIsTheOneRestored()
        {
            Assert.That(BatchModeMute.Remember(-1, false), Is.EqualTo(0), "unmuted before the run");
            Assert.That(BatchModeMute.Remember(-1, true), Is.EqualTo(1), "muted by hand before the run");
            Assert.That(
                BatchModeMute.Remember(0, true),
                Is.EqualTo(0),
                "after a domain reload the run's own mute must not be taken for the user's"
            );
        }
    }
}
