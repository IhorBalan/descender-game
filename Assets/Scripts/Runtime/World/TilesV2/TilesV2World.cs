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
    /// liquids, ledges), the lamps, the tile light and the ambient, the sky and the backdrops,
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
        public const int FoliageOrder = -285;
        public const int DecorOrder = -110;
        public const int TerrainOrder = -100;
        public const int BlendOrder = -95;
        public const int TorchOrder = -60;
        public const int GlowOrder = -40;
        public const int ParticleOrder = 60;
        public const int LedgeOrder = -98;

        /// <summary>A map of this many cells or more says how long it took to build.</summary>
        public const int BigMap = 100000;

        /// <summary>Rows either side of the edge between two regions over which the light in the air of one turns into the other's.</summary>
        public const int RegionBlend = 6;

        public const string TorchKey = "torch";
        public const string SpawnKey = "spawn";

        /// <summary>The kind of a mark that is a stretch with no light in its air.</summary>
        public const string DarkKind = "dark";

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
        private readonly List<(Vector2 at, TilesV2Set.Prop prop)> propPoints =
            new List<(Vector2 at, TilesV2Set.Prop prop)>();

        public TilesV2Set Set => set;
        public bool IsBuilt => built != null;

        /// <summary>The map as read at the last Build; null before the first.</summary>
        public TilesV2Map Map { get; private set; }

        public Tilemap Terrain { get; private set; }
        public Tilemap Walls { get; private set; }

        /// <summary>
        /// The walls that stand under the open sky and do not shut it out (a tree's trunk and
        /// crown): on a tilemap of their own, in front of the other walls and not tinted back.
        /// </summary>
        public Tilemap Foliage { get; private set; }
        public Tilemap Decor { get; private set; }
        public Tilemap Shade { get; private set; }
        public Tilemap Blend { get; private set; }
        public Tilemap Glow { get; private set; }

        /// <summary>The ledges: one-way platforms, on the game's Ledge layer.</summary>
        public Tilemap Ledges { get; private set; }
        public IReadOnlyList<Tilemap> Liquids => liquidMaps;
        public TileLightMap Light { get; private set; }
        public DayNight Day { get; private set; }

        /// <summary>What moves and what comes with the hour (TilesV2Life): built with the world.</summary>
        public TilesV2Life Life { get; private set; }
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
            var clock = System.Diagnostics.Stopwatch.StartNew();
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
            Foliage = MakeTilemap("Foliage", FoliageOrder, set.litMaterial, false);
            Shade = MakeTilemap("Shade", ShadeOrder, set.litMaterial, false);
            Decor = MakeTilemap("Decor", DecorOrder, set.litMaterial, false);
            Terrain = MakeTilemap("Terrain", TerrainOrder, set.litMaterial, true);
            Ledges = MakeLedges();
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
            Life = TilesV2Life.Build(this, built);
            if (Map.Width * Map.Height >= BigMap)
            {
                // A world takes a moment to build: say how long, where a player's log keeps it.
                Debug.Log($"Tiles V2: built {Map.Width} x {Map.Height} tiles in {clock.ElapsedMilliseconds} ms.");
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
            propPoints.Clear();
            Map = null;
            Terrain = null;
            Walls = null;
            Foliage = null;
            Decor = null;
            Shade = null;
            Blend = null;
            Glow = null;
            Ledges = null;
            Light = null;
            Day = null;
            Life = null;
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
                        var open = behind.tile is TerrainTile wall && wall.Open;
                        Add(cells, tiles, open ? Foliage : Walls, cell, behind.tile);
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
                            var prop = set.PropOf(glyph.key);
                            if (prop != null && prop.frames.Length > 0)
                            {
                                propPoints.Add((new Vector2(cell.x + 0.5f, cell.y + 0.5f), prop));
                            }
                            break;
                        case TilesV2Set.Layer.Ledge:
                            taken.Add(cell);
                            var ledge = set.LedgeOf(glyph.key);
                            if (ledge != null)
                            {
                                var character = Map.TerrainAt(column, row);
                                var piece = ledge.Piece(
                                    Map.TerrainAt(column - 1, row) == character,
                                    Map.TerrainAt(column + 1, row) == character
                                );
                                Add(cells, tiles, Ledges, cell, piece);
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

        // The ledges' tilemap: on the Ledge layer with a one-way effector, as RegionTiles makes the
        // game's own, so the hero jumps up through a ledge and drops through it with down and jump.
        private Tilemap MakeLedges()
        {
            var tilemap = MakeTilemap("Ledges", LedgeOrder, set.litMaterial, false);
            var holder = tilemap.gameObject;
            holder.layer = LayerMask.NameToLayer("Ledge");
            var body = holder.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var tiles = holder.AddComponent<TilemapCollider2D>();
            tiles.compositeOperation = Collider2D.CompositeOperation.Merge;
            var composite = holder.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            composite.usedByEffector = true;
            var effector = holder.AddComponent<PlatformEffector2D>();
            // The hero drops through by turning the Player and Ledge layer pair off, which an
            // effector's own mask would override.
            effector.useColliderMask = false;
            effector.useOneWay = true;
            effector.surfaceArc = 170f;
            effector.useSideFriction = false;
            return tilemap;
        }

        // A lamp at every prop cell of the map (a torch, a brazier, a crystal, a lantern): its
        // frames, unlit, the light it gives its tile and a small flickering glow.
        private Transform MakeTorches()
        {
            var holder = new GameObject("Torches").transform;
            holder.SetParent(built, false);
            foreach (var (point, prop) in propPoints)
            {
                var torch = new GameObject(prop.key);
                torch.transform.SetParent(holder, false);
                torch.transform.localPosition = point;
                var flame = torch.AddComponent<SpriteRenderer>();
                flame.sprite = prop.frames[0];
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
                glow.color = prop.colour;
                glow.intensity = set.torchGlow;
                glow.pointLightInnerRadius = 0f;
                glow.pointLightOuterRadius = set.torchGlowRadius;
                glow.falloffIntensity = 0.5f;
                glow.shadowsEnabled = false;
                var lamp = torch.AddComponent<TorchFlame>();
                lamp.Configure(prop.frames, glow, set.torchGlow, prop.colour, prop.intensity);
                lamp.Embers = prop.embers;
            }
            return holder;
        }

        /// <summary>
        /// The light that hangs in the air of each row of cells, the bottom row first: each
        /// region's own, turning into the next one's over <see cref="RegionBlend"/> rows either
        /// side of the edge between them. Black for a map that names no regions.
        /// </summary>
        public Color[] AirLight()
        {
            var rows = new Color[Map.Height];
            var regions = Map.Regions;
            for (var row = 0; row < Map.Height && regions.Count > 0; row++)
            {
                var index = Map.RegionAt(row);
                var top = regions[index].Top;
                var end = Map.RegionEnd(index);
                var colour = Ambient(index);
                if (index > 0 && row - top < RegionBlend)
                {
                    var t = 0.5f + (row - top + 0.5f) / (2f * RegionBlend);
                    colour = Color.LerpUnclamped(Ambient(index - 1), colour, t);
                }
                else if (index < regions.Count - 1 && end - row <= RegionBlend)
                {
                    var t = 0.5f - (end - row - 0.5f) / (2f * RegionBlend);
                    colour = Color.LerpUnclamped(colour, Ambient(index + 1), t);
                }
                colour.a = 1f;
                rows[Map.Height - 1 - row] = colour;
            }
            return rows;
        }

        /// <summary>The stretches the map marks as dark (`@mark dark column row width height`), in cells.</summary>
        public List<RectInt> Dark()
        {
            var cells = new List<RectInt>();
            foreach (var mark in Map.Marks)
            {
                if (mark.Kind == DarkKind)
                {
                    var wide = Mathf.Max(mark.Number(0, 1), 1);
                    var tall = Mathf.Max(mark.Number(1, 1), 1);
                    cells.Add(new RectInt(mark.Column, Map.Height - mark.Row - tall, wide, tall));
                }
            }
            return cells;
        }

        /// <summary>The region a point of the world is in; null outside a map that names its regions.</summary>
        public TilesV2Set.Region RegionAt(Vector2 point)
        {
            if (Map == null || Map.Regions.Count == 0)
            {
                return null;
            }
            var row = Mathf.FloorToInt(Map.Height - (point.y - transform.position.y));
            return set.RegionOf(Map.Regions[Map.RegionAt(row)].Key);
        }

        private Color Ambient(int region)
        {
            var entry = set.RegionOf(Map.Regions[region].Key);
            return entry != null ? entry.ambient : Color.black;
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
            Light.SetAirLight(AirLight(), 0);
            Light.SetDark(Dark());
            if (Map.Regions.Count > 0)
            {
                // A world has islands in its sky: they do not shade the land under them.
                Light.SetOpenSky(Map.Height - Map.Surface);
            }
            Light.Refresh();
        }
    }
}
