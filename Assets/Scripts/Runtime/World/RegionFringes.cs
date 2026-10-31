using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// The pieces of art along the exposed edges of a region's solid terrain: tufts
    /// on the tops, growth under the undersides, ivy down the sides (port of
    /// scripts/edge_decor.gd and scripts/world/region_fringes.gd). Which piece a cell
    /// gets, if any, is fixed by its position with the same hash Godot used, so the
    /// regions look as they did. Built as sprites once the region's tiles exist.
    /// </summary>
    public sealed class RegionFringes : MonoBehaviour
    {
        public const int Order = -90;

        [Tooltip("Pieces standing on the top edges, anchored by their bottom centre.")]
        [SerializeField]
        private Sprite[] top = new Sprite[0];

        [Tooltip("Pieces hanging from the undersides, anchored by their top centre.")]
        [SerializeField]
        private Sprite[] bottom = new Sprite[0];

        [Tooltip("Pieces on the left sides, anchored by their right centre.")]
        [SerializeField]
        private Sprite[] left = new Sprite[0];

        [Tooltip("Pieces on the right sides, anchored by their left centre.")]
        [SerializeField]
        private Sprite[] right = new Sprite[0];

        [SerializeField, Range(0f, 1f)]
        private float topChance = 1f;

        [SerializeField, Range(0f, 1f)]
        private float bottomChance = 1f;

        [SerializeField, Range(0f, 1f)]
        private float leftChance = 1f;

        [SerializeField, Range(0f, 1f)]
        private float rightChance = 1f;

        [SerializeField]
        private Material material;

        [SerializeField]
        private RegionTiles tiles;

        private Transform built;

        public int Count { get; private set; }

        private void Start()
        {
            if (tiles == null)
            {
                tiles = GetComponentInParent<RegionTiles>();
            }
            if (tiles == null)
            {
                return;
            }
            if (tiles.IsBuilt)
            {
                Build();
            }
            else
            {
                tiles.Built += Build;
            }
        }

        private void OnDestroy()
        {
            if (tiles != null)
            {
                tiles.Built -= Build;
            }
        }

        /// <summary>The Godot hash that fixes a cell's piece: 64-bit arithmetic masked to 31 bits.</summary>
        public static long Hash(int x, int y, int salt)
        {
            var h = (x * 374761393L + y * 668265263L + salt * 2246822519L) & 0x7fffffffL;
            h = ((h ^ (h >> 13)) * 1274126177L) & 0x7fffffffL;
            return h ^ (h >> 16);
        }

        public void Build()
        {
            if (built != null)
            {
                Destroy(built.gameObject);
            }
            built = new GameObject("Fringes").transform;
            built.SetParent(tiles.transform, false);
            Count = 0;
            var topRow = tiles.GetComponentInParent<RegionRoot>()?.TopRow ?? 0;
            for (var y = 0; y < tiles.Rows; y++)
            {
                for (var x = 0; x < tiles.Width; x++)
                {
                    if (!tiles.IsSolid(x, y))
                    {
                        continue;
                    }
                    // The hash runs on world rows, as the Godot grid was the whole world.
                    var worldY = y + topRow;
                    Place(top, topChance, x, y, worldY, 't', 0, -1);
                    Place(bottom, bottomChance, x, y, worldY, 'b', 0, 1);
                    Place(left, leftChance, x, y, worldY, 'l', -1, 0);
                    Place(right, rightChance, x, y, worldY, 'r', 1, 0);
                }
            }
        }

        private void Place(Sprite[] options, float chance, int x, int y, int worldY, char side, int dx, int dy)
        {
            if (options.Length == 0)
            {
                return;
            }
            var nx = x + dx;
            var ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= tiles.Width || ny >= tiles.Rows || tiles.IsSolid(nx, ny))
            {
                return;
            }
            var roll = Hash(x, worldY, side);
            if (roll % 1000 >= (long)(chance * 1000f))
            {
                return;
            }
            var sprite = options[(int)((roll / 1000) % options.Length)];
            if (sprite == null)
            {
                return;
            }
            var size = sprite.bounds.size;
            // Where the piece's top-left corner goes, in units, from the cell's corner.
            var cellLeft = (float)x;
            var cellTop = -(float)y;
            Vector2 topLeft;
            switch (side)
            {
                case 't':
                    topLeft = new Vector2(cellLeft + 0.5f - size.x / 2f, cellTop + size.y);
                    break;
                case 'b':
                    topLeft = new Vector2(cellLeft + 0.5f - size.x / 2f, cellTop - 1f);
                    break;
                case 'l':
                    topLeft = new Vector2(cellLeft - size.x, cellTop - 0.5f + size.y / 2f);
                    break;
                default:
                    topLeft = new Vector2(cellLeft + 1f, cellTop - 0.5f + size.y / 2f);
                    break;
            }
            var piece = new GameObject($"{side}{x},{y}");
            piece.transform.SetParent(built, false);
            var bounds = sprite.bounds;
            piece.transform.localPosition = new Vector3(topLeft.x - bounds.min.x, topLeft.y - bounds.max.y, 0f);
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = Order;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            Count++;
        }
    }
}
