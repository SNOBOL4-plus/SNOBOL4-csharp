// Tests_01.cs — mirrors test_01.py
//
// Covers: identifier, real_number, BEAD (test_one), BAL, ARB
// ─────────────────────────────────────────────────────────────────────────────
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_01 : TestBase
    {
        static readonly PATTERN identifier =
              POS(0)
            + ANY(ALPHA)
            + FENCE(SPAN("." + DIGITS + UCASE + "_" + LCASE) | ε())
            + RPOS(0);

        // Captures are declared alongside the pattern; tests only check match/no-match.
        static string _whole = "", _fract = "", _exp = "";

        static readonly PATTERN real_number =
              POS(0)
            + (   (    SPAN(DIGITS) % (v => _whole = v)
                   +   (σ(".") + FENCE(SPAN(DIGITS) | ε()) % (v => _fract = v) | ε())
                   +   (σ("E") | σ("e"))
                   +   (σ("+") | σ("-") | ε())
                   +   SPAN(DIGITS) % (v => _exp = v)
                  )
               |  (    SPAN(DIGITS) % (v => _whole = v)
                   +   σ(".")
                   +   FENCE(SPAN(DIGITS) | ε()) % (v => _fract = v)
                  )
              )
            + RPOS(0);

        static readonly PATTERN test_one =
              POS(0)
            + Π(σ("B"), σ("F"), σ("L"), σ("R"))
            + Π(σ("E"), σ("EA"))
            + Π(σ("D"), σ("DS"))
            + RPOS(0);

        static readonly PATTERN Bal = POS(0) + BAL() + RPOS(0);
        static readonly PATTERN Arb = POS(0) + ARB() + RPOS(0);

        // ── identifier ────────────────────────────────────────────────────────

        [Theory]
        [InlineData("Id_99")] [InlineData("A")]   [InlineData("Z")]
        [InlineData("abc")]   [InlineData("X1")]  [InlineData("a.b")]
        [InlineData("A_B_C")]
        public void identifier_matches(string s) => AssertMatch(s, identifier);

        [Theory]
        [InlineData("")] [InlineData("9abc")] [InlineData("_abc")] [InlineData("a b")]
        public void identifier_no_match(string s) => AssertNoMatch(s, identifier);

        // ── real_number ───────────────────────────────────────────────────────

        [Theory]
        [InlineData("12.99E+3")] [InlineData("1.0E0")]    [InlineData("0.5E-10")]
        [InlineData("99.E+1")]   [InlineData("3.14")]      [InlineData("0.0")]
        [InlineData("100.001")]
        public void real_number_matches(string s) => AssertMatch(s, real_number);

        [Theory]
        [InlineData("")] [InlineData("abc")] [InlineData("1")]
        [InlineData(".5")] [InlineData("1.2.3")]
        public void real_number_no_match(string s) => AssertNoMatch(s, real_number);

        // ── BEAD (test_one) ───────────────────────────────────────────────────

        [Theory]
        [InlineData("BED")]  [InlineData("FED")]  [InlineData("LED")]  [InlineData("RED")]
        [InlineData("BEAD")] [InlineData("FEAD")] [InlineData("LEAD")] [InlineData("READ")]
        [InlineData("BEDS")] [InlineData("FEDS")] [InlineData("LEDS")] [InlineData("REDS")]
        [InlineData("BEADS")][InlineData("FEADS")][InlineData("LEADS")][InlineData("READS")]
        public void bead_matches(string word) => AssertMatch(word, test_one);

        [Theory]
        [InlineData("BID")] [InlineData("BREAD")] [InlineData("ED")]
        [InlineData("BEDSS")] [InlineData("")]
        public void bead_no_match(string word) => AssertNoMatch(word, test_one);

        // ── BAL ───────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("(A+B)")]          [InlineData("A+B()")]
        [InlineData("A()+B")]          [InlineData("X")]
        [InlineData("XYZ")]            [InlineData("A(B*C) (E/F)G+H")]
        [InlineData("( (A+ ( B*C) ) +D)")] [InlineData("(0+(1*9))")]
        [InlineData("((A+(B*C))+D)")]
        public void bal_matches(string expr) => AssertMatch(expr, Bal);

        [Theory]
        [InlineData("")] [InlineData(")A+B(")] [InlineData("A+B)")]
        [InlineData("(A+B")] [InlineData("A+B())")] [InlineData("((A+B)")]
        public void bal_no_match(string expr) => AssertNoMatch(expr, Bal);

        // ── ARB ───────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("")] [InlineData("$")] [InlineData("$$")]
        [InlineData("$$$")] [InlineData("hello")] [InlineData("1 2 3")]
        public void arb_matches(string s) => AssertMatch(s, Arb);
    }
}
