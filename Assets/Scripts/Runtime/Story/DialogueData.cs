using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Descender.Story
{
    /// <summary>One box of text: who says it and what.</summary>
    public readonly struct DialogueLine
    {
        public readonly string Who;
        public readonly string Text;

        public DialogueLine(string who, string text)
        {
            Who = who ?? "";
            Text = text ?? "";
        }
    }

    /// <summary>One option offered at the end of a line: its text and the entry it leads to.</summary>
    public readonly struct DialogueChoice
    {
        public readonly string Text;
        public readonly string Then;

        public DialogueChoice(string text, string then)
        {
            Text = text ?? "";
            Then = then ?? "";
        }
    }

    /// <summary>
    /// The dialogue of one level, read from a JSON file (see Assets/Data/dialogue/world_1.json,
    /// the file the Godot game used unchanged).
    ///
    /// Each entry is keyed by id and has:
    ///   lines     a list of {"who": speaker, "text": one box}; a line may carry
    ///             `requires` / `unless` of its own and is left out when its flags
    ///             do not hold
    ///   requires  a flag, or a list of flags, that must all be set (optional)
    ///   unless    a flag, or a list of flags, that must all be unset (optional)
    ///   sets      a flag, or a list of flags, set when the entry has been read to the end
    ///   then      the id of the entry that plays right after this one
    ///   warp      "start": the hero goes back to the level's start when the entry ends
    ///   save      true for an entry that saves the game when it ends
    ///   gives     an upgrade kind (heart, vial, iron) handed over when the entry ends
    ///   once      true for an entry that plays a single time per save; every entry read
    ///             to the end sets `seen:<id>`, and a `once` entry is skipped when it is set
    ///
    /// An NPC or trigger lists several ids in priority order; <see cref="Pick"/> returns
    /// the first whose flags match, so lines change as the story moves on.
    /// </summary>
    public sealed class DialogueData
    {
        public string Path { get; }
        public JObject Entries { get; }

        public DialogueData(string path, string json)
        {
            Path = path;
            Entries = Parse(json);
        }

        public static JObject Parse(string json)
        {
            try
            {
                return string.IsNullOrEmpty(json) ? new JObject() : JObject.Parse(json);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return new JObject();
            }
        }

        public int Count => Entries.Count;

        public bool HasEntry(string id) => !string.IsNullOrEmpty(id) && Entries[id] is JObject;

        public IEnumerable<string> Ids()
        {
            foreach (var property in Entries.Properties())
            {
                yield return property.Name;
            }
        }

        /// <summary>
        /// The entry with this id, with its id filled in and the lines the story flags
        /// leave out dropped, or null.
        /// </summary>
        public JObject Entry(string id)
        {
            if (!HasEntry(id))
            {
                return null;
            }
            var found = (JObject)Entries[id];
            if (!(found["lines"] is JArray lines))
            {
                return null;
            }
            var result = (JObject)found.DeepClone();
            result["id"] = id;
            result["lines"] = VisibleLines(lines);
            return result;
        }

        /// <summary>The lines whose own flag conditions hold right now; a line without any always does.</summary>
        public static JArray VisibleLines(JArray lines)
        {
            var result = new JArray();
            foreach (var raw in lines)
            {
                if (raw is JObject line)
                {
                    if (!StoryFlags.HasAll(Flags(line["requires"])))
                    {
                        continue;
                    }
                    if (StoryFlags.HasAny(Flags(line["unless"])))
                    {
                        continue;
                    }
                }
                result.Add(raw.DeepClone());
            }
            return result;
        }

        /// <summary>Whether the entry's flag conditions hold right now, and it has a line left to say.</summary>
        public static bool Available(JObject data)
        {
            if (data == null || LineCount(data) == 0)
            {
                return false;
            }
            if (data.Value<bool?>("once") == true && StoryFlags.Has(SeenFlag(data.Value<string>("id") ?? "")))
            {
                return false;
            }
            return StoryFlags.HasAll(Flags(data["requires"])) && !StoryFlags.HasAny(Flags(data["unless"]));
        }

        /// <summary>A flag field as a list: one flag, a list of them, or nothing. Empty and null flags are dropped.</summary>
        public static List<string> Flags(JToken value)
        {
            var result = new List<string>();
            if (value == null || value.Type == JTokenType.Null)
            {
                return result;
            }
            if (value is JArray array)
            {
                foreach (var item in array)
                {
                    AddFlag(result, item);
                }
            }
            else
            {
                AddFlag(result, value);
            }
            return result;
        }

        private static void AddFlag(List<string> into, JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return;
            }
            var text = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                into.Add(text);
            }
        }

        /// <summary>
        /// A line may carry `choices`: a list of {text, then}. The box offers them once the
        /// line is typed out, and the picked one's `then` becomes the entry's `then`.
        /// </summary>
        public static List<DialogueChoice> Choices(JObject data, int index)
        {
            var result = new List<DialogueChoice>();
            if (!(data?["lines"] is JArray lines) || index < 0 || index >= lines.Count)
            {
                return result;
            }
            if (!(lines[index] is JObject line) || !(line["choices"] is JArray choices))
            {
                return result;
            }
            foreach (var raw in choices)
            {
                if (raw is JObject choice)
                {
                    result.Add(new DialogueChoice(choice.Value<string>("text"), choice.Value<string>("then")));
                }
            }
            return result;
        }

        /// <summary>The entry to play right after this one, or "".</summary>
        public static string NextId(JObject data) => data?.Value<string>("then") ?? "";

        /// <summary>The flags an entry sets when it has been read to the end.</summary>
        public static List<string> Sets(JObject data) => Flags(data?["sets"]);

        /// <summary>The upgrade kind an entry gives when it ends, or "".</summary>
        public static string Gives(JObject data) => data?.Value<string>("gives") ?? "";

        public static bool Saves(JObject data) => data?.Value<bool?>("save") == true;

        public static string Warp(JObject data) => data?.Value<string>("warp") ?? "";

        /// <summary>The flag set once an entry has been read to the end.</summary>
        public static string SeenFlag(string id) => $"seen:{id}";

        /// <summary>The first of the ids whose conditions hold, or null when none does.</summary>
        public JObject Pick(IEnumerable<string> ids)
        {
            if (ids == null)
            {
                return null;
            }
            foreach (var id in ids)
            {
                var data = Entry(id);
                if (Available(data))
                {
                    return data;
                }
            }
            return null;
        }

        /// <summary>The speaker and text of one box, with missing fields filled in.</summary>
        public static DialogueLine Line(JObject data, int index)
        {
            if (!(data?["lines"] is JArray lines) || index < 0 || index >= lines.Count)
            {
                return new DialogueLine("", "");
            }
            var raw = lines[index];
            if (raw is JObject line)
            {
                return new DialogueLine(line.Value<string>("who"), line.Value<string>("text"));
            }
            return new DialogueLine("", raw.ToString());
        }

        public static int LineCount(JObject data) => data?["lines"] is JArray lines ? lines.Count : 0;
    }
}
