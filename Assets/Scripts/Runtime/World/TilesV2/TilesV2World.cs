using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Builds a Tiles V2 world from a text map and the tile set (docs/art/tiles-v2.md), the
    /// way RegionTiles builds a region: nothing is painted into the scene, so a scene with a
    /// whole world in it stays a few lines. Under a child called Built it makes the Grid
    /// and its tilemaps (walls, shade, decor, terrain with its collider, blends, glints,
    /// liquids), the torches, the tile light and the ambient, the sky and the backdrops,
    /// the clock, the particles, and the areas of the liquids (water to swim in, lava that
    /// hurts). It builds when the game starts and, in the editor, as a preview that is
    /// never saved; Build on the context menu builds it again after a change to the map.
    /// </summary>
    [ExecuteAlways]
    public sealed class TilesV2World : MonoBehaviour
    {
        public const string BuiltName = "Built";

        public const int WallsOrder = -300;
        public const int ShadeOrder = -290;
        public const int DecorOrder = -110;
        public const int TerrainOrder = -100;
        public const int BlendOrder = -95;
        public const int TorchOrder = -60;
        public const int GlowOrder = -40;
        public const int ParticleOrder = 60;

        public const string TorchKey = "torch";
        public const string SpawnKey = "spawn";

        [SerializeField, Tooltip("The map: text, see TilesV2Map.")]
        private TextAsset map;

        [SerializeField, Tooltip("Assets/Art/TilesV2/TilesV2Set.asset.")]
        private TilesV2Set set;

        [SerializeField, Range(0f, 24f), Tooltip("The hour the world starts at.")]
        private float hour = 10f;

        [SerializeField, Tooltip("Whether the day runs while the game plays.")]
        private bool dayRuns = true;

        [SerializeField, Tooltip("Makes the faint ambient light. Off where the scene has a global light of its own.")]
        private bool ambientLight = true;

        private Transform built;
        private readonly List<Tilemap> liquidMaps = new List<Tilemap>();
        private readonly List<Vector2> torchPoints = new List<Vector2>();

        public TilesV2Set Set => set;
        public bool IsBuilt => built != null;

        /// <summary>The map as read at the last Build; null before the first.</summary>
        public TilesV2Map Map { get; private set; }

        public Tilemap Terrain { get; private set; }
        public Tilemap Walls { get; private set; }
        public Tilemap Decor { get; private set; }
        public Tilemap Shade { get; private set; }
        public Tilemap Blend { get; private set; }
        public Tilemap Glow { get; private set; }
        public IReadOnlyList<Tilemap> Liquids => liquidMaps;
        public TileLightMap Light { get; private set; }
        public DayNight Day { get; private set; }
        public Sky Sky { get; private set; }

        /// <summary>The holder of the torches.</summary>
        public Transform Torches { get; private set; }

        /// <summary>Where the map puts the hero (the middle of the foot of its start cell), in the world.</summary>
        public Vector2 Spawn { get; private set; }

        /// <summary>The map's rectangle in the world.</summary>
        public Rect Area
        {
            get
            {
                Vector2 corner = transform.position;
                return Map == null
                    ? new Rect(corner, Vector2.zero)
                    : new Rect(corner, new Vector2(Map.Width, Map.Height));
            }
        }

        /// <summary>
        /// The middle of a view of the tile set's size nearest `centre` that shows nothing past
        /// the map's edges (the map's own middle on an axis the map is too small for).
        /// </summary>
        public Vector2 Framed(Vector2 centre)
        {
            var area = Area;
            var half = set != null ? set.View / 2f : Vector2.zero;
            centre.x =
                area.width > half.x * 2f
                    ? Mathf.Clamp(centre.x, area.xMin + half.x, area.xMax - half.x)
                    : area.center.x;
            centre.y =
                area.height > half.y * 2f
                    ? Mathf.Clamp(centre.y, area.yMin + half.y, area.yMax - half.y)
                    : area.center.y;
            return centre;
        }

        /// <summary>Raised once everything is built.</summary>
        public event System.Action Built;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                Build();
            }
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            // The editor's preview: built on the editor's next tick after the scene opens or the
            // scripts reload (not here: a scene still loading cannot take new objects).
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += Preview;
            }
        }

        private void OnDisable()
        {
            UnityEditor.EditorApplication.delayCall -= Preview;
        }

        private void Preview()
        {
            if (this != null && !Application.isPlaying && !IsBuilt)
            {
                Build();
            }
        }
#endif

        private void OnDestroy()
        {
            Clear();
        }

        /// <summary>Sets what to build (the scene builders' entry point).</summary>
        public void Configure(
            TextAsset mapText,
            TilesV2Set tileSet,
            float startHour,
            bool dayIsRunning,
            bool makeAmbient
        )
        {
            map = mapText;
            set = tileSet;
            hour = Mathf.Repeat(startHour, 24f);
            dayRuns = dayIsRunning;
            ambientLight = makeAmbient;
        }

        /// <summary>A named place of the map (`@view`) in the world; false when the map has none of that name.</summary>
        public bool TryView(string name, out Vector2 point)
        {
            point = Vector2.zero;
            if (Map == null || !Map.Views.TryGetValue(name, out var columnRow))
            {
                return false;
            }
            point = (Vector2)transform.position + Map.PointOf(columnRow);
            return true;
        }

        [ContextMenu("Build")]
        public void Build()
        {
            Clear();
            if (map == null || set == null)
            {
                Debug.LogWarning($"{name}: a Tiles V2 world needs a map and the tile set.", this);
                return;
            }
            Map = TilesV2Map.Parse(map.text);
            built = new GameObject(BuiltName).transform;
            built.SetParent(transform, false);
            if (!Application.isPlaying)
            {
                built.gameObject.hideFlags = HideFlags.DontSave;
            }
            var grid = built.gameObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            Walls = MakeTilemap("Walls", WallsOrder, set.litMaterial, false);
            Walls.color = new Color(set.wallTint.r, set.wallTint.g, set.wallTint.b, 1f);
            Shade = MakeTilemap("Shade", ShadeOrder, set.litMaterial, false);
            Decor = MakeTilemap("Decor", DecorOrder, set.litMaterial, false);
            Terrain = MakeTilemap("Terrain", TerrainOrder, set.litMaterial, true);
            Blend = MakeTilemap("Blend", BlendOrder, set.litMaterial, false);
            Glow = MakeTilemap("Glow", GlowOrder, set.unlitMaterial, false);
            var liquidOf = new Dictionary<string, Tilemap>();
            foreach (var liquid in set.liquids)
            {
                var material = liquid.lit ? set.litMaterial : set.unlitMaterial;
                var liquidMap = MakeTilemap("Liquid " + liquid.key, liquid.order, material, false);
                liquidOf[liquid.key] = liquidMap;
                liquidMaps.Add(liquidMap);
            }

            var taken = Lay(liquidOf);
            var field = TileField.Read(Terrain, Walls, liquidMaps);
            if (field != null)
            {
                TilesV2Painter.Blends(field, set, Blend);
                TilesV2Painter.Shade(field, set, Shade);
                TilesV2Painter.Decor(field, set, Decor, taken);
                TilesV2Painter.Glints(field, set, Glow);
            }

            Torches = MakeTorches();
            MakeLights();
            Sky = TilesV2Backdrops.Build(this, built);
            Day = built.gameObject.AddComponent<DayNight>();
            Day.Configure(set, Light, Sky, hour, dayRuns);
            if (field != null)
            {
                TilesV2Particles.Build(this, field, built);
                TilesV2Zones.Build(this, field, built);
            }
            Built?.Invoke();
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            if (built != null)
            {
                Remove(built.gameObject);
                built = null;
            }
            // A preview left by an earlier session of the editor.
            var stale = transform.Find(BuiltName);
            if (stale != null)
            {
                Remove(stale.gameObject);
            }
            liquidMaps.Clear();
            torchPoints.Clear();
            Map = null;
            Terrain = null;
            Walls = null;
            Decor = null;
            Shade = null;
            Blend = null;
            Glow = null;
            Light = null;
            Day = null;
            Sky = null;
            Torches = null;
        }

        private static void Remove(GameObject thing)
        {
            if (Application.isPlaying)
            {
                Destroy(thing);
            }
            else
            {
                DestroyImmediate(thing);
            }
        }

        // Lays the map's two grids; returns the cells decor must keep clear (torches, the hero's start).
        private HashSet<Vector3Int> Lay(Dictionary<string, Tilemap> liquidOf)
        {
            var taken = new HashSet<Vector3Int>();
            var cells = new Dictionary<Tilemap, List<Vector3Int>>();
            var tiles = new Dictionary<Tilemap, List<TileBase>>();
            Spawn = (Vector2)transform.position + new Vector2(Map.Width / 2f, Map.Height);
            for (var row = 0; row < Map.Height; row++)
            {
                for (var column = 0; column < Map.Width; column++)
                {
                    var cell = Map.CellOf(column, row);
                    if (set.TryGlyph(Map.WallAt(column, row), out var behind) && behind.layer == TilesV2Set.Layer.Wall)
                    {
                        Add(cells, tiles, Walls, cell, behind.tile);
                    }
                    if (!set.TryGlyph(Map.TerrainAt(column, row), out var glyph))
                    {
                        continue;
                    }
                    switch (glyph.layer)
                    {
                        case TilesV2Set.Layer.Terrain:
                            Add(cells, tiles, Terrain, cell, glyph.tile);
                            break;
                        case TilesV2Set.Layer.Liquid:
                            if (liquidOf.TryGetValue(glyph.key, out var liquidMap))
                            {
                                Add(cells, tiles, liquidMap, cell, glyph.tile);
                            }
                            break;
                        case TilesV2Set.Layer.Prop:
                            taken.Add(cell);
                            if (glyph.key == TorchKey)
                            {
                                torchPoints.Add(new Vector2(cell.x + 0.5f, cell.y + 0.5f));
                            }
                            break;
                        case TilesV2Set.Layer.Mark:
                            taken.Add(cell);
                            if (glyph.key == SpawnKey)
                            {
                                Spawn = (Vector2)transform.position + new Vector2(cell.x + 0.5f, cell.y);
                            }
                            break;
                    }
                }
            }
            foreach (var pair in cells)
            {
                pair.Key.SetTiles(pair.Value.ToArray(), tiles[pair.Key].ToArray());
            }
            return taken;
        }

        private static void Add(
            Dictionary<Tilemap, List<Vector3Int>> cells,
            Dictionary<Tilemap, List<TileBase>> tiles,
            Tilemap into,
            Vector3Int cell,
            TileBase tile
        )
        {
            if (tile == null)
            {
                return;
            }
            if (!cells.TryGetValue(into, out var list))
            {
                list = new List<Vector3Int>();
                cells[into] = list;
                tiles[into] = new List<TileBase>();
            }
            list.Add(cell);
            tiles[into].Add(tile);
        }

        private Tilemap MakeTilemap(string layerName, int order, Material material, bool collides)
        {
            var holder = new GameObject(layerName);
            holder.transform.SetParent(built, false);
            var tilemap = holder.AddComponent<Tilemap>();
            var shown = holder.AddComponent<TilemapRenderer>();
            shown.sortingOrder = order;
            shown.mode = TilemapRenderer.Mode.Chunk;
            if (material != null)
            {
                shown.sharedMaterial = material;
            }
            if (collides)
            {
                holder.layer = LayerMask.NameToLayer("World");
                var body = holder.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                var tiles = holder.AddComponent<TilemapCollider2D>();
                tiles.compositeOperation = Collider2D.CompositeOperation.Merge;
                var composite = holder.AddComponent<CompositeCollider2D>();
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            }
            return tilemap;
        }

        // A torch at every torch cell of the map: its flame, unlit, the light it gives its tile and a small flickering glow.
        private Transform MakeTorches()
        {
            var holder = new GameObject("Torches").transform;
            holder.SetParent(built, false);
            if (set.torchFrames.Length == 0)
            {
                return holder;
            }
            foreach (var point in torchPoints)
            {
                var torch = new GameObject("Torch");
                torch.transform.SetParent(holder, false);
                torch.transform.localPosition = point;
                var flame = torch.AddComponent<SpriteRenderer>();
                flame.sprite = set.torchFrames[0];
                flame.sortingOrder = TorchOrder;
                if (set.unlitMaterial != null)
                {
                    flame.sharedMaterial = set.unlitMaterial;
                }
                var glow = new GameObject("Glow").AddComponent<Light2D>();
                glow.transform.SetParent(torch.transform, false);
                glow.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                glow.lightType = Light2D.LightType.Point;
                glow.blendStyleIndex = 0;
                glow.color = set.torchColour;
                glow.intensity = set.torchGlow;
                glow.pointLightInnerRadius = 0f;
                glow.pointLightOuterRadius = set.torchGlowRadius;
                glow.falloffIntensity = 0.5f;
                glow.shadowsEnabled = false;
                torch
                    .AddComponent<TorchFlame>()
                    .Configure(set.torchFrames, glow, set.torchGlow, set.torchColour, set.torchIntensity);
            }
            return holder;
        }

        // A faint, cool ambient everywhere (unless the scene has its own) and the tile light.
        private void MakeLights()
        {
            if (ambientLight)
            {
                var ambient = new GameObject("Ambient").AddComponent<Light2D>();
                ambient.transform.SetParent(built, false);
                ambient.lightType = Light2D.LightType.Global;
                ambient.blendStyleIndex = 0;
                ambient.color = set.ambientColour;
                ambient.intensity = set.ambient;
            }
            var holder = new GameObject("Tile Light");
            holder.transform.SetParent(built, false);
            var shown = holder.AddComponent<Light2D>();
            shown.lightType = Light2D.LightType.Sprite;
            shown.blendStyleIndex = 0;
            shown.color = Color.white;
            shown.intensity = 1f;
            shown.shadowsEnabled = false;
            Light = holder.AddComponent<TileLightMap>();
            Light.Configure(Terrain, Walls, liquidMaps, Torches, set.airKeeps, set.solidKeeps);
            Light.Refresh();
        }
    }
}
