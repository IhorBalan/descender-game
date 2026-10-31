using System.Collections.Generic;

namespace Descender.Story
{
    public sealed class QuestStep
    {
        public string Text;
        public string Flag;

        public QuestStep(string text, string flag)
        {
            Text = text;
            Flag = flag;
        }
    }

    public sealed class Quest
    {
        public string Id;
        public string Title;
        public string Start;
        public string Done;
        public QuestStep[] Steps;
    }

    /// <summary>
    /// The promises the hero makes on the way down: the side quests, read off the
    /// story flags. A quest starts when its `start` flag is set, is done when its
    /// `done` flag is, and its steps are the flags set along the way, so the log in
    /// the pause menu needs nothing but <see cref="StoryFlags"/>.
    /// </summary>
    public static class Quests
    {
        public static readonly Quest[] List =
        {
            new Quest
            {
                Id = "scarf",
                Title = "The red scarf",
                Start = "quest:scarf",
                Done = "quest:scarf:done",
                Steps = new[]
                {
                    new QuestStep("Find the lad in the red scarf, down in Violence", "item:scarf"),
                    new QuestStep("Bring the scarf back to Corporal Dace", "quest:scarf:done"),
                },
            },
            new Quest
            {
                Id = "drawings",
                Title = "Hanne's drawings",
                Start = "quest:drawings",
                Done = "quest:drawings:done",
                Steps = new[]
                {
                    new QuestStep("Tell Hanne about the names in the shaft", "drawing:shaft"),
                    new QuestStep("Tell Hanne about the wall that says DON'T FOLLOW", "drawing:wall"),
                    new QuestStep("Tell Hanne about the pipes", "drawing:pipes"),
                    new QuestStep("Tell Hanne what Tobiah looks like", "quest:drawings:done"),
                },
            },
            new Quest
            {
                Id = "wife",
                Title = "Orla Pell",
                Start = "quest:wife",
                Done = "quest:wife:done",
                Steps = new[]
                {
                    new QuestStep("Ask the gathered in the orchard about Orla", "wife_word"),
                    new QuestStep("Tell Old Pell what she said", "quest:wife:done"),
                },
            },
            new Quest
            {
                Id = "song",
                Title = "Ithuriel's song",
                Start = "quest:song",
                Done = "quest:song:done",
                Steps = new[]
                {
                    new QuestStep("Ask below where the song comes from", "song_word"),
                    new QuestStep("Tell Ithuriel what he is humming", "quest:song:done"),
                },
            },
        };

        /// <summary>The quest a flag starts, or null.</summary>
        public static Quest StartedBy(string flag)
        {
            foreach (var quest in List)
            {
                if (quest.Start == flag)
                {
                    return quest;
                }
            }
            return null;
        }

        /// <summary>The quest a flag finishes, or null.</summary>
        public static Quest FinishedBy(string flag)
        {
            foreach (var quest in List)
            {
                if (quest.Done == flag)
                {
                    return quest;
                }
            }
            return null;
        }

        /// <summary>The quests the hero has taken on, open ones first.</summary>
        public static List<Quest> Active()
        {
            var open = new List<Quest>();
            var done = new List<Quest>();
            foreach (var quest in List)
            {
                if (!StoryFlags.Has(quest.Start))
                {
                    continue;
                }
                (StoryFlags.Has(quest.Done) ? done : open).Add(quest);
            }
            open.AddRange(done);
            return open;
        }

        public static bool IsDone(Quest quest) => StoryFlags.Has(quest.Done);

        /// <summary>The steps of a quest with whether each is done.</summary>
        public static List<(string text, bool done)> Steps(Quest quest)
        {
            var result = new List<(string, bool)>();
            foreach (var step in quest.Steps)
            {
                result.Add((step.Text, StoryFlags.Has(step.Flag)));
            }
            return result;
        }
    }
}
