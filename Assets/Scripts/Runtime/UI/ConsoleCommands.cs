using System;
using System.Collections.Generic;

namespace Descender.UI
{
    /// <summary>
    /// The developer console's command registry, apart from its panel so it can be
    /// tested: commands by name, each a function of the words typed after the name
    /// returning the text to print (null prints "ok"), with a usage line and a
    /// sentence of help. `help` and `clear` are built in; the console adds `show_boxes`.
    /// </summary>
    public sealed class ConsoleCommands
    {
        public const string DoneText = "ok";

        public sealed class Command
        {
            public string Name;
            public string Usage;
            public string Help;
            public Func<string[], string> Run;
        }

        private readonly Dictionary<string, Command> commands = new Dictionary<string, Command>(StringComparer.Ordinal);

        /// <summary>Fired by `clear`, for the panel to empty its output.</summary>
        public event Action Cleared;

        public ConsoleCommands()
        {
            Register("help", Help, "help [command]", "Lists the commands, or explains one.");
            Register(
                "clear",
                _ =>
                {
                    Cleared?.Invoke();
                    return "";
                },
                "clear",
                "Clears the output."
            );
        }

        /// <summary>Adds a command; registering a name again replaces the earlier one.</summary>
        public void Register(string name, Func<string[], string> run, string usage = "", string help = "")
        {
            commands[name] = new Command
            {
                Name = name,
                Usage = string.IsNullOrEmpty(usage) ? name : usage,
                Help = help ?? "",
                Run = run,
            };
        }

        public void Unregister(string name) => commands.Remove(name);

        public bool Has(string name) => commands.ContainsKey(name);

        /// <summary>The registered command names, sorted.</summary>
        public List<string> Names()
        {
            var names = new List<string>(commands.Keys);
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>Whether an answer is an error the panel should colour as one.</summary>
        public static bool IsError(string answer) =>
            answer.StartsWith("Unknown command", StringComparison.Ordinal)
            || answer.StartsWith("Usage:", StringComparison.Ordinal);

        /// <summary>Runs one typed line and returns what it printed.</summary>
        public string Run(string line)
        {
            var words = (line ?? "").Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return "";
            }
            if (!commands.TryGetValue(words[0], out var command))
            {
                return $"Unknown command '{words[0]}'. Type `help` for the list.";
            }
            var args = new string[words.Length - 1];
            Array.Copy(words, 1, args, 0, args.Length);
            var result = command.Run(args);
            return result ?? DoneText;
        }

        private string Help(string[] args)
        {
            if (args.Length >= 1)
            {
                if (!commands.TryGetValue(args[0], out var command))
                {
                    return $"Unknown command '{args[0]}'. Type `help` for the list.";
                }
                return $"{command.Usage}\n  {command.Help}";
            }
            var rows = new List<string>();
            foreach (var name in Names())
            {
                var command = commands[name];
                rows.Add($"{MenuText.Pad(command.Usage, 22)} {command.Help}");
            }
            return string.Join("\n", rows);
        }
    }
}
