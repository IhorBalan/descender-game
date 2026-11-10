using UnityEngine;

namespace Descender.Game
{
    /// <summary>
    /// "Play from here" (Descender &gt; Play from here, in the editor): the editor leaves a
    /// world point in EditorPrefs and enters play mode on Main.unity; GameFlow takes
    /// it on start and drops the hero there in a throwaway game instead of showing
    /// the title, with saves kept out of the player's own slots. Only in the editor:
    /// a build always boots to the title.
    /// </summary>
    public static class DevStart
    {
        public const string PointKey = "Descender.PlayFromHere.Point";
        public const string AbilitiesKey = "Descender.PlayFromHere.AllAbilities";
        public const string LabKey = "Descender.Lab.Station";

        /// <summary>The station value that means "the first one".</summary>
        public const string FirstStation = "first";

        /// <summary>Takes the pending point, if any, so it is used once.</summary>
        public static bool TryTake(out Vector2 point, out bool allAbilities)
        {
            point = Vector2.zero;
            allAbilities = false;
#if UNITY_EDITOR
            var text = UnityEditor.EditorPrefs.GetString(PointKey, "");
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            UnityEditor.EditorPrefs.DeleteKey(PointKey);
            allAbilities = UnityEditor.EditorPrefs.GetBool(AbilitiesKey, true);
            var parts = text.Split(',');
            if (
                parts.Length != 2
                || !float.TryParse(
                    parts[0],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var x
                )
                || !float.TryParse(
                    parts[1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var y
                )
            )
            {
                return false;
            }
            point = new Vector2(x, y);
            return true;
#else
            return false;
#endif
        }

        /// <summary>Takes the pending lab station (Descender &gt; Lab &gt; Play), if any, so it is used once.</summary>
        public static bool TryTakeLab(out string station)
        {
            station = "";
#if UNITY_EDITOR
            station = UnityEditor.EditorPrefs.GetString(LabKey, "");
            if (string.IsNullOrEmpty(station))
            {
                return false;
            }
            UnityEditor.EditorPrefs.DeleteKey(LabKey);
            return true;
#else
            return false;
#endif
        }

        /// <summary>Leaves a lab station for the next play mode; empty means the first.</summary>
        public static void LeaveLab(string station)
        {
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetString(LabKey, string.IsNullOrEmpty(station) ? FirstStation : station);
#endif
        }

        /// <summary>Leaves a point for the next play mode (called by the editor command).</summary>
        public static void Leave(Vector2 point, bool allAbilities)
        {
#if UNITY_EDITOR
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            UnityEditor.EditorPrefs.SetString(
                PointKey,
                point.x.ToString("R", culture) + "," + point.y.ToString("R", culture)
            );
            UnityEditor.EditorPrefs.SetBool(AbilitiesKey, allAbilities);
#endif
        }
    }
}
