using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// A single piece of scenery at its own depth (docs/design/paradise.md, section 5). It sits
    /// at its placed position when the camera is centred on it and shifts by <see cref="extra"/>
    /// of the camera's travel from there: positive for a foreground silhouette that sweeps past
    /// faster than the world (0.3 moves 1.3 times as fast on screen), negative for something far
    /// away that hardly moves (the golden tree at -0.996 drifts across the whole of Heaven 1).
    /// <see cref="extraY"/> does the same vertically, so a far landmark stays at the horizon.
    /// </summary>
    public sealed class ForegroundParallax : MonoBehaviour
    {
        [Tooltip("Horizontal: + passes faster than the world (foreground), - slower (far away).")]
        [SerializeField, Range(-1f, 2f)]
        private float extra = 0.3f;

        [Tooltip("Vertical, as above; 0 keeps it fixed in the world vertically.")]
        [SerializeField, Range(-1f, 2f)]
        private float extraY;

        [Tooltip("Added after the parallax, in world units: where a far landmark sits relative to the view.")]
        [SerializeField]
        private Vector2 offset;

        [Tooltip("The camera height the piece was placed for; it fades as the camera leaves it (0..0: never).")]
        [SerializeField]
        private float fadeAround;

        [Tooltip("Units from that height at which it starts to fade, and at which it is gone.")]
        [SerializeField]
        private Vector2 fadeOver;

        private Vector3 anchor;
        private SpriteRenderer sprite;
        private float restAlpha = 1f;

        public void Configure(float speedUp, float speedUpY = 0f, Vector2 viewOffset = default)
        {
            extra = speedUp;
            extraY = speedUpY;
            offset = viewOffset;
        }

        /// <summary>
        /// Fades the piece as the camera leaves the height it was placed for: framing set for a
        /// walk would show its cut edge to a route far above or below it.
        /// </summary>
        public void FadeAwayFrom(float cameraY, float near, float far)
        {
            fadeAround = cameraY;
            fadeOver = new Vector2(near, far);
        }

        /// <summary>The share of the piece left with the camera at a height, for a fade from near to far.</summary>
        public static float FadeAt(float cameraY, float placedFor, float near, float far)
        {
            if (far <= near)
            {
                return 1f;
            }
            var t = Mathf.Clamp01((Mathf.Abs(cameraY - placedFor) - near) / (far - near));
            return 1f - t * t * (3f - 2f * t);
        }

        private void Awake()
        {
            anchor = transform.position;
            if (TryGetComponent(out sprite))
            {
                restAlpha = sprite.color.a;
            }
        }

        private void LateUpdate()
        {
            var camera = CameraRig.GameCamera.Main;
            if (camera == null)
            {
                return;
            }
            var at = camera.transform.position;
            transform.position = new Vector3(
                anchor.x - (at.x - anchor.x) * extra + offset.x,
                anchor.y - (at.y - anchor.y) * extraY + offset.y,
                anchor.z
            );
            if (sprite != null && fadeOver.y > fadeOver.x)
            {
                var alpha = restAlpha * FadeAt(at.y, fadeAround, fadeOver.x, fadeOver.y);
                var c = sprite.color;
                if (!Mathf.Approximately(c.a, alpha))
                {
                    c.a = alpha;
                    sprite.color = c;
                }
                sprite.enabled = alpha > 0.001f;
            }
        }
    }
}
