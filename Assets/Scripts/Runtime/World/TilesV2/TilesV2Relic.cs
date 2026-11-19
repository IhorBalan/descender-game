using System;
using Descender.Pickups;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// A relic of a Tiles V2 world: a small gold star that bobs and glints where a dead end
    /// or a hard climb ends, taken by walking into it. The gate at the world's end is one
    /// too, larger and brighter, and is not taken away. Origin at the foot of its cell.
    /// </summary>
    public sealed class TilesV2Relic : MonoBehaviour
    {
        private const float Lift = 0.6f;
        private const float BobHeight = 0.12f;
        private const float BobSpeed = 2.4f;
        private const float GlintEvery = 0.35f;
        private static readonly Color Glow = new Color(1f, 0.86f, 0.4f);

        private SpriteRenderer shown;
        private Sprite[] frames = Array.Empty<Sprite>();
        private bool stays;
        private float time;

        /// <summary>Whether the hero has found it.</summary>
        public bool IsFound { get; private set; }

        /// <summary>The hero walked into it.</summary>
        public event Action Found;

        /// <summary>Sets its two frames and what it draws with; `last` makes it the gate at the world's end.</summary>
        public void Configure(Sprite first, Sprite second, Material material, bool last)
        {
            stays = last;
            frames = new[] { first, second };
            Pickup.Box(gameObject, new Vector2(16f, 24f), new Vector2(0f, 12f));
            var picture = new GameObject("Picture");
            picture.transform.SetParent(transform, false);
            picture.transform.localScale = Vector3.one * (last ? 2f : 1f);
            shown = picture.AddComponent<SpriteRenderer>();
            shown.sprite = first;
            shown.sortingOrder = 5;
            if (material != null)
            {
                shown.sharedMaterial = material;
            }
            var light = new GameObject("Glow").AddComponent<Light2D>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = new Vector3(0f, Lift, 0f);
            light.lightType = Light2D.LightType.Point;
            light.blendStyleIndex = 0;
            light.color = Glow;
            light.intensity = last ? 0.9f : 0.5f;
            light.pointLightInnerRadius = 0f;
            light.pointLightOuterRadius = last ? 6f : 3f;
            light.falloffIntensity = 0.5f;
            light.shadowsEnabled = false;
            ReliefLight.Raise(light);
            time = Mathf.Repeat(transform.position.x * 0.37f, 1f) * 6f;
            Bob();
        }

        private void Update()
        {
            time += Time.deltaTime;
            Bob();
        }

        private void Bob()
        {
            if (shown == null)
            {
                return;
            }
            var lift = Lift * (stays ? 2f : 1f) + Mathf.Sin(time * BobSpeed) * BobHeight;
            shown.transform.localPosition = new Vector3(0f, lift, 0f);
            if (frames.Length > 0)
            {
                shown.sprite = frames[(int)(time / GlintEvery) % frames.Length];
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsFound || Pickup.HeroFrom(other) == null)
            {
                return;
            }
            IsFound = true;
            Audio.Sfx.Play("ability");
            Found?.Invoke();
            if (!stays)
            {
                Destroy(gameObject);
            }
        }
    }
}
