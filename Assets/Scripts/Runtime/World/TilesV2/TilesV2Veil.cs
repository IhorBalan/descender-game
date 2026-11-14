using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// The shimmer of a Tiles V2 light barrier: its veil turns between its two frames and
    /// breathes brighter and dimmer, each barrier out of step with the others.
    /// </summary>
    public sealed class TilesV2Veil : MonoBehaviour
    {
        private const float FrameSeconds = 0.18f;
        private const float BreathSpeed = 3.2f;
        private const float Dimmest = 0.7f;

        private SpriteRenderer shown;
        private Sprite[] frames = System.Array.Empty<Sprite>();
        private float time;

        public void Configure(SpriteRenderer veil, Sprite first, Sprite second)
        {
            shown = veil;
            frames = new[] { first, second };
            time = Mathf.Repeat(transform.position.x * 0.61f + transform.position.y * 0.37f, 1f) * 5f;
        }

        private void Update()
        {
            if (shown == null)
            {
                return;
            }
            time += Time.deltaTime;
            shown.sprite = frames[(int)(time / FrameSeconds) % frames.Length];
            var glow = Mathf.Lerp(Dimmest, 1f, (Mathf.Sin(time * BreathSpeed) + 1f) / 2f);
            shown.color = new Color(1f, 1f, 1f, glow);
        }
    }
}
