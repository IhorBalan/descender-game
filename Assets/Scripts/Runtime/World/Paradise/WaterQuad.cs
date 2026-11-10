using System.Collections.Generic;
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
    ///
    /// A pool (the reflection shader) also lists its surface while enabled, so the hero's
    /// feet can tell when they come down in water (<see cref="PoolAt"/>).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class WaterQuad : MonoBehaviour
    {
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private const string PoolShader = "Descender/Water Reflection";

        /// <summary>A pool's surface: from left to right at height y, in world units.</summary>
        public struct Surface
        {
            public float left;
            public float right;
            public float y;
        }

        private static readonly List<WaterQuad> Pools = new List<WaterQuad>();
        private Surface surface;

        [Tooltip("Shader-specific shape in units, e.g. the fall's half width and the ground's height in the quad.")]
        [SerializeField]
        private Vector4 shape = new Vector4(0.8f, 0.6f, 4f, 0f);

        /// <summary>Sets everything a scene would serialize, for the editor tool that builds the zone.</summary>
        public void Configure(Vector4 effectShape) => shape = effectShape;

        /// <summary>How many pools are enabled now.</summary>
        public static int PoolCount => Pools.Count;

        /// <summary>The surface of the i-th enabled pool.</summary>
        public static Surface PoolSurface(int i) => Pools[i].surface;

        /// <summary>Whether a foot at <paramref name="feet"/> stands in a pool, within <paramref name="margin"/> of its surface.</summary>
        public static bool PoolAt(Vector2 feet, float margin = 0.25f)
        {
            for (var i = 0; i < Pools.Count; i++)
            {
                var s = Pools[i].surface;
                if (feet.x >= s.left && feet.x <= s.right && Mathf.Abs(feet.y - s.y) <= margin)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The surface a pool quad of this shape covers: its reach either side of the centre, at its top edge.</summary>
        public static Surface SurfaceOf(Bounds bounds, Vector4 poolShape) =>
            new Surface
            {
                left = bounds.center.x - Mathf.Max(poolShape.x, 0.2f),
                right = bounds.center.x + Mathf.Max(poolShape.y, 0.2f),
                y = bounds.max.y,
            };

        private void OnEnable()
        {
            var renderer = GetComponent<SpriteRenderer>();
            var material = renderer.sharedMaterial;
            if (material != null && material.shader != null && material.shader.name == PoolShader)
            {
                surface = SurfaceOf(renderer.bounds, shape);
                Pools.Add(this);
            }
        }

        private void OnDisable() => Pools.Remove(this);

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
