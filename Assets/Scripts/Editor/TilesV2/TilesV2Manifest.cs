using System;
using System.IO;
using UnityEngine;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// The manifest tools/tiles_v2/gen_tiles_v2.py writes beside the Tiles V2 sheets
    /// (Assets/Art/TilesV2/tiles_v2.json): the tile size, the autotile frames, the
    /// materials, the blends, the map legend, the liquids, the decor rules, the shade
    /// masks, the glint, the numbers of the light and of the day and the backdrop layers.
    /// The Unity builder makes the tiles and the tile set asset from it.
    /// </summary>
    [Serializable]
    public sealed class TilesV2Manifest
    {
        public const string Folder = "Assets/Art/TilesV2";
        public const string TileFolder = Folder + "/RuleTiles";
        public const string ManifestPath = Folder + "/tiles_v2.json";
        public const string SetPath = Folder + "/TilesV2Set.asset";
        public const string GlintPath = TileFolder + "/glint.asset";
        public const string ShadeFullPath = TileFolder + "/Shade/shade_full.asset";
        public const string ShadeFullSprite = "tv2_shade_full";
        public const string GlintNoneSprite = "tv2_glint_none";

        public int tile;
        public int ppu;
        public ViewSection view = new ViewSection();
        public int variants;
        public int[] frames = Array.Empty<int>();
        public string[] slopes = Array.Empty<string>();

        /// <summary>The steep slopes (two cells up for one across) a material that asks for them has besides.</summary>
        public string[] steep = Array.Empty<string>();
        public MaterialEntry[] materials = Array.Empty<MaterialEntry>();
        public BlendEntry[] blends = Array.Empty<BlendEntry>();
        public LegendEntry[] legend = Array.Empty<LegendEntry>();
        public PropEntry[] props = Array.Empty<PropEntry>();
        public LedgeSection ledges = new LedgeSection();
        public ThingSection things = new ThingSection();
        public RegionEntry[] regions = Array.Empty<RegionEntry>();
        public LiquidEntry[] liquids = Array.Empty<LiquidEntry>();
        public DecorSection decor = new DecorSection();
        public ShadeSection shade = new ShadeSection();
        public GlintSection glint = new GlintSection();
        public LightSection light = new LightSection();
        public DaySection day = new DaySection();
        public SkySection sky = new SkySection();
        public BackdropEntry[] backdrops = Array.Empty<BackdropEntry>();

        /// <summary>The camera's view in art pixels.</summary>
        [Serializable]
        public sealed class ViewSection
        {
            public int width;
            public int height;
        }

        /// <summary>One material: its sheet, terrain or wall, its join group and the light it gives.</summary>
        [Serializable]
        public sealed class MaterialEntry
        {
            public string key;
            public string kind;
            public string group;
            public string sheet;
            public bool slopes;
            public bool steep;
            public bool open;
            public float[] emits = Array.Empty<float>();

            public bool IsWall => kind == "wall";
        }

        /// <summary>One blend: `from` materials laid over the edges of an `onto` tile.</summary>
        [Serializable]
        public sealed class BlendEntry
        {
            public string key;
            public string onto;
            public string[] from = Array.Empty<string>();
            public string sheet;
            public int[] masks = Array.Empty<int>();
        }

        /// <summary>One character of a map: the layer it is laid on and the key (and slope) it stands for.</summary>
        [Serializable]
        public sealed class LegendEntry
        {
            public string character;
            public string layer;
            public string key;
            public string slope;
        }

        /// <summary>One prop that gives light: a sheet of frames, the light it gives its tile, whether embers rise from it.</summary>
        [Serializable]
        public sealed class PropEntry
        {
            public string key;
            public string sheet;
            public int frames;
            public float[] colour = Array.Empty<float>();
            public float intensity;
            public bool embers;
        }

        /// <summary>The ledges: one sheet, a row for each, its pieces side by side.</summary>
        [Serializable]
        public sealed class LedgeSection
        {
            public string sheet;
            public int thick;
            public string[] keys = Array.Empty<string>();
            public string[] pieces = Array.Empty<string>();
        }

        /// <summary>The sprites the game's own things on a map are drawn with (a barrier, a seal, a relic).</summary>
        [Serializable]
        public sealed class ThingSection
        {
            public string sheet;
            public string[] names = Array.Empty<string>();
        }

        /// <summary>One region of a world: its name, the light in its air, the rock behind its caves, what drifts in it.</summary>
        [Serializable]
        public sealed class RegionEntry
        {
            public string key;
            public string name;
            public string subtitle;
            public float[] ambient = Array.Empty<float>();
            public string cave;
            public string caveTop;
            public string far;
            public AirEntry air = new AirEntry();
        }

        /// <summary>What drifts in a region's air: how many a second, their colour, their speed across and up, their life.</summary>
        [Serializable]
        public sealed class AirEntry
        {
            public float rate;
            public float[] colour = Array.Empty<float>();
            public float[] across = Array.Empty<float>();
            public float[] up = Array.Empty<float>();
            public float life;
            public bool lit;
        }

        /// <summary>One liquid: its sheet of surface and body frames and what it does.</summary>
        [Serializable]
        public sealed class LiquidEntry
        {
            public string key;
            public string sheet;
            public int frames;
            public float fps;
            public bool lit;
            public float[] keeps = Array.Empty<float>();
            public float[] emits = Array.Empty<float>();
            public bool swim;
            public int hazard;
            public int order;
        }

        /// <summary>The decor sheet and the rules that scatter it.</summary>
        [Serializable]
        public sealed class DecorSection
        {
            public string sheet;
            public int floorSalt;
            public int ceilingSalt;
            public DecorRule[] rules = Array.Empty<DecorRule>();
        }

        /// <summary>One kind of decor: where it grows and the sprite sets of a stack of it.</summary>
        [Serializable]
        public sealed class DecorRule
        {
            public string key;
            public string anchor;
            public string[] on = Array.Empty<string>();
            public string where;
            public float chance;
            public int variants;
            public int min;
            public int max;
            public string first;
            public string middle;
            public string last;
        }

        /// <summary>The shade sheet: the full tile and one for each mask.</summary>
        [Serializable]
        public sealed class ShadeSection
        {
            public string sheet;
            public int[] masks = Array.Empty<int>();
        }

        /// <summary>The glint sheet, the materials that glint and the loop a glinting tile plays.</summary>
        [Serializable]
        public sealed class GlintSection
        {
            public string sheet;
            public string[] on = Array.Empty<string>();
            public float fps;
            public int[] loop = Array.Empty<int>();
        }

        /// <summary>The numbers of the tile light.</summary>
        [Serializable]
        public sealed class LightSection
        {
            public float ambient;
            public float[] ambientColour = Array.Empty<float>();
            public float airKeeps;
            public float solidKeeps;
            public float[] torchColour = Array.Empty<float>();
            public float torchIntensity;
            public float torchGlow;
            public float torchGlowRadius;
            public float[] wallTint = Array.Empty<float>();
        }

        /// <summary>The day: its length, the hour it starts at, the sun's hours and the keys.</summary>
        [Serializable]
        public sealed class DaySection
        {
            public float length;
            public float start;
            public float sunrise;
            public float sunset;
            public DayKey[] keys = Array.Empty<DayKey>();
        }

        /// <summary>The day's values at one hour.</summary>
        [Serializable]
        public sealed class DayKey
        {
            public float hour;
            public float[] light = Array.Empty<float>();
            public float[] top = Array.Empty<float>();
            public float[] low = Array.Empty<float>();
            public float[] tint = Array.Empty<float>();
            public float stars;
        }

        /// <summary>How the sky is drawn.</summary>
        [Serializable]
        public sealed class SkySection
        {
            public int bands;
            public int order;
            public float cloudDrift;
            public float caveScroll;
            public int caveOrder;
            public float farScroll;
        }

        /// <summary>One layer of the backdrop, back to front.</summary>
        [Serializable]
        public sealed class BackdropEntry
        {
            public string key;
            public string kind;
            public float scroll;
            public int order;
            public bool lit;
            public float scrollUp = 1f;
            public float rise;
            public float drift;
            public bool tinted;
        }

        /// <summary>The slopes a material has, in the order they lie in its sheet: none, the two, or the two and the steep ones.</summary>
        public string[] SlopesOf(MaterialEntry material)
        {
            var kinds = new System.Collections.Generic.List<string>();
            if (material.slopes)
            {
                kinds.AddRange(slopes);
            }
            if (material.steep)
            {
                kinds.AddRange(steep);
            }
            return kinds.ToArray();
        }

        public static TilesV2Manifest Load()
        {
            return JsonUtility.FromJson<TilesV2Manifest>(File.ReadAllText(ManifestPath));
        }

        /// <summary>A colour from the manifest's three floats; black when they are missing.</summary>
        public static Color ColourOf(float[] rgb) =>
            rgb != null && rgb.Length >= 3 ? new Color(rgb[0], rgb[1], rgb[2], 1f) : Color.black;

        public static string SheetPath(string sheet) => Folder + "/" + sheet;

        public static string TilePath(string key) => TileFolder + "/" + key + ".asset";

        public static string SlopePath(string key, string slope) => TileFolder + "/" + key + "_" + slope + ".asset";

        public static string BlendPath(string key, int sides) =>
            TileFolder + "/Blends/" + key + "_s" + sides + ".asset";

        public static string DecorPath(string part, int variant) =>
            TileFolder + "/Decor/" + part + "_" + variant + ".asset";

        public static string ShadePath(int mask) => TileFolder + "/Shade/shade_m" + mask + ".asset";

        public static string FrameSprite(string key, int frame, int variant) => $"tv2_{key}_f{frame}_v{variant}";

        public static string SlopeSprite(string key, string slope, int variant) => $"tv2_{key}_{slope}_v{variant}";

        public static string BlendSprite(string key, int sides, int variant) => $"tv2_{key}_s{sides}_v{variant}";

        public static string DecorSprite(string part, int variant) => $"tv2_decor_{part}_{variant}";

        public static string ShadeSprite(int mask) => $"tv2_shade_m{mask}";

        public static string LiquidSprite(string key, string part, int frame) => $"tv2_{key}_{part}_{frame}";

        public static string GlintSprite(int variant, int frame) => $"tv2_glint_v{variant}_{frame}";

        public static string PropSprite(string key, int frame) => $"tv2_{key}_{frame}";

        public static string LedgePath(string key, string piece) =>
            TileFolder + "/Ledges/" + key + "_" + piece + ".asset";

        public static string LedgeSprite(string key, string piece) => $"tv2_ledge_{key}_{piece}";

        public static string ThingSprite(string name) => $"tv2_thing_{name}";
    }
}
