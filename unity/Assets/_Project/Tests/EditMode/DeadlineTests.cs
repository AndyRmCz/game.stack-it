using System;
using System.Threading.Tasks;
using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class DeadlineTests
    {
        private static readonly TimeSpan Limit = TimeSpan.FromMilliseconds(200);

        [Test]
        public async Task AStepThatAnswersInTimeIsReported()
        {
            Assert.IsTrue(await Deadline.Within(Task.Delay(10), Limit));
        }

        [Test]
        public async Task AStepThatNeverAnswersStopsWaitingAtTheLimit()
        {
            // Ads waited forever on a callback that never came: no ad was ever requested.
            var never = new TaskCompletionSource<bool>();
            DateTime start = DateTime.UtcNow;

            Assert.IsFalse(await Deadline.Within(never.Task, Limit));
            Assert.Less((DateTime.UtcNow - start).TotalMilliseconds, 2000);
        }

        [Test]
        public async Task AStepThatFailsStillCountsAsAnswered()
        {
            // Its own error is the caller's to read; the deadline only says it didn't hang.
            Assert.IsTrue(await Deadline.Within(Task.FromException(new InvalidOperationException()), Limit));
        }
    }
}
