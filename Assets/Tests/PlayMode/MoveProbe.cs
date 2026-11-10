using System;
using System.Collections;
using Descender.Input;
using Descender.Player;
using UnityEngine;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// Drives a hero through a <see cref="ScriptedInput"/> one physics step per frame
    /// (the test sets Time.captureDeltaTime to the fixed step) and measures the move:
    /// how high he got, how far, how long he was in the air. Everything is in level
    /// pixels and seconds, as <see cref="PlayerTuning"/> is. <see cref="Begin"/> marks
    /// where the measuring starts; every stepping method samples after each frame.
    /// </summary>
    public sealed class MoveProbe
    {
        private readonly PlayerController hero;
        private readonly ScriptedInput input;

        public MoveProbe(PlayerController hero, ScriptedInput input)
        {
            this.hero = hero;
            this.input = input;
            Begin();
        }

        /// <summary>One physics step, in seconds.</summary>
        public static float Dt => Time.fixedDeltaTime;

        /// <summary>Where the hero is, in pixels.</summary>
        public Vector2 At => (Vector2)hero.transform.position * PlayerTuning.PixelsPerUnit;

        /// <summary>Where he was at <see cref="Begin"/>.</summary>
        public Vector2 From { get; private set; }

        /// <summary>How far he is above where he began.</summary>
        public float Height => At.y - From.y;

        /// <summary>The highest he has been above where he began.</summary>
        public float Apex { get; private set; }

        /// <summary>How far he is sideways from where he began, either way.</summary>
        public float Distance => Mathf.Abs(At.x - From.x);

        /// <summary>The frames since <see cref="Begin"/> he was off the ground.</summary>
        public int AirSteps { get; private set; }

        /// <summary>The time since <see cref="Begin"/> he was off the ground.</summary>
        public float AirTime => AirSteps * Dt;

        /// <summary>The frames since <see cref="Begin"/>.</summary>
        public int Steps { get; private set; }

        /// <summary>Starts measuring from where he is now.</summary>
        public void Begin()
        {
            From = At;
            Apex = 0f;
            AirSteps = 0;
            Steps = 0;
        }

        private void Sample()
        {
            Steps++;
            if (!hero.IsGrounded)
            {
                AirSteps++;
            }
            Apex = Mathf.Max(Apex, Height);
        }

        /// <summary>Lets frames pass.</summary>
        public IEnumerator Step(int count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
                Sample();
            }
        }

        /// <summary>Lets frames pass until something is so, or the limit runs out.</summary>
        public IEnumerator Until(Func<bool> done, int limit)
        {
            for (var i = 0; i < limit && !done(); i++)
            {
                yield return null;
                Sample();
            }
        }

        /// <summary>
        /// One press: set by `press`, read by the hero in the frame this takes, then
        /// cleared. He acts on it in the next physics step.
        /// </summary>
        public IEnumerator Press(Action<ScriptedInput> press)
        {
            press(input);
            yield return null;
            Sample();
            input.ClearPresses();
        }

        /// <summary>A press of jump, held down after it or let go at once.</summary>
        public IEnumerator PressJump(bool hold = true)
        {
            input.JumpHeld = hold;
            input.JumpPressed = true;
            yield return null;
            Sample();
            input.ClearPresses();
        }

        /// <summary>
        /// Lets go of everything and waits until he stands still on the ground. A hit in the
        /// test before may have left time slowed, which ends on the real clock; while it lasts
        /// a frame is a twentieth of a physics step, so counted steps would count nothing.
        /// </summary>
        public IEnumerator Settle(int limit = 300)
        {
            input.Release();
            input.ClearPresses();
            var giveUpAt = Time.realtimeSinceStartup + 5f;
            while (Fx.Feedback.IsHitStopped && Time.realtimeSinceStartup < giveUpAt)
            {
                yield return null;
            }
            for (var i = 0; i < limit && !hero.IsGrounded; i++)
            {
                yield return null;
            }
            for (var i = 0; i < 25; i++)
            {
                yield return null;
            }
        }
    }
}
