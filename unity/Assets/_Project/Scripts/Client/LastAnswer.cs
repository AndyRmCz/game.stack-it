namespace StackIt.Client
{
    /// <summary>
    /// The latest answer of one read endpoint (the shop, the missions...), so its screen can draw it
    /// at once and refresh behind it instead of opening empty. Every read and write takes a place in
    /// line, and an answer is kept only if nothing later in line was kept already: answers arriving
    /// out of order, or a read started before a purchase, can never undo a newer answer.
    /// </summary>
    public sealed class LastAnswer<T> where T : class
    {
        private int _issued;
        private int _kept;

        public T Value { get; private set; }

        /// <summary>A read starts: its place in line, handed back with its answer.</summary>
        public int Begin() => ++_issued;

        /// <summary>
        /// Keeps a read's answer unless something later in line than <paramref name="ticket"/> was
        /// kept. Returns what the screen should show: the newest answer there is.
        /// </summary>
        public T Offer(int ticket, T answer)
        {
            if (ticket > _kept)
            {
                Value = answer;
                _kept = ticket;
            }
            return Value ?? answer;
        }

        /// <summary>A write's answer: newer than any read still on its way.</summary>
        public void Set(T value)
        {
            Value = value;
            _kept = ++_issued;
        }

        /// <summary>A write that answers something else (adding a friend): reads already on their
        /// way are out of date, and the answer kept stays until a newer read replaces it.</summary>
        public void Changed() => _kept = ++_issued;

        /// <summary>Another account signed in: its screens must not show this one's answer.</summary>
        public void Clear() => Set(null);
    }
}
