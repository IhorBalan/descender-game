using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Descender.Save;
using UnityEngine.InputSystem;

namespace Descender.UI
{
    /// <summary>
    /// Button prompts for whichever device the player last touched. A hint is a
    /// template with `{action}` tokens, one per input action ("{interact} talk",
    /// "{navigate} pick   {ui_accept} chooses"); <see cref="Render"/> turns each into
    /// the key's name on the keyboard or the button's name on a pad, in the Xbox or
    /// PlayStation layout (the pad's name decides, or the Pad prompts setting pins
    /// one), written in the key colour. A token no action answers to is left as it
    /// is, so a typo shows. The Godot action names (`ui_accept`, `move_left`) are
    /// understood beside the Unity ones. Port of scripts/prompts.gd; the icons under
    /// Assets/Art/UI/Prompts wait for a TMP sprite asset.
    /// </summary>
    public static class Prompts
    {
        private static readonly Regex Token = new Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

        /// <summary>Godot action names, and shorthand, to the Input System action names.</summary>
        public static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ui_accept"] = "Submit",
            ["ui_cancel"] = "Cancel",
            ["ui_left"] = "PageLeft",
            ["ui_right"] = "PageRight",
            ["page_left"] = "PageLeft",
            ["page_right"] = "PageRight",
            ["move_left"] = "MoveLeft",
            ["move_right"] = "MoveRight",
            ["move_down"] = "MoveDown",
            ["dev_console"] = "Console",
        };

        /// <summary>Button names in the Xbox layout, by the control's name under &lt;Gamepad&gt;/.</summary>
        public static readonly Dictionary<string, string> XboxNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["buttonSouth"] = "A",
            ["buttonEast"] = "B",
            ["buttonWest"] = "X",
            ["buttonNorth"] = "Y",
            ["leftShoulder"] = "LB",
            ["rightShoulder"] = "RB",
            ["leftTrigger"] = "LT",
            ["rightTrigger"] = "RT",
            ["start"] = "Menu",
            ["select"] = "View",
            ["leftStickPress"] = "LS",
            ["rightStickPress"] = "RS",
        };

        /// <summary>The same in the PlayStation layout.</summary>
        public static readonly Dictionary<string, string> PlayStationNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["buttonSouth"] = "Cross",
            ["buttonEast"] = "Circle",
            ["buttonWest"] = "Square",
            ["buttonNorth"] = "Triangle",
            ["leftShoulder"] = "L1",
            ["rightShoulder"] = "R1",
            ["leftTrigger"] = "L2",
            ["rightTrigger"] = "R2",
            ["start"] = "Options",
            ["select"] = "Share",
            ["leftStickPress"] = "L3",
            ["rightStickPress"] = "R3",
        };

        /// <summary>Key names shortened to fit a hint.</summary>
        public static readonly Dictionary<string, string> ShortKeyNames = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Escape"] = "Esc",
            ["Backspace"] = "Bksp",
            ["Delete"] = "Del",
            ["Insert"] = "Ins",
            ["Page Up"] = "PgUp",
            ["Page Down"] = "PgDn",
            ["Left Shift"] = "Shift",
            ["Right Shift"] = "Shift",
            ["Left Arrow"] = "Left",
            ["Right Arrow"] = "Right",
            ["Up Arrow"] = "Up",
            ["Down Arrow"] = "Down",
            ["Backquote"] = "`",
        };

        /// <summary>Words in a pad's name that mark it as a PlayStation one.</summary>
        public static readonly string[] PlayStationMarks =
        {
            "playstation",
            "dualshock",
            "dualsense",
            "sony",
            "ps2",
            "ps3",
            "ps4",
            "ps5",
            "wireless controller",
        };

        /// <summary>Whether the prompts show pad buttons right now.</summary>
        public static bool UsingGamepad
        {
            get
            {
                var router = Input.InputRouter.Instance;
                return router != null && router.UsingGamepad;
            }
        }

        /// <summary>The pad layout the prompts draw: the setting, or the pad's own when it is Auto.</summary>
        public static PromptLayout Layout
        {
            get
            {
                var pinned = SettingsData.Current.prompts;
                if (pinned != PromptLayout.Auto)
                {
                    return pinned;
                }
                return IsPlayStation(Gamepad.current?.name) ? PromptLayout.PlayStation : PromptLayout.Xbox;
            }
        }

        public static bool IsPlayStation(string padName)
        {
            if (string.IsNullOrEmpty(padName))
            {
                return false;
            }
            var lower = padName.ToLowerInvariant();
            foreach (var mark in PlayStationMarks)
            {
                if (lower.Contains(mark))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Renders a template for the device in the player's hands.</summary>
        public static string Render(string template) => Render(template, DisplayName);

        /// <summary>
        /// Renders a template with `nameOf` naming each action; a null name leaves the
        /// token as it was. Names are wrapped in the key colour.
        /// </summary>
        public static string Render(string template, Func<string, string> nameOf)
        {
            if (string.IsNullOrEmpty(template))
            {
                return "";
            }
            return Token.Replace(
                template,
                match =>
                {
                    var name = nameOf(match.Groups[1].Value);
                    return name == null ? match.Value : $"<color={UiTheme.KeyColorHex}>{name}</color>";
                }
            );
        }

        /// <summary>The Input System action a token names, or null.</summary>
        public static InputAction ActionFor(string token)
        {
            var router = Input.InputRouter.Instance;
            if (router == null || router.Actions == null || string.IsNullOrEmpty(token))
            {
                return null;
            }
            var name = Aliases.TryGetValue(token, out var alias) ? alias : token;
            foreach (var map in router.Actions.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    if (string.Equals(action.name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return action;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// What a token shows right now: "Arrows" or "D-pad" for `navigate`, the move
        /// keys or the stick for `move`, else the first binding of the action for the
        /// device in use. Null for a token nothing answers to.
        /// </summary>
        public static string DisplayName(string token)
        {
            var pad = UsingGamepad;
            switch (token.ToLowerInvariant())
            {
                case "navigate":
                    return pad ? "D-pad" : "Arrows";
                case "move":
                    return pad ? "Stick" : Join(DisplayName("MoveLeft"), DisplayName("MoveRight"));
            }
            var action = ActionFor(token);
            return action == null ? null : NameOf(action, pad, Layout);
        }

        /// <summary>The name of an action's first binding for a device, in a layout.</summary>
        public static string NameOf(InputAction action, bool gamepad, PromptLayout layout)
        {
            var group = gamepad ? "Gamepad" : "Keyboard";
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.isComposite)
                {
                    continue;
                }
                var inGroup = (binding.groups ?? "").Contains(group);
                var path = binding.effectivePath ?? "";
                var onDevice = gamepad ? path.StartsWith("<Gamepad>", StringComparison.Ordinal) : path.StartsWith("<Keyboard>", StringComparison.Ordinal);
                if (!inGroup && !onDevice)
                {
                    continue;
                }
                return gamepad ? PadName(path, layout) : KeyName(action, i);
            }
            return null;
        }

        /// <summary>The short name of a pad control path such as "&lt;Gamepad&gt;/buttonSouth".</summary>
        public static string PadName(string path, PromptLayout layout)
        {
            var slash = path.IndexOf('/');
            var control = slash >= 0 ? path.Substring(slash + 1) : path;
            var names = layout == PromptLayout.PlayStation ? PlayStationNames : XboxNames;
            if (names.TryGetValue(control, out var name))
            {
                return name;
            }
            switch (control)
            {
                case "dpad":
                    return "D-pad";
                case "dpad/up":
                    return "D-Up";
                case "dpad/down":
                    return "D-Down";
                case "dpad/left":
                    return "D-Left";
                case "dpad/right":
                    return "D-Right";
                case "leftStick":
                    return "Stick";
                case "leftStick/left":
                    return "Stick left";
                case "leftStick/right":
                    return "Stick right";
                case "leftStick/up":
                    return "Stick up";
                case "leftStick/down":
                    return "Stick down";
                case "rightStick":
                    return "Right stick";
            }
            return control;
        }

        private static string KeyName(InputAction action, int bindingIndex)
        {
            var name = action.GetBindingDisplayString(bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            return ShortKeyNames.TryGetValue(name, out var shorter) ? shorter : name;
        }

        private static string Join(string a, string b)
        {
            if (a == null)
            {
                return b;
            }
            if (b == null)
            {
                return a;
            }
            return $"{a}/{b}";
        }

        /// <summary>Strips the colour tags, for a test or a plain log.</summary>
        public static string Plain(string rendered)
        {
            var builder = new StringBuilder(rendered.Length);
            var inTag = false;
            foreach (var c in rendered)
            {
                if (c == '<')
                {
                    inTag = true;
                }
                else if (c == '>')
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    builder.Append(c);
                }
            }
            return builder.ToString();
        }
    }
}
