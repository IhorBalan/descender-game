using Descender.Story;
using NUnit.Framework;

namespace Descender.Tests
{
    public class StoryTests
    {
        [SetUp]
        public void Reset() => StoryFlags.Reset();

        [Test]
        public void FlagsAreSetOnceAndNeverEmpty()
        {
            Assert.IsFalse(StoryFlags.Has("a"));
            StoryFlags.Set("a");
            StoryFlags.Set("a");
            StoryFlags.Set("");
            StoryFlags.Set(null);
            Assert.IsTrue(StoryFlags.Has("a"));
            Assert.AreEqual(1, StoryFlags.Count);
        }

        [Test]
        public void HasAllWantsEveryFlagAndHasAnyWantsOne()
        {
            StoryFlags.Set("a");
            Assert.IsTrue(StoryFlags.HasAll(new[] { "a" }));
            Assert.IsFalse(StoryFlags.HasAll(new[] { "a", "b" }));
            Assert.IsTrue(StoryFlags.HasAny(new[] { "b", "a" }));
            Assert.IsFalse(StoryFlags.HasAny(new[] { "b" }));
            Assert.IsTrue(StoryFlags.HasAll(new string[0]));
            Assert.IsFalse(StoryFlags.HasAny(new string[0]));
        }

        [Test]
        public void TheListForTheSaveIsSortedAndASaveReplacesIt()
        {
            StoryFlags.Set("c");
            StoryFlags.Set("a");
            StoryFlags.Set("b");
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, StoryFlags.ToList());
            StoryFlags.FromList(new[] { "z", "", "7" });
            CollectionAssert.AreEqual(new[] { "7", "z" }, StoryFlags.ToList());
            StoryFlags.Reset();
            Assert.AreEqual(0, StoryFlags.Count);
        }
    }
}
