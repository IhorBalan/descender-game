using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// A picture of light stood in a Tiles V2 world (a rainbow, a glory, the light of a
    /// hall's windows in its air, the windows of a castle) that is not always the same: it
    /// breathes slowly; one that the sun makes shows by day and is gone by night; and one
    /// that is the night's (a lit window's own light) comes as the day goes.
    /// </summary>
    public sealed class TilesV2Shimmer : MonoBehaviour
    {
        // The daylight under which the sun's picture is gone, and over which it is whole: its green,
        // which is low at night and at dusk (the night's light is blue, the dusk's red).
        private const float Gone = 0.45f;
        private const float Whole = 0.9f;

        private SpriteRenderer shown;
        private TilesV2World world;
        private bool sunMade;
        private bool nightMade;
        private float breath;
        private float time;

        /// <summary>How much of the picture shows, 0 to 1.</summary>
        public float Strength { get; private set; } = 1f;

        /// <summary>
        /// Sets whose day it follows, whether the sun makes it or (`ofTheNight`) the dark
        /// shows it, and how deep it breathes (0: not at all).
        /// </summary>
        public void Configure(
            TilesV2World tiles,
            SpriteRenderer picture,
            bool ofTheSun,
            float depth,
            bool ofTheNight = false
        )
        {
            world = tiles;
            shown = picture;
            sunMade = ofTheSun;
            nightMade = ofTheNight && !ofTheSun;
            breath = Mathf.Clamp01(depth);
            time = Mathf.Repeat(transform.position.x * 0.17f, 1f) * 10f;
            Apply(world != null && world.Day != null ? world.Day.Hour : 12f);
        }

        /// <summary>Shows the picture as it is at an hour of the day.</summary>
        public void Apply(float hour)
        {
            var sun = 1f;
            if ((sunMade || nightMade) && world != null && world.Set != null)
            {
                sun = Mathf.InverseLerp(Gone, Whole, world.Set.DayAt(hour).light.g);
                sun = nightMade ? 1f - sun : sun;
            }
            Strength = sun * (1f - breath * (0.5f + 0.5f * Mathf.Sin(time * 0.7f)));
            if (shown != null)
            {
                shown.color = new Color(1f, 1f, 1f, Strength);
            }
        }

        private void Update()
        {
            time += Time.deltaTime;
            Apply(world != null && world.Day != null ? world.Day.Hour : 12f);
        }
    }
}
