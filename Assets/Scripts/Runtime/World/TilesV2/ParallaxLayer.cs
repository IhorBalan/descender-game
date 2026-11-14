using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// One Tiles V2 backdrop layer: a sprite drawn tiled that follows the camera by the
    /// share of its movement it does not scroll, each way on its own (scroll 0 stays on
    /// screen, 1 moves with the world). It sits at its anchor when the camera stands at the
    /// camera anchor. Where the picture repeats, the layer jumps by whole repeats to stay
    /// under the camera, so it never runs out; it can drift sideways with time (clouds),
    /// and it snaps to whole art pixels so the pixel art never shimmers.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ParallaxLayer : MonoBehaviour
    {
        private const float PixelsPerUnit = 16f;

        [Tooltip(
            "The share of the camera's movement the layer scrolls by, across and up: 0 stays on screen, 1 moves with the world."
        )]
        [SerializeField]
        private Vector2 scroll;

        [Tooltip("Where the layer sits when the camera stands at the camera anchor.")]
        [SerializeField]
        private Vector2 anchor;

        [Tooltip("The camera position the anchor is measured from.")]
        [SerializeField]
        private Vector2 cameraAnchor;

        [Tooltip("The size the picture repeats at, in units, each way; 0 where it does not repeat.")]
        [SerializeField]
        private Vector2 repeat;

        [Tooltip("Units a second the layer moves to the right by itself, while playing.")]
        [SerializeField]
        private float drift;

        private SpriteRenderer picture;

        public Vector2 Scroll => scroll;
        public Vector2 Repeat => repeat;

        /// <summary>Sets the layer up (the world builder's one entry point).</summary>
        public void Configure(
            Vector2 layerScroll,
            Vector2 layerAnchor,
            Vector2 viewAnchor,
            Vector2 layerRepeat,
            float layerDrift
        )
        {
            scroll = new Vector2(Mathf.Clamp01(layerScroll.x), Mathf.Clamp01(layerScroll.y));
            anchor = layerAnchor;
            cameraAnchor = viewAnchor;
            repeat = layerRepeat;
            drift = layerDrift;
            Place(viewAnchor);
        }

        /// <summary>Puts the layer where it belongs for a camera at `cameraPosition`.</summary>
        public void Place(Vector2 cameraPosition)
        {
            var drifted = anchor + new Vector2(Application.isPlaying ? drift * Time.time : 0f, 0f);
            // The picture's middle is not where the layer stands when its pivot is at its foot.
            if (picture == null)
            {
                picture = GetComponent<SpriteRenderer>();
            }
            Vector2 toMiddle = picture.bounds.center - transform.position;
            var at = PositionFor(drifted, cameraAnchor, cameraPosition, scroll, repeat, toMiddle);
            transform.position = new Vector3(at.x, at.y, transform.position.z);
        }

        /// <summary>
        /// Where a layer anchored at `layerAnchor` (for a camera at `viewAnchor`) sits for a
        /// camera at `cameraPosition`: moved by the share of the camera's way it does not
        /// scroll, brought by whole repeats until its middle (`toMiddle` from where it
        /// stands) is as near the camera as it gets, and snapped to whole art pixels.
        /// </summary>
        public static Vector2 PositionFor(
            Vector2 layerAnchor,
            Vector2 viewAnchor,
            Vector2 cameraPosition,
            Vector2 scroll,
            Vector2 repeat,
            Vector2 toMiddle
        )
        {
            var moved = cameraPosition - viewAnchor;
            var at = layerAnchor + new Vector2(moved.x * (1f - scroll.x), moved.y * (1f - scroll.y));
            if (repeat.x > 0f)
            {
                at.x += Mathf.Round((cameraPosition.x - at.x - toMiddle.x) / repeat.x) * repeat.x;
            }
            if (repeat.y > 0f)
            {
                at.y += Mathf.Round((cameraPosition.y - at.y - toMiddle.y) / repeat.y) * repeat.y;
            }
            return new Vector2(
                Mathf.Round(at.x * PixelsPerUnit) / PixelsPerUnit,
                Mathf.Round(at.y * PixelsPerUnit) / PixelsPerUnit
            );
        }

        private void LateUpdate()
        {
            var view = Camera.main;
            if (view != null)
            {
                Place(view.transform.position);
            }
        }
    }
}
