using Descender.Economy;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>The numbers of one flying enemy body; see <see cref="WalkerProfile"/>.</summary>
    [CreateAssetMenu(menuName = "Descender/Flyer Profile", fileName = "flyer")]
    public sealed class FlyerProfile : ScriptableObject
    {
        [Tooltip("Hits to kill.")]
        public int health = 2;
        [Tooltip("Speed about its post.")]
        public float hover = 25f;
        [Tooltip("Speed after the hero.")]
        public float chase = 55f;
        [Tooltip("How close the hero has to come.")]
        public float aggro = 110f;

        [Header("Drops")]
        [Range(0f, 1f)] public float dropChance = 0.5f;
        public LootTable loot = new LootTable();
        public Color puff = new Color(1.0f, 0.95f, 0.8f);
    }
}
