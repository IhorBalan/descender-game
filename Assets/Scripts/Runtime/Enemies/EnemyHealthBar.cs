using Descender.Fx;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// A small bar over an enemy's head that appears when it is hit and hides again
    /// a moment later. Two tinted squares from Shapes, made in code by the enemy that
    /// owns it. Port of scripts/enemy_health_bar.gd.
    /// </summary>
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        public const float Width = 16f;
        public const float Height = 3f;
        public const float ShowTime = 1.5f;
        public static readonly Color Back = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color Fill = new Color(0.96f, 0.3f, 0.3f);
        public static readonly Color FillLow = new Color(1.0f, 0.75f, 0.3f);

        private SpriteRenderer back;
        private SpriteRenderer fill;
        private float timer;

        public int Current { get; private set; } = 1;
        public int Maximum { get; private set; } = 1;
        public bool Showing => timer > 0f;

        /// <summary>Makes a bar under a parent at an offset in level pixels from its origin.</summary>
        public static EnemyHealthBar Create(Transform parent, Vector2 offsetPixels)
        {
            var go = new GameObject("HealthBar");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offsetPixels / 16f;
            var bar = go.AddComponent<EnemyHealthBar>();
            bar.back = Shapes.Block(go.transform, "Back", new Vector2(Width + 2f, Height + 2f), Vector2.zero, Back);
            bar.back.sortingOrder = 20;
            bar.fill = Shapes.Block(go.transform, "Fill", new Vector2(Width, Height), Vector2.zero, Fill);
            bar.fill.sortingOrder = 21;
            go.SetActive(false);
            return bar;
        }

        private void Update()
        {
            if (timer > 0f)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Shows the bar at this health for a while.</summary>
        public void ShowHealth(int value, int maximum)
        {
            Current = Mathf.Clamp(value, 0, Mathf.Max(maximum, 1));
            Maximum = Mathf.Max(maximum, 1);
            timer = ShowTime;
            gameObject.SetActive(true);
            var width = Mathf.Round(Width * Current / (float)Maximum);
            fill.transform.localScale = new Vector3(Mathf.Max(width, 0f) / 4f, Height / 4f, 1f);
            fill.transform.localPosition = new Vector3((width - Width) / 2f / 16f, 0f, 0f);
            fill.color = Current * 2 > Maximum ? Fill : FillLow;
        }

        public void Hide()
        {
            timer = 0f;
            gameObject.SetActive(false);
        }
    }
}
