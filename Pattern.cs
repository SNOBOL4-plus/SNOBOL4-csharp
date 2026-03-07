// Pattern.cs — SNOBOL4 pattern engine core
// Mirrors _backend_pure.py: each Pattern subclass implements
// γ(MatchState) as an IEnumerable<Slice> generator.
//
// Slice   = (int Start, int End)  ← equivalent to Python slice(start, stop)
// MatchState holds pos + subject  ← equivalent to Python's SNOBOL class
//
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
    // ── Slice: equivalent to Python slice(start, stop) ───────────────────────
    public readonly struct Slice
    {
        public readonly int Start;
        public readonly int End;
        public Slice(int start, int end) { Start = start; End = end; }
        public int Length => End - Start;
        public override string ToString() => $"[{Start}:{End}]";
    }

    // ── MatchState: equivalent to Python SNOBOL class ────────────────────────
    public class MatchState
    {
        public string Subject;
        public int    Pos;

        public MatchState(string subject, int pos = 0)
        {
            Subject = subject;
            Pos     = pos;
        }
    }

    // ── Pattern: abstract base, equivalent to Python PATTERN class ────────────
    public abstract class Pattern
    {
        // γ is the generator method — yields Slices of successful matches,
        // restores Pos on backtrack, exactly as in Python.
        public abstract IEnumerable<Slice> γ(MatchState s);

        // Operator + → Concat (Σ)  ← Python __add__
        public static Pattern operator +(Pattern left, Pattern right)
            => new Concat(left, right);

        // Operator | → Alt (Π)  ← Python __or__
        public static Pattern operator |(Pattern left, Pattern right)
            => new Alt(left, right);
    }

    // ── σ: literal string match ───────────────────────────────────────────────
    // Equivalent to Python class σ(PATTERN)
    public class Str : Pattern
    {
        readonly string _s;
        public Str(string s) { _s = s; }

        public override IEnumerable<Slice> γ(MatchState s)
        {
            int pos0 = s.Pos;
            if (pos0 + _s.Length <= s.Subject.Length &&
                s.Subject.Substring(pos0, _s.Length) == _s)
            {
                s.Pos += _s.Length;
                yield return new Slice(pos0, s.Pos);
                s.Pos = pos0;               // restore on backtrack
            }
        }

        public override string ToString() => $"Str({_s!r})".Replace("!r", $"\"{_s}\"");
    }

    // ── POS(n): match only at absolute cursor position n ─────────────────────
    // Equivalent to Python class POS(PATTERN)
    public class Pos : Pattern
    {
        readonly int _n;
        public Pos(int n) { _n = n; }

        public override IEnumerable<Slice> γ(MatchState s)
        {
            if (s.Pos == _n)
                yield return new Slice(s.Pos, s.Pos);
        }
    }

    // ── RPOS(n): match only when n chars remain ───────────────────────────────
    // Equivalent to Python class RPOS(PATTERN)
    public class RPos : Pattern
    {
        readonly int _n;
        public RPos(int n) { _n = n; }

        public override IEnumerable<Slice> γ(MatchState s)
        {
            if (s.Pos == s.Subject.Length - _n)
                yield return new Slice(s.Pos, s.Pos);
        }
    }

    // ── Alt (Π): try left, then right — alternation ───────────────────────────
    // Equivalent to Python class Π(PATTERN)
    // yield from P.γ() then yield from Q.γ() — direct translation
    public class Alt : Pattern
    {
        readonly Pattern _left;
        readonly Pattern _right;
        public Alt(Pattern left, Pattern right) { _left = left; _right = right; }

        public override IEnumerable<Slice> γ(MatchState s)
        {
            foreach (var slice in _left.γ(s))
                yield return slice;
            foreach (var slice in _right.γ(s))
                yield return slice;
        }
    }

    // ── Concat (Σ): sequential — left then right, with backtracking ───────────
    // Equivalent to Python class Σ(PATTERN)
    // Uses the same cursor-and-generator-array approach as Python Σ.γ()
    // but for two patterns only (we'll expand to N later).
    public class Concat : Pattern
    {
        readonly Pattern _left;
        readonly Pattern _right;
        public Concat(Pattern left, Pattern right) { _left = left; _right = right; }

        public override IEnumerable<Slice> γ(MatchState s)
        {
            int pos0 = s.Pos;

            // For each position left succeeds at, try right from there.
            // If right fails, we fall back to the next left alternative.
            // This is exactly Python's Σ.γ() backtracking loop.
            foreach (var leftSlice in _left.γ(s))
            {
                // s.Pos was advanced by left — now try right from here
                foreach (var rightSlice in _right.γ(s))
                {
                    yield return new Slice(pos0, s.Pos);
                    // s.Pos restored by right's own backtrack after yield
                }
                // right exhausted — s.Pos restored by right; left will
                // try its next alternative (or restore itself)
            }
        }
    }

    // ── SEARCH: outer cursor loop — equivalent to Python SEARCH() ────────────
    public static class Engine
    {
        /// <summary>
        /// Anchored search: tries pattern at every cursor position 0..len(S).
        /// Returns the first successful Slice, or null on failure.
        /// Equivalent to Python SEARCH(S, P).
        /// </summary>
        public static Slice? Search(string subject, Pattern pattern)
        {
            for (int cursor = 0; cursor <= subject.Length; cursor++)
            {
                var state = new MatchState(subject, cursor);
                foreach (var slice in pattern.γ(state))
                    return slice;           // first success wins
            }
            return null;
        }

        /// <summary>
        /// Anchored full-match: equivalent to Python FULLMATCH(S, P)
        /// which is SEARCH(S, POS(0) + P + RPOS(0))
        /// </summary>
        public static Slice? FullMatch(string subject, Pattern pattern)
        {
            var fullPattern = new Pos(0) + pattern + new RPos(0);
            return Search(subject, fullPattern);
        }
    }
}
