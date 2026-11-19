using System;
using Descender.Pickups;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// One of the lights of a Tiles V2 world's Paradise: a bowl of gold on a stem, dark until
    /// the hero walks up to it, then burning for good. The doors that count lights are the
    /// world's things' to open (TilesV2Things). Origin at the foot of its cell.
    /// </summary>
    public sealed class TilesV2Beacon : MonoBehaviour
    {
        private const float FlickerEvery = 0.16f;
        private const float Reach = 7f;
        private static readonly Color Glow = new Color(1f, 0.93f, 0.7f);

        private SpriteRenderer shown;
        private Sprite[] burning = Array.Empty<Sprite>();
        private Light2D glow;
        private float time;

        /// <summary>Whether it burns.</summary>
        public bool IsLit { get; private set; }

        /// <summary>The hero lit it.</summary>
        public event Action Lit;

        /// <summary>Sets its picture dark, the two frames of its flame and what it draws with.</summary>
        public void Configure(Sprite dark, Sprite first, Sprite second, Material material)
        {
            burning = new[] { first, second };
            Pickup.Box(gameObject, new Vector2(20f, 28f), new Vector2(0f, 14f));
            var picture = new GameObject("Picture");
            picture.transform.SetParent(transform, false);
            picture.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            shown = picture.AddComponent<SpriteRenderer>();
            shown.sprite = dark;
            shown.sortingOrder = TilesV2World.TorchOrder;
            if (material != null)
            {
                shown.sharedMaterial = material;
            }
            glow = new GameObject("Glow").AddComponent<Light2D>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            glow.lightType = Light2D.LightType.Point;
            glow.blendStyleIndex = 0;
            glow.color = Glow;
            glow.intensity = 0f;
            glow.pointLightInnerRadius = 1f;
            glow.pointLightOuterRadius = Reach;
            glow.falloffIntensity = 0.5f;
            glow.shadowsEnabled = false;
            ReliefLight.Raise(glow);
        }

        /// <summary>Lights it, once.</summary>
        public void Light()
        {
            if (IsLit)
            {
                return;
            }
            IsLit = true;
            if (glow != null)
            {
                glow.intensity = 1.1f;
            }
            if (shown != null && burning.Length > 0)
            {
                shown.sprite = burning[0];
            }
            Audio.Sfx.Play("ability");
            Lit?.Invoke();
        }

        private void Update()
        {
            if (!IsLit || shown == null || burning.Length == 0)
            {
                return;
            }
            time += Time.deltaTime;
            shown.sprite = burning[(int)(time / FlickerEvery) % burning.Length];
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsLit && Pickup.HeroFrom(other) != null)
            {
                Light();
            }
        }
    }
}
