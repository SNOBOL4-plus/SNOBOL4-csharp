// Tests_CLAWS.cs — parses CLAWS5inTASA.dat
//
// Format: each token is  word_TAG  separated by spaces or newlines.
// Sentence boundaries are marked:  42_CRD :_PUN
//
// The parser builds:  mem[sentenceNum][word][tag] = count
//
// Key fix vs the Python original: the Python pattern is
//   word = NOTANY("( )\n") + BREAK("( )\n")
// which matches one-or-more non-delimiter characters.  In C# the capture
// must wrap the whole unit — (NOTANY(...) + BREAK(...)) % slot — so that
// the first character is not swallowed before the slot is written.
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using System.IO;
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_CLAWS : TestBase
    {
        // ── Parse (runs once in the static constructor) ───────────────────────
        static readonly Dictionary<int, Dictionary<string, Dictionary<string, int>>> mem = new();
        static readonly bool parseSucceeded;

        static Tests_CLAWS()
        {
            FreshEnv();

            // Separators between tokens: one or more spaces or newlines
            var sep = SPAN(" \n");

            // A tagged token looks like:  taxing_VVG
            // Capture the whole word (including first char) then the tag.
            var taggedWord =
                  (NOTANY("_() \n") + BREAK("_")) % (Slot)S4._.wrd
                + σ("_")
                + (ANY(UCASE) + SPAN(DIGITS + UCASE)) % (Slot)S4._.tag
                + λ(() => {
                      int    n = (int)(Slot)S4._.curNum;
                      string w = (string)(Slot)S4._.wrd;
                      string t = (string)(Slot)S4._.tag;
                      if (!mem.ContainsKey(n))    mem[n]    = new();
                      if (!mem[n].ContainsKey(w)) mem[n][w] = new();
                      if (!mem[n][w].ContainsKey(t)) mem[n][w][t] = 0;
                      mem[n][w][t]++;
                  });

            // Sentence header looks like:  1_CRD :_PUN
            var sentenceHeader =
                  SPAN(DIGITS) % (Slot)S4._.num
                + σ("_CRD :_PUN")
                + λ(() => {
                      int n = int.Parse((string)(Slot)S4._.num);
                      S4._.curNum = n;
                      mem[n] = new();
                  });

            var claws_info =
                  POS(0)
                + ARBNO(
                      (sentenceHeader | taggedWord)
                    + sep
                  )
                + RPOS(0);

            string data = string.Join("", File.ReadAllLines("CLAWS5inTASA.dat"));
            parseSucceeded = Engine.SEARCH(data, claws_info) != null;
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Fact]
        public void claws_parse_succeeds() =>
            Assert.True(parseSucceeded);

        [Fact]
        public void claws_produces_244_sentences() =>
            Assert.Equal(244, mem.Count);

        [Fact]
        public void claws_sentence_1_contains_taxing() =>
            Assert.True(mem[1].ContainsKey("taxing"),
                $"sentence 1 words: {string.Join(", ", mem[1].Keys)}");

        [Fact]
        public void claws_taxing_tagged_VVG() =>
            Assert.True(mem[1]["taxing"].ContainsKey("VVG"));

        [Fact]
        public void claws_taxing_VVG_count_is_1() =>
            Assert.Equal(1, mem[1]["taxing"]["VVG"]);

        [Fact]
        public void claws_sentence_1_contains_power() =>
            Assert.True(mem[1].ContainsKey("power"));

        [Fact]
        public void claws_power_tagged_NN1() =>
            Assert.True(mem[1]["power"].ContainsKey("NN1"));

        [Fact]
        public void claws_VVG_tags_appear_across_corpus()
        {
            int vvgCount = 0;
            foreach (var sent in mem.Values)
                foreach (var wordTags in sent.Values)
                    if (wordTags.ContainsKey("VVG"))
                        vvgCount++;
            Assert.True(vvgCount > 0);
        }

        [Fact]
        public void claws_sentence_1_that_tagged_CJT() =>
            Assert.True(mem[1].ContainsKey("That") && mem[1]["That"].ContainsKey("CJT"));
    }
}
