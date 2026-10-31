using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// One piece of a region's set: a lantern, a pillar, a floating ledge, placed
    /// in the region scene by its top-left corner. A prefab per prop under
    /// Assets/Prefabs/Props/&lt;region&gt;/, its Art child a lit sprite with the normal
    /// map as its _NormalMap, its Glow child (when it shines) additive. Flip a copy
    /// by setting the X scale to -1, as the Godot scenes did.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldProp : MonoBehaviour
    {
        public bool Flipped => transform.localScale.x < 0f;

        public void SetFlipped(bool flipped)
        {
            var scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (flipped ? -1f : 1f);
            transform.localScale = scale;
        }
    }
}
