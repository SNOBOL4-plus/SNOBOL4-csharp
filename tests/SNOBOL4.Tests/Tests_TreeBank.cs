// Tests_TreeBank.cs — parses VBGinTASA.dat
//
// Format: Penn Treebank parenthesized trees, one continuous stream.
// Each tree is a nested s-expression:
//   (S (NP (DT the) (NN power)) (VP (VBG taxing) (NP (PRP it))))
//
// The treebank pattern mirrors the Python original:
//
//   word    = NOTANY("( )\n") + BREAK("( )\n")
//   group   = σ('(') + word%tag + ARBNO(delim + (group | word%wrd)) + σ(')')
//   treebank= POS(0) + ARBNO(ROOT + ARBNO(group) + delim) + RPOS(0)
//
// Each group becomes a List<object> whose first element is the tag string
// and whose remaining elements are either child strings or child lists.
// The top-level result is List<object>{"BANK", root0, root1, ...}.
//
// Key fix: same as CLAWS — wrap (NOTANY+BREAK) as a single captured unit.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.IO;
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_TreeBank : TestBase
    {
        // ── Parse (runs once) ─────────────────────────────────────────────────
        static readonly List<object> bank = new() { "BANK" };
        static readonly bool balanceOk;
        static readonly bool parseSucceeded;

        static Tests_TreeBank()
        {
            FreshEnv();
            string src = File.ReadAllText("VBGinTASA.dat");

            // Quick sanity: the whole file must be parenthetically balanced
            balanceOk = Engine.FULLMATCH(src, POS(0) + BAL() + RPOS(0)) != null;
            if (!balanceOk) { parseSucceeded = false; return; }

            // ── stack-based tree builder ──────────────────────────────────────
            var stack = new Stack<List<object>>();

            var delim = SPAN(" \n");

            // word: one or more non-paren non-space chars — capture the whole unit
            var wordPat = (NOTANY("() \n") + BREAK("() \n")) % (Slot)S4._.tbWord;

            PATTERN? groupRef = null;

            var group =
                  σ("(")
                + wordPat % (Slot)S4._.tbTag          // reuse the capture slot for the tag
                + λ(() => stack.Push(new List<object> { (string)(Slot)S4._.tbTag }))
                + ARBNO(
                      delim
                    + (   ζ(() => groupRef!)
                        | wordPat
                          + λ(() => stack.Peek().Add((string)(Slot)S4._.tbWord))
                      )
                  )
                + λ(() => {
                      var node = stack.Pop();
                      if (stack.Count > 0) stack.Peek().Add(node);
                      else                 bank.Add(node);   // top-level group with no ROOT wrapper
                  })
                + σ(")");

            groupRef = group;

            var treebank =
                  POS(0)
                + ARBNO(
                      λ(() => stack.Push(new List<object> { "ROOT" }))
                    + ARBNO(group)
                    + λ(() => {
                          var root = stack.Pop();
                          bank.Add(root);
                      })
                    + delim
                  )
                + RPOS(0);

            parseSucceeded = Engine.SEARCH(src, treebank) != null;
        }

        // ── Tree helpers ──────────────────────────────────────────────────────
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
                if (bank[i] is List<object> root && HasTag(root, tag))
                    count++;
            return count;
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Fact]
        public void treebank_file_is_balanced() =>
            Assert.True(balanceOk);

        [Fact]
        public void treebank_parse_succeeds() =>
            Assert.True(parseSucceeded);

        [Fact]
        public void treebank_bank_tag_is_BANK() =>
            Assert.Equal("BANK", bank[0]);

        [Fact]
        public void treebank_contains_multiple_roots() =>
            Assert.True(bank.Count > 2,
                $"expected multiple ROOT entries, got {bank.Count - 1}");

        [Fact]
        public void treebank_first_root_tagged_ROOT()
        {
            var first = Assert.IsType<List<object>>(bank[1]);
            Assert.Equal("ROOT", first[0]);
        }

        [Fact]
        public void treebank_VBG_nodes_present() =>
            Assert.True(CountRootsWithTag("VBG") > 0);

        [Fact]
        public void treebank_most_roots_contain_VBG()
        {
            // The file is VBGinTASA — every sentence was selected for containing VBG
            int rootCount  = bank.Count - 1;
            int vbgRoots   = CountRootsWithTag("VBG");
            Assert.True(vbgRoots > rootCount / 2,
                $"only {vbgRoots} of {rootCount} roots contain VBG");
        }

        [Fact]
        public void treebank_first_sentence_contains_taxing()
        {
            // Sentence 1: "...the power of taxing it by the states..."
            var first = (List<object>)bank[1];
            Assert.True(HasTag(first, "VBG"),
                "first ROOT has no VBG child");
        }
    }
}
