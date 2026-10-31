using System.Collections.Generic;
using System.IO;
using Descender.Enemies;
using Descender.Story;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Descender.Tests
{
    /// <summary>
    /// The data half: reads the dialogue file the game reads and checks that every
    /// name in it resolves. A failing check names the offending entry.
    /// </summary>
    public class DialogueFileTests
    {
        public static readonly string DialoguePath = Path.Combine(Application.dataPath, "Data/dialogue/world_1.json");
        private static readonly string[] UpgradeKinds = { "heart", "vial", "iron" };

        /// <summary>
        /// The flags the code sets without a dialogue entry: their prefixes. `item:` is
        /// here until the relic placements are in the Unity region scenes (Phase 3), when
        /// the relics are read from the scenes as the Godot test read placements.json.
        /// </summary>
        private static readonly string[] CodeFlagPrefixes =
        {
            "boss:",
            "secret:",
            "upgrade:",
            "sword:",
            "seen:",
            "room:",
            "item:",
        };

        private DialogueData dialogue;
        private HashSet<string> setBy;

        [SetUp]
        public void Load()
        {
            StoryFlags.Reset();
            dialogue = new DialogueData(DialoguePath, File.ReadAllText(DialoguePath));
            setBy = new HashSet<string>();
            foreach (var id in dialogue.Ids())
            {
                setBy.Add(DialogueData.SeenFlag(id));
                foreach (var flag in DialogueData.Sets((JObject)dialogue.Entries[id]))
                {
                    setBy.Add(flag);
                }
            }
            foreach (var id in BossIds.All)
            {
                setBy.Add(BossIds.FlagFor(id));
            }
        }

        private bool FlagKnown(string flag)
        {
            if (setBy.Contains(flag))
            {
                return true;
            }
            foreach (var prefix in CodeFlagPrefixes)
            {
                if (flag.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        [Test]
        public void TheDialogueHasItsEntries()
        {
            Assert.Greater(dialogue.Count, 100);
        }

        [Test]
        public void EveryEntryResolves()
        {
            var bad = new List<string>();
            var choices = 0;
            foreach (var id in dialogue.Ids())
            {
                var raw = (JObject)dialogue.Entries[id];
                var lines = raw["lines"] as JArray;
                if (lines == null || lines.Count == 0)
                {
                    bad.Add($"{id}: no lines");
                    continue;
                }
                var next = DialogueData.NextId(raw);
                if (next.Length > 0 && !dialogue.HasEntry(next))
                {
                    bad.Add($"{id}: then '{next}' does not exist");
                }
                var gift = DialogueData.Gives(raw);
                if (gift.Length > 0 && System.Array.IndexOf(UpgradeKinds, gift) < 0)
                {
                    bad.Add($"{id}: gives an unknown upgrade '{gift}'");
                }
                var warp = DialogueData.Warp(raw);
                if (warp.Length > 0 && warp != "start")
                {
                    bad.Add($"{id}: warps to an unknown place '{warp}'");
                }
                foreach (var key in new[] { "requires", "unless" })
                {
                    foreach (var flag in DialogueData.Flags(raw[key]))
                    {
                        if (!FlagKnown(flag))
                        {
                            bad.Add($"{id}: {key} '{flag}', which nothing sets");
                        }
                    }
                }
                for (var i = 0; i < lines.Count; i++)
                {
                    if (!(lines[i] is JObject line))
                    {
                        continue;
                    }
                    if (string.IsNullOrEmpty(line.Value<string>("text")))
                    {
                        bad.Add($"{id}: line {i} has no text");
                    }
                    foreach (var key in new[] { "requires", "unless" })
                    {
                        foreach (var flag in DialogueData.Flags(line[key]))
                        {
                            if (!FlagKnown(flag))
                            {
                                bad.Add($"{id}: line {i} {key} '{flag}', which nothing sets");
                            }
                        }
                    }
                    foreach (var choice in DialogueData.Choices(raw, i))
                    {
                        choices++;
                        if (choice.Text.Length == 0)
                        {
                            bad.Add($"{id}: line {i} has a choice without text");
                        }
                        if (choice.Then.Length == 0 || !dialogue.HasEntry(choice.Then))
                        {
                            bad.Add($"{id}: line {i} has a choice leading to '{choice.Then}'");
                        }
                    }
                }
            }
            Assert.GreaterOrEqual(choices, 3, "the script has its choices");
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void EveryQuestFlagIsSetSomewhere()
        {
            var bad = new List<string>();
            foreach (var quest in Quests.List)
            {
                foreach (var flag in new[] { quest.Start, quest.Done })
                {
                    if (!FlagKnown(flag))
                    {
                        bad.Add($"quest {quest.Id}: '{flag}' is set by nothing");
                    }
                }
                foreach (var step in quest.Steps)
                {
                    if (!FlagKnown(step.Flag))
                    {
                        bad.Add($"quest {quest.Id}: step '{step.Flag}' is set by nothing");
                    }
                }
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void EveryBossHasItsIntroAndItsLastLines()
        {
            var bad = new List<string>();
            foreach (var id in BossIds.All)
            {
                foreach (var part in new[] { "intro", "defeat" })
                {
                    if (dialogue.Entry($"{id}_{part}") == null)
                    {
                        bad.Add($"{id} has no {part} lines");
                    }
                }
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}
