using Descender.Player;
using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Something in a Tiles V2 world that says a line when the hero stands by it: a
    /// signpost, a tablet with words cut in it, a shade, who wavers where it stands, or one
    /// of the blessed, a soul, who does too.
    /// The scene's runner shows the line of the one being read. Origin at the foot of its cell.
    /// </summary>
    public sealed class TilesV2Sign : MonoBehaviour
    {
        public const string Post = "post";
        public const string Tablet = "tablet";
        public const string Shade = "shade";
        public const string Soul = "soul";

        /// <summary>How near the hero reads it, in units, across and up.</summary>
        public static readonly Vector2 Reach = new Vector2(2.2f, 2.5f);

        private SpriteRenderer shown;
        private bool wavers;
        private float time;

        /// <summary>The sign the hero stands by now; null for none.</summary>
        public static TilesV2Sign Reading { get; private set; }

        /// <summary>What it says.</summary>
        public string Text { get; private set; } = "";

        /// <summary>What it is: a post, a tablet, a shade or a soul.</summary>
        public string Kind { get; private set; } = Post;

        /// <summary>Sets what it is, what it says and what it is drawn with.</summary>
        public void Configure(string kind, string text, Sprite sprite, Material material)
        {
            Kind = kind;
            Text = text ?? "";
            wavers = kind == Shade || kind == Soul;
            var picture = new GameObject("Picture");
            picture.transform.SetParent(transform, false);
            picture.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            shown = picture.AddComponent<SpriteRenderer>();
            shown.sprite = sprite;
            shown.sortingOrder = TilesV2World.TorchOrder;
            if (material != null)
            {
                shown.sharedMaterial = material;
            }
            time = Mathf.Repeat(transform.position.x * 0.43f, 1f) * 6f;
        }

        /// <summary>Whether a hero whose feet are at a point is near enough to read it.</summary>
        public bool IsRead(Vector2 feet)
        {
            var from = feet - (Vector2)transform.position;
            return Mathf.Abs(from.x) <= Reach.x && from.y >= -0.5f && from.y <= Reach.y;
        }

        private void Update()
        {
            if (wavers && shown != null)
            {
                time += Time.deltaTime;
                shown.transform.localPosition = new Vector3(0f, 0.6f + Mathf.Sin(time * 1.7f) * 0.08f, 0f);
                shown.color = new Color(1f, 1f, 1f, 0.75f + Mathf.Sin(time * 2.3f) * 0.15f);
            }
            var hero = PlayerController.Current;
            var near = hero != null && hero.IsAlive && IsRead(hero.transform.position);
            if (near)
            {
                Reading = this;
            }
            else if (Reading == this)
            {
                Reading = null;
            }
        }

        private void OnDisable()
        {
            if (Reading == this)
            {
                Reading = null;
            }
        }
    }
}
