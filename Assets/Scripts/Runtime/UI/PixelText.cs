using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// A line of text drawn from a <see cref="PixelFont"/>: one quad per glyph, each
    /// on a whole pixel from the rect's top left, at a whole-number scale, with
    /// whole-pixel letter spacing. The text starts where its ink starts, so a rect
    /// as wide as <see cref="InkWidth"/> holds it exactly. Tinted by the graphic's
    /// colour; the sheet is white.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PixelText : MaskableGraphic
    {
        private PixelFont font;
        private string text = "";
        private int spacing;
        private int scale = 1;

        public override Texture mainTexture => font != null ? font.Sheet : s_WhiteTexture;

        public string Text
        {
            get => text;
            set
            {
                var next = value ?? "";
                if (next != text)
                {
                    text = next;
                    SetVerticesDirty();
                }
            }
        }

        /// <summary>How wide the text's ink is, in pixels at this scale.</summary>
        public int InkWidth
        {
            get
            {
                if (font == null)
                {
                    return 0;
                }
                font.Measure(text, spacing, out var left, out var right);
                return (right - left) * scale;
            }
        }

        /// <summary>A line's height at this scale.</summary>
        public int LineHeight => font != null ? font.Line * scale : 0;

        public void Setup(PixelFont pixelFont, int letterSpacing, int pixelScale)
        {
            font = pixelFont;
            spacing = letterSpacing;
            scale = Mathf.Max(1, pixelScale);
            raycastTarget = false;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (font == null || font.Sheet == null || text.Length == 0)
            {
                return;
            }
            font.Measure(text, spacing, out var left, out _);
            var rect = rectTransform.rect;
            var sheetWidth = (float)font.Sheet.width;
            var sheetHeight = (float)font.Sheet.height;
            var pen = 0f;
            var tint = (Color32)color;
            for (var i = 0; i < text.Length; i++)
            {
                font.TryGlyph(text[i], out var cell, out var inkOffset, out var step);
                if (cell.width > 0)
                {
                    var x = rect.xMin + (Mathf.RoundToInt(pen) + inkOffset - left) * scale;
                    var top = rect.yMax;
                    var bottom = top - cell.height * scale;
                    var right = x + cell.width * scale;
                    var u0 = cell.x / sheetWidth;
                    var u1 = (cell.x + cell.width) / sheetWidth;
                    // The sheet's rows count from its top; texture coordinates from its bottom.
                    var v1 = 1f - cell.y / sheetHeight;
                    var v0 = 1f - (cell.y + cell.height) / sheetHeight;
                    var index = vh.currentVertCount;
                    vh.AddVert(new Vector3(x, bottom), tint, new Vector2(u0, v0));
                    vh.AddVert(new Vector3(x, top), tint, new Vector2(u0, v1));
                    vh.AddVert(new Vector3(right, top), tint, new Vector2(u1, v1));
                    vh.AddVert(new Vector3(right, bottom), tint, new Vector2(u1, v0));
                    vh.AddTriangle(index, index + 1, index + 2);
                    vh.AddTriangle(index + 2, index + 3, index);
                }
                pen += step + spacing;
            }
        }
    }
}
