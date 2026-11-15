using System.Collections.Generic;
using UnityEngine;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// The 8-neighbour masks of the Tiles V2 autotile frames, the same bits as
    /// tools/tiles_v2/gen_tiles_v2.py: N 1, NE 2, E 4, SE 8, S 16, SW 32, W 64, NW 128,
    /// with y up. A corner counts only when both edges beside it are solid, which leaves
    /// 47 frames.
    /// </summary>
    public static class TilesV2Masks
    {
        public const int N = 1;
        public const int NE = 2;
        public const int E = 4;
        public const int SE = 8;
        public const int S = 16;
        public const int SW = 32;
        public const int W = 64;
        public const int NW = 128;

        /// <summary>Each neighbour's bit and cell offset, y up.</summary>
        public static readonly (int bit, Vector3Int offset)[] Neighbours =
        {
            (N, new Vector3Int(0, 1, 0)),
            (NE, new Vector3Int(1, 1, 0)),
            (E, new Vector3Int(1, 0, 0)),
            (SE, new Vector3Int(1, -1, 0)),
            (S, new Vector3Int(0, -1, 0)),
            (SW, new Vector3Int(-1, -1, 0)),
            (W, new Vector3Int(-1, 0, 0)),
            (NW, new Vector3Int(-1, 1, 0)),
        };

        /// <summary>The frame a neighbour mask shows: corners kept only between two solid edges.</summary>
        public static int Canonical(int mask)
        {
            var result = mask & (N | E | S | W);
            if ((mask & NE) != 0 && (mask & N) != 0 && (mask & E) != 0)
            {
                result |= NE;
            }
            if ((mask & SE) != 0 && (mask & S) != 0 && (mask & E) != 0)
            {
                result |= SE;
            }
            if ((mask & SW) != 0 && (mask & S) != 0 && (mask & W) != 0)
            {
                result |= SW;
            }
            if ((mask & NW) != 0 && (mask & N) != 0 && (mask & W) != 0)
            {
                result |= NW;
            }
            return result;
        }

        /// <summary>Whether a neighbour decides the frame: every edge, and a corner between two solid edges.</summary>
        public static bool Matters(int frame, int bit)
        {
            switch (bit)
            {
                case NE:
                    return (frame & N) != 0 && (frame & E) != 0;
                case SE:
                    return (frame & S) != 0 && (frame & E) != 0;
                case SW:
                    return (frame & S) != 0 && (frame & W) != 0;
                case NW:
                    return (frame & N) != 0 && (frame & W) != 0;
                default:
                    return true;
            }
        }

        /// <summary>Every distinct frame, ascending (47 of them).</summary>
        public static List<int> AllFrames()
        {
            var seen = new SortedSet<int>();
            for (var mask = 0; mask < 256; mask++)
            {
                seen.Add(Canonical(mask));
            }
            return new List<int>(seen);
        }
    }
}
