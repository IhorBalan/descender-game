using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.World.Paradise
{
    /// <summary>
    /// The slow breath of a lantern's flame: its light (the Light2D its WorldLight makes) and its
    /// glass swell and ebb a little, each lantern on its own, so the lit walls of the garden are
    /// alive rather than painted. Smooth noise, never a flicker; nothing changes while the light
    /// is off (far from the camera).
    /// </summary>
    public sealed class LanternFlame : MonoBehaviour
    {
        [Tooltip("How far the light swells and ebbs about its own strength (0.08 = 8%).")]
        [SerializeField, Range(0f, 0.5f)]
        private float amount = 0.08f;

        [Tooltip("How quickly it breathes, in cycles a second (about).")]
        [SerializeField, Min(0.01f)]
        private float speed = 0.45f;

        [Tooltip("The lantern's glowing glass, which breathes with it.")]
        [SerializeField]
        private SpriteRenderer glass;

        private Light2D light2D;
        private float energy;
        private float glassAlpha;
        private float seed;

        /// <summary>Sets up the flame; for editor tools.</summary>
        public void Configure(SpriteRenderer glowingGlass, float breath, float cycles)
        {
            glass = glowingGlass;
            amount = breath;
            speed = cycles;
        }

        private void Start()
        {
            // WorldLight makes the Light2D in its Awake, so it is there by now.
            light2D = GetComponent<Light2D>();
            energy = light2D != null ? light2D.intensity : 0f;
            glassAlpha = glass != null ? glass.color.a : 0f;
            // Each lantern breathes on its own: seeded by where it hangs.
            var p = transform.position;
            seed = Mathf.Repeat(p.x * 0.618f + p.y * 0.37f, 97f);
        }

        /// <summary>The flame's strength at a time, about 1 (1 - amount to 1 + amount).</summary>
        public float Level(float time) => 1f + amount * (2f * Mathf.PerlinNoise(seed, time * speed) - 1f);

        private void Update()
        {
            if (light2D == null || !light2D.enabled)
            {
                return;
            }
            var level = Level(Time.time);
            light2D.intensity = energy * level;
            if (glass != null)
            {
                var c = glass.color;
                c.a = Mathf.Clamp01(glassAlpha * (0.5f + 0.5f * level));
                glass.color = c;
            }
        }
    }
}
