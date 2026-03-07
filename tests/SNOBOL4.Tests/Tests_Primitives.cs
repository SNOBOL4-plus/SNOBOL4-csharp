// Tests_Primitives.cs — core pattern primitives and engine stages 1–9
//
// Covers: σ, POS, RPOS, LEN, TAB, RTAB, REM, ARB, ANY, NOTANY, SPAN, NSPAN,
//         BREAK, BREAKX, ARBNO, BAL, Φ/φ, ζ, nPush/nInc/nPop, Shift/Reduce/Pop,
//         and all Func<> deferred-argument overloads.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_Primitives : TestBase
    {
        // ── nPush / nInc / nPop ───────────────────────────────────────────────

        [Fact]
        public void nStack_basic_counter()
        {
            var p = POS(0)
                  + nPush()
                  + σ("a") + nInc()
                  + σ("b") + nInc()
                  + σ("c") + nInc()
                  + nPop()
                  + RPOS(0);
            AssertMatch("abc", p);
        }

        [Fact]
        public void nStack_with_alternation()
        {
            var p = POS(0)
                  + nPush()
                  + (σ("x") + nInc() | σ("a") + nInc())
                  + (σ("y") + nInc() | σ("b") + nInc())
                  + (σ("z") + nInc() | σ("c") + nInc())
                  + Reduce("Test")
                  + nPop()
                  + RPOS(0);
            AssertMatch("abc", p);
        }

        // ── Shift / Reduce / Pop ──────────────────────────────────────────────

        [Fact]
        public void Shift_tag_only_then_Pop()
        {
            FreshEnv();
            var p = POS(0) + SPAN(ALPHA) % (Slot)S4._.word + Shift("Word") + Pop((Slot)S4._.tree) + RPOS(0);
            Engine.FULLMATCH("hello", p);
            var t = (List<object>)((Slot)S4._.tree).Value!;
            Assert.Equal("Word", t[0]);
        }

        [Fact]
        public void Shift_with_value_expression()
        {
            FreshEnv();
            var p = POS(0)
                  + SPAN(DIGITS) % (Slot)S4._.n
                  + Shift("Int", () => (object)int.Parse((string)(Slot)S4._.n))
                  + Pop((Slot)S4._.tree)
                  + RPOS(0);
            Engine.FULLMATCH("42", p);
            var t = (List<object>)((Slot)S4._.tree).Value!;
            Assert.Equal("Int", t[0]);
            Assert.Equal(42, t[1]);
        }

        [Fact]
        public void Reduce_wraps_two_children()
        {
            FreshEnv();
            var p = POS(0)
                  + nPush()
                  + SPAN(UCASE) % (Slot)S4._.a + Shift("A") + nInc()
                  + SPAN(LCASE) % (Slot)S4._.b + Shift("B") + nInc()
                  + Reduce("Pair")
                  + nPop()
                  + Pop((Slot)S4._.tree)
                  + RPOS(0);
            Engine.FULLMATCH("HELLOworld", p);
            var t = (List<object>)((Slot)S4._.tree).Value!;
            Assert.Equal("Pair", t[0]);
            Assert.Equal(2, t.Count - 1);
        }

        [Fact]
        public void Reduce_not_fired_on_failure()
        {
            FreshEnv();
            S4._.treeX = new List<object> { "before" };
            var p = POS(0)
                  + nPush()
                  + SPAN(ALPHA) % (Slot)S4._.w + Shift("W") + nInc()
                  + Reduce("Node")
                  + nPop()
                  + σ("NOPE")
                  + Pop((Slot)S4._.treeX)
                  + RPOS(0);
            Engine.FULLMATCH("hello", p);
            // treeX was set to ["before"] before the match; the Pop was on
            // the failing branch so it should never have fired
            var tx = Env.G.TryGetValue("treeX", out var v) ? v : null;
            Assert.Equal("before", tx is List<object> tl ? tl[0] : tx);
        }

        // ── ζ(Func<PATTERN>) — recursive grammar ─────────────────────────────

        [Fact]
        public void ζ_func_simple_recursion_matches()
        {
            PATTERN? expr_ref = null;
            var atom = ANY(ALPHA) | σ("(") + ζ(() => expr_ref!) + σ(")");
            var expr = atom + ARBNO((σ("+") | σ("-")) + atom);
            expr_ref = expr;

            AssertMatch("a",       POS(0) + expr + RPOS(0));
            AssertMatch("a+b",     POS(0) + expr + RPOS(0));
            AssertMatch("(a+b)",   POS(0) + expr + RPOS(0));
            AssertMatch("a+(b+c)", POS(0) + expr + RPOS(0));
        }

        [Fact]
        public void ζ_func_no_match_on_bare_operator()
        {
            PATTERN? expr_ref = null;
            var atom = ANY(ALPHA) | σ("(") + ζ(() => expr_ref!) + σ(")");
            var expr = atom + ARBNO((σ("+") | σ("-")) + atom);
            expr_ref = expr;
            AssertNoMatch("1+", POS(0) + expr + RPOS(0));
        }

        // ── Φ / φ — regex patterns ────────────────────────────────────────────

        [Fact]
        public void Φ_matches_digits() => AssertMatch("123", POS(0) + Φ(@"\d+") + RPOS(0));

        [Fact]
        public void Φ_no_match_alpha()  => AssertNoMatch("abc", POS(0) + Φ(@"\d+") + RPOS(0));

        [Fact]
        public void Φ_named_groups_written_immediately()
        {
            FreshEnv();
            Engine.SEARCH("hello42", POS(0) + Φ(@"(?<word>[a-z]+)(?<num>\d+)"));
            Assert.Equal("hello", (string)(Slot)S4._.word);
            Assert.Equal("42",    (string)(Slot)S4._.num);
        }

        [Fact]
        public void φ_silent_on_failure()
        {
            FreshEnv();
            S4._.ctag = "before";
            Engine.FULLMATCH("abc123", POS(0) + φ(@"(?<ctag>[a-z]+)") + σ("NOPE") + RPOS(0));
            Assert.Equal("before", (string)(Slot)S4._.ctag);
        }

        [Fact]
        public void φ_fires_on_success()
        {
            FreshEnv();
            S4._.ctag = "before";
            Engine.FULLMATCH("abc", POS(0) + φ(@"(?<ctag>[a-z]+)") + RPOS(0));
            Assert.Equal("abc", (string)(Slot)S4._.ctag);
        }

        // ── % conditional and * immediate operators ───────────────────────────

        [Fact]
        public void percent_operator_captures_on_success()
        {
            FreshEnv();
            Engine.FULLMATCH("hello42",
                POS(0) + (SPAN(ALPHA) % (Slot)S4._.rw) + (SPAN(DIGITS) % (Slot)S4._.rn) + RPOS(0));
            Assert.Equal("hello", (string)(Slot)S4._.rw);
            Assert.Equal("42",    (string)(Slot)S4._.rn);
        }

        // ── ζ(string) — name lookup ───────────────────────────────────────────

        [Fact]
        public void ζ_string_forward_reference()
        {
            FreshEnv();
            PATTERN atom = SPAN(DIGITS) | σ("(") + ζ("rexpr") + σ(")");
            PATTERN expr = atom + ARBNO((σ("+") | σ("-")) + atom);
            S4._.rexpr = expr;
            AssertMatch("1+(2+3)", POS(0) + expr + RPOS(0));
            AssertNoMatch("1+",    POS(0) + expr + RPOS(0));
        }

        // ── ident / real — classic SNOBOL4 patterns ──────────────────────────

        [Fact]
        public void identifier_matches()
        {
            var ident = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);
            AssertMatch("Hello42", ident);
        }

        [Fact]
        public void identifier_no_match_leading_digit()
        {
            var ident = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);
            AssertNoMatch("1bad", ident);
        }

        [Fact]
        public void real_number_matches()
        {
            var real = POS(0) + ~ANY("+-") + SPAN(DIGITS) + ~(σ(".") + NSPAN(DIGITS)) + RPOS(0);
            AssertMatch("+3.14", real);
        }

        [Fact]
        public void real_number_no_match_alpha()
        {
            var real = POS(0) + ~ANY("+-") + SPAN(DIGITS) + ~(σ(".") + NSPAN(DIGITS)) + RPOS(0);
            AssertNoMatch("abc", real);
        }

        [Fact]
        public void ARBNO_repeating_literal() =>
            AssertMatch("ababab", POS(0) + ARBNO(σ("ab")) + RPOS(0));

        [Fact]
        public void BAL_balanced_expression() =>
            AssertMatch("(a+b)", POS(0) + BAL() + RPOS(0));

        // ── Deferred Func<> argument overloads ───────────────────────────────

        [Fact]
        public void σ_deferred_reads_env_at_match_time()
        {
            FreshEnv();
            S4._.kw = "hello";
            var p = POS(0) + σ(() => (string)(Slot)S4._.kw) + RPOS(0);
            AssertMatch("hello", p);
            AssertNoMatch("world", p);
            S4._.kw = "world";
            AssertMatch("world", p);
            AssertNoMatch("hello", p);
        }

        [Fact]
        public void POS_deferred_reads_env_at_match_time()
        {
            FreshEnv();
            S4._.p = 2;
            var pat = POS(() => (int)(Slot)S4._.p) + SPAN(DIGITS) % (Slot)S4._.found;
            S4._.found = "";
            Engine.SEARCH("ab42cd", pat);
            Assert.Equal("42", (string)(Slot)S4._.found);
        }

        [Fact]
        public void RPOS_deferred_reads_env_at_match_time()
        {
            FreshEnv();
            S4._.r = 0;
            var p = POS(0) + SPAN(ALPHA) % (Slot)S4._.w + RPOS(() => (int)(Slot)S4._.r);
            AssertMatch("hello", p);
            S4._.r = 3;
            AssertNoMatch("hello", p);
        }

        [Fact]
        public void LEN_deferred()
        {
            FreshEnv();
            S4._.n = 3;
            var p = POS(0) + LEN(() => (int)(Slot)S4._.n) % (Slot)S4._.chunk + RPOS(0);
            AssertMatch("abc", p);
            Assert.Equal("abc", (string)(Slot)S4._.chunk);
            AssertNoMatch("ab", p);
            S4._.n = 2;
            AssertMatch("ab", p);
        }

        [Fact]
        public void TAB_deferred()
        {
            FreshEnv();
            S4._.t = 3;
            var p = POS(0) + TAB(() => (int)(Slot)S4._.t) % (Slot)S4._.pre + REM() % (Slot)S4._.suf;
            Engine.FULLMATCH("abcdef", p);
            Assert.Equal("abc", (string)(Slot)S4._.pre);
            Assert.Equal("def", (string)(Slot)S4._.suf);
        }

        [Fact]
        public void RTAB_deferred()
        {
            FreshEnv();
            S4._.rtabN = 2;
            var p = POS(0) + RTAB(() => (int)(Slot)S4._.rtabN) % (Slot)S4._.rtabBody + REM() % (Slot)S4._.rtabTail;
            Engine.FULLMATCH("abcdef", p);
            Assert.Equal("abcd", (string)(Slot)S4._.rtabBody);
            Assert.Equal("ef",   (string)(Slot)S4._.rtabTail);
        }

        [Theory]
        [InlineData("a", true)]
        [InlineData("b", false)]
        public void ANY_deferred(string subject, bool shouldMatch)
        {
            FreshEnv();
            S4._.ch = "aeiou";
            var p = POS(0) + ANY(() => (string)(Slot)S4._.ch) + RPOS(0);
            if (shouldMatch) AssertMatch(subject, p);
            else             AssertNoMatch(subject, p);
        }

        [Theory]
        [InlineData("b", true)]
        [InlineData("a", false)]
        public void NOTANY_deferred(string subject, bool shouldMatch)
        {
            FreshEnv();
            S4._.ex = "aeiou";
            var p = POS(0) + NOTANY(() => (string)(Slot)S4._.ex) + RPOS(0);
            if (shouldMatch) AssertMatch(subject, p);
            else             AssertNoMatch(subject, p);
        }

        [Fact]
        public void SPAN_deferred()
        {
            FreshEnv();
            S4._.cs = DIGITS;
            var p = POS(0) + SPAN(() => (string)(Slot)S4._.cs) % (Slot)S4._.tok + RPOS(0);
            AssertMatch("123", p);
            AssertNoMatch("abc", p);
            S4._.cs = ALPHA;
            AssertMatch("abc", p);
            AssertNoMatch("123", p);
        }

        [Fact]
        public void NSPAN_deferred_accepts_empty()
        {
            FreshEnv();
            S4._.cs = DIGITS;
            var p = POS(0) + NSPAN(() => (string)(Slot)S4._.cs) % (Slot)S4._.tok + RPOS(0);
            AssertMatch("123", p);
            AssertMatch("", p);
        }

        [Fact]
        public void BREAK_deferred()
        {
            FreshEnv();
            S4._.dlm = ":";
            var p = POS(0) + BREAK(() => (string)(Slot)S4._.dlm) % (Slot)S4._.k
                  + σ(() => (string)(Slot)S4._.dlm)
                  + REM() % (Slot)S4._.v;
            Engine.SEARCH("name:val", p);
            Assert.Equal("name", (string)(Slot)S4._.k);
            Assert.Equal("val",  (string)(Slot)S4._.v);
            S4._.dlm = "=";
            Engine.SEARCH("x=42", p);
            Assert.Equal("x",  (string)(Slot)S4._.k);
            Assert.Equal("42", (string)(Slot)S4._.v);
        }

        [Fact]
        public void BREAKX_deferred()
        {
            FreshEnv();
            S4._.bxDelim = ",";
            var p = POS(0) + BREAKX(() => (string)(Slot)S4._.bxDelim) % (Slot)S4._.bxFirst
                  + σ(() => (string)(Slot)S4._.bxDelim)
                  + REM() % (Slot)S4._.bxRest;
            Engine.SEARCH("a,b,c", p);
            Assert.Equal("a",   (string)(Slot)S4._.bxFirst);
            Assert.Equal("b,c", (string)(Slot)S4._.bxRest);
        }

        [Fact]
        public void combined_deferred_charset_and_literal()
        {
            FreshEnv();
            Engine.FULLMATCH("0123", POS(0) + SPAN(ALNUM) % (Slot)S4._.valid + RPOS(0));
            var chk = POS(0) + SPAN(() => (string)(Slot)S4._.valid) + RPOS(0);
            AssertMatch("031", chk);
            AssertNoMatch("456", chk);
        }
    }
}
