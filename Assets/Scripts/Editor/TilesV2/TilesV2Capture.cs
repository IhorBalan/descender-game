using System;
using System.IO;
using Descender.World.TilesV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// Renders the Tiles V2 sample to PNGs, at one art pixel a screen pixel: the surface
    /// and the cave at the game's 320 x 180, and the whole map. From the command line
    /// (no window; not with -nographics, it renders):
    /// <code>Unity -batchmode -projectPath . -executeMethod Descender.EditorTools.TilesV2.TilesV2Capture.BuildAndCapture -tilesV2Out &lt;folder&gt;</code>
    /// which runs the generator's output through the builders first.
    /// </summary>
    public static class TilesV2Capture
    {
        [MenuItem("Descender/Tiles V2/Capture sample")]
        public static void CaptureMenu()
        {
            var folder = Path.GetFullPath("Logs/TilesV2");
            Capture(folder);
            Debug.Log($"Tiles V2: captured the sample to {folder}.");
        }

        /// <summary>Batch entry: builds the tiles and the scene, captures, and quits.</summary>
        public static void BuildAndCapture()
        {
            try
            {
                TilesV2Builder.Build();
                TilesV2Sample.Build();
                Capture(OutFolder());
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        public static void Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene(TilesV2Sample.ScenePath);
            BuildLightMeshes();
            // A shader still compiling in the background draws as nothing in the first frame
            // that needs it; compile in line so every capture shows every layer.
            var async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                CaptureViews(folder);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
            }
            // Put the backdrop back where the scene's camera has it.
            PlaceLayers(TilesV2Sample.SurfaceView);
        }

        private static void CaptureViews(string folder)
        {
            var (terrain, _) = TilesV2Sample.ReadMap(File.ReadAllText(TilesV2Sample.MapPath));
            var width = terrain[0].Length;
            // The first render after the scene opens draws the unlit backdrop wrong (seen
            // upside down in batch mode); render once and throw it away.
            Render(TilesV2Sample.SurfaceView, 320, 180, null);
            Render(TilesV2Sample.SurfaceView, 320, 180, Path.Combine(folder, "surface.png"));
            Render(TilesV2Sample.CaveView, 320, 180, Path.Combine(folder, "cave.png"));
            Render(TilesV2Sample.DesertView, 320, 180, Path.Combine(folder, "desert.png"));
            Render(
                new Vector2(width / 2f, terrain.Count / 2f),
                width * 16,
                terrain.Count * 16,
                Path.Combine(folder, "map.png")
            );
        }

        private static void Render(Vector2 at, int width, int height, string file)
        {
            var main = Camera.main;
            var holder = new GameObject("Tiles V2 capture") { hideFlags = HideFlags.HideAndDontSave };
            var camera = holder.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = height / 16f / 2f;
            camera.transform.position = new Vector3(at.x, at.y, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = main != null ? main.backgroundColor : Color.black;
            if (main != null)
            {
                main.enabled = false;
            }
            PlaceLayers(at);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
            };
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(camera, request);
                }
                else
                {
                    camera.targetTexture = target;
                    camera.Render();
                }
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                if (file != null)
                {
                    File.WriteAllBytes(file, image.EncodeToPNG());
                }
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(holder);
                if (main != null)
                {
                    main.enabled = true;
                }
            }
        }

        /// <summary>
        /// A batch-mode editor never ticks, so the Light2D components never run the
        /// LateUpdate that builds their meshes and only the global light would show: run it
        /// once for each light, as the first editor frame would.
        /// </summary>
        private static void BuildLightMeshes()
        {
            var update = typeof(Light2D).GetMethod(
                "LateUpdate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );
            if (update == null)
            {
                Debug.LogWarning("Tiles V2: Light2D has no LateUpdate to build its mesh; lights may not show.");
                return;
            }
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsInactive.Exclude))
            {
                update.Invoke(light, null);
            }
        }

        private static void PlaceLayers(Vector2 cameraAt)
        {
            foreach (var layer in UnityEngine.Object.FindObjectsByType<ParallaxLayer>(FindObjectsInactive.Exclude))
            {
                layer.Place(cameraAt);
            }
        }

        private static string OutFolder()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-tilesV2Out")
                {
                    return args[i + 1];
                }
            }
            return Path.GetFullPath("Logs/TilesV2");
        }
    }
}
