using Descender.Economy;
using UnityEngine;

namespace Descender.Enemies
{
    public enum PowerKind
    {
        Halo = 0,
        Blade = 1,
        Zeal = 2,
    }

    /// <summary>
    /// The numbers of one ground enemy body: its size, pace, health, swing and drops.
    /// One asset per body under Assets/Data/Enemies, set on the body's prefab variant,
    /// so a body is tuned in the Inspector. Filled from the Godot .tres by the
    /// profile importer (Descender > Import > Profiles).
    /// </summary>
    [CreateAssetMenu(menuName = "Descender/Walker Profile", fileName = "walker")]
    public sealed class WalkerProfile : ScriptableObject
    {
        [Tooltip("Collision size, in level pixels, with the origin at the feet.")]
        public Vector2 body = new Vector2(18, 24);

        [Tooltip("Patrol speed, level pixels a second.")]
        public float speed = 32f;

        [Tooltip("Hits to kill.")]
        public int health = 3;

        [Tooltip("The looping animation it walks with (run or walk).")]
        public string walkAnim = "run";

        [Tooltip("Whether the strips face left.")]
        public bool facesLeft;

        [Tooltip("Hits never cancel its attack or push it back while it attacks (and push it half as far otherwise).")]
        public bool armour;

        [Header("Swing")]
        [Tooltip("How close (horizontally) the hero must be to trigger a swing.")]
        public float attackRange = 30f;

        [Tooltip("The strike hitbox size, placed in front of the body.")]
        public Vector2 reach = new Vector2(22, 20);
        public float windup = 0.35f;
        public float strike = 0.15f;
        public float recover = 0.45f;

        [Tooltip("Pause before the next swing can start.")]
        public float cooldown = 0.8f;

        [Tooltip("Hearts a strike takes.")]
        public int attackDamage = 1;

        [Header("Drops")]
        [Range(0f, 1f)]
        public float dropChance = 0.35f;
        public PowerKind power = PowerKind.Blade;

        [Range(0f, 1f)]
        public float powerChance = 0.1f;
        public LootTable loot = new LootTable();

        [Tooltip("The colour of the dust (or light) it dies in.")]
        public Color puff = new Color(0.55f, 0.15f, 0.15f);
    }
}
