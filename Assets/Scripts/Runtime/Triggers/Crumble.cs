using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// A platform that gives way: once the hero stands on it it shakes and fades for a
    /// time, drops out, and comes back after a time (not while he is where it would
    /// be). Stepping off does not stop the shaking.
    /// </summary>
    public sealed class Crumble : TimedPlatform
    {
        /// <summary>How far its picture moves while it shakes, in units.</summary>
        private const float ShakeBy = 1.5f / Player.PlayerTuning.PixelsPerUnit;

        /// <summary>How much of its colour is left at the end of the shaking.</summary>
        private const float ShakenAlpha = 0.45f;

        public enum Stage
        {
            Whole,
            Shaking,
            Gone,
        }

        [SerializeField, Min(0f)]
        [Tooltip("Seconds it holds once stood on.")]
        private float shakeTime = 0.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds it stays gone.")]
        private float goneTime = 2f;

        private float left;
        private int steps;

        public Stage Now { get; private set; }
        public float ShakeTime => shakeTime;
        public float GoneTime => goneTime;

        public void Configure(float holdsFor, float goneFor, bool startsOff = false)
        {
            shakeTime = Mathf.Max(holdsFor, 0f);
            goneTime = Mathf.Max(goneFor, 0f);
            StartOff(startsOff);
        }

        protected override void Restart()
        {
            Now = Stage.Whole;
            left = 0f;
            Show(true, 1f, Vector2.zero);
        }

        private void FixedUpdate()
        {
            if (!IsOn)
            {
                return;
            }
            var dt = Time.fixedDeltaTime;
            switch (Now)
            {
                case Stage.Whole:
                    if (HeroOnTop())
                    {
                        Now = Stage.Shaking;
                        left = shakeTime;
                        steps = 0;
                    }
                    break;
                case Stage.Shaking:
                    left -= dt;
                    steps++;
                    if (left <= 0f)
                    {
                        Now = Stage.Gone;
                        left = goneTime;
                        Show(false, GoneAlpha, Vector2.zero);
                        break;
                    }
                    var done = shakeTime > 0f ? 1f - left / shakeTime : 1f;
                    // Side to side every other step: seen, and the same every time.
                    var shift = new Vector2(steps % 4 < 2 ? ShakeBy : -ShakeBy, 0f);
                    Show(true, Mathf.Lerp(1f, ShakenAlpha, done), shift);
                    break;
                case Stage.Gone:
                    left -= dt;
                    if (left <= 0f && !HeroInside())
                    {
                        Restart();
                    }
                    break;
            }
        }
    }
}
