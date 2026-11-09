using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Descender.World.Paradise
{
    /// <summary>
    /// A Paradise zone's look, blended by where the camera is (docs/design/paradise.md,
    /// section 5): its parallax backdrop, its post-processing Volume and its ambient light
    /// fade in over <see cref="blend"/> units either side of the zone's edges, so crossing
    /// from the dawn of the Hollow into the teal of the Orchard is one smooth change. Two
    /// neighbours' weights always add up to 1 across a border. A zone stays whole under the
    /// next one fading in over it: its sky, so the screen never dips to the clear colour, and
    /// its grade, so the crossing never dips to the ungraded picture. Sits on the zone scene's
    /// root.
    /// </summary>
    public sealed class ZoneLook : MonoBehaviour
    {
        private static readonly List<ZoneLook> Active = new List<ZoneLook>();

        [SerializeField]
        private int zone;

        [SerializeField]
        private float xMin;

        [SerializeField]
        private float xMax = 400f;

        [Tooltip("Units either side of an edge over which this zone fades in or out.")]
        [SerializeField, Min(0.1f)]
        private float blend = 16f;

        [Tooltip("The global light's colour while in this zone (lit sprites are multiplied by it).")]
        [SerializeField]
        private Color ambient = Color.white;

        [SerializeField]
        private Volume volume;

        [Tooltip("The sky layer: kept opaque while a later zone fades in over it.")]
        [SerializeField]
        private Transform sky;

        [Tooltip("The other backdrop layers, faded with the zone.")]
        [SerializeField]
        private Transform[] layers = new Transform[0];

        private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        private float refreshLeft;

        public int Zone => zone;
        public Color Ambient => ambient;

        public void Configure(
            int number,
            float left,
            float right,
            Color ambientColor,
            Volume zoneVolume,
            Transform skyLayer,
            Transform[] otherLayers
        )
        {
            zone = number;
            xMin = left;
            xMax = right;
            ambient = ambientColor;
            volume = zoneVolume;
            sky = skyLayer;
            layers = otherLayers;
        }

        /// <summary>This zone's share of the look with the camera at x: 1 inside, a ramp across each edge.</summary>
        public static float Weight(float x, float left, float right, float blendUnits)
        {
            var enter = Mathf.Clamp01((x - (left - blendUnits)) / (2f * blendUnits));
            var leave = Mathf.Clamp01(((right + blendUnits) - x) / (2f * blendUnits));
            return Mathf.Min(enter, leave);
        }

        /// <summary>
        /// A zone's share while the next zone fades in over it: whole until the next one has
        /// covered it, then gone. URP lays Volumes over each other in order of priority, each
        /// lerped over the result so far, so two at half weight would let a quarter of the
        /// ungraded picture through.
        /// </summary>
        public static float WeightUnderNext(float x, float right, float blendUnits, float weight) =>
            x > right - blendUnits && weight > 0f ? 1f : weight;

        /// <summary>The order zones' Volumes are laid in: each over the one before it.</summary>
        public static float VolumePriority(int number) => 1f + number;

        public float WeightAt(float x) => Weight(x, xMin, xMax, blend);

        private void OnEnable()
        {
            Active.Add(this);
            if (volume != null)
            {
                volume.priority = VolumePriority(zone);
            }
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void LateUpdate()
        {
            var camera = CameraRig.GameCamera.Main;
            if (camera == null)
            {
                return;
            }
            var x = camera.transform.position.x;
            var weight = WeightAt(x);
            if (volume != null)
            {
                volume.weight = NextZoneIsGraded() ? WeightUnderNext(x, xMax, blend, weight) : weight;
            }
            ApplyBackdrop(x, weight);
            // One zone mixes the ambient for all, so it is set once a frame.
            if (Active.Count > 0 && Active[0] == this)
            {
                MixAmbient(x);
            }
        }

        /// <summary>With nothing to lay over it, a zone's grade fades out by itself.</summary>
        private bool NextZoneIsGraded()
        {
            foreach (var look in Active)
            {
                if (look.zone == zone + 1 && look.volume != null && look.volume.sharedProfile != null)
                {
                    return true;
                }
            }
            return false;
        }

        private void ApplyBackdrop(float x, float weight)
        {
            refreshLeft -= Time.deltaTime;
            if (refreshLeft <= 0f || renderers.Count == 0)
            {
                // Backdrop layers make their copies as the view needs them.
                renderers.Clear();
                foreach (var layer in layers)
                {
                    if (layer != null)
                    {
                        renderers.AddRange(layer.GetComponentsInChildren<SpriteRenderer>(true));
                    }
                }
                refreshLeft = 0.5f;
            }
            foreach (var r in renderers)
            {
                SetAlpha(r, weight);
            }
            if (sky != null)
            {
                var skyAlpha = WeightUnderNext(x, xMax, blend, weight);
                foreach (var r in sky.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    SetAlpha(r, skyAlpha);
                }
            }
        }

        private static void SetAlpha(SpriteRenderer r, float alpha)
        {
            var c = r.color;
            if (!Mathf.Approximately(c.a, alpha))
            {
                c.a = alpha;
                r.color = c;
            }
            var visible = alpha > 0.001f;
            if (r.enabled != visible && r.GetComponent<BackdropLayer>() == null)
            {
                r.enabled = visible;
            }
        }

        private static void MixAmbient(float x)
        {
            var streamer = WorldStreamer.Instance;
            if (streamer == null || streamer.GlobalLight == null || streamer.AmbientFading)
            {
                return;
            }
            var sum = 0f;
            var mixed = Color.black;
            foreach (var look in Active)
            {
                var w = look.WeightAt(x);
                sum += w;
                mixed += look.ambient * w;
            }
            if (sum > 0.001f)
            {
                // With a sun overhead (SunAndClouds) the global light is only the sky's fill.
                streamer.GlobalLight.color = mixed / sum * SunAndClouds.SkyShare;
            }
        }
    }
}
