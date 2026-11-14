using System;
using Descender.Economy;
using Descender.Fx;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>The numbers of a <see cref="Foe"/>'s body.</summary>
    [Serializable]
    public sealed class FoeBody
    {
        [Tooltip("Its size, in level pixels.")]
        public Vector2 size = new Vector2(18f, 24f);

        [Tooltip("Hits to kill, at a damage of one a hit.")]
        [Min(1)]
        public int health = 3;

        [Tooltip("How fast it walks, level pixels a second.")]
        [Min(0f)]
        public float speed = 32f;

        [Tooltip("Hearts its touch takes; 0 for a touch that does not hurt.")]
        [Min(0)]
        public int contactDamage = 1;

        [Tooltip("How much of a strike's push moves it: 1 for all of it, 0 for a body nothing moves.")]
        [Range(0f, 1f)]
        public float pushShare = 1f;

        [Tooltip("Seconds a hit holds it still.")]
        [Min(0f)]
        public float hurtTime = 0.2f;

        [Tooltip("A hit does not hold it, push it or cancel its attack.")]
        public bool armour;

        [Tooltip("No gravity: it stays at the height it is put.")]
        public bool flies;

        public ShapeKind shape = ShapeKind.Square;
        public Color color = new Color(0.85f, 0.3f, 0.3f);

        public FoeBody Copy() => (FoeBody)MemberwiseClone();
    }

    /// <summary>
    /// What a foe leaves when it dies, as the old profiles had it: what its
    /// <see cref="LootTable"/> rolls, a boss's sure motes and its story flag, and then
    /// either a power-up or a mote of healing.
    /// </summary>
    [Serializable]
    public sealed class FoeDrops
    {
        [Tooltip("How often it leaves a mote of healing.")]
        [Range(0f, 1f)]
        public float moteChance = 0.35f;

        [Tooltip("How often it leaves its power-up instead.")]
        [Range(0f, 1f)]
        public float powerChance = 0.1f;
        public PowerKind power = PowerKind.Blade;
        public LootTable loot = new LootTable();

        [Tooltip("Motes of healing it always leaves, as a boss does, before any by chance.")]
        [Min(0)]
        public int motes;

        [Tooltip("A story flag its death sets, as a boss's does; empty for none.")]
        public string flag = "";
    }

    /// <summary>
    /// What a kind of foe is made of: a name, a body, behaviours and, for one that
    /// leaves something, its drops. Each foe built from it gets its own copies of the
    /// body and the behaviours.
    /// </summary>
    public sealed class FoeRecipe
    {
        public string name = "";
        public FoeBody body = new FoeBody();
        public FoeBehaviour[] behaviours = new FoeBehaviour[0];

        /// <summary>What it leaves when it dies; null for nothing, as in the lab.</summary>
        public FoeDrops drops;

        public FoeRecipe() { }

        public FoeRecipe(string called, FoeBody with, params FoeBehaviour[] acts)
        {
            name = called;
            body = with;
            behaviours = acts;
        }
    }
}
