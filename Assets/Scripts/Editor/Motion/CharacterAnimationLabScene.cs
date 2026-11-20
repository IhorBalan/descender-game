using System.IO;
using Descender.Motion;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.EditorTools.Motion
{
    /// <summary>
    /// Descender &gt; Lab &gt; Character animation lab: makes Assets/Scenes/Sandbox/
    /// CharacterAnimationLab.unity, the scene where the procedural run (<see cref="RunSkeleton"/>)
    /// is tried on its own: a camera, the runner, its <see cref="RunDebugOverlay"/> and the
    /// <see cref="CharacterAnimationLab"/> that keeps them together. The scene is not in the
    /// build's scene list and nothing of the game is in it. Descender &gt; Lab &gt; Build
    /// character animation lab makes a Windows player of it alone. From the command line:
    /// <c>Unity -batchmode -quit -projectPath . -executeMethod Descender.EditorTools.Motion.CharacterAnimationLabScene.Create</c>
    /// (and <c>-buildTarget win64 ... CharacterAnimationLabScene.Windows</c> for the player).
    /// </summary>
    public static class CharacterAnimationLabScene
    {
        public const string ScenePath = "Assets/Scenes/Sandbox/CharacterAnimationLab.unity";
        public const string OutputPath = "Build/CharacterAnimationLab64/CharacterAnimationLab.exe";

        [MenuItem("Descender/Lab/Character animation lab")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var holder = new GameObject("Main Camera") { tag = "MainCamera" };
            holder.transform.position = new Vector3(0.6f, 1.3f, -10f);
            var camera = holder.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.1f, 0.14f);
            camera.GetUniversalAdditionalCameraData();

            var lab = new GameObject("CharacterAnimationLab").AddComponent<CharacterAnimationLab>();
            var runner = new GameObject("Runner").AddComponent<RunSkeleton>();
            runner.transform.SetParent(lab.transform, false);
            var overlay = new GameObject("Debug Overlay").AddComponent<RunDebugOverlay>();
            overlay.transform.SetParent(lab.transform, false);
            overlay.Bind(runner);
            lab.Configure(runner, overlay, camera);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = runner;
            Debug.Log($"Character animation lab: {ScenePath}");
        }

        /// <summary>
        /// Descender &gt; Lab &gt; Picture the run's key poses: the eight key poses side by side,
        /// and under them sixteen moments of the cycle, to Logs/CharacterAnimationLab/run.png.
        /// </summary>
        [MenuItem("Descender/Lab/Picture the run's key poses")]
        public static void Picture()
        {
            const int Cell = 240;
            const int Rows = 2;
            const int Columns = 16;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var holder = new GameObject("Camera");
            var camera = holder.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 1.45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.1f, 0.14f);
            camera.GetUniversalAdditionalCameraData();
            holder.transform.position = new Vector3(0.1f, 1.3f, -10f);
            var runner = new GameObject("Runner").AddComponent<RunSkeleton>();
            var ground = Fx.Shapes.Block(
                null,
                "Ground",
                new Vector2(160f, 2f),
                new Vector2(0f, -1f),
                new Color(0.4f, 0.45f, 0.55f)
            );
            var target = new RenderTexture(Cell, Cell, 24);
            camera.targetTexture = target;
            var sheet = new Texture2D(Cell * Columns, Cell * Rows, TextureFormat.RGB24, false);
            for (var row = 0; row < Rows; row++)
            {
                var count = row == 0 ? RunGait.KeyCount : Columns;
                for (var i = 0; i < count; i++)
                {
                    runner.ShowAt(i / (float)count);
                    camera.Render();
                    RenderTexture.active = target;
                    sheet.ReadPixels(
                        new Rect(0, 0, Cell, Cell),
                        i * Cell * (row == 0 ? 2 : 1),
                        (Rows - 1 - row) * Cell
                    );
                }
            }
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            sheet.Apply();
            var path = Path.Combine("Logs", "CharacterAnimationLab", "run.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(ground.gameObject);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log($"Character animation lab: the run pictured at {path} ({scene.name})");
        }

        [MenuItem("Descender/Lab/Build character animation lab")]
        public static void Windows()
        {
            if (!File.Exists(ScenePath))
            {
                Create();
            }
            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = OutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                }
            );
            var summary = report.summary;
            Debug.Log(
                $"Character animation lab build: {summary.result}, {summary.totalErrors} errors, at {OutputPath}"
            );
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }
    }
}
