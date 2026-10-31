using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Descender.EditorTools.Importers
{
    /// <summary>
    /// Reads the `[resource]` section of a Godot text resource (.tres) into plain
    /// values: numbers as double, strings, bools, Vector2, Color, dictionaries and
    /// lists. Enough to carry the enemy, boss and region resources over without
    /// retyping them; `ExtResource(...)` and other constructors are read as strings.
    /// </summary>
    public sealed class TresParser
    {
        public string ScriptClass { get; private set; } = "";
        public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();

        private readonly string text;
        private int at;

        public static TresParser Parse(string tresText)
        {
            var parser = new TresParser(tresText);
            parser.ReadAll();
            return parser;
        }

        private TresParser(string tresText)
        {
            text = tresText ?? "";
        }

        public double Number(string key, double fallback) => Values.TryGetValue(key, out var v) && v is double d ? d : fallback;
        public float Float(string key, float fallback) => (float)Number(key, fallback);
        public int Int(string key, int fallback) => (int)Math.Round(Number(key, fallback));
        public bool Bool(string key, bool fallback) => Values.TryGetValue(key, out var v) && v is bool b ? b : fallback;
        public string String(string key, string fallback) => Values.TryGetValue(key, out var v) && v is string s ? s : fallback;
        public Vector2 Vector(string key, Vector2 fallback) => Values.TryGetValue(key, out var v) && v is Vector2 vec ? vec : fallback;
        public Color ColorOf(string key, Color fallback) => Values.TryGetValue(key, out var v) && v is Color c ? c : fallback;
        public Dictionary<string, object> Dict(string key) => Values.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        public static double DictNumber(Dictionary<string, object> dict, string key, double fallback) =>
            dict != null && dict.TryGetValue(key, out var v) && v is double d ? d : fallback;

        public static string DictString(Dictionary<string, object> dict, string key, string fallback) =>
            dict != null && dict.TryGetValue(key, out var v) && v is string s ? s : fallback;

        public static List<object> DictList(Dictionary<string, object> dict, string key) =>
            dict != null && dict.TryGetValue(key, out var v) ? v as List<object> : null;

        private void ReadAll()
        {
            var header = text.IndexOf("script_class=\"", StringComparison.Ordinal);
            if (header >= 0)
            {
                var start = header + "script_class=\"".Length;
                var end = text.IndexOf('"', start);
                if (end > start)
                {
                    ScriptClass = text.Substring(start, end - start);
                }
            }
            var section = text.IndexOf("[resource]", StringComparison.Ordinal);
            if (section < 0)
            {
                return;
            }
            at = section + "[resource]".Length;
            while (true)
            {
                SkipSpace();
                if (at >= text.Length || text[at] == '[')
                {
                    return;
                }
                var key = ReadIdentifier();
                SkipSpace();
                if (key.Length == 0 || at >= text.Length || text[at] != '=')
                {
                    SkipLine();
                    continue;
                }
                at++;
                SkipSpace();
                Values[key] = ReadValue();
            }
        }

        private object ReadValue()
        {
            SkipSpace();
            if (at >= text.Length)
            {
                return null;
            }
            var c = text[at];
            if (c == '"')
            {
                return ReadString();
            }
            if (c == '{')
            {
                return ReadDictionary();
            }
            if (c == '[')
            {
                return ReadList();
            }
            if (c == '-' || char.IsDigit(c) || c == '.')
            {
                return ReadNumber();
            }
            var word = ReadIdentifier();
            SkipSpace();
            if (at < text.Length && text[at] == '(')
            {
                var args = ReadArguments();
                switch (word)
                {
                    case "Vector2":
                        return new Vector2(Arg(args, 0), Arg(args, 1));
                    case "Color":
                        return new Color(Arg(args, 0), Arg(args, 1), Arg(args, 2), args.Count > 3 ? Arg(args, 3) : 1f);
                    default:
                        return $"{word}({Join(args)})";
                }
            }
            switch (word)
            {
                case "true": return true;
                case "false": return false;
                case "null": return null;
                default: return word;
            }
        }

        private static float Arg(List<object> args, int index) =>
            index < args.Count && args[index] is double d ? (float)d : 0f;

        private static string Join(List<object> args)
        {
            var parts = new List<string>();
            foreach (var arg in args)
            {
                parts.Add(arg is string s ? $"\"{s}\"" : Convert.ToString(arg, CultureInfo.InvariantCulture));
            }
            return string.Join(", ", parts);
        }

        private List<object> ReadArguments()
        {
            at++; // (
            var args = new List<object>();
            while (true)
            {
                SkipSpace();
                if (at >= text.Length)
                {
                    return args;
                }
                if (text[at] == ')')
                {
                    at++;
                    return args;
                }
                args.Add(ReadValue());
                SkipSpace();
                if (at < text.Length && text[at] == ',')
                {
                    at++;
                }
            }
        }

        private List<object> ReadList()
        {
            at++; // [
            var list = new List<object>();
            while (true)
            {
                SkipSpace();
                if (at >= text.Length)
                {
                    return list;
                }
                if (text[at] == ']')
                {
                    at++;
                    return list;
                }
                list.Add(ReadValue());
                SkipSpace();
                if (at < text.Length && text[at] == ',')
                {
                    at++;
                }
            }
        }

        private Dictionary<string, object> ReadDictionary()
        {
            at++; // {
            var dict = new Dictionary<string, object>();
            while (true)
            {
                SkipSpace();
                if (at >= text.Length)
                {
                    return dict;
                }
                if (text[at] == '}')
                {
                    at++;
                    return dict;
                }
                var key = ReadValue();
                SkipSpace();
                if (at < text.Length && text[at] == ':')
                {
                    at++;
                }
                var value = ReadValue();
                dict[Convert.ToString(key, CultureInfo.InvariantCulture)] = value;
                SkipSpace();
                if (at < text.Length && text[at] == ',')
                {
                    at++;
                }
            }
        }

        private string ReadString()
        {
            at++; // "
            var builder = new StringBuilder();
            while (at < text.Length && text[at] != '"')
            {
                if (text[at] == '\\' && at + 1 < text.Length)
                {
                    at++;
                    switch (text[at])
                    {
                        case 'n': builder.Append('\n'); break;
                        case 't': builder.Append('\t'); break;
                        default: builder.Append(text[at]); break;
                    }
                }
                else
                {
                    builder.Append(text[at]);
                }
                at++;
            }
            at++; // closing "
            return builder.ToString();
        }

        private object ReadNumber()
        {
            var start = at;
            while (at < text.Length && (char.IsDigit(text[at]) || text[at] == '.' || text[at] == '-' || text[at] == 'e' || text[at] == 'E' || text[at] == '+'))
            {
                at++;
            }
            var token = text.Substring(start, at - start);
            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0;
        }

        private string ReadIdentifier()
        {
            var start = at;
            while (at < text.Length && (char.IsLetterOrDigit(text[at]) || text[at] == '_' || text[at] == '/'))
            {
                at++;
            }
            return text.Substring(start, at - start);
        }

        private void SkipSpace()
        {
            while (at < text.Length)
            {
                if (char.IsWhiteSpace(text[at]))
                {
                    at++;
                }
                else if (text[at] == ';')
                {
                    SkipLine();
                }
                else
                {
                    return;
                }
            }
        }

        private void SkipLine()
        {
            while (at < text.Length && text[at] != '\n')
            {
                at++;
            }
        }
    }
}
