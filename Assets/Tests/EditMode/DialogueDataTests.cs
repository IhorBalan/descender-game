using Descender.Story;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Descender.Tests
{
    public class DialogueDataTests
    {
        private const string Json = @"{
            ""plain"": {""lines"": [{""who"": ""A"", ""text"": ""one""}, ""a bare line""]},
            ""gated"": {""requires"": [""key""], ""unless"": ""lock"", ""lines"": [{""who"": """", ""text"": ""x""}]},
            ""single"": {""once"": true, ""sets"": [""s1"", ""s2""], ""then"": ""plain"", ""lines"": [{""text"": ""y""}]},
            ""picky"": {""lines"": [
                {""who"": ""A"", ""text"": ""always""},
                {""who"": ""A"", ""text"": ""with key"", ""requires"": ""key""},
                {""who"": ""A"", ""text"": ""without lock"", ""unless"": [""lock""]}
            ]},
            ""choice"": {""lines"": [{""who"": ""A"", ""text"": ""pick"", ""choices"": [
                {""text"": ""left"", ""then"": ""plain""}, {""text"": ""right"", ""then"": ""gated""}]}]},
            ""empty"": {""lines"": []},
            ""broken"": {""lines"": ""not a list""}
        }";

        private DialogueData dialogue;

        [SetUp]
        public void Load()
        {
            StoryFlags.Reset();
            dialogue = new DialogueData("scratch", Json);
        }

        [Test]
        public void TheFileReadsAndEntriesCarryTheirIdAndLines()
        {
            Assert.AreEqual(7, dialogue.Count);
            var plain = dialogue.Entry("plain");
            Assert.AreEqual("plain", plain.Value<string>("id"));
            var first = DialogueData.Line(plain, 0);
            Assert.AreEqual("A", first.Who);
            Assert.AreEqual("one", first.Text);
            var bare = DialogueData.Line(plain, 1);
            Assert.AreEqual("", bare.Who);
            Assert.AreEqual("a bare line", bare.Text);
            Assert.AreEqual("", DialogueData.Line(plain, 9).Text);
            Assert.IsNull(dialogue.Entry("missing"));
            Assert.IsNull(dialogue.Entry("broken"));
        }

        [Test]
        public void AvailabilityFollowsTheFlags()
        {
            Assert.IsFalse(DialogueData.Available(dialogue.Entry("empty")));
            Assert.IsFalse(DialogueData.Available(dialogue.Entry("gated")));
            StoryFlags.Set("key");
            Assert.IsTrue(DialogueData.Available(dialogue.Entry("gated")));
            StoryFlags.Set("lock");
            Assert.IsFalse(DialogueData.Available(dialogue.Entry("gated")));
            Assert.AreEqual("plain", dialogue.Pick(new[] { "gated", "missing", "plain" }).Value<string>("id"));
            Assert.IsNull(dialogue.Pick(new[] { "gated", "missing" }));
        }

        [Test]
        public void ALinesOwnFlagsLeaveItOut()
        {
            Assert.AreEqual(2, DialogueData.LineCount(dialogue.Entry("picky")));
            StoryFlags.Set("key");
            Assert.AreEqual(3, DialogueData.LineCount(dialogue.Entry("picky")));
            StoryFlags.Set("lock");
            Assert.AreEqual(2, DialogueData.LineCount(dialogue.Entry("picky")));
        }

        [Test]
        public void OnceEntriesSetsAndThen()
        {
            var single = dialogue.Entry("single");
            Assert.IsTrue(DialogueData.Available(single));
            CollectionAssert.AreEqual(new[] { "s1", "s2" }, DialogueData.Sets(single));
            Assert.IsEmpty(DialogueData.Sets(dialogue.Entry("plain")));
            Assert.AreEqual("plain", DialogueData.NextId(single));
            StoryFlags.Set(DialogueData.SeenFlag("single"));
            Assert.IsFalse(DialogueData.Available(single));
        }

        [Test]
        public void FlagFieldsAreOneAListOrNothing()
        {
            CollectionAssert.AreEqual(new[] { "one" }, DialogueData.Flags(new JValue("one")));
            CollectionAssert.AreEqual(new[] { "a" }, DialogueData.Flags(new JArray("", "a", null)));
            Assert.IsEmpty(DialogueData.Flags(null));
            Assert.IsEmpty(DialogueData.Flags(new JValue("")));
            Assert.IsEmpty(DialogueData.Flags(JValue.CreateNull()));
        }

        [Test]
        public void Choices()
        {
            var choices = DialogueData.Choices(dialogue.Entry("choice"), 0);
            Assert.AreEqual(2, choices.Count);
            Assert.AreEqual("gated", choices[1].Then);
            Assert.IsEmpty(DialogueData.Choices(dialogue.Entry("choice"), 1));
            Assert.IsEmpty(DialogueData.Choices(dialogue.Entry("plain"), 0));
        }

        [Test]
        public void BadJsonIsEmptyNotACrash()
        {
            var none = new DialogueData("nope", "not json");
            Assert.AreEqual(0, none.Count);
            Assert.IsNull(none.Pick(new[] { "plain" }));
            Assert.AreEqual(0, new DialogueData("nope", "").Count);
        }
    }
}
