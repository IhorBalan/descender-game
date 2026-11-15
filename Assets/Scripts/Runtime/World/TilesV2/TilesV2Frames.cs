using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// A picture of a Tiles V2 world that is several pictures shown in turn: a tree whose
    /// crown stirs, a banner, a pennant flying, water running in a fountain. Each starts
    /// at its own moment, told by where it stands, so no two move as one. One that rests
    /// (a glint on gold) shows its frames once, is gone for a while, and comes again.
    /// </summary>
    public sealed class TilesV2Frames : MonoBehaviour
    {
        private SpriteRenderer shown;
        private Sprite[] frames = System.Array.Empty<Sprite>();
        private float rate = 1f;
        private float rest;
        private float time;
        private float wait;

        /// <summary>The frame it shows.</summary>
        public int Frame { get; private set; }

        /// <summary>How many frames it has.</summary>
        public int Count => frames.Length;

        /// <summary>Whether it is gone for now, between two showings.</summary>
        public bool Resting => wait > 0f;

        /// <summary>
        /// Sets what it shows on, its frames, how many of them a second, and how long it is
        /// gone between two showings of them (0: it never rests).
        /// </summary>
        public void Configure(SpriteRenderer picture, Sprite[] pictures, float framesASecond, float restFor)
        {
            shown = picture;
            frames = pictures ?? System.Array.Empty<Sprite>();
            rate = Mathf.Max(framesASecond, 0.01f);
            rest = Mathf.Max(restFor, 0f);
            var at = transform.position;
            var own = Mathf.Repeat(at.x * 0.37f + at.y * 0.11f, 1f);
            time = own * frames.Length / rate;
            wait = rest > 0f ? own * rest + 0.01f : 0f;
            Show();
        }

        /// <summary>Moves it on by a time.</summary>
        public void Step(float seconds)
        {
            if (frames.Length == 0 || shown == null)
            {
                return;
            }
            if (wait > 0f)
            {
                wait -= seconds;
                if (wait > 0f)
                {
                    return;
                }
                time = 0f;
            }
            else
            {
                time += seconds;
            }
            if (rest > 0f && time * rate >= frames.Length)
            {
                // Shown once: gone for its rest, a little more or less each time.
                wait = rest * (0.6f + 0.8f * Mathf.Repeat(Time.time * 0.731f + transform.position.x * 0.37f, 1f));
            }
            Show();
        }

        private void Show()
        {
            if (frames.Length == 0 || shown == null)
            {
                return;
            }
            shown.enabled = wait <= 0f;
            Frame = (int)(time * rate) % frames.Length;
            shown.sprite = frames[Frame];
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }
    }
}
