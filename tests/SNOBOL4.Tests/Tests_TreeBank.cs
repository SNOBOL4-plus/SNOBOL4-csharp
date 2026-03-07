// Tests_TreeBank.cs — parses VBGinTASA.dat
//
// Format: Penn Treebank parenthesized trees, one continuous stream.
//   (S (NP (DT the) (NN power)) (VP (VBG taxing) (NP (PRP it))))
//
// The treebank pattern builds a nested List<object> tree:
//   ["BANK", ["ROOT", ["S", ["NP",...], ["VP",...]], ...], ...]
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.IO;
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_TreeBank : TestBase
    {
        static readonly List<object> bank = new() { "BANK" };
        static readonly bool balanceOk;
        static readonly bool parseSucceeded;

        static Tests_TreeBank()
        {
            string src = File.ReadAllText("VBGinTASA.dat");
            balanceOk = Engine.FULLMATCH(src, POS(0) + BAL() + RPOS(0)) != null;
            if (!balanceOk) { parseSucceeded = false; return; }

            var stack   = new System.Collections.Generic.Stack<List<object>>();
            var delim   = SPAN(" \n");
            string tbTag = "", tbWord = "";

            var word = (NOTANY("() \n") + BREAK("() \n")) % (v => tbWord = v);

            PATTERN? groupRef = null;

            var group =
                  σ("(")
                + word % (v => tbTag = v)
                + λ(() => stack.Push(new List<object> { tbTag }))
                + ARBNO(
                      delim
                    + (   ζ(() => groupRef!)
                        | word + λ(() => stack.Peek().Add(tbWord))
                      )
                  )
                + λ(() => {
                      var node = stack.Pop();
                      if (stack.Count > 0) stack.Peek().Add(node);
                      else                 bank.Add(node);
                  })
                + σ(")");

            groupRef = group;

            var treebank =
                  POS(0)
                + ARBNO(
                      λ(() => stack.Push(new List<object> { "ROOT" }))
                    + ARBNO(group)
                    + λ(() => bank.Add(stack.Pop()))
                    + delim
                  )
                + RPOS(0);

            parseSucceeded = Engine.SEARCH(src, treebank) != null;
        }

        static bool HasTag(List<object> node, string tag)
        {
            if (node.Count >= 1 && node[0] is string t && t == tag) return true;
            foreach (var child in node)
                if (child is List<object> c && HasTag(c, tag)) return true;
            return false;
        }

        static int CountRootsWithTag(string tag)
        {
            int count = 0;
            for (int i = 1; i < bank.Count; i++)
                if (bank[i] is List<object> root && HasTag(root, tag)) count++;
            return count;
        }

        [Fact] public void treebank_file_is_balanced()       => Assert.True(balanceOk);
        [Fact] public void treebank_parse_succeeds()         => Assert.True(parseSucceeded);
        [Fact] public void treebank_bank_tag_is_BANK()       => Assert.Equal("BANK", bank[0]);
        [Fact] public void treebank_contains_multiple_roots()=> Assert.True(bank.Count > 2);
        [Fact] public void treebank_first_root_tagged_ROOT() =>
            Assert.Equal("ROOT", ((List<object>)bank[1])[0]);
        [Fact] public void treebank_VBG_nodes_present()      => Assert.True(CountRootsWithTag("VBG") > 0);
        [Fact] public void treebank_first_sentence_contains_VBG() =>
            Assert.True(HasTag((List<object>)bank[1], "VBG"));

        [Fact]
        public void treebank_most_roots_contain_VBG()
        {
            int rootCount = bank.Count - 1;
            int vbgRoots  = CountRootsWithTag("VBG");
            Assert.True(vbgRoots > rootCount / 2,
                $"only {vbgRoots} of {rootCount} roots contain VBG");
        }
    }
}
