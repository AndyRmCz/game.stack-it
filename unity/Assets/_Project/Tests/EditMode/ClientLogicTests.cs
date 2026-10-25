using System;
using System.Collections.Generic;
using NUnit.Framework;
using StackIt.Client;
using StackIt.Client.Api;
using StackIt.Core;

namespace StackIt.Tests
{
    public class DeepLinksTests
    {
        private const string Base = "https://stackit.up.railway.app";

        [TestCase("https://stackit.up.railway.app/c/K7QX9PA", "K7QX9PA")]
        [TestCase("https://stackit.up.railway.app/c/k7qx9pa/", "K7QX9PA")]
        [TestCase("https://stackit.up.railway.app/c/K7QX9PA?utm_source=whatsapp", "K7QX9PA")]
        [TestCase("stackit://c/K7QX9PA", "K7QX9PA")]
        [TestCase("STACKIT://c/K7QX9PA", "K7QX9PA")]
        public void ParsesChallengeLinks(string url, string expected)
        {
            Assert.AreEqual(expected, DeepLinks.ParseChallengeCode(url, Base, "stackit"));
        }

        [TestCase("https://evil.example/c/K7QX9PA")]
        [TestCase("https://stackit.up.railway.app/privacy")]
        [TestCase("https://stackit.up.railway.app/c/")]
        [TestCase("https://stackit.up.railway.app/c/<script>")]
        [TestCase("othergame://c/K7QX9PA")]
        [TestCase("")]
        [TestCase(null)]
        public void RejectsForeignOrMalformedLinks(string url)
        {
            Assert.IsNull(DeepLinks.ParseChallengeCode(url, Base, "stackit"));
        }

        [TestCase("challenge=K7QX9PA", "K7QX9PA")]
        [TestCase("utm_source=google-play&challenge=k7qx9pa", "K7QX9PA")]
        [TestCase("utm_source=google-play&utm_medium=organic", null)]
        public void ParsesInstallReferrer(string referrer, string expected)
        {
            Assert.AreEqual(expected, DeepLinks.ParseInstallReferrer(referrer));
        }

        [TestCase(" k7q x9pa ", "K7QX9PA")]
        [TestCase("ab", null)]
        public void NormalizesTypedCodes(string typed, string expected)
        {
            Assert.AreEqual(expected, DeepLinks.NormalizeTypedCode(typed));
        }
    }

    public class AdPolicyTests
    {
        private static AdPolicy Policy() => new AdPolicy(minRunsBetween: 3, minSecondsBetween: 90);

        [Test]
        public void NeverOnTheFirstSession()
        {
            var p = Policy();
            for (int i = 0; i < 10; i++) p.RecordRunEnded();
            Assert.IsFalse(p.ShouldShowInterstitial(isFirstSession: true, newRecord: false, nowSeconds: 1000));
        }

        [Test]
        public void NeverRightAfterANewRecord()
        {
            var p = Policy();
            for (int i = 0; i < 10; i++) p.RecordRunEnded();
            Assert.IsFalse(p.ShouldShowInterstitial(false, newRecord: true, nowSeconds: 1000));
        }

        [Test]
        public void FrequencyCappedByRunsAndTime()
        {
            var p = Policy();
            p.RecordRunEnded();
            p.RecordRunEnded();
            Assert.IsFalse(p.ShouldShowInterstitial(false, false, 1000), "only 2 runs");
            p.RecordRunEnded();
            Assert.IsTrue(p.ShouldShowInterstitial(false, false, 1000));

            p.RecordInterstitialShown(1000);
            for (int i = 0; i < 5; i++) p.RecordRunEnded();
            Assert.IsFalse(p.ShouldShowInterstitial(false, false, 1060), "60 s after the last one");
            Assert.IsTrue(p.ShouldShowInterstitial(false, false, 1091));
        }

        [Test]
        public void RewardedAdCountsAsAnAdBreak()
        {
            var p = Policy();
            for (int i = 0; i < 5; i++) p.RecordRunEnded();
            p.RecordRewardedShown(1000);
            Assert.IsFalse(p.ShouldShowInterstitial(false, false, 1030));
        }

        [Test]
        public void DoubleCoinsOfferedOnlyForVerifiedRunsWithCoinsAndAnAd()
        {
            var verified = new SubmitRunResponse { Status = "verified", CoinsEarned = 12 };
            Assert.IsTrue(AdPolicy.OffersDoubleCoins(verified, rewardedReady: true));
            Assert.IsFalse(AdPolicy.OffersDoubleCoins(verified, rewardedReady: false), "no ad loaded");
            Assert.IsFalse(AdPolicy.OffersDoubleCoins(new SubmitRunResponse { Status = "rejected", CoinsEarned = 12 }, true));
            Assert.IsFalse(AdPolicy.OffersDoubleCoins(new SubmitRunResponse { Status = "verified" }, true), "no coins");
            Assert.IsFalse(AdPolicy.OffersDoubleCoins(new SubmitRunResponse { Status = "verified", CoinsEarned = 12, CoinsDoubled = true }, true));
        }

        [Test]
        public void NoDoubleCoinsOfferForARunQueuedOffline()
        {
            // An offline submit is queued and has no result yet (null): this used to throw and
            // leave every later Play press failing.
            Assert.IsFalse(AdPolicy.OffersDoubleCoins(null, rewardedReady: true));
        }
    }

    public class RetryPolicyTests
    {
        [TestCase(0, false)]
        [TestCase(408, true)]
        [TestCase(429, true)]
        [TestCase(500, true)]
        [TestCase(503, true)]
        [TestCase(400, false)]
        [TestCase(401, false)]
        [TestCase(409, false)]
        public void RetriesOnlyTransientFailures(int status, bool retry)
        {
            // 0 = network error without a response: retried separately via isNetworkError.
            Assert.AreEqual(retry, RetryPolicy.IsTransientStatus(status));
        }

        [Test]
        public void BackoffGrowsExponentiallyWithJitterAndCap()
        {
            var policy = new RetryPolicy(maxAttempts: 6, baseDelaySeconds: 0.5, maxDelaySeconds: 8, random: () => 1.0);
            var delays = new List<double>();
            for (int attempt = 1; attempt <= 5; attempt++) delays.Add(policy.DelaySeconds(attempt));

            CollectionAssert.AreEqual(new[] { 0.5, 1.0, 2.0, 4.0, 8.0 }, delays);
            Assert.AreEqual(8.0, policy.DelaySeconds(10));
            Assert.IsTrue(policy.CanRetry(5));
            Assert.IsFalse(policy.CanRetry(6));
        }

        [Test]
        public void JitterStaysWithinHalfToFullDelay()
        {
            var low = new RetryPolicy(5, 1, 30, () => 0.0);
            var high = new RetryPolicy(5, 1, 30, () => 0.999);
            Assert.AreEqual(1.0, low.DelaySeconds(2), 1e-9);
            Assert.AreEqual(2.0, high.DelaySeconds(2), 0.01);
        }
    }

    public class OfflineRunQueueTests
    {
        private sealed class MemoryStore : IKeyValueStore
        {
            public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
            public string GetString(string key) => Data.TryGetValue(key, out var v) ? v : null;
            public void SetString(string key, string value) => Data[key] = value;
            public void Delete(string key) => Data.Remove(key);
        }

        [Test]
        public void PersistsInOrderAndSurvivesRestart()
        {
            var store = new MemoryStore();
            var queue = new OfflineRunQueue(store, capacity: 10);
            queue.Enqueue("run-1", "{\"a\":1}");
            queue.Enqueue("run-2", "{\"b\":2}");

            var reloaded = new OfflineRunQueue(store, capacity: 10);

            Assert.AreEqual(2, reloaded.Count);
            Assert.AreEqual("run-1", reloaded.Peek().RunId);
            Assert.AreEqual("{\"a\":1}", reloaded.Peek().Payload);
        }

        [Test]
        public void SameRunIsQueuedOnceAndRemovedById()
        {
            var queue = new OfflineRunQueue(new MemoryStore(), capacity: 10);
            queue.Enqueue("run-1", "first");
            queue.Enqueue("run-1", "retry");
            queue.Enqueue("run-2", "x");

            queue.Remove("run-1");

            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual("run-2", queue.Peek().RunId);
        }

        [Test]
        public void DropsOldestWhenFull()
        {
            var queue = new OfflineRunQueue(new MemoryStore(), capacity: 2);
            queue.Enqueue("a", "1");
            queue.Enqueue("b", "2");
            queue.Enqueue("c", "3");

            Assert.AreEqual(2, queue.Count);
            Assert.AreEqual("b", queue.Peek().RunId);
        }

        [Test]
        public void PayloadsWithSeparatorsRoundTrip()
        {
            var store = new MemoryStore();
            var queue = new OfflineRunQueue(store, 5);
            const string payload = "{\"events\":[{\"t\":10,\"type\":\"tap\"}],\"note\":\"a|b\\nc\"}";
            queue.Enqueue("r", payload);

            Assert.AreEqual(payload, new OfflineRunQueue(store, 5).Peek().Payload);
        }
    }

    public class RivalTrackerTests
    {
        [Test]
        public void FiresApproachingThenPassedOnce()
        {
            var tracker = new RivalTracker(rivalHeight: 10, approachDistance: 3);
            var events = new List<RivalEvent>();
            for (int h = 1; h <= 14; h++) events.Add(tracker.Update(h));

            Assert.AreEqual(RivalEvent.Approaching, events[6]); // height 7
            Assert.AreEqual(RivalEvent.Passed, events[10]); // height 11: strictly above the ghost
            Assert.AreEqual(1, events.FindAll(e => e == RivalEvent.Passed).Count);
            Assert.AreEqual(1, events.FindAll(e => e == RivalEvent.Approaching).Count);
            Assert.IsTrue(tracker.HasPassed);
        }

        [Test]
        public void NoRivalMeansNoEvents()
        {
            var tracker = new RivalTracker(0, 3);
            for (int h = 1; h < 5; h++) Assert.AreEqual(RivalEvent.None, tracker.Update(h));
        }
    }

    public class EconomyTests
    {
        // Same cases as backend/tests/domain/test_economy.py.
        [TestCase(0, 0)]
        [TestCase(4, 0)]
        [TestCase(5, 1)]
        [TestCase(49, 9)]
        [TestCase(507, 101)]
        public void CoinsForRunMatchesServer(int score, int coins)
        {
            Assert.AreEqual(coins, Economy.CoinsForRun(score));
        }
    }
}
