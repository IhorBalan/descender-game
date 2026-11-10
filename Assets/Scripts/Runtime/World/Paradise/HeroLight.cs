using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.World.Paradise
{
    /// <summary>
    /// The light in Ansel, and the line around him that keeps him easy to see.
    /// <list type="bullet">
    /// <item>His outline: a copy of his body (RigCopy) under the rig, just behind every part of
    /// him, drawn with Descender/Hero Silhouette: a dark ink edge and a halo of light. It
    /// takes his pose every frame before his skins are bent (2D Animation bends them at
    /// order 10, after HeroRig has posed the bones), so it never trails him.</item>
    /// <item>His cloak (Descender/Hero Cloak Light): how much light runs in it (energy), how
    /// fast its threads flow, and the surges that run from the collar to the hem: one on
    /// every cut, dash, slam and landing, and a soft one every few seconds, like breath.
    /// A hit dims it and makes it falter; healing swells it.</item>
    /// <item>A light at the hem that throws the cloak's glow on what is around him, and motes
    /// of light shed from the hem, more as he runs and a stream as he dashes.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(5)]
    public sealed class HeroLight : MonoBehaviour
    {
        private static readonly int EnergyId = Shader.PropertyToID("_Energy");
        private static readonly int FlowId = Shader.PropertyToID("_Flow");
        private static readonly int SurgeId = Shader.PropertyToID("_Surge");
        private static readonly int SurgePowerId = Shader.PropertyToID("_SurgePower");
        private static readonly int TexelId = Shader.PropertyToID("_Texel");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");

        /// <summary>The most light he holds.</summary>
        public const float MaxEnergy = 1.9f;

        private static readonly Color Gold = new Color(1f, 0.82f, 0.48f);
        private static readonly Color Pale = new Color(0.66f, 0.9f, 1f);

        [Tooltip("The parts woven with light (the cloak and the hood), on the cloak light material.")]
        [SerializeField]
        private SpriteRenderer[] cloth;

        [SerializeField]
        private Material silhouette;

        [Tooltip("The sorting order of the outline: behind all of him, in front of his shadow.")]
        [SerializeField]
        private int outlineOrder = 10;

        [Tooltip("The light at his hem.")]
        [SerializeField]
        private Light2D hemLight;

        [SerializeField]
        private float hemIntensity = 0.55f;

        [SerializeField]
        private ParticleSystem motes;

        [Tooltip("The tips of the cloak's chains, where the motes are shed.")]
        [SerializeField]
        private Transform[] hem;

        [Tooltip("Seconds between two of the soft surges he breathes.")]
        [SerializeField, Min(0.5f)]
        private float breathEvery = 3.2f;

        private Player.PlayerController hero;
        private HeroRig rig;
        private MaterialPropertyBlock block;
        private Vector4[] clothTexel;
        private RigCopy.Copy outline;
        private Vector4[] outlineTexel;
        private Transform[] live;

        private float energy = 1f;
        private float flare;
        private float dim;
        private float flow;
        private float surgeAt = -1f;
        private float surgePower;
        private float surgeSpeed = 1f;
        private float nextBreath;
        private float moteDebt;
        private int attacks;
        private Player.PlayerState was;
        private bool grounded = true;
        private float fallSpeed;

        /// <summary>How bright the light in him is now: 1 standing, more as he moves and strikes.</summary>
        public float Energy => energy;

        /// <summary>Where a surge is on the cloak: 0 at the collar, 1 at the hem, below 0 none.</summary>
        public float Surge => surgeAt;

        public bool Outlined => outline != null && outline.root.activeSelf;

        public SpriteRenderer[] Cloth => cloth;

        public Material Silhouette => silhouette;

        public Light2D HemLight => hemLight;

        public ParticleSystem Motes => motes;

        public void Configure(
            SpriteRenderer[] wovenWithLight,
            Material outlineMaterial,
            int order,
            Light2D light,
            ParticleSystem shed,
            Transform[] tips
        )
        {
            cloth = wovenWithLight;
            silhouette = outlineMaterial;
            outlineOrder = order;
            hemLight = light;
            motes = shed;
            hem = tips;
        }

        private void LateUpdate()
        {
            if (hero == null)
            {
                hero = GetComponentInParent<Player.PlayerController>();
                rig = GetComponent<HeroRig>();
                if (hero == null)
                {
                    return;
                }
                attacks = hero.AttackCount;
                was = hero.State;
                nextBreath = Time.time + breathEvery;
            }
            var dt = Time.deltaTime;
            var now = Time.time;
            Read(dt, now, out var speed01);
            Outline();
            Weave(speed01);
            Shed(dt, speed01);
            if (dt > 0f)
            {
                flow = (flow + dt * (0.1f + 0.45f * speed01 + (hero.IsDashing ? 1.6f : 0f))) % 1000f;
            }
        }

        private void OnDestroy()
        {
            if (outline != null && outline.root != null)
            {
                Destroy(outline.root);
            }
        }

        /// <summary>Reads what he is doing into the energy and the surges.</summary>
        private void Read(float dt, float now, out float speed01)
        {
            var state = hero.State;
            var runSpeed = Mathf.Max(0.01f, hero.Tuning.runSpeed / Player.PlayerTuning.PixelsPerUnit);
            speed01 = Mathf.Clamp01(Mathf.Abs(hero.Velocity.x) / runSpeed);

            if (hero.AttackCount != attacks)
            {
                attacks = hero.AttackCount;
                flare = Mathf.Max(flare, 0.7f);
                Send(1f, 2.8f);
            }
            if (state != was)
            {
                switch (state)
                {
                    case Player.PlayerState.Dash:
                        flare = Mathf.Max(flare, 0.5f);
                        Send(1.2f, 3.6f);
                        break;
                    case Player.PlayerState.Slam:
                        flare = Mathf.Max(flare, 0.6f);
                        Send(1f, 2.2f);
                        break;
                    case Player.PlayerState.Hurt:
                        dim = 1f;
                        surgeAt = -1f;
                        break;
                    case Player.PlayerState.Heal:
                        Send(0.9f, 1.2f);
                        break;
                }
                was = state;
            }
            var nowGrounded = hero.IsGrounded;
            if (nowGrounded && !grounded && fallSpeed > 4f)
            {
                Send(Mathf.Clamp(fallSpeed / 12f, 0.5f, 1f), 3f);
            }
            fallSpeed = nowGrounded ? 0f : Mathf.Max(0f, -hero.Velocity.y);
            grounded = nowGrounded;

            if (surgeAt < 0f && now >= nextBreath)
            {
                Send(0.45f, 1.3f);
            }
            if (surgeAt >= 0f)
            {
                surgeAt += dt * surgeSpeed;
                if (surgeAt > 1.25f)
                {
                    surgeAt = -1f;
                    nextBreath = now + breathEvery;
                }
            }

            var target = 1f + 0.3f * speed01;
            if (hero.IsDashing)
            {
                target += 0.45f;
            }
            if (state == Player.PlayerState.Heal)
            {
                target += 0.5f + 0.2f * Mathf.Sin(now * 9f);
            }
            if (rig != null && rig.RestKind >= 0)
            {
                // Resting, the light breathes slowly.
                target += 0.12f * Mathf.Sin(now * 1.4f);
            }
            target += flare;
            flare = Mathf.MoveTowards(flare, 0f, dt * 2.2f);
            // Capped, so the brightest moments still show the folds of the cloth.
            energy = Mathf.Min(MaxEnergy, Mathf.Lerp(energy, target, 1f - Mathf.Exp(-dt * 10f)));
            if (dim > 0f)
            {
                // A hit knocks the light out of him; it falters back over half a second.
                var falter = 0.5f + 0.5f * Mathf.Sin(now * 55f);
                energy *= 1f - 0.8f * dim * (0.6f + 0.4f * falter);
                dim = Mathf.MoveTowards(dim, 0f, dt * 2f);
            }
        }

        private void Send(float power, float speed)
        {
            surgeAt = 0f;
            surgePower = power;
            surgeSpeed = speed;
        }

        private void Outline()
        {
            if (silhouette == null)
            {
                return;
            }
            if (outline == null)
            {
                live = GetComponentsInChildren<Transform>(true);
                outline = RigCopy.Make(gameObject, RigCopy.Outline, silhouette, outlineOrder);
                outline.root.transform.SetParent(transform, false);
                outlineTexel = new Vector4[outline.renderers.Length];
                for (var i = 0; i < outline.renderers.Length; i++)
                {
                    outlineTexel[i] = Texel(outline.renderers[i]);
                }
                outline.root.SetActive(true);
            }
            RigCopy.Pose(outline, live);
            // He blinks while he cannot be hurt; his outline blinks with him.
            var alpha = cloth != null && cloth.Length > 0 && cloth[0] != null ? cloth[0].color.a : 1f;
            var glow = Mathf.Clamp(0.55f + 0.45f * energy, 0.3f, 1.6f);
            block ??= new MaterialPropertyBlock();
            for (var i = 0; i < outline.renderers.Length; i++)
            {
                var r = outline.renderers[i];
                r.color = new Color(1f, 1f, 1f, alpha);
                r.GetPropertyBlock(block);
                block.SetVector(TexelId, outlineTexel[i]);
                block.SetFloat(GlowId, glow);
                r.SetPropertyBlock(block);
            }
        }

        private void Weave(float speed01)
        {
            if (cloth == null)
            {
                return;
            }
            block ??= new MaterialPropertyBlock();
            if (clothTexel == null)
            {
                clothTexel = new Vector4[cloth.Length];
                for (var i = 0; i < cloth.Length; i++)
                {
                    clothTexel[i] = cloth[i] != null ? Texel(cloth[i]) : Vector4.zero;
                }
            }
            for (var i = 0; i < cloth.Length; i++)
            {
                var r = cloth[i];
                if (r == null)
                {
                    continue;
                }
                r.GetPropertyBlock(block);
                block.SetFloat(EnergyId, energy);
                block.SetFloat(FlowId, flow);
                block.SetFloat(SurgeId, surgeAt);
                block.SetFloat(SurgePowerId, surgePower);
                block.SetVector(TexelId, clothTexel[i]);
                r.SetPropertyBlock(block);
            }
            if (hemLight != null)
            {
                var surge = surgeAt >= 0f ? surgePower * Mathf.Clamp01(surgeAt * 1.5f) * 0.6f : 0f;
                hemLight.intensity = hemIntensity * (energy + surge);
                if (hem != null && hem.Length > 0)
                {
                    hemLight.transform.position = Middle(hem) + Vector3.up * 0.25f;
                }
            }
        }

        private void Shed(float dt, float speed01)
        {
            if (motes == null || hem == null || hem.Length == 0 || dt <= 0f)
            {
                return;
            }
            var rate = 2.5f + 12f * speed01 + (hero.IsDashing ? 45f : 0f) + 6f * flare;
            if (dim > 0.3f)
            {
                rate = 0f;
            }
            moteDebt += rate * dt;
            var emit = new ParticleSystem.EmitParams();
            var back = -hero.Facing;
            while (moteDebt >= 1f)
            {
                moteDebt -= 1f;
                var tip = hem[Random.Range(0, hem.Length)];
                var t = Random.value;
                emit.startColor = Color.Lerp(Gold, Pale, t) * new Color(1f, 1f, 1f, 0.9f);
                emit.position =
                    tip.position + new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.24f, 0.04f), 0f);
                emit.velocity = new Vector3(
                    back * Random.Range(0.1f, 0.5f) * (0.4f + speed01),
                    Random.Range(0.15f, 0.5f),
                    0f
                );
                emit.startSize = Random.Range(0.03f, 0.075f);
                emit.startLifetime = Random.Range(0.7f, 1.5f);
                motes.Emit(emit, 1);
            }
        }

        private static Vector3 Middle(Transform[] points)
        {
            var sum = Vector3.zero;
            foreach (var p in points)
            {
                sum += p.position;
            }
            return sum / points.Length;
        }

        private static Vector4 Texel(SpriteRenderer r)
        {
            var texture = r.sprite != null ? r.sprite.texture : null;
            if (texture == null)
            {
                return new Vector4(1f / 256f, 1f / 256f, 256f, 256f);
            }
            return new Vector4(1f / texture.width, 1f / texture.height, texture.width, texture.height);
        }
    }
}
