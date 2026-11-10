using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.CameraRig
{
    /// <summary>
    /// Follows the hero, clamps to the current room's bounds and shakes in whole pixels so
    /// the pixel-perfect camera never shimmers. Port of scripts/game_camera.gd. Runs in
    /// LateUpdate, after the interpolated body has moved and before everything placed by
    /// the view (backdrops, framing, the sun), so they never trail it by a frame. The aim
    /// follows <see cref="CameraFollow"/>: a look-ahead that travels to the side he faces
    /// as he runs, and a height held through his jumps and eased to each new floor, each
    /// axis smoothed on its own. While the hero stands in a <see cref="CameraZone"/> the
    /// framing, zoom, look-ahead and held axes blend to the zone's
    /// <see cref="CameraSettings"/>, and back to the room's when he leaves.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class GameCamera : MonoBehaviour
    {
        public const float PixelsPerUnit = 16f;

        /// <summary>Before the scripts that place themselves by the view, all at the default order.</summary>
        public const int ExecutionOrder = -20;

        /// <summary>The game camera in Main.unity, or null before it exists.</summary>
        public static GameCamera Main { get; private set; }

        [SerializeField]
        private Transform target;

        [SerializeField, Min(0f)]
        private float lookAheadPixels = 24f;

        [Tooltip("Seconds the view takes to catch up across, about.")]
        [SerializeField, Min(0.01f)]
        private float smoothTime = 0.12f;

        [Tooltip("Seconds the view takes to catch up up and down: a landing eases to the new floor.")]
        [SerializeField, Min(0.01f)]
        private float verticalSmoothTime = 0.22f;

        [Tooltip("The share of the hero's running speed the look-ahead travels at when he turns; standing, it stays.")]
        [SerializeField, Min(0f)]
        private float lookAheadCatchUp = 0.6f;

        [Tooltip(
            "How far he may rise above the floor he left, in pixels, before the view follows: a hop never moves it."
        )]
        [SerializeField, Min(0f)]
        private float riseBeforeFollowPixels = 48f;

        [Tooltip("How far he may drop below the floor he left, in pixels, before the view follows him down.")]
        [SerializeField, Min(0f)]
        private float dropBeforeFollowPixels = 8f;

        [Tooltip("Where the view centres relative to the target, in units: up shows more above the hero.")]
        [SerializeField]
        private Vector2 framing;

        [Tooltip("Snap the view to the 16 px art grid (pixel art); painted art scrolls smoothly without it.")]
        [SerializeField]
        private bool snapToPixelGrid = true;

        private Camera cam;
        private Vector2 velocity;
        private float shakePixels;
        private float shakeTime;
        private float shakeLeft;

        /// <summary>No room: the camera goes wherever the target does.</summary>
        public static readonly Rect Unbounded = new Rect(
            float.NegativeInfinity,
            float.NegativeInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity
        );

        private Rect bounds = Unbounded;
        private World.WorldRoom room;
        private Vector2 focus;
        private Player.PlayerController hero;
        private float lookAhead;
        private float baseline;
        private float lastTargetX;

        // Zones: the settings in force, where the blend started and how far along it is.
        private CameraSettings current;
        private CameraSettings blendFrom;
        private CameraZone zone;
        private float blendTime;
        private float blendElapsed;
        private float baseSize;
        private float appliedZoom = 1f;
        private PixelPerfectCamera pixelPerfect;
        private Vector2Int baseResolution;

        /// <summary>The settings in force this frame: the room's, a zone's, or a blend of the two.</summary>
        public CameraSettings Settings => current;

        /// <summary>The zone the camera obeys, or null in the open room.</summary>
        public CameraZone Zone => zone;

        /// <summary>The settings outside any zone, from this component's own fields.</summary>
        public CameraSettings RoomSettings
        {
            get
            {
                var room = new CameraSettings();
                room.framing = framing;
                room.zoom = 1f;
                room.lookAheadPixels = lookAheadPixels;
                return room;
            }
        }

        /// <summary>The rect the camera is clamped to, in units: a room, a station, or a boss's arena sealed.</summary>
        public Rect Bounds
        {
            get => bounds;
            set
            {
                bounds = value;
                room = null;
            }
        }

        /// <summary>
        /// Clamps the camera to a room as the hero enters it: a joined room together with the
        /// loaded rooms joined to it, so it never stops at their shared edges, kept up to date
        /// as they stream in and out; any other room alone.
        /// </summary>
        public void FrameRoom(World.WorldRoom entered)
        {
            room = entered != null && entered.Joined ? entered : null;
            bounds = entered != null ? entered.CameraBounds() : Unbounded;
        }

        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                hero = target != null ? target.GetComponent<Player.PlayerController>() : null;
                if (target != null)
                {
                    focus = target.position;
                    Aim();
                }
            }
        }

        /// <summary>Half the view in units, from the orthographic size.</summary>
        public Vector2 HalfExtents => new Vector2(cam.orthographicSize * cam.aspect, cam.orthographicSize);

        /// <summary>How much more than the room's view is shown now: 1 in the room, more where a zone pulls back.</summary>
        public float Zoom => appliedZoom;

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
            baseSize = cam.orthographicSize;
            if (TryGetComponent(out pixelPerfect))
            {
                baseResolution = new Vector2Int(pixelPerfect.refResolutionX, pixelPerfect.refResolutionY);
            }
            current = RoomSettings;
            if (target != null)
            {
                hero = target.GetComponent<Player.PlayerController>();
                focus = target.position;
                Aim();
            }
        }

        /// <summary>The aim set straight on the target: the look-ahead to his side, the height at his.</summary>
        private void Aim()
        {
            lookAhead = Facing() * current.lookAheadPixels / PixelsPerUnit;
            baseline = target.position.y;
            lastTargetX = target.position.x;
        }

        private int Facing()
        {
            if (hero != null)
            {
                return hero.Facing;
            }
            return target.localScale.x < 0f ? -1 : 1;
        }

        /// <summary>Moves the settings toward the hero's zone, or the room's; a change of zone starts a blend.</summary>
        private void StepSettings(float dt)
        {
            var now = CameraZone.Current;
            if (now != zone)
            {
                // Out of a zone the blend back takes that zone's time.
                var timed = now != null ? now : zone;
                blendTime = timed != null ? timed.BlendTime : 0f;
                blendFrom = current;
                blendElapsed = 0f;
                zone = now;
            }
            var wanted = zone != null ? zone.Settings(RoomSettings) : RoomSettings;
            blendElapsed += dt;
            current = wanted;
            if (blendElapsed < blendTime)
            {
                current = CameraSettings.Blend(blendFrom, wanted, blendElapsed / blendTime);
            }
            ApplyZoom(current.zoom);
        }

        /// <summary>Takes the settings of where the hero stands at once, with no blend.</summary>
        private void SettleSettings()
        {
            zone = CameraZone.Current;
            current = zone != null ? zone.Settings(RoomSettings) : RoomSettings;
            blendFrom = current;
            blendTime = 0f;
            blendElapsed = 0f;
            ApplyZoom(current.zoom);
        }

        /// <summary>
        /// Shows more or less of the level. Under the Pixel Perfect Camera the view is its
        /// reference resolution, kept to whole even pixels; without one it is the
        /// orthographic size.
        /// </summary>
        private void ApplyZoom(float zoom)
        {
            if (Mathf.Approximately(zoom, appliedZoom))
            {
                return;
            }
            appliedZoom = zoom;
            if (pixelPerfect != null)
            {
                pixelPerfect.refResolutionX = Mathf.Max(2, Mathf.RoundToInt(baseResolution.x * zoom / 2f) * 2);
                pixelPerfect.refResolutionY = Mathf.Max(2, Mathf.RoundToInt(baseResolution.y * zoom / 2f) * 2);
            }
            cam.orthographicSize = baseSize * zoom;
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
            SettleSettings();
            Aim();
            focus = current.Hold(new Vector2(target.position.x + lookAhead, baseline) + current.framing);
            velocity = Vector2.zero;
            Place(Vector2.zero);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }
            var dt = Time.deltaTime;
            StepSettings(dt);
            if (room != null)
            {
                bounds = room.CameraBounds();
            }
            var at = (Vector2)target.position;
            var speedX =
                hero != null ? hero.Velocity.x
                : dt > 0f ? (at.x - lastTargetX) / dt
                : 0f;
            lastTargetX = at.x;
            lookAhead = CameraFollow.LookAhead(
                lookAhead,
                Facing(),
                current.lookAheadPixels / PixelsPerUnit,
                speedX,
                lookAheadCatchUp,
                dt
            );
            baseline = CameraFollow.Baseline(
                baseline,
                at.y,
                hero == null || hero.IsGrounded,
                riseBeforeFollowPixels / PixelsPerUnit,
                dropBeforeFollowPixels / PixelsPerUnit
            );
            var wanted = current.Hold(new Vector2(at.x + lookAhead, baseline) + current.framing);
            focus.x = Mathf.SmoothDamp(focus.x, wanted.x, ref velocity.x, smoothTime, float.PositiveInfinity, dt);
            focus.y = Mathf.SmoothDamp(
                focus.y,
                wanted.y,
                ref velocity.y,
                verticalSmoothTime,
                float.PositiveInfinity,
                dt
            );

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
            if (snapToPixelGrid)
            {
                position.x = Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit;
                position.y = Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit;
            }
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }
    }
}
