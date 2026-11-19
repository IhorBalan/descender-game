using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Everything a Tiles V2 world is built from at run time (docs/art/tiles-v2.md): the map
    /// legend's tiles, the overlays painted by rule (blends, shade, decor, glints), the
    /// liquids, the torch, the backdrops and the numbers of the light and of the day. One
    /// asset, Assets/Art/TilesV2/TilesV2Set.asset, written from the generator's manifest by
    /// Descender > Tiles V2 > Build tiles: change the generator, not the asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Tiles V2/Tile Set", fileName = "TilesV2Set")]
    public sealed class TilesV2Set : ScriptableObject
    {
        [Header("Materials")]
        [Tooltip("What everything the tile light falls on draws with.")]
        public Material litMaterial;

        [Tooltip("What gives light or stands outside it draws with: flames, lava, glints, the far backdrop.")]
        public Material unlitMaterial;

        [Header("Map legend")]
        public Glyph[] legend = new Glyph[0];

        [Header("Painted by rule")]
        public Blend[] blends = new Blend[0];

        [Tooltip("The shade behind a ground tile itself, seen through its rounded corners.")]
        public TileBase shadeFull;

        public Shade[] shades = new Shade[0];
        public DecorRule[] decor = new DecorRule[0];
        public int floorSalt;
        public int ceilingSalt;
        public GlintTile glint;

        [Tooltip("The materials that glint.")]
        public string[] glintOn = new string[0];

        [Header("Props and liquids")]
        public Sprite[] torchFrames = new Sprite[0];

        [Tooltip("The props that give light where they stand: the torch and the others.")]
        public Prop[] props = new Prop[0];

        [Tooltip("The ledges: thin platforms the hero jumps up through and drops down through.")]
        public Ledge[] ledges = new Ledge[0];

        [Tooltip("The sprites the game's own things on a map are drawn with, by name.")]
        public Sprite[] things = new Sprite[0];

        [Header("Regions")]
        public Region[] regions = new Region[0];

        [Tooltip("The share of the camera's movement the rock behind the caves scrolls by.")]
        public float caveScroll = 0.55f;

        [Tooltip("The sorting order of the first region's cave rock; each region after it is three further on.")]
        public int caveOrder = -960;

        [Tooltip("The share of the camera's movement what stands far off scrolls by: nearer than the rock.")]
        public float farScroll = 0.78f;
        public Liquid[] liquids = new Liquid[0];

        [Header("Light (1 is full daylight)")]
        public float ambient = 0.04f;
        public Color ambientColour = Color.white;

        [Tooltip("The share of its light a tile of air passes on.")]
        public float airKeeps = 0.91f;

        [Tooltip("The share a solid tile passes on.")]
        public float solidKeeps = 0.56f;

        public Color torchColour = Color.white;
        public float torchIntensity = 1.6f;
        public float torchGlow = 0.35f;
        public float torchGlowRadius = 3f;

        [Tooltip("What the wall layer is multiplied by: a wall is further back than the ground.")]
        public Color wallTint = Color.white;

        [Header("Day")]
        [Tooltip("Seconds of play a whole day takes.")]
        public float dayLength = 480f;

        [Tooltip("The hour a scene starts at.")]
        public float dayStart = 10f;

        public float sunrise = 5.5f;
        public float sunset = 19.5f;
        public DayKey[] day = new DayKey[0];

        [Header("View")]
        [Tooltip(
            "The camera's view in art pixels: its reference resolution, and the size the sky and the backdrops are drawn for."
        )]
        public int viewWidth = 1280;

        public int viewHeight = 720;

        [Tooltip("Pixels of the art to a unit: a tile is one unit, and this many pixels across.")]
        public int pixelsPerUnit = PixelsPerUnit;

        [Header("Backdrop")]
        public int skyBands = 24;
        public int skyOrder = -1010;

        [Tooltip("Units a second the clouds move to the right.")]
        public float cloudDrift = 0.25f;

        public Backdrop[] backdrops = new Backdrop[0];

        private Dictionary<char, Glyph> glyphs;
        private Dictionary<int, TileBase> shadeByMask;

        /// <summary>The camera's view in art pixels.</summary>
        public Vector2Int ViewPixels => new Vector2Int(viewWidth, viewHeight);

        /// <summary>
        /// Pixels of the art to a unit, as the standard ships it: a tile is 32 x 32. The art is
        /// drawn on a draft of 16 to the tile and ships twice as fine (docs/art/tiles-v2.md).
        /// </summary>
        public const int PixelsPerUnit = 32;

        /// <summary>
        /// A pixel of the draft, in units: a sixteenth of a tile. What a map gives beside a
        /// mark (how far aside, how far up) is counted in these, whatever the art ships at.
        /// </summary>
        public const float DraftPixel = 1f / 16f;

        /// <summary>The camera's view in units.</summary>
        public Vector2 View => new Vector2(viewWidth, viewHeight) / Mathf.Max(pixelsPerUnit, 1);

        /// <summary>The legend's entry for a map character; false for air and anything unknown.</summary>
        public bool TryGlyph(char character, out Glyph glyph)
        {
            if (glyphs == null)
            {
                glyphs = new Dictionary<char, Glyph>();
                foreach (var entry in legend)
                {
                    if (!string.IsNullOrEmpty(entry.character))
                    {
                        glyphs[entry.character[0]] = entry;
                    }
                }
            }
            return glyphs.TryGetValue(character, out glyph);
        }

        /// <summary>The shade tile for a mask of the ground round a wall cell (TilesV2Painter.ShadeMask); null for none.</summary>
        public TileBase ShadeFor(int mask)
        {
            if (shadeByMask == null)
            {
                shadeByMask = new Dictionary<int, TileBase>();
                foreach (var entry in shades)
                {
                    shadeByMask[entry.mask] = entry.tile;
                }
            }
            return shadeByMask.TryGetValue(mask, out var tile) ? tile : null;
        }

        /// <summary>The prop of a key; null when there is none.</summary>
        public Prop PropOf(string key)
        {
            foreach (var prop in props)
            {
                if (prop.key == key)
                {
                    return prop;
                }
            }
            return null;
        }

        /// <summary>The ledge of a key; null when there is none.</summary>
        public Ledge LedgeOf(string key)
        {
            foreach (var ledge in ledges)
            {
                if (ledge.key == key)
                {
                    return ledge;
                }
            }
            return null;
        }

        /// <summary>The region of a key; null when there is none.</summary>
        public Region RegionOf(string key)
        {
            foreach (var region in regions)
            {
                if (region.key == key)
                {
                    return region;
                }
            }
            return null;
        }

        /// <summary>One of the things' sprites, by its name in the manifest (`barrier_0`); null when there is none.</summary>
        public Sprite Thing(string name)
        {
            foreach (var sprite in things)
            {
                if (sprite != null && sprite.name == "tv2_thing_" + name)
                {
                    return sprite;
                }
            }
            return null;
        }

        /// <summary>The liquid of a key; null when there is none.</summary>
        public Liquid LiquidOf(string key)
        {
            foreach (var liquid in liquids)
            {
                if (liquid.key == key)
                {
                    return liquid;
                }
            }
            return null;
        }

        /// <summary>The day's values at an hour (0 to 24): mixed between the keys either side, round the clock.</summary>
        public DayValues DayAt(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            if (day.Length == 0)
            {
                return new DayValues
                {
                    hour = hour,
                    light = Color.white,
                    top = Color.white,
                    low = Color.white,
                    tint = Color.white,
                };
            }
            for (var i = 0; i < day.Length; i++)
            {
                var a = day[i];
                var last = i == day.Length - 1;
                var b = last ? day[0] : day[i + 1];
                var end = last ? 24f : b.hour;
                if (hour < a.hour || hour > end)
                {
                    continue;
                }
                var t = end <= a.hour ? 0f : (hour - a.hour) / (end - a.hour);
                return new DayValues
                {
                    hour = hour,
                    light = Color.LerpUnclamped(a.light, b.light, t),
                    top = Color.LerpUnclamped(a.top, b.top, t),
                    low = Color.LerpUnclamped(a.low, b.low, t),
                    tint = Color.LerpUnclamped(a.tint, b.tint, t),
                    stars = Mathf.LerpUnclamped(a.stars, b.stars, t),
                };
            }
            var first = day[0];
            return new DayValues
            {
                hour = hour,
                light = first.light,
                top = first.top,
                low = first.low,
                tint = first.tint,
                stars = first.stars,
            };
        }

        /// <summary>
        /// How far along its arc the body in the sky is at an hour, 0 (rising) to 1 (setting),
        /// and whether that body is the sun (else the moon).
        /// </summary>
        public float SkyArc(float hour, out bool sun)
        {
            hour = Mathf.Repeat(hour, 24f);
            sun = hour >= sunrise && hour <= sunset;
            if (sun)
            {
                return (hour - sunrise) / Mathf.Max(sunset - sunrise, 0.01f);
            }
            return Mathf.Repeat(hour - sunset, 24f) / Mathf.Max(24f - (sunset - sunrise), 0.01f);
        }

        private void OnValidate()
        {
            glyphs = null;
            shadeByMask = null;
        }

        /// <summary>What a map character stands for.</summary>
        [Serializable]
        public sealed class Glyph
        {
            [Tooltip("The character in the map text.")]
            public string character = "";

            public Layer layer;

            [Tooltip("The material, liquid or prop, by its key in the manifest.")]
            public string key = "";

            [Tooltip("The tile laid for it (ground, walls, liquids); none for a prop or a mark.")]
            public TileBase tile;
        }

        /// <summary>Overlays of the `from` materials on the sides of an `onto` tile that touch them.</summary>
        [Serializable]
        public sealed class Blend
        {
            public string key = "";
            public string onto = "";
            public string[] from = new string[0];

            [Tooltip(
                "The overlay for each mask of sides (1 up, 2 right, 4 down, 8 left); 16 entries, the first empty."
            )]
            public TileBase[] overlays = new TileBase[16];
        }

        /// <summary>The shade a wall cell shows for one mask of the ground round it.</summary>
        [Serializable]
        public sealed class Shade
        {
            public int mask;
            public TileBase tile;
        }

        /// <summary>One kind of decor and where it goes.</summary>
        [Serializable]
        public sealed class DecorRule
        {
            public string key = "";
            public Anchor anchor;

            [Tooltip("The materials it grows on.")]
            public string[] on = new string[0];

            public Where where;

            [Tooltip("The share of fitting cells that get it.")]
            [Range(0f, 1f)]
            public float chance;

            [Tooltip("How many cells it stacks, at least and at most.")]
            public int min = 1;

            public int max = 1;

            [Tooltip("The variants of the cell next to the ground of a stack of two or more.")]
            public TileBase[] first = new TileBase[0];

            [Tooltip("The variants of the cells between the first and the last.")]
            public TileBase[] middle = new TileBase[0];

            [Tooltip("The variants of the last cell: all there is of a decor one cell tall.")]
            public TileBase[] last = new TileBase[0];
        }

        /// <summary>A prop that gives light where it stands.</summary>
        [Serializable]
        public sealed class Prop
        {
            public string key = "";
            public Sprite[] frames = new Sprite[0];

            [Tooltip("The light it gives its tile is this colour times the intensity.")]
            public Color colour = Color.white;
            public float intensity = 1f;

            [Tooltip("Embers rise from it: a flame.")]
            public bool embers;
        }

        /// <summary>A ledge: its four pieces, by whether the same ledge goes on to the left and the right.</summary>
        [Serializable]
        public sealed class Ledge
        {
            public string key = "";
            public TileBase single;
            public TileBase left;
            public TileBase middle;
            public TileBase right;

            /// <summary>The piece for a cell whose neighbours to the left and right are, or are not, the same ledge.</summary>
            public TileBase Piece(bool goesLeft, bool goesRight)
            {
                if (goesLeft && goesRight)
                {
                    return middle;
                }
                return goesLeft ? right
                    : goesRight ? left
                    : single;
            }
        }

        /// <summary>A region of a world: what it is called and what makes its air its own.</summary>
        [Serializable]
        public sealed class Region
        {
            public string key = "";
            public string name = "";
            public string subtitle = "";

            [Tooltip("The faint light that hangs in its air, in the tile light's units.")]
            public Color ambient = Color.black;

            [Tooltip("The rock behind its caves, and the ragged strip along its top; none for open sky.")]
            public Sprite cave;
            public Sprite caveTop;

            [Tooltip("What stands far off in its caves, between the rock and the walls; none for none.")]
            public Sprite far;

            [Tooltip("What drifts in its air: how many a second (0 for nothing).")]
            public float airRate;
            public Color airColour = Color.white;

            [Tooltip("Units a second across, from and to.")]
            public Vector2 airAcross;

            [Tooltip("Units a second up, from and to.")]
            public Vector2 airUp;

            [Tooltip("Seconds a mote lives.")]
            public float airLife = 5f;

            [Tooltip("Whether the motes take the tile light.")]
            public bool airLit;
        }

        /// <summary>A liquid and how it is drawn and felt.</summary>
        [Serializable]
        public sealed class Liquid
        {
            public string key = "";
            public LiquidTile tile;

            [Tooltip("Takes the tile light (water); unlit when it gives light itself (lava).")]
            public bool lit = true;

            [Tooltip("The hero swims in it.")]
            public bool swim;

            [Tooltip("Hearts it takes from the hero who touches it; 0 for none.")]
            public int hazard;

            [Tooltip("Its tilemap's sorting order: in front of the hero.")]
            public int order = 20;
        }

        /// <summary>The day at one hour; the hours between are mixed.</summary>
        [Serializable]
        public sealed class DayKey
        {
            public float hour;

            [Tooltip("The colour of the daylight: what the sky's half of the tile light is multiplied by.")]
            public Color light = Color.white;

            [Tooltip("The sky's colour at the top of the view.")]
            public Color top = Color.white;

            [Tooltip("The sky's colour low down.")]
            public Color low = Color.white;

            [Tooltip("The tint of the far backdrop: ridges and clouds.")]
            public Color tint = Color.white;

            [Range(0f, 1f)]
            public float stars;
        }

        /// <summary>One layer of the backdrop, back to front.</summary>
        [Serializable]
        public sealed class Backdrop
        {
            public string key = "";
            public BackdropKind kind;
            public Sprite sprite;

            [Tooltip(
                "The share of the camera's movement the layer scrolls by: 0 stays on screen, 1 moves with the world."
            )]
            public float scroll;

            public int order;
            public bool lit;

            [Tooltip("A layer of heaven: the share of the camera's way up it moves with the world.")]
            public float scrollUp = 1f;

            [Tooltip(
                "A layer of heaven: the tiles over the surface line its foot stands at, for a camera on that line."
            )]
            public float rise;

            [Tooltip("A layer of heaven: units a second it moves to the right by itself.")]
            public float drift;

            [Tooltip("A layer of heaven: whether it takes the hour's tint, as the ridges and the clouds do.")]
            public bool tinted;
        }

        /// <summary>The day's values at one moment.</summary>
        public struct DayValues
        {
            public float hour;
            public Color light;
            public Color top;
            public Color low;
            public Color tint;
            public float stars;
        }

        public enum Layer
        {
            Terrain,
            Wall,
            Liquid,
            Prop,
            Mark,
            Ledge,
        }

        public enum Anchor
        {
            Floor,
            Ceiling,
        }

        public enum Where
        {
            Any,
            Sky,
            Cave,
        }

        public enum BackdropKind
        {
            Stars,
            Sun,
            Moon,
            Clouds,
            Ridge,
            Cave,
            CaveTop,

            /// <summary>Over a world with regions: islands far off, shafts of light, banks of cloud.</summary>
            Heaven,

            /// <summary>A picture a map stands somewhere itself (`@mark picture column row key`).</summary>
            Picture,
        }
    }
}
