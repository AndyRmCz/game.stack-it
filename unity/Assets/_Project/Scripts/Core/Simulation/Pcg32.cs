namespace StackIt.Core.Simulation
{
    /// <summary>PCG32 (XSH-RR), bit-for-bit identical to backend/app/domain/sim/prng.py.</summary>
    public sealed class Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const ulong Stream = 54UL;

        private ulong _state;
        private readonly ulong _inc;

        public Pcg32(uint seed)
        {
            _state = 0UL;
            _inc = (Stream << 1) | 1UL;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            unchecked
            {
                _state = old * Multiplier + _inc;
            }
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((32 - rot) & 31));
        }
    }
}
