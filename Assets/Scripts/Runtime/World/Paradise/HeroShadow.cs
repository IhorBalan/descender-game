using Descender.Combat;
using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// A soft contact shadow under Ansel's feet, on the ground beneath him: full and dark when
    /// he stands on it, smaller and fainter the higher he rises, gone past
    /// <see cref="reach"/>. Lies along a slope. The blob is taken out of the rig when he
    /// wakes, so the rig's mirroring and his fall when he dies do not squeeze or tip it.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class HeroShadow : MonoBehaviour
    {
        [SerializeField]
        private SpriteRenderer blob;

        [SerializeField, Min(0.1f)]
        private float width = 1.05f;

        [SerializeField, Min(0.02f)]
        private float height = 0.2f;

        [SerializeField, Range(0f, 1f)]
        private float darkness = 0.5f;

        [Tooltip("How high above the ground he can be and still cast it, in units.")]
        [SerializeField, Min(0.5f)]
        private float reach = 3f;

        public SpriteRenderer Blob => blob;

        public void Configure(SpriteRenderer shadow) => blob = shadow;

        private void Awake()
        {
            if (blob != null)
            {
                blob.transform.SetParent(null, true);
                blob.enabled = false;
            }
        }

        private void OnDisable()
        {
            if (blob != null)
            {
                blob.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (blob != null)
            {
                Destroy(blob.gameObject);
            }
        }

        private void LateUpdate()
        {
            if (blob == null)
            {
                return;
            }
            var feet = (Vector2)transform.position;
            var hit = Physics2D.Raycast(feet + Vector2.up * 0.1f, Vector2.down, reach + 0.1f, Layers.GroundMask);
            if (hit.collider == null || hit.normal.y < 0.5f)
            {
                blob.enabled = false;
                return;
            }
            var near = 1f - Mathf.Clamp01((feet.y - hit.point.y) / reach);
            blob.enabled = near > 0.02f;
            var size = Mathf.Lerp(0.45f, 1f, near);
            blob.transform.SetPositionAndRotation(
                new Vector3(feet.x, hit.point.y + 0.01f, 0f),
                Quaternion.Euler(0f, 0f, -Mathf.Atan2(hit.normal.x, hit.normal.y) * Mathf.Rad2Deg)
            );
            blob.transform.localScale = new Vector3(width * size, height * size, 1f);
            blob.color = new Color(0f, 0f, 0f, darkness * near * near);
        }
    }
}
