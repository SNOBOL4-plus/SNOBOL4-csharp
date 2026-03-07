// Tests_Env.cs — SnobolEnv, Slot, TRACE, NSPAN
//
// Covers the _ dynamic accessor, Slot implicit conversions, % and * operators,
// TRACE output format, and NSPAN zero-length behaviour.
// ─────────────────────────────────────────────────────────────────────────────
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_Env : TestBase
    {
        // ── SnobolEnv / Slot ──────────────────────────────────────────────────

        [Fact]
        public void slot_set_and_get_roundtrip()
        {
            FreshEnv();
            S4._.slotWord = "hello";
            Assert.Equal("hello", (string)(Slot)S4._.slotWord);
        }

        [Fact]
        public void percent_slot_fires_on_success()
        {
            FreshEnv();
            S4._.w = "before";
            Engine.FULLMATCH("abc", POS(0) + (SPAN(ALPHA) % (Slot)S4._.w) + RPOS(0));
            Assert.Equal("abc", (string)(Slot)S4._.w);
        }

        [Fact]
        public void percent_slot_silent_on_failure()
        {
            FreshEnv();
            S4._.w = "before";
            Engine.FULLMATCH("abc123", POS(0) + (SPAN(ALPHA) % (Slot)S4._.w) + σ("NOPE") + RPOS(0));
            Assert.Equal("before", (string)(Slot)S4._.w);
        }

        [Fact]
        public void slot_implicit_int_conversion()
        {
            FreshEnv();
            S4._.slotInt = 42;
            int n = (Slot)S4._.slotInt;
            Assert.Equal(42, n);
        }

        [Fact]
        public void unset_slot_returns_empty_string()
        {
            FreshEnv();
            string unset = (Slot)S4._.totally_absent_key;
            Assert.Equal("", unset);
        }

        [Fact]
        public void unset_slot_returns_zero_as_int()
        {
            FreshEnv();
            int unset = (Slot)S4._.totally_absent_key;
            Assert.Equal(0, unset);
        }

        [Fact]
        public void star_slot_fires_immediately_even_on_outer_failure()
        {
            FreshEnv();
            S4._.imm = "before";
            Engine.FULLMATCH("abc123", POS(0) + (SPAN(ALPHA) * (Slot)S4._.imm) + σ("NOPE") + RPOS(0));
            Assert.Equal("abc", (string)(Slot)S4._.imm);
        }

        [Fact]
        public void ζ_slot_resolves_pattern_at_match_time()
        {
            FreshEnv();
            S4._.zpat = SPAN(DIGITS);
            AssertMatch("123", POS(0) + ζ((Slot)S4._.zpat) + RPOS(0));
            S4._.zpat = SPAN(ALPHA);
            AssertMatch("abc", POS(0) + ζ((Slot)S4._.zpat) + RPOS(0));
        }

        // ── TRACE ─────────────────────────────────────────────────────────────

        [Fact]
        public void trace_output_contains_window_and_success()
        {
            FreshEnv();
            var sw = new System.IO.StringWriter();
            TRACE(TraceLevel.Info, window: 6, output: sw);
            Engine.FULLMATCH("hello", POS(0) + SPAN(ALPHA) % (Slot)S4._.w + RPOS(0));
            TRACE();  // turn off
            var output = sw.ToString();

            Assert.True(output.Length > 0);
            Assert.Contains("|",       output);
            Assert.Contains("SPAN",    output);
            Assert.Contains("SUCCESS", output);
            Assert.Contains("hello",   output);
        }

        // ── NSPAN ─────────────────────────────────────────────────────────────

        [Fact]
        public void NSPAN_matches_empty_string() =>
            AssertMatch("", POS(0) + NSPAN(DIGITS) + RPOS(0));

        [Fact]
        public void NSPAN_matches_all_digits() =>
            AssertMatch("123", POS(0) + NSPAN(DIGITS) + RPOS(0));

        [Fact]
        public void NSPAN_no_match_when_anchored_past_non_digits() =>
            // NSPAN matches zero chars but then RPOS(0) fails because we're not at the end
            AssertNoMatch("abc", POS(0) + NSPAN(DIGITS) + RPOS(0));

        [Fact]
        public void NSPAN_partial_match()
        {
            // NSPAN matches "123", cursor lands at 3, RPOS(3) succeeds from position 3
            var r = Engine.SEARCH("123abc", POS(0) + NSPAN(DIGITS) + RPOS(3));
            Assert.NotNull(r);
        }

        [Fact]
        public void NSPAN_zero_length_as_suffix() =>
            AssertMatch("abc", POS(0) + SPAN(ALPHA) + NSPAN(DIGITS) + RPOS(0));

        [Fact]
        public void NSPAN_after_SPAN() =>
            AssertMatch("abc123", POS(0) + SPAN(ALPHA) + NSPAN(DIGITS) + RPOS(0));

        [Fact]
        public void NSPAN_identifier_single_char()
        {
            var ident = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);
            AssertMatch("A", ident);
        }

        [Fact]
        public void NSPAN_identifier_mixed()
        {
            var ident = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);
            AssertMatch("Abc123", ident);
        }

        [Fact]
        public void NSPAN_identifier_rejects_leading_digit()
        {
            var ident = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);
            AssertNoMatch("1abc", ident);
        }
    }
}
