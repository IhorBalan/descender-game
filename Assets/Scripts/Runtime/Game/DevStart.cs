using UnityEngine;

namespace Descender.Game
{
    /// <summary>
    /// "Play from here" (Descender &gt; Play from here, in the editor): the editor leaves a
    /// world point in EditorPrefs and enters play mode on Main.unity; GameFlow takes
    /// it on start and drops the hero there in a throwaway game instead of showing
    /// the title, with saves kept out of the player's own slots. Only in the editor;
    /// a build boots to the title, or to a lab station when started with
    /// <c>-lab &lt;station&gt;</c> on its command line (<c>Descender.exe -lab fruit</c>).
    /// </summary>
    public static class DevStart
    {
        public const string PointKey = "Descender.PlayFromHere.Point";
        public const string AbilitiesKey = "Descender.PlayFromHere.AllAbilities";
        public const string LabKey = "Descender.Lab.Station";

        /// <summary>The station value that means "the first one".</summary>
        public const string FirstStation = "first";

        /// <summary>The command-line option that opens a build in the lab.</summary>
        public const string LabArgument = "-lab";

#if !UNITY_EDITOR
        private static bool argumentTaken;
#endif

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
            // Once only, so leaving the lab for the title does not open it again.
            if (argumentTaken)
            {
                return false;
            }
            argumentTaken = true;
            return TryLabFromArguments(System.Environment.GetCommandLineArgs(), out station);
#endif
        }

        /// <summary>
        /// The station a command line asks for with <c>-lab [station]</c>: the word after
        /// it, or the first station when nothing (or another option) follows.
        /// </summary>
        public static bool TryLabFromArguments(string[] args, out string station)
        {
            station = "";
            if (args == null)
            {
                return false;
            }
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], LabArgument, System.StringComparison.OrdinalIgnoreCase))
                {
                    var next = i + 1 < args.Length ? args[i + 1] : "";
                    station = next.Length > 0 && !next.StartsWith("-") ? next.ToLowerInvariant() : FirstStation;
                    return true;
                }
            }
            return false;
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
