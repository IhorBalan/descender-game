using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// A procedural water effect drawn on a SpriteRenderer stretched to its area: a
    /// waterfall's foam (Descender/Water Foam), the pool it lands in (Descender/Water
    /// Reflection) or its mist (Descender/Water Mist).
    /// Tells the shader the area's size in units, a per-place seed and the effect's shape
    /// (what each shader reads from it is in the shader's header), once on Start; the motion
    /// is all shader time. Placed by ParadiseGarden's Fall.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WaterQuad : MonoBehaviour
    {
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        [Tooltip("Shader-specific shape in units, e.g. the fall's half width and the ground's height in the quad.")]
        [SerializeField]
        private Vector4 shape = new Vector4(0.8f, 0.6f, 4f, 0f);

        /// <summary>Sets everything a scene would serialize, for the editor tool that builds the zone.</summary>
        public void Configure(Vector4 effectShape) => shape = effectShape;

        private void Start()
        {
            var renderer = GetComponent<SpriteRenderer>();
            var size = renderer.bounds.size;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetVector(SizeId, new Vector4(size.x, size.y, 0f, 0f));
            block.SetVector(ShapeId, shape);
            block.SetFloat(SeedId, Waterfall.Seed(transform.position.x + transform.position.y * 0.31f));
            renderer.SetPropertyBlock(block);
        }
    }
}
