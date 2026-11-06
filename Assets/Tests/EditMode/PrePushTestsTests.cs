using System.IO;
using Descender.EditorTools.Build;
using NUnit.Framework;
using UnityEditor.TestTools.TestRunner.Api;

namespace Descender.Tests.EditMode
{
    /// <summary>
    /// The rule the pre-push runner uses to let a push through, and the result files the hook
    /// reads. A run with a failure, an inconclusive test or no test at all must not pass.
    /// </summary>
    public class PrePushTestsTests
    {
        [Test]
        public void ARunPassesOnlyWithTestsAndNoFailures()
        {
            Assert.That(PrePushTests.Passed(77, 0, 0), Is.True);
            Assert.That(PrePushTests.Passed(76, 1, 0), Is.False, "a failed test must block the push");
            Assert.That(PrePushTests.Passed(76, 0, 1), Is.False, "an inconclusive test must block the push");
            Assert.That(PrePushTests.Passed(0, 0, 0), Is.False, "a run that executed no test must block the push");
        }

        [Test]
        public void ResultFilesAreNamedAsTheHookReadsThem()
        {
            Assert.That(
                PrePushTests.ResultsPath("Logs", TestMode.EditMode).Replace(Path.DirectorySeparatorChar, '/'),
                Is.EqualTo("Logs/pre-push-EditMode.xml")
            );
            Assert.That(
                PrePushTests.ResultsPath("Logs", TestMode.PlayMode).Replace(Path.DirectorySeparatorChar, '/'),
                Is.EqualTo("Logs/pre-push-PlayMode.xml")
            );
        }
    }
}
