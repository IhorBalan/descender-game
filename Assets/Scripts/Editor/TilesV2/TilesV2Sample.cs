using System.Collections.Generic;
using System.IO;
using Descender.World.TilesV2;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// Descender > Tiles V2 > Build sample scene: lays the sample map
    /// (Assets/Art/TilesV2/sample_map.txt) out with the Tiles V2 tiles in its own scene,
    /// Assets/Scenes/Sandbox/TilesV2Sample.unity, which is not in the build: a Grid with
    /// Walls, Terrain and Blend tilemaps, the parallax backdrop, a pixel-perfect camera at
    /// 320 x 180 and one global light. The scene is rebuilt from the map each time.
    /// </summary>
    public static class TilesV2Sample
    {
        public const string ScenePath = "Assets/Scenes/Sandbox/TilesV2Sample.unity";
        public const string MapPath = TilesV2Manifest.Folder + "/sample_map.txt";

        public const int WallsOrder = -300;
        public const int TerrainOrder = -100;
        public const int BlendOrder = -95;

        /// <summary>Where the scene's camera looks: the hill and the shaft's mouth.</summary>
        public static readonly Vector2 SurfaceView = new Vector2(16f, 15f);

        /// <summary>The cave under the shaft.</summary>
        public static readonly Vector2 CaveView = new Vector2(31f, 7.5f);

        private static readonly Dictionary<char, string> TerrainTiles = new Dictionary<char, string>
        {
            { 'G', TilesV2Manifest.TilePath("grass") },
            { 'D', TilesV2Manifest.TilePath("dirt") },
            { 'S', TilesV2Manifest.TilePath("stone") },
            { '/', TilesV2Manifest.SlopePath("grass", "up_right") },
            { '\\', TilesV2Manifest.SlopePath("grass", "up_left") },
        };

        private static readonly Dictionary<char, string> WallTiles = new Dictionary<char, string>
        {
            { 'd', TilesV2Manifest.TilePath("dirt_wall") },
            { 's', TilesV2Manifest.TilePath("stone_wall") },
        };

        [MenuItem("Descender/Tiles V2/Build sample scene")]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            TilesV2Builder.Build();
            Build();
            Debug.Log($"Tiles V2: built {ScenePath} from {MapPath}.");
        }

        /// <summary>Builds and saves the sample scene; returns it, open.</summary>
        public static Scene Build()
        {
            var manifest = TilesV2Manifest.Load();
            var (terrainRows, wallRows) = ReadMap(File.ReadAllText(MapPath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("TilesV2 Sample");
            var grid = root.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var walls = MakeTilemap(root.transform, "Walls", WallsOrder);
            var terrain = MakeTilemap(root.transform, TilesV2Blends.TerrainName, TerrainOrder);
            var blend = MakeTilemap(root.transform, TilesV2Blends.BlendName, BlendOrder);
            AddCollider(terrain.gameObject);

            Lay(walls, wallRows, WallTiles);
            Lay(terrain, terrainRows, TerrainTiles);
            TilesV2Blends.Paint(terrain, blend, manifest);

            MakeCamera(SurfaceView);
            MakeLight();
            MakeBackdrop(manifest, SurfaceView, terrainRows.Count);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        /// <summary>
        /// The map's two grids, top row first: terrain, then (after a blank line) walls.
        /// Lines starting with # are comments.
        /// </summary>
        public static (List<string> terrain, List<string> walls) ReadMap(string text)
        {
            var grids = new List<List<string>> { new List<string>() };
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                if (raw.StartsWith("#"))
                {
                    continue;
                }
                if (raw.Length == 0)
                {
                    if (grids[grids.Count - 1].Count > 0)
                    {
                        grids.Add(new List<string>());
                    }
                    continue;
                }
                grids[grids.Count - 1].Add(raw);
            }
            if (grids.Count < 2 || grids[1].Count == 0)
            {
                throw new InvalidDataException("Tiles V2: the sample map needs a terrain grid and a wall grid.");
            }
            return (grids[0], grids[1]);
        }

        /// <summary>Map (column, row) with row 0 on top to a cell whose y is up, the map's bottom row at 0.</summary>
        public static Vector3Int CellOf(int column, int row, int rows) => new Vector3Int(column, rows - 1 - row, 0);

        private static void Lay(Tilemap map, List<string> rows, Dictionary<char, string> legend)
        {
            var tiles = new Dictionary<char, TileBase>();
            foreach (var pair in legend)
            {
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(pair.Value);
                if (tile == null)
                {
                    throw new FileNotFoundException("Tiles V2: build the tiles first.", pair.Value);
                }
                tiles[pair.Key] = tile;
            }
            var cells = new List<Vector3Int>();
            var placed = new List<TileBase>();
            for (var row = 0; row < rows.Count; row++)
            {
                for (var column = 0; column < rows[row].Length; column++)
                {
                    if (tiles.TryGetValue(rows[row][column], out var tile))
                    {
                        cells.Add(CellOf(column, row, rows.Count));
                        placed.Add(tile);
                    }
                }
            }
            map.SetTiles(cells.ToArray(), placed.ToArray());
        }

        private static Tilemap MakeTilemap(Transform parent, string name, int order)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            var map = holder.AddComponent<Tilemap>();
            var renderer = holder.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            return map;
        }

        private static void AddCollider(GameObject holder)
        {
            holder.layer = LayerMask.NameToLayer("World");
            var body = holder.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var collider = holder.AddComponent<TilemapCollider2D>();
            collider.compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = holder.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        }

        private static void MakeCamera(Vector2 at)
        {
            var holder = new GameObject("Main Camera") { tag = "MainCamera" };
            holder.transform.position = new Vector3(at.x, at.y, -10f);
            var camera = holder.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 180f / 16f / 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.23f, 0.44f, 0.75f);
            camera.GetUniversalAdditionalCameraData();
            var pixelPerfect = holder.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = 16;
            pixelPerfect.refResolutionX = 320;
            pixelPerfect.refResolutionY = 180;
            pixelPerfect.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
        }

        private static void MakeLight()
        {
            var holder = new GameObject("Global Light");
            var light = holder.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;
        }

        /// <summary>
        /// The backdrop layers, back to front: the sky pinned to the view, then ridges that
        /// scroll more the nearer they are, their feet a little below the surface line.
        /// </summary>
        private static void MakeBackdrop(TilesV2Manifest manifest, Vector2 view, int rows)
        {
            var parent = new GameObject("Backdrop").transform;
            var surface = rows - 12f; // the sample's surface row, in world units
            foreach (var entry in manifest.backdrops)
            {
                var path = TilesV2Manifest.Folder + "/" + entry.key + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    throw new FileNotFoundException("Tiles V2: run the generator first.", path);
                }
                var holder = new GameObject(entry.key);
                holder.transform.SetParent(parent, false);
                var renderer = holder.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = entry.order;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                var size = sprite.bounds.size;
                renderer.size = new Vector2(size.x * 8f, size.y);
                var layer = holder.AddComponent<ParallaxLayer>();
                // The sprite's pivot is its bottom middle; the sky's foot sits at the view's
                // bottom, a ridge's a tile under the surface.
                var foot = entry.scroll <= 0f ? view.y - 180f / 16f / 2f : surface - 1f;
                layer.Configure(entry.scroll, new Vector2(view.x, foot), view);
            }
        }
    }
}
