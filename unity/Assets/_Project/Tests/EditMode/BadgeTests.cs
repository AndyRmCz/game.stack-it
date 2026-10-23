using System;
using System.Collections.Generic;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    internal sealed class MemoryKeyValueStore : IKeyValueStore
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        public string GetString(string key) => _values.TryGetValue(key, out string v) ? v : null;
        public void SetString(string key, string value) => _values[key] = value;
        public void Delete(string key) => _values.Remove(key);
    }

    public class PendingChallengesTests
    {
        [Test]
        public void SurvivesARestartNewestFirst()
        {
            var store = new MemoryKeyValueStore();
            var pending = new PendingChallenges(store);
            pending.Add("AAAA11", "Ana", 12);
            pending.Add("BBBB22", "Bo\tb", 30);

            var restored = new PendingChallenges(store);
            Assert.AreEqual(2, restored.Items.Count);
            Assert.AreEqual("BBBB22", restored.Items[0].Code);
            Assert.AreEqual("Bo b", restored.Items[0].CreatorName);
            Assert.AreEqual(12, restored.Items[1].Score);
        }

        [Test]
        public void OpeningTheSameCodeAgainMovesItToTheTop()
        {
            var pending = new PendingChallenges(new MemoryKeyValueStore());
            pending.Add("AAAA11", "Ana", 12);
            pending.Add("BBBB22", "Bob", 30);
            pending.Add("AAAA11", "Ana", 12);
            CollectionAssert.AreEqual(new[] { "AAAA11", "BBBB22" }, new[] { pending.Items[0].Code, pending.Items[1].Code });
        }

        [Test]
        public void PlayingAChallengeClearsIt()
        {
            var pending = new PendingChallenges(new MemoryKeyValueStore());
            pending.Add("AAAA11", "Ana", 12);
            pending.Remove("AAAA11");
            Assert.IsFalse(pending.Any);
        }

        [Test]
        public void KeepsOnlyTheNewest()
        {
            var pending = new PendingChallenges(new MemoryKeyValueStore());
            for (int i = 0; i < PendingChallenges.Max + 3; i++) pending.Add("C" + i, "x", i);
            Assert.AreEqual(PendingChallenges.Max, pending.Items.Count);
            Assert.AreEqual("C" + (PendingChallenges.Max + 2), pending.Items[0].Code);
        }
    }

    public class SeenSkinsTests
    {
        private static readonly (string, int, bool)[] Items = { ("glass", 100, false), ("gold", 900, false), ("classic", 0, true) };

        [Test]
        public void AnAffordableUnseenSkinIsNew()
        {
            var seen = new SeenSkins(new MemoryKeyValueStore());
            Assert.IsTrue(seen.HasNew(Items, 150));
            Assert.IsFalse(seen.HasNew(Items, 50), "nothing affordable");
        }

        [Test]
        public void OpeningTheShopMarksAffordableSkinsSeenUntilMoreBecomeAffordable()
        {
            var store = new MemoryKeyValueStore();
            new SeenSkins(store).MarkSeen(Items, 150);
            var seen = new SeenSkins(store);
            Assert.IsFalse(seen.HasNew(Items, 150));
            Assert.IsTrue(seen.HasNew(Items, 1000), "gold became affordable");
        }
    }

    public class WeeklyResetTests
    {
        private static readonly DateTime Since = new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void CountsDownToAWeekAfterThePeriodStarted()
        {
            TimeSpan left = WeeklyReset.Remaining(Since, Since.AddDays(2).AddHours(3));
            Assert.AreEqual(TimeSpan.FromDays(4) + TimeSpan.FromHours(21), left);
            Assert.AreEqual("4d 21h", WeeklyReset.Short(left));
        }

        [Test]
        public void ShowsHoursAndMinutesOnTheLastDayAndNeverGoesNegative()
        {
            Assert.AreEqual("5h 12m", WeeklyReset.Short(WeeklyReset.Remaining(Since, Since.AddDays(7).AddHours(-5).AddMinutes(-12))));
            Assert.AreEqual(TimeSpan.Zero, WeeklyReset.Remaining(Since, Since.AddDays(8)));
        }
    }
}
