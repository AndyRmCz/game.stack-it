using NUnit.Framework;
using StackIt.Core.Simulation;

namespace StackIt.Tests
{
    public class Pcg32Tests
    {
        [Test]
        public void MatchesPcg32ReferenceOutput()
        {
            // pcg-c-basic demo: pcg32_srandom_r(&rng, 42, 54). Same check as the Python suite.
            var rng = new Pcg32(42);
            uint[] expected = { 0xA15C02B7, 0x7B47F409, 0xBA1D3330, 0x83D2F293, 0xBFA4784B, 0xCBED606E };

            foreach (uint value in expected) Assert.AreEqual(value, rng.NextUInt());
        }
    }
}
