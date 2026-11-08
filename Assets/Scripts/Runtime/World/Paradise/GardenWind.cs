using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// Paradise's one wind field, in world space: a steady breeze plus gusts that travel
    /// across the level at <see cref="GustSpeed"/>, so a gust can be watched rippling
    /// through a row of plants from one side to the other before it calms. Each frame it
    /// writes the global <c>_GardenWind</c> (x = base breeze, y = gust strength at the
    /// camera, z = direction ±1, w = gust travel phase in world units) that
    /// Assets/Shaders/Paradise/ParadiseSway.hlsl samples to bend plants, ivy and cloth;
    /// petals and particles read the same field from C# through <see cref="At"/>. The
    /// field itself is a pure function of x and the phase, written twice (here and in
    /// ParadiseWind in the shader) and kept identical. One component for the whole
    /// level, added next to HeroWindPush on the camera; no allocations per frame.
    /// </summary>
    public sealed class GardenWind : MonoBehaviour
    {
        /// <summary>How fast gusts travel downwind, in world units per second. Matches PARADISE_GUST_SPEED.</summary>
        public const float GustSpeed = 4.5f;

        private static readonly int GardenWindId = Shader.PropertyToID("_GardenWind");

        private static float breezeNow;
        private static float directionNow;
        private static double phaseNow;

        [Tooltip("The steady breeze under the gusts (gusts add up to 1 on top).")]
        [SerializeField, Range(0f, 0.5f)]
        private float breeze = 0.35f;

        [Tooltip("Which way the wind blows: right when ticked, else left.")]
        [SerializeField]
        private bool blowsRight = true;

        /// <summary>
        /// The wind at <paramref name="worldX"/> now, signed: about 0 to 1.5 in strength,
        /// positive blowing right. Zero while there is no GardenWind in the scene.
        /// </summary>
        public static float At(float worldX) =>
            directionNow * Strength(worldX, breezeNow, directionNow, (float)phaseNow);

        /// <summary>
        /// The wind's strength (unsigned) at <paramref name="x"/> for a breeze, direction
        /// and gust phase: the breeze, breathing a little, plus the gusts. Mirrors
        /// ParadiseWind in ParadiseSway.hlsl term for term.
        /// </summary>
        public static float Strength(float x, float baseBreeze, float direction, float phase)
        {
            var breathing = 0.85f + 0.15f * Mathf.Sin(phase * 0.041f + x * 0.017f);
            return baseBreeze * breathing + Gust(x - direction * phase, phase);
        }

        /// <summary>
        /// The gusts, 0 to 1, as a pattern carried downwind: u is x in the frame moving with
        /// the gusts. Three incommensurate waves make gusts of uneven size and spacing (one
        /// passes a spot every few seconds, some barely form), and a slow envelope lets the
        /// pattern grow and fade as it travels, so it never repeats.
        /// </summary>
        public static float Gust(float u, float phase)
        {
            var n =
                0.5f * Mathf.Sin(u * 0.21f) + 0.3f * Mathf.Sin(u * 0.127f + 1.7f) + 0.2f * Mathf.Sin(u * 0.067f + 4.1f);
            var envelope = 0.7f + 0.3f * Mathf.Sin(phase * 0.023f + u * 0.011f + 0.6f);
            return Smooth(0.1f, 0.8f, n) * envelope;
        }

        private void OnEnable()
        {
            phaseNow = 0d;
            Publish();
        }

        private void Update()
        {
            phaseNow += GustSpeed * Time.deltaTime;
            Publish();
        }

        private void OnDisable()
        {
            breezeNow = 0f;
            directionNow = 0f;
            Shader.SetGlobalVector(GardenWindId, Vector4.zero);
        }

        private void Publish()
        {
            breezeNow = breeze;
            directionNow = blowsRight ? 1f : -1f;
            var phase = (float)phaseNow;
            var camera = Camera.main;
            var x = camera != null ? camera.transform.position.x : transform.position.x;
            var gust = Gust(x - directionNow * phase, phase);
            Shader.SetGlobalVector(GardenWindId, new Vector4(breezeNow, gust, directionNow, phase));
        }

        // HLSL's smoothstep, which Mathf.SmoothStep is not (it interpolates between its first two arguments).
        private static float Smooth(float edge0, float edge1, float x)
        {
            var t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
