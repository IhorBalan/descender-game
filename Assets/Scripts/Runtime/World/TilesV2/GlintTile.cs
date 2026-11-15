using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// The glint of a Tiles V2 ore: an overlay on the unlit Glow tilemap that is nothing
    /// most of the time and now and then a dot and a cross of light on one of the ore
    /// tile's nuggets, so ore twinkles in the dark. It picks the frames of the variant the
    /// ore tile under it shows (the same hash of the cell) and starts its loop at a moment
    /// of the cell's own, so a vein does not blink as one. Built from the generator's
    /// manifest by Descender > Tiles V2 > Build tiles.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Tiles V2/Glint Tile")]
    public sealed class GlintTile : TileBase
    {
        /// <summary>The frames a variant has: a dot and a cross at each of two nuggets.</summary>
        public const int FramesPerVariant = 4;

        private const int StartSalt = 977;

        [Tooltip("The empty sprite shown between glints.")]
        [SerializeField]
        private Sprite none;

        [Tooltip("The glint frames, four for each variant of the ore tile, variant by variant.")]
        [SerializeField]
        private Sprite[] frames = new Sprite[0];

        [Tooltip("What the tile shows frame by frame: -1 nothing, else one of the variant's four frames.")]
        [SerializeField]
        private int[] loop = new int[0];

        [SerializeField, Min(0.1f)]
        private float framesPerSecond = 10f;

        // The loop of each variant from each starting frame, made once.
        private Sprite[][] turns;

        public int Variants => frames.Length / FramesPerVariant;
        public int LoopLength => loop.Length;

        /// <summary>Sets the glint up (the builder's one entry point).</summary>
        public void Configure(Sprite empty, Sprite[] variantFrames, int[] sequence, float fps)
        {
            none = empty;
            frames = variantFrames;
            loop = sequence;
            framesPerSecond = Mathf.Max(fps, 0.1f);
            turns = null;
        }

        /// <summary>The frame of the loop a cell starts at.</summary>
        public int StartOf(Vector3Int position) =>
            loop.Length == 0 ? 0 : TilesV2Painter.Hash(position.x, position.y, StartSalt) % loop.Length;

        /// <summary>The sprites a cell cycles through, from its own start.</summary>
        public Sprite[] SequenceAt(Vector3Int position)
        {
            if (loop.Length == 0 || Variants == 0)
            {
                return System.Array.Empty<Sprite>();
            }
            if (turns == null || turns.Length != Variants * loop.Length)
            {
                turns = new Sprite[Variants * loop.Length][];
            }
            var variant = TerrainTile.Variant(position, Variants);
            var start = StartOf(position);
            var index = variant * loop.Length + start;
            if (turns[index] == null)
            {
                var sequence = new Sprite[loop.Length];
                for (var i = 0; i < sequence.Length; i++)
                {
                    var frame = loop[(start + i) % loop.Length];
                    sequence[i] = frame < 0 ? none : frames[variant * FramesPerVariant + frame];
                }
                turns[index] = sequence;
            }
            return turns[index];
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            var sequence = SequenceAt(position);
            tileData.sprite = sequence.Length > 0 ? sequence[0] : none;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockAll;
            tileData.colliderType = Tile.ColliderType.None;
        }

        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data)
        {
            var sequence = SequenceAt(position);
            if (sequence.Length < 2)
            {
                return false;
            }
            data.animatedSprites = sequence;
            data.animationSpeed = framesPerSecond;
            data.animationStartTime = 0f;
            return true;
        }

        private void OnValidate()
        {
            turns = null;
        }
    }
}
