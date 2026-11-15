using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Birds over a Tiles V2 world's Paradise: now and then a flock crosses the view far off,
    /// white, in a wedge, behind everything that is built. They fly only while the view is
    /// in the sky, over a row of the world the builder gives.
    /// </summary>
    public sealed class TilesV2Birds : MonoBehaviour
    {
        /// <summary>The most birds in a flock.</summary>
        public const int Most = 9;

        // In front of the backdrop's pictures, behind the walls.
        private const int Order = -961;
        private const float FlapEvery = 0.22f;
        private const float OffView = 26f;

        private readonly SpriteRenderer[] birds = new SpriteRenderer[Most];
        private Sprite up;
        private Sprite down;
        private float floor;
        private float wait = 4f;
        private float time;
        private int flying;
        private Vector2 lead;
        private float speed;

        /// <summary>How many birds are in the air.</summary>
        public int Flying => flying;

        /// <summary>Makes the birds; they fly while the view is over `skyFloor` (a height in the world).</summary>
        public static TilesV2Birds Build(
            Transform parent,
            Sprite wingsUp,
            Sprite wingsDown,
            Material material,
            float skyFloor
        )
        {
            if (wingsUp == null || wingsDown == null)
            {
                return null;
            }
            var holder = new GameObject("Birds");
            holder.transform.SetParent(parent, false);
            var flock = holder.AddComponent<TilesV2Birds>();
            flock.up = wingsUp;
            flock.down = wingsDown;
            flock.floor = skyFloor;
            for (var i = 0; i < Most; i++)
            {
                var bird = new GameObject("Bird");
                bird.transform.SetParent(holder.transform, false);
                var shown = bird.AddComponent<SpriteRenderer>();
                shown.sprite = wingsUp;
                shown.sortingOrder = Order;
                shown.enabled = false;
                if (material != null)
                {
                    shown.sharedMaterial = material;
                }
                flock.birds[i] = shown;
            }
            return flock;
        }

        /// <summary>Sends a flock across a view from one side (1: from the left).</summary>
        public void Release(Vector2 view, int count, int from, float height, float unitsASecond)
        {
            flying = Mathf.Clamp(count, 1, Most);
            speed = Mathf.Abs(unitsASecond) * (from >= 0 ? 1f : -1f);
            lead = new Vector2(view.x - Mathf.Sign(speed) * OffView, height);
            for (var i = 0; i < Most; i++)
            {
                birds[i].enabled = i < flying;
            }
            Place();
        }

        /// <summary>Moves the flock on by a time, for a view at a place; lets a new one go when it is due.</summary>
        public void Step(Vector2 view, float seconds)
        {
            time += seconds;
            if (flying > 0)
            {
                lead.x += speed * seconds;
                Place();
                if (Mathf.Abs(lead.x - view.x) > OffView + Most * 1.5f)
                {
                    flying = 0;
                    foreach (var bird in birds)
                    {
                        bird.enabled = false;
                    }
                    wait = Random.Range(7f, 18f);
                }
                return;
            }
            if (view.y < floor)
            {
                return;
            }
            wait -= seconds;
            if (wait <= 0f)
            {
                Release(
                    view,
                    Random.Range(4, Most + 1),
                    Random.value < 0.5f ? 1 : -1,
                    view.y + Random.Range(-3f, 9f),
                    Random.Range(3.5f, 6f)
                );
            }
        }

        // A wedge behind the lead bird, each beating its wings on its own beat.
        private void Place()
        {
            for (var i = 0; i < flying; i++)
            {
                var rank = (i + 1) / 2;
                var side = i % 2 == 0 ? 1f : -1f;
                var back = -Mathf.Sign(speed) * rank * 1.3f;
                birds[i].transform.position = new Vector3(lead.x + back, lead.y + side * rank * 0.7f, 0f);
                birds[i].sprite = (int)((time + i * 0.07f) / FlapEvery) % 2 == 0 ? up : down;
            }
        }

        private void LateUpdate()
        {
            var view = Camera.main;
            if (view != null)
            {
                Step(view.transform.position, Time.deltaTime);
            }
        }
    }
}
