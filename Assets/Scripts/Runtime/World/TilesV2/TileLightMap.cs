using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Lights a Tiles V2 map tile by tile (docs/art/tiles-v2.md): the open sky, each torch
    /// under the torches holder, the liquids and the ground that give light, spread by
    /// TileLight and drawn as one sprite light whose texels are the map's tiles, filtered so
    /// the light is smooth between them. The sky's half and the sources' half are kept
    /// apart, so the colour of the daylight (the hour, see DayNight) changes without
    /// spreading again. Runs in the editor too, so a scene renders lit without entering
    /// play. Call Refresh after changing the tiles or the torches.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Light2D))]
    public sealed class TileLightMap : MonoBehaviour
    {
        [SerializeField]
        private Tilemap terrain;

        [SerializeField]
        private Tilemap walls;

        [SerializeField]
        private List<Tilemap> liquids = new List<Tilemap>();

        [SerializeField, Tooltip("The torches whose light the tiles spread: every TorchFlame under it.")]
        private Transform torches;

        [SerializeField, Tooltip("The colour of the daylight: what the sky's half of the light is multiplied by.")]
        private Color daylight = Color.white;

        [SerializeField, Range(0f, 1f), Tooltip("The share of its light a tile of air passes on.")]
        private float airKeeps = 0.91f;

        [SerializeField, Range(0f, 1f), Tooltip("The share a solid tile passes on.")]
        private float solidKeeps = 0.56f;

        [SerializeField, Tooltip("The faint light that hangs in the air of each row of cells: a region's own.")]
        private Color[] airLight = System.Array.Empty<Color>();

        [SerializeField, Tooltip("The cell row the first of them is for.")]
        private int airLightFrom;

        private Texture2D texture;
        private Sprite sprite;
        private Color[] sky = System.Array.Empty<Color>();
        private Color[] sources = System.Array.Empty<Color>();
        private Color[] light = System.Array.Empty<Color>();

        /// <summary>The light per tile from the last Refresh, row major from the bounds' bottom left.</summary>
        public Color[] Light => light;

        /// <summary>The sky's half of the light, for a white daylight.</summary>
        public Color[] Sky => sky;

        /// <summary>The half that torches, liquids and glowing ground give.</summary>
        public Color[] Sources => sources;

        /// <summary>The cells the light covers.</summary>
        public BoundsInt Bounds { get; private set; }

        /// <summary>What stood in the cells at the last Refresh; null before the first.</summary>
        public TileField Field { get; private set; }

        /// <summary>The colour of the daylight; setting it mixes the two halves again and shows the result.</summary>
        public Color Daylight
        {
            get => daylight;
            set
            {
                if (daylight == value)
                {
                    return;
                }
                daylight = value;
                Show();
            }
        }

        private void OnEnable() => Refresh();

        private void OnDisable() => Release();

        public void Configure(
            Tilemap terrainMap,
            Tilemap wallMap,
            IEnumerable<Tilemap> liquidMaps,
            Transform torchHolder,
            float air,
            float solid
        )
        {
            terrain = terrainMap;
            walls = wallMap;
            liquids = liquidMaps != null ? new List<Tilemap>(liquidMaps) : new List<Tilemap>();
            torches = torchHolder;
            airKeeps = air;
            solidKeeps = solid;
        }

        /// <summary>
        /// Sets the light that hangs in the air row by row (a region's own; black for none):
        /// `rows[i]` is for the cells of row `from + i`. Every cell of air in a row gives it.
        /// </summary>
        public void SetAirLight(Color[] rows, int from)
        {
            airLight = rows ?? System.Array.Empty<Color>();
            airLightFrom = from;
        }

        /// <summary>The light at a cell from the last Refresh; black outside the map.</summary>
        public Color At(Vector3Int cell)
        {
            var x = cell.x - Bounds.xMin;
            var y = cell.y - Bounds.yMin;
            if (x < 0 || y < 0 || x >= Bounds.size.x || y >= Bounds.size.y || light.Length == 0)
            {
                return Color.black;
            }
            return light[y * Bounds.size.x + x];
        }

        /// <summary>Works the light out again from the tiles and torches and shows it.</summary>
        public void Refresh()
        {
            if (terrain == null)
            {
                return;
            }
            var field = TileField.Read(terrain, walls, liquids);
            if (field == null)
            {
                return;
            }
            var count = field.Width * field.Height;
            var solid = new bool[count];
            var shades = new bool[count];
            var keep = new Color[count];
            var emits = new Color[count];
            var air = new Color(airKeeps, airKeeps, airKeeps, 1f);
            var rock = new Color(solidKeeps, solidKeeps, solidKeeps, 1f);
            for (var i = 0; i < count; i++)
            {
                var ground = field.Ground[i];
                var liquid = field.Liquid[i];
                solid[i] = ground != null;
                shades[i] = field.Shades(i);
                keep[i] = ground != null ? rock : air;
                emits[i] = Color.black;
                if (ground != null && !ground.IsSlope)
                {
                    emits[i] = ground.Emits;
                }
                if (liquid != null)
                {
                    keep[i] = liquid.Keeps;
                    emits[i] = liquid.Emits;
                }
                else if (ground == null && airLight.Length > 0)
                {
                    var row = field.Bounds.yMin + i / field.Width - airLightFrom;
                    if (row >= 0 && row < airLight.Length)
                    {
                        emits[i] = airLight[row];
                    }
                }
            }
            if (torches != null)
            {
                foreach (var torch in torches.GetComponentsInChildren<TorchFlame>())
                {
                    var cell = terrain.WorldToCell(torch.transform.position) - field.Bounds.min;
                    if (field.Inside(cell.x, cell.y))
                    {
                        var i = field.Index(cell.x, cell.y);
                        var seed = emits[i];
                        var torchLight = torch.Emission;
                        emits[i] = new Color(
                            Mathf.Max(seed.r, torchLight.r),
                            Mathf.Max(seed.g, torchLight.g),
                            Mathf.Max(seed.b, torchLight.b),
                            1f
                        );
                    }
                }
            }
            var seeds = new Color[count];
            TileLight.SeedSky(seeds, shades, field.Width, field.Height, Color.white, null);
            sky = TileLight.Spread(seeds, keep, solid, field.Width, field.Height);
            sources = TileLight.Spread(emits, keep, solid, field.Width, field.Height);
            light = new Color[count];
            Field = field;
            Bounds = field.Bounds;
            transform.position = terrain.CellToWorld(field.Bounds.min);
            Show();
        }

        // Mixes the two halves under the daylight and puts the result in the light's texture.
        private void Show()
        {
            if (light.Length == 0)
            {
                return;
            }
            TileLight.Combine(sky, sources, daylight, light);
            var width = Bounds.size.x;
            var height = Bounds.size.y;
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
                // One texel per tile, its centre on the tile's centre. A full rectangle: the
                // light's mesh is the sprite's, and it is made again only for a new sprite.
                sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, width, height),
                    Vector2.zero,
                    1f,
                    0,
                    SpriteMeshType.FullRect
                );
                sprite.name = "Tile light";
                sprite.hideFlags = HideFlags.HideAndDontSave;
            }
            texture.SetPixels(light);
            texture.Apply(false);
            var shown = GetComponent<Light2D>();
            shown.lightType = Light2D.LightType.Sprite;
            shown.lightCookieSprite = sprite;
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
