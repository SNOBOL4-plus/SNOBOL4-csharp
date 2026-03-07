// Core.cs — foundation types for the SNOBOL4 pattern engine
//
// Defines the types that every other file depends on:
//
//   F          — exception thrown by ABORT / FENCE to cut all backtracking
//   MatchState — per-attempt mutable state: cursor, stacks, deferred-action queue
//   Ϣ          — thread-local stack of MatchState frames (one per nested SEARCH)
//   Slice      — immutable [start, stop) span within the subject string
//   PATTERN    — abstract base; subclasses implement γ() as IEnumerable<Slice>
//
// Assignment targets are plain C# delegates — no global environment or string
// names.  Use captured local variables and setter lambdas:
//
//   string word = "";
//   SPAN(ALPHA) % (v => word = v)    — conditional capture, fires on commit
//   SPAN(ALPHA) * (v => word = v)    — immediate capture, fires on sub-match
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
    // ── F ─────────────────────────────────────────────────────────────────────
    // Thrown by ABORT to terminate the entire match unconditionally, and by
    // FENCE() (the zero-argument form) to cut backtracking once a match point
    // has been committed.  Caught by Engine.SEARCH; re-thrown when exc=true.
    public sealed class F : Exception { public F(string m) : base(m) {} }

    // ── MatchState ────────────────────────────────────────────────────────────
    // Holds all mutable state for one match attempt.  A new MatchState is pushed
    // onto Ϣ at the start of each SEARCH iteration and popped when done.
    //
    //   pos     — current cursor position within subject
    //   subject — the string being matched (immutable during a match)
    //   cstack  — deferred-action queue.  Conditional patterns (δ, θ, λ, nPush,
    //             Shift, Reduce, Pop) push Actions here before yielding, and pop
    //             them on backtrack.  Engine.SEARCH fires all remaining actions
    //             in order after the first successful yield — this is the
    //             "commit" step that makes conditional assignment work.
    //   istack  — integer counter stack for nPush/nInc/nPop.  Tracks how many
    //             children have been shifted since the last nPush, so Reduce
    //             knows how many items to pop from vstack.
    //   itop    — index of the current top of istack (-1 when empty)
    //   vstack  — parse-tree value stack for Shift/Reduce/Pop
    //   depth   — nesting depth, incremented by Σ/Π/ARBNO for trace indentation
    public sealed class MatchState
    {
        public int pos;
        public readonly string             subject;
        public readonly List<Action>       cstack = new();
        public readonly List<int>          istack = new();
        public int                         itop   = -1;
        public readonly List<List<object>> vstack = new();
        public int                         depth  = 0;
        public MatchState(int p, string s) { pos = p; subject = s; }
    }

    // ── Ϣ ─────────────────────────────────────────────────────────────────────
    // The match state stack.  Engine.SEARCH pushes a fresh MatchState before
    // iterating γ() and pops it in a finally block.  Nested matches stack
    // additional frames on top.
    //
    // [ThreadStatic] makes the backing store thread-local so concurrent matches
    // on different threads never interfere — no locking required.
    public static class Ϣ
    {
        [ThreadStatic] static Stack<MatchState>? _ts;
        static Stack<MatchState> _s => _ts ??= new Stack<MatchState>();
        public static void       Push(MatchState s) => _s.Push(s);
        public static void       Pop()              => _s.Pop();
        public static MatchState Top               => _s.Peek();
    }

    // ── Slice ─────────────────────────────────────────────────────────────────
    // An immutable half-open span [Start, Stop) into the subject string.
    // γ() methods yield Slices to report where they matched.  The cursor
    // advances to Stop before the yield; the Slice records where it started
    // so the pattern can reset pos on backtrack.
    public readonly struct Slice
    {
        public readonly int Start, Stop;
        public Slice(int s, int e) { Start = s; Stop = e; }
        public int    Length             => Stop - Start;
        public string Of(string subject) => subject.Substring(Start, Length);
        public override string ToString()=> $"[{Start}:{Stop}]";
    }

    // ── PATTERN ───────────────────────────────────────────────────────────────
    // Abstract base for all patterns.  The single abstract method γ() returns
    // an IEnumerable<Slice> — a lazy generator that produces one Slice per
    // alternative the pattern can match at the current cursor position.
    //
    // Backtracking is driven by the generator protocol: _Σ calls MoveNext() on
    // each sub-pattern's enumerator in sequence.  When a later pattern fails,
    // _Σ calls MoveNext() again on the earlier enumerator to get its next
    // alternative.  When an enumerator is exhausted the pattern has no more
    // alternatives at this position.
    //
    // Operators and their SNOBOL4 equivalents:
    //   P + Q               concatenation         (SNOBOL4 juxtaposition P Q)
    //   P | Q               alternation           (SNOBOL4 |)
    //   ~P                  optional              (SNOBOL4 P | "")
    //   P % (v => x = v)    conditional capture   (SNOBOL4 P . N)
    //   P * (v => x = v)    immediate capture     (SNOBOL4 P $ N)
    //
    // % and * bind tighter than + so  SPAN(A) + ANY(D) % (v => x = v)  parses
    // as  SPAN(A) + (ANY(D) % (v => x = v)),  matching SNOBOL4 precedence.
    public abstract class PATTERN
    {
        public abstract IEnumerable<Slice> γ();

        // + builds a flat _Σ by absorbing an existing left-hand _Σ
        public static PATTERN operator+(PATTERN p, PATTERN q)
        {
            if (p is _Σ ps) { var a = new PATTERN[ps._AP.Length+1]; ps._AP.CopyTo(a,0); a[ps._AP.Length] = q; return new _Σ(a); }
            return new _Σ(p, q);
        }

        // | builds a flat _Π by absorbing an existing left-hand _Π
        public static PATTERN operator|(PATTERN p, PATTERN q)
        {
            if (p is _Π pp) { var a = new PATTERN[pp._AP.Length+1]; pp._AP.CopyTo(a,0); a[pp._AP.Length] = q; return new _Π(a); }
            return new _Π(p, q);
        }

        public static PATTERN operator~(PATTERN p) => new _π(p);

        // % Action<string> — conditional capture (deferred to commit)
        public static PATTERN operator%(PATTERN p, Action<string> set) => new _δ(p, set);

        // * Action<string> — immediate capture (fires as sub-pattern matches)
        public static PATTERN operator*(PATTERN p, Action<string> set) => new _Δ(p, set);
    }
}
