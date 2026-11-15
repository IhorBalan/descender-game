using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// A Tiles V2 liquid (water, lava): it fills whole cells, shows its surface frames in
    /// the top cell of a body (the liquid stands a few pixels under the cell's top, with a
    /// ripple) and its body frames under that, both animated. It also says what it does to
    /// the tile light: the share of light a tile of it passes on and the light it gives.
    /// Built from the generator's manifest by Descender > Tiles V2 > Build tiles.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Tiles V2/Liquid Tile")]
    public sealed class LiquidTile : TileBase
    {
        [Tooltip("The liquid's key in the Tiles V2 manifest (water, lava).")]
        [SerializeField]
        private string liquidKey = "";

        [Tooltip("The frames of the top cell of a body of the liquid.")]
        [SerializeField]
        private Sprite[] surface = new Sprite[0];

        [Tooltip("The frames of every cell under the top one.")]
        [SerializeField]
        private Sprite[] body = new Sprite[0];

        [SerializeField, Min(0.1f)]
        private float framesPerSecond = 6f;

        [Tooltip("The share of its light a tile of the liquid passes on, per channel.")]
        [SerializeField]
        private Color keeps = Color.white;

        [Tooltip("The light a tile of the liquid gives, in the tile light's units.")]
        [SerializeField]
        private Color emits = Color.black;

        public string LiquidKey => liquidKey;
        public Color Keeps => keeps;
        public Color Emits => emits;
        public int FrameCount => surface.Length;

        /// <summary>Sets the liquid up (the builder's one entry point).</summary>
        public void Configure(string key, Sprite[] top, Sprite[] under, float fps, Color share, Color light)
        {
            liquidKey = key ?? "";
            surface = top;
            body = under;
            framesPerSecond = Mathf.Max(fps, 0.1f);
            keeps = new Color(share.r, share.g, share.b, 1f);
            emits = new Color(light.r, light.g, light.b, 1f);
        }

        /// <summary>Whether the cell is the top of its body: no tile of this liquid over it.</summary>
        public bool IsSurface(Vector3Int position, ITilemap tilemap) =>
            tilemap.GetTile(position + Vector3Int.up) != this;

        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            tilemap.RefreshTile(position);
            // The cell under this one is the surface or not by what stands here.
            tilemap.RefreshTile(position + Vector3Int.down);
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            var frames = IsSurface(position, tilemap) ? surface : body;
            tileData.sprite = frames.Length > 0 ? frames[0] : null;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockAll;
            tileData.colliderType = Tile.ColliderType.None;
        }

        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data)
        {
            var frames = IsSurface(position, tilemap) ? surface : body;
            if (frames.Length < 2)
            {
                return false;
            }
            data.animatedSprites = frames;
            data.animationSpeed = framesPerSecond;
            data.animationStartTime = 0f;
            return true;
        }
    }
}
