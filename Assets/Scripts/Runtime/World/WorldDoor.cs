using System.Collections.Generic;
using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// A door of the world: the opening between two rooms, at the cell its position
    /// falls in (port of scripts/world/world_door.gd and door_lock.gd). A door
    /// sideways ("h") is an opening in a room's side wall; a door down ("v") is a gap
    /// in a ledge. A lock seals it with a wall of light until the hero has the
    /// ability, or has killed the boss of the region, it names; the seal's cells are
    /// found on the region's tiles the way the Godot builder found them. A secret
    /// door is a passage through the rock, hidden until a Smite breaks it.
    /// </summary>
    public sealed class WorldDoor : MonoBehaviour
    {
        public const int MaxSealHeight = 16;
        private const float CheckEvery = 0.5f;

        [SerializeField]
        private int id;

        [SerializeField]
        private int roomA;

        [SerializeField]
        private int roomB;

        [Tooltip("h for a door sideways, v for a door down.")]
        [SerializeField]
        private string dir = "h";

        [Tooltip("Empty for an open door, ability or boss for a sealed one.")]
        [SerializeField]
        private string lockKind = "";

        [Tooltip("For an ability lock: the Save.Ability the hero needs.")]
        [SerializeField]
        private int lockAbility;

        [Tooltip("For a boss lock: the key of the region whose boss must fall.")]
        [SerializeField]
        private string lockRegion = "";

        [SerializeField]
        private bool secret;

        [SerializeField]
        private Sprite sealSprite;

        [SerializeField]
        private Material sealMaterial;

        private GameObject seal;
        private float nextCheck;

        public int Id => id;
        public int RoomA => roomA;
        public int RoomB => roomB;
        public string Dir => dir;
        public string LockKind => lockKind;
        public int LockAbility => lockAbility;
        public string LockRegion => lockRegion;
        public bool Secret => secret;
        public bool IsLocked => seal != null;

        private void Start()
        {
            if (string.IsNullOrEmpty(lockKind) || secret)
            {
                return;
            }
            var tiles = GetComponentInParent<RegionRoot>()?.Tiles;
            if (tiles == null)
            {
                return;
            }
            if (tiles.IsBuilt)
            {
                Seal(tiles);
            }
            else
            {
                tiles.Built += () => Seal(tiles);
            }
        }

        private void Update()
        {
            if (seal == null || Time.time < nextCheck)
            {
                return;
            }
            nextCheck = Time.time + CheckEvery;
            if (Satisfied())
            {
                Open();
            }
        }

        /// <summary>Whether the hero may pass: he has the ability, or the region's boss is dead.</summary>
        public bool Satisfied()
        {
            switch (lockKind)
            {
                case "ability":
                    var hero = WorldStreamer.Instance?.Hero;
                    return hero != null && hero.Has((Save.Ability)lockAbility);
                case "boss":
                    return Story.StoryFlags.Has(BossFlag);
                default:
                    return true;
            }
        }

        /// <summary>The flag a boss lock waits for: the region's boss is dead.</summary>
        public string BossFlag => "boss:" + lockRegion;

        public void Open()
        {
            if (seal != null)
            {
                Destroy(seal);
                seal = null;
            }
        }

        /// <summary>The cells the seal covers, in Godot cells of the region, from the tiles.</summary>
        public List<Vector2Int> SealCells(RegionTiles tiles)
        {
            var cells = new List<Vector2Int>();
            var local = tiles.transform.InverseTransformPoint(transform.position);
            var x = Mathf.FloorToInt(local.x);
            var y = Mathf.FloorToInt(-local.y);
            if (dir == "v")
            {
                var row = y;
                while (row > 0 && RowIsAir(tiles, x - 2, row - 1, 4))
                {
                    row--;
                }
                for (var i = 0; i < 4; i++)
                {
                    cells.Add(new Vector2Int(x - 2 + i, row));
                }
            }
            else
            {
                var row = y;
                while (row >= 0 && tiles.KindAt(x, row) == TileKind.None)
                {
                    cells.Add(new Vector2Int(x, row));
                    row--;
                    if (cells.Count >= MaxSealHeight)
                    {
                        break;
                    }
                }
            }
            return cells;
        }

        private static bool RowIsAir(RegionTiles tiles, int x, int y, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (tiles.KindAt(x + i, y) != TileKind.None)
                {
                    return false;
                }
            }
            return true;
        }

        private void Seal(RegionTiles tiles)
        {
            if (seal != null || Satisfied())
            {
                return;
            }
            var cells = SealCells(tiles);
            if (cells.Count == 0)
            {
                return;
            }
            seal = new GameObject("Seal") { layer = LayerMask.NameToLayer("World") };
            seal.transform.SetParent(tiles.transform, false);
            var body = seal.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var tint = lockKind == "boss" ? new Color(1f, 0.7f, 0.6f) : new Color(1f, 0.95f, 0.75f);
            foreach (var cell in cells)
            {
                var box = seal.AddComponent<BoxCollider2D>();
                box.size = Vector2.one;
                box.offset = new Vector2(cell.x + 0.5f, -cell.y - 0.5f);
                if (sealSprite == null)
                {
                    continue;
                }
                var art = new GameObject($"Cell {cell.x},{cell.y}");
                art.transform.SetParent(seal.transform, false);
                art.transform.localPosition = new Vector3(cell.x + 0.5f, -cell.y - 0.5f, 0f);
                var renderer = art.AddComponent<SpriteRenderer>();
                renderer.sprite = sealSprite;
                renderer.color = tint;
                renderer.sortingOrder = 20;
                if (sealMaterial != null)
                {
                    renderer.sharedMaterial = sealMaterial;
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = secret ? new Color(0.8f, 0.5f, 1f, 0.9f)
                : string.IsNullOrEmpty(lockKind) ? new Color(0.4f, 0.9f, 1f, 0.8f)
                : new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawSphere(transform.position, 0.3f);
        }
    }
}
