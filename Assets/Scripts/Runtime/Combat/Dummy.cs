using System;
using System.Collections.Generic;
using System.Globalization;
using Descender.Fx;
using UnityEngine;

namespace Descender.Combat
{
    /// <summary>
    /// A training dummy: it takes every hit, never dies, and says what hit it. It keeps
    /// the last strike, the count and the total, and the damage a second over the last
    /// few seconds, and writes them on its shape's label when it has one.
    /// </summary>
    public sealed class Dummy : MonoBehaviour, IStruck
    {
        /// <summary>The seconds the damage a second is taken over.</summary>
        public const float Window = 3f;

        private readonly Queue<KeyValuePair<float, int>> recent = new Queue<KeyValuePair<float, int>>();
        private Shape shape;
        private string title = "";
        private int order;

        /// <summary>How many strikes it has taken.</summary>
        public int Hits { get; private set; }

        /// <summary>The damage of all of them.</summary>
        public int Total { get; private set; }

        /// <summary>The last strike; meaningless before the first.</summary>
        public Strike Last { get; private set; }

        /// <summary>The game time of the last strike.</summary>
        public float LastAt { get; private set; }

        /// <summary>Raised with each strike it takes.</summary>
        public event Action<Strike> Took;

        /// <summary>The damage a second over the last <see cref="Window"/> seconds.</summary>
        public float DamagePerSecond
        {
            get
            {
                var sum = 0;
                foreach (var entry in recent)
                {
                    sum += Time.time - entry.Key > Window ? 0 : entry.Value;
                }
                return sum / Window;
            }
        }

        /// <summary>The shape whose label is its read-out, with the name shown above the numbers.</summary>
        public void Show(Shape on, string name, int sortingOrder)
        {
            shape = on;
            title = name ?? "";
            order = sortingOrder;
            Write();
        }

        /// <summary>Forgets every strike.</summary>
        public void Clear()
        {
            Hits = 0;
            Total = 0;
            Last = default;
            LastAt = 0f;
            recent.Clear();
            Write();
        }

        public bool Hit(int damage, Vector2 from) => Struck(new Strike(StrikeKind.Sword, damage, from));

        public bool Struck(Strike strike)
        {
            Hits++;
            Total += strike.Damage;
            Last = strike;
            LastAt = Time.time;
            recent.Enqueue(new KeyValuePair<float, int>(Time.time, strike.Damage));
            Feedback.Spark(transform.position);
            Write();
            Took?.Invoke(strike);
            return true;
        }

        /// <summary>The read-out as text: the last strike, the count and total, and the damage a second.</summary>
        public string ReadOut()
        {
            if (Hits == 0)
            {
                return title;
            }
            var head = string.IsNullOrEmpty(title) ? "" : title + "\n";
            var rate = DamagePerSecond.ToString("0.0", CultureInfo.InvariantCulture);
            return $"{head}{Last.Kind} {Last.Damage}\n{Hits} hits, {Total} in all\n{rate} a second";
        }

        private void Update()
        {
            // The damage a second falls as strikes leave the window.
            var left = false;
            while (recent.Count > 0 && Time.time - recent.Peek().Key > Window)
            {
                recent.Dequeue();
                left = true;
            }
            if (left)
            {
                Write();
            }
        }

        private void Write()
        {
            if (shape != null)
            {
                shape.Set(shape.Kind, shape.Size, shape.Color, ReadOut(), order);
            }
        }
    }
}
