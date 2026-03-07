// Tests_Env.cs — conditional vs immediate capture semantics
//
// Now that Env/Slot/_ are gone, these tests verify the delegate-based
// capture operators directly:
//   P % (v => x = v)   conditional: fires only on full commit
//   P * (v => x = v)   immediate:   fires on every sub-match
// ─────────────────────────────────────────────────────────────────────────────
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_Env : TestBase
    {
        // ── % conditional — fires only on commit ─────────────────────────────

        [Fact]
        public void conditional_fires_on_match()
        {
            string word = "";
            Engine.FULLMATCH("hello", POS(0) + SPAN(ALPHA) % (v => word = v) + RPOS(0));
            Assert.Equal("hello", word);
        }

        [Fact]
        public void conditional_silent_on_failure()
        {
            string word = "before";
            Engine.FULLMATCH("hello", POS(0) + SPAN(ALPHA) % (v => word = v) + σ("NOPE") + RPOS(0));
            Assert.Equal("before", word);
        }

        [Fact]
        public void conditional_captures_winning_branch()
        {
            string tok = "";
            var p = POS(0) + (SPAN(DIGITS) % (v => tok = v) | SPAN(ALPHA) % (v => tok = v)) + RPOS(0);
            Engine.FULLMATCH("hello", p);
            Assert.Equal("hello", tok);
            Engine.FULLMATCH("123", p);
            Assert.Equal("123", tok);
        }

        // ── * immediate — fires on every sub-match ────────────────────────────

        [Fact]
        public void immediate_fires_even_if_outer_fails()
        {
            string seen = "";
            Engine.FULLMATCH("hello",
                POS(0) + SPAN(ALPHA) * (v => seen = v) + σ("NOPE") + RPOS(0));
            Assert.Equal("hello", seen);  // written immediately, not rolled back
        }

        [Fact]
        public void immediate_fires_on_match()
        {
            string seen = "";
            Engine.FULLMATCH("hello", POS(0) + SPAN(ALPHA) * (v => seen = v) + RPOS(0));
            Assert.Equal("hello", seen);
        }

        // ── Λ guard on captured value ─────────────────────────────────────────

        [Fact]
        public void lambda_guard_accepts_correct_length()
        {
            string tok = "";
            var p = POS(0) + SPAN(ALPHA) * (v => tok = v) + Λ(() => tok.Length == 4) + RPOS(0);
            AssertMatch("four", p);
            AssertNoMatch("hi",   p);
            AssertNoMatch("hello", p);
        }

        // ── NSPAN zero-length behaviour ───────────────────────────────────────

        [Fact]
        public void NSPAN_accepts_zero_length()
        {
            string tok = "X";
            var p = POS(0) + NSPAN(DIGITS) % (v => tok = v) + RPOS(0);
            AssertMatch("", p);
            Assert.Equal("", tok);
        }

        [Fact]
        public void NSPAN_accepts_nonzero_length()
        {
            string tok = "";
            var p = POS(0) + NSPAN(DIGITS) % (v => tok = v) + RPOS(0);
            AssertMatch("42", p);
            Assert.Equal("42", tok);
        }

        // ── Multiple captures in one pattern ──────────────────────────────────

        [Fact]
        public void multiple_captures_all_fire()
        {
            string k = "", v = "";
            var p = POS(0)
                  + BREAK("=") % (x => k = x)
                  + σ("=")
                  + REM()      % (x => v = x);
            Engine.SEARCH("key=value", p);
            Assert.Equal("key",   k);
            Assert.Equal("value", v);
        }

        // ── Cursor capture ────────────────────────────────────────────────────

        [Fact]
        public void theta_conditional_cursor_capture()
        {
            int pos = -1;
            var p = POS(0) + SPAN(ALPHA) + θ(n => pos = n) + RPOS(0);
            Engine.FULLMATCH("hello", p);
            Assert.Equal(5, pos);
        }

        [Fact]
        public void Theta_immediate_cursor_capture()
        {
            int pos = -1;
            var p = POS(0) + SPAN(ALPHA) + Θ(n => pos = n) + σ("NOPE") + RPOS(0);
            Engine.FULLMATCH("hello", p);
            Assert.Equal(5, pos);  // written immediately even though match fails
        }
    }
}
