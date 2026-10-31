using Descender.Combat;
using Descender.Player;
using UnityEngine;

namespace Descender.Pickups
{
    /// <summary>
    /// Spikes. The hero loses health and is put back on the last safe ground. Polling
    /// while he is inside (rather than the enter alone) hurts a hero who is still on
    /// the spikes when his invulnerability ends. Port of scripts/hazard.gd.
    /// </summary>
    public sealed class Hazard : MonoBehaviour
    {
        [SerializeField]
        private int damage = 1;

        private readonly Collider2D[] hits = new Collider2D[2];
        private Collider2D area;
        private ContactFilter2D filter;
        private int inside;

        public int Damage
        {
            get => damage;
            set => damage = value;
        }

        /// <summary>Makes a hazard of <paramref name="sizePixels"/> whose bottom-left corner is <paramref name="at"/>.</summary>
        public static Hazard Spawn(Vector2 at, Vector2 sizePixels, int damage = 1)
        {
            var hazard = new GameObject("Hazard").AddComponent<Hazard>();
            hazard.transform.position = at;
            hazard.damage = damage;
            var box = (BoxCollider2D)hazard.area;
            box.size = sizePixels / PlayerTuning.PixelsPerUnit;
            box.offset = box.size / 2f;
            return hazard;
        }

        private void Awake()
        {
            gameObject.layer = Layers.Hazard;
            area = GetComponent<Collider2D>();
            if (area == null)
            {
                area = gameObject.AddComponent<BoxCollider2D>();
            }
            area.isTrigger = true;
            filter = Layers.Any(Layers.PlayerMask);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.attachedRigidbody != null && other.attachedRigidbody.GetComponent<PlayerController>() != null)
            {
                inside++;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.attachedRigidbody != null && other.attachedRigidbody.GetComponent<PlayerController>() != null)
            {
                inside = Mathf.Max(inside - 1, 0);
            }
        }

        private void FixedUpdate()
        {
            if (inside <= 0)
            {
                return;
            }
            var found = area.Overlap(filter, hits);
            for (var i = 0; i < found; i++)
            {
                var hero = Pickup.HeroFrom(hits[i]);
                if (hero != null)
                {
                    hero.TakeHazardDamage(damage);
                }
            }
        }
    }
}
