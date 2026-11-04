using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// A waterfall: a sprite stretched from where the water leaves its lip down to where it
    /// lands (a pool or the cloud sea), drawn with Descender/Waterfall. Sets how often the
    /// water texture repeats down the fall from the fall's height, so a tall fall and a short
    /// one have streaks of the same size, and staggers each fall's flow so neighbours
    /// differ. Water comes from somewhere and goes somewhere (docs/design/paradise.md, 5).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Waterfall : MonoBehaviour
    {
        private static readonly int Tiles = Shader.PropertyToID("_Tiles");
        private static readonly int Speed = Shader.PropertyToID("_Speed");

        [Tooltip("World units of fall per repeat of the water texture.")]
        [SerializeField, Min(0.5f)]
        private float unitsPerRepeat = 4f;

        [SerializeField, Min(0.05f)]
        private float speed = 1.2f;

        public void Configure(float repeatUnits, float fallSpeed)
        {
            unitsPerRepeat = repeatUnits;
            speed = fallSpeed;
        }

        private void Start()
        {
            var renderer = GetComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat(Tiles, Mathf.Max(1f, renderer.bounds.size.y / unitsPerRepeat));
            // A slight per-fall difference in speed, so no two falls move in step.
            block.SetFloat(Speed, speed * (0.9f + Mathf.Repeat(transform.position.x * 0.137f, 0.2f)));
            renderer.SetPropertyBlock(block);
        }
    }
}
