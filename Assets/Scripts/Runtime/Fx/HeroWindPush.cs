using Descender.Player;
using Descender.World.Paradise;
using UnityEngine;

namespace Descender.Fx
{
    /// <summary>
    /// Tells the Paradise plant shaders where the hero has been, so plants part as he runs
    /// through them and spring back after him with a little overshoot (docs/design/paradise.md,
    /// "Light and effects"). Each frame it writes two globals read by
    /// Assets/Shaders/Paradise/ParadiseSway.hlsl: _HeroPos (xy = the hero's chest in world
    /// units, w = 1 while there is a hero) and _HeroTrail, his last <see cref="TrailLength"/>
    /// positions (xy, z = age in seconds, w = how hard he pushes: more while running, 0 with
    /// no hero). Each plant runs that trail through a damped spring, which is what makes it
    /// lag, overshoot and settle, with no state of its own. One set of globals for every
    /// material keeps the plants batched; no allocations per frame. Also makes sure the
    /// level's <see cref="GardenWind"/> exists, on the same object.
    /// </summary>
    public sealed class HeroWindPush : MonoBehaviour
    {
        /// <summary>Samples in the trail, the newest being where the hero is now. Matches PARADISE_HERO_TRAIL.</summary>
        public const int TrailLength = 12;

        /// <summary>Seconds between recorded samples: the trail spans about 0.85 s, as long as a plant remembers.</summary>
        public const float TrailStep = 0.075f;

        private static readonly int HeroPosId = Shader.PropertyToID("_HeroPos");
        private static readonly int HeroTrailId = Shader.PropertyToID("_HeroTrail");

        /// <summary>
        /// From the hero's origin (his feet) to the centre of the push, in world units,
        /// so plants are brushed by his body rather than only at ground level.
        /// </summary>
        [SerializeField]
        private Vector2 offset = new Vector2(0f, 0.75f);

        [Tooltip("Running speed (units per second) at which the hero pushes plants hardest.")]
        [SerializeField, Min(0.1f)]
        private float fullPushSpeed = 7f;

        private readonly Vector4[] trail = new Vector4[TrailLength];
        private readonly Vector3[] recorded = new Vector3[TrailLength]; // x, y, push
        private readonly float[] stamps = new float[TrailLength];
        private int head;
        private float lastStamp = float.NegativeInfinity;
        private Vector2 lastPosition;
        private float speed;
        private bool hadHero;

        private void Awake()
        {
            if (GetComponent<GardenWind>() == null)
            {
                gameObject.AddComponent<GardenWind>();
            }
            Clear();
        }

        // After the hero has moved this frame, before rendering.
        private void LateUpdate()
        {
            var now = Time.time;
            var hero = PlayerController.Current;
            var push = 0f;
            var position = lastPosition;
            if (hero != null)
            {
                var p = hero.transform.position;
                position = new Vector2(p.x + offset.x, p.y + offset.y);
                var dt = Time.deltaTime;
                if (hadHero && dt > 0f)
                {
                    // Smoothed over about a tenth of a second, so a single odd frame does not kick the plants.
                    var instant = (position - lastPosition).magnitude / dt;
                    speed = Mathf.Lerp(speed, instant, 1f - Mathf.Exp(-dt / 0.1f));
                }
                // Even standing still in a bush he holds it apart; running through it, he throws it.
                push = 0.55f + 0.45f * Mathf.Clamp01(speed / fullPushSpeed);
                Shader.SetGlobalVector(HeroPosId, new Vector4(position.x, position.y, p.z, 1f));
            }
            else
            {
                speed = 0f;
                Shader.SetGlobalVector(HeroPosId, Vector4.zero);
            }
            hadHero = hero != null;
            lastPosition = position;

            if (now - lastStamp >= TrailStep)
            {
                lastStamp = now;
                head = (head + 1) % TrailLength;
                recorded[head] = new Vector3(position.x, position.y, push);
                stamps[head] = now;
            }
            // Newest first: slot 0 is the hero now, then the recorded samples back in time.
            trail[0] = new Vector4(position.x, position.y, 0f, push);
            for (var i = 1; i < TrailLength; i++)
            {
                var k = (head - i + 1 + TrailLength) % TrailLength;
                var r = recorded[k];
                trail[i] = new Vector4(r.x, r.y, now - stamps[k], r.z);
            }
            Shader.SetGlobalVectorArray(HeroTrailId, trail);
        }

        private void OnDisable()
        {
            Shader.SetGlobalVector(HeroPosId, Vector4.zero);
            Clear();
            Shader.SetGlobalVectorArray(HeroTrailId, trail);
        }

        // Every sample far in the past and pushing nothing, so no plant leans toward a stale spot.
        private void Clear()
        {
            for (var i = 0; i < TrailLength; i++)
            {
                trail[i] = new Vector4(0f, 0f, 1000f, 0f);
                recorded[i] = Vector3.zero;
                stamps[i] = -1000f;
            }
            lastStamp = float.NegativeInfinity;
            hadHero = false;
        }
    }
}
