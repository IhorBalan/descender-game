using System;
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

        [Tooltip("No gravity: it stays at the height it is put.")]
        public bool flies;

        public ShapeKind shape = ShapeKind.Square;
        public Color color = new Color(0.85f, 0.3f, 0.3f);

        public FoeBody Copy() => (FoeBody)MemberwiseClone();
    }

    /// <summary>What a kind of foe is made of: a name, a body and behaviours. Each foe built from it gets its own copies.</summary>
    public sealed class FoeRecipe
    {
        public string name = "";
        public FoeBody body = new FoeBody();
        public FoeBehaviour[] behaviours = new FoeBehaviour[0];

        public FoeRecipe() { }

        public FoeRecipe(string called, FoeBody with, params FoeBehaviour[] acts)
        {
            name = called;
            body = with;
            behaviours = acts;
        }
    }
}
