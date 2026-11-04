using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Descender.EditorTools.Build
{
    /// <summary>
    /// Descender &gt; Build &gt; Windows (new Paradise): a Windows player that boots into
    /// ParadiseMain instead of Main. The build settings keep their order; only this build
    /// puts the Paradise entry first. From the command line:
    /// <c>Unity -batchmode -quit -projectPath . -executeMethod Descender.EditorTools.Build.ParadiseBuild.Windows</c>
    /// </summary>
    public static class ParadiseBuild
    {
        private const string EntryPath = "Assets/Scenes/Paradise/ParadiseMain.unity";
        private const string OutputPath = "Build/ParadiseWindows64/Descender.exe";

        [MenuItem("Descender/Build/Windows (new Paradise)")]
        public static void Windows()
        {
            var scenes = EditorBuildSettings
                .scenes.Where(s => s.enabled && s.path != EntryPath)
                .Select(s => s.path)
                .Prepend(EntryPath)
                .ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log(
                $"Paradise build: {summary.result}, {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime}"
            );
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
