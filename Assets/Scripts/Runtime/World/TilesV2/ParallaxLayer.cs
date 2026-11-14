using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// One Tiles V2 backdrop layer: a sprite drawn tiled sideways that follows the camera
    /// by the share of its movement it does not scroll (scroll 0 stays on screen, 1 moves
    /// with the world). It sits at its anchor when the camera stands at the camera anchor,
    /// and snaps to whole art pixels so the pixel art never shimmers.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ParallaxLayer : MonoBehaviour
    {
        private const float PixelsPerUnit = 16f;

        [Tooltip("The share of the camera's movement the layer scrolls by: 0 stays on screen, 1 moves with the world.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float scroll;

        [Tooltip("Where the layer sits when the camera stands at the camera anchor.")]
        [SerializeField]
        private Vector2 anchor;

        [Tooltip("The camera position the anchor is measured from.")]
        [SerializeField]
        private Vector2 cameraAnchor;

        public float Scroll => scroll;

        /// <summary>Sets the layer up (the sample builder's one entry point).</summary>
        public void Configure(float layerScroll, Vector2 layerAnchor, Vector2 viewAnchor)
        {
            scroll = Mathf.Clamp01(layerScroll);
            anchor = layerAnchor;
            cameraAnchor = viewAnchor;
            Place(viewAnchor);
        }

        /// <summary>Puts the layer where it belongs for a camera at `cameraPosition`.</summary>
        public void Place(Vector2 cameraPosition)
        {
            var at = PositionFor(anchor, cameraAnchor, cameraPosition, scroll);
            transform.position = new Vector3(at.x, at.y, transform.position.z);
        }

        /// <summary>
        /// Where a layer anchored at `layerAnchor` (for a camera at `viewAnchor`) sits for a
        /// camera at `cameraPosition`, snapped to whole art pixels.
        /// </summary>
        public static Vector2 PositionFor(Vector2 layerAnchor, Vector2 viewAnchor, Vector2 cameraPosition, float scroll)
        {
            var at = layerAnchor + (cameraPosition - viewAnchor) * (1f - scroll);
            return new Vector2(
                Mathf.Round(at.x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(at.y * PixelsPerUnit) / PixelsPerUnit
            );
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera != null)
            {
                Place(camera.transform.position);
            }
        }
    }
}
