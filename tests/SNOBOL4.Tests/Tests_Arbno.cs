// Tests_Arbno.cs — mirrors test_arbno.py
//
// Covers: ARBNO with fixed literals, alternation, and LEN-based matching.
// ─────────────────────────────────────────────────────────────────────────────
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_Arbno : TestBase
    {
        static readonly PATTERN As =
              POS(0) + ARBNO(σ("a")) + RPOS(0);

        static readonly PATTERN Alist =
              POS(0)
            + (σ("a") | σ("b"))
            + ARBNO(σ(",") + (σ("a") | σ("b")))
            + RPOS(0);

        // ARBNO(σ("AA") | LEN(2) | σ("XX")) — three alternatives each 2 chars wide
        static readonly PATTERN Pairs =
              POS(0)
            + ARBNO(σ("AA") | LEN(2) | σ("XX"))
            + RPOS(0);

        // ── As ────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("aa")]
        [InlineData("aaa")]
        [InlineData("aaaa")]
        public void As_matches(string s) => AssertMatch(s, As);

        [Theory]
        [InlineData("b")]
        [InlineData("ab")]
        [InlineData("ba")]
        [InlineData("aab")]
        [InlineData("aaab")]
        public void As_no_match(string s) => AssertNoMatch(s, As);

        // ── Alist ─────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("a")]
        [InlineData("b")]
        [InlineData("a,a")]
        [InlineData("a,b")]
        [InlineData("b,a")]
        [InlineData("b,b")]
        [InlineData("a,a,a")]
        [InlineData("b,b,b")]
        [InlineData("a,a,a,a")]
        public void Alist_matches(string s) => AssertMatch(s, Alist);

        [Theory]
        [InlineData("")]
        [InlineData(",a")]
        [InlineData("a,")]
        [InlineData("a,,a")]
        [InlineData("a,c")]
        [InlineData("c")]
        public void Alist_no_match(string s) => AssertNoMatch(s, Alist);

        // ── Pairs ─────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("")]
        [InlineData("AA")]
        [InlineData("XX")]
        [InlineData("AB")]
        [InlineData("AAXX")]
        [InlineData("AABB")]
        [InlineData("XXAA")]
        [InlineData("AABBCC")]
        public void Pairs_matches(string s) => AssertMatch(s, Pairs);

        [Theory]
        [InlineData("CCXXAA$")]
        [InlineData("A")]
        [InlineData("AAA")]
        public void Pairs_no_match(string s) => AssertNoMatch(s, Pairs);
    }
}
