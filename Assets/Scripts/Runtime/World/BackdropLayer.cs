using System.Collections.Generic;
using UnityEngine;

namespace Descender.World
{
    /// <summary>
    /// One painted parallax layer of a region's backdrop (port of
    /// scripts/backdrop_layer.gd): a sprite repeated sideways across the view that
    /// follows the camera by `scroll` of its movement (1 moves with the world, 0 stays
    /// on screen), anchored so it sits at its own start position when the camera is
    /// centred there. A `cover` layer is stretched over the whole view instead (a
    /// sky); a `front` layer draws over the level (haze, wisps). `drift` moves the
    /// layer by itself in level pixels a second, `sway` rocks it sideways by an
    /// amplitude over a period. The SpriteRenderer on this object is the first copy
    /// (what the editor shows); the others are made as the view needs them.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BackdropLayer : MonoBehaviour
    {
        public const int BackOrder = -1000;
        public const int FrontOrder = 500;

        [Tooltip("The share of the camera's movement the layer follows, per axis.")]
        [SerializeField]
        private Vector2 scroll = Vector2.one;

        [Tooltip("Stretched over the whole view (a sky) instead of repeated.")]
        [SerializeField]
        private bool cover;

        [Tooltip("Drawn in front of the level rather than behind it.")]
        [SerializeField]
        private bool front;

        [Tooltip("Level pixels a second the layer moves by itself.")]
        [SerializeField]
        private Vector2 drift;

        [Tooltip("Sideways sway: amplitude in level pixels, period in seconds.")]
        [SerializeField]
        private Vector2 sway;

        [Tooltip("Tiled up and down too, for a region taller than its art.")]
        [SerializeField]
        private bool repeatY;

        private readonly List<SpriteRenderer> copies = new List<SpriteRenderer>(); // the extra copies, children
        private SpriteRenderer first;
        private Vector3 anchor;
        private Vector3 restScale;
        private float time;

        public bool Front => front;
        public bool Cover => cover;

        private void Awake()
        {
            first = GetComponent<SpriteRenderer>();
            anchor = transform.position;
            restScale = transform.localScale;
        }

        private void LateUpdate()
        {
            var camera = CameraRig.GameCamera.Main;
            if (camera == null || first.sprite == null)
            {
                return;
            }
            time += Time.deltaTime;
            var at = camera.transform.position;
            var half = camera.HalfExtents;
            var size = first.sprite.bounds.size;
            if (cover)
            {
                transform.position = new Vector3(at.x, at.y, transform.position.z);
                transform.localScale = new Vector3(
                    half.x * 2f / size.x * restScale.x,
                    half.y * 2f / size.y * restScale.y,
                    restScale.z
                );
                return;
            }
            // The layer drifts by the share of the camera's travel it does not
            // follow, measured from its anchor, plus its own motion.
            var origin = anchor + Vector3.Scale(at - anchor, new Vector3(1f - scroll.x, 1f - scroll.y, 0f));
            origin += (Vector3)Motion();
            var left = at.x - half.x;
            var right = at.x + half.x;
            var firstColumn = Mathf.FloorToInt((left - (origin.x - size.x / 2f)) / size.x);
            var lastColumn = Mathf.CeilToInt((right - (origin.x - size.x / 2f)) / size.x);
            var firstRow = 0;
            var lastRow = 0;
            if (repeatY)
            {
                var below = at.y - half.y;
                var above = at.y + half.y;
                firstRow = Mathf.FloorToInt((below - (origin.y - size.y / 2f)) / size.y);
                lastRow = Mathf.CeilToInt((above - (origin.y - size.y / 2f)) / size.y);
            }
            transform.position = new Vector3(origin.x, origin.y, transform.position.z);
            var used = 0;
            first.enabled = false;
            for (var row = firstRow; row <= lastRow; row++)
            {
                for (var column = firstColumn; column <= lastColumn; column++)
                {
                    if (column == 0 && row == 0)
                    {
                        // The first copy is this object's own renderer, at the origin.
                        first.enabled = true;
                        continue;
                    }
                    var copy = Copy(used++);
                    copy.transform.localPosition = new Vector3(column * size.x, row * size.y, 0f);
                    copy.enabled = true;
                }
            }
            for (var i = used; i < copies.Count; i++)
            {
                copies[i].enabled = false;
            }
        }

        private Vector2 Motion()
        {
            var ppu = Player.PlayerTuning.PixelsPerUnit;
            var motion = drift / ppu * time;
            if (sway.x != 0f && sway.y > 0f)
            {
                motion.x += sway.x / ppu * Mathf.Sin(time / sway.y * Mathf.PI * 2f);
            }
            return motion;
        }

        private SpriteRenderer Copy(int index)
        {
            while (copies.Count <= index)
            {
                var child = new GameObject($"Copy {copies.Count + 1}");
                child.transform.SetParent(transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = first.sprite;
                renderer.sharedMaterial = first.sharedMaterial;
                renderer.color = first.color;
                renderer.sortingLayerID = first.sortingLayerID;
                renderer.sortingOrder = first.sortingOrder;
                renderer.flipX = first.flipX;
                copies.Add(renderer);
            }
            return copies[index];
        }
    }
}
