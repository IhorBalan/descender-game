using Descender.Story;
using NUnit.Framework;

namespace Descender.Tests
{
    public class QuestsTests
    {
        [SetUp]
        public void Reset() => StoryFlags.Reset();

        [Test]
        public void FlagsNameTheirQuests()
        {
            Assert.IsEmpty(Quests.Active());
            Assert.AreEqual("scarf", Quests.StartedBy("quest:scarf").Id);
            Assert.AreEqual("scarf", Quests.FinishedBy("quest:scarf:done").Id);
            Assert.IsNull(Quests.StartedBy("reveal"));
        }

        [Test]
        public void OpenPromisesFirstAndStepsTick()
        {
            StoryFlags.Set("quest:scarf");
            StoryFlags.Set("quest:wife");
            StoryFlags.Set("quest:wife:done");
            var active = Quests.Active();
            Assert.AreEqual(2, active.Count);
            Assert.AreEqual("scarf", active[0].Id);
            Assert.AreEqual("wife", active[1].Id);
            Assert.IsFalse(Quests.IsDone(active[0]));
            Assert.IsTrue(Quests.IsDone(active[1]));
            StoryFlags.Set("item:scarf");
            var steps = Quests.Steps(active[0]);
            Assert.AreEqual(2, steps.Count);
            Assert.IsTrue(steps[0].done);
            Assert.IsFalse(steps[1].done);
        }

        [Test]
        public void TheLastStepIsThePromiseKept()
        {
            foreach (var quest in Quests.List)
            {
                Assert.AreEqual(quest.Done, quest.Steps[quest.Steps.Length - 1].Flag, quest.Id);
            }
        }
    }
}
