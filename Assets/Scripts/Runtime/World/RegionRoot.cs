using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// The root of a region scene (Assets/Scenes/Region_&lt;key&gt;.unity): names it, holds
    /// its look and its band of the world grid, and knows its tiles and backdrop.
    /// Its transform sits at the band's top-left corner in world units, so the
    /// region's children are in level coordinates from the region's own corner, as
    /// in the Godot scenes. Found by WorldStreamer after an additive load.
    /// </summary>
    public sealed class RegionRoot : MonoBehaviour
    {
        [SerializeField]
        private string key = "";

        [SerializeField]
        private string displayName = "";

        [SerializeField]
        private string subtitle = "";

        [SerializeField]
        private RegionLook look;

        [Tooltip("The band's first row of the world grid.")]
        [SerializeField]
        private int topRow;

        [Tooltip("How many tiles tall the band is.")]
        [SerializeField]
        private int rows = 34;

        [SerializeField]
        private RegionTiles tiles;

        [SerializeField]
        private Transform backdrop;

        public string Key => key;
        public string DisplayName => displayName;
        public string Subtitle => subtitle;
        public RegionLook Look => look;
        public int TopRow => topRow;
        public int Rows => rows;
        public RegionTiles Tiles => tiles;
        public Transform Backdrop => backdrop;

        /// <summary>The band in world units.</summary>
        public Rect Bounds => new Rect(0f, -(topRow + rows), tiles != null ? tiles.Width : 480, rows);

        /// <summary>The region's ambient tint, the Godot CanvasModulate colour, for the global light.</summary>
        public Color Ambient => look != null ? look.ambient : new Color(0.76f, 0.8f, 0.9f);

        /// <summary>Only the region the camera is in shows its backdrop, else the sets overlap.</summary>
        public void SetBackdropVisible(bool visible)
        {
            if (backdrop != null && backdrop.gameObject.activeSelf != visible)
            {
                backdrop.gameObject.SetActive(visible);
            }
        }

        public WorldRoom[] Rooms => GetComponentsInChildren<WorldRoom>(true);
        public WorldDoor[] Doors => GetComponentsInChildren<WorldDoor>(true);
        public WorldPlacement[] Placements => GetComponentsInChildren<WorldPlacement>(true);

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            var b = Bounds;
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
