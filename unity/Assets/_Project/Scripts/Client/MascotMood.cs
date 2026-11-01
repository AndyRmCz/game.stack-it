namespace StackIt.Client
{
    public enum MascotMood
    {
        Happy, // an ordinary run: a gentle bob
        Cheer, // a new best or a won duel: hops under the gold slab
        Rally, // a lost duel or a continue on offer: a determined squint, "again!"
    }

    /// <summary>How Blocky reacts on the results sheet.</summary>
    public static class MascotMoods
    {
        /// <param name="duelWon">Null outside a challenge run.</param>
        public static MascotMood ForResult(bool newRecord, bool? duelWon)
        {
            if (duelWon == false) return MascotMood.Rally;
            return newRecord || duelWon == true ? MascotMood.Cheer : MascotMood.Happy;
        }
    }
}
