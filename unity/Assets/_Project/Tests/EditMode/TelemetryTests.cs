using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class TelemetryTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

        private static SessionTracker Tracker(IKeyValueStore store)
        {
            int n = 0;
            return new SessionTracker(store, () => "s" + ++n);
        }

        [Test]
        public void ClientEventNamesMatchTheSharedList()
        {
            string json = File.ReadAllText(Path.Combine(TestPaths.FindUp(Path.Combine("shared", "telemetry")), "events.json"));
            string events = json.Substring(json.IndexOf("\"events\"", StringComparison.Ordinal));
            var shared = Regex.Matches(events, "\"([a-z_]+)\":\\s*\\[").Cast<Match>().Select(m => m.Groups[1].Value);
            CollectionAssert.AreEquivalent(shared.ToArray(), TelemetryEvents.All);
        }

        [Test]
        public void QueueSurvivesARestartAndDropsTheOldestPastCapacity()
        {
            var store = new MemoryKeyValueStore();
            var queue = new TelemetryQueue(store, capacity: 3);
            for (int i = 1; i <= 4; i++) queue.Enqueue("e" + i, "{\"n\":" + i + "}");

            var reloaded = new TelemetryQueue(store, capacity: 3);

            Assert.AreEqual(new[] { "e2", "e3", "e4" }, reloaded.Peek().Select(e => e.Id).ToArray());
            Assert.AreEqual("{\"n\":2}", reloaded.Peek(1)[0].Json);
        }

        [Test]
        public void SentEventsAreRemovedAndAnEmptyQueueLeavesNothingStored()
        {
            var store = new MemoryKeyValueStore();
            var queue = new TelemetryQueue(store);
            queue.Enqueue("a", "{}");
            queue.Enqueue("b", "{}");

            queue.Remove(queue.Peek(1).Select(e => e.Id));
            Assert.AreEqual(1, queue.Count);
            queue.Remove(new[] { "b" });

            Assert.AreEqual(0, new TelemetryQueue(store).Count);
        }

        [Test]
        public void PeekNeverReturnsMoreThanABatch()
        {
            var queue = new TelemetryQueue(new MemoryKeyValueStore());
            for (int i = 0; i < TelemetryText.MaxBatch + 5; i++) queue.Enqueue("e" + i, "{}");
            Assert.AreEqual(TelemetryText.MaxBatch, queue.Peek().Count);
        }

        [Test]
        public void FirstLaunchStartsSessionOne()
        {
            SessionTracker tracker = Tracker(new MemoryKeyValueStore());

            SessionChange change = tracker.Foreground(T0, coldStart: true);

            Assert.IsTrue(change.Started);
            Assert.IsNull(change.Ended);
            Assert.AreEqual(1, tracker.Index);
            Assert.AreEqual("s1", tracker.SessionId);
        }

        [Test]
        public void ComingBackWithinThirtySecondsContinuesTheSession()
        {
            SessionTracker tracker = Tracker(new MemoryKeyValueStore());
            tracker.Foreground(T0, true);
            tracker.Background(T0.AddSeconds(60));

            SessionChange change = tracker.Foreground(T0.AddSeconds(85), false);

            Assert.IsFalse(change.Started);
            Assert.AreEqual("s1", tracker.SessionId);
            Assert.AreEqual(65, tracker.ForegroundSeconds(T0.AddSeconds(90)), 0.01, "background time is not counted");
        }

        [Test]
        public void ComingBackLaterEndsTheSessionWithItsForegroundTimeAndCounts()
        {
            SessionTracker tracker = Tracker(new MemoryKeyValueStore());
            tracker.Foreground(T0, true);
            tracker.CountRun();
            tracker.CountRun();
            tracker.CountAd();
            tracker.Background(T0.AddSeconds(120));

            SessionChange change = tracker.Foreground(T0.AddMinutes(10), false);

            Assert.IsTrue(change.Started);
            Assert.AreEqual("s1", change.Ended.Id);
            Assert.AreEqual(120, change.Ended.DurationSeconds, 0.01);
            Assert.AreEqual(2, change.Ended.Runs);
            Assert.AreEqual(1, change.Ended.Ads);
            Assert.AreEqual("timeout", change.Ended.EndedBy);
            Assert.AreEqual(2, tracker.Index);
            Assert.AreEqual(0, tracker.Runs);
        }

        [Test]
        public void ASessionCutShortByAKillIsReportedOnTheNextLaunch()
        {
            var store = new MemoryKeyValueStore();
            SessionTracker before = Tracker(store);
            before.Foreground(T0, true);
            before.CountRun();
            before.Checkpoint(T0.AddSeconds(40));
            // killed here: no Background call

            SessionTracker after = Tracker(store);
            SessionChange change = after.Foreground(T0.AddHours(3), coldStart: true);

            Assert.AreEqual("s1", change.Ended.Id);
            Assert.AreEqual(40, change.Ended.DurationSeconds, 0.01);
            Assert.AreEqual(1, change.Ended.Runs);
            Assert.AreEqual("relaunch", change.Ended.EndedBy);
            Assert.AreEqual(2, after.Index);
        }

        [Test]
        public void AColdStartAlwaysStartsANewSessionEvenWithinTheWindow()
        {
            var store = new MemoryKeyValueStore();
            SessionTracker before = Tracker(store);
            before.Foreground(T0, true);
            before.Background(T0.AddSeconds(10));

            SessionChange change = Tracker(store).Foreground(T0.AddSeconds(15), coldStart: true);

            Assert.IsTrue(change.Started);
            Assert.AreEqual("relaunch", change.Ended.EndedBy);
        }

        [Test]
        public void ARepeatedForegroundCallChangesNothing()
        {
            SessionTracker tracker = Tracker(new MemoryKeyValueStore());
            tracker.Foreground(T0, true);

            SessionChange change = tracker.Foreground(T0.AddSeconds(5), false);

            Assert.IsFalse(change.Started);
            Assert.IsNull(change.Ended);
            Assert.AreEqual(1, tracker.Index);
        }

        [Test]
        public void FrameStatsReportMedianAndSlowFrames()
        {
            var stats = new FrameStats();
            for (int i = 0; i < 95; i++) stats.Add(1f / 60f);
            for (int i = 0; i < 5; i++) stats.Add(1f / 20f);

            Assert.AreEqual(60f, stats.MedianFps, 0.1f);
            Assert.AreEqual(20f, stats.P5Fps, 0.1f);
        }

        [Test]
        public void FrameStatsKeepOnlyTheLatestFrames()
        {
            var stats = new FrameStats(capacity: 4);
            for (int i = 0; i < 4; i++) stats.Add(1f / 10f);
            for (int i = 0; i < 4; i++) stats.Add(1f / 50f);

            Assert.AreEqual(4, stats.Count);
            Assert.AreEqual(50f, stats.MedianFps, 0.1f);
        }

        [TestCase("/v1/runs/3f2a9c1e-0000-4000-8000-000000000001/submit", "/v1/runs/{id}/submit")]
        [TestCase("/v1/challenges/K7QX9PA", "/v1/challenges/{id}")]
        [TestCase("/v1/leaderboards/friends?period=weekly", "/v1/leaderboards/friends")]
        [TestCase("/v1/shop/purchase", "/v1/shop/purchase")]
        public void ApiPathsLoseIdsAndQueries(string path, string expected)
        {
            Assert.AreEqual(expected, TelemetryText.PathTemplate(path));
        }

        [Test]
        public void LongStringsAreCutToWhatTheServerAccepts()
        {
            string os = new string('x', 100);
            Assert.AreEqual(TelemetryText.MaxString, TelemetryText.Clamp(os).Length);
            Assert.AreEqual("short", TelemetryText.Clamp("short"));
            Assert.IsNull(TelemetryText.Clamp(null));
        }
    }
}
