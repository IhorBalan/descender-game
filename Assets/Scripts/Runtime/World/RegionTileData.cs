using System;
using System.Collections.Generic;

namespace Descender.World
{
    /// <summary>
    /// The compact tile layout of a region, as tools/unity/import_godot_regions.py
    /// writes it next to the region scene: a header line, then one line per layer,
    /// "layer &lt;Name&gt; &lt;count&gt; &lt;base64&gt;", the base64 holding little-endian
    /// int16 triples (x, y, tile) in Godot cell coordinates (y down from the region's
    /// top row). Kept this small because the twelve regions hold 200k cells, which
    /// would make the scenes unreadable and the repo heavy if painted into them.
    /// </summary>
    public static class RegionTileData
    {
        public const string Header = "descender-tiles 1";

        public struct Cell
        {
            public int X;
            public int Y;
            public int Tile;
        }

        public sealed class Data
        {
            public readonly Dictionary<string, Cell[]> Layers = new Dictionary<string, Cell[]>();
            public int[] Kinds = new int[0];

            public TileKind KindOf(int tile) =>
                tile >= 0 && tile < Kinds.Length ? (TileKind)Kinds[tile] : TileKind.None;
        }

        public static Data Parse(string text)
        {
            var data = new Data();
            var layers = data.Layers;
            if (string.IsNullOrEmpty(text))
            {
                return data;
            }
            var lines = text.Split('\n');
            if (lines.Length == 0 || lines[0].Trim() != Header)
            {
                throw new FormatException("Not a region tile file: missing header '" + Header + "'.");
            }
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                var parts = line.Split(' ');
                if (parts.Length == 3 && parts[0] == "kinds")
                {
                    var kindBytes = Convert.FromBase64String(parts[2]);
                    data.Kinds = new int[kindBytes.Length / 2];
                    for (var k = 0; k < data.Kinds.Length; k++)
                    {
                        data.Kinds[k] = BitConverter.ToInt16(kindBytes, k * 2);
                    }
                    continue;
                }
                if (parts.Length != 4 || parts[0] != "layer")
                {
                    throw new FormatException($"Bad tile line {i + 1}: '{line}'.");
                }
                var count = int.Parse(parts[2]);
                var bytes = Convert.FromBase64String(parts[3]);
                if (bytes.Length != count * 6)
                {
                    throw new FormatException($"Layer {parts[1]} says {count} cells but holds {bytes.Length / 6}.");
                }
                var cells = new Cell[count];
                for (var c = 0; c < count; c++)
                {
                    var at = c * 6;
                    cells[c] = new Cell
                    {
                        X = BitConverter.ToInt16(bytes, at),
                        Y = BitConverter.ToInt16(bytes, at + 2),
                        Tile = BitConverter.ToInt16(bytes, at + 4),
                    };
                }
                layers[parts[1]] = cells;
            }
            return data;
        }

        /// <summary>The text for a set of layers; the inverse of Parse, for tests and tools.</summary>
        public static string Write(Dictionary<string, Cell[]> layers, int[] kinds)
        {
            var text = new System.Text.StringBuilder(Header);
            text.Append('\n');
            if (kinds != null && kinds.Length > 0)
            {
                var kindBytes = new byte[kinds.Length * 2];
                for (var k = 0; k < kinds.Length; k++)
                {
                    BitConverter.GetBytes((short)kinds[k]).CopyTo(kindBytes, k * 2);
                }
                text.Append("kinds ")
                    .Append(kinds.Length)
                    .Append(' ')
                    .Append(Convert.ToBase64String(kindBytes))
                    .Append('\n');
            }
            foreach (var pair in layers)
            {
                var bytes = new byte[pair.Value.Length * 6];
                for (var c = 0; c < pair.Value.Length; c++)
                {
                    var cell = pair.Value[c];
                    var at = c * 6;
                    BitConverter.GetBytes((short)cell.X).CopyTo(bytes, at);
                    BitConverter.GetBytes((short)cell.Y).CopyTo(bytes, at + 2);
                    BitConverter.GetBytes((short)cell.Tile).CopyTo(bytes, at + 4);
                }
                text.Append("layer ")
                    .Append(pair.Key)
                    .Append(' ')
                    .Append(pair.Value.Length)
                    .Append(' ')
                    .Append(Convert.ToBase64String(bytes))
                    .Append('\n');
            }
            return text.ToString();
        }
    }
}
