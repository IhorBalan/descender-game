using System.Collections;
using UnityEngine;

namespace Descender.Fx
{
    /// <summary>
    /// Whitens a character's sprite for a moment when it is hit by swapping the
    /// renderers' shared material for Assets/Settings/Materials/Sprite Flash.mat
    /// (set on the prefab; found by shader name when missing) and back. Respects the
    /// hit flash setting. Runs on real time so a hit-stop does not stretch it.
    /// </summary>
    public sealed class HitFlash : MonoBehaviour
    {
        public const string ShaderName = "Descender/Sprite Flash";

        [SerializeField]
        private SpriteRenderer[] renderers;

        [SerializeField]
        private Material flashMaterial;

        private Material[] originals;
        private Coroutine running;
        private static Material fallback;

        public bool IsFlashing => running != null;

        private void Awake()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
            }
            originals = new Material[renderers.Length];
            if (flashMaterial == null)
            {
                flashMaterial = Fallback();
            }
        }

        private void OnDisable()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
                Restore();
            }
        }

        /// <summary>Flashes for a time in real seconds; a new flash restarts the clock.</summary>
        public void Flash(float seconds = 0.1f)
        {
            if (!isActiveAndEnabled || flashMaterial == null || !Save.SettingsData.Current.hitFlash)
            {
                return;
            }
            if (running != null)
            {
                StopCoroutine(running);
            }
            else
            {
                for (var i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                    {
                        originals[i] = renderers[i].sharedMaterial;
                        renderers[i].sharedMaterial = flashMaterial;
                    }
                }
            }
            running = StartCoroutine(Run(seconds));
        }

        private IEnumerator Run(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            running = null;
            Restore();
        }

        private void Restore()
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && originals[i] != null)
                {
                    renderers[i].sharedMaterial = originals[i];
                }
            }
        }

        private static Material Fallback()
        {
            if (fallback == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader != null)
                {
                    fallback = new Material(shader) { name = "Sprite Flash (runtime)" };
                }
            }
            return fallback;
        }
    }
}
