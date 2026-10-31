using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// The shape of the world: its regions as horizontal bands of the one grid, top
    /// to bottom (a band's top row and height in tiles, its scene is
    /// "Region_&lt;key&gt;"), where a new game starts, and every checkpoint in the order
    /// the saves number them (top to bottom, left to right over the whole world).
    /// One asset at Assets/Data/World.asset, written by
    /// tools/unity/import_godot_regions.py from scenes/world/world.tscn. Positions are
    /// world units: X right, Y down from the world's top edge is negative.
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/World Atlas", fileName = "World")]
    public sealed class WorldAtlas : ScriptableObject
    {
        public const string ScenePrefix = "Region_";

        [System.Serializable]
        public struct Band
        {
            public string key;
            public string displayName;
            public string subtitle;
            public int topRow;
            public int rows;

            public string SceneName => ScenePrefix + key;
            public float Top => -topRow;
            public float Bottom => -(topRow + rows);
        }

        [System.Serializable]
        public struct Checkpoint
        {
            public string region;
            public Vector2 position;
        }

        [Tooltip("The regions, top to bottom.")]
        public Band[] regions = new Band[0];

        [Tooltip("How many tiles wide the world is.")]
        public int width = 480;

        [Tooltip("The region a new game starts in and where the hero stands.")]
        public string startRegion = "paradise";
        public Vector2 start;

        [Tooltip("The checkpoints in save order.")]
        public Checkpoint[] checkpoints = new Checkpoint[0];

        [Tooltip("The abilities in the map tool's order, matching Save.Ability.")]
        public string[] abilities = new string[0];

        public static string SceneName(string key) => ScenePrefix + key;

        /// <summary>The index of the band holding world Y, clamped to the first and last.</summary>
        public int IndexAt(float worldY)
        {
            if (regions.Length == 0)
            {
                return -1;
            }
            var row = Mathf.FloorToInt(-worldY);
            for (var i = 0; i < regions.Length; i++)
            {
                if (row < regions[i].topRow + regions[i].rows)
                {
                    return i;
                }
            }
            return regions.Length - 1;
        }

        public int IndexOf(string key)
        {
            for (var i = 0; i < regions.Length; i++)
            {
                if (regions[i].key == key)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The checkpoint at a save's index, or the start when the index is out of range.</summary>
        public Checkpoint CheckpointAt(int index)
        {
            if (index >= 0 && index < checkpoints.Length)
            {
                return checkpoints[index];
            }
            return new Checkpoint { region = startRegion, position = start };
        }

        public int AbilityIndex(string abilityName) => System.Array.IndexOf(abilities, abilityName);
    }
}
