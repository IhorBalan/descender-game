using Descender.Player;
using UnityEngine;

namespace Descender.Fx
{
    /// <summary>
    /// Tells the Paradise plant shaders where the hero is so plants bend away as he runs
    /// through them (docs/design/paradise.md, "Light and effects"). Each frame it writes
    /// the global _HeroPos read by Assets/Shaders/Paradise/ParadiseSway.hlsl: xy = the
    /// hero's chest in world units, w = 1 while there is a hero and 0 otherwise, so no
    /// plant leans toward a stale spot after he dies or the scene unloads. One global for
    /// every material keeps the plants batched; no allocations per frame.
    /// </summary>
    public sealed class HeroWindPush : MonoBehaviour
    {
        private static readonly int HeroPosId = Shader.PropertyToID("_HeroPos");

        /// <summary>
        /// From the hero's origin (his feet) to the centre of the push, in world units,
        /// so plants are brushed by his body rather than only at ground level.
        /// </summary>
        [SerializeField]
        private Vector2 offset = new Vector2(0f, 0.75f);

        // After the hero has moved this frame, before rendering.
        private void LateUpdate()
        {
            var hero = PlayerController.Current;
            if (hero == null)
            {
                Shader.SetGlobalVector(HeroPosId, Vector4.zero);
                return;
            }
            var p = hero.transform.position;
            Shader.SetGlobalVector(HeroPosId, new Vector4(p.x + offset.x, p.y + offset.y, p.z, 1f));
        }

        private void OnDisable()
        {
            Shader.SetGlobalVector(HeroPosId, Vector4.zero);
        }
    }
}
