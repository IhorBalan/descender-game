using System.Collections;
using System.Collections.Generic;
using Descender.CameraRig;
using Descender.Input;
using Descender.Player;
using Descender.Save;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Runs the Tiles V2 playground (Assets/Scenes/Sandbox/TilesV2Playground.unity), a scene
    /// that stands on its own: the hero with every ability in a Tiles V2 world, followed by
    /// the game's camera, with no title, no saves and no HUD. It puts him at the map's
    /// start, fences the map's sides, and gives the keys a tester needs: the hour of the
    /// day, a jump between the map's named places, a way back to the start and out. Started
    /// with `-tilesV2Shots &lt;folder&gt;` it photographs the named places instead and quits,
    /// which is how the Windows test build is checked.
    ///
    /// As a journey (the world scene, Assets/Scenes/Sandbox/TilesV2Descent.unity) the hero
    /// starts with nothing and the map's marks are stood in the world (TilesV2Things): he
    /// finds his abilities in their shrines, rests at checkpoints and comes back at the last
    /// one when he dies; each region says its name as he walks into it, and M opens the
    /// chart of what he has seen.
    /// </summary>
    public sealed class TilesV2Playground : MonoBehaviour
    {
        public const string ShotsArgument = "-tilesV2Shots";

        /// <summary>How many times faster the day runs while its key is held.</summary>
        public const float FastForward = 60f;

        private const float LanternIntensity = 0.5f;
        private const int ShotWidth = 1280;
        private const int ShotHeight = 720;

        private static readonly (Key key, float hour, string name)[] Hours =
        {
            (Key.Digit1, 7f, "morning"),
            (Key.Digit2, 12f, "noon"),
            (Key.Digit3, 18.75f, "sunset"),
            (Key.Digit4, 23f, "night"),
        };

        [SerializeField]
        private TilesV2World world;

        [SerializeField]
        private PlayerController hero;

        [SerializeField]
        private GameCamera view;

        [SerializeField, Tooltip("The hero starts with no ability and finds them in the world; else he has them all.")]
        private bool journey;

        [SerializeField, Tooltip("The game's checkpoint, stood where the map marks one.")]
        private GameObject checkpointPrefab;

        [SerializeField, Tooltip("The game's ability pickup, stood where the map marks one.")]
        private GameObject abilityPrefab;

        private TilesV2Things things;
        private TilesV2Chart chart;
        private bool chartShown;
        private bool everything;
        private TilesV2Set.Region region;
        private string title = "";
        private string subtitle = "";
        private float titleLeft;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;

        private readonly List<string> places = new List<string>();
        private int place = -1;
        private bool helpShown = true;
        private bool shooting;
        private string note = "";
        private float noteLeft;
        private GUIStyle style;
        private Light2D lantern;
        private string shown = "";
        private int shownMinute = -1;
        private bool shownHelp;
        private bool shownNote;

        public TilesV2World World => world;
        public PlayerController Hero => hero;

        /// <summary>The game's things the map marks, once the scene has started; null for a map with none.</summary>
        public TilesV2Things Things => things;

        /// <summary>The chart of what the hero has seen; null for a map that is all one region.</summary>
        public TilesV2Chart Chart => chart;

        public bool IsJourney => journey;

        /// <summary>The region's name on the screen, while it shows.</summary>
        public string Title => titleLeft > 0f ? title : "";

        private void Start()
        {
            SettingsApplier.Apply(SettingsData.Current);
            FitWindow();
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Enemies.Kills.Reset();
            Pickups.Checkpoint.Reset();
            Triggers.Keys.Reset();
            if (world == null || hero == null)
            {
                Debug.LogError("TilesV2Playground: the scene has lost its world or its hero; build it again.", this);
                enabled = false;
                return;
            }
            if (!world.IsBuilt)
            {
                world.Build();
            }
            Fence();
            everything = !journey;
            hero.SetAbilities(journey ? new Ability[0] : (Ability[])System.Enum.GetValues(typeof(Ability)));
            if (world.Map != null && world.Map.Marks.Count > 0)
            {
                things = TilesV2Things.Build(world, checkpointPrefab, abilityPrefab);
                things.RelicFound += () => Say($"a relic: {things.RelicsFound} of {things.Relics}");
                things.Ended += () => Announce("The Gate of Paradise", "the descent is done");
            }
            if (world.Map != null && world.Map.Regions.Count > 0)
            {
                chart = new TilesV2Chart(world);
            }
            hero.Died += () => StartCoroutine(Revive());
            hero.AbilityUnlocked += ability => Announce(Spaced(ability.ToString()), AbilityHint(ability));
            hero.PlaceAt(world.Spawn);
            hero.SetRespawnPoint(world.Spawn);
            hero.DeathY = world.Area.yMin - 6f;
            if (view != null)
            {
                view.Bounds = world.Area;
                view.Target = hero.transform;
                view.Snap();
            }
            if (InputRouter.Instance != null)
            {
                InputRouter.Instance.UsePlayer();
            }
            lantern = MakeLantern();
            if (world.Map != null)
            {
                places.AddRange(world.Map.Views.Keys);
            }
            var folder = ShotsFolder();
            if (folder != null)
            {
                StartCoroutine(Shoot(folder));
            }
        }

        private void Update()
        {
            Wander();
            if (noteLeft > 0f)
            {
                noteLeft -= Time.unscaledDeltaTime;
            }
            if (lantern != null && lantern.enabled && world.Light != null)
            {
                // The darker the tile light on him, the more the lantern gives: nothing in daylight.
                var lit = world.Light.At(world.Terrain.WorldToCell(hero.transform.position + Vector3.up));
                lantern.intensity = LanternIntensity * Mathf.Clamp01(1f - lit.maxColorComponent);
            }
            var keys = Keyboard.current;
            if (keys == null || shooting)
            {
                return;
            }
            if (keys.escapeKey.wasPressedThisFrame)
            {
                Application.Quit();
            }
            if (keys.f1Key.wasPressedThisFrame)
            {
                helpShown = !helpShown;
            }
            if (keys.mKey.wasPressedThisFrame && chart != null)
            {
                chartShown = !chartShown;
            }
            if (keys.f3Key.wasPressedThisFrame)
            {
                everything = !everything;
                hero.SetAbilities(everything ? (Ability[])System.Enum.GetValues(typeof(Ability)) : new Ability[0]);
                Say(everything ? "every ability" : "no ability");
            }
            if (keys.f4Key.wasPressedThisFrame && chart != null)
            {
                chart.RevealAll();
                Say("the whole chart");
            }
            if (keys.f2Key.wasPressedThisFrame)
            {
                lantern.enabled = !lantern.enabled;
                Say(lantern.enabled ? "lantern lit" : "lantern out: the tile light alone");
            }
            if (InputRouter.Instance != null && InputRouter.Instance.RestartPressed)
            {
                if (journey)
                {
                    hero.Respawn();
                    Say("back at the last rest");
                }
                else
                {
                    hero.Respawn(world.Spawn);
                    Say("back at the start");
                }
            }
            if (keys.pageDownKey.wasPressedThisFrame)
            {
                Visit(place + 1);
            }
            if (keys.pageUpKey.wasPressedThisFrame)
            {
                Visit(place - 1);
            }
            var day = world.Day;
            if (day == null)
            {
                return;
            }
            foreach (var (key, hour, name) in Hours)
            {
                if (keys[key].wasPressedThisFrame)
                {
                    day.Hour = hour;
                    Say(name);
                }
            }
            day.Speed = keys.tKey.isPressed ? FastForward : 1f;
            if (keys.pKey.wasPressedThisFrame)
            {
                day.Running = !day.Running;
                Say(day.Running ? "the day runs" : "the day stands still");
            }
        }

        private void OnGUI()
        {
            if (shooting)
            {
                return;
            }
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = false };
                style.normal.textColor = Color.white;
            }
            var scale = Mathf.Max(1f, Mathf.Floor(Screen.height / 360f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            var hour = world != null && world.Day != null ? world.Day.Hour : 0f;
            var minute = (int)(hour * 60f);
            var noted = noteLeft > 0f;
            if (minute != shownMinute || helpShown != shownHelp || noted != shownNote)
            {
                // The text is put together when the clock's minute turns, not every frame.
                shownMinute = minute;
                shownHelp = helpShown;
                shownNote = noted;
                var clock = $"{minute / 60:00}:{minute % 60:00}";
                shown = helpShown
                    ? $"Tiles V2 playground   {clock}\n"
                        + "move A D / arrows   jump Space   dash Shift   sword J\n"
                        + "1 morning  2 noon  3 sunset  4 night   hold T: the day runs fast   P: stop the day\n"
                        + "PgDn / PgUp: next / last place   R: back to the start   F2: lantern   F1: hide this   Esc: quit"
                        + (
                            chart != null
                                ? "\nM: the chart   F3: every ability / none   F4: the whole chart   cast L   heal H"
                                : ""
                        )
                    : clock;
                if (things != null && things.Relics > 0)
                {
                    shown += $"   relics {things.RelicsFound}/{things.Relics}";
                }
                if (noted)
                {
                    shown += "\n" + note;
                }
            }
            var text = shown;
            var area = new Rect(6f, 4f, Screen.width / scale - 12f, 110f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), text, style);
            GUI.color = Color.white;
            GUI.Label(area, text, style);
            DrawTitle(scale);
            DrawChart(scale);
        }

        // The region's name (or an ability's) across the middle of the screen, fading out.
        private void DrawTitle(float scale)
        {
            if (titleLeft <= 0f)
            {
                return;
            }
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 26,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
                titleStyle.normal.textColor = Color.white;
                subtitleStyle = new GUIStyle(titleStyle) { fontSize = 13, fontStyle = FontStyle.Normal };
            }
            var alpha = Mathf.Clamp01(titleLeft / 0.8f) * Mathf.Clamp01((TitleTime - titleLeft) / 0.4f);
            var wide = Screen.width / scale;
            var top = Screen.height / scale * 0.22f;
            foreach (
                var (text, look, y, tall) in new[]
                {
                    (title, titleStyle, top, 34f),
                    (subtitle, subtitleStyle, top + 32f, 20f),
                }
            )
            {
                GUI.color = new Color(0f, 0f, 0f, 0.75f * alpha);
                GUI.Label(new Rect(1f, y + 1f, wide, tall), text, look);
                GUI.color = new Color(1f, 0.96f, 0.84f, alpha);
                GUI.Label(new Rect(0f, y, wide, tall), text, look);
            }
            GUI.color = Color.white;
        }

        // The chart over the middle of the screen, as large as fits, with the hero on it.
        private void DrawChart(float scale)
        {
            if (!chartShown || chart == null)
            {
                return;
            }
            chart.Show();
            var screen = new Vector2(Screen.width, Screen.height) / scale;
            var picture = chart.Picture;
            var size = Mathf.Min(screen.x * 0.9f / picture.width, screen.y * 0.94f / picture.height);
            var area = new Rect(
                (screen.x - picture.width * size) / 2f,
                (screen.y - picture.height * size) / 2f,
                picture.width * size,
                picture.height * size
            );
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0f, 0f, screen.x, screen.y), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(area, picture);
            if (Time.unscaledTime % 0.6f < 0.4f)
            {
                Vector2 at = hero.transform.position - world.transform.position;
                var dot = new Rect(
                    area.x + at.x * size - 2f,
                    area.yMax - (at.y + 1f) * size - 2f,
                    Mathf.Max(size, 4f),
                    Mathf.Max(size * 2f, 4f)
                );
                GUI.DrawTexture(dot, Texture2D.whiteTexture);
            }
        }

        /// <summary>
        /// Sets the scene up as a journey: the hero starts with nothing, and the game's
        /// checkpoint and ability pickup stand where the map marks them.
        /// </summary>
        public void ConfigureJourney(GameObject checkpoint, GameObject ability)
        {
            journey = true;
            checkpointPrefab = checkpoint;
            abilityPrefab = ability;
        }

        private const float TitleTime = 4f;

        private void OnDestroy()
        {
            chart?.Release();
        }

        // What walking does: the chart fills in, and a new region says its name.
        private void Wander()
        {
            if (titleLeft > 0f)
            {
                titleLeft -= Time.unscaledDeltaTime;
            }
            if (world == null || world.Map == null || hero == null)
            {
                return;
            }
            Vector2 at = hero.transform.position - world.transform.position;
            if (chart != null)
            {
                chart.Reveal(new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y + 1f)));
            }
            var now = world.RegionAt((Vector2)hero.transform.position + Vector2.up);
            if (now != region)
            {
                region = now;
                if (now != null && !shooting)
                {
                    Announce(now.name, now.subtitle);
                }
            }
        }

        private void Announce(string big, string small)
        {
            title = big;
            subtitle = small;
            titleLeft = TitleTime;
        }

        // The hero comes back at his last rest, a moment after he dies.
        private IEnumerator Revive()
        {
            yield return new WaitForSeconds(1.4f);
            if (hero != null && !hero.IsAlive)
            {
                hero.Respawn();
            }
        }

        // `LightDash` as it is said: "Light Dash".
        private static string Spaced(string name)
        {
            var text = new System.Text.StringBuilder();
            foreach (var letter in name)
            {
                if (char.IsUpper(letter) && text.Length > 0)
                {
                    text.Append(' ');
                }
                text.Append(letter);
            }
            return text.ToString();
        }

        private static string AbilityHint(Ability ability)
        {
            switch (ability)
            {
                case Ability.LightDash:
                    return "dash (Shift) through a veil of light";
                case Ability.WallGrip:
                    return "hold towards a wall to cling; jump to leap from it";
                case Ability.HolyLight:
                    return "cast (L) a bolt: it breaks the seals of night";
                case Ability.Glide:
                    return "hold jump while falling to glide";
                case Ability.Smite:
                    return "down and sword (J) in the air: the blow breaks cracked slabs";
                case Ability.Wings:
                    return "jump again in the air";
                default:
                    return "";
            }
        }

        /// <summary>Sets the scene's parts (the scene builder's one entry point).</summary>
        public void Configure(TilesV2World tiles, PlayerController player, GameCamera gameCamera)
        {
            world = tiles;
            hero = player;
            view = gameCamera;
        }

        /// <summary>Puts the hero at one of the map's named places, by its number round the list.</summary>
        public void Visit(int index)
        {
            if (places.Count == 0)
            {
                return;
            }
            place = (index % places.Count + places.Count) % places.Count;
            if (world.TryView(places[place], out var point))
            {
                hero.PlaceAt(StandingNear(point));
                if (view != null)
                {
                    view.Snap();
                }
                Say(places[place]);
            }
        }

        // The nearest spot to a point where he can stand: two free cells with ground under them.
        private Vector2 StandingNear(Vector2 point)
        {
            var terrain = world.Terrain;
            var centre = terrain.WorldToCell(point);
            for (var reach = 0; reach <= 16; reach++)
            {
                for (var dy = -reach; dy <= reach; dy++)
                {
                    for (var dx = -reach; dx <= reach; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != reach)
                        {
                            continue;
                        }
                        var cell = centre + new Vector3Int(dx, dy, 0);
                        var free = !terrain.HasTile(cell) && !terrain.HasTile(cell + Vector3Int.up);
                        if (free && terrain.HasTile(cell + Vector3Int.down))
                        {
                            return (Vector2)terrain.CellToWorld(cell) + new Vector2(0.5f, 0f);
                        }
                    }
                }
            }
            return point;
        }

        // The settings size the window in steps of the game's own 320 x 180. The playground's view
        // is another size, so a step that is no whole multiple of it would leave bars round the
        // picture: take the nearest whole multiple that fits the screen.
        private void FitWindow()
        {
            var settings = SettingsData.Current;
            if (
                Application.isEditor
                || Application.isBatchMode
                || settings == null
                || settings.fullscreen
                || world == null
            )
            {
                return;
            }
            var pixels = world.Set.ViewPixels;
            var (wide, tall) = settings.WindowSizeFor(settings.scale);
            var times = Mathf.Max(1, Mathf.CeilToInt(tall / (float)pixels.y));
            var usable = Screen.mainWindowDisplayInfo.workArea;
            while (times > 1 && (pixels.x * times > usable.width || pixels.y * times > usable.height))
            {
                times--;
            }
            if (pixels.x * times != wide || pixels.y * times != tall)
            {
                Screen.SetResolution(pixels.x * times, pixels.y * times, FullScreenMode.Windowed);
            }
        }

        private void Say(string text)
        {
            note = text;
            noteLeft = 2.5f;
            shownMinute = -1;
        }

        // A small light the hero carries, so he and the ground round him can be seen in a cave no
        // torch reaches. It is the playground's, not the standard's: it shines through rock.
        private Light2D MakeLantern()
        {
            var holder = new GameObject("Lantern");
            holder.transform.SetParent(hero.transform, false);
            holder.transform.localPosition = new Vector3(0f, 1f, 0f);
            var light = holder.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.blendStyleIndex = 0;
            light.color = new Color(1f, 0.93f, 0.8f);
            light.intensity = LanternIntensity;
            light.pointLightInnerRadius = 0.5f;
            light.pointLightOuterRadius = 5f;
            light.falloffIntensity = 0.6f;
            light.shadowsEnabled = false;
            return light;
        }

        // Walls at the map's two sides, so the hero cannot walk off the world.
        private void Fence()
        {
            var area = world.Area;
            foreach (var x in new[] { area.xMin - 0.5f, area.xMax + 0.5f })
            {
                var wall = new GameObject("Fence") { layer = LayerMask.NameToLayer("World") };
                wall.transform.SetParent(transform, false);
                wall.transform.position = new Vector3(x, area.center.y + 20f, 0f);
                wall.AddComponent<BoxCollider2D>().size = new Vector2(1f, area.height + 80f);
            }
        }

        private static string ShotsFolder()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == ShotsArgument)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        // Photographs the hero at the start, then the map's named places at the day's start and
        // its pictures (a place at an hour), and quits.
        private IEnumerator Shoot(string folder)
        {
            shooting = true;
            System.IO.Directory.CreateDirectory(folder);
            world.Day.Running = false;
            // Let him land and the particles fill the air first.
            yield return new WaitForSeconds(4f);
            Photograph(System.IO.Path.Combine(folder, "start.png"));
            hero.Freeze();
            hero.gameObject.SetActive(false);
            if (view != null)
            {
                view.Target = null;
            }
            var shots = new List<(string place, float hour, string file)>();
            foreach (var name in places)
            {
                shots.Add((name, world.Set.dayStart, name));
            }
            foreach (var shot in world.Map.Shots)
            {
                shots.Add((shot.View, shot.Hour, shot.Name));
            }
            foreach (var (name, hour, file) in shots)
            {
                if (!world.TryView(name, out var point))
                {
                    continue;
                }
                world.Day.Hour = hour;
                point = world.Framed(point);
                var eye = Camera.main;
                if (eye != null)
                {
                    eye.transform.position = new Vector3(point.x, point.y, eye.transform.position.z);
                }
                yield return new WaitForSeconds(1.2f);
                Photograph(System.IO.Path.Combine(folder, file + ".png"));
            }
            Application.Quit();
        }

        // Renders the game's camera into a picture of the default window's size. It asks the
        // camera for the frame itself, so it also works in a player started with -batchmode,
        // which has no window to take a screenshot of.
        private static void Photograph(string file)
        {
            var eye = Camera.main;
            if (eye == null)
            {
                return;
            }
            var target = new RenderTexture(ShotWidth, ShotHeight, 24, RenderTextureFormat.ARGB32);
            var request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(eye, request))
            {
                RenderPipeline.SubmitRenderRequest(eye, request);
            }
            else
            {
                var before = eye.targetTexture;
                eye.targetTexture = target;
                eye.Render();
                eye.targetTexture = before;
            }
            var active = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(ShotWidth, ShotHeight, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            System.IO.File.WriteAllBytes(file, image.EncodeToPNG());
            Destroy(image);
            target.Release();
            Destroy(target);
        }
    }
}
