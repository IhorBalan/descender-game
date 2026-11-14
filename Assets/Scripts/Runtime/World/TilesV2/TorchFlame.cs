using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// A Tiles V2 wall torch: cycles its flame frames and wavers its glow a little, each
    /// torch out of step with the others. Its colour and intensity seed the tile light
    /// (TileLightMap), which carries it through the caves; the glow is a small flicker on
    /// top, set up in the editor (so a scene renders lit without entering play) and
    /// switched off when the torch is far from the camera, checked a few times a second.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TorchFlame : MonoBehaviour
    {
        [SerializeField]
        private Sprite[] frames = System.Array.Empty<Sprite>();

        [SerializeField]
        private Light2D glow;

        [SerializeField, Min(0.02f)]
        private float frameSeconds = 0.12f;

        [SerializeField, Min(0f)]
        private float intensity = 0.35f;

        [SerializeField]
        private Color colour = new Color(1f, 0.72f, 0.42f);

        [SerializeField, Min(0f)]
        private float tileIntensity = 1.6f;

        [SerializeField, Min(1f)]
        private float activeDistance = 30f;

        private const float CheckEvery = 0.25f;

        private SpriteRenderer flame;
        private float offset;
        private float nextCheck;

        public Light2D Glow => glow;
        public int FrameCount => frames.Length;

        /// <summary>The light the torch gives its tile, for the tile light to spread.</summary>
        public Color Emission => colour * tileIntensity;

        public void Configure(
            Sprite[] flameFrames,
            Light2D light,
            float glowIntensity,
            Color lightColour,
            float lightIntensity
        )
        {
            frames = flameFrames;
            glow = light;
            intensity = glowIntensity;
            colour = lightColour;
            tileIntensity = lightIntensity;
        }

        /// <summary>The light's share of its intensity at time t: within 8% of 1, smooth, never periodic to the eye.</summary>
        public static float Waver(float t, float offset)
        {
            var a = Mathf.Sin((t + offset) * 7.3f);
            var b = Mathf.Sin((t + offset * 1.7f) * 12.9f);
            return 1f + 0.05f * a + 0.03f * b;
        }

        private void Awake()
        {
            flame = GetComponent<SpriteRenderer>();
            var p = transform.position;
            offset = Mathf.Repeat(p.x * 0.37f + p.y * 0.61f, 1f) * 10f;
            nextCheck = Mathf.Repeat(offset, CheckEvery);
        }

        private void Update()
        {
            var t = Time.time;
            if (frames.Length > 0)
            {
                flame.sprite = frames[(int)((t + offset) / frameSeconds) % frames.Length];
            }
            if (glow == null)
            {
                return;
            }
            if (t >= nextCheck)
            {
                nextCheck = t + CheckEvery;
                var view = Camera.main;
                glow.enabled =
                    view == null
                    || ((Vector2)(view.transform.position - transform.position)).sqrMagnitude
                        < activeDistance * activeDistance;
            }
            glow.intensity = intensity * Waver(t, offset);
        }
    }
}
