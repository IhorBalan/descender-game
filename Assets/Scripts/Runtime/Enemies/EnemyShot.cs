using Descender.Combat;
using Descender.Fx;
using Descender.Player;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// Something an enemy throws at the hero. A Bolt of hellfire flies straight,
    /// hurts the hero once and breaks on the world and on walls of light; a Wave is
    /// a shockwave that runs along the floor from a slam, low enough to jump over,
    /// dying at the end of its ledge or at a wall. Made in code by
    /// <see cref="Spawn"/>; drawn with Shapes. Port of scripts/enemies/enemy_shot.gd.
    /// </summary>
    public sealed class EnemyShot : MonoBehaviour
    {
        public enum Kind
        {
            Bolt,
            Wave,
        }

        private const float Ppu = PlayerTuning.PixelsPerUnit;
        public const float BoltSpeed = 150f;
        public const float BoltLifetime = 1.3f;
        public static readonly Vector2 BoltSize = new Vector2(8f, 6f);
        public static readonly Color BoltColor = new Color(1.0f, 0.5f, 0.2f);
        public static readonly Color BoltCore = new Color(1.0f, 0.9f, 0.6f);
        public const float WaveSpeed = 120f;
        public const float WaveLifetime = 1.1f;
        public static readonly Vector2 WaveSize = new Vector2(12f, 10f);
        public static readonly Vector2 BigWaveSize = new Vector2(18f, 15f);
        public const float BigWaveSpeed = 145f;
        public const float BigWaveLifetime = 1.7f;
        public static readonly Color WaveColor = new Color(0.75f, 0.5f, 0.35f);
        public static readonly Color WaveDust = new Color(0.9f, 0.75f, 0.6f);

        private static readonly ContactFilter2D Solid = Layers.Solid(Layers.SolidMask);
        private static readonly ContactFilter2D Ground = Layers.Solid(Layers.GroundMask);

        private readonly Collider2D[] hits = new Collider2D[4];
        private Kind kind;
        private Vector2 direction = Vector2.right;
        private int damage = 1;
        private bool big;
        private Color color = BoltColor;
        private float time;
        private bool done;
        private SpriteRenderer flicker;

        public Kind ShotKind => kind;
        public Vector2 Direction => direction;

        /// <summary>A shot at a point (bolt: its centre; wave: its feet) going a way.</summary>
        public static EnemyShot Spawn(
            Kind kind,
            Vector2 at,
            Vector2 direction,
            int damage = 1,
            bool big = false,
            Color? color = null,
            Color? core = null
        )
        {
            var go = new GameObject(kind == Kind.Bolt ? "Bolt" : "Wave") { layer = Layers.Hazard };
            go.transform.position = at;
            var shot = go.AddComponent<EnemyShot>();
            shot.kind = kind;
            shot.direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            shot.damage = damage;
            shot.big = big;
            shot.color = color ?? BoltColor;
            if (kind == Kind.Bolt)
            {
                var angle = Mathf.Atan2(shot.direction.y, shot.direction.x) * Mathf.Rad2Deg;
                go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                Shapes.Block(go.transform, "Tail", new Vector2(8f, 3f), new Vector2(-4f, 0f), shot.color);
                Shapes.Block(go.transform, "Core", new Vector2(4f, 4f), Vector2.zero, core ?? BoltCore);
                shot.flicker = Shapes.Block(go.transform, "Flicker", new Vector2(2f, 2f), new Vector2(-1f, 4f), shot.color);
            }
            else
            {
                var size = shot.Size;
                var k = size.y / WaveSize.y;
                // A low hump of rubble with dust flying off its back.
                Shapes.Block(go.transform, "Hump", new Vector2(10f * k, 6f * k), new Vector2(0f, 3f * k), WaveColor);
                Shapes.Block(go.transform, "Crest", new Vector2(6f * k, 9f * k), new Vector2(0f, 4.5f * k), WaveColor);
                var behind = -shot.direction.x;
                for (var i = 0; i < 3; i++)
                {
                    Shapes.Block(
                        go.transform,
                        "Dust",
                        new Vector2(2f, 2f),
                        new Vector2(behind * (6f + i * 4f) * k, (6f + i * 2f) * k),
                        WaveDust
                    );
                }
            }
            return shot;
        }

        private Vector2 Size => kind == Kind.Bolt ? BoltSize : (big ? BigWaveSize : WaveSize);

        /// <summary>The hit box, in units: the bolt about its centre, the wave a pixel above the floor.</summary>
        private Vector2 Centre =>
            kind == Kind.Bolt ? (Vector2)transform.position : (Vector2)transform.position + new Vector2(0f, (Size.y / 2f + 1f) / Ppu);

        private void FixedUpdate()
        {
            if (done)
            {
                return;
            }
            var dt = Time.fixedDeltaTime;
            time += dt;
            if (kind == Kind.Bolt)
            {
                transform.position += (Vector3)(direction * BoltSpeed * dt / Ppu);
                if (flicker != null)
                {
                    var up = (int)(time * 30f) % 2 == 0 ? 1f : -1f;
                    flicker.transform.localPosition = new Vector3(-1f / Ppu, 4f * up / Ppu, 0f);
                }
                if (time >= BoltLifetime)
                {
                    Fade();
                    return;
                }
            }
            else
            {
                var speed = big ? BigWaveSpeed : WaveSpeed;
                transform.position += new Vector3(direction.x * speed * dt / Ppu, 0f, 0f);
                var lifetime = big ? BigWaveLifetime : WaveLifetime;
                if (time >= lifetime || !FloorUnderFront())
                {
                    Fade();
                    return;
                }
            }
            Touch();
        }

        private bool FloorUnderFront()
        {
            var front = (Vector2)transform.position + new Vector2(direction.x * Size.x / 2f / Ppu, -2f / Ppu);
            return Physics2D.OverlapBox(front, new Vector2(2f / Ppu, 8f / Ppu), 0f, Ground, hits) > 0;
        }

        private void Touch()
        {
            var size = Size / Ppu;
            if (EnemyPhysics.HurtHero(Centre, size, damage, transform.position, hits))
            {
                Feedback.Spark(transform.position, kind == Kind.Bolt ? color : WaveDust);
                Fade();
                return;
            }
            if (Physics2D.OverlapBox(Centre, size, 0f, Solid, hits) > 0)
            {
                // The bolt breaks on the world; the wave has hit a wall.
                if (kind == Kind.Bolt)
                {
                    Feedback.Spark(transform.position, color);
                }
                Fade();
            }
        }

        /// <summary>Gone at once: no second hit.</summary>
        private void Fade()
        {
            done = true;
            Destroy(gameObject);
        }
    }
}
