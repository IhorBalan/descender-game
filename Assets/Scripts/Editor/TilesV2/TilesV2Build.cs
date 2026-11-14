using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// Descender &gt; Tiles V2 &gt; Build Windows playground: a Windows player of the Tiles V2
    /// playground alone (Assets/Scenes/Sandbox/TilesV2Playground.unity), for trying the new
    /// map graphics with the hero. The scene is named here and nowhere else: the build
    /// settings, and so the game's own builds, never hear of it. From the command line:
    /// <c>Unity -batchmode -quit -projectPath . -buildTarget win64 -executeMethod Descender.EditorTools.TilesV2.TilesV2Build.Windows</c>
    /// </summary>
    public static class TilesV2Build
    {
        public const string OutputPath = "Build/TilesV2Windows64/DescenderTilesV2.exe";

        [MenuItem("Descender/Tiles V2/Build Windows playground")]
        public static void Windows()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { TilesV2Sample.PlaygroundPath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log(
                $"Tiles V2 playground build: {summary.result}, {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime}, at {OutputPath}"
            );
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
