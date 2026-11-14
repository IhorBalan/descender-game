using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Descender.EditorTools.Build
{
    /// <summary>
    /// Runs the EditMode and then the PlayMode tests in one editor launch, for the pre-push hook
    /// (<c>tools/hooks/pre-push</c>). Unity's own <c>-runTests</c> takes one platform per launch, and
    /// starting the editor twice is most of what a warm run costs. Started with
    /// <c>-executeMethod Descender.EditorTools.Build.PrePushTests.Run -prePushResults &lt;folder&gt;</c>,
    /// it writes the same NUnit XML as <c>-runTests</c> (<c>pre-push-EditMode.xml</c> and
    /// <c>pre-push-PlayMode.xml</c>), keeps <c>pre-push-status.txt</c> current so the hook can show
    /// progress, and exits 0 only when both platforms ran tests and none failed. PlayMode does not
    /// run when EditMode fails, as in the hook's two-launch form.
    /// </summary>
    [InitializeOnLoad]
    public static class PrePushTests
    {
        public const string ResultsArgument = "-prePushResults";
        public const string StatusFileName = "pre-push-status.txt";

        // The run survives the domain reload on entering play mode through SessionState.
        private const string StageKey = "Descender.PrePushTests.Stage";
        private const string FolderKey = "Descender.PrePushTests.Folder";
        private const string ExitCodeKey = "Descender.PrePushTests.ExitCode";
        private const string TotalKey = "Descender.PrePushTests.Total";
        private const string DoneKey = "Descender.PrePushTests.Done";

        private enum Stage
        {
            Idle,
            StartEditMode,
            EditModeRunning,
            StartPlayMode,
            PlayModeRunning,
            Exit,
        }

        private static TestRunnerApi api;

        // The Test Framework keeps this internal; its own -runTests waits on it before exiting.
        private static readonly MethodInfo RunActive = typeof(TestRunnerApi).GetMethod(
            "IsRunActive",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public
        );

        static PrePushTests()
        {
            if (Current != Stage.Idle)
            {
                Attach();
            }
        }

        private static Stage Current
        {
            get => (Stage)SessionState.GetInt(StageKey, (int)Stage.Idle);
            set => SessionState.SetInt(StageKey, (int)value);
        }

        private static string Folder => SessionState.GetString(FolderKey, "Logs");

        /// <summary>The <c>-executeMethod</c> entry point. The editor stays open until both runs end.</summary>
        public static void Run()
        {
            BatchModeMute.Mute();
            var folder = "Logs";
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == ResultsArgument)
                {
                    folder = args[i + 1];
                }
            }
            if (RunActive == null)
            {
                // Fail rather than guess when a run has ended; the hook reports the exit code.
                Debug.LogError(
                    "pre-push: TestRunnerApi.IsRunActive is gone; update PrePushTests for this Test Framework."
                );
                BatchModeMute.Restore();
                EditorApplication.Exit(1);
                return;
            }
            Directory.CreateDirectory(folder);
            SessionState.SetString(FolderKey, folder);
            Current = Stage.StartEditMode;
            Attach();
        }

        /// <summary>The results file of one platform, as the hook reads it.</summary>
        public static string ResultsPath(string folder, TestMode mode)
        {
            return Path.Combine(folder, "pre-push-" + mode + ".xml");
        }

        /// <summary>
        /// Whether a finished run lets the push through: at least one test passed, and none failed
        /// or was inconclusive. A run that executed nothing is a failure, as it is for -runTests.
        /// </summary>
        public static bool Passed(int passCount, int failCount, int inconclusiveCount)
        {
            return passCount > 0 && failCount == 0 && inconclusiveCount == 0;
        }

        private static void Attach()
        {
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || RunIsActive())
            {
                return;
            }
            switch (Current)
            {
                case Stage.StartEditMode:
                    Current = Stage.EditModeRunning;
                    Execute(TestMode.EditMode);
                    break;
                case Stage.StartPlayMode:
                    if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        Current = Stage.PlayModeRunning;
                        Execute(TestMode.PlayMode);
                    }
                    break;
                case Stage.Exit:
                    if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        Current = Stage.Idle;
                        BatchModeMute.Restore();
                        EditorApplication.Exit(SessionState.GetInt(ExitCodeKey, 1));
                    }
                    break;
            }
        }

        // A run is still active after RunFinished while the framework cleans up: it leaves play
        // mode, deletes its bootstrap scene and restores the player settings it changed. Starting
        // the next run or exiting before that leaves those changes in the working tree.
        private static bool RunIsActive()
        {
            return (bool)RunActive.Invoke(null, null);
        }

        private static void Execute(TestMode mode)
        {
            WriteStatus(mode + " starting");
            Debug.Log("pre-push: running the " + mode + " tests.");
            api.Execute(new ExecutionSettings(new Filter { testMode = mode }));
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetInt(ExitCodeKey, exitCode);
            Current = Stage.Exit;
        }

        private static void WriteStatus(string status)
        {
            try
            {
                File.WriteAllText(Path.Combine(Folder, StatusFileName), status + "\n");
            }
            catch (IOException)
            {
                // The hook may be reading it; the next test writes it again.
            }
        }

        private sealed class Callbacks : IErrorCallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                SessionState.SetInt(TotalKey, testsToRun.TestCaseCount);
                SessionState.SetInt(DoneKey, 0);
                WriteProgress();
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite)
                {
                    SessionState.SetInt(DoneKey, SessionState.GetInt(DoneKey, 0) + 1);
                    WriteProgress();
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var stage = Current;
                if (stage != Stage.EditModeRunning && stage != Stage.PlayModeRunning)
                {
                    return;
                }
                var mode = stage == Stage.EditModeRunning ? TestMode.EditMode : TestMode.PlayMode;
                TestRunnerApi.SaveResultToFile(result, ResultsPath(Folder, mode));
                var passed = Passed(result.PassCount, result.FailCount, result.InconclusiveCount);
                Debug.Log(
                    $"pre-push: {mode} finished: {result.PassCount} passed, {result.FailCount} failed, "
                        + $"{result.InconclusiveCount} inconclusive, {result.SkipCount} skipped."
                );
                if (!passed)
                {
                    Finish(1);
                }
                else if (mode == TestMode.EditMode)
                {
                    Current = Stage.StartPlayMode;
                }
                else
                {
                    Finish(0);
                }
            }

            public void OnError(string message)
            {
                Debug.LogError("pre-push: the test run could not start: " + message);
                Finish(1);
            }

            private static void WriteProgress()
            {
                var mode = Current == Stage.PlayModeRunning ? TestMode.PlayMode : TestMode.EditMode;
                WriteStatus($"{mode} {SessionState.GetInt(DoneKey, 0)}/{SessionState.GetInt(TotalKey, 0)}");
            }
        }
    }
}
