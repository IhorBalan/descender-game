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

        [Tooltip(
            "How much of the sky a canopy of leaves closes over this zone (0 open, 1 closed): the sun comes through only in flecks (SunAndClouds) and the sky's fill is dimmer."
        )]
        [SerializeField, Range(0f, 1f)]
        private float canopy;

        [Tooltip("Where the canopy runs (x from, to), and over how many units either end it opens out.")]
        [SerializeField]
        private Vector3 canopySpan;

        [Tooltip("The canopy's own layers (the roof of leaves), faded with it rather than with the zone.")]
        [SerializeField]
        private Transform[] canopyLayers = new Transform[0];

        private readonly List<SpriteRenderer> canopyRenderers = new List<SpriteRenderer>();
        private readonly List<BackdropLayer> canopyBackdrops = new List<BackdropLayer>();

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

        /// <summary>How much of the sky the canopy closes where the camera is now, mixed across zone borders.</summary>
        public static float Canopy { get; private set; }

        /// <summary>How much of the sky's fill a closed canopy keeps out (the sun's share goes to its flecks).</summary>
        public const float CanopyShade = 0.32f;

        /// <summary>How far the roof of leaves rises out of the view where the canopy has opened right out.</summary>
        public const float CanopyLiftUnits = 6f;

        /// <summary>How far the roof is raised with the canopy at `roof` of its cover: 0 closed, all the way open.</summary>
        public static float CanopyLift(float roof)
        {
            var open = 1f - Mathf.Clamp01(roof);
            return CanopyLiftUnits * open * open * (3f - 2f * open);
        }

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

        /// <summary>
        /// Closes `cover` of the sky with a canopy from x `from` to `to`, opening out over `ramp`
        /// units either end; for editor tools.
        /// </summary>
        public void SetCanopy(float cover, float from, float to, float ramp, Transform[] layersOfIt = null)
        {
            canopy = cover;
            canopySpan = new Vector3(from, to, Mathf.Max(0.1f, ramp));
            canopyLayers = layersOfIt ?? new Transform[0];
        }

        /// <summary>How much of the sky this zone's canopy closes with the camera at x.</summary>
        public float CanopyAt(float x) =>
            canopy <= 0f ? 0f : canopy * Weight(x, canopySpan.x, canopySpan.y, canopySpan.z);

        /// <summary>The canopy's cover mixed from each zone's by its share of the look (weights by zone).</summary>
        public static float MixCanopy(IReadOnlyList<(float weight, float cover)> zones)
        {
            var sum = 0f;
            var mixed = 0f;
            foreach (var (weight, cover) in zones)
            {
                sum += weight;
                mixed += weight * cover;
            }
            return sum > 0.001f ? mixed / sum : 0f;
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
            if (Active.Count == 0)
            {
                Canopy = 0f;
            }
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
                canopyRenderers.Clear();
                canopyBackdrops.Clear();
                foreach (var layer in canopyLayers)
                {
                    if (layer != null)
                    {
                        canopyRenderers.AddRange(layer.GetComponentsInChildren<SpriteRenderer>(true));
                        if (layer.TryGetComponent<BackdropLayer>(out var backdrop))
                        {
                            canopyBackdrops.Add(backdrop);
                        }
                    }
                }
                refreshLeft = 0.5f;
            }
            foreach (var r in renderers)
            {
                SetAlpha(r, weight);
            }
            // Where the canopy ends the roof of leaves draws back up out of the view, as walking out
            // from under trees, rather than fading where it hangs (a ghost of leaves over the sky);
            // it fades only once it is nearly gone.
            var roof = canopy > 0f ? weight * CanopyAt(x) / canopy : 0f;
            var lift = CanopyLift(roof);
            foreach (var backdrop in canopyBackdrops)
            {
                backdrop.SetLift(lift);
            }
            var shown = Mathf.Clamp01(roof * 4f);
            foreach (var r in canopyRenderers)
            {
                SetAlpha(r, shown);
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
            var sum = 0f;
            var mixed = Color.black;
            Covers.Clear();
            foreach (var look in Active)
            {
                var w = look.WeightAt(x);
                sum += w;
                mixed += look.ambient * w;
                Covers.Add((w, look.CanopyAt(x)));
            }
            // The canopy is mixed even while the ambient fades in, so the sun's flecks never jump.
            Canopy = MixCanopy(Covers);
            var streamer = WorldStreamer.Instance;
            if (streamer == null || streamer.GlobalLight == null || streamer.AmbientFading)
            {
                return;
            }
            if (sum > 0.001f)
            {
                // With a sun overhead (SunAndClouds) the global light is only the sky's fill,
                // and under a canopy less of the sky reaches the ground.
                streamer.GlobalLight.color = mixed / sum * SunAndClouds.SkyShare * (1f - Canopy * CanopyShade);
            }
        }

        private static readonly List<(float weight, float cover)> Covers = new List<(float, float)>();
    }
}
