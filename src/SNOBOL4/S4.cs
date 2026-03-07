// S4.cs — public factory façade and match engine
//
// S4 is a static class intended to be imported with  using static SNOBOL4.S4;
// Every pattern constructor and configuration function is exposed here so
// call sites read like SNOBOL4 programs:
//
//   var ident = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);
//   Engine.FULLMATCH("Hello", ident);
//
// The dynamic accessor _ makes variable capture concise:
//   SPAN(ALPHA) % (Slot)_.word    — conditional capture into _.word
//   SPAN(ALPHA) * (Slot)_.word    — immediate capture into _.word
//   (string)(Slot)_.word          — read back the value
//
// Engine holds SEARCH / MATCH / FULLMATCH — the three entry points to the
// matching loop.  SEARCH slides the pattern across the subject; MATCH anchors
// at position 0; FULLMATCH anchors at both ends.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
public static class S4
{
    // ── Sequence / alternation / optional ─────────────────────────────────────
    // These factory methods are rarely needed — the + | ~ operators on PATTERN
    // build the same objects.  They exist for cases where you want to name a
    // combinator explicitly or pass it as a value.
    public static PATTERN Σ(params PATTERN[] ap) => new _Σ(ap);
    public static PATTERN Π(params PATTERN[] ap) => new _Π(ap);
    public static PATTERN π(PATTERN p)           => new _π(p);

    // ── Literal string ─────────────────────────────────────────────────────────
    // σ(string)        — match a fixed literal
    // σ(Func<string>)  — evaluate the string at match time
    public static PATTERN σ(string s)       => new _σ(s);
    public static PATTERN σ(Func<string> f) => new _σ(f);

    // ── Positional anchors ─────────────────────────────────────────────────────
    public static PATTERN POS(int n)        => new _POS(n);
    public static PATTERN POS(Func<int> f)  => new _POS(f);
    public static PATTERN RPOS(int n)       => new _RPOS(n);
    public static PATTERN RPOS(Func<int> f) => new _RPOS(f);

    // ── Trivials ───────────────────────────────────────────────────────────────
    public static PATTERN ε()       => new _ε();
    public static PATTERN FAIL()    => new _FAIL();
    public static PATTERN ABORT()   => new _ABORT();
    public static PATTERN SUCCEED() => new _SUCCEED();

    // ── Line anchors ───────────────────────────────────────────────────────────
    public static PATTERN α() => new _α();   // start-of-line
    public static PATTERN ω() => new _ω();   // end-of-line

    // ── FENCE ──────────────────────────────────────────────────────────────────
    // FENCE()   — commit: succeeds once, throws on backtrack
    // FENCE(P)  — succeed on P's matches; suppress external backtracking into P
    public static PATTERN FENCE()           => new _FENCE();
    public static PATTERN FENCE(PATTERN p)  => new _FENCE(p);

    // ── Length and position advance ────────────────────────────────────────────
    public static PATTERN LEN(int n)        => new _LEN(n);
    public static PATTERN LEN(Func<int> f)  => new _LEN(f);
    public static PATTERN TAB(int n)        => new _TAB(n);
    public static PATTERN TAB(Func<int> f)  => new _TAB(f);
    public static PATTERN RTAB(int n)       => new _RTAB(n);
    public static PATTERN RTAB(Func<int> f) => new _RTAB(f);
    public static PATTERN REM()             => new _REM();

    // ── Wildcard ───────────────────────────────────────────────────────────────
    public static PATTERN ARB()             => new _ARB();
    public static PATTERN MARB()            => new _MARB();

    // ── Character-class primitives ─────────────────────────────────────────────
    public static PATTERN ANY(string c)          => new _ANY(c);
    public static PATTERN ANY(Func<string> f)    => new _ANY(f);
    public static PATTERN NOTANY(string c)       => new _NOTANY(c);
    public static PATTERN NOTANY(Func<string> f) => new _NOTANY(f);
    public static PATTERN SPAN(string c)         => new _SPAN(c);
    public static PATTERN SPAN(Func<string> f)   => new _SPAN(f);
    // NSPAN — like SPAN but accepts a zero-length match (never fails)
    public static PATTERN NSPAN(string c)        => new _NSPAN(c);
    public static PATTERN NSPAN(Func<string> f)  => new _NSPAN(f);
    public static PATTERN BREAK(string c)        => new _BREAK(c);
    public static PATTERN BREAK(Func<string> f)  => new _BREAK(f);
    public static PATTERN BREAKX(string c)       => new _BREAKX(c);
    public static PATTERN BREAKX(Func<string> f) => new _BREAKX(f);

    // ── Repetition ─────────────────────────────────────────────────────────────
    public static PATTERN ARBNO(PATTERN p)  => new _ARBNO(p);
    public static PATTERN MARBNO(PATTERN p) => new _MARBNO(p);

    // ── Balanced parentheses ───────────────────────────────────────────────────
    public static PATTERN BAL() => new _BAL();

    // ── Assignment operators ───────────────────────────────────────────────────
    // Δ — immediate: writes on every sub-match, permanent (SNOBOL4 P $ N)
    // δ — conditional: deferred to commit, rolled back on failure (SNOBOL4 P . N)
    public static PATTERN Δ(PATTERN p, string n) => new _Δ(p, n);
    public static PATTERN Δ(PATTERN p, Slot s)   => new _Δ(p, s.Name);
    public static PATTERN δ(PATTERN p, string n) => new _δ(p, n);
    public static PATTERN δ(PATTERN p, Slot s)   => new _δ(p, s.Name);

    // ── Cursor-position capture ────────────────────────────────────────────────
    // Θ — immediate: writes pos now
    // θ — conditional: defers pos write to commit
    public static PATTERN Θ(string n) => new _Θ(n);
    public static PATTERN θ(string n) => new _θ(n);

    // ── Predicate and action ───────────────────────────────────────────────────
    // Λ — immediate predicate: evaluates Func<bool> now, fails if false
    // λ — conditional action: queues Action to run on commit
    public static PATTERN Λ(Func<bool> t) => new _Λ(t);
    public static PATTERN λ(Action c)     => new _λ(c);

    // ── Deferred pattern reference ─────────────────────────────────────────────
    // ζ(string)        — resolves Env[name] as a PATTERN at match time
    // ζ(Func<PATTERN>) — invokes lambda at match time; enables mutual recursion
    //                    between C# variables without needing Env string names
    // ζ(Slot)          — shorthand for ζ(slot.Name)
    public static PATTERN ζ(string n)         => new _ζ_name(n);
    public static PATTERN ζ(Func<PATTERN> f)  => new _ζ_func(f);
    public static PATTERN ζ(Slot s)           => new _ζ_name(s.Name);

    // ── Regex ──────────────────────────────────────────────────────────────────
    // Φ — immediate regex (named groups written to Env on sub-match)
    // φ — conditional regex (named groups deferred to commit)
    public static PATTERN Φ(string rx) => new _Φ(rx);
    public static PATTERN φ(string rx) => new _φ(rx);

    // ── Shift-reduce parser stack ──────────────────────────────────────────────
    // nPush / nInc / nPop — integer child counter (drives Reduce with no explicit n)
    // Shift / Reduce / Pop — parse-tree value stack
    public static PATTERN nPush()                        => new _nPush();
    public static PATTERN nInc()                         => new _nInc();
    public static PATTERN nPop()                         => new _nPop();
    public static PATTERN Shift(string tag)              => new _Shift(tag);
    public static PATTERN Shift(string tag, Func<object> v) => new _Shift(tag, v);
    public static PATTERN Reduce(string tag)             => new _Reduce(tag);
    public static PATTERN Reduce(string tag, int n)      => new _Reduce(tag, n);
    public static PATTERN Pop(string name)               => new _Pop(name);
    public static PATTERN Pop(Slot s)                    => new _Pop(s.Name);

    // ── Environment ────────────────────────────────────────────────────────────
    // GLOBALS — register an external dictionary as the Env store.
    // Only needed when sharing a pre-existing Dictionary<string,object> with
    // other code.  New code should use _ instead.
    public static void GLOBALS(Dictionary<string, object> g) => Env.GLOBALS(g);

    // TRACE — configure sliding-window diagnostic output.
    // Call TRACE() with no arguments to silence it again.
    public static void TRACE(TraceLevel level = TraceLevel.Off, int window = 12,
                             System.IO.TextWriter? output = null)
        => Tracer.TRACE(level, window, output);

    // _ — the dynamic SNOBOL4 environment accessor (thread-local).
    // Property accesses on _ return Slot objects; assignments write into Env.
    //   _.word = "hello"        writes Env["word"]
    //   (Slot)_.word            returns a live Slot reference
    //   (string)(Slot)_.word    reads Env["word"] as string
    [ThreadStatic] static SnobolEnv? __env;
    public static dynamic _ => __env ??= new SnobolEnv();
}

// ── Engine ────────────────────────────────────────────────────────────────────
// The three match entry points.  All return a nullable Slice — the span within
// the subject that the pattern matched — or null on failure.
//
// SEARCH — slides the pattern across the subject, trying each start position
//          from 0 to len in order.  Returns the first match found.
// MATCH  — anchors at position 0  (prepends POS(0))
// FULLMATCH — anchors at both ends  (wraps with POS(0) and RPOS(0))
//
// The exc parameter causes a failed match to throw F("FAIL") instead of
// returning null — useful for mandatory matches where failure is a bug.
//
// Commit protocol:
//   After the first successful yield from P.γ(), Engine fires every Action
//   remaining on state.cstack in registration order.  Conditional patterns
//   (δ, θ, λ, Shift, Reduce, Pop …) push their write Actions before yielding
//   and pop them on backtrack — so only the actions for the successful path
//   are present when commit fires.
public static class Engine
{
    public static Slice? SEARCH(string S, PATTERN P, bool exc = false)
    {
        for (int c = 0; c <= S.Length; c++) {
            var state = new MatchState(c, S);
            Ϣ.Push(state);
            bool popped = false;
            try {
                foreach (var sl in P.γ()) {
                    Ϣ.Pop(); popped = true;
                    // Commit: fire all surviving deferred actions in order
                    foreach (var act in state.cstack) act();
                    return sl;
                }
            }
            catch (F) {
                if (!popped) { Ϣ.Pop(); popped = true; }
                if (exc) throw;
                return null;
            }
            finally {
                if (!popped) Ϣ.Pop();
            }
        }
        if (exc) throw new F("FAIL");
        return null;
    }

    public static Slice? MATCH    (string S, PATTERN P, bool exc = false)
        => SEARCH(S, S4.POS(0) + P, exc);

    public static Slice? FULLMATCH(string S, PATTERN P, bool exc = false)
        => SEARCH(S, S4.POS(0) + P + S4.RPOS(0), exc);
}

}
