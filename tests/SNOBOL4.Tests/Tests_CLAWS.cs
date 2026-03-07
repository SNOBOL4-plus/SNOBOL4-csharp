// Tests_CLAWS.cs — parses CLAWS5inTASA.dat
//
// Format: each token is  word_TAG  separated by spaces or newlines.
// Sentence boundaries are marked:  42_CRD :_PUN
//
// The parser builds:  mem[sentenceNum][word][tag] = count
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
        static readonly Dictionary<int, Dictionary<string, Dictionary<string, int>>> mem = new();
        static readonly bool parseSucceeded;

        static Tests_CLAWS()
        {
            string wrd = "", tag = "", num = "";
            int curNum = 0;

            var sep = SPAN(" \n");

            var taggedWord =
                  (NOTANY("_() \n") + BREAK("_")) % (v => wrd = v)
                + σ("_")
                + (ANY(UCASE) + SPAN(DIGITS + UCASE)) % (v => tag = v)
                + λ(() => {
                      if (!mem.ContainsKey(curNum))        mem[curNum]      = new();
                      if (!mem[curNum].ContainsKey(wrd))   mem[curNum][wrd] = new();
                      if (!mem[curNum][wrd].ContainsKey(tag)) mem[curNum][wrd][tag] = 0;
                      mem[curNum][wrd][tag]++;
                  });

            var sentenceHeader =
                  SPAN(DIGITS) % (v => num = v)
                + σ("_CRD :_PUN")
                + λ(() => {
                      curNum = int.Parse(num);
                      mem[curNum] = new();
                  });

            var claws_info =
                  POS(0)
                + ARBNO((sentenceHeader | taggedWord) + sep)
                + RPOS(0);

            string data = string.Join("", File.ReadAllLines("CLAWS5inTASA.dat"));
            parseSucceeded = Engine.SEARCH(data, claws_info) != null;
        }

        [Fact] public void claws_parse_succeeds()          => Assert.True(parseSucceeded);
        [Fact] public void claws_produces_244_sentences()  => Assert.Equal(244, mem.Count);
        [Fact] public void claws_sentence_1_contains_taxing() =>
            Assert.True(mem[1].ContainsKey("taxing"));
        [Fact] public void claws_taxing_tagged_VVG()       => Assert.True(mem[1]["taxing"].ContainsKey("VVG"));
        [Fact] public void claws_taxing_VVG_count_is_1()   => Assert.Equal(1, mem[1]["taxing"]["VVG"]);
        [Fact] public void claws_sentence_1_contains_power() => Assert.True(mem[1].ContainsKey("power"));
        [Fact] public void claws_power_tagged_NN1()        => Assert.True(mem[1]["power"].ContainsKey("NN1"));
        [Fact] public void claws_sentence_1_that_tagged_CJT() =>
            Assert.True(mem[1].ContainsKey("That") && mem[1]["That"].ContainsKey("CJT"));

        [Fact]
        public void claws_VVG_tags_appear_across_corpus()
        {
            int count = 0;
            foreach (var sent in mem.Values)
                foreach (var wordTags in sent.Values)
                    if (wordTags.ContainsKey("VVG")) count++;
            Assert.True(count > 0);
        }
    }
}
