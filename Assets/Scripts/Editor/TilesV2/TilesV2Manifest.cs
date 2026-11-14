using System;
using System.IO;
using UnityEngine;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// The manifest tools/tiles_v2/gen_tiles_v2.py writes beside the Tiles V2 sheets
    /// (Assets/Art/TilesV2/tiles_v2.json): the tile size, the autotile frames, the
    /// materials, the blends and the backdrop layers the Unity builders make assets from.
    /// </summary>
    [Serializable]
    public sealed class TilesV2Manifest
    {
        public const string Folder = "Assets/Art/TilesV2";
        public const string TileFolder = Folder + "/RuleTiles";
        public const string ManifestPath = Folder + "/tiles_v2.json";

        public int tile;
        public int ppu;
        public int variants;
        public int[] frames = Array.Empty<int>();
        public string[] slopes = Array.Empty<string>();
        public MaterialEntry[] materials = Array.Empty<MaterialEntry>();
        public BlendEntry[] blends = Array.Empty<BlendEntry>();
        public BackdropEntry[] backdrops = Array.Empty<BackdropEntry>();

        /// <summary>One material: its sheet, terrain or wall, and its join group.</summary>
        [Serializable]
        public sealed class MaterialEntry
        {
            public string key;
            public string kind;
            public string group;
            public string sheet;
            public bool slopes;

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

        /// <summary>One parallax layer of the backdrop, back to front.</summary>
        [Serializable]
        public sealed class BackdropEntry
        {
            public string key;
            public float scroll;
            public int order;
        }

        public static TilesV2Manifest Load()
        {
            return JsonUtility.FromJson<TilesV2Manifest>(File.ReadAllText(ManifestPath));
        }

        public static string TilePath(string key) => TileFolder + "/" + key + ".asset";

        public static string SlopePath(string key, string slope) => TileFolder + "/" + key + "_" + slope + ".asset";

        public static string BlendPath(string key, int sides) =>
            TileFolder + "/Blends/" + key + "_s" + sides + ".asset";

        public static string FrameSprite(string key, int frame, int variant) => $"tv2_{key}_f{frame}_v{variant}";

        public static string SlopeSprite(string key, string slope, int variant) => $"tv2_{key}_{slope}_v{variant}";

        public static string BlendSprite(string key, int sides, int variant) => $"tv2_{key}_s{sides}_v{variant}";
    }
}
