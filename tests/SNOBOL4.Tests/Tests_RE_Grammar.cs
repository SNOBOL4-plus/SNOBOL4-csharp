// Tests_RE_Grammar.cs — shift-reduce RE grammar (mirrors test_re_simple.py)
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_RE_Grammar : TestBase
    {
        static string _tx = "";
        static List<object>? _tree = null;

        static readonly PATTERN re_Quantifier =
              σ("*") + Shift("*")
            | σ("+") + Shift("+")
            | σ("?") + Shift("?");

        static readonly PATTERN re_Item =
              σ(".") + Shift(".")
            | σ("\\") + ANY(".\\" + "(|*+?)") % (v => _tx = v)
                      + Shift("σ", () => (object)_tx)
            | ANY(ALPHA + DIGITS) % (v => _tx = v)
                      + Shift("σ", () => (object)_tx)
            | σ("(") + ζ(() => re_Expression) + σ(")");

        static readonly PATTERN re_Factor =
              re_Item + (re_Quantifier + Reduce("ς", 2) | ε());

        static readonly PATTERN re_Term =
              nPush() + ARBNO(re_Factor + nInc()) + Reduce("Σ") + nPop();

        static readonly PATTERN re_Expression =
              nPush()
            + re_Term + nInc()
            + ARBNO(σ("|") + re_Term + nInc())
            + Reduce("Π")
            + nPop();

        static readonly PATTERN re_RegEx =
              POS(0) + re_Expression + Pop(t => _tree = t) + RPOS(0);

        // ── Parses ────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("")]         [InlineData("A")]        [InlineData("AA")]
        [InlineData("AAA")]      [InlineData("A*")]        [InlineData("A+")]
        [InlineData("A?")]       [InlineData("A|B")]       [InlineData("A|BC")]
        [InlineData("AB|C")]     [InlineData("(A|)")]      [InlineData("(A|B)*")]
        [InlineData("(A|B)+")]   [InlineData("(A|B)?")]    [InlineData("(A|B)C")]
        [InlineData("(A|)*")]    [InlineData("A|(BC)")]    [InlineData("(AB|CD)")]
        [InlineData("(AB*|CD*)")][InlineData("((AB)*|(CD)*)")]
        [InlineData("(A|(BC))")] [InlineData("((AB)|C)")]  [InlineData("(Ab|(CD))")]
        [InlineData("A(A|B)*B")]
        public void re_parses(string rex) => AssertMatch(rex, re_RegEx);

        // ── Rejects ───────────────────────────────────────────────────────────

        [Theory]
        [InlineData("(")] [InlineData(")")] [InlineData("*")] [InlineData("+")]
        public void re_no_parse(string bad) => AssertNoMatch(bad, re_RegEx);

        // ── Tree shape ────────────────────────────────────────────────────────

        [Fact]
        public void re_tree_is_list_with_at_least_one_element()
        {
            _tree = null;
            Engine.FULLMATCH("A|B", re_RegEx);
            Assert.NotNull(_tree);
            Assert.True(_tree!.Count >= 1);
        }
    }
}
