using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// A waterfall: a sprite stretched from where the water leaves its lip down to where it
    /// lands (a pool or the cloud sea), drawn with Descender/Waterfall. Tells the shader the
    /// fall's size in units, so the water accelerates over real distance and a tall fall and
    /// a short one fall alike; gives each fall its own seed and a slightly different speed,
    /// so neighbours never move in step; and marks a fall into the clouds, which thins into
    /// mist over its last stretch instead of ending. Water comes from somewhere and goes
    /// somewhere (docs/design/paradise.md, 5). Set once on Start: the motion is all shader time.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Waterfall : MonoBehaviour
    {
        private static readonly int HeightId = Shader.PropertyToID("_Height");
        private static readonly int WidthId = Shader.PropertyToID("_Width");
        private static readonly int SpeedId = Shader.PropertyToID("_Speed");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int DissolveLengthId = Shader.PropertyToID("_DissolveLength");

        [Tooltip("Multiplies the material's lip speed: how fast this fall's water leaves its lip.")]
        [SerializeField, Min(0.05f)]
        private float speed = 1f;

        [Tooltip("Falls into the cloud sea: thins into mist over its last stretch instead of ending in foam.")]
        [SerializeField]
        private bool intoClouds;

        [Tooltip("Units at the foot over which a fall into the clouds thins away.")]
        [SerializeField, Min(0.5f)]
        private float dissolveLength = 12f;

        /// <summary>Sets everything a scene would serialize, for the editor tool that builds the zone.</summary>
        public void Configure(float fallSpeed, bool clouds, float thinOver)
        {
            speed = fallSpeed;
            intoClouds = clouds;
            dissolveLength = thinOver;
        }

        private void Start()
        {
            var renderer = GetComponent<SpriteRenderer>();
            var size = renderer.bounds.size;
            var x = transform.position.x;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat(HeightId, Mathf.Max(0.5f, size.y));
            block.SetFloat(WidthId, Mathf.Max(0.3f, size.x));
            // A slight per-fall difference in speed, so no two falls move in step.
            block.SetFloat(SpeedId, speed * (0.9f + Mathf.Repeat(x * 0.137f, 0.2f)));
            block.SetFloat(SeedId, Seed(x));
            block.SetFloat(DissolveId, intoClouds ? 1f : 0f);
            block.SetFloat(DissolveLengthId, Mathf.Min(dissolveLength, size.y));
            renderer.SetPropertyBlock(block);
        }

        /// <summary>A stable per-place seed for the water shaders, from a world x.</summary>
        public static float Seed(float x) => Mathf.Repeat(x * 7.31f, 97f);
    }
}
