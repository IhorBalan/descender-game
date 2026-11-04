using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.World.Paradise
{
    /// <summary>
    /// The faint light Ansel carries: he is half-made, an angel's light half in him
    /// (docs/story/script.md). In the dark of the Orchard it keeps him readable against
    /// the play layer and lights the ground he runs on. Sits on the camera of
    /// ParadiseMain.unity and gives the current hero a soft point light; the shared
    /// Player prefab is not changed.
    /// </summary>
    public sealed class HeroAura : MonoBehaviour
    {
        [SerializeField]
        private Color color = new Color(1f, 0.93f, 0.8f);

        [SerializeField, Min(0f)]
        private float intensity = 0.55f;

        [Tooltip("Reach in units.")]
        [SerializeField, Min(0.5f)]
        private float radius = 5f;

        private Player.PlayerController lit;

        private void LateUpdate()
        {
            var hero = Player.PlayerController.Current;
            if (hero == null || hero == lit)
            {
                return;
            }
            lit = hero;
            if (hero.transform.Find("Aura") != null)
            {
                return;
            }
            var go = new GameObject("Aura");
            go.transform.SetParent(hero.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.pointLightInnerRadius = 0f;
            light.pointLightOuterRadius = radius;
            light.falloffIntensity = 0.6f;
            light.shadowsEnabled = false;
        }
    }
}
