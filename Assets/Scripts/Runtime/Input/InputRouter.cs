using UnityEngine;
using UnityEngine.InputSystem;

namespace Descender.Input
{
    /// <summary>
    /// The one owner of the Descender.inputactions asset, in Main.unity. Play code
    /// reads the typed accessors in Update and buffers for FixedUpdate; menus
    /// switch to the UI map with <see cref="UseUi"/>. Binding overrides from the
    /// settings are applied here on boot. Actions are looked up by name from the
    /// asset so no generated wrapper is needed.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class InputRouter : MonoBehaviour
    {
        public static InputRouter Instance { get; private set; }

        [SerializeField]
        private InputActionAsset actions;

        private InputActionMap player;
        private InputActionMap ui;
        private InputAction moveLeft;
        private InputAction moveRight;
        private InputAction moveDown;
        private InputAction jump;
        private InputAction attack;
        private InputAction dash;
        private InputAction cast;
        private InputAction heal;
        private InputAction interact;
        private InputAction pause;
        private InputAction restart;

        public InputActionAsset Actions => actions;

        /// <summary>The device the player last used, for the prompt glyphs.</summary>
        public InputDevice LastDevice { get; private set; }

        public bool UsingGamepad => LastDevice is Gamepad;

        public float MoveX => (moveRight.IsPressed() ? 1f : 0f) - (moveLeft.IsPressed() ? 1f : 0f);
        public bool DownHeld => moveDown.IsPressed();
        public bool JumpPressed => jump.WasPressedThisFrame();
        public bool JumpHeld => jump.IsPressed();
        public bool JumpReleased => jump.WasReleasedThisFrame();
        public bool AttackPressed => attack.WasPressedThisFrame();
        public bool DashPressed => dash.WasPressedThisFrame();
        public bool CastPressed => cast.WasPressedThisFrame();
        public bool HealPressed => heal.WasPressedThisFrame();
        public bool InteractPressed => interact.WasPressedThisFrame();
        public bool PausePressed => pause.WasPressedThisFrame();
        public bool RestartPressed => restart.WasPressedThisFrame();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (actions == null)
            {
                Debug.LogError("InputRouter has no actions asset; assign Assets/Settings/Descender.inputactions.");
                enabled = false;
                return;
            }
            player = actions.FindActionMap("Player", true);
            ui = actions.FindActionMap("UI", true);
            moveLeft = player.FindAction("MoveLeft", true);
            moveRight = player.FindAction("MoveRight", true);
            moveDown = player.FindAction("MoveDown", true);
            jump = player.FindAction("Jump", true);
            attack = player.FindAction("Attack", true);
            dash = player.FindAction("Dash", true);
            cast = player.FindAction("Cast", true);
            heal = player.FindAction("Heal", true);
            interact = player.FindAction("Interact", true);
            pause = player.FindAction("Pause", true);
            restart = player.FindAction("Restart", true);

            var bindings = Save.SettingsData.Current.bindings;
            if (!string.IsNullOrEmpty(bindings))
            {
                actions.LoadBindingOverridesFromJson(bindings);
            }
            UsePlayer();
        }

        private void OnEnable()
        {
            InputSystem.onActionChange += OnActionChange;
        }

        private void OnDisable()
        {
            InputSystem.onActionChange -= OnActionChange;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void UsePlayer()
        {
            ui?.Disable();
            player?.Enable();
        }

        public void UseUi()
        {
            player?.Disable();
            ui?.Enable();
        }

        /// <summary>Stores the current overrides in the settings and saves them.</summary>
        public void SaveBindings()
        {
            Save.SettingsData.Current.bindings = actions.SaveBindingOverridesAsJson();
            Save.SettingsData.Current.Save();
        }

        public void ResetBindings()
        {
            actions.RemoveAllBindingOverrides();
            SaveBindings();
        }

        private void OnActionChange(object action, InputActionChange change)
        {
            if (change == InputActionChange.ActionPerformed && action is InputAction performed)
            {
                LastDevice = performed.activeControl?.device;
            }
        }
    }
}
