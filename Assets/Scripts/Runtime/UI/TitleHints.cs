using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// The gilded screens' shared frame: the dark ground (opaque, or a veil over the
    /// game) with its embers and the 1920 x 1080 stage on it (<see cref="MakeStage"/>),
    /// and the key hints along the bottom (<see cref="Build"/>): the D-pad and
    /// Navigate, then a key cap and what it does for confirming and for going back,
    /// centred between two fading hairlines. Used by the title, the pause menu, the
    /// camp and its pages.
    /// </summary>
    public static class TitleHints
    {
        /// <summary>Where the hints sit, in pixels from the stage's top.</summary>
        public const float Y = 1002f;

        public const float TextSize = 21f;

        /// <summary>How much of the game shows through a veiled ground: none at 1.</summary>
        public const float VeilAlpha = 0.86f;

        /// <summary>
        /// Scales the canvas smoothly from 1920 x 1080, lays the ground (opaque, or a
        /// veil the game shows through), and returns the stage centred on it with the
        /// embers already rising on it. `group` fades the whole stage.
        /// </summary>
        public static RectTransform MakeStage(
            RectTransform root,
            MenuTheme theme,
            bool veil,
            int seed,
            out CanvasGroup group
        )
        {
            if (TitleKit.Font == null && theme != null)
            {
                TitleKit.Font = theme.titleFont;
            }
            TitleScreen.ScaleSmooth(root.GetComponent<Canvas>());
            var ground = UiKit.Box(root, "Background", TitleKit.Background);
            if (veil)
            {
                var color = TitleKit.Background;
                color.a = VeilAlpha;
                ground.color = color;
            }
            UiKit.Fill(ground.rectTransform);
            var stage = UiKit.Rect(root, "Stage");
            stage.anchorMin = new Vector2(0.5f, 0.5f);
            stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = new Vector2(TitleKit.Width, TitleKit.Height);
            group = stage.gameObject.AddComponent<CanvasGroup>();
            TitleEmbers.Make(stage, theme != null ? theme.mote : null, seed);
            return stage;
        }

        /// <summary>
        /// Lays the hints under `keys` (cleared first) for the keyboard or the pad:
        /// Navigate, then `confirm` and `back` with their keys.
        /// </summary>
        public static void Build(RectTransform keys, MenuTheme theme, bool gamepad, string confirm, string back)
        {
            UiKit.Clear(keys);
            var row = UiKit.Rect(keys, "Row");
            UiKit.Fill(row);
            var x = 0f;
            var parts = new List<RectTransform>();
            if (theme.keyDpad != null)
            {
                var pad = TitleKit.Sprite(row, "Dpad", theme.keyDpad, 0f, Y);
                var padWidth = TitleKit.SizeOf(theme.keyDpad).x;
                pad.rectTransform.anchoredPosition = new Vector2(x + padWidth * 0.5f, -Y);
                parts.Add(pad.rectTransform);
                x += padWidth + 12f;
            }
            x = Hint(row, parts, "Navigate", x) + 46f;
            x = Hint(row, parts, confirm, x + KeyCap(row, parts, theme, gamepad ? "A" : "Enter", x) + 12f) + 46f;
            x = Hint(row, parts, back, x + KeyCap(row, parts, theme, gamepad ? "B" : "Esc", x) + 12f);
            // Everything was laid out from 0; shift it so the row is centred.
            var shift = TitleKit.Width * 0.5f - x * 0.5f;
            foreach (var part in parts)
            {
                part.anchoredPosition += new Vector2(shift, 0f);
            }
            if (theme.ruleSide == null)
            {
                return;
            }
            var reach = TitleKit.SizeOf(theme.ruleSide).x * 0.5f + 34f;
            TitleKit.Sprite(keys, "Line", theme.ruleSide, shift - reach, Y);
            var right = TitleKit.Sprite(keys, "Line", theme.ruleSide, shift + x + reach, Y);
            right.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }

        /// <summary>A key cap at `x`, added to the row's parts; returns its width.</summary>
        private static float KeyCap(Transform row, List<RectTransform> parts, MenuTheme theme, string key, float x)
        {
            var w = TitleKit.KeyCap(row, theme.keyCap, key, x, Y);
            parts.Add((RectTransform)row.GetChild(row.childCount - 1));
            return w;
        }

        /// <summary>What a key does, its left edge at `x`; returns where it ends.</summary>
        private static float Hint(Transform row, List<RectTransform> parts, string text, float x)
        {
            var label = TitleKit.Label(row, text, TextSize, TitleKit.Muted, 4f, 0f, Y, 400f);
            var w = TitleKit.WidthOf(label);
            TitleKit.Center(label.rectTransform, x + w * 0.5f, Y, w + 4f, 44f);
            parts.Add(label.rectTransform);
            return x + w;
        }
    }
}
