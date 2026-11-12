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
    /// moment; at no health it dies. Every attack a behaviour makes starts with a
    /// telegraph (<see cref="Telegraph"/>): a colour and a mark for as long as the
    /// behaviour holds it, before anything can hurt. A hit that holds it cancels what
    /// its behaviours were doing; an armoured body is not held. Its origin is the middle
    /// of its body. Drawn as a <see cref="Shape"/> whose label is its name and health.
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
        public static readonly Color TelegraphColor = new Color(1f, 0.75f, 0.3f);

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
        private int painted;

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

        /// <summary>True while a behaviour is warning of an attack.</summary>
        public bool IsTelegraphing { get; private set; }

        /// <summary>True when a wall, or for a walking body the end of the floor, is just ahead.</summary>
        public bool IsBlockedAhead =>
            EnemyPhysics.IsOnWall(box, Facing, hits) || (!build.flies && !EnemyPhysics.FloorAhead(box, Facing, hits));

        public IReadOnlyList<FoeBehaviour> Behaviours => behaviours;

        /// <summary>Its body and its behaviours, in the order they are asked. Both are copied: they are this foe's own.</summary>
        public void Configure(FoeBody with, IEnumerable<FoeBehaviour> acts)
        {
            Find();
            build = with != null ? with.Copy() : new FoeBody();
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

        /// <summary>The warning before an attack, on or off: its colour and a mark on its label.</summary>
        public void Telegraph(bool on)
        {
            if (IsTelegraphing != on)
            {
                IsTelegraphing = on;
                Write();
            }
        }

        /// <summary>Where the hero's middle is from its own, in level pixels. False when there is no hero to find.</summary>
        public bool HeroOffset(out Vector2 pixels)
        {
            var hero = EnemyPhysics.LiveHero();
            if (hero == null)
            {
                pixels = Vector2.zero;
                return false;
            }
            // His position is his feet; his middle is a unit above them.
            pixels = ((Vector2)hero.transform.position + Vector2.up - (Vector2)box.bounds.center) * Ppu;
            return true;
        }

        /// <summary>Hurts the hero if he is inside a box of a size (level pixels) just ahead of its body. True when it landed.</summary>
        public bool HurtAhead(Vector2 reach, int damage)
        {
            var bounds = box.bounds;
            var size = reach / Ppu;
            var centre = new Vector2(bounds.center.x + Facing * (bounds.extents.x + size.x / 2f), bounds.center.y);
            return EnemyPhysics.HurtHero(centre, size, damage, bounds.center, hits);
        }

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
            if (painted != Look())
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
            var held = !build.armour;
            foreach (var behaviour in behaviours)
            {
                behaviour.OnStruck(this, held);
            }
            if (!held)
            {
                // Armour: the blow lands, and what it was doing goes on.
                Write();
                return true;
            }
            Telegraph(false);
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

        /// <summary>What it looks like now: 1 held by a hit, 2 warning of an attack, 0 otherwise.</summary>
        private int Look() => IsHurt ? 1 : (IsTelegraphing ? 2 : 0);

        private void Write()
        {
            painted = Look();
            if (shape == null)
            {
                return;
            }
            var label = painted == 2 ? "!\n" : "";
            label += string.IsNullOrEmpty(title) ? "" : title + "\n";
            label += health + "/" + Mathf.Max(build.health, 1);
            var color = painted == 1 ? HurtColor : (painted == 2 ? TelegraphColor : build.color);
            shape.Set(build.shape, box.size, color, label, order);
        }
    }
}
