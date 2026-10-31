using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// A wall of angel light: a column of cells solid for everyone except the hero
    /// while he is light dashing, which the hero does by ignoring the LightBarrier
    /// layer for the dash (port of scripts/light_barrier.gd). The cells run down from
    /// this object's position, one per `height`; each gets a collider and a sprite
    /// that shimmers.
    /// </summary>
    public sealed class LightBarrier : MonoBehaviour
    {
        public const float ShimmerSpeed = 4f;
        public const float ShimmerMin = 0.7f;

        [SerializeField, Min(1)]
        private int height = 1;

        [SerializeField]
        private Sprite sprite;

        [SerializeField]
        private Material material;

        private SpriteRenderer[] renderers;
        private float time;

        public int Height => height;

        private void Awake()
        {
            time = Random.Range(0f, Mathf.PI * 2f);
            gameObject.layer = LayerMask.NameToLayer("LightBarrier");
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            renderers = new SpriteRenderer[height];
            for (var i = 0; i < height; i++)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.size = Vector2.one;
                box.offset = new Vector2(0.5f, -i - 0.5f);
                var cell = new GameObject($"Cell {i}") { layer = gameObject.layer };
                cell.transform.SetParent(transform, false);
                cell.transform.localPosition = new Vector3(0.5f, -i - 0.5f, 0f);
                var renderer = cell.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 20;
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                }
                renderers[i] = renderer;
            }
        }

        private void Update()
        {
            time += Time.deltaTime;
            var glow = Mathf.Lerp(ShimmerMin, 1f, (Mathf.Sin(time * ShimmerSpeed) + 1f) / 2f);
            var color = new Color(1f, 1f, 1f, glow);
            foreach (var renderer in renderers)
            {
                renderer.color = color;
            }
        }
    }
}
