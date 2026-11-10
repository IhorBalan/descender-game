using Descender.Combat;
using Descender.Fx;
using Descender.Player;
using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// An area that pushes: a wind along a corridor, a lift up a shaft, a downdraught.
    /// While the hero is in it its wind, a speed in level pixels a second, is added to
    /// his own: he runs faster with it and slower against it, a lift slows his fall and
    /// raises him when it is stronger than the fall (a glide falls slowly, so a lift a
    /// plain fall beats carries a glide up), a downdraught shortens his jump. He picks the
    /// wind up and loses it at `gain`. It can gust, blowing for a time and resting for a
    /// time, and a trigger's <see cref="SetForce"/> action switches it. It runs before
    /// the hero in each physics step, so he is blown by this step's wind.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class ForceZone : MonoBehaviour
    {
        /// <summary>How much of its colour the area keeps while it rests.</summary>
        private const float RestingAlpha = 0.35f;

        /// <summary>The marks drift at this share of the wind's speed, so a strong wind is still read.</summary>
        private const float DriftShare = 0.5f;

        [SerializeField]
        [Tooltip("The area, in units, centred on this object.")]
        private Vector2 size = new Vector2(8f, 4f);

        [SerializeField]
        [Tooltip("The speed it adds, in level pixels a second: x to the right, y up.")]
        private Vector2 wind = new Vector2(60f, 0f);

        [SerializeField, Min(0f)]
        [Tooltip("How fast he picks it up and loses it, in pixels a second squared; 0 for at once.")]
        private float gain = 600f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds it blows in each gust; 0 for a steady wind.")]
        private float gustOn;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds it rests between gusts.")]
        private float gustOff;

        [SerializeField]
        [Tooltip("Seconds it is ahead of a zone with no offset, along the gust cycle.")]
        private float offset;

        [SerializeField]
        private bool startsOff;

        private static readonly Collider2D[] Found = new Collider2D[4];

        private ContactFilter2D heroFilter;
        private Transform[] marks = new Transform[0];
        private SpriteRenderer[] sprites = new SpriteRenderer[0];
        private float[] alphas = new float[0];
        private int steps;
        private bool on = true;
        private bool applied;

        public Vector2 Size => size;
        public Vector2 Wind => wind;
        public float Gain => gain;

        /// <summary>Switched on; off, it never blows.</summary>
        public bool IsOn => on;

        /// <summary>It blows now: switched on and not between gusts.</summary>
        public bool IsBlowing { get; private set; }

        public void Configure(Vector2 area, Vector2 pixelsPerSecond, float pickUp, bool off = false)
        {
            size = area;
            wind = pixelsPerSecond;
            gain = Mathf.Max(pickUp, 0f);
            startsOff = off;
        }

        /// <summary>Blows for a time and rests for a time, over and over, `ahead` seconds along the cycle.</summary>
        public void Gust(float blowsFor, float restsFor, float ahead = 0f)
        {
            gustOn = Mathf.Max(blowsFor, 0f);
            gustOff = Mathf.Max(restsFor, 0f);
            offset = ahead;
        }

        /// <summary>The small shapes that drift along the wind; made by whoever draws the zone.</summary>
        public void Drift(Transform[] drifting)
        {
            marks = drifting ?? new Transform[0];
        }

        public void Set(SwitchTo to)
        {
            on = to == SwitchTo.Toggle ? !on : to == SwitchTo.On;
            applied = true;
            steps = 0;
        }

        private void Awake()
        {
            heroFilter = Layers.Solid(Layers.PlayerMask);
        }

        private void Start()
        {
            if (!applied)
            {
                on = !startsOff;
            }
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            alphas = new float[sprites.Length];
            for (var i = 0; i < sprites.Length; i++)
            {
                alphas[i] = sprites[i].color.a;
            }
            IsBlowing = Blows();
            Look();
        }

        private void FixedUpdate()
        {
            steps++;
            var blows = Blows();
            if (blows != IsBlowing)
            {
                IsBlowing = blows;
                Look();
            }
            if (!IsBlowing)
            {
                return;
            }
            var count = Physics2D.OverlapBox(transform.position, size, 0f, heroFilter, Found);
            for (var i = 0; i < count; i++)
            {
                var hero = Found[i].GetComponentInParent<PlayerController>();
                if (hero != null)
                {
                    hero.Blow(wind, gain);
                    return;
                }
            }
        }

        private void Update()
        {
            if (!IsBlowing || marks.Length == 0 || wind == Vector2.zero)
            {
                return;
            }
            var half = size / 2f;
            var move = (Vector3)(wind / PlayerTuning.PixelsPerUnit * (DriftShare * Time.deltaTime));
            foreach (var mark in marks)
            {
                if (mark == null)
                {
                    continue;
                }
                var at = mark.localPosition + move;
                at.x = Mathf.Repeat(at.x + half.x, size.x) - half.x;
                at.y = Mathf.Repeat(at.y + half.y, size.y) - half.y;
                mark.localPosition = at;
            }
        }

        private bool Blows()
        {
            if (!on)
            {
                return false;
            }
            var period = gustOn + gustOff;
            if (gustOn <= 0f || period <= 0f)
            {
                return true;
            }
            return Mathf.Repeat(steps * Time.fixedDeltaTime + offset, period) < gustOn;
        }

        /// <summary>Faint while it rests.</summary>
        private void Look()
        {
            for (var i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                {
                    continue;
                }
                var color = sprites[i].color;
                color.a = IsBlowing ? alphas[i] : alphas[i] * RestingAlpha;
                sprites[i].color = color;
            }
        }
    }
}
