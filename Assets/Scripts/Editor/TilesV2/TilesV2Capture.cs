using System;
using System.Collections.Generic;
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
    /// Renders the Tiles V2 sample to PNGs at one art pixel a screen pixel: each of the
    /// map's named places (`@view`) at the game's 320 x 180, the first of them again at
    /// sunset and at night, and the whole map. From the command line (no window; not with
    /// -nographics, it renders):
    /// <code>Unity -batchmode -projectPath . -executeMethod Descender.EditorTools.TilesV2.TilesV2Capture.BuildAndCapture -tilesV2Out &lt;folder&gt;</code>
    /// which runs the generator's output through the builders first.
    /// </summary>
    public static class TilesV2Capture
    {
        /// <summary>The hours the first place is shown at besides the day's start: sunset and night.</summary>
        public static readonly (float hour, string suffix)[] ExtraHours = { (18.75f, "_sunset"), (23f, "_night") };

        // How long the particles are run before a capture, in seconds: the air is full by then.
        private const float ParticleTime = 8f;

        [MenuItem("Descender/Tiles V2/Capture sample")]
        public static void CaptureMenu()
        {
            var folder = Path.GetFullPath("Logs/TilesV2");
            Capture(folder);
            Debug.Log($"Tiles V2: captured the sample to {folder}.");
        }

        /// <summary>
        /// Batch entry: builds the tiles, and the scenes where one is missing (a scene built
        /// again is the same scene with new ids, which is noise in a diff), captures, and quits.
        /// </summary>
        public static void BuildAndCapture()
        {
            try
            {
                TilesV2Builder.Build();
                if (!File.Exists(TilesV2Sample.PlaygroundPath))
                {
                    TilesV2Sample.BuildPlayground();
                }
                if (!File.Exists(TilesV2Sample.ScenePath))
                {
                    TilesV2Sample.Build();
                }
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
            var world = UnityEngine.Object.FindAnyObjectByType<TilesV2World>();
            if (world == null)
            {
                throw new InvalidOperationException(
                    $"Tiles V2: {TilesV2Sample.ScenePath} has no world; build the scenes."
                );
            }
            world.Build();
            foreach (var system in world.GetComponentsInChildren<ParticleSystem>())
            {
                system.Simulate(ParticleTime, true, true);
            }
            BuildLightMeshes();
            // A shader still compiling in the background draws as nothing in the first frame
            // that needs it; compile in line so every capture shows every layer.
            var async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                CaptureViews(world, folder);
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
            }
        }

        private static void CaptureViews(TilesV2World world, string folder)
        {
            var game = TilesV2Backdrops.View;
            var start = world.Day.Hour;
            var first = true;
            foreach (var pair in world.Map.Views)
            {
                world.TryView(pair.Key, out var at);
                if (first)
                {
                    // The first render after the scene opens draws the unlit backdrop wrong (seen
                    // upside down in batch mode); render once and throw it away.
                    Render(world, at, game, null);
                }
                Render(world, at, game, Path.Combine(folder, pair.Key + ".png"));
                if (first)
                {
                    foreach (var (hour, suffix) in ExtraHours)
                    {
                        world.Day.Hour = hour;
                        Render(world, at, game, Path.Combine(folder, pair.Key + suffix + ".png"));
                    }
                    world.Day.Hour = start;
                }
                first = false;
            }
            var area = world.Area;
            Render(world, area.center, area.size, Path.Combine(folder, "map.png"));
        }

        // Renders a view of `size` units centred on `at`, at 16 pixels a unit, to a PNG (to nothing when `file` is null).
        private static void Render(TilesV2World world, Vector2 at, Vector2 size, string file)
        {
            var width = Mathf.RoundToInt(size.x * 16f);
            var height = Mathf.RoundToInt(size.y * 16f);
            var main = Camera.main;
            var holder = new GameObject("Tiles V2 capture") { hideFlags = HideFlags.HideAndDontSave };
            var camera = holder.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = size.y / 2f;
            camera.transform.position = new Vector3(at.x, at.y, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = main != null ? main.backgroundColor : Color.black;
            if (main != null)
            {
                main.enabled = false;
            }
            var restore = PlaceBackdrop(world, at, size);
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
                foreach (var (picture, pictureSize) in restore)
                {
                    picture.size = pictureSize;
                }
                if (main != null)
                {
                    main.enabled = true;
                }
            }
        }

        // Puts the sky and the parallax layers where a camera at `at` seeing `size` units has
        // them. A view larger than the game's needs wider layers: returns the sizes to put back.
        private static List<(SpriteRenderer, Vector2)> PlaceBackdrop(TilesV2World world, Vector2 at, Vector2 size)
        {
            var restore = new List<(SpriteRenderer, Vector2)>();
            var more = size - TilesV2Backdrops.View;
            if (world.Sky != null)
            {
                world.Sky.Follow(at, size);
            }
            foreach (var layer in world.GetComponentsInChildren<ParallaxLayer>())
            {
                var picture = layer.GetComponent<SpriteRenderer>();
                if (more.x > 0.01f || more.y > 0.01f)
                {
                    restore.Add((picture, picture.size));
                    picture.size += new Vector2(
                        layer.Repeat.x > 0f ? Mathf.Max(more.x, 0f) : 0f,
                        layer.Repeat.y > 0f ? Mathf.Max(more.y, 0f) : 0f
                    );
                }
                layer.Place(at);
                if (layer.Scroll.y <= 0f && more.y > 0.01f)
                {
                    // Pinned to the view's height (clouds): keep it by the top of this taller view.
                    layer.transform.position += new Vector3(0f, more.y / 2f, 0f);
                }
            }
            return restore;
        }

        /// <summary>
        /// A batch-mode editor never ticks, so the Light2D components never run the
        /// LateUpdate that builds their meshes and only the global light would show: run it
        /// once for each light, as the first editor frame would.
        /// </summary>
        public static void BuildLightMeshes()
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
