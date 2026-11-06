using Descender.Fx;
using UnityEngine;

namespace Descender.Combat
{
    /// <summary>
    /// A bolt of Holy Light: flies straight from the hero's hand, hurts the first
    /// enemy it meets and breaks on the world. A kinematic body with a trigger
    /// collider on the Trigger layer, moved in FixedUpdate; drawn as a short streak
    /// with a bright head made of Shapes. Port of scripts/light_bolt.gd.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class LightBolt : MonoBehaviour
    {
        private const float Ppu = Player.PlayerTuning.PixelsPerUnit;

        public const float Speed = 230f;
        public const float Lifetime = 0.55f;
        public const int Damage = 1;
        public static readonly Color Glow = new Color(1.0f, 0.95f, 0.7f);
        public static readonly Color Head = new Color(1.0f, 1.0f, 0.9f);
        public static readonly Vector2 Size = new Vector2(10f, 6f);

        private Rigidbody2D body;
        private BoxCollider2D box;
        private ContactFilter2D hitFilter;
        private readonly Collider2D[] hits = new Collider2D[4];
        private int direction = 1;
        private float time;
        private bool done;

        public int Direction => direction;

        /// <summary>Makes a bolt at a point going one way, from the prefab or, without one, from parts.</summary>
        public static LightBolt Spawn(LightBolt prefab, Vector2 at, int facing)
        {
            LightBolt bolt;
            if (prefab != null)
            {
                bolt = Instantiate(prefab, at, Quaternion.identity);
            }
            else
            {
                var go = new GameObject("LightBolt") { layer = Layers.Trigger };
                go.transform.position = at;
                bolt = go.AddComponent<LightBolt>();
                Shapes.Block(go.transform, "Tail", new Vector2(10f, 2f), new Vector2(-5f, 0f), Glow);
                Shapes.Block(go.transform, "Head", new Vector2(4f, 4f), Vector2.zero, Head);
            }
            bolt.direction = facing >= 0 ? 1 : -1;
            bolt.transform.localScale = new Vector3(bolt.direction, 1f, 1f);
            return bolt;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            box.isTrigger = true;
            box.size = Size / Ppu;
            hitFilter = Layers.Any(Layers.EnemyMask | Layers.WorldMask | Layers.LightBarrierMask);
        }

        private void FixedUpdate()
        {
            if (done)
            {
                return;
            }
            var dt = Time.fixedDeltaTime;
            time += dt;
            // The box is tested where the bolt will be once MovePosition lands, so a hit is seen the step it happens.
            var next = body.position + new Vector2(direction * Speed * dt / Ppu, 0f);
            body.MovePosition(next);
            var count = Physics2D.OverlapBox(next, box.size, 0f, hitFilter, hits);
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.isTrigger && hit.gameObject.layer != Layers.Enemy)
                {
                    continue;
                }
                var target =
                    hit.attachedRigidbody != null && hit.attachedRigidbody.TryGetComponent<IHittable>(out var onBody)
                        ? onBody
                        : hit.GetComponentInParent<IHittable>();
                target?.Hit(Damage, body.position - new Vector2(direction * 20f / Ppu, 0f));
                Feedback.Spark(body.position, Glow);
                Fade();
                return;
            }
            if (time >= Lifetime)
            {
                Fade();
            }
        }

        /// <summary>Gone at once: no second hit, no lingering streak.</summary>
        private void Fade()
        {
            done = true;
            Destroy(gameObject);
        }
    }
}
