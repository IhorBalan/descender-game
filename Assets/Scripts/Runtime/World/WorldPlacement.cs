using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// Something that stands in the world with its feet at its position, as
    /// scripts/world/world_placement.gd placed it: the start, a checkpoint (save),
    /// a feather (ability), an NPC, a relic (item), a story trigger, an enemy or
    /// boss, an upgrade or a power-up. `type` says which fields matter. When the
    /// region loads it spawns the prefab the PlacementCatalog gives for its type and
    /// character, unless its `unless` flag is set or the IsGone hook (the kills since
    /// the last checkpoint) says so, and hands itself to the prefab's
    /// IPlacementReceiver. The id is "&lt;region&gt;/&lt;name&gt;".
    /// </summary>
    public sealed class WorldPlacement : MonoBehaviour
    {
        /// <summary>Asked with the placement id before spawning; true leaves the thing out. Set by the kills tracker.</summary>
        public static System.Func<string, bool> IsGone;

        [SerializeField]
        private string id = "";

        [Tooltip("start, save, ability, npc, item, trigger, enemy, upgrade, power.")]
        [SerializeField]
        private string type = "enemy";

        [SerializeField]
        private PlacementCatalog catalog;

        [Header("What it is")]
        [Tooltip("A feather's ability, by its Godot name (\"Wings (double jump)\").")]
        [SerializeField]
        private string ability = "";

        [Tooltip("The feather's ability as Save.Ability, or -1.")]
        [SerializeField]
        private int abilityIndex = -1;

        [Tooltip("For a save: its index among the world's checkpoints, top to bottom, left to right.")]
        [SerializeField]
        private int checkpointIndex = -1;

        [Tooltip("An NPC's name, as the dialogue box shows it.")]
        [SerializeField]
        private string placementName = "";

        [Tooltip("An NPC's or an enemy's body: a WalkerProfile, FlyerProfile or BossProfile id.")]
        [SerializeField]
        private string character = "";

        [SerializeField]
        private Color tint = Color.white;

        [SerializeField]
        private bool facesLeft;

        [Tooltip("The dialogue entries to try in order, and the lines to rotate through after.")]
        [SerializeField]
        private string[] dialogue = new string[0];

        [SerializeField]
        private string[] rotate = new string[0];

        [Tooltip("A story flag that, once set, leaves the thing out.")]
        [SerializeField]
        private string unless = "";

        [Tooltip("A Shops id for an NPC that trades.")]
        [SerializeField]
        private string shop = "";

        [Tooltip("A relic's item id.")]
        [SerializeField]
        private string item = "";

        [Header("Enemies")]
        [SerializeField]
        private string barks = "";

        [SerializeField]
        private bool passive;

        [Tooltip("chase, ambush, pounce, hurl, slam, lunge, dive.")]
        [SerializeField]
        private string behaviour = "";

        [Tooltip("A flag the enemy waits for before it appears.")]
        [SerializeField]
        private string requires = "";

        [Header("Pickups")]
        [Tooltip("heart, vial or iron.")]
        [SerializeField]
        private string upgrade = "";

        [Tooltip("halo, blade or zeal.")]
        [SerializeField]
        private string power = "";

        private GameObject spawned;

        public string Id => id;
        public string Type => type;
        public string Ability => ability;
        public int AbilityIndex => abilityIndex;
        public int CheckpointIndex => checkpointIndex;
        public string PlacementName => placementName;
        public string Character => character;
        public Color Tint => tint;
        public bool FacesLeft => facesLeft;
        public string[] Dialogue => dialogue;
        public string[] Rotate => rotate;
        public string Unless => unless;
        public string Shop => shop;
        public string Item => item;
        public string Barks => barks;
        public bool Passive => passive;
        public string Behaviour => behaviour;
        public string Requires => requires;
        public string Upgrade => upgrade;
        public string Power => power;
        public GameObject Spawned => spawned;

        private void Start()
        {
            Spawn();
        }

        /// <summary>Whether the placement should stand right now: not flagged away, not killed, its requirement met.</summary>
        public bool ShouldSpawn()
        {
            if (type == "start")
            {
                return false;
            }
            if (!string.IsNullOrEmpty(unless) && Story.StoryFlags.Has(unless))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(requires) && !Story.StoryFlags.Has(requires))
            {
                return false;
            }
            return IsGone == null || !IsGone(id);
        }

        public void Spawn()
        {
            if (spawned != null || !ShouldSpawn())
            {
                return;
            }
            var prefab = catalog != null ? catalog.Resolve(type, character) : null;
            if (prefab == null)
            {
                return;
            }
            spawned = Instantiate(prefab, transform.position, Quaternion.identity, transform);
            spawned.name = prefab.name;
            if (facesLeft)
            {
                var scale = spawned.transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                spawned.transform.localScale = scale;
            }
            var receiver = spawned.GetComponentInChildren<IPlacementReceiver>(true);
            receiver?.Configure(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor();
            var at = transform.position;
            Gizmos.DrawWireCube(at + new Vector3(0f, 0.45f, 0f), new Vector3(0.75f, 0.9f, 0f));
        }

        private Color GizmoColor()
        {
            switch (type)
            {
                case "start":
                    return Color.white;
                case "save":
                    return new Color(1f, 0.9f, 0.3f);
                case "ability":
                    return new Color(0.9f, 0.8f, 1f);
                case "npc":
                    return new Color(0.5f, 1f, 0.6f);
                case "item":
                    return new Color(1f, 0.85f, 0.4f);
                case "trigger":
                    return new Color(0.7f, 0.7f, 1f);
                case "upgrade":
                    return new Color(1f, 0.6f, 0.9f);
                case "power":
                    return new Color(0.6f, 0.9f, 1f);
                default:
                    return new Color(1f, 0.4f, 0.4f);
            }
        }
    }
}
