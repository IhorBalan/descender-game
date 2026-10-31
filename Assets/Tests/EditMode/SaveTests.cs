using System.IO;
using Descender.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Descender.Tests
{
    public class SaveTests
    {
        private string directory;

        [SetUp]
        public void Scratch()
        {
            directory = Path.Combine(Path.GetTempPath(), "descender_save_tests_" + Path.GetRandomFileName());
            SaveManager.UseDirectory(directory);
            SaveManager.Detach();
        }

        [TearDown]
        public void Clean()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void ASlotIsWrittenReadAndDeleted()
        {
            var slots = SaveManager.Slots;
            var slot = SaveSlots.SlotCount - 1;
            Assert.IsFalse(slots.HasSave(slot));
            Assert.IsNull(slots.Read(slot));
            var data = new SaveData
            {
                Level = "world_1",
                Checkpoint = 2,
                PlayTime = 61.5,
                SavedAt = 1000,
            };
            data.Abilities.Add(Ability.Wings);
            data.Abilities.Add(Ability.LightDash);
            data.Flags.Add("seen:intro");
            Assert.IsTrue(slots.Write(slot, data));
            var back = slots.Read(slot);
            Assert.AreEqual(2, back.Checkpoint);
            Assert.AreEqual(61.5, back.PlayTime, 0.001);
            CollectionAssert.AreEqual(new[] { Ability.Wings, Ability.LightDash }, back.Abilities);
            CollectionAssert.AreEqual(new[] { "seen:intro" }, back.Flags);
            Assert.AreEqual(slot, slots.LatestSlot());
            slots.Delete(slot);
            Assert.IsFalse(slots.HasSave(slot));
            Assert.AreEqual(-1, slots.LatestSlot());
        }

        [Test]
        public void AGodotSaveLoads()
        {
            const string godot =
                @"{
                ""version"": 1, ""level"": ""world_1"", ""checkpoint"": 4, ""abilities"": [0, 1, 3],
                ""flags"": [""boss:seraph_warden"", ""seen:intro""],
                ""inventory"": {""purse"": {""light"": 40, ""bones"": 2}, ""items"": {""flask"": 1}, ""equipped"": {}},
                ""skills"": {""xp"": 30, ""ranks"": {""vigour"": 1}},
                ""difficulty"": 2, ""play_time"": 1234.5, ""saved_at"": 1761900000.0
            }";
            var data = SaveData.FromJson(JObject.Parse(godot));
            Assert.IsNotNull(data);
            Assert.AreEqual(Difficulty.Hard, data.Difficulty);
            Assert.AreEqual(3, data.Abilities.Count);
            Assert.AreEqual(Ability.HolyLight, data.Abilities[2]);
            Assert.AreEqual(40, data.Inventory["purse"]["light"].Value<int>());
            Assert.IsNull(SaveData.FromJson(JObject.Parse(@"{""version"": 2}")), "a later version is not read");
            var round = SaveData.FromJson(data.ToJson());
            Assert.AreEqual(data.Flags, round.Flags);
            Assert.AreEqual(1234.5, round.PlayTime, 0.001);
        }

        [Test]
        public void TheManagerStartsLoadsAndWrites()
        {
            Assert.IsFalse(SaveManager.Write(new SaveData()), "no slot, no save");
            SaveManager.StartNewGame(0, Difficulty.Easy);
            Assert.AreEqual(1, SaveManager.BonusHearts());
            Assert.AreEqual(1, SaveManager.BonusCharges());
            Assert.AreEqual(2, SaveManager.ScaledDamage(2));
            Assert.IsTrue(SaveManager.Write(new SaveData { Checkpoint = 1, SavedAt = 5 }));
            SaveManager.Detach();
            Assert.IsTrue(SaveManager.LoadSlot(0));
            Assert.AreEqual(Difficulty.Easy, SaveManager.Difficulty);
            Assert.AreEqual(1, SaveManager.Pending.Checkpoint);
            Assert.IsFalse(SaveManager.LoadSlot(1));
            Assert.AreEqual("Empty", SaveManager.Summary(1, null));
            StringAssert.StartsWith("World 1 checkpoint 2", SaveManager.Summary(0, id => "World 1: The Descent"));
            StringAssert.Contains("Easy", SaveManager.Summary(0, null));
            SaveManager.StartNewGame(0, Difficulty.Hard);
            Assert.AreEqual(4, SaveManager.ScaledDamage(2));
            Assert.IsFalse(SaveManager.Slots.HasSave(0), "a new game clears the slot");
        }

        [Test]
        public void AnUnreadableSlotIsEmpty()
        {
            var slots = SaveManager.Slots;
            Directory.CreateDirectory(directory);
            File.WriteAllText(slots.SlotPath(1), "{not json");
            var warned = "";
            slots.Warning += message => warned = message;
            Assert.IsNull(slots.Read(1));
            StringAssert.Contains("slot 2", warned);
        }

        [Test]
        public void SettingsRoundTripAndClamp()
        {
            var settings = new SettingsData
            {
                volume = 2f,
                scale = 9,
                textSpeed = 7,
                prompts = (PromptLayout)5,
            };
            var back = SettingsData.FromJson(settings.ToJson());
            Assert.AreEqual(1f, back.volume);
            Assert.AreEqual(SettingsData.MaxWindowScale, back.scale);
            Assert.AreEqual(3, back.textSpeed);
            Assert.AreEqual(PromptLayout.PlayStation, back.prompts);
            Assert.AreEqual("Instant", back.TextSpeedName());
            Assert.AreEqual(0f, back.CharsPerSecond());
            Assert.AreEqual((1280, 720), back.WindowSizeFor(4));
            Assert.AreEqual("Half", SettingsData.ShakeName(0.5f));
            Assert.AreEqual(1f, SettingsData.FromJson("garbage").volume, "bad text means defaults");
            var path = Path.Combine(directory, SettingsData.FileName);
            Assert.IsTrue(back.Save(path));
            Assert.AreEqual(SettingsData.MaxWindowScale, SettingsData.Load(path).scale);
            Assert.AreEqual(SettingsData.DefaultWindowScale, SettingsData.Load(path + ".missing").scale);
        }
    }
}
