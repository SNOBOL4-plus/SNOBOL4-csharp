// Core.cs — foundation types for the SNOBOL4 pattern engine
//
// Defines the types that every other file depends on:
//
//   F            — exception thrown by ABORT / FENCE to cut all backtracking
//   Env          — flat string→object variable namespace shared by all patterns
//   Slot         — live named reference into Env; returned by the dynamic _ object
//   SnobolEnv    — DynamicObject that makes  _.name = value  and  (string)_.name  work
//   MatchState   — per-attempt mutable state: cursor, stacks, deferred-action queue
//   Ϣ            — thread-local stack of MatchState frames (one per nested SEARCH)
//   Slice        — immutable [start, stop) span within the subject string
//   PATTERN      — abstract base; subclasses implement γ() as IEnumerable<Slice>
//
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace SNOBOL4
{
    // ── F ─────────────────────────────────────────────────────────────────────
    // Thrown by ABORT to terminate the entire match unconditionally, and by
    // FENCE() (the zero-argument form) to cut backtracking once a match point
    // has been committed.  Caught by Engine.SEARCH; re-thrown when exc=true.
    public sealed class F : Exception { public F(string m) : base(m) {} }

    // ── Env ───────────────────────────────────────────────────────────────────
    // The single flat variable namespace for a SNOBOL4 session.
    //
    // All pattern assignment operators (Δ, δ, Θ, θ) write here.
    // ζ reads PATTERN objects from here by name for forward references.
    // GLOBALS(dict) lets callers supply their own dictionary; useful when
    // existing code already holds a Dictionary<string,object> it wants to share.
    // The default pre-wired dictionary means GLOBALS() is optional for new code
    // that uses the _ dynamic accessor instead.
    public static class Env
    {
        internal static Dictionary<string,object> _g = new();
        public static void GLOBALS(Dictionary<string,object> g) { _g = g; }
        public static Dictionary<string,object> G  => _g;
        public static void   Set(string k, object v) => _g[k] = v;
        public static object Get(string k) => _g.TryGetValue(k, out var v) ? v : throw new KeyNotFoundException(k);
        public static bool   Has(string k) => _g.ContainsKey(k);
        // Str / Int — safe reads that return SNOBOL4 NULL ("" / 0) when a key
        // is absent, matching SNOBOL4's semantics for uninitialized variables.
        public static string Str(string k) => _g.TryGetValue(k, out var v) ? v?.ToString() ?? "" : "";
        public static int    Int(string k) => _g.TryGetValue(k, out var v) ? Convert.ToInt32(v) : 0;
    }

    // ── Slot ──────────────────────────────────────────────────────────────────
    // A Slot is a live, named reference into Env._g.  It is what _.foo returns
    // via SnobolEnv.TryGetMember.  Rather than reading the value eagerly, a Slot
    // holds only the key string and reads Env on demand through implicit
    // conversions.  This makes the following idioms natural at the call site:
    //
    //   SPAN(ALPHA) % (Slot)_.word      — conditional capture into _.word
    //   SPAN(ALPHA) * (Slot)_.word      — immediate capture into _.word
    //   string s = (string)(Slot)_.word — read current value
    //   int    n = (int)(Slot)_.count   — read as int (0 if unset)
    //   ζ((Slot)_.pat)                  — resolve PATTERN stored in _.pat
    //
    // Length is a convenience for Λ guards that check a captured string's size:
    //   Λ(() => ((Slot)_.hex).Length == 4)
    public sealed class Slot
    {
        public readonly string Name;
        public Slot(string name) { Name = name; }

        public object? Value  => Env._g.TryGetValue(Name, out var v) ? v : null;
        public int     Length => Env.Str(Name).Length;

        public static implicit operator string(Slot s)  => Env.Str(s.Name);
        public static implicit operator int(Slot s)     => Env.Int(s.Name);
        public static implicit operator bool(Slot s)    =>
            Env._g.TryGetValue(s.Name, out var v) && Convert.ToBoolean(v);
        public static implicit operator PATTERN(Slot s) =>
            Env._g.TryGetValue(s.Name, out var v) && v is PATTERN p
                ? p : throw new InvalidCastException($"Slot '{s.Name}' is not a PATTERN");

        public override string ToString() => Env.Str(Name);
    }

    // ── SnobolEnv ─────────────────────────────────────────────────────────────
    // A DynamicObject that turns property access syntax into Env reads/writes:
    //
    //   _.word = "hello"       TrySetMember → Env._g["word"] = "hello"
    //   (Slot)_.word           TryGetMember → new Slot("word")
    //   (string)(Slot)_.word   → Slot implicit → Env.Str("word")
    //
    // Setting a property to null removes the key (SNOBOL4 undefine semantics).
    // S4._ is thread-local, so each thread gets its own SnobolEnv instance.
    public sealed class SnobolEnv : DynamicObject
    {
        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = new Slot(binder.Name);
            return true;
        }
        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            if (value == null) Env._g.Remove(binder.Name);
            else               Env._g[binder.Name] = value;
            return true;
        }
    }

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
    // iterating γ() and pops it in a finally block.  Nested matches (a ζ
    // pattern resolving to a PATTERN that calls SEARCH again) stack additional
    // frames on top.
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
    //   P + Q    concatenation  (SNOBOL4 juxtaposition P Q)
    //   P | Q    alternation    (SNOBOL4 |)
    //   ~P       optional       (SNOBOL4 P | "")
    //   P % "n"  conditional capture into Env["n"]   (SNOBOL4 P . N)
    //   P % slot conditional capture via Slot
    //   P * slot immediate capture via Slot           (SNOBOL4 P $ N)
    //
    // % and * bind tighter than + so  SPAN(A) + ANY(D) % _.x  parses as
    // SPAN(A) + (ANY(D) % _.x),  matching SNOBOL4's operator precedence.
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

        // % string / % Slot — conditional capture (deferred to commit)
        public static PATTERN operator%(PATTERN p, string n) => new _δ(p, n);
        public static PATTERN operator%(PATTERN p, Slot s)   => new _δ(p, s.Name);
        // * Slot — immediate capture (fires as sub-pattern matches)
        public static PATTERN operator*(PATTERN p, Slot s)   => new _Δ(p, s.Name);
    }
}
