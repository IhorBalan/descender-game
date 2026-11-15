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
    /// map's named places (`@view`) as the camera frames it at the day's start, each of its
    /// pictures (`@shot`: a place at an hour), and the whole map. From the command line (no
    /// window; not with -nographics, it renders):
    /// <code>Unity -batchmode -projectPath . -executeMethod Descender.EditorTools.TilesV2.TilesV2Capture.BuildAndCapture -tilesV2Out &lt;folder&gt;</code>
    /// which runs the generator's output through the builders first.
    /// </summary>
    public static class TilesV2Capture
    {
        // How long the particles are run before a capture, in seconds: the air is full by then.
        private const float ParticleTime = 8f;

        // The longest side of a picture the capture asks a texture for, in pixels.
        private const float WidestPicture = 8192f;

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

        /// <summary>
        /// Batch entry for the world: builds the tiles, the world scene where it is missing,
        /// captures its named places and the whole of it, and quits.
        /// </summary>
        public static void BuildAndCaptureWorld()
        {
            try
            {
                TilesV2Builder.Build();
                if (!File.Exists(TilesV2Sample.DescentPath))
                {
                    TilesV2Sample.BuildDescent();
                }
                Capture(OutFolder(), TilesV2Sample.DescentPath);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        public static void Capture(string folder) => Capture(folder, TilesV2Sample.ScenePath);

        /// <summary>Captures the Tiles V2 world of a scene: its named places, its pictures and the whole map.</summary>
        public static void Capture(string folder, string scenePath)
        {
            Directory.CreateDirectory(folder);
            var world = Prepare(scenePath);
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

        /// <summary>
        /// Batch entry for tools/tiles_v2/photo_world.py: photographs the whole of a world at
        /// every art pixel (or fewer: <c>-tilesV2Pixels 8</c>), in pieces a texture can hold,
        /// with a list of them for the tool to join, and quits. <c>-tilesV2Out</c> is where,
        /// <c>-tilesV2Scene</c> the scene (the world's by default), <c>-tilesV2Hour</c> the hour.
        /// </summary>
        public static void PhotoWorld()
        {
            try
            {
                TilesV2Builder.Build();
                if (!File.Exists(TilesV2Sample.DescentPath))
                {
                    TilesV2Sample.BuildDescent();
                }
                if (!File.Exists(TilesV2Sample.ScenePath))
                {
                    TilesV2Sample.Build();
                }
                var pixels = int.TryParse(Argument("-tilesV2Pixels"), out var asked) ? Mathf.Clamp(asked, 1, 16) : 16;
                var hour = float.TryParse(
                    Argument("-tilesV2Hour"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var at
                )
                    ? at
                    : -1f;
                Photograph(OutFolder(), Argument("-tilesV2Scene") ?? TilesV2Sample.DescentPath, pixels, hour);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>The side of a piece of a photograph of a whole world, in tiles.</summary>
        public const int PhotoPiece = 120;

        /// <summary>The list of a photograph's pieces, beside them.</summary>
        public const string PhotoIndexName = "photo.json";

        /// <summary>
        /// Photographs the whole world of a scene into `folder`: pieces of <see cref="PhotoPiece"/>
        /// tiles a side at `pixels` pixels a tile, named <c>piece_row_column.png</c> from the top
        /// left, and <see cref="PhotoIndexName"/>, which says how they join and where the
        /// regions are. The backdrop is placed once, for a view of the whole map, so the
        /// pieces join with no seam. `hour` below zero keeps the scene's own hour.
        /// </summary>
        public static int Photograph(string folder, string scenePath, int pixels, float hour)
        {
            Directory.CreateDirectory(folder);
            var world = Prepare(scenePath);
            if (hour >= 0f)
            {
                ShowHour(world, hour);
            }
            var map = world.Map;
            var area = world.Area;
            var index = new PhotoIndex
            {
                width = map.Width,
                height = map.Height,
                pixels = pixels,
                piece = PhotoPiece,
                hour = world.Day.Hour,
            };
            foreach (var mark in map.Marks)
            {
                index.marks.Add(
                    new PhotoMark
                    {
                        kind = mark.Kind,
                        column = mark.Column,
                        row = mark.Row,
                        words = string.Join(" ", mark.Rest),
                    }
                );
            }
            for (var i = 0; i < map.Regions.Count; i++)
            {
                var region = world.Set.RegionOf(map.Regions[i].Key);
                index.regions.Add(
                    new PhotoRegion
                    {
                        key = map.Regions[i].Key,
                        name = region != null ? region.name : map.Regions[i].Key,
                        top = map.Regions[i].Top,
                        end = map.RegionEnd(i),
                    }
                );
            }
            var async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var restore = new List<(SpriteRenderer, Vector2)>();
            // The sky is painted for a view; stretched over a whole world its bands are a chequer.
            // A photograph has a plain sky of the hour's colour instead.
            var day = world.Set.DayAt(world.Day.Hour);
            photoSky = Color.Lerp(day.top, day.low, 0.45f);
            var skyShown = world.Sky != null && world.Sky.gameObject.activeSelf;
            try
            {
                // The first render after the scene opens draws the unlit backdrop wrong; throw one away.
                Render(world, world.Framed(area.center), world.Set.View, null);
                restore = PlaceBackdrop(world, area.center, area.size);
                if (world.Sky != null)
                {
                    world.Sky.gameObject.SetActive(false);
                }
                for (var row = 0; row * PhotoPiece < map.Height; row++)
                {
                    for (var column = 0; column * PhotoPiece < map.Width; column++)
                    {
                        var wide = Mathf.Min(PhotoPiece, map.Width - column * PhotoPiece);
                        var tall = Mathf.Min(PhotoPiece, map.Height - row * PhotoPiece);
                        var centre = new Vector2(
                            area.xMin + column * PhotoPiece + wide / 2f,
                            area.yMax - row * PhotoPiece - tall / 2f
                        );
                        var name = $"piece_{row:00}_{column:00}.png";
                        Render(world, centre, new Vector2(wide, tall), Path.Combine(folder, name), pixels, false);
                        index.pieces.Add(name);
                    }
                }
            }
            finally
            {
                foreach (var (picture, size) in restore)
                {
                    picture.size = size;
                }
                if (world.Sky != null)
                {
                    world.Sky.gameObject.SetActive(skyShown);
                }
                photoSky = null;
                ShaderUtil.allowAsyncCompilation = async;
            }
            File.WriteAllText(Path.Combine(folder, PhotoIndexName), JsonUtility.ToJson(index, true));
            Debug.Log(
                $"Tiles V2: photographed {map.Width} x {map.Height} tiles in {index.pieces.Count} pieces to {folder}."
            );
            return index.pieces.Count;
        }

        /// <summary>What <see cref="Photograph"/> writes beside its pieces.</summary>
        [Serializable]
        public sealed class PhotoIndex
        {
            public int width;
            public int height;
            public int pixels;
            public int piece;
            public float hour;
            public List<string> pieces = new List<string>();
            public List<PhotoRegion> regions = new List<PhotoRegion>();
            public List<PhotoMark> marks = new List<PhotoMark>();
        }

        /// <summary>One of the map's marks in a photograph: what it is and where.</summary>
        [Serializable]
        public sealed class PhotoMark
        {
            public string kind;
            public int column;
            public int row;
            public string words;
        }

        // The colour a photograph's pieces are cleared to, while one is being taken.
        private static Color? photoSky;

        /// <summary>A region in a photograph: its rows.</summary>
        [Serializable]
        public sealed class PhotoRegion
        {
            public string key;
            public string name;
            public int top;
            public int end;
        }

        // Opens a scene and makes its world ready to be photographed: built, the game's things
        // standing, the hero out of the picture, the air full, the lights' meshes made.
        private static TilesV2World Prepare(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath);
            var world = UnityEngine.Object.FindAnyObjectByType<TilesV2World>();
            if (world == null)
            {
                throw new InvalidOperationException($"Tiles V2: {scenePath} has no world; build the scenes.");
            }
            world.Build();
            if (world.Map.Marks.Count > 0)
            {
                // The game's things the map marks stand in a capture as they do in play.
                TilesV2Things.Build(
                    world,
                    AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.CheckpointPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.AbilityPath),
                    AssetDatabase.LoadAssetAtPath<GameObject>(TilesV2Sample.UpgradePath)
                );
            }
            var hero = UnityEngine.Object.FindAnyObjectByType<Descender.Player.PlayerController>();
            if (hero != null)
            {
                hero.gameObject.SetActive(false);
            }
            foreach (var system in world.GetComponentsInChildren<ParticleSystem>())
            {
                system.Simulate(ParticleTime, true, true);
            }
            BuildLightMeshes();
            return world;
        }

        // Sets the hour, and shows what is not the same at every hour (a rainbow, the light of
        // windows) as it is at that one: in the editor nothing does it frame by frame.
        private static void ShowHour(TilesV2World world, float hour)
        {
            world.Day.Hour = hour;
            foreach (var shimmer in world.GetComponentsInChildren<TilesV2Shimmer>(true))
            {
                shimmer.Apply(hour);
            }
        }

        private static void CaptureViews(TilesV2World world, string folder)
        {
            var game = world.Set.View;
            var start = world.Day.Hour;
            // The first render after the scene opens draws the unlit backdrop wrong (seen upside
            // down in batch mode); render once and throw it away.
            Render(world, world.Framed(world.Area.center), game, null);
            foreach (var pair in world.Map.Views)
            {
                world.TryView(pair.Key, out var at);
                Render(world, world.Framed(at), game, Path.Combine(folder, pair.Key + ".png"));
            }
            foreach (var shot in world.Map.Shots)
            {
                if (world.TryView(shot.View, out var at))
                {
                    ShowHour(world, shot.Hour);
                    Render(world, world.Framed(at), game, Path.Combine(folder, shot.Name + ".png"));
                }
            }
            ShowHour(world, start);
            var area = world.Area;
            // The whole map, at as many pixels a tile as a texture can hold.
            var most = Mathf.Max(area.width, area.height);
            var pixels =
                most * 16f <= WidestPicture ? 16f
                : most * 8f <= WidestPicture ? 8f
                : 4f;
            Render(world, area.center, area.size, Path.Combine(folder, "map.png"), pixels);
        }

        // Renders a view of `size` units centred on `at`, at 16 pixels a unit, to a PNG (to nothing when
        // `file` is null). With `place` false the backdrop stays where it was put: a piece of a larger picture.
        private static void Render(
            TilesV2World world,
            Vector2 at,
            Vector2 size,
            string file,
            float pixels = 16f,
            bool place = true
        )
        {
            var width = Mathf.RoundToInt(size.x * pixels);
            var height = Mathf.RoundToInt(size.y * pixels);
            var air = world.GetComponentInChildren<TilesV2Air>();
            if (air != null && pixels >= 16f && place)
            {
                // The air of the region this view is in, as full as it gets.
                air.Follow(at);
                foreach (var system in air.GetComponentsInChildren<ParticleSystem>())
                {
                    system.Simulate(ParticleTime, true, true);
                }
            }
            var main = Camera.main;
            var holder = new GameObject("Tiles V2 capture") { hideFlags = HideFlags.HideAndDontSave };
            var camera = holder.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = size.y / 2f;
            camera.transform.position = new Vector3(at.x, at.y, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = photoSky ?? (main != null ? main.backgroundColor : Color.black);
            if (main != null)
            {
                main.enabled = false;
            }
            var restore = place ? PlaceBackdrop(world, at, size) : new List<(SpriteRenderer, Vector2)>();
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
            var more = size - world.Set.View;
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

        private static string OutFolder() => Argument("-tilesV2Out") ?? Path.GetFullPath("Logs/TilesV2");

        // What follows a word on the editor's command line; null when the word is not there.
        private static string Argument(string word)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == word)
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
