using NUnit.Framework;
using StackIt.Client;

namespace StackIt.Tests
{
    public class MascotMoodTests
    {
        [Test]
        public void AnOrdinaryRunIsHappy()
        {
            Assert.AreEqual(MascotMood.Happy, MascotMoods.ForResult(newRecord: false, duelWon: null));
        }

        [Test]
        public void ANewRecordOrAWonDuelCheers()
        {
            Assert.AreEqual(MascotMood.Cheer, MascotMoods.ForResult(newRecord: true, duelWon: null));
            Assert.AreEqual(MascotMood.Cheer, MascotMoods.ForResult(newRecord: false, duelWon: true));
        }

        [Test]
        public void ALostDuelRalliesEvenWithARecord()
        {
            // The sheet says the rival won: cheering under it would read as mockery.
            Assert.AreEqual(MascotMood.Rally, MascotMoods.ForResult(newRecord: false, duelWon: false));
            Assert.AreEqual(MascotMood.Rally, MascotMoods.ForResult(newRecord: true, duelWon: false));
        }
    }
}
