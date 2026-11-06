using System;
using System.Collections;
using Descender.Fx;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// How an enemy's body goes: it plays its death strip (or, without one, topples
    /// over), lies there as a corpse for a moment, then fades out, and `done` runs so
    /// the enemy can free itself. The caller has already stopped the enemy and turned
    /// its collision off. Port of scripts/enemies/enemy_death.gd.
    /// </summary>
    public static class EnemyDeath
    {
        public const float ToppleTime = 0.5f;
        public const float FadeTime = 0.4f;

        public static IEnumerator Play(SpriteAnimator sprite, int fallDir, float corpseTime, Action done)
        {
            if (sprite == null)
            {
                done?.Invoke();
                yield break;
            }
            if (sprite.Has("death"))
            {
                sprite.Restart("death");
                yield return Tween.Wait(sprite.Length("death") + corpseTime);
            }
            else
            {
                sprite.Pose("hurt", 0);
                yield return Tween.Topple(
                    sprite.transform,
                    -fallDir * 90f,
                    new Vector3(fallDir * 6f / 16f, 0f, 0f),
                    ToppleTime
                );
                yield return Tween.Wait(corpseTime);
            }
            yield return Tween.Fade(sprite.Renderer, 0f, FadeTime);
            done?.Invoke();
        }
    }
}
