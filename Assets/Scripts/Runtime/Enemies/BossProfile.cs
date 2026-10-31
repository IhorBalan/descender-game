using Descender.Economy;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The numbers of one boss. One asset per boss under Assets/Data/Bosses; the asset
    /// name is the id the placements name (see <see cref="BossIds"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Boss Profile", fileName = "boss")]
    public sealed class BossProfile : ScriptableObject
    {
        [Tooltip("Shown on the health bar.")]
        public string displayName = "";

        [Tooltip("The character whose strips it wears, and the name of their walking loop.")]
        public string character = "angel_guard";
        public string walkAnim = "walk";

        [Tooltip("How much bigger than the strips, and the sprite's colour.")]
        public float scale = 1.5f;
        public Color tint = Color.white;
        public bool facesLeft;

        [Tooltip("Collision size, in level pixels, at the scale, with the origin at the feet.")]
        public Vector2 body = new Vector2(32, 80);
        public int health = 24;
        public float speed = 34f;

        [Tooltip("Hearts for touching it.")]
        public int contactDamage = 1;

        [Tooltip("The third-phase move of the last bosses (rampage, court, mirror), or empty for two phases.")]
        public string third = "";

        [Header("Swing")]
        public float attackRange = 60f;
        public Vector2 reach = new Vector2(52, 60);
        public float windup = 0.55f;
        public float strike = 0.2f;
        public float recover = 0.6f;
        public float cooldown = 1f;
        public int attackDamage = 2;

        [Header("Charge")]
        public float chargeSpeed = 260f;
        public float chargeWindup = 0.45f;
        public float chargeCooldown = 2f;

        [Header("Drops")]
        [Tooltip("Heal motes dropped on death.")]
        public int motes = 2;
        public LootTable loot = new LootTable();
    }
}
