using System;
using System.Collections;
using UnityEngine;

namespace Descender.Fx
{
    /// <summary>
    /// The small tweens the game needs, as coroutines to start on the object they
    /// move: scale, fade, a position slide. No tween package. Every one runs on scaled
    /// time, so a hit-stop holds it with the rest of the world.
    /// </summary>
    public static class Tween
    {
        public static IEnumerator Scale(Transform target, Vector3 from, Vector3 to, float seconds)
        {
            var t = 0f;
            while (t < seconds && target != null)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            if (target != null)
            {
                target.localScale = to;
            }
        }

        /// <summary>Fades a renderer's alpha to a value.</summary>
        public static IEnumerator Fade(SpriteRenderer renderer, float toAlpha, float seconds)
        {
            if (renderer == null)
            {
                yield break;
            }
            var from = renderer.color;
            var to = new Color(from.r, from.g, from.b, toAlpha);
            var t = 0f;
            while (t < seconds && renderer != null)
            {
                t += Time.deltaTime;
                renderer.color = Color.Lerp(from, to, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            if (renderer != null)
            {
                renderer.color = to;
            }
        }

        /// <summary>Rotates and shifts a transform, eased out: a body toppling over.</summary>
        public static IEnumerator Topple(Transform target, float degrees, Vector3 shift, float seconds)
        {
            var fromRotation = target.localRotation;
            var toRotation = Quaternion.Euler(0f, 0f, degrees);
            var fromPosition = target.localPosition;
            var toPosition = fromPosition + shift;
            var t = 0f;
            while (t < seconds && target != null)
            {
                t += Time.deltaTime;
                var k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / seconds), 2f);
                target.localRotation = Quaternion.Slerp(fromRotation, toRotation, k);
                target.localPosition = Vector3.Lerp(fromPosition, toPosition, k);
                yield return null;
            }
        }

        /// <summary>Moves a transform to a point, eased in and out.</summary>
        public static IEnumerator Move(Transform target, Vector3 to, float seconds, Action done = null)
        {
            var from = target.position;
            var t = 0f;
            while (t < seconds && target != null)
            {
                t += Time.deltaTime;
                target.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            if (target != null)
            {
                target.position = to;
            }
            done?.Invoke();
        }

        public static IEnumerator Wait(float seconds)
        {
            var t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
