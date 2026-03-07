// BeadTests.cs — C# port of test_bead_stage1.py
//
// Original Python pattern:
//   Σ( POS(0), Π(B,F,L,R), Π(E,EA), Π(D,DS), RPOS(0) )
//
// Built here as:
//   new Pos(0) + (B|F|L|R) + (E|EA) + (D|DS) + new RPos(0)
//
// The + operator builds Concat, | builds Alt — exactly like Python __add__ / __or__
//
using System;
using SNOBOL4;

class BeadTests
{
    static int pass_count = 0;
    static int fail_count = 0;

    static void Check(string label, Slice? got, bool shouldMatch, string subject)
    {
        bool matched  = got.HasValue;
        bool fullspan = got.HasValue && got.Value.Start == 0 && got.Value.End == subject.Length;
        bool ok       = shouldMatch ? fullspan : !matched;
        string mark   = ok ? "✓" : "✗";
        Console.WriteLine($"  {mark}  {label}");
        if (!ok)
        {
            Console.WriteLine($"       got      {(got.HasValue ? got.Value.ToString() : "null")}");
            Console.WriteLine($"       expected {(shouldMatch ? $"[0:{subject.Length}]" : "null")}");
            fail_count++;
        }
        else pass_count++;
    }

    static Pattern BuildBead()
    {
        // ── First component: B | F | L | R ───────────────────────────────────
        Pattern B   = new Str("B");
        Pattern F   = new Str("F");
        Pattern L   = new Str("L");
        Pattern R   = new Str("R");
        Pattern BFR = B | F | L | R;

        // ── Second component: E | EA ──────────────────────────────────────────
        // Left-to-right alternation: E is tried first. When E matches but the
        // overall pattern fails (e.g. "BEAD" needs EA so D lands at RPOS(0)),
        // the backtracking engine will exhaust E and then try EA. Correct.
        Pattern E   = new Str("E");
        Pattern EA  = new Str("EA");
        Pattern EEA = E | EA;

        // ── Third component: D | DS ───────────────────────────────────────────
        Pattern D   = new Str("D");
        Pattern DS  = new Str("DS");
        Pattern DDS = D | DS;

        // ── Full pattern: POS(0) + BFR + EEA + DDS + RPOS(0) ─────────────────
        return new Pos(0) + BFR + EEA + DDS + new RPos(0);
    }

    static void Main()
    {
        Pattern bead = BuildBead();

        Console.WriteLine("\n── BEAD: POS(0)+(B|F|L|R)+(E|EA)+(D|DS)+RPOS(0) ──\n");

        // ── Should MATCH (full span: start=0, end=subject.Length) ────────────
        string[] shouldMatch = {
            "BED",   "FED",   "LED",   "RED",
            "BEAD",  "FEAD",  "LEAD",  "READ",
            "BEDS",  "FEDS",  "LEDS",  "REDS",
            "BEADS", "FEADS", "LEADS", "READS"
        };

        foreach (string s in shouldMatch)
            Check($"{s,-8} matches", Engine.Search(s, bead), true, s);

        Console.WriteLine();

        // ── Should NOT match ──────────────────────────────────────────────────
        string[] shouldFail = { "CAT", "BE", "BREAD", "XED" };

        foreach (string s in shouldFail)
            Check($"{s,-8} no match", Engine.Search(s, bead), false, s);

        Console.WriteLine($"\n── Results: {pass_count} passed, {fail_count} failed ──\n");
        Environment.Exit(fail_count > 0 ? 1 : 0);
    }
}
