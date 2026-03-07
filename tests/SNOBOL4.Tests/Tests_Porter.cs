// Tests_Porter.cs — Porter Stemmer (ported from CSCE_5200_Homework_3.ipynb)
//
// The stemmer is a direct C# port of the SNOBOL4python implementation.
// Each pipeline step is a factory method that builds a fresh PATTERN with
// its own closed-over stem/target locals, so no shared state bleeds between
// steps or across Engine.SEARCH start-position attempts.
//
// The key insight driving this design:
//   * (immediate) writes during every candidate attempt that Engine.SEARCH
//   tries, including failed ones.  Static fields therefore get polluted by
//   earlier failed attempts within the same SEARCH call.  Per-call locals
//   (returned via out parameters from the factory) are clean each time.
//
// Verification: every (word, expectedStem) pair from porter_voc.txt /
// porter_output.txt is tested as a single bulk [Fact].
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_Porter : TestBase
    {
        const string vowels = "aeiou";

        // ── Pure helper functions (operate on an explicit stem string) ─────────

        static bool Cons(string s, int i) {
            if (s[i] == 'y') return i == 0 || !Cons(s, i - 1);
            return !vowels.Contains(s[i]);
        }

        static int M(string s) {
            if (s.Length == 0) return 0;
            int n = 0, i = 0, len = s.Length;
            while (i < len &&  Cons(s,i)) i++;
            if (i >= len) return 0; i++;
            while (true) {
                while (i < len && !Cons(s,i)) i++;
                if (i >= len) return n; i++; n++;
                while (i < len &&  Cons(s,i)) i++;
                if (i >= len) return n; i++;
            }
        }

        static bool VowelInStem(string s) =>
            Enumerable.Range(0, s.Length).Any(i => !Cons(s, i));

        static bool DoubleC(string s, int j) =>
            j >= 1 && s[j] == s[j-1] && Cons(s, j);

        static bool CVC(string s, int i) {
            if (i < 2 || !Cons(s,i) || !Cons(s,i-2) || Cons(s,i-1)) return false;
            return !"wxy".Contains(s[i]);
        }

        // ── Step helpers (take/return stem by value, set target) ──────────────

        static (string stem, string target) Step1b_eed(string stem) =>
            (stem, M(stem) > 0 ? "ee" : "eed");

        static (string stem, string target) Step1ab_cleanup(string stem) {
            if (stem.EndsWith("at") || stem.EndsWith("bl") || stem.EndsWith("iz"))
                return (stem, "e");
            if (stem.Length >= 2 && DoubleC(stem, stem.Length-1) && !"lsz".Contains(stem[^1]))
                return (stem[..^1], "");
            if (M(stem) == 1 && CVC(stem, stem.Length-1))
                return (stem, "e");
            return (stem, "");
        }

        // ── Pipeline step factory ─────────────────────────────────────────────
        // Each call builds a PATTERN and returns it along with a Func that
        // reads the captured result.  The closure vars are stack-allocated per
        // call to RunStep, so no cross-step leakage.

        static string? RunStep(string token, PATTERN pattern) {
            // Try the pattern; return updated token if it fired, else null.
            string capturedStem  = "";
            string? capturedTarget = null;

            // We need to run the match and read back what the pattern captured.
            // Because the patterns are pre-built statics (they close over
            // mutable refs), we use the instance approach: build per-call.
            // See MakeP* methods below.
            return null; // placeholder - see PorterStem for actual approach
        }

        // ── Per-call pattern builders ─────────────────────────────────────────
        // Each Make* method creates a fresh PATTERN with its own local stem/target.
        // This is the only way to avoid cross-call pollution with * (immediate).

        static string? TryStep(string token, Func<Box, PATTERN> makePattern) {
            var box = new Box();
            var pat = makePattern(box);
            if (Engine.SEARCH(token, pat) != null && box.Target != null)
                return box.Stem + box.Target;
            return null;
        }

        // Box holds the mutable stem/target for one pattern invocation
        class Box {
            public string  Stem   = "";
            public string? Target = null;
        }

        static Func<Box, PATTERN> MakeP1a() => b => POS(0)
            + ( RTAB(4) * (v=>b.Stem=v) + σ("sses") + λ(()=>b.Target="ss")
              | RTAB(3) * (v=>b.Stem=v) + σ("ies")  + λ(()=>b.Target="i")
              | RTAB(2) * (v=>b.Stem=v) + σ("ss")   + λ(()=>b.Target="ss")
              | RTAB(1) * (v=>b.Stem=v) + σ("s")    + λ(()=>b.Target="")
              ) + RPOS(0);

        static Func<Box, PATTERN> MakeP1b() => b => POS(0)
            + ( RTAB(3) * (v=>b.Stem=v) + σ("eed")
                    + λ(() => { var r=Step1b_eed(b.Stem); b.Stem=r.stem; b.Target=r.target; })
              | RTAB(2) * (v=>b.Stem=v) + σ("ed")
                    + Λ(() => VowelInStem(b.Stem))
                    + λ(() => { var r=Step1ab_cleanup(b.Stem); b.Stem=r.stem; b.Target=r.target; })
              | RTAB(3) * (v=>b.Stem=v) + σ("ing")
                    + Λ(() => VowelInStem(b.Stem))
                    + λ(() => { var r=Step1ab_cleanup(b.Stem); b.Stem=r.stem; b.Target=r.target; })
              ) + RPOS(0);

        static Func<Box, PATTERN> MakeP1c() => b => POS(0)
            + ( RTAB(1) * (v=>b.Stem=v) + σ("y")
                    + Λ(() => VowelInStem(b.Stem))
                    + λ(()=>b.Target="i")
              ) + RPOS(0);

        static Func<Box, PATTERN> MakeP2() => b => POS(0)
            + ( RTAB(7) * (v=>b.Stem=v) + σ("ational") + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ate")
              | RTAB(6) * (v=>b.Stem=v) + σ("tional")  + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="tion")
              | RTAB(4) * (v=>b.Stem=v) + σ("enci")    + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ence")
              | RTAB(4) * (v=>b.Stem=v) + σ("anci")    + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ance")
              | RTAB(4) * (v=>b.Stem=v) + σ("izer")    + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ize")
              | RTAB(3) * (v=>b.Stem=v) + σ("bli")     + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ble")
              | RTAB(4) * (v=>b.Stem=v) + σ("alli")    + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="al")
              | RTAB(5) * (v=>b.Stem=v) + σ("entli")   + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ent")
              | RTAB(3) * (v=>b.Stem=v) + σ("eli")     + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="e")
              | RTAB(5) * (v=>b.Stem=v) + σ("ousli")   + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ous")
              | RTAB(7) * (v=>b.Stem=v) + σ("ization") + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ize")
              | RTAB(5) * (v=>b.Stem=v) + σ("ation")   + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ate")
              | RTAB(4) * (v=>b.Stem=v) + σ("ator")    + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ate")
              | RTAB(5) * (v=>b.Stem=v) + σ("alism")   + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="al")
              | RTAB(7) * (v=>b.Stem=v) + σ("iveness") + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ive")
              | RTAB(7) * (v=>b.Stem=v) + σ("fulness") + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ful")
              | RTAB(7) * (v=>b.Stem=v) + σ("ousness") + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ous")
              | RTAB(5) * (v=>b.Stem=v) + σ("aliti")   + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="al")
              | RTAB(5) * (v=>b.Stem=v) + σ("iviti")   + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ive")
              | RTAB(6) * (v=>b.Stem=v) + σ("biliti")  + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ble")
              | RTAB(4) * (v=>b.Stem=v) + σ("logi")    + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="log")
              ) + RPOS(0);

        static Func<Box, PATTERN> MakeP3() => b => POS(0)
            + ( RTAB(5) * (v=>b.Stem=v) + σ("icate") + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ic")
              | RTAB(5) * (v=>b.Stem=v) + σ("ative") + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="")
              | RTAB(5) * (v=>b.Stem=v) + σ("alize") + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="al")
              | RTAB(5) * (v=>b.Stem=v) + σ("iciti") + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ic")
              | RTAB(4) * (v=>b.Stem=v) + σ("ical")  + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="ic")
              | RTAB(3) * (v=>b.Stem=v) + σ("ful")   + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="")
              | RTAB(4) * (v=>b.Stem=v) + σ("ness")  + Λ(()=>M(b.Stem)>0) + λ(()=>b.Target="")
              ) + RPOS(0);

        static Func<Box, PATTERN> MakeP4() => b => {
            void Check()    { b.Target = M(b.Stem) > 1 ? "" : null; }
            void IonCheck() { b.Target = M(b.Stem) > 1 && "st".Contains(b.Stem[^1]) ? "" : null; }
            return POS(0)
                + ( RTAB(2) * (v=>b.Stem=v) + σ("al")    + λ(Check)
                  | RTAB(4) * (v=>b.Stem=v) + σ("ance")  + λ(Check)
                  | RTAB(4) * (v=>b.Stem=v) + σ("ence")  + λ(Check)
                  | RTAB(2) * (v=>b.Stem=v) + σ("er")    + λ(Check)
                  | RTAB(2) * (v=>b.Stem=v) + σ("ic")    + λ(Check)
                  | RTAB(4) * (v=>b.Stem=v) + σ("able")  + λ(Check)
                  | RTAB(4) * (v=>b.Stem=v) + σ("ible")  + λ(Check)
                  | RTAB(3) * (v=>b.Stem=v) + σ("ant")   + λ(Check)
                  | RTAB(5) * (v=>b.Stem=v) + σ("ement") + λ(Check)
                  | RTAB(4) * (v=>b.Stem=v) + σ("ment")  + λ(Check)
                  | RTAB(3) * (v=>b.Stem=v) + σ("ent")   + λ(Check)
                  | RTAB(3) * (v=>b.Stem=v) + σ("ion")   + λ(IonCheck)
                  | RTAB(2) * (v=>b.Stem=v) + σ("ou")    + λ(Check)
                  | RTAB(3) * (v=>b.Stem=v) + σ("ism")   + λ(Check)
                  | RTAB(3) * (v=>b.Stem=v) + σ("ate")   + λ(Check)
                  | RTAB(3) * (v=>b.Stem=v) + σ("iti")   + λ(Check)
                  | RTAB(3) * (v=>b.Stem=v) + σ("ous")   + λ(Check)
                  | RTAB(3) * (v=>b.Stem=v) + σ("ive")   + λ(Check)
                  | RTAB(3) * (v=>b.Stem=v) + σ("ize")   + λ(Check)
                  ) + RPOS(0);
        };

        static Func<Box, PATTERN> MakeP5a() => b => POS(0)
            + RTAB(1) * (v=>b.Stem=v) + σ("e")
            + ( Λ(() => M(b.Stem) > 1)
              | Λ(() => M(b.Stem) == 1) + Λ(() => !CVC(b.Stem, b.Stem.Length-1))
              )
            + λ(()=>b.Target="")
            + RPOS(0);

        static Func<Box, PATTERN> MakeP5b() => b => POS(0)
            + RTAB(2) * (v=>b.Stem=v) + σ("ll")
            + Λ(() => { var s = b.Stem + "ll"; return M(s) > 1; })
            + λ(()=>b.Target="l")
            + RPOS(0);

        // ── Pipeline ──────────────────────────────────────────────────────────

        static readonly Func<Box, PATTERN>[] pipeline = {
            MakeP1a(), MakeP1b(), MakeP1c(),
            MakeP2(),  MakeP3(),  MakeP4(),
            MakeP5a(), MakeP5b()
        };

        public static string PorterStem(string word) {
            if (word.Length <= 2) return word.ToLower();
            string token = word.ToLower();
            foreach (var make in pipeline) {
                var result = TryStep(token, make);
                if (result != null) token = result;
            }
            return token;
        }

        // ── Spot-check tests ──────────────────────────────────────────────────

        [Theory]
        [InlineData("abandoned",   "abandon")]
        [InlineData("abandonment", "abandon")]
        [InlineData("absorbency",  "absorb")]
        [InlineData("absorbent",   "absorb")]
        [InlineData("marketing",   "market")]
        [InlineData("markets",     "market")]
        [InlineData("university",  "univers")]
        [InlineData("universe",    "univers")]
        [InlineData("volume",      "volum")]
        [InlineData("volumes",     "volum")]
        [InlineData("running",     "run")]
        [InlineData("taxing",      "tax")]
        [InlineData("hopeful",     "hope")]
        [InlineData("goodness",    "good")]
        [InlineData("troubled",    "troubl")]
        [InlineData("electrically","electr")]
        [InlineData("aged",        "ag")]
        [InlineData("aweary",      "aweari")]
        [InlineData("baying",      "bai")]
        [InlineData("beds",        "bed")]
        [InlineData("a",           "a")]     // too short to stem
        [InlineData("by",          "by")]    // too short to stem
        public void spot_check(string word, string expected) =>
            Assert.Equal(expected, PorterStem(word));

        // ── Bulk corpus test ──────────────────────────────────────────────────

        [Fact]
        public void corpus_all_23531_words()
        {
            var words    = File.ReadAllLines("porter_voc.txt");
            var expected = File.ReadAllLines("porter_output.txt");
            Assert.Equal(words.Length, expected.Length);

            var failures = new List<string>();
            for (int i = 0; i < words.Length; i++) {
                var got = PorterStem(words[i]);
                if (got != expected[i])
                    failures.Add($"  [{i+1}] \"{words[i]}\" → \"{got}\" (expected \"{expected[i]}\")");
            }

            if (failures.Count > 0) {
                var sample = failures.Take(20);
                Assert.Fail(
                    $"{failures.Count} of {words.Length} stems incorrect " +
                    $"(first {sample.Count()}):\n" +
                    string.Join("\n", sample));
            }
        }

        // ── Corpus stats (always passes) ──────────────────────────────────────

        [Fact]
        public void corpus_stats()
        {
            var words    = File.ReadAllLines("porter_voc.txt");
            var expected = File.ReadAllLines("porter_output.txt");
            int changed  = words.Zip(expected).Count(p => p.First != p.Second);
            Assert.Equal(23531, words.Length);
            Assert.Equal(14402, changed);
        }
    }
}
