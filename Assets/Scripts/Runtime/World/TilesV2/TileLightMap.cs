using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Lights a Tiles V2 map tile by tile: daylight down every column to its first tile or
    /// wall, and each torch under the torches holder, spread by TileLight and drawn as one sprite light
    /// whose texels are the map's tiles, filtered so the light is smooth between them.
    /// Runs in the editor too, so a scene renders lit without entering play. Call Refresh
    /// after changing the tiles or the torches.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Light2D))]
    public sealed class TileLightMap : MonoBehaviour
    {
        [SerializeField]
        private Tilemap terrain;

        [SerializeField]
        private Tilemap walls;

        [SerializeField, Tooltip("The torches whose light the tiles spread: every TorchFlame under it.")]
        private Transform torches;

        [SerializeField]
        private Color daylight = Color.white;

        [SerializeField, Range(0f, 1f)]
        private float airKeeps = 0.9f;

        [SerializeField, Range(0f, 1f)]
        private float solidKeeps = 0.7f;

        /// <summary>The rows of open air the light covers over the map's top tile.</summary>
        public const int SkyRows = 2;

        private Texture2D texture;
        private Sprite sprite;

        /// <summary>The light per tile from the last Refresh, row major from the bounds' bottom left.</summary>
        public Color[] Light { get; private set; } = System.Array.Empty<Color>();

        /// <summary>The cells the light covers.</summary>
        public BoundsInt Bounds { get; private set; }

        public void Configure(
            Tilemap terrainMap,
            Tilemap wallMap,
            Transform torchHolder,
            Color day,
            float air,
            float solid
        )
        {
            terrain = terrainMap;
            walls = wallMap;
            torches = torchHolder;
            daylight = day;
            airKeeps = air;
            solidKeeps = solid;
        }

        private void OnEnable() => Refresh();

        private void OnDisable() => Release();

        /// <summary>Works the light out again from the tiles and torches and shows it.</summary>
        public void Refresh()
        {
            if (terrain == null)
            {
                return;
            }
            var bounds = terrain.cellBounds;
            if (walls != null)
            {
                var w = walls.cellBounds;
                var min = Vector3Int.Min(bounds.min, w.min);
                var max = Vector3Int.Max(bounds.max, w.max);
                bounds = new BoundsInt(min, max - min);
            }
            // Open air over the top tile, so every column starts under the sky.
            bounds = new BoundsInt(bounds.min, bounds.size + new Vector3Int(0, SkyRows, 0));
            var width = bounds.size.x;
            var height = bounds.size.y;
            if (width <= 0 || height <= 0)
            {
                return;
            }
            var solid = new bool[width * height];
            var backed = new bool[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var cell = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                    solid[y * width + x] = terrain.HasTile(cell);
                    backed[y * width + x] = walls != null && walls.HasTile(cell);
                }
            }
            var seeds = new Color[width * height];
            TileLight.SeedSky(seeds, solid, backed, width, height, daylight);
            if (torches != null)
            {
                foreach (var torch in torches.GetComponentsInChildren<TorchFlame>())
                {
                    var cell = terrain.WorldToCell(torch.transform.position) - bounds.min;
                    if (cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height)
                    {
                        var i = cell.y * width + cell.x;
                        var e = torch.Emission;
                        var s = seeds[i];
                        seeds[i] = new Color(Mathf.Max(s.r, e.r), Mathf.Max(s.g, e.g), Mathf.Max(s.b, e.b), 1f);
                    }
                }
            }
            var decay = new TileLight.Decay(airKeeps, solidKeeps);
            Light = TileLight.Spread(seeds, solid, width, height, decay);
            Bounds = bounds;
            Show(width, height, terrain.CellToWorld(bounds.min));
        }

        private void Show(int width, int height, Vector3 corner)
        {
            if (texture == null || texture.width != width || texture.height != height)
            {
                Release();
                texture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
                {
                    name = "Tile light",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                // One texel per tile, its centre on the tile's centre.
                sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.zero, 1f);
                sprite.name = "Tile light";
                sprite.hideFlags = HideFlags.HideAndDontSave;
            }
            texture.SetPixels(Light);
            texture.Apply(false);
            transform.position = corner;
            var light = GetComponent<Light2D>();
            light.lightType = Light2D.LightType.Sprite;
            light.lightCookieSprite = sprite;
        }

        private void Release()
        {
            if (sprite != null)
            {
                DestroyImmediate(sprite);
            }
            if (texture != null)
            {
                DestroyImmediate(texture);
            }
            sprite = null;
            texture = null;
        }
    }
}
