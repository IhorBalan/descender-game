using TMPro;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// Text over an enemy's head for a while: the ambush's "!" or a bark. A world-space
    /// TextMeshPro made in code with the project's default TMP font; without one (TMP
    /// essentials not imported) the mark keeps its text for the tests and the HUD's
    /// Marked event but draws nothing.
    /// </summary>
    public sealed class EnemyMark : MonoBehaviour
    {
        private TextMeshPro label;
        private float timer;

        public string Text { get; private set; } = "";
        public bool Showing => timer > 0f;

        public static EnemyMark Create(Transform parent, Vector2 offsetPixels)
        {
            var go = new GameObject("Mark");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offsetPixels / 16f;
            var mark = go.AddComponent<EnemyMark>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                var label = go.AddComponent<TextMeshPro>();
                label.font = TMP_Settings.defaultFontAsset;
                label.fontSize = 1.5f;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.sortingOrder = 30;
                var rect = label.rectTransform;
                rect.sizeDelta = new Vector2(8f, 1f);
                mark.label = label;
            }
            go.SetActive(false);
            return mark;
        }

        private void Update()
        {
            if (timer > 0f)
            {
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    Hide();
                }
            }
        }

        public void Show(string text, Color color, float duration)
        {
            Text = text ?? "";
            timer = duration;
            if (label != null)
            {
                label.text = Text;
                label.color = color;
            }
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            timer = 0f;
            Text = "";
            gameObject.SetActive(false);
        }
    }
}
