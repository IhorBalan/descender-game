using Descender.Combat;
using Descender.Player;
using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// What a <see cref="Crumble"/> and a <see cref="Blinker"/> share: a platform that is
    /// there or gone by the clock. It is whatever colliders and sprites the object and
    /// its children have; gone, it is passable and faint. It stands still. A trigger's
    /// <see cref="SetTimed"/> action switches it: off, it is gone and stays gone; on, it
    /// starts again from whole.
    /// </summary>
    public abstract class TimedPlatform : MonoBehaviour, IUnsteadyGround
    {
        /// <summary>How much of its colour it keeps while gone.</summary>
        public const float GoneAlpha = 0.15f;

        [SerializeField]
        [Tooltip("Gone until a trigger switches it on.")]
        private bool startsOff;

        private static readonly Collider2D[] Found = new Collider2D[4];

        private Collider2D[] boxes;
        private SpriteRenderer[] sprites;
        private Vector3[] rest;
        private Bounds bounds;
        private ContactFilter2D heroFilter;
        private bool cached;

        /// <summary>It can be stood on now.</summary>
        public bool IsSolid { get; private set; } = true;

        /// <summary>It is working; switched off it is gone.</summary>
        public bool IsOn { get; private set; } = true;

        protected virtual void Start()
        {
            Cache();
            if (startsOff)
            {
                Set(SwitchTo.Off);
            }
        }

        public void Set(SwitchTo to)
        {
            Cache();
            IsOn = to == SwitchTo.Toggle ? !IsOn : to == SwitchTo.On;
            Restart();
            if (!IsOn)
            {
                Show(false, GoneAlpha, Vector2.zero);
            }
        }

        /// <summary>Whether it starts gone, for the code that makes one.</summary>
        protected void StartOff(bool off) => startsOff = off;

        /// <summary>Back to the start of its cycle, whole.</summary>
        protected abstract void Restart();

        /// <summary>Solid or passable, how much of its colour shows, and how far its picture is shifted.</summary>
        protected void Show(bool solid, float alpha, Vector2 shift)
        {
            Cache();
            IsSolid = solid;
            foreach (var box in boxes)
            {
                box.enabled = solid;
            }
            for (var i = 0; i < sprites.Length; i++)
            {
                var color = sprites[i].color;
                color.a = alpha;
                sprites[i].color = color;
                sprites[i].transform.localPosition = rest[i] + (Vector3)shift;
            }
        }

        /// <summary>The hero stands on its top.</summary>
        protected bool HeroOnTop()
        {
            var pixel = 1f / PlayerTuning.PixelsPerUnit;
            var centre = new Vector2(bounds.center.x, bounds.max.y + pixel);
            var size = new Vector2(bounds.size.x - 2f * pixel, 2f * pixel);
            var count = Physics2D.OverlapBox(centre, size, 0f, heroFilter, Found);
            for (var i = 0; i < count; i++)
            {
                var hero = Found[i].GetComponentInParent<PlayerController>();
                if (hero != null && hero.IsGrounded && Found[i].bounds.min.y >= bounds.max.y - 2f * pixel)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The hero is where it would be, so it cannot come back yet.</summary>
        protected bool HeroInside()
        {
            var pixel = 1f / PlayerTuning.PixelsPerUnit;
            var size = new Vector2(bounds.size.x - 2f * pixel, bounds.size.y - 2f * pixel);
            return Physics2D.OverlapBox(bounds.center, size, 0f, heroFilter, Found) > 0;
        }

        private void Cache()
        {
            if (cached)
            {
                return;
            }
            cached = true;
            boxes = GetComponentsInChildren<Collider2D>(true);
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            rest = new Vector3[sprites.Length];
            for (var i = 0; i < sprites.Length; i++)
            {
                rest[i] = sprites[i].transform.localPosition;
            }
            // A switched-off collider has no bounds, so they are taken while it is whole.
            bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var box in boxes)
            {
                var was = box.enabled;
                box.enabled = true;
                bounds.Encapsulate(box.bounds);
                box.enabled = was;
            }
            heroFilter = Layers.Solid(Layers.PlayerMask);
        }
    }
}
