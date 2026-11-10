using UnityEngine;

namespace Descender.Triggers
{
    /// <summary>
    /// A platform on a timer: solid for a time, gone for a time, over and over, blinking
    /// for a moment before each change. Its offset shifts it along the cycle, so a row
    /// of them with rising offsets makes a pattern. It does not turn solid on the hero:
    /// that turn waits until he is out of it.
    /// </summary>
    public sealed class Blinker : TimedPlatform
    {
        /// <summary>One blink of the warning, on and off, in seconds.</summary>
        private const float BlinkPeriod = 0.16f;

        /// <summary>What a blink shows in between, whichever way it is about to change.</summary>
        private const float BlinkAlpha = 0.5f;

        [SerializeField, Min(0.02f)]
        private float solidTime = 1.5f;

        [SerializeField, Min(0.02f)]
        private float goneTime = 1.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds of blinking before each change.")]
        private float warnTime = 0.4f;

        [SerializeField]
        [Tooltip("Seconds it is ahead of a blinker with no offset.")]
        private float offset;

        private int steps;

        public float SolidTime => solidTime;
        public float GoneTime => goneTime;
        public float WarnTime => warnTime;
        public float Offset => offset;

        /// <summary>It is about to change.</summary>
        public bool IsWarning { get; private set; }

        public void Configure(float solidFor, float goneFor, float warnFor, float ahead, bool startsOff = false)
        {
            solidTime = Mathf.Max(solidFor, 0.02f);
            goneTime = Mathf.Max(goneFor, 0.02f);
            warnTime = Mathf.Max(warnFor, 0f);
            offset = ahead;
            StartOff(startsOff);
        }

        protected override void Restart()
        {
            steps = 0;
            IsWarning = false;
            Apply();
        }

        private void FixedUpdate()
        {
            if (!IsOn)
            {
                return;
            }
            steps++;
            Apply();
        }

        private void Apply()
        {
            var period = solidTime + goneTime;
            var at = Mathf.Repeat(steps * Time.fixedDeltaTime + offset, period);
            var solid = at < solidTime;
            var toChange = solid ? solidTime - at : period - at;
            IsWarning = toChange <= warnTime;
            if (solid && !IsSolid && HeroInside())
            {
                solid = false;
            }
            var lit = !IsWarning || Mathf.Repeat(toChange, BlinkPeriod) < BlinkPeriod / 2f;
            var alpha = lit ? (solid ? 1f : GoneAlpha) : BlinkAlpha;
            Show(solid, alpha, Vector2.zero);
        }
    }
}
