"""PCG32 (XSH-RR), bit-for-bit identical to the C# port. See shared/SIMULATION.md."""

_MASK64 = (1 << 64) - 1
_MASK32 = (1 << 32) - 1
_MULTIPLIER = 6364136223846793005
_STREAM = 54


class Pcg32:
    __slots__ = ("_inc", "_state")

    def __init__(self, seed: int) -> None:
        self._state = 0
        self._inc = ((_STREAM << 1) | 1) & _MASK64
        self.next_u32()
        self._state = (self._state + (seed & _MASK32)) & _MASK64
        self.next_u32()

    def next_u32(self) -> int:
        old = self._state
        self._state = (old * _MULTIPLIER + self._inc) & _MASK64
        xorshifted = (((old >> 18) ^ old) >> 27) & _MASK32
        rot = old >> 59
        return ((xorshifted >> rot) | (xorshifted << ((32 - rot) & 31))) & _MASK32
