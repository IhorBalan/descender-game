using Descender.Save;
using NUnit.Framework.Interfaces;
using UnityEngine.TestRunner;
using UnityEngine.TestTools;

[assembly: TestRunCallback(typeof(Descender.Tests.PlayMode.MuteDuringTests))]

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// Keeps a test run silent, and on the game's own clock. The Test Framework calls this at the start and end of every run
    /// (the Test Runner window, <c>-runTests</c> and the pre-push hook alike), and the PlayMode tests
    /// boot Main.unity, whose title music would otherwise play through the developer's speakers.
    /// The mute goes through <see cref="SettingsApplier.Muted"/>, so the settings the game applies
    /// on boot cannot turn the volume back up mid-run. Hit-stops are off for the run
    /// (<see cref="Fx.Feedback.HitStopOff"/>): they end on the real clock, so one left by a test
    /// slowed the frames the next test counted, more of them the faster the machine.
    /// </summary>
    public sealed class MuteDuringTests : ITestRunCallback
    {
        public void RunStarted(ITest testsToRun)
        {
            SettingsApplier.Muted = true;
            Fx.Feedback.HitStopOff = true;
        }

        public void RunFinished(ITestResult testResults)
        {
            SettingsApplier.Muted = false;
            Fx.Feedback.HitStopOff = false;
        }

        public void TestStarted(ITest test) { }

        public void TestFinished(ITestResult result) { }
    }
}
