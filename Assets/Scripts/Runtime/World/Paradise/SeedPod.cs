using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// A harmless seed pod that bursts into light when struck: the Waking Hollow
    /// teaches the attack on these before anything fights back (docs/design/paradise.md,
    /// zone 1). One hit; the pod is gone for the rest of the visit.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class SeedPod : MonoBehaviour, Combat.IHittable
    {
        public static readonly Color BurstColor = new Color(1f, 0.92f, 0.65f, 1f);

        private bool burst;

        public bool IsBurst => burst;

        private void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer("Enemy");
            GetComponent<Collider2D>().isTrigger = true;
        }

        public bool Hit(int damage, Vector2 from)
        {
            if (burst)
            {
                return false;
            }
            burst = true;
            Fx.Feedback.Puff(transform.position, BurstColor);
            Audio.Sfx.Play("pickup");
            gameObject.SetActive(false);
            return true;
        }
    }
}
