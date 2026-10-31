using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.World
{
    /// <summary>
    /// The root of a region scene: names it, holds its look and applies the look
    /// to the global light and the sky when the scene loads. One per region scene,
    /// found by WorldStreamer after an additive load.
    /// </summary>
    public sealed class RegionRoot : MonoBehaviour
    {
        [SerializeField]
        private string id = "";

        [SerializeField]
        private RegionLook look;

        [SerializeField]
        private Light2D globalLight;

        [SerializeField]
        private SpriteRenderer sky;

        public string Id => id;
        public RegionLook Look => look;

        private void Start()
        {
            Apply();
        }

        public void Apply()
        {
            if (look == null)
            {
                return;
            }
            if (globalLight != null)
            {
                globalLight.color = look.ambient;
                globalLight.intensity = look.light;
            }
            if (sky != null)
            {
                sky.enabled = !look.cave;
                var block = new MaterialPropertyBlock();
                sky.GetPropertyBlock(block);
                block.SetColor(SkyTopId, look.skyTop);
                block.SetColor(SkyBottomId, look.skyBottom);
                sky.SetPropertyBlock(block);
            }
        }

        private static readonly int SkyTopId = Shader.PropertyToID("_Top");
        private static readonly int SkyBottomId = Shader.PropertyToID("_Bottom");

        /// <summary>Every door in this region, for the streamer and the link test.</summary>
        public WorldDoor[] Doors => GetComponentsInChildren<WorldDoor>(true);

        public WorldDoor FindDoor(string doorId)
        {
            foreach (var door in Doors)
            {
                if (door.Id == doorId)
                {
                    return door;
                }
            }
            return null;
        }
    }
}
