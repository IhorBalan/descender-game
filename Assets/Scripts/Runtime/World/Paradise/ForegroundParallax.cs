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

        private Vector3 anchor;

        public void Configure(float speedUp, float speedUpY = 0f, Vector2 viewOffset = default)
        {
            extra = speedUp;
            extraY = speedUpY;
            offset = viewOffset;
        }

        private void Awake()
        {
            anchor = transform.position;
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
        }
    }
}
