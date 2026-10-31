using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.World
{
    /// <summary>
    /// Builds a region's tilemaps from its RegionTileSet when the scene loads: a
    /// Grid with Background, Decor, Terrain and Glow Tilemaps (a tile is one unit, the
    /// 64 px art at 64 PPU), the Terrain's solid tiles collided through a
    /// TilemapCollider2D merged by a CompositeCollider2D on the World layer, one-way
    /// ledges as a PlatformEffector2D on the Ledge layer and spikes as HazardArea
    /// triggers on the Hazard layer, all as runs of cells the way
    /// scripts/level_builder.gd made them. Godot cell (x, y), y down from the region's
    /// top row, is Unity cell (x, -y - 1) under the region root, so a region's
    /// world offset is its root's position. In the editor "Build tiles" on the
    /// context menu previews the result; the built objects are never saved.
    /// </summary>
    public sealed class RegionTiles : MonoBehaviour
    {
        public const string BackgroundLayer = "Background";
        public const string DecorLayer = "Decor";
        public const string TerrainLayer = "Terrain";
        public const string GlowLayer = "Glow";

        public const int BackgroundOrder = -300;
        public const int DecorOrder = -200;
        public const int TerrainOrder = -100;
        public const int GlowOrder = -40;

        /// <summary>A ledge's collider height in units (4 level pixels).</summary>
        public const float LedgeHeight = 4f / 16f;

        [SerializeField]
        private RegionTileSet tileSet;

        [Tooltip("How many cells wide the region is.")]
        [SerializeField, Min(1)]
        private int width = 480;

        [Tooltip("How many cells tall the region's band is.")]
        [SerializeField, Min(1)]
        private int rows = 34;

        private readonly Dictionary<int, Tile> tiles = new Dictionary<int, Tile>();
        private TileKind[] kinds;
        private Transform built;
        private Tilemap terrain;

        public RegionTileSet TileSet => tileSet;
        public int Width => width;
        public int Rows => rows;
        public bool IsBuilt => built != null;
        public Tilemap Terrain => terrain;

        /// <summary>Raised once the tilemaps and colliders exist, for fringes and locks.</summary>
        public event System.Action Built;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                Build();
            }
        }

        private void OnDestroy()
        {
            foreach (var tile in tiles.Values)
            {
                if (tile != null)
                {
                    Destroy(tile);
                }
            }
            tiles.Clear();
        }

        /// <summary>The kind of the terrain cell at Godot cell (x, y); None outside the band.</summary>
        public TileKind KindAt(int x, int y)
        {
            if (kinds == null || x < 0 || y < 0 || x >= width || y >= rows)
            {
                return TileKind.None;
            }
            return kinds[y * width + x];
        }

        /// <summary>Whether the terrain cell collides as a wall (solid or a hidden secret wall).</summary>
        public bool IsSolid(int x, int y)
        {
            var kind = KindAt(x, y);
            return kind == TileKind.Solid || kind == TileKind.Hidden;
        }

        /// <summary>The bottom-left corner of Godot cell (x, y) in this transform's local space.</summary>
        public static Vector3 CellOrigin(int x, int y) => new Vector3(x, -y - 1, 0f);

        public static Vector3Int CellOf(int x, int y) => new Vector3Int(x, -y - 1, 0);

        [ContextMenu("Build tiles")]
        public void Build()
        {
            Clear();
            if (tileSet == null || tileSet.cells == null)
            {
                Debug.LogWarning($"{name}: no tile set or cell data to build from.", this);
                return;
            }
            var layers = tileSet.Data.Layers;
            kinds = new TileKind[width * rows];
            if (layers.TryGetValue(TerrainLayer, out var terrainCells))
            {
                foreach (var cell in terrainCells)
                {
                    if (cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < rows)
                    {
                        kinds[cell.Y * width + cell.X] = tileSet.KindOf(cell.Tile);
                    }
                }
            }

            built = new GameObject("Tilemaps").transform;
            built.SetParent(transform, false);
            if (!Application.isPlaying)
            {
                built.gameObject.hideFlags = HideFlags.DontSave;
            }
            if (!TryGetComponent<Grid>(out var grid))
            {
                grid = gameObject.AddComponent<Grid>();
            }
            grid.cellSize = Vector3.one;

            MakeLayer(layers, BackgroundLayer, BackgroundOrder, tileSet.litMaterial, false);
            MakeLayer(layers, DecorLayer, DecorOrder, tileSet.litMaterial, false);
            terrain = MakeLayer(layers, TerrainLayer, TerrainOrder, tileSet.litMaterial, true);
            MakeLayer(layers, GlowLayer, GlowOrder, tileSet.glowMaterial, false);
            BuildRuns();
            Built?.Invoke();
        }

        [ContextMenu("Clear tiles")]
        public void Clear()
        {
            if (built != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(built.gameObject);
                }
                else
                {
                    DestroyImmediate(built.gameObject);
                }
                built = null;
            }
            // A preview left in the scene by an earlier session.
            var stale = transform.Find("Tilemaps");
            if (stale != null)
            {
                DestroyImmediate(stale.gameObject);
            }
            terrain = null;
            kinds = null;
        }

        private Tilemap MakeLayer(
            Dictionary<string, RegionTileData.Cell[]> layers,
            string layerName,
            int order,
            Material material,
            bool collides
        )
        {
            var holder = new GameObject(layerName);
            holder.transform.SetParent(built, false);
            var tilemap = holder.AddComponent<Tilemap>();
            var renderer = holder.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = order;
            renderer.mode = TilemapRenderer.Mode.Chunk;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            if (collides)
            {
                holder.layer = LayerMask.NameToLayer("World");
                var body = holder.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                var collider = holder.AddComponent<TilemapCollider2D>();
                collider.compositeOperation = Collider2D.CompositeOperation.Merge;
                var composite = holder.AddComponent<CompositeCollider2D>();
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            }
            if (!layers.TryGetValue(layerName, out var cells) || cells.Length == 0)
            {
                return tilemap;
            }
            var positions = new Vector3Int[cells.Length];
            var tileAssets = new TileBase[cells.Length];
            for (var i = 0; i < cells.Length; i++)
            {
                positions[i] = CellOf(cells[i].X, cells[i].Y);
                tileAssets[i] = TileFor(cells[i].Tile, collides);
            }
            tilemap.SetTiles(positions, tileAssets);
            return tilemap;
        }

        /// <summary>One Tile per atlas index (and per collider choice), shared by every cell using it.</summary>
        private Tile TileFor(int index, bool collides)
        {
            var kind = tileSet.KindOf(index);
            var solid = collides && (kind == TileKind.Solid || kind == TileKind.Hidden);
            var key = solid ? index + 1000 : index;
            if (tiles.TryGetValue(key, out var tile))
            {
                return tile;
            }
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = $"{tileSet.name}_{index}";
            tile.sprite = tileSet.SpriteOf(index);
            tile.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            tile.hideFlags = HideFlags.DontSave;
            tiles[key] = tile;
            return tile;
        }

        /// <summary>Ledges and hazards as horizontal runs of cells, as the Godot builder made them.</summary>
        private void BuildRuns()
        {
            var ledges = new GameObject("Ledges") { layer = LayerMask.NameToLayer("Ledge") };
            ledges.transform.SetParent(built, false);
            var ledgeBody = ledges.AddComponent<Rigidbody2D>();
            ledgeBody.bodyType = RigidbodyType2D.Static;
            var effector = ledges.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 170f;
            effector.useSideFriction = false;

            var hazards = new GameObject("Hazards").transform;
            hazards.SetParent(built, false);

            for (var y = 0; y < rows; y++)
            {
                var x = 0;
                while (x < width)
                {
                    var kind = kinds[y * width + x];
                    if (kind != TileKind.Ledge && kind != TileKind.Hazard)
                    {
                        x++;
                        continue;
                    }
                    var run = 1;
                    while (x + run < width && kinds[y * width + x + run] == kind)
                    {
                        run++;
                    }
                    if (kind == TileKind.Ledge)
                    {
                        AddLedge(ledges, x, y, run);
                    }
                    else
                    {
                        AddHazard(hazards, x, y, run);
                    }
                    x += run;
                }
            }
        }

        private static void AddLedge(GameObject ledges, int x, int y, int run)
        {
            var box = ledges.AddComponent<BoxCollider2D>();
            box.usedByEffector = true;
            box.size = new Vector2(run, LedgeHeight);
            // The top of the cell row, a thin strip the hero lands on and can drop through.
            box.offset = new Vector2(x + run / 2f, -y - LedgeHeight / 2f);
        }

        private static void AddHazard(Transform hazards, int x, int y, int run)
        {
            var hazard = new GameObject($"Hazard {x},{y}") { layer = LayerMask.NameToLayer("Hazard") };
            hazard.transform.SetParent(hazards, false);
            // A bit narrower than the tiles and only the lower part, so brushing the
            // edge of a pit is forgiven (scripts/level_builder.gd, _add_hazard_run).
            hazard.transform.localPosition = new Vector3(x + run / 2f, -y - 1f + 4.5f / 16f, 0f);
            var box = hazard.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(run - 4f / 16f, 9f / 16f);
            hazard.AddComponent<HazardArea>();
        }
    }
}
