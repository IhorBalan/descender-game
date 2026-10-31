using System.Collections;
using Descender.Audio;
using Descender.Input;
using Descender.Player;
using Descender.Save;
using Descender.Story;
using Descender.UI;
using Descender.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Descender.Game
{
    /// <summary>
    /// The game's phases and the moves between them: the title, entering the world
    /// (a new game or a save), pausing, saving, restarting at the last checkpoint,
    /// quitting to the title. One in Main.unity, which is the only scene in the
    /// build besides the regions; it makes the EventSystem the menus need and
    /// spawns the hero when the world is entered. The menus call its public methods
    /// and listen to its events. Port of the flow parts of scripts/main.gd and
    /// scripts/ui/title.gd.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        /// <summary>The id saves store for the descent; the Godot game knew only this one level.</summary>
        public const string LevelId = "world_1";
        public const string LevelName = "World 1: The Descent";
        public const float DeathFadeTime = 0.4f;

        [SerializeField]
        [Tooltip("Assets/Prefabs/Player.prefab; spawned when the world is entered.")]
        private PlayerController heroPrefab;

        [SerializeField]
        private CameraRig.GameCamera gameCamera;

        [SerializeField]
        private Music music;

        [SerializeField]
        private ScreenFade fade;

        private Coroutine transition;

        public Phase Current { get; private set; } = Phase.Title;
        public PlayerController Hero { get; private set; }

        /// <summary>Seconds played in this save, carried over from the loaded file.</summary>
        public float PlayTime { get; private set; }

        /// <summary>Index of the checkpoint the hero last rested at, -1 for none; the checkpoints set it.</summary>
        public int ActiveCheckpoint { get; set; } = -1;

        /// <summary>Where a restart puts the hero; the checkpoints set it, else the start door.</summary>
        public Vector2? RespawnPoint { get; set; }

        public event System.Action TitleShown;
        public event System.Action<PlayerController> WorldEntered;
        public event System.Action Paused;
        public event System.Action Resumed;

        /// <summary>
        /// The world could not be entered (no hero prefab, no region scene); the reason, for the title.
        /// </summary>
        public event System.Action<string> WorldUnavailable;

        public bool IsPlaying => Current == Phase.Playing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.targetFrameRate = -1;
            EnsureEventSystem();
        }

        private void Start()
        {
            SettingsApplier.Apply(SettingsData.Current);
            ShowTitle();
        }

        private void Update()
        {
            if (Current != Phase.Playing)
            {
                return;
            }
            PlayTime += Time.deltaTime;
            var input = InputRouter.Instance;
            if (input == null)
            {
                return;
            }
            if (input.PausePressed)
            {
                Pause();
            }
            else if (input.RestartPressed && Hero != null && Hero.IsAlive)
            {
                Restart();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Losing the window pauses in a build; in the editor the inspector takes focus all the time.
            if (!hasFocus && Current == Phase.Playing && !Application.isEditor)
            {
                Pause();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Clears the slot, resets every static state and enters the world fresh.</summary>
        public void StartNewGame(int slot, Difficulty difficulty)
        {
            if (transition != null)
            {
                return;
            }
            SaveManager.StartNewGame(slot, difficulty);
            ResetStatics();
            PlayTime = 0f;
            ActiveCheckpoint = -1;
            RespawnPoint = null;
            transition = StartCoroutine(EnterWorld(null));
        }

        /// <summary>Loads a slot and enters the world where it left off. False when the slot is empty.</summary>
        public bool LoadGame(int slot)
        {
            if (transition != null || !SaveManager.LoadSlot(slot))
            {
                return false;
            }
            var data = SaveManager.Pending;
            StoryFlags.FromList(data.Flags);
            Economy.Inventory.FromJson(data.Inventory);
            Economy.Skills.FromJson(data.Skills);
            PlayTime = (float)data.PlayTime;
            ActiveCheckpoint = data.Checkpoint;
            RespawnPoint = null;
            transition = StartCoroutine(EnterWorld(data));
            return true;
        }

        /// <summary>Loads the slot saved most recently. False when every slot is empty.</summary>
        public bool ContinueGame()
        {
            var slot = SaveManager.Slots.LatestSlot();
            return slot >= 0 && LoadGame(slot);
        }

        public void Pause()
        {
            if (Current != Phase.Playing)
            {
                return;
            }
            Current = Phase.Paused;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (music != null)
            {
                music.Ducked = true;
            }
            InputRouter.Instance?.UseUi();
            Paused?.Invoke();
        }

        public void Resume()
        {
            if (Current != Phase.Paused)
            {
                return;
            }
            Current = Phase.Playing;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (music != null)
            {
                music.Ducked = false;
            }
            InputRouter.Instance?.UsePlayer();
            Resumed?.Invoke();
        }

        /// <summary>Writes the progress to the current slot. False when the game runs in no slot.</summary>
        public bool SaveGame(out string place)
        {
            place = PlaceName();
            if (SaveManager.CurrentSlot < 0)
            {
                return false;
            }
            var data = new SaveData
            {
                Level = LevelId,
                Checkpoint = ActiveCheckpoint,
                Flags = StoryFlags.ToList(),
                Inventory = Economy.Inventory.ToJson(),
                Skills = Economy.Skills.ToJson(),
                PlayTime = PlayTime,
                SavedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            if (Hero != null)
            {
                foreach (Ability ability in System.Enum.GetValues(typeof(Ability)))
                {
                    if (Hero.Has(ability))
                    {
                        data.Abilities.Add(ability);
                    }
                }
            }
            return SaveManager.Write(data);
        }

        /// <summary>Puts the hero back at the last checkpoint (or the start) with full health.</summary>
        public void Restart()
        {
            if (Hero == null || Current == Phase.Title || Current == Phase.Loading)
            {
                return;
            }
            if (Current == Phase.Paused)
            {
                Resume();
            }
            if (transition == null)
            {
                transition = StartCoroutine(RestartRoutine());
            }
        }

        /// <summary>Leaves the world and shows the title; whatever was not saved is gone.</summary>
        public void QuitToTitle()
        {
            if (Current == Phase.Title)
            {
                return;
            }
            if (transition != null)
            {
                StopCoroutine(transition);
                transition = null;
            }
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (music != null)
            {
                music.Ducked = false;
            }
            if (Hero != null)
            {
                Hero.Died -= OnHeroDied;
                Destroy(Hero.gameObject);
                Hero = null;
            }
            if (gameCamera != null)
            {
                gameCamera.Target = null;
            }
            if (WorldStreamer.Instance != null)
            {
                WorldStreamer.Instance.RegionChanged -= OnRegionChanged;
                WorldStreamer.Instance.Unload();
            }
            SaveManager.Detach();
            ResetStatics();
            ShowTitle();
        }

        /// <summary>One line on where the hero is, for the pause menu and the save button.</summary>
        public string PlaceName()
        {
            var region = WorldStreamer.Instance != null ? WorldStreamer.Instance.CurrentRegion : "";
            var place = string.IsNullOrEmpty(region) ? LevelName : Capitalize(region);
            return ActiveCheckpoint >= 0 ? $"{place} checkpoint {ActiveCheckpoint + 1}" : place;
        }

        /// <summary>The level's display name for a save's level id, for the slot picker.</summary>
        public static string LevelNameOf(string levelId) => levelId == LevelId ? LevelName : levelId;

        private void ShowTitle()
        {
            Current = Phase.Title;
            InputRouter.Instance?.UseUi();
            if (music != null)
            {
                music.Play("title");
            }
            if (fade != null)
            {
                fade.FadeIn();
            }
            TitleShown?.Invoke();
        }

        private IEnumerator EnterWorld(SaveData data)
        {
            Current = Phase.Loading;
            if (fade != null)
            {
                yield return fade.FadeOut();
            }
            var streamer = WorldStreamer.Instance;
            var atlas = streamer != null ? streamer.Atlas : null;
            var scene = atlas != null ? WorldAtlas.SceneName(atlas.startRegion) : "";
            string problem = null;
            if (heroPrefab == null)
            {
                problem = "No hero prefab is assigned on GameFlow (Assets/Prefabs/Player.prefab).";
            }
            else if (streamer == null)
            {
                problem = "Main.unity has no WorldStreamer.";
            }
            else if (atlas == null)
            {
                problem = "The WorldStreamer has no world atlas (Assets/Data/World.asset).";
            }
            else if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                problem = $"The region scene '{scene}' is not in the build settings yet.";
            }
            if (problem != null)
            {
                Debug.LogWarning($"GameFlow: cannot enter the world. {problem}");
                SaveManager.Detach();
                transition = null;
                ShowTitle();
                WorldUnavailable?.Invoke(problem);
                yield break;
            }

            Hero = Instantiate(heroPrefab);
            Hero.name = "Hero";
            if (data != null)
            {
                Hero.SetAbilities(data.Abilities);
            }
            Hero.Died += OnHeroDied;
            streamer.RegionChanged -= OnRegionChanged;
            streamer.RegionChanged += OnRegionChanged;
            if (data != null && data.Checkpoint >= 0)
            {
                streamer.ContinueAt(Hero, data.Checkpoint);
            }
            else
            {
                streamer.StartNewGame(Hero);
            }
            yield return null;
            while (streamer.Busy)
            {
                yield return null;
            }
            if (gameCamera != null)
            {
                gameCamera.Target = Hero.transform;
                gameCamera.Snap();
            }
            OnRegionChanged(streamer.Current);
            Current = Phase.Playing;
            InputRouter.Instance?.UsePlayer();
            WorldEntered?.Invoke(Hero);
            if (fade != null)
            {
                yield return fade.FadeIn();
            }
            transition = null;
        }

        private IEnumerator RestartRoutine()
        {
            if (fade != null)
            {
                yield return fade.FadeOut(DeathFadeTime);
            }
            var point = RespawnPoint ?? StartPoint();
            Hero.Respawn(point);
            if (gameCamera != null)
            {
                gameCamera.Snap();
            }
            if (fade != null)
            {
                yield return fade.FadeIn(DeathFadeTime);
            }
            transition = null;
        }

        private static Vector2 StartPoint()
        {
            var atlas = WorldStreamer.Instance != null ? WorldStreamer.Instance.Atlas : null;
            return atlas != null ? atlas.start : Vector2.zero;
        }

        /// <summary>The music follows the region the hero is in: the look's loop, else the region's key.</summary>
        private void OnRegionChanged(RegionRoot region)
        {
            if (music == null || region == null)
            {
                return;
            }
            var look = region.Look;
            music.Play(look != null && !string.IsNullOrEmpty(look.music) ? look.music : region.Id);
        }

        private void OnHeroDied()
        {
            if (Current == Phase.Playing && transition == null)
            {
                transition = StartCoroutine(RestartRoutine());
            }
        }

        private static void ResetStatics()
        {
            StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
        }

        private static string Capitalize(string id)
        {
            var words = id.Replace('_', ' ').Split(' ');
            for (var i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
                }
            }
            return string.Join(" ", words);
        }

        /// <summary>
        /// The menus need an EventSystem driven by the Input System's UI map. Made here
        /// rather than saved in the scene so the action references stay in code.
        /// </summary>
        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }
            var holder = new GameObject("EventSystem");
            holder.transform.SetParent(transform, false);
            holder.SetActive(false);
            holder.AddComponent<EventSystem>();
            var module = holder.AddComponent<InputSystemUIInputModule>();
            var router = InputRouter.Instance;
            if (router != null && router.Actions != null)
            {
                var ui = router.Actions.FindActionMap("UI", true);
                module.actionsAsset = router.Actions;
                module.move = InputActionReference.Create(ui.FindAction("Navigate", true));
                module.submit = InputActionReference.Create(ui.FindAction("Submit", true));
                module.cancel = InputActionReference.Create(ui.FindAction("Cancel", true));
                module.point = InputActionReference.Create(ui.FindAction("Point", true));
                module.leftClick = InputActionReference.Create(ui.FindAction("Click", true));
                module.scrollWheel = null;
                module.middleClick = null;
                module.rightClick = null;
            }
            module.deselectOnBackgroundClick = false;
            holder.SetActive(true);
        }

        public enum Phase
        {
            Title,
            Loading,
            Playing,
            Paused,
        }
    }
}
