// S4.cs -- SNOBOL4 pattern library
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
// ════════════════════════════════════════════════════════════════════════════
// S4 factory — all public constructors
// ════════════════════════════════════════════════════════════════════════════
public static class S4 {
    public static PATTERN Σ(params PATTERN[] ap) => new _Σ(ap);
    public static PATTERN Π(params PATTERN[] ap) => new _Π(ap);
    public static PATTERN π(PATTERN p)           => new _π(p);
    public static PATTERN σ(string s)            => new _σ(s);
    public static PATTERN σ(Func<string> f)      => new _σ(f);
    public static PATTERN POS(int n)             => new _POS(n);
    public static PATTERN POS(Func<int> f)       => new _POS(f);
    public static PATTERN RPOS(int n)            => new _RPOS(n);
    public static PATTERN RPOS(Func<int> f)      => new _RPOS(f);
    public static PATTERN ε()                    => new _ε();
    public static PATTERN FAIL()                 => new _FAIL();
    public static PATTERN ABORT()                => new _ABORT();
    public static PATTERN SUCCEED()              => new _SUCCEED();
    public static PATTERN α()                    => new _α();
    public static PATTERN ω()                    => new _ω();
    public static PATTERN FENCE()                => new _FENCE();
    public static PATTERN FENCE(PATTERN p)       => new _FENCE(p);
    public static PATTERN LEN(int n)             => new _LEN(n);
    public static PATTERN LEN(Func<int> f)       => new _LEN(f);
    public static PATTERN TAB(int n)             => new _TAB(n);
    public static PATTERN TAB(Func<int> f)       => new _TAB(f);
    public static PATTERN RTAB(int n)            => new _RTAB(n);
    public static PATTERN RTAB(Func<int> f)      => new _RTAB(f);
    public static PATTERN REM()                  => new _REM();
    public static PATTERN ARB()                  => new _ARB();
    public static PATTERN MARB()                 => new _MARB();
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
    public static PATTERN ARBNO(PATTERN p)       => new _ARBNO(p);
    public static PATTERN MARBNO(PATTERN p)      => new _MARBNO(p);
    public static PATTERN BAL()                  => new _BAL();
    public static PATTERN δ(PATTERN p, string n) => new _δ(p, n);
    public static PATTERN Δ(PATTERN p, string n) => new _Δ(p, n);
    public static PATTERN Θ(string n)            => new _Θ(n);
    public static PATTERN θ(string n)            => new _θ(n);
    public static PATTERN Λ(Func<bool> t)        => new _Λ(t);
    public static PATTERN λ(Action c)            => new _λ(c);
    // ζ — two overloads matching Python's ζ(string) and ζ(lambda: P)
    public static PATTERN ζ(string n)            => new _ζ_name(n);
    public static PATTERN ζ(Func<PATTERN> f)     => new _ζ_func(f);
    // Φ / φ — regex
    public static PATTERN Φ(string rx)           => new _Φ(rx);
    public static PATTERN φ(string rx)           => new _φ(rx);
    // Stage 8: shift-reduce parser stack
    public static PATTERN nPush()                => new _nPush();
    public static PATTERN nInc()                 => new _nInc();
    public static PATTERN nPop()                 => new _nPop();
    // Shift — two overloads: tag only, or tag + Func<object> for value expr
    public static PATTERN Shift(string tag)                  => new _Shift(tag);
    public static PATTERN Shift(string tag, Func<object> v)  => new _Shift(tag, v);
    // Reduce — default uses istack top; explicit n overload
    public static PATTERN Reduce(string tag)                 => new _Reduce(tag);
    public static PATTERN Reduce(string tag, int n)          => new _Reduce(tag, n);
    // Pop — pops vstack top into Env[name]
    public static PATTERN Pop(string name)                   => new _Pop(name);
    // GLOBALS
    public static void GLOBALS(Dictionary<string, object> g) => Env.GLOBALS(g);
}

// ── Engine ────────────────────────────────────────────────────────────────────
public static class Engine {
    public static Slice? SEARCH(string S, PATTERN P, bool exc = false) {
        for (int c = 0; c <= S.Length; c++) {
            var state = new MatchState(c, S);
            Ϣ.Push(state);
            bool popped = false;
            try {
                foreach (var sl in P.γ()) {
                    Ϣ.Pop(); popped = true;
                    // Fire all cstack actions in order (mirrors Python commit loop)
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
    public static Slice? MATCH    (string S, PATTERN P, bool exc = false) => SEARCH(S, S4.POS(0) + P, exc);
    public static Slice? FULLMATCH(string S, PATTERN P, bool exc = false) => SEARCH(S, S4.POS(0) + P + S4.RPOS(0), exc);
}

// ── Test harness ──────────────────────────────────────────────────────────────
}
