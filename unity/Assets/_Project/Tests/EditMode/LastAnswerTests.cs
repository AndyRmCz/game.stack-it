using NUnit.Framework;
using StackIt.Client;
using StackIt.Client.Api;

namespace StackIt.Tests
{
    public class LastAnswerTests
    {
        private static Shop Shop(int balance) => new Shop { Balance = balance };

        [Test]
        public void NothingIsKeptBeforeTheFirstAnswer()
        {
            Assert.IsNull(new LastAnswer<Shop>().Value);
        }

        [Test]
        public void AReadsAnswerIsKeptAndShown()
        {
            var last = new LastAnswer<Shop>();
            Shop answer = Shop(10);

            Assert.AreSame(answer, last.Offer(last.Begin(), answer));
            Assert.AreSame(answer, last.Value);
        }

        [Test]
        public void AReadStartedBeforeAPurchaseCantUndoIt()
        {
            var last = new LastAnswer<Shop>();
            int ticket = last.Begin(); // the screen opens and asks for the shop
            Shop bought = Shop(40);
            last.Set(bought); // a purchase answers first

            Assert.AreSame(bought, last.Offer(ticket, Shop(100)), "the older shop is not shown");
            Assert.AreSame(bought, last.Value);
        }

        [Test]
        public void AReadStartedAfterAPurchaseIsKept()
        {
            var last = new LastAnswer<Shop>();
            last.Set(Shop(40));
            Shop fresh = Shop(55);

            Assert.AreSame(fresh, last.Offer(last.Begin(), fresh));
        }

        [Test]
        public void AnAccountChangeForgetsTheAnswer()
        {
            var last = new LastAnswer<Shop>();
            last.Set(Shop(40));

            last.Clear();

            Assert.IsNull(last.Value);
        }

        [Test]
        public void AReadOfTheOldAccountIsNotKeptButStillShown()
        {
            var last = new LastAnswer<Shop>();
            int ticket = last.Begin();
            last.Clear(); // signed in to another account meanwhile
            Shop old = Shop(70);

            Assert.AreSame(old, last.Offer(ticket, old), "the screen that asked still gets an answer");
            Assert.IsNull(last.Value, "the next screen doesn't show the old account's shop");
        }

        [Test]
        public void AnOlderReadArrivingLastDoesntReplaceANewerOne()
        {
            var last = new LastAnswer<Shop>();
            int first = last.Begin();
            int second = last.Begin();
            Shop newer = Shop(20);
            last.Offer(second, newer);

            Assert.AreSame(newer, last.Offer(first, Shop(10)));
            Assert.AreSame(newer, last.Value);
        }

        [Test]
        public void AnOlderReadArrivingFirstDoesntBlockANewerOne()
        {
            var last = new LastAnswer<Shop>();
            int first = last.Begin();
            int second = last.Begin();
            last.Offer(first, Shop(10));
            Shop newer = Shop(20);

            Assert.AreSame(newer, last.Offer(second, newer));
        }

        [Test]
        public void AChangeWithoutAnAnswerDropsReadsStartedBeforeIt()
        {
            var last = new LastAnswer<Shop>();
            Shop kept = Shop(10);
            last.Offer(last.Begin(), kept);
            int before = last.Begin(); // the screen asks...
            last.Changed(); // ...a friend is added, which answers no list...
            int after = last.Begin(); // ...and the screen asks again

            Assert.AreSame(kept, last.Offer(before, Shop(11)), "the list from before the change isn't kept");
            Shop fresh = Shop(12);
            Assert.AreSame(fresh, last.Offer(after, fresh));
        }
    }
}
