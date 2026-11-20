using System;
using System.Collections.Generic;
using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// What moves in a Tiles V2 world and what comes and goes with the hour, from the map's
    /// `life` marks (`@mark life column row kind picture dx dy`; docs/art/tiles-v2.md, "Life"):
    /// smoke from a chimney, leaves falling from a tree, fireflies at night, butterflies by
    /// day, the splash where falling water meets a pool, drips from a mine's roof, pollen
    /// that hangs in warm air, snow that comes down slowly where it is cold (all single art
    /// pixels, as every particle is); a mill's sails that turn; lights that show only at
    /// dusk and at night (a lit window, the glow in the Gate, the lights of the far villages);
    /// shafts of low sun at dawn and at sunset; dark shapes close to the eye that pass in
    /// front of everything a little faster than the world; what stands far off behind a
    /// place, and passes slower than the world (`far`); reeds and grass that sway; birds
    /// that cross the sky far off; and now and then rain over the surface. `dx` and `dy` are
    /// art pixels from the foot of the mark's cell.
    /// </summary>
    public sealed class TilesV2Life : MonoBehaviour
    {
        /// <summary>The kind of mark this reads.</summary>
        public const string Kind = "life";

        /// <summary>The backdrop layer of the far villages' lights, shown only at night.</summary>
        public const string WindowsKey = "backdrop_earth_windows";

        /// <summary>How much faster than the world the shapes close to the eye move.</summary>
        public const float FrontScroll = 1.3f;

        /// <summary>How much slower than the world what stands far off behind a place moves.</summary>
        public const float FarScroll = 0.88f;

        /// <summary>Frames a second the sails turn by.</summary>
        public const float SailFrames = 2.5f;

        /// <summary>Frames a second what sways in the wind leans by.</summary>
        public const float SwayFrames = 1.6f;

        /// <summary>Seconds of play between the starts of two showers, and how long one lasts.</summary>
        public const float RainEvery = 420f;

        public const float RainFor = 100f;

        private const int Flock = 5;
        private const float FlockSpan = 120f;
        private const float FlockSpeed = 1.6f;

        private const float Pixel = TilesV2Particles.Pixel;
        private const int FrontOrder = 40;

        // The birds fly far off: behind the ridges' nearest wood, in front of the far hills.
        private const int BirdOrder = -975;
        private const float Drop = 9f;

        // Seconds a system is run on for at once out of play, when the hour changes its rate.
        private const float Settle = 6f;

        private static readonly Color Smoke = new Color(0.72f, 0.72f, 0.75f, 0.95f);
        private static readonly Color SmokeThin = new Color(0.88f, 0.88f, 0.9f, 0.55f);
        private static readonly Color LeafRed = new Color(0.86f, 0.42f, 0.14f, 1f);
        private static readonly Color LeafGold = new Color(0.96f, 0.74f, 0.26f, 1f);
        private static readonly Color Firefly = new Color(1f, 0.96f, 0.5f, 1f);
        private static readonly Color FireflyGreen = new Color(0.78f, 1f, 0.46f, 1f);
        private static readonly Color PollenGold = new Color(1f, 0.9f, 0.5f, 0.9f);
        private static readonly Color PollenPale = new Color(1f, 0.98f, 0.82f, 0.7f);
        private static readonly Color SnowBlue = new Color(0.82f, 0.9f, 1f, 0.9f);
        private static readonly Color Water = new Color(0.86f, 0.94f, 1f, 0.9f);
        private static readonly Color WaterDeep = new Color(0.62f, 0.8f, 0.95f, 0.8f);

        [SerializeField]
        private List<SpriteRenderer> night = new List<SpriteRenderer>();

        [SerializeField]
        private List<SpriteRenderer> dawn = new List<SpriteRenderer>();

        [SerializeField]
        private List<SpriteRenderer> turning = new List<SpriteRenderer>();

        [SerializeField]
        private List<ParticleSystem> nightly = new List<ParticleSystem>();

        [SerializeField]
        private List<ParticleSystem> daily = new List<ParticleSystem>();

        [SerializeField, Tooltip("The rate each of `nightly` gives at full night, and each of `daily` at full day.")]
        private List<float> nightlyRates = new List<float>();

        [SerializeField]
        private List<float> dailyRates = new List<float>();

        [SerializeField]
        private Sprite[] sails = Array.Empty<Sprite>();

        [SerializeField, Tooltip("What sways: each shown with its own frames (its picture, then its leans).")]
        private List<SpriteRenderer> swaying = new List<SpriteRenderer>();

        private readonly List<Sprite[]> swayFrames = new List<Sprite[]>();

        [SerializeField]
        private List<SpriteRenderer> far = new List<SpriteRenderer>();

        [SerializeField, Tooltip("What bends as the hero goes through it: grass, a fern, a clump of wheat.")]
        private List<Transform> brush = new List<Transform>();

        private readonly List<float> leans = new List<float>();
        private readonly List<float> leanSpeeds = new List<float>();
        private readonly List<SpriteRenderer> brushShown = new List<SpriteRenderer>();
        private MaterialPropertyBlock pushBlock;

        /// <summary>The brush material's property a renderer's push is told by: -1 .. 1, over zero to the right.</summary>
        public static readonly int PushId = Shader.PropertyToID("_Push");

        [SerializeField]
        private List<SpriteRenderer> birds = new List<SpriteRenderer>();

        [SerializeField, Tooltip("Where each bird's flock is anchored, and where in the flock it flies.")]
        private List<Vector4> birdPlaces = new List<Vector4>();

        [SerializeField]
        private Sprite[] wings = Array.Empty<Sprite>();

        [SerializeField]
        private ParticleSystem rain;

        [SerializeField, Tooltip("The heights the rain falls between: the foot and the top of the surface's band.")]
        private Vector2 rainBand;

        private readonly Dictionary<string, int> made = new Dictionary<string, int>();
        private DayNight day;
        private float shownHour = -1f;

        /// <summary>How many of each kind the world has, by kind.</summary>
        public IReadOnlyDictionary<string, int> Made => made;

        /// <summary>The lights shown only at dusk and at night.</summary>
        public IReadOnlyList<SpriteRenderer> Night => night;

        /// <summary>The shafts of low sun.</summary>
        public IReadOnlyList<SpriteRenderer> Dawn => dawn;

        /// <summary>The sails that turn.</summary>
        public IReadOnlyList<SpriteRenderer> Turning => turning;

        /// <summary>The frames the sails turn through.</summary>
        public IReadOnlyList<Sprite> SailSprites => sails;

        /// <summary>What sways in the wind: reeds, and the shapes close to the eye.</summary>
        public IReadOnlyList<SpriteRenderer> Swaying => swaying;

        /// <summary>
        /// What bends as the hero goes through it (`life brush`): drawn with the brush material,
        /// its picture bends from its foot in the wind and with his push; else it turns about its foot.
        /// </summary>
        public IReadOnlyList<Transform> Brush => brush;

        /// <summary>How near the hero a thing of the brush bends, in tiles to either side of him.</summary>
        public const float BrushReach = 1.6f;

        /// <summary>The most it leans, in degrees, with the hero right at it and running.</summary>
        public const float BrushLean = 34f;

        /// <summary>How hard it springs back, and how soon it comes to rest.</summary>
        public const float BrushSpring = 70f;
        public const float BrushDamp = 7f;

        /// <summary>
        /// Bends the brush for a moment of play: what the hero is at leans away from him, the
        /// more the faster he goes, and springs back when he has gone by, with a swing or two
        /// before it is still. `hero` is where his feet are, `speed` his speed across.
        /// </summary>
        public void Bend(Vector2 hero, float speed, float seconds)
        {
            while (leans.Count < brush.Count)
            {
                leans.Add(0f);
                leanSpeeds.Add(0f);
                // Its renderer if its material bends it (asked once), else none: it turns.
                var stem = brush[brushShown.Count];
                var shown = stem != null ? stem.GetComponent<SpriteRenderer>() : null;
                var bends = shown != null && shown.sharedMaterial != null && shown.sharedMaterial.HasFloat(PushId);
                brushShown.Add(bends ? shown : null);
            }
            pushBlock ??= new MaterialPropertyBlock();
            for (var i = 0; i < brush.Count; i++)
            {
                var stem = brush[i];
                if (stem == null)
                {
                    continue;
                }
                Vector2 at = stem.position;
                var across = at.x - hero.x;
                var over = hero.y - at.y;
                var want = 0f;
                if (Mathf.Abs(across) < BrushReach && over > -0.6f && over < 2.5f)
                {
                    var push = 1f - Mathf.Abs(across) / BrushReach;
                    var way = Mathf.Abs(across) > 0.05f ? Mathf.Sign(across) : Mathf.Sign(speed);
                    want = -way * BrushLean * push * Mathf.Clamp01(0.3f + Mathf.Abs(speed) / 6f);
                }
                var lean = leans[i];
                var turning = leanSpeeds[i];
                if (want == 0f && Mathf.Abs(lean) < 0.02f && Mathf.Abs(turning) < 0.02f)
                {
                    continue;
                }
                turning += ((want - lean) * BrushSpring - turning * BrushDamp) * seconds;
                lean = Mathf.Clamp(lean + turning * seconds, -BrushLean * 1.5f, BrushLean * 1.5f);
                leans[i] = lean;
                leanSpeeds[i] = turning;
                Lean(i, lean);
            }
        }

        // Tells a thing of the brush how far it leans: the brush material bends its picture
        // from its foot (over zero there is to the right, as a lean over zero is to the left);
        // drawn with another, it turns about its foot instead.
        private void Lean(int index, float lean)
        {
            var shown = brushShown[index];
            if (shown != null)
            {
                // The block holds its push alone, so it is set afresh rather than read back.
                pushBlock.Clear();
                pushBlock.SetFloat(PushId, -lean / BrushLean);
                shown.SetPropertyBlock(pushBlock);
                return;
            }
            brush[index].localRotation = Quaternion.Euler(0f, 0f, lean);
        }

        /// <summary>How far a thing of the brush is pushed now, as its material is told: -1 .. 1, over zero to the right.</summary>
        public float PushOf(int index)
        {
            var shown = index < brushShown.Count ? brushShown[index] : null;
            if (shown == null || !shown.HasPropertyBlock())
            {
                return 0f;
            }
            pushBlock ??= new MaterialPropertyBlock();
            shown.GetPropertyBlock(pushBlock);
            return pushBlock.GetFloat(PushId);
        }

        /// <summary>How far a thing of the brush leans now, in degrees: over zero to the left.</summary>
        public float LeanOf(int index) => index < leans.Count ? leans[index] : 0f;

        /// <summary>What stands far off behind a place.</summary>
        public IReadOnlyList<SpriteRenderer> Far => far;

        /// <summary>The birds far off.</summary>
        public IReadOnlyList<SpriteRenderer> Birds => birds;

        /// <summary>The rain over the surface (none if the map asks for none).</summary>
        public ParticleSystem Rain => rain;

        /// <summary>Whether it rains at a moment of play: a shower of RainFor seconds every RainEvery.</summary>
        public static bool RainsAt(float seconds) => Mathf.Repeat(seconds, RainEvery) < RainFor;

        /// <summary>The hour last shown.</summary>
        public float ShownHour => shownHour;

        /// <summary>
        /// How much of the night it is at an hour: 1 from 20:30 to 5:00, nothing from 7:30 to
        /// 18:00, and between those it comes and goes.
        /// </summary>
        public static float NightAt(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            if (hour >= 20.5f || hour <= 5f)
            {
                return 1f;
            }
            if (hour >= 7.5f && hour <= 18f)
            {
                return 0f;
            }
            return hour < 12f ? Mathf.InverseLerp(7.5f, 5f, hour) : Mathf.InverseLerp(18f, 20.5f, hour);
        }

        /// <summary>How strong the low sun is at an hour: at its most at 7:00 and at 18:40, gone by noon.</summary>
        public static float DawnAt(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            var morning = 1f - Mathf.Abs(hour - 7f) / 1.6f;
            var evening = 1f - Mathf.Abs(hour - 18.66f) / 1.4f;
            return Mathf.Clamp01(Mathf.Max(morning, evening));
        }

        /// <summary>Builds what the map's life marks ask for under `parent`; returns the component.</summary>
        public static TilesV2Life Build(TilesV2World world, Transform parent)
        {
            var holder = new GameObject("Life");
            holder.transform.SetParent(parent, false);
            var life = holder.AddComponent<TilesV2Life>();
            var map = world.Map;
            var set = world.Set;
            Vector2 origin = world.transform.position;
            var sailFrames = new List<Sprite>();
            for (var i = 0; ; i++)
            {
                var frame = Picture(set, "earth_sails_" + i);
                if (frame == null)
                {
                    break;
                }
                sailFrames.Add(frame);
            }
            life.sails = sailFrames.ToArray();
            foreach (var mark in map.Marks)
            {
                if (mark.Kind != Kind)
                {
                    continue;
                }
                var cell = map.CellOf(mark.Column, mark.Row);
                var at =
                    origin
                    + new Vector2(cell.x + 0.5f, cell.y)
                    + new Vector2(mark.Number(2, 0), mark.Number(3, 0)) * Pixel;
                var kind = mark.Word(0);
                if (life.Make(world, kind, mark.Word(1), at))
                {
                    life.made[kind] = life.made.TryGetValue(kind, out var n) ? n + 1 : 1;
                }
            }
            // The far villages' lights are a layer of the backdrop: they come on with the night too.
            foreach (var shown in parent.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (shown.gameObject.name == WindowsKey)
                {
                    life.night.Add(shown);
                    life.made["windows"] = 1;
                }
            }
            life.day = world.Day;
            if (life.day != null)
            {
                life.day.Applied += life.Show;
                life.Show(life.day.Hour);
            }
            return life;
        }

        private void OnDestroy()
        {
            if (day != null)
            {
                day.Applied -= Show;
            }
        }

        /// <summary>Shows the hour: the lights of the night, the low sun, the fireflies and the butterflies.</summary>
        public void Show(float hour)
        {
            shownHour = hour;
            var dark = NightAt(hour);
            var low = DawnAt(hour);
            foreach (var shown in night)
            {
                Alpha(shown, dark);
            }
            foreach (var shown in dawn)
            {
                Alpha(shown, low);
            }
            for (var i = 0; i < nightly.Count; i++)
            {
                Rate(nightly[i], nightlyRates[i] * dark);
            }
            for (var i = 0; i < daily.Count; i++)
            {
                Rate(daily[i], dailyRates[i] * (1f - dark));
            }
        }

        /// <summary>Shows the sails at a moment: they turn a frame every 1 / SailFrames seconds.</summary>
        public void Turn(float seconds)
        {
            if (sails.Length == 0)
            {
                return;
            }
            var frame = sails[Mathf.FloorToInt(seconds * SailFrames) % sails.Length];
            foreach (var shown in turning)
            {
                shown.sprite = frame;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            Turn(Time.time);
            Sway(Time.time);
            Fly(Time.time);
            var view = Camera.main;
            if (rain != null && view != null)
            {
                Vector2 at = view.transform.position;
                rain.transform.position = new Vector3(at.x, at.y + 14f, 0f);
                var emission = rain.emission;
                emission.enabled = RainsAt(Time.time) && at.y > rainBand.x && at.y < rainBand.y;
            }
        }

        /// <summary>Shows what sways at a moment: each through its frames, each from its own place in the swing.</summary>
        public void Sway(float seconds)
        {
            for (var i = 0; i < swaying.Count && i < swayFrames.Count; i++)
            {
                var frames = swayFrames[i];
                var step = Mathf.FloorToInt(seconds * SwayFrames + i * 1.37f);
                swaying[i].sprite = frames[(step % frames.Length + frames.Length) % frames.Length];
            }
        }

        /// <summary>Puts the birds where they are at a moment: each flock crosses its stretch of sky and comes round again.</summary>
        public void Fly(float seconds)
        {
            for (var i = 0; i < birds.Count; i++)
            {
                var place = birdPlaces[i];
                var along = Mathf.Repeat(seconds * FlockSpeed + place.z, FlockSpan) - FlockSpan / 2f;
                var bob = Mathf.Sin(seconds * 1.3f + place.w) * 0.25f;
                var at = new Vector2(place.x + along, place.y + place.w * 0.3f + bob);
                birds[i].transform.position = new Vector3(
                    Mathf.Round(at.x * TilesV2Set.PixelsPerUnit) / TilesV2Set.PixelsPerUnit,
                    Mathf.Round(at.y * TilesV2Set.PixelsPerUnit) / TilesV2Set.PixelsPerUnit,
                    0f
                );
                if (wings.Length > 0)
                {
                    birds[i].sprite = wings[Mathf.FloorToInt(seconds * 4f + place.w * 3f) % wings.Length];
                }
            }
        }

        private static void Alpha(SpriteRenderer shown, float alpha)
        {
            if (shown == null)
            {
                return;
            }
            var colour = shown.color;
            colour.a = alpha;
            shown.color = colour;
            shown.enabled = alpha > 0.005f;
        }

        private static void Rate(ParticleSystem system, float rate)
        {
            if (system == null)
            {
                return;
            }
            var emission = system.emission;
            var was = emission.rateOverTime.constant;
            emission.rateOverTime = rate;
            if (!Application.isPlaying && Mathf.Abs(was - rate) > 1e-4f)
            {
                // Out of play nothing runs the systems: fill them as they would be by now
                // (a picture of the world at an hour shows its fireflies).
                system.Simulate(Settle, true, true);
            }
        }

        /// <summary>The rate a system of fireflies (or of butterflies) gives now.</summary>
        public static float RateOf(ParticleSystem system) => system.emission.rateOverTime.constant;

        /// <summary>The fireflies, which come with the night, and the butterflies, which go with it.</summary>
        public IReadOnlyList<ParticleSystem> Nightly => nightly;

        public IReadOnlyList<ParticleSystem> Daily => daily;

        private static Sprite Picture(TilesV2Set set, string key)
        {
            var entry = Array.Find(set.backdrops, backdrop => backdrop.key == key);
            return entry != null ? entry.sprite : null;
        }

        private bool Make(TilesV2World world, string kind, string key, Vector2 at)
        {
            var set = world.Set;
            switch (kind)
            {
                case "sails":
                {
                    if (sails.Length == 0)
                    {
                        return false;
                    }
                    var shown = Shown("Sails", sails[0], at, TilesV2World.FoliageOrder - 1, set.litMaterial);
                    turning.Add(shown);
                    return true;
                }
                case "glow":
                case "rays":
                {
                    var sprite = Picture(set, key);
                    if (sprite == null)
                    {
                        return false;
                    }
                    var entry = Array.Find(set.backdrops, backdrop => backdrop.key == key);
                    var shown = Shown(kind + " " + key, sprite, at, entry.order, set.unlitMaterial);
                    (kind == "glow" ? night : dawn).Add(shown);
                    return true;
                }
                case "front":
                {
                    var sprite = Picture(set, key);
                    if (sprite == null)
                    {
                        return false;
                    }
                    var shown = Shown("Front " + key, sprite, at, FrontOrder, set.unlitMaterial);
                    var layer = shown.gameObject.AddComponent<ParallaxLayer>();
                    // It stands where it is marked when the camera is there, and slides past faster than the world.
                    layer.Configure(new Vector2(FrontScroll, 1f), at, at, Vector2.zero, 0f);
                    Swings(set, key, shown);
                    return true;
                }
                case "far":
                {
                    var entry = Array.Find(set.backdrops, backdrop => backdrop.key == key);
                    if (entry == null || entry.sprite == null)
                    {
                        return false;
                    }
                    var material = entry.lit ? set.litMaterial : set.unlitMaterial;
                    var shown = Shown("Far " + key, entry.sprite, at, entry.order, material);
                    var layer = shown.gameObject.AddComponent<ParallaxLayer>();
                    // It stands where it is marked when the camera is there, and is passed slower than the world.
                    layer.Configure(new Vector2(FarScroll, 1f), at, at, Vector2.zero, 0f);
                    far.Add(shown);
                    return true;
                }
                case "brush":
                {
                    var entry = Array.Find(set.backdrops, backdrop => backdrop.key == key);
                    if (entry == null || entry.sprite == null)
                    {
                        return false;
                    }
                    var material =
                        !entry.lit ? set.unlitMaterial
                        : set.brushMaterial != null ? set.brushMaterial
                        : set.litMaterial;
                    var shown = Shown("Brush " + key, entry.sprite, at, entry.order, material);
                    brush.Add(shown.transform);
                    return true;
                }
                case "sway":
                {
                    var sprite = Picture(set, key);
                    if (sprite == null)
                    {
                        return false;
                    }
                    var entry = Array.Find(set.backdrops, backdrop => backdrop.key == key);
                    var shown = Shown("Sway " + key, sprite, at, entry.order, set.litMaterial);
                    Swings(set, key, shown);
                    return true;
                }
                case "birds":
                {
                    if (wings.Length == 0)
                    {
                        wings = Frames(set, "earth_bird_");
                    }
                    if (wings.Length == 0)
                    {
                        return false;
                    }
                    for (var i = 0; i < Flock; i++)
                    {
                        var bird = Shown("Bird", wings[0], at, BirdOrder, set.unlitMaterial);
                        birds.Add(bird);
                        // The flock's anchor, and this bird's place in it (how far along, how high).
                        birdPlaces.Add(new Vector4(at.x, at.y, i * 1.1f + (i % 2) * 0.4f, i * 0.7f % 2.1f));
                    }
                    Fly(0f);
                    return true;
                }
                case "rain":
                {
                    rain = Rainfall(world, set.litMaterial);
                    rainBand = new Vector2(at.y - 20f, at.y + 40f);
                    return true;
                }
                case "smoke":
                    SmokeFrom(at, set.litMaterial);
                    return true;
                case "leaves":
                    Leaves(at, set.litMaterial);
                    return true;
                case "fireflies":
                {
                    var system = Fireflies(at, set.unlitMaterial);
                    nightly.Add(system);
                    nightlyRates.Add(RateOf(system));
                    return true;
                }
                case "butterflies":
                {
                    var system = Butterflies(at, set.litMaterial);
                    daily.Add(system);
                    dailyRates.Add(RateOf(system));
                    return true;
                }
                case "splash":
                    Splash(at, set.unlitMaterial);
                    return true;
                case "pollen":
                    Pollen(at, set.unlitMaterial);
                    return true;
                case "snow":
                    Snow(at, set.litMaterial);
                    return true;
                case "drip":
                    Drip(at, set.litMaterial);
                    return true;
                default:
                    return false;
            }
        }

        // The frames of a picture that sways: itself, its leans one way, and back.
        private void Swings(TilesV2Set set, string key, SpriteRenderer shown)
        {
            var leans = Frames(set, key + "_s");
            if (leans.Length == 0)
            {
                return;
            }
            var frames = new List<Sprite> { shown.sprite };
            frames.AddRange(leans);
            for (var i = leans.Length - 2; i >= 0; i--)
            {
                frames.Add(leans[i]);
            }
            swaying.Add(shown);
            swayFrames.Add(frames.ToArray());
        }

        // The pictures key0, key1, ... the tile set has (from 1 when the first is the picture itself).
        private static Sprite[] Frames(TilesV2Set set, string prefix)
        {
            var frames = new List<Sprite>();
            for (var i = prefix.EndsWith("_s") ? 1 : 0; ; i++)
            {
                var frame = Picture(set, prefix + i);
                if (frame == null)
                {
                    break;
                }
                frames.Add(frame);
            }
            return frames.ToArray();
        }

        private SpriteRenderer Shown(string name, Sprite sprite, Vector2 at, int order, Material material)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(transform, false);
            holder.transform.position = at;
            var shown = holder.AddComponent<SpriteRenderer>();
            shown.sprite = sprite;
            shown.sortingOrder = order;
            if (material != null)
            {
                shown.sharedMaterial = material;
            }
            return shown;
        }

        private ParticleSystem System(string name, Vector2 at, Material material, int most)
        {
            var system = TilesV2Particles.Make(transform, name, at, material, most);
            system.transform.position = at;
            return system;
        }

        private static void Push(ParticleSystem system, float across, float up)
        {
            var force = system.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = across;
            force.y = up;
            force.z = 0f;
        }

        private static void Velocity(ParticleSystem system, Vector2 from, Vector2 to)
        {
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(from.x, to.x);
            velocity.y = new ParticleSystem.MinMaxCurve(from.y, to.y);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        private static void Wander(ParticleSystem system, float strength, float frequency)
        {
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.3f;
        }

        private static void Box(ParticleSystem system, float wide, float high)
        {
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(wide, high, 0f);
        }

        // Smoke from a chimney: grey pixels that rise, lean with a light wind and thin out.
        private void SmokeFrom(Vector2 at, Material material)
        {
            var system = System("Smoke", at, material, 110);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
            main.startSpeed = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(Smoke, SmokeThin);
            main.prewarm = true;
            var emission = system.emission;
            emission.rateOverTime = 20f;
            Box(system, 0.5f, 0.1f);
            Velocity(system, new Vector2(0.15f, 0.45f), new Vector2(0.4f, 0.75f));
            Wander(system, 0.25f, 0.4f);
            TilesV2Particles.Fade(system, Color.white, new Color(1f, 1f, 1f, 0.6f), 0.1f);
            system.Play();
        }

        // Leaves from an autumn crown: they fall slowly and rock as they go.
        private void Leaves(Vector2 at, Material material)
        {
            var system = System("Leaves", at, material, 24);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(LeafRed, LeafGold);
            main.prewarm = true;
            var emission = system.emission;
            emission.rateOverTime = 1.4f;
            Box(system, 5f, 2.5f);
            Velocity(system, new Vector2(-0.1f, -0.55f), new Vector2(0.35f, -0.3f));
            Wander(system, 0.6f, 0.5f);
            TilesV2Particles.Fade(system, Color.white, Color.white, 0.05f);
            system.Play();
        }

        // Fireflies: points of light that wander low over the grass, coming and going.
        private ParticleSystem Fireflies(Vector2 at, Material material)
        {
            var system = System("Fireflies", at, material, 40);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(Firefly, FireflyGreen);
            main.prewarm = true;
            var emission = system.emission;
            emission.rateOverTime = 8f;
            Box(system, 14f, 3f);
            Wander(system, 0.5f, 0.6f);
            TilesV2Particles.Fade(system, Color.white, Color.white, 0.3f);
            system.Play();
            return system;
        }

        // Butterflies: a few bright specks that flutter over a meadow by day.
        private ParticleSystem Butterflies(Vector2 at, Material material)
        {
            var system = System("Butterflies", at, material, 6);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = 0f;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 0.95f), 0f),
                    new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.7f, 1f), 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(gradient)
            {
                mode = ParticleSystemGradientMode.RandomColor,
            };
            main.prewarm = true;
            var emission = system.emission;
            emission.rateOverTime = 0.7f;
            Box(system, 8f, 2f);
            Wander(system, 1.2f, 1.6f);
            TilesV2Particles.Fade(system, Color.white, Color.white, 0.15f);
            system.Play();
            return system;
        }

        // Pollen: motes of gold that hang in warm air, rise a little and wander, catching the light.
        private void Pollen(Vector2 at, Material material)
        {
            var system = System("Pollen", at, material, 40);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(PollenGold, PollenPale);
            main.prewarm = true;
            var emission = system.emission;
            emission.rateOverTime = 4.5f;
            Box(system, 18f, 6f);
            Velocity(system, new Vector2(-0.08f, 0.04f), new Vector2(0.2f, 0.16f));
            Wander(system, 0.35f, 0.35f);
            TilesV2Particles.Fade(system, Color.white, Color.white, 0.35f);
            system.Play();
        }

        // Snow: flakes that come down slowly, rocking, from over the view of a cold place.
        private void Snow(Vector2 at, Material material)
        {
            var system = System("Snow", at, material, 90);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
            main.startSpeed = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, SnowBlue);
            main.prewarm = true;
            var emission = system.emission;
            emission.rateOverTime = 7f;
            Box(system, 26f, 1f);
            Velocity(system, new Vector2(-0.35f, -0.85f), new Vector2(-0.05f, -0.5f));
            Wander(system, 0.3f, 0.5f);
            TilesV2Particles.Fade(system, Color.white, Color.white, 0.08f);
            system.Play();
        }

        // Where falling water meets a pool: drops thrown up and out, falling back.
        private void Splash(Vector2 at, Material material)
        {
            var system = System("Splash", at, material, 40);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(Water, WaterDeep);
            var emission = system.emission;
            emission.rateOverTime = 40f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.5f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Push(system, 0f, -Drop);
            TilesV2Particles.Fade(system, Color.white, Color.white, 0f);
            system.Play();
        }

        // Rain: streaks a pixel wide that fall fast, born over the view and riding with it; it
        // falls only in a shower and only while the camera is over the surface (Update).
        private ParticleSystem Rainfall(TilesV2World world, Material material)
        {
            var system = System("Rain", Vector2.zero, material, 900);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.5f);
            main.startSpeed = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.75f, 0.84f, 0.95f, 0.75f),
                new Color(0.6f, 0.72f, 0.88f, 0.55f)
            );
            var emission = system.emission;
            emission.rateOverTime = 520f;
            emission.enabled = false;
            Box(system, world.Set.View.x + 8f, 0.5f);
            Velocity(system, new Vector2(-2.2f, -20f), new Vector2(-1.6f, -16f));
            var shown = system.GetComponent<ParticleSystemRenderer>();
            // A drop is a streak of three art pixels: stretched along its fall.
            shown.renderMode = ParticleSystemRenderMode.Stretch;
            shown.velocityScale = 0.01f;
            shown.lengthScale = 2f;
            system.Play();
            return system;
        }

        // A drop now and then from a mine's roof.
        private void Drip(Vector2 at, Material material)
        {
            var system = System("Drip", at, material, 4);
            var main = system.main;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startColor = WaterDeep;
            var emission = system.emission;
            emission.rateOverTime = 0.6f;
            var shape = system.shape;
            shape.enabled = false;
            Push(system, 0f, -Drop);
            system.Play();
        }
    }
}
