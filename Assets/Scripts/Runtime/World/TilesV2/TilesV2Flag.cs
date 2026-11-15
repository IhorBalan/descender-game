using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// A pennant flying from a rod in a Tiles V2 world: two tiles of cloth to the right of its
    /// cell's foot, a wave running along them. Each flag starts the wave at its own moment.
    /// </summary>
    public sealed class TilesV2Flag : MonoBehaviour
    {
        /// <summary>Frames of the wave a second.</summary>
        public const float FramesASecond = 7f;

        private SpriteRenderer near;
        private SpriteRenderer far;
        private Sprite[] nearFrames = System.Array.Empty<Sprite>();
        private Sprite[] farFrames = System.Array.Empty<Sprite>();
        private float time;

        /// <summary>The frame of the wave it shows.</summary>
        public int Frame { get; private set; }

        /// <summary>Sets the frames of its half at the rod and of its tail, and what it draws with.</summary>
        public void Configure(Sprite[] atRod, Sprite[] tail, Material material)
        {
            nearFrames = atRod;
            farFrames = tail;
            near = Half("Rod", new Vector3(0f, 0.5f, 0f), material);
            far = Half("Tail", new Vector3(1f, 0.5f, 0f), material);
            time = Mathf.Repeat(transform.position.x * 0.61f, 1f) * nearFrames.Length / FramesASecond;
            Show();
        }

        /// <summary>Moves the wave on by a time.</summary>
        public void Step(float seconds)
        {
            time += seconds;
            Show();
        }

        private SpriteRenderer Half(string name, Vector3 at, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = at;
            var shown = child.AddComponent<SpriteRenderer>();
            shown.sortingOrder = TilesV2World.FoliageOrder + 1;
            if (material != null)
            {
                shown.sharedMaterial = material;
            }
            return shown;
        }

        private void Show()
        {
            if (nearFrames.Length == 0 || near == null)
            {
                return;
            }
            Frame = (int)(time * FramesASecond) % nearFrames.Length;
            near.sprite = nearFrames[Frame];
            far.sprite = farFrames[Frame % farFrames.Length];
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }
    }
}
