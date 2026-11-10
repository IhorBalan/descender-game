using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Descender.UI
{
    /// <summary>
    /// A bitmap font baked by tools/title_textures.py: printable ASCII in cells one
    /// line tall on a sheet, and for each character its cell, where its ink starts
    /// relative to the pen and how far the pen then moves (in fractions of a pixel,
    /// rounded only where a glyph lands). Read once from the sheet and its JSON;
    /// <see cref="PixelText"/> draws with it.
    /// </summary>
    public sealed class PixelFont
    {
        public const int First = 32;
        public const int Last = 126;

        private readonly int[] cellX = new int[Last - First + 1];
        private readonly int[] cellY = new int[Last - First + 1];
        private readonly int[] width = new int[Last - First + 1];
        private readonly int[] ink = new int[Last - First + 1];
        private readonly float[] advance = new float[Last - First + 1];

        private PixelFont(Texture2D sheet, int line, int ascent)
        {
            Sheet = sheet;
            Line = line;
            Ascent = ascent;
        }

        public Texture2D Sheet { get; }

        /// <summary>The height of a line, and of every cell, in pixels.</summary>
        public int Line { get; }

        /// <summary>From the top of a line to its baseline.</summary>
        public int Ascent { get; }

        /// <summary>Reads the metrics JSON; null when either half is missing.</summary>
        public static PixelFont Read(Texture2D sheet, string json)
        {
            if (sheet == null || string.IsNullOrEmpty(json))
            {
                return null;
            }
            var data = JObject.Parse(json);
            var font = new PixelFont(sheet, (int)data["line"], (int)data["ascent"]);
            var glyphs = (JObject)data["glyphs"];
            for (var code = First; code <= Last; code++)
            {
                if (!(glyphs[code.ToString()] is JArray glyph))
                {
                    continue;
                }
                var i = code - First;
                font.cellX[i] = (int)glyph[0];
                font.cellY[i] = (int)glyph[1];
                font.width[i] = (int)glyph[2];
                font.ink[i] = (int)glyph[3];
                font.advance[i] = (float)glyph[4];
            }
            return font;
        }

        /// <summary>Where a character's cell is on the sheet, in pixels from its top left; false for one it lacks.</summary>
        public bool TryGlyph(char c, out RectInt cell, out int inkOffset, out float penAdvance)
        {
            var i = c - First;
            if (i < 0 || i > Last - First)
            {
                // Anything outside ASCII is drawn as a question mark rather than vanishing.
                i = '?' - First;
            }
            cell = new RectInt(cellX[i], cellY[i], width[i], Line);
            inkOffset = ink[i];
            penAdvance = advance[i];
            return true;
        }

        /// <summary>
        /// Where a text's ink starts and ends, in whole pixels from the pen's start,
        /// with `spacing` extra pixels between characters. Empty text measures 0..0.
        /// </summary>
        public void Measure(string text, float spacing, out int left, out int right)
        {
            left = 0;
            right = 0;
            var any = false;
            var pen = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                TryGlyph(text[i], out var cell, out var inkOffset, out var step);
                if (cell.width > 0)
                {
                    var x = Mathf.RoundToInt(pen) + inkOffset;
                    left = any ? Mathf.Min(left, x) : x;
                    right = any ? Mathf.Max(right, x + cell.width) : x + cell.width;
                    any = true;
                }
                pen += step + spacing;
            }
        }
    }
}
