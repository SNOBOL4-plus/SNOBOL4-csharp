// Tests_RE_Grammar.cs — shift-reduce RE grammar (mirrors test_re_simple.py)
//
// Builds a tiny regex meta-grammar using Shift/Reduce/Pop and verifies that
// it correctly parses well-formed regex strings and rejects malformed ones.
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_RE_Grammar : TestBase
    {
        // ── Grammar definition (module-level, constructed once) ───────────────
        //
        // re_Quantifier = σ('*')|σ('+')|σ('?')   with Shift of the tag
        // re_Item       = σ('.')|ANY(alnum)|σ('(')…σ(')')   with recursive ζ
        // re_Factor     = re_Item + optional quantifier → Reduce('ς',2)
        // re_Term       = nPush + ARBNO(re_Factor+nInc) + Reduce('Σ') + nPop
        // re_Expression = nPush + re_Term+nInc + ARBNO('|'+re_Term+nInc) + Reduce('Π') + nPop
        // re_RegEx      = POS(0) + re_Expression + Pop('RE_tree') + RPOS(0)

        static readonly PATTERN re_Quantifier =
              σ("*") + Shift("*")
            | σ("+") + Shift("+")
            | σ("?") + Shift("?");

        // re_Item references re_Expression via ζ(lambda) for the group case
        static readonly PATTERN re_Item =
              σ(".") + Shift(".")
            | σ("\\") + ANY(".\\" + "(|*+?)") % (Slot)S4._.tx
                      + Shift("σ", () => (string)(Slot)S4._.tx)
            | ANY("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
                      % (Slot)S4._.tx
                      + Shift("σ", () => (string)(Slot)S4._.tx)
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
              POS(0) + re_Expression + Pop((Slot)S4._.RE_tree) + RPOS(0);

        // ── Parses ────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("")]
        [InlineData("A")]
        [InlineData("AA")]
        [InlineData("AAA")]
        [InlineData("A*")]
        [InlineData("A+")]
        [InlineData("A?")]
        [InlineData("A|B")]
        [InlineData("A|BC")]
        [InlineData("AB|C")]
        [InlineData("(A|)")]
        [InlineData("(A|B)*")]
        [InlineData("(A|B)+")]
        [InlineData("(A|B)?")]
        [InlineData("(A|B)C")]
        [InlineData("(A|)*")]
        [InlineData("A|(BC)")]
        [InlineData("(AB|CD)")]
        [InlineData("(AB*|CD*)")]
        [InlineData("((AB)*|(CD)*)")]
        [InlineData("(A|(BC))")]
        [InlineData("((AB)|C)")]
        [InlineData("(Ab|(CD))")]
        [InlineData("A(A|B)*B")]
        public void re_parses(string rex)
        {
            FreshEnv();
            AssertMatch(rex, re_RegEx);
        }

        // ── Rejects ───────────────────────────────────────────────────────────

        [Theory]
        [InlineData("(")]
        [InlineData(")")]
        [InlineData("*")]
        [InlineData("+")]
        public void re_no_parse(string bad)
        {
            FreshEnv();
            AssertNoMatch(bad, re_RegEx);
        }

        // ── Tree shape ────────────────────────────────────────────────────────

        [Fact]
        public void re_tree_is_list_with_at_least_one_element()
        {
            FreshEnv();
            Engine.FULLMATCH("A|B", re_RegEx);
            Assert.True(Env.G.TryGetValue("RE_tree", out var tree));
            var lst = Assert.IsType<List<object>>(tree);
            Assert.True(lst.Count >= 1);
        }
    }
}
