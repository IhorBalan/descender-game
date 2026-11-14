using System.Collections;
using System.Collections.Generic;
using Descender.CameraRig;
using Descender.Input;
using Descender.Player;
using Descender.Save;
using UnityEngine;
using UnityEngine.InputSystem;

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
    /// </summary>
    public sealed class TilesV2Playground : MonoBehaviour
    {
        public const string ShotsArgument = "-tilesV2Shots";

        /// <summary>How many times faster the day runs while its key is held.</summary>
        public const float FastForward = 60f;

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

        private readonly List<string> places = new List<string>();
        private int place = -1;
        private bool helpShown = true;
        private bool shooting;
        private string note = "";
        private float noteLeft;
        private GUIStyle style;
        private string shown = "";
        private int shownMinute = -1;
        private bool shownHelp;
        private bool shownNote;

        public TilesV2World World => world;
        public PlayerController Hero => hero;

        private void Start()
        {
            SettingsApplier.Apply(SettingsData.Current);
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
            hero.SetAbilities((Ability[])System.Enum.GetValues(typeof(Ability)));
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
            if (noteLeft > 0f)
            {
                noteLeft -= Time.unscaledDeltaTime;
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
            if (InputRouter.Instance != null && InputRouter.Instance.RestartPressed)
            {
                hero.Respawn(world.Spawn);
                Say("back at the start");
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
                        + "PgDn / PgUp: next / last place   R: back to the start   F1: hide this   Esc: quit"
                    : clock;
                if (noted)
                {
                    shown += "\n" + note;
                }
            }
            var text = shown;
            var area = new Rect(6f, 4f, Screen.width / scale - 12f, 90f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), text, style);
            GUI.color = Color.white;
            GUI.Label(area, text, style);
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
                hero.PlaceAt(point);
                if (view != null)
                {
                    view.Snap();
                }
                Say(places[place]);
            }
        }

        private void Say(string text)
        {
            note = text;
            noteLeft = 2.5f;
            shownMinute = -1;
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

        // Photographs the map's named places at noon, and the first of them at sunset and at night, then quits.
        private IEnumerator Shoot(string folder)
        {
            shooting = true;
            System.IO.Directory.CreateDirectory(folder);
            hero.Freeze();
            hero.gameObject.SetActive(false);
            if (view != null)
            {
                view.Target = null;
            }
            world.Day.Running = false;
            var shots = new List<(string place, float hour, string file)>();
            foreach (var name in places)
            {
                shots.Add((name, 12f, name));
            }
            if (places.Count > 0)
            {
                shots.Add((places[0], 18.75f, places[0] + "_sunset"));
                shots.Add((places[0], 23f, places[0] + "_night"));
            }
            // Let the particles fill the air first.
            yield return new WaitForSeconds(4f);
            foreach (var (name, hour, file) in shots)
            {
                if (!world.TryView(name, out var point))
                {
                    continue;
                }
                world.Day.Hour = hour;
                var eye = Camera.main;
                if (eye != null)
                {
                    eye.transform.position = new Vector3(point.x, point.y, eye.transform.position.z);
                }
                yield return new WaitForSeconds(1.2f);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folder, file + ".png"));
                yield return null;
                yield return null;
            }
            Application.Quit();
        }
    }
}
