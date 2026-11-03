using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// A giant soft seed head that throws the hero up when he lands on it (Paradise,
    /// zone 2 on: docs/design/paradise.md). A small pod lifts about 6 tiles, a big one
    /// as high as a canopy. Triggers only on the way down, from above, so walking
    /// into its side does nothing. The pod squashes and springs back as it throws.
    /// Its collider is a trigger on its top; the art is the child named "Visual".
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class BouncePod : MonoBehaviour
    {
        [Tooltip("How high the hero is thrown, in tiles.")]
        [SerializeField, Min(1f)]
        private float heightTiles = 6f;

        [SerializeField]
        private Transform visual;

        /// <summary>
        /// Rising faster than this (units/s) the hero passes through: he is on his way up
        /// from a throw or a jump. On the ground the solver can leave a hair of upward
        /// speed, so the limit is not zero.
        /// </summary>
        public const float RisingSpeed = 1f;

        private float squash;
        private Vector3 restScale = Vector3.one;

        public float HeightTiles => heightTiles;

        public void Configure(float tiles)
        {
            heightTiles = tiles;
        }

        private void Awake()
        {
            gameObject.layer = LayerMask.NameToLayer("Trigger");
            GetComponent<BoxCollider2D>().isTrigger = true;
            if (visual == null)
            {
                visual = transform.Find("Visual");
            }
            if (visual != null)
            {
                restScale = visual.localScale;
            }
        }

        private void OnTriggerEnter2D(Collider2D other) => TryThrow(other);

        private void OnTriggerStay2D(Collider2D other) => TryThrow(other);

        private void TryThrow(Collider2D other)
        {
            if (other.attachedRigidbody == null
                || !other.attachedRigidbody.TryGetComponent<Player.PlayerController>(out var hero)
                || hero.Velocity.y > RisingSpeed)
            {
                return;
            }
            hero.Launch(heightTiles * Player.PlayerTuning.PixelsPerUnit);
            squash = 1f;
        }

        private void Update()
        {
            if (visual == null || squash <= 0f)
            {
                return;
            }
            squash = Mathf.Max(0f, squash - Time.deltaTime * 4f);
            // A squash that springs past its rest shape and settles.
            var s = Mathf.Sin(squash * Mathf.PI * 2.5f) * squash * 0.35f;
            visual.localScale = new Vector3(restScale.x * (1f + s), restScale.y * (1f - s), restScale.z);
        }
    }
}
