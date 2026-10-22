from app.domain.sim.prng import Pcg32


def test_matches_pcg32_reference_output() -> None:
    # Reference values from the pcg-c-basic demo: pcg32_srandom_r(&rng, 42, 54).
    rng = Pcg32(42)

    assert [rng.next_u32() for _ in range(6)] == [
        0xA15C02B7,
        0x7B47F409,
        0xBA1D3330,
        0x83D2F293,
        0xBFA4784B,
        0xCBED606E,
    ]


def test_outputs_stay_unsigned_32_bit_for_extreme_seeds() -> None:
    for seed in (0, 0xFFFFFFFF):
        rng = Pcg32(seed)
        values = [rng.next_u32() for _ in range(1000)]
        assert all(0 <= v <= 0xFFFFFFFF for v in values)
        assert len(set(values)) > 990
