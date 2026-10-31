using UnityEngine;

namespace Descender.CameraRig
{
    /// <summary>
    /// Follows the hero with a look-ahead in the facing direction, clamps to the
    /// current room's bounds and shakes in whole pixels so the pixel-perfect
    /// camera never shimmers. Port of scripts/game_camera.gd. Runs in LateUpdate,
    /// after the interpolated body has moved.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class GameCamera : MonoBehaviour
    {
        public const float PixelsPerUnit = 16f;

        /// <summary>The game camera in Main.unity, or null before it exists.</summary>
        public static GameCamera Main { get; private set; }

        [SerializeField]
        private Transform target;

        [SerializeField, Min(0f)]
        private float lookAheadPixels = 24f;

        [SerializeField, Min(0.01f)]
        private float smoothTime = 0.12f;

        [SerializeField, Min(0f)]
        private float verticalDeadZonePixels = 20f;

        private Camera cam;
        private Vector2 velocity;
        private float shakePixels;
        private float shakeTime;
        private float shakeLeft;
        private Rect bounds = new Rect(
            float.NegativeInfinity,
            float.NegativeInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity
        );
        private Vector2 focus;

        /// <summary>The room the camera is clamped to, in units. Set by WorldRoom when the hero enters.</summary>
        public Rect Bounds
        {
            get => bounds;
            set => bounds = value;
        }

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                if (target != null)
                {
                    focus = target.position;
                }
            }
        }

        /// <summary>Half the view in units, from the orthographic size.</summary>
        public Vector2 HalfExtents => new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);

        private void OnEnable()
        {
            Main = this;
        }

        private void OnDisable()
        {
            if (Main == this)
            {
                Main = null;
            }
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (target != null)
            {
                focus = target.position;
            }
        }

        /// <summary>Shakes for a time; the strength is scaled by the shake setting (0..1).</summary>
        public void Shake(float pixels, float seconds)
        {
            var scale = Save.SettingsData.Current.shake;
            if (scale <= 0f || pixels <= 0f)
            {
                return;
            }
            shakePixels = Mathf.Max(shakePixels, pixels * scale);
            shakeTime = Mathf.Max(shakeTime, seconds);
            shakeLeft = shakeTime;
        }

        /// <summary>Puts the camera on the target at once, for respawns and doors.</summary>
        public void Snap()
        {
            if (target == null)
            {
                return;
            }
            focus = target.position;
            velocity = Vector2.zero;
            Place(Vector2.zero);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }
            var wanted = (Vector2)target.position;
            var facing = target.localScale.x < 0f ? -1f : 1f;
            wanted.x += facing * lookAheadPixels / PixelsPerUnit;
            var deadZone = verticalDeadZonePixels / PixelsPerUnit;
            if (Mathf.Abs(wanted.y - focus.y) < deadZone)
            {
                wanted.y = focus.y;
            }
            focus = Vector2.SmoothDamp(focus, wanted, ref velocity, smoothTime, float.PositiveInfinity, Time.deltaTime);

            var offset = Vector2.zero;
            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.deltaTime;
                var strength = shakePixels * Mathf.Clamp01(shakeLeft / Mathf.Max(shakeTime, 0.0001f));
                offset =
                    new Vector2(
                        Mathf.Round(Random.Range(-strength, strength)),
                        Mathf.Round(Random.Range(-strength, strength))
                    ) / PixelsPerUnit;
                if (shakeLeft <= 0f)
                {
                    shakePixels = 0f;
                }
            }
            Place(offset);
        }

        private void Place(Vector2 offset)
        {
            var half = HalfExtents;
            var position = focus;
            if (bounds.width > half.x * 2f)
            {
                position.x = Mathf.Clamp(position.x, bounds.xMin + half.x, bounds.xMax - half.x);
            }
            else
            {
                position.x = bounds.center.x;
            }
            if (bounds.height > half.y * 2f)
            {
                position.y = Mathf.Clamp(position.y, bounds.yMin + half.y, bounds.yMax - half.y);
            }
            else
            {
                position.y = bounds.center.y;
            }
            position += offset;
            position.x = Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit;
            position.y = Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }
}
