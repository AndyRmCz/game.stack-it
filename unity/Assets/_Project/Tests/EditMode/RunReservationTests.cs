using System;
using System.Threading.Tasks;
using NUnit.Framework;
using StackIt.Client;
using StackIt.Client.Api;

namespace StackIt.Tests
{
    public class RunReservationTests
    {
        private int _starts;
        private TaskCompletionSource<StartRunResponse> _answer;
        private TaskCompletionSource<bool> _deadline;

        private RunReservation Reservation() => new RunReservation(
            () =>
            {
                _starts++;
                return _answer.Task;
            },
            TimeSpan.FromSeconds(2),
            _ => _deadline.Task);

        [SetUp]
        public void SetUp()
        {
            _starts = 0;
            _answer = new TaskCompletionSource<StartRunResponse>();
            _deadline = new TaskCompletionSource<bool>();
        }

        [Test]
        public void AReadyReservationIsTakenWithoutWaiting()
        {
            var run = new StartRunResponse { RunId = "r1" };
            RunReservation reservation = Reservation();
            reservation.Reserve();
            _answer.SetResult(run);

            Task<StartRunResponse> take = reservation.TakeAsync();

            Assert.IsTrue(take.IsCompleted);
            Assert.AreSame(run, take.Result);
            Assert.AreEqual(1, _starts);
        }

        [Test]
        public void PlayDoesNotWaitForAServerThatNeverAnswers()
        {
            // Offline on a LAN address: each attempt hangs until its timeout (about a minute in all).
            RunReservation reservation = Reservation();

            Task<StartRunResponse> take = reservation.TakeAsync();
            Assert.IsFalse(take.IsCompleted, "waits up to the budget");
            _deadline.SetResult(true);

            Assert.IsTrue(take.IsCompleted);
            Assert.IsNull(take.Result, "an unranked local run");
        }

        [Test]
        public void ALateAnswerServesTheNextPlay()
        {
            RunReservation reservation = Reservation();
            _deadline.SetResult(true);
            Assert.IsNull(reservation.TakeAsync().Result);

            var run = new StartRunResponse { RunId = "late" };
            _answer.SetResult(run);

            Assert.AreSame(run, reservation.TakeAsync().Result);
            Assert.AreEqual(1, _starts, "the request still on its way is reused, not repeated");
        }

        [Test]
        public void AFailedReservationIsRequestedAgain()
        {
            RunReservation reservation = Reservation();
            reservation.Reserve();
            _answer.SetException(new InvalidOperationException("offline"));
            _answer = new TaskCompletionSource<StartRunResponse>();
            var run = new StartRunResponse { RunId = "back online" };
            _answer.SetResult(run);

            Assert.AreSame(run, reservation.TakeAsync().Result);
            Assert.AreEqual(2, _starts);
        }

        [Test]
        public void AFailureWithinTheBudgetReachesTheCaller()
        {
            RunReservation reservation = Reservation();
            _answer.SetException(new InvalidOperationException("refused"));

            Assert.ThrowsAsync<InvalidOperationException>(() => reservation.TakeAsync());
        }
    }
}
