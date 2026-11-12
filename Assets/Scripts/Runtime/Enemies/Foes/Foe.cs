using System;
using System.Collections.Generic;
using Descender.Combat;
using Descender.Fx;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// An enemy made of parts: a body (<see cref="FoeBody"/>: its size, health, pace and
    /// how it takes a hit) and a list of <see cref="FoeBehaviour"/>s asked in order each
    /// physics step, the first that acts taking the step. It stands on the Enemy layer,
    /// so the hero's attacks find it and he passes through it; touching it hurts him. A
    /// strike takes its damage, pushes it by the strike's push and stuns it for a
    /// moment; at no health it dies. Its origin is the middle of its body. Drawn as a
    /// <see cref="Shape"/> whose label is its name and health.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class Foe : MonoBehaviour, IStruck, IEnemy
    {
        private const float Ppu = EnemyPhysics.Ppu;

        /// <summary>Level pixels a second squared, and the fastest it falls.</summary>
        public const float Gravity = 700f;
        public const float MaxFallSpeed = 260f;

        /// <summary>What slows a pushed foe on the ground, level pixels a second squared.</summary>
        public const float Friction = 400f;

        /// <summary>The push of a strike that asks for none, away from where it came.</summary>
        public const float PlainPush = 60f;
        public static readonly Color HurtColor = new Color(1f, 0.85f, 0.8f);

        private readonly List<FoeBehaviour> behaviours = new List<FoeBehaviour>();
        private readonly Collider2D[] hits = new Collider2D[4];
        private FoeBody build = new FoeBody();
        private Rigidbody2D body;
        private BoxCollider2D box;
        private Shape shape;
        private string title = "";
        private int order;
        private int health;
        private float hurtLeft;
        private bool painted;

        public string PlacementId { get; set; } = "";
        public bool IsDead { get; private set; }
        public event Action<IEnemy> Died;

        public FoeBody Body => build;
        public int Health => health;
        public BoxCollider2D Box => box;

        /// <summary>A buffer for the behaviours' queries.</summary>
        public Collider2D[] Hits => hits;

        /// <summary>The way it faces: 1 right, -1 left.</summary>
        public int Facing { get; private set; } = 1;

        /// <summary>The speed along the ground a behaviour asks for this step, in level pixels a second.</summary>
        public float Move { get; set; }

        public bool IsGrounded { get; private set; }

        /// <summary>True while a hit holds it: it does not act.</summary>
        public bool IsHurt => hurtLeft > 0f;

        public IReadOnlyList<FoeBehaviour> Behaviours => behaviours;

        /// <summary>Its body and its behaviours, in the order they are asked. Each behaviour is this foe's own copy.</summary>
        public void Configure(FoeBody with, IEnumerable<FoeBehaviour> acts)
        {
            Find();
            build = with ?? new FoeBody();
            behaviours.Clear();
            if (acts != null)
            {
                foreach (var act in acts)
                {
                    if (act != null)
                    {
                        behaviours.Add(act.Copy());
                    }
                }
            }
            health = Mathf.Max(build.health, 1);
            box.size = build.size / Ppu;
            Write();
        }

        /// <summary>The shape that draws it, with the name shown above its health.</summary>
        public void Show(Shape on, string name, int sortingOrder)
        {
            shape = on;
            title = name ?? "";
            order = sortingOrder;
            Write();
        }

        public void Turn() => Facing = -Facing;

        public void Face(int direction)
        {
            if (direction != 0)
            {
                Facing = direction > 0 ? 1 : -1;
            }
        }

        private void Awake()
        {
            Find();
        }

        private void Find()
        {
            if (body != null)
            {
                return;
            }
            body = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            gameObject.layer = Layers.Enemy;
            EnemyPhysics.Prepare(body, box);
            health = Mathf.Max(build.health, 1);
        }

        private void FixedUpdate()
        {
            if (IsDead)
            {
                return;
            }
            var dt = Time.fixedDeltaTime;
            IsGrounded = EnemyPhysics.IsGrounded(box, hits);
            var velocity = body.linearVelocity * Ppu;
            if (!build.flies)
            {
                velocity.y = Mathf.Max(velocity.y - Gravity * dt, -MaxFallSpeed);
            }
            if (hurtLeft > 0f)
            {
                hurtLeft -= dt;
                velocity.x = Mathf.MoveTowards(velocity.x, 0f, Friction * dt);
                if (build.flies)
                {
                    velocity.y = Mathf.MoveTowards(velocity.y, 0f, Friction * dt);
                }
            }
            else
            {
                Move = 0f;
                foreach (var behaviour in behaviours)
                {
                    if (behaviour.Step(this, dt))
                    {
                        break;
                    }
                }
                velocity.x = Move;
            }
            body.linearVelocity = velocity / Ppu;
            if (painted != IsHurt)
            {
                Write();
            }
            if (build.contactDamage > 0)
            {
                var bounds = box.bounds;
                EnemyPhysics.HurtHero(bounds.center, bounds.size, build.contactDamage, bounds.center, hits);
            }
        }

        /// <summary>A hit that does not say what it is counts as the sword's, with the plain push.</summary>
        public bool Hit(int damage, Vector2 from) => Struck(new Strike(StrikeKind.Sword, damage, from));

        public bool Struck(Strike strike)
        {
            if (IsDead)
            {
                return false;
            }
            Find();
            health -= strike.Damage;
            Audio.Sfx.Play("hit");
            Feedback.Spark(transform.position);
            if (health <= 0)
            {
                Die();
                return true;
            }
            hurtLeft = build.hurtTime;
            var push = strike.Push;
            if (push == Vector2.zero)
            {
                push = new Vector2(transform.position.x >= strike.From.x ? PlainPush : -PlainPush, 0f);
            }
            body.linearVelocity = push * build.pushShare / Ppu;
            Write();
            return true;
        }

        private void Die()
        {
            IsDead = true;
            health = 0;
            box.enabled = false;
            body.linearVelocity = Vector2.zero;
            Audio.Sfx.Play("enemy_die");
            Feedback.HitStop(Walker.DeathHitStop);
            Feedback.Spark(transform.position, build.color);
            Died?.Invoke(this);
            gameObject.SetActive(false);
        }

        private void Write()
        {
            painted = IsHurt;
            if (shape == null)
            {
                return;
            }
            var label = string.IsNullOrEmpty(title) ? "" : title + "\n";
            label += health + "/" + Mathf.Max(build.health, 1);
            shape.Set(build.shape, box.size, IsHurt ? HurtColor : build.color, label, order);
        }
    }
}
