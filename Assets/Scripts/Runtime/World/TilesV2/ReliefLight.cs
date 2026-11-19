using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Makes a lamp of Tiles V2 light the relief of what it shines on: a picture drawn in
    /// the second style carries a normal map (its <c>_NormalMap</c> secondary texture,
    /// docs/art/tiles-v2.md), and a light that reads it lights the faces turned toward it
    /// and leaves those turned away. URP lets a light's normal map quality and distance be
    /// set only in the inspector, so this sets their serialized fields.
    /// </summary>
    public static class ReliefLight
    {
        /// <summary>How far in front of the pictures a lamp hangs, in world units: lower is more raking.</summary>
        public const float Distance = 1.2f;

        private static readonly FieldInfo Quality = typeof(Light2D).GetField(
            "m_NormalMapQuality",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        private static readonly FieldInfo Height = typeof(Light2D).GetField(
            "m_NormalMapDistance",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        /// <summary>Whether this version of URP has the fields to set; without them a lamp stays flat.</summary>
        public static bool Supported => Quality != null && Height != null;

        /// <summary>Lights the relief of what the light shines on, from `distance` in front of it.</summary>
        public static void Raise(Light2D light, float distance = Distance)
        {
            if (light == null || !Supported)
            {
                return;
            }
            Quality.SetValue(light, Light2D.NormalMapQuality.Accurate);
            Height.SetValue(light, distance);
        }
    }
}
