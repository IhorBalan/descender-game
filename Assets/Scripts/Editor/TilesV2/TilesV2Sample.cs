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
    /// 320 x 180, a dim ambient, daylight over the open sky and the wall torches. The scene is rebuilt from the map each time.
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

        /// <summary>The dunes and the dark under them.</summary>
        public static readonly Vector2 DesertView = new Vector2(54f, 13f);

        /// <summary>The map character for a wall torch (air for the terrain).</summary>
        public const char TorchChar = 'T';

        // The lighting standard (docs/art/tiles-v2.md), the generator's AMBIENT, SKY_FALLOFF
        // and TORCH_* constants.
        public const float Ambient = 0.08f;
        public const float SkyFalloff = 5f;
        public const float TorchRadius = 8f;
        public const float TorchIntensity = 1.4f;
        public static readonly Color AmbientColour = new Color(0.55f, 0.62f, 0.85f);
        public static readonly Color TorchColour = new Color(1f, 0.72f, 0.42f);

        private const string UnlitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        private static readonly Dictionary<char, string> TerrainTiles = new Dictionary<char, string>
        {
            { 'G', TilesV2Manifest.TilePath("grass") },
            { 'D', TilesV2Manifest.TilePath("dirt") },
            { 'S', TilesV2Manifest.TilePath("stone") },
            { 'A', TilesV2Manifest.TilePath("sand") },
            { 'C', TilesV2Manifest.TilePath("clay") },
            { 'O', TilesV2Manifest.TilePath("copper_ore") },
            { '/', TilesV2Manifest.SlopePath("grass", "up_right") },
            { '\\', TilesV2Manifest.SlopePath("grass", "up_left") },
            { '(', TilesV2Manifest.SlopePath("sand", "up_right") },
            { ')', TilesV2Manifest.SlopePath("sand", "up_left") },
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
            MakeLights(terrainRows);
            MakeTorches(terrainRows);
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

        /// <summary>
        /// For each column, the first map row (from the top) holding terrain: open sky
        /// reaches down to it (the map's height where a column is open all the way).
        /// </summary>
        public static int[] SkyReach(List<string> terrain)
        {
            var width = 0;
            foreach (var row in terrain)
            {
                width = Mathf.Max(width, row.Length);
            }
            var reach = new int[width];
            for (var x = 0; x < width; x++)
            {
                reach[x] = terrain.Count;
                for (var y = 0; y < terrain.Count; y++)
                {
                    if (x < terrain[y].Length && TerrainTiles.ContainsKey(terrain[y][x]))
                    {
                        reach[x] = y;
                        break;
                    }
                }
            }
            return reach;
        }

        /// <summary>
        /// The daylight's outline in world units: everything above the terrain's skyline,
        /// through the top middle of each column's first tile (so a step or a slope is a
        /// diagonal, not a notch the falloff cannot fill), down open shafts, carried a margin
        /// past both ends of the map.
        /// </summary>
        public static Vector3[] SkyOutline(List<string> terrain, float margin = 24f, float above = 24f)
        {
            var reach = SkyReach(terrain);
            var rows = terrain.Count;
            var top = rows + above;
            var points = new List<Vector3> { new Vector3(-margin, top), new Vector3(-margin, rows - reach[0]) };
            for (var x = 0; x < reach.Length; x++)
            {
                points.Add(new Vector3(x + 0.5f, rows - reach[x]));
            }
            points.Add(new Vector3(reach.Length + margin, rows - reach[reach.Length - 1]));
            points.Add(new Vector3(reach.Length + margin, top));
            return points.ToArray();
        }

        /// <summary>
        /// A dim, cool ambient everywhere, and the daylight as a freeform light over the
        /// open sky that fades a few tiles into the ground: deep rock is dark until a
        /// torch lights it.
        /// </summary>
        private static void MakeLights(List<string> terrain)
        {
            var ambient = new GameObject("Ambient").AddComponent<Light2D>();
            ambient.lightType = Light2D.LightType.Global;
            ambient.blendStyleIndex = 0;
            ambient.color = AmbientColour;
            ambient.intensity = Ambient;

            var sky = new GameObject("Daylight").AddComponent<Light2D>();
            sky.lightType = Light2D.LightType.Freeform;
            sky.blendStyleIndex = 0;
            sky.color = Color.white;
            sky.intensity = 1f - Ambient;
            sky.SetShapePath(SkyOutline(terrain));
            sky.shapeLightFalloffSize = SkyFalloff;
            sky.falloffIntensity = 0.5f;
            sky.shadowsEnabled = false;
        }

        /// <summary>A torch on the wall at every T of the map: its flame, unlit, and a warm point light.</summary>
        private static void MakeTorches(List<string> terrain)
        {
            var path = TilesV2Manifest.Folder + "/torch.png";
            var frames = new List<Sprite>();
            foreach (var item in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (item is Sprite sprite)
                {
                    frames.Add(sprite);
                }
            }
            if (frames.Count == 0)
            {
                throw new FileNotFoundException("Tiles V2: run the generator first.", path);
            }
            frames.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            var parent = new GameObject("Torches").transform;
            for (var row = 0; row < terrain.Count; row++)
            {
                for (var column = 0; column < terrain[row].Length; column++)
                {
                    if (terrain[row][column] != TorchChar)
                    {
                        continue;
                    }
                    var cell = CellOf(column, row, terrain.Count);
                    var holder = new GameObject($"Torch {column},{row}");
                    holder.transform.SetParent(parent, false);
                    holder.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
                    var renderer = holder.AddComponent<SpriteRenderer>();
                    renderer.sprite = frames[0];
                    renderer.sortingOrder = BlendOrder + 1;
                    if (unlit != null)
                    {
                        renderer.sharedMaterial = unlit;
                    }
                    var glow = new GameObject("Light").AddComponent<Light2D>();
                    glow.transform.SetParent(holder.transform, false);
                    glow.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    glow.lightType = Light2D.LightType.Point;
                    glow.blendStyleIndex = 0;
                    glow.color = TorchColour;
                    glow.intensity = TorchIntensity;
                    glow.pointLightInnerRadius = 0.5f;
                    glow.pointLightOuterRadius = TorchRadius;
                    glow.falloffIntensity = 0.5f;
                    glow.shadowsEnabled = false;
                    holder.AddComponent<TorchFlame>().Configure(frames.ToArray(), glow, TorchIntensity);
                }
            }
        }

        /// <summary>
        /// The backdrop layers, back to front: the sky pinned to the view, then ridges that
        /// scroll more the nearer they are, their feet a little below the surface line.
        /// </summary>
        private static void MakeBackdrop(TilesV2Manifest manifest, Vector2 view, int rows)
        {
            var parent = new GameObject("Backdrop").transform;
            var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
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
                // The backdrop is the far daylit world: the scene's lights do not touch it.
                if (unlit != null)
                {
                    renderer.sharedMaterial = unlit;
                }
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
