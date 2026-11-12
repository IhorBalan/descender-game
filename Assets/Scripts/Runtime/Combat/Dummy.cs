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
    /// few seconds, and writes them on its shape's label when it has one. A strike's push
    /// slides it, friction stops it, and after a rest it glides back to where it stood.
    /// </summary>
    public sealed class Dummy : MonoBehaviour, IStruck
    {
        /// <summary>The seconds the damage a second is taken over.</summary>
        public const float Window = 3f;

        /// <summary>What slows a pushed dummy, in level pixels a second squared.</summary>
        public const float Friction = 400f;

        /// <summary>The seconds it rests where a push left it before it goes home.</summary>
        public const float Rest = 0.6f;

        /// <summary>How fast it goes home, in level pixels a second.</summary>
        public const float ReturnSpeed = 60f;

        private const float Ppu = Player.PlayerTuning.PixelsPerUnit;

        private readonly Queue<KeyValuePair<float, int>> recent = new Queue<KeyValuePair<float, int>>();
        private Shape shape;
        private string title = "";
        private int order;
        private Vector2 home;
        private Vector2 offset;
        private Vector2 slide;
        private float restLeft;

        /// <summary>How many strikes it has taken.</summary>
        public int Hits { get; private set; }

        /// <summary>The damage of all of them.</summary>
        public int Total { get; private set; }

        /// <summary>The last strike; meaningless before the first.</summary>
        public Strike Last { get; private set; }

        /// <summary>The game time of the last strike.</summary>
        public float LastAt { get; private set; }

        /// <summary>How far from where it stood it is, in level pixels.</summary>
        public float Pushed => offset.magnitude;

        /// <summary>The farthest it has been pushed since it last stood at home, in level pixels.</summary>
        public float Farthest { get; private set; }

        /// <summary>True while a push is still carrying it.</summary>
        public bool IsSliding => slide != Vector2.zero;

        /// <summary>How far a push of a speed (px/s) slides it before friction stops it, in level pixels.</summary>
        public static float SlideOf(float push) => push * push / (2f * Friction);

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
            if (offset != Vector2.zero || IsSliding)
            {
                Place(Vector2.zero);
            }
            slide = Vector2.zero;
            Farthest = 0f;
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
            if (strike.Push != Vector2.zero)
            {
                if (offset == Vector2.zero && !IsSliding)
                {
                    home = transform.position;
                    Farthest = 0f;
                }
                slide = strike.Push;
                restLeft = Rest;
            }
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
            var text = $"{head}{Last.Kind} {Last.Damage}\n{Hits} hits, {Total} in all\n{rate} a second";
            if (Farthest > 0f)
            {
                text += "\npushed " + Farthest.ToString("0", CultureInfo.InvariantCulture) + " px";
            }
            return text;
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
            if (Move(Time.deltaTime) || left)
            {
                Write();
            }
        }

        /// <summary>Slides, rests, then goes home. True when the read-out changed.</summary>
        private bool Move(float dt)
        {
            if (IsSliding)
            {
                Place(offset + slide * dt);
                slide = Vector2.MoveTowards(slide, Vector2.zero, Friction * dt);
                var before = Mathf.RoundToInt(Farthest);
                Farthest = Mathf.Max(Farthest, Pushed);
                return Mathf.RoundToInt(Farthest) != before;
            }
            if (offset == Vector2.zero)
            {
                return false;
            }
            restLeft -= dt;
            if (restLeft > 0f)
            {
                return false;
            }
            Place(Vector2.MoveTowards(offset, Vector2.zero, ReturnSpeed * dt));
            if (offset != Vector2.zero)
            {
                return false;
            }
            Farthest = 0f;
            return true;
        }

        /// <summary>Puts it a number of level pixels from where it stood.</summary>
        private void Place(Vector2 pixels)
        {
            offset = pixels;
            var to = home + offset / Ppu;
            transform.position = new Vector3(to.x, to.y, transform.position.z);
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
