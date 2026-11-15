using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// The Tiles V2 clock: the hour of the day, running while the game plays (a whole day
    /// takes the tile set's `dayLength` seconds), and what the hour does, from the set's
    /// day keys: the colour of the daylight goes to the tile light, the sky's colours, the
    /// stars, the sun or the moon and the far backdrop's tint go to the Sky. In the editor
    /// it shows its hour and stands still.
    /// </summary>
    [ExecuteAlways]
    public sealed class DayNight : MonoBehaviour
    {
        // A change of daylight smaller than this (per channel) is not worth a new light texture.
        private const float Step = 1f / 400f;

        [SerializeField]
        private TilesV2Set set;

        [SerializeField]
        private TileLightMap tileLight;

        [SerializeField]
        private Sky sky;

        [SerializeField, Range(0f, 24f)]
        private float hour = 10f;

        [SerializeField, Tooltip("Whether the clock runs while the game plays.")]
        private bool running = true;

        [SerializeField, Min(0f), Tooltip("How many times faster than the set's day the clock runs.")]
        private float speed = 1f;

        /// <summary>The hour of the day, 0 to 24; setting it shows that hour at once.</summary>
        public float Hour
        {
            get => hour;
            set
            {
                hour = Mathf.Repeat(value, 24f);
                Apply();
            }
        }

        public bool Running
        {
            get => running;
            set => running = value;
        }

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(value, 0f);
        }

        private void OnEnable() => Apply();

        private void Update()
        {
            if (!Application.isPlaying || !running || set == null || set.dayLength <= 0f)
            {
                return;
            }
            hour = Mathf.Repeat(hour + Time.deltaTime * speed * 24f / set.dayLength, 24f);
            Apply();
        }

        /// <summary>Sets the clock up (the world builder's one entry point).</summary>
        public void Configure(TilesV2Set tileSet, TileLightMap lightMap, Sky skyAbove, float startHour, bool runs)
        {
            set = tileSet;
            tileLight = lightMap;
            sky = skyAbove;
            hour = Mathf.Repeat(startHour, 24f);
            running = runs;
            Apply();
        }

        /// <summary>Shows the hour: the daylight on the tiles and the sky above them.</summary>
        public void Apply()
        {
            if (set == null)
            {
                return;
            }
            var day = set.DayAt(hour);
            if (tileLight != null)
            {
                var shown = tileLight.Daylight;
                if (
                    Mathf.Abs(shown.r - day.light.r) >= Step
                    || Mathf.Abs(shown.g - day.light.g) >= Step
                    || Mathf.Abs(shown.b - day.light.b) >= Step
                )
                {
                    tileLight.Daylight = new Color(day.light.r, day.light.g, day.light.b, 1f);
                }
            }
            if (sky != null)
            {
                var along = set.SkyArc(hour, out var sunUp);
                sky.Show(day, along, sunUp);
            }
            Applied?.Invoke(hour);
        }

        /// <summary>Called with the hour each time it is shown (TilesV2Life shows the lights of the night by it).</summary>
        public event System.Action<float> Applied;
    }
}
