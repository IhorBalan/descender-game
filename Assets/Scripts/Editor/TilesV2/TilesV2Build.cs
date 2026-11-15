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

        /// <summary>The Windows player of the world: Paradise, the Earth and the nine circles, as a journey.</summary>
        public const string WorldOutputPath = "Build/TilesV2World64/DescenderWorld.exe";

        [MenuItem("Descender/Tiles V2/Build Windows playground")]
        public static void Windows() => Build(TilesV2Sample.PlaygroundPath, OutputPath);

        /// <summary>
        /// Descender &gt; Tiles V2 &gt; Build Windows world: a Windows player of the world scene alone
        /// (Assets/Scenes/Sandbox/TilesV2Descent.unity). From the command line:
        /// <c>Unity -batchmode -quit -projectPath . -buildTarget win64 -executeMethod Descender.EditorTools.TilesV2.TilesV2Build.World</c>
        /// </summary>
        [MenuItem("Descender/Tiles V2/Build Windows world")]
        public static void World() => Build(TilesV2Sample.DescentPath, WorldOutputPath);

        /// <summary>
        /// The Paradise level as a Windows player:
        /// <c>Unity -batchmode -quit -projectPath . -buildTarget win64 -executeMethod Descender.EditorTools.TilesV2.TilesV2Build.Paradise</c>
        /// </summary>
        public const string ParadiseOutputPath = "Build/TilesV2Paradise64/DescenderParadise.exe";

        public static void Paradise()
        {
            TilesV2Sample.Ensure(TilesV2Sample.ParadisePath);
            Build(TilesV2Sample.ParadisePath, ParadiseOutputPath);
        }

        private static void Build(string scene, string output)
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log(
                $"Tiles V2 build of {scene}: {summary.result}, {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, {summary.totalTime}, at {output}"
            );
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
