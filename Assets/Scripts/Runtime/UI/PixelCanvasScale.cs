using UnityEngine;
using UnityEngine.UI;

namespace Descender.UI
{
    /// <summary>
    /// Keeps a canvas at a whole-number scale of the 320 x 180 view, the largest
    /// that fits the window, so every art pixel is the same number of screen pixels
    /// and the 9 px raster font stays crisp. The canvas then spans the window at
    /// that scale; content placed on a 320 x 180 stage in its middle stays whole.
    /// Used by the title screen.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class PixelCanvasScale : MonoBehaviour
    {
        private CanvasScaler scaler;
        private int width;
        private int height;

        /// <summary>The largest whole scale at which the 320 x 180 view fits the screen, at least 1.</summary>
        public static int ScaleFor(int screenWidth, int screenHeight) =>
            Mathf.Max(1, Mathf.Min(screenWidth / UiTheme.Width, screenHeight / UiTheme.Height));

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            Apply();
        }

        private void Update()
        {
            if (Screen.width != width || Screen.height != height)
            {
                Apply();
            }
        }

        private void Apply()
        {
            width = Screen.width;
            height = Screen.height;
            scaler.scaleFactor = ScaleFor(width, height);
        }
    }
}
