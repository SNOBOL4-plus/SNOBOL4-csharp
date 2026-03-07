// S4.cs — public factory façade and match engine
//
// S4 is a static class intended to be imported with  using static SNOBOL4.S4;
// Every pattern constructor and configuration function is exposed here so
// call sites read like SNOBOL4 programs:
//
//   var ident = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);
//   Engine.FULLMATCH("Hello", ident);
//
// Capture uses closed-over local variables and setter delegates:
//   string word = "";
//   SPAN(ALPHA) % (v => word = v)    — conditional (fires on commit)
//   SPAN(ALPHA) * (v => word = v)    — immediate   (fires on sub-match)
//
// Engine holds SEARCH / MATCH / FULLMATCH — the three entry points.
// SEARCH slides the pattern across the subject; MATCH anchors at position 0;
// FULLMATCH anchors at both ends.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
public static class S4
{
    // ── Sequence / alternation / optional ─────────────────────────────────────
    public static PATTERN Σ(params PATTERN[] ap) => new _Σ(ap);
    public static PATTERN Π(params PATTERN[] ap) => new _Π(ap);
    public static PATTERN π(PATTERN p)           => new _π(p);

    // ── Literal string ─────────────────────────────────────────────────────────
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
    public static PATTERN α() => new _α();
    public static PATTERN ω() => new _ω();

    // ── FENCE ──────────────────────────────────────────────────────────────────
    // FENCE()   — commit: succeeds once, throws on backtrack
    // FENCE(P)  — succeed on P's matches; suppress external backtracking into P
    public static PATTERN FENCE()          => new _FENCE();
    public static PATTERN FENCE(PATTERN p) => new _FENCE(p);

    // ── Length and position advance ────────────────────────────────────────────
    public static PATTERN LEN(int n)        => new _LEN(n);
    public static PATTERN LEN(Func<int> f)  => new _LEN(f);
    public static PATTERN TAB(int n)        => new _TAB(n);
    public static PATTERN TAB(Func<int> f)  => new _TAB(f);
    public static PATTERN RTAB(int n)       => new _RTAB(n);
    public static PATTERN RTAB(Func<int> f) => new _RTAB(f);
    public static PATTERN REM()             => new _REM();

    // ── Wildcard ───────────────────────────────────────────────────────────────
    public static PATTERN ARB()  => new _ARB();
    public static PATTERN MARB() => new _MARB();

    // ── Character-class primitives ─────────────────────────────────────────────
    public static PATTERN ANY(string c)          => new _ANY(c);
    public static PATTERN ANY(Func<string> f)    => new _ANY(f);
    public static PATTERN NOTANY(string c)       => new _NOTANY(c);
    public static PATTERN NOTANY(Func<string> f) => new _NOTANY(f);
    public static PATTERN SPAN(string c)         => new _SPAN(c);
    public static PATTERN SPAN(Func<string> f)   => new _SPAN(f);
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
    // Δ — immediate: calls set on every sub-match, permanent  (SNOBOL4 P $ N)
    // δ — conditional: deferred to commit, rolled back on failure  (SNOBOL4 P . N)
    // These factory methods are the named-function form; the operator form
    // P % setter  and  P * setter  is preferred at call sites.
    public static PATTERN Δ(PATTERN p, Action<string> set) => new _Δ(p, set);
    public static PATTERN δ(PATTERN p, Action<string> set) => new _δ(p, set);

    // ── Cursor-position capture ────────────────────────────────────────────────
    // Θ — immediate: calls set(pos) now
    // θ — conditional: defers pos write to commit
    public static PATTERN Θ(Action<int> set) => new _Θ(set);
    public static PATTERN θ(Action<int> set) => new _θ(set);

    // ── Predicate and action ───────────────────────────────────────────────────
    // Λ — immediate predicate: evaluates Func<bool> now, fails if false
    // λ — conditional action: queues Action to run on commit
    public static PATTERN Λ(Func<bool> t) => new _Λ(t);
    public static PATTERN λ(Action c)     => new _λ(c);

    // ── Deferred pattern reference ─────────────────────────────────────────────
    // ζ(Func<PATTERN>) — invokes lambda at match time; enables mutual recursion
    //                    between C# local variables without string indirection:
    //   PATTERN? expr = null;
    //   var term = σ("(") + ζ(() => expr!) + σ(")");
    //   expr = term | SPAN(ALPHA);
    public static PATTERN ζ(Func<PATTERN> f) => new _ζ(f);

    // ── Regex ──────────────────────────────────────────────────────────────────
    // Φ — immediate regex (named groups written immediately on sub-match)
    // φ — conditional regex (named groups deferred to commit)
    // onCapture(name, value) is called for each named group that succeeded.
    public static PATTERN Φ(string rx, Action<string,string> onCapture) => new _Φ(rx, onCapture);
    public static PATTERN φ(string rx, Action<string,string> onCapture) => new _φ(rx, onCapture);

    // ── Shift-reduce parser stack ──────────────────────────────────────────────
    // nPush / nInc / nPop — integer child counter (drives Reduce with no explicit n)
    // Shift / Reduce / Pop — parse-tree value stack
    public static PATTERN nPush()                         => new _nPush();
    public static PATTERN nInc()                          => new _nInc();
    public static PATTERN nPop()                          => new _nPop();
    public static PATTERN Shift(string tag)               => new _Shift(tag);
    public static PATTERN Shift(string tag, Func<object> v) => new _Shift(tag, v);
    public static PATTERN Reduce(string tag)              => new _Reduce(tag);
    public static PATTERN Reduce(string tag, int n)       => new _Reduce(tag, n);
    public static PATTERN Pop(Action<List<object>> set)   => new _Pop(set);

    // ── Trace ──────────────────────────────────────────────────────────────────
    // TRACE() with no arguments silences output.
    public static void TRACE(TraceLevel level = TraceLevel.Off, int window = 12,
                             System.IO.TextWriter? output = null)
        => Tracer.TRACE(level, window, output);
}

// ── Engine ────────────────────────────────────────────────────────────────────
// The three match entry points.  All return a nullable Slice — the span within
// the subject that the pattern matched — or null on failure.
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
