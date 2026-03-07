// Tests_Primitives.cs — core pattern primitives
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
            var p = POS(0) + nPush() + σ("a") + nInc() + σ("b") + nInc()
                  + σ("c") + nInc() + nPop() + RPOS(0);
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
            string word = "";
            List<object>? tree = null;
            var p = POS(0)
                  + SPAN(ALPHA) % (v => word = v)
                  + Shift("Word")
                  + Pop(t => tree = t)
                  + RPOS(0);
            Engine.FULLMATCH("hello", p);
            Assert.NotNull(tree);
            Assert.Equal("Word", tree![0]);
        }

        [Fact]
        public void Shift_with_value_expression()
        {
            string n = "";
            List<object>? tree = null;
            var p = POS(0)
                  + SPAN(DIGITS) % (v => n = v)
                  + Shift("Int", () => (object)int.Parse(n))
                  + Pop(t => tree = t)
                  + RPOS(0);
            Engine.FULLMATCH("42", p);
            Assert.NotNull(tree);
            Assert.Equal("Int", tree![0]);
            Assert.Equal(42,    tree[1]);
        }

        [Fact]
        public void Reduce_wraps_two_children()
        {
            string a = "", b = "";
            List<object>? tree = null;
            var p = POS(0)
                  + nPush()
                  + SPAN(UCASE) % (v => a = v) + Shift("A") + nInc()
                  + SPAN(LCASE) % (v => b = v) + Shift("B") + nInc()
                  + Reduce("Pair")
                  + nPop()
                  + Pop(t => tree = t)
                  + RPOS(0);
            Engine.FULLMATCH("HELLOworld", p);
            Assert.NotNull(tree);
            Assert.Equal("Pair", tree![0]);
            Assert.Equal(2, tree.Count - 1);
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
        public void Φ_matches_digits()
        {
            var groups = new Dictionary<string,string>();
            AssertMatch("123", POS(0) + Φ(@"\d+", (n,v) => groups[n]=v) + RPOS(0));
        }

        [Fact]
        public void Φ_no_match_alpha()
        {
            var groups = new Dictionary<string,string>();
            AssertNoMatch("abc", POS(0) + Φ(@"\d+", (n,v) => groups[n]=v) + RPOS(0));
        }

        [Fact]
        public void Φ_named_groups_written_immediately()
        {
            string word = "", num = "";
            Engine.SEARCH("hello42",
                POS(0) + Φ(@"(?<word>[a-z]+)(?<num>\d+)", (n,v) => {
                    if (n == "word") word = v;
                    if (n == "num")  num  = v;
                }));
            Assert.Equal("hello", word);
            Assert.Equal("42",    num);
        }

        [Fact]
        public void φ_silent_on_failure()
        {
            string ctag = "before";
            Engine.FULLMATCH("abc123",
                POS(0) + φ(@"(?<ctag>[a-z]+)", (n,v) => ctag = v) + σ("NOPE") + RPOS(0));
            Assert.Equal("before", ctag);
        }

        [Fact]
        public void φ_fires_on_success()
        {
            string ctag = "before";
            Engine.FULLMATCH("abc",
                POS(0) + φ(@"(?<ctag>[a-z]+)", (n,v) => ctag = v) + RPOS(0));
            Assert.Equal("abc", ctag);
        }

        // ── % conditional operator ────────────────────────────────────────────

        [Fact]
        public void percent_operator_captures_on_success()
        {
            string rw = "", rn = "";
            Engine.FULLMATCH("hello42",
                POS(0) + SPAN(ALPHA) % (v => rw = v)
                       + SPAN(DIGITS) % (v => rn = v) + RPOS(0));
            Assert.Equal("hello", rw);
            Assert.Equal("42",    rn);
        }

        // ── σ deferred ────────────────────────────────────────────────────────

        [Fact]
        public void σ_deferred_reads_variable_at_match_time()
        {
            string kw = "hello";
            var p = POS(0) + σ(() => kw) + RPOS(0);
            AssertMatch("hello", p);
            AssertNoMatch("world", p);
            kw = "world";
            AssertMatch("world", p);
            AssertNoMatch("hello", p);
        }

        // ── Deferred positional arguments ─────────────────────────────────────

        [Fact]
        public void POS_deferred()
        {
            int pos = 2; string found = "";
            var pat = POS(() => pos) + SPAN(DIGITS) % (v => found = v);
            Engine.SEARCH("ab42cd", pat);
            Assert.Equal("42", found);
        }

        [Fact]
        public void RPOS_deferred()
        {
            int r = 0; string w = "";
            var p = POS(0) + SPAN(ALPHA) % (v => w = v) + RPOS(() => r);
            AssertMatch("hello", p);
            r = 3;
            AssertNoMatch("hello", p);
        }

        [Fact]
        public void LEN_deferred()
        {
            int n = 3; string chunk = "";
            var p = POS(0) + LEN(() => n) % (v => chunk = v) + RPOS(0);
            AssertMatch("abc", p);
            Assert.Equal("abc", chunk);
            AssertNoMatch("ab", p);
            n = 2;
            AssertMatch("ab", p);
        }

        [Fact]
        public void TAB_deferred()
        {
            int t = 3; string pre = "", suf = "";
            var p = POS(0) + TAB(() => t) % (v => pre = v) + REM() % (v => suf = v);
            Engine.FULLMATCH("abcdef", p);
            Assert.Equal("abc", pre);
            Assert.Equal("def", suf);
        }

        [Fact]
        public void RTAB_deferred()
        {
            int rt = 2; string body = "", tail = "";
            var p = POS(0) + RTAB(() => rt) % (v => body = v) + REM() % (v => tail = v);
            Engine.FULLMATCH("abcdef", p);
            Assert.Equal("abcd", body);
            Assert.Equal("ef",   tail);
        }

        [Theory]
        [InlineData("a", true)]
        [InlineData("b", false)]
        public void ANY_deferred(string subject, bool shouldMatch)
        {
            string ch = "aeiou";
            var p = POS(0) + ANY(() => ch) + RPOS(0);
            if (shouldMatch) AssertMatch(subject, p);
            else             AssertNoMatch(subject, p);
        }

        [Theory]
        [InlineData("b", true)]
        [InlineData("a", false)]
        public void NOTANY_deferred(string subject, bool shouldMatch)
        {
            string ex = "aeiou";
            var p = POS(0) + NOTANY(() => ex) + RPOS(0);
            if (shouldMatch) AssertMatch(subject, p);
            else             AssertNoMatch(subject, p);
        }

        [Fact]
        public void SPAN_deferred()
        {
            string cs = DIGITS; string tok = "";
            var p = POS(0) + SPAN(() => cs) % (v => tok = v) + RPOS(0);
            AssertMatch("123", p);
            AssertNoMatch("abc", p);
            cs = ALPHA;
            AssertMatch("abc", p);
            AssertNoMatch("123", p);
        }

        [Fact]
        public void NSPAN_deferred_accepts_empty()
        {
            string cs = DIGITS; string tok = "";
            var p = POS(0) + NSPAN(() => cs) % (v => tok = v) + RPOS(0);
            AssertMatch("123", p);
            AssertMatch("", p);
        }

        [Fact]
        public void BREAK_deferred()
        {
            string dlm = ":"; string k = "", v = "";
            var p = POS(0) + BREAK(() => dlm) % (x => k = x)
                  + σ(() => dlm)
                  + REM() % (x => v = x);
            Engine.SEARCH("name:val", p);
            Assert.Equal("name", k);
            Assert.Equal("val",  v);
            dlm = "=";
            Engine.SEARCH("x=42", p);
            Assert.Equal("x",  k);
            Assert.Equal("42", v);
        }

        [Fact]
        public void BREAKX_deferred()
        {
            string dlm = ","; string first = "", rest = "";
            var p = POS(0) + BREAKX(() => dlm) % (x => first = x)
                  + σ(() => dlm)
                  + REM() % (x => rest = x);
            Engine.SEARCH("a,b,c", p);
            Assert.Equal("a",   first);
            Assert.Equal("b,c", rest);
        }

        // ── Classic patterns ──────────────────────────────────────────────────

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
            var real = POS(0) + ~ANY("+-") + SPAN(DIGITS)
                     + ~(σ(".") + NSPAN(DIGITS)) + RPOS(0);
            AssertMatch("+3.14", real);
        }

        [Fact]
        public void ARBNO_repeating_literal() =>
            AssertMatch("ababab", POS(0) + ARBNO(σ("ab")) + RPOS(0));

        [Fact]
        public void BAL_balanced_expression() =>
            AssertMatch("(a+b)", POS(0) + BAL() + RPOS(0));
    }
}
