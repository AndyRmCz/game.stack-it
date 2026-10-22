namespace StackIt.Client
{
    public enum RivalEvent
    {
        None,
        Approaching,
        Passed,
    }

    /// <summary>
    /// Drives the rival ghost: one "almost there" beat a few blocks below the rival's height and
    /// one "YOU PASSED @name!" moment when the tower rises above it.
    /// </summary>
    public sealed class RivalTracker
    {
        private readonly int _rivalHeight;
        private readonly int _approachDistance;
        private bool _approached;

        public bool HasPassed { get; private set; }
        public int RivalHeight => _rivalHeight;

        public RivalTracker(int rivalHeight, int approachDistance = 3)
        {
            _rivalHeight = rivalHeight;
            _approachDistance = approachDistance;
        }

        public RivalEvent Update(int height)
        {
            if (_rivalHeight <= 0 || HasPassed) return RivalEvent.None;
            if (height > _rivalHeight)
            {
                HasPassed = true;
                return RivalEvent.Passed;
            }
            if (!_approached && height >= _rivalHeight - _approachDistance)
            {
                _approached = true;
                return RivalEvent.Approaching;
            }
            return RivalEvent.None;
        }
    }
}
