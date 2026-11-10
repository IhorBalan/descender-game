using UnityEngine;

namespace Descender.Combat
{
    /// <summary>What an attack is, so what it hits can tell them apart.</summary>
    public enum StrikeKind
    {
        Sword,
        Slam,
        Bolt,
    }

    /// <summary>
    /// One hit as the attacker sends it: what it is, how much it hurts, where it came
    /// from and how hard it pushes.
    /// </summary>
    public readonly struct Strike
    {
        public readonly StrikeKind Kind;
        public readonly int Damage;

        /// <summary>Where the attacker is, in the world.</summary>
        public readonly Vector2 From;

        /// <summary>The push the attack asks for, in level pixels a second; zero for none.</summary>
        public readonly Vector2 Push;

        public Strike(StrikeKind kind, int damage, Vector2 from, Vector2 push = default)
        {
            Kind = kind;
            Damage = damage;
            From = from;
            Push = push;
        }
    }

    /// <summary>
    /// Something that wants the whole <see cref="Strike"/>, not only its damage. It is an
    /// <see cref="IHittable"/> too, so every attacker finds it the same way; one that
    /// does not care what hit it implements <see cref="IHittable"/> alone.
    /// </summary>
    public interface IStruck : IHittable
    {
        /// <summary>Takes the strike; returns false when it did nothing.</summary>
        bool Struck(Strike strike);
    }

    /// <summary>How every attack finds what it hit and hands the strike over.</summary>
    public static class Strikes
    {
        /// <summary>What a collider belongs to that can be hit, on its body or a parent; null for nothing.</summary>
        public static IHittable Find(Collider2D collider)
        {
            if (collider == null)
            {
                return null;
            }
            var body = collider.attachedRigidbody;
            if (body != null && body.TryGetComponent<IHittable>(out var onBody))
            {
                return onBody;
            }
            return collider.GetComponentInParent<IHittable>();
        }

        /// <summary>Hands a strike to its target: whole where the target takes it whole, as damage otherwise.</summary>
        public static bool Deliver(IHittable target, Strike strike)
        {
            if (target == null)
            {
                return false;
            }
            return target is IStruck struck ? struck.Struck(strike) : target.Hit(strike.Damage, strike.From);
        }
    }
}
