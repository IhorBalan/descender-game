using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// The spray at a waterfall's foot, as two small particle systems built once when the
    /// zone loads (like RegionParticles): splash droplets thrown up and out of the pool,
    /// streaked along their flight and pulled back down, and soft mist puffs that swell,
    /// turn and drift up and downwind. A fall into the clouds has no droplets, only larger,
    /// slower puffs along the stretch where it thins away. Sits where the water lands; the
    /// sizes follow the fall's width. Placed by ParadiseGarden's Fall. Both systems pause
    /// off screen and allocate nothing per frame.
    /// </summary>
    public sealed class WaterfallSpray : MonoBehaviour
    {
        [SerializeField]
        private Material droplets;

        [SerializeField]
        private Material puffs;

        [Tooltip("The fall's width in units.")]
        [SerializeField, Min(0.3f)]
        private float width = 1.6f;

        [SerializeField]
        private bool intoClouds;

        [Tooltip("Into the clouds: the height in units of the stretch the puffs rise from.")]
        [SerializeField, Min(0.5f)]
        private float thinOver = 12f;

        [SerializeField]
        private int sortingOrder = -84;

        /// <summary>Sets everything a scene would serialize, for the editor tool that builds the zone.</summary>
        public void Configure(
            Material dropletMaterial,
            Material puffMaterial,
            float fallWidth,
            bool clouds,
            float cloudStretch,
            int order
        )
        {
            droplets = dropletMaterial;
            puffs = puffMaterial;
            width = fallWidth;
            intoClouds = clouds;
            thinOver = cloudStretch;
            sortingOrder = order;
        }

        private void Awake()
        {
            if (!intoClouds && droplets != null)
            {
                Droplets();
            }
            if (puffs != null)
            {
                Puffs();
            }
        }

        private void Droplets()
        {
            var system = Child("Droplets");
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
            main.startColor = new Color(0.95f, 0.98f, 1f, 0.8f);
            main.gravityModifier = 0.85f;
            main.maxParticles = 96;
            var emission = system.emission;
            emission.rateOverTime = 34f * width;
            // A thin strip across the fall's foot, throwing straight up; the sideways
            // velocity below fans them out both ways.
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.scale = new Vector3(width * 0.85f, 0.02f, 0.1f);
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-1.7f, 1.7f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            Fade(system, 0.08f, 0.6f, 0.75f);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 1.3f;
            renderer.sharedMaterial = droplets;
            renderer.sortingOrder = sortingOrder;
            system.Play();
        }

        private void Puffs()
        {
            var system = Child("Puffs");
            var main = system.main;
            var big = intoClouds ? 2.4f : 1f;
            main.startLifetime = intoClouds
                ? new ParticleSystem.MinMaxCurve(4f, 6.5f)
                : new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f * width * big, 1.1f * width * big);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 0.99f, 0.97f, 1f);
            main.maxParticles = intoClouds ? 40 : 28;
            var emission = system.emission;
            emission.rateOverTime = intoClouds ? 6f : 3.5f + 2f * width;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Rectangle;
            shape.scale = intoClouds
                ? new Vector3(width * 1.6f, thinOver * 0.7f, 1f)
                : new Vector3(width * 1.2f, 0.25f, 1f);
            // Up, a little downwind, at mist speed.
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.3f);
            velocity.y = intoClouds
                ? new ParticleSystem.MinMaxCurve(0.12f, 0.35f)
                : new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.7f));
            var spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            Fade(system, 0.25f, 0.55f, intoClouds ? 0.3f : 0.22f);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = puffs;
            renderer.sortingOrder = sortingOrder;
            system.Play();
        }

        /// <summary>A looping, prewarmed, world-space system on a child, stopped so it can be set up.</summary>
        private ParticleSystem Child(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 4f;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.Pause;
            return system;
        }

        /// <summary>Fades each particle in, holds, and fades out, at `peak` alpha.</summary>
        private static void Fade(ParticleSystem system, float fadeIn, float holdUntil, float peak)
        {
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peak, fadeIn),
                    new GradientAlphaKey(peak * 0.7f, holdUntil),
                    new GradientAlphaKey(0f, 1f),
                }
            );
            color.color = gradient;
        }
    }
}
