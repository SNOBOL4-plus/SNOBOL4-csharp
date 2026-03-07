// SNOBOL4cs_v7.cs  —  SNOBOL4 pattern engine in C#, Version 7
//
// Stage 6 — Assignment and predicate patterns + Env singleton:
//
//   Env          Static singleton holding Dictionary<string,object> — the single
//                flat SNOBOL variable space.  Holds captured strings, cursor
//                positions, AND pattern objects (needed by ζ for forward refs).
//                GLOBALS(dict) registers it.
//
//   δ(P, name)   Immediate match assignment  (SNOBOL4: P $ N)
//                Writes env[name] = matched substring on every yield from P.
//                Permanent — not rolled back on backtrack.
//
//   Δ(P, name)   Conditional match assignment  (SNOBOL4: P . N)
//   P % "name"   Same via operator.
//                Defers write until whole match succeeds; rolled back otherwise.
//
//   Θ(name)      Immediate cursor assignment  — writes cursor position now.
//   θ(name)      Conditional cursor assignment — defers until success.
//
//   Λ(func)      Immediate predicate — Func<bool>, yields iff true.
//   λ(action)    Conditional action  — Action, fires after whole match succeeds.
//
//   ζ(name)      Deferred pattern reference — looks up env[name] as a PATTERN
//                at match time.  Enables forward references and recursive grammars.
//
// MatchState gains List<Action> cstack.
// Engine.SEARCH executes cstack actions after first successful yield.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using static S4;

// ── F ─────────────────────────────────────────────────────────────────────────
sealed class F : Exception { public F(string m) : base(m) {} }

// ── Env — the single flat SNOBOL variable space ───────────────────────────────
//
// Holds everything the patterns and caller share:
//   • Captured string values written by δ / Δ
//   • Cursor positions written by Θ / θ
//   • PATTERN objects stored by name for ζ forward references
//
// Must be registered before matching via GLOBALS(dict).
// Using a Dictionary<string,object> mirrors Python's module globals() exactly.

static class Env
{
    static Dictionary<string,object>? _g;

    public static void GLOBALS(Dictionary<string,object> g) => _g = g;

    public static Dictionary<string,object> G =>
        _g ?? throw new InvalidOperationException(
            "GLOBALS(dict) has not been called before matching.");

    public static void   Set(string k, object v)  => G[k] = v;
    public static object Get(string k)            => G.TryGetValue(k, out var v) ? v
        : throw new KeyNotFoundException($"SNOBOL env: '{k}' not found");
    public static bool   Has(string k)            => _g != null && _g.ContainsKey(k);
}

// ── MatchState — carries cstack for deferred conditional actions ──────────────
sealed class MatchState
{
    public int          pos;
    public string       subject;
    public List<Action> cstack = new();
    public MatchState(int p, string s) { pos=p; subject=s; }
}

// ── Ϣ — match-state stack ────────────────────────────────────────────────────
static class Ϣ
{
    static readonly Stack<MatchState> _s = new();
    public static void       Push(MatchState s) => _s.Push(s);
    public static void       Pop()              => _s.Pop();
    public static MatchState Top                => _s.Peek();
}

// ── Slice ─────────────────────────────────────────────────────────────────────
readonly struct Slice
{
    public readonly int Start, Stop;
    public Slice(int s, int e) { Start=s; Stop=e; }
    public override string ToString() => $"[{Start}:{Stop}]";
}

// ── PATTERN — base class ──────────────────────────────────────────────────────
abstract class PATTERN
{
    public abstract IEnumerable<Slice> γ();

    public static PATTERN operator +(PATTERN p, PATTERN q) {
        if (p is _Σ ps) { var a=new PATTERN[ps._AP.Length+1]; ps._AP.CopyTo(a,0); a[ps._AP.Length]=q; return new _Σ(a); }
        return new _Σ(p,q);
    }
    public static PATTERN operator |(PATTERN p, PATTERN q) {
        if (p is _Π pp) { var a=new PATTERN[pp._AP.Length+1]; pp._AP.CopyTo(a,0); a[pp._AP.Length]=q; return new _Π(a); }
        return new _Π(p,q);
    }
    public static PATTERN operator ~(PATTERN p) => new _π(p);

    // P % "name"  →  Δ(P, "name")   conditional assignment operator
    public static PATTERN operator %(PATTERN p, string name) => new _Δ(p, name);
}

// ═════════════════════════════════════════════════════════════════════════════
// V6 carry-forward (condensed)
// ═════════════════════════════════════════════════════════════════════════════

sealed class _σ : PATTERN {
    readonly string _s; public _σ(string s){_s=s;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int p=st.pos;
        if (p+_s.Length<=st.subject.Length && string.CompareOrdinal(st.subject,p,_s,0,_s.Length)==0)
            { st.pos=p+_s.Length; yield return new Slice(p,st.pos); st.pos=p; }
    }
}
sealed class _Σ : PATTERN {
    internal readonly PATTERN[] _AP; public _Σ(params PATTERN[] ap){_AP=ap;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int p0=st.pos; int n=_AP.Length;
        var Ag=new IEnumerator<Slice>?[n]; int c=0;
        while (c>=0) {
            if (c>=n) { yield return new Slice(p0,st.pos); c--; continue; }
            if (Ag[c]==null) Ag[c]=_AP[c].γ().GetEnumerator();
            if (Ag[c]!.MoveNext()) c++;
            else { Ag[c]!.Dispose(); Ag[c]=null; c--; }
        }
        foreach (var e in Ag) e?.Dispose();
    }
}
sealed class _Π : PATTERN {
    internal readonly PATTERN[] _AP; public _Π(params PATTERN[] ap){_AP=ap;}
    public override IEnumerable<Slice> γ() {
        foreach (var P in _AP) foreach (var s in P.γ()) yield return s;
    }
}
sealed class _POS  : PATTERN { readonly int _n; public _POS(int n){_n=n;}
    public override IEnumerable<Slice> γ()
    { var s=Ϣ.Top; if(s.pos==_n) yield return new Slice(s.pos,s.pos); } }
sealed class _RPOS : PATTERN { readonly int _n; public _RPOS(int n){_n=n;}
    public override IEnumerable<Slice> γ()
    { var s=Ϣ.Top; if(s.pos==s.subject.Length-_n) yield return new Slice(s.pos,s.pos); } }
sealed class _ε : PATTERN {
    public override IEnumerable<Slice> γ()
    { var s=Ϣ.Top; yield return new Slice(s.pos,s.pos); } }
sealed class _FAIL    : PATTERN { public override IEnumerable<Slice> γ() { yield break; } }
sealed class _ABORT   : PATTERN {
    public override IEnumerable<Slice> γ() {
        throw new F("ABORT");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
sealed class _SUCCEED : PATTERN {
    public override IEnumerable<Slice> γ()
    { var s=Ϣ.Top; while(true) yield return new Slice(s.pos,s.pos); } }
sealed class _π : PATTERN {
    readonly PATTERN _P; public _π(PATTERN p){_P=p;}
    public override IEnumerable<Slice> γ() {
        foreach (var s in _P.γ()) yield return s;
        var st=Ϣ.Top; yield return new Slice(st.pos,st.pos);
    }
}
sealed class _α : PATTERN {
    public override IEnumerable<Slice> γ() {
        var s=Ϣ.Top;
        if (s.pos==0||(s.pos>0&&s.subject[s.pos-1]=='\n'))
            yield return new Slice(s.pos,s.pos);
    }
}
sealed class _ω : PATTERN {
    public override IEnumerable<Slice> γ() {
        var s=Ϣ.Top;
        if (s.pos==s.subject.Length||(s.pos<s.subject.Length&&s.subject[s.pos]=='\n'))
            yield return new Slice(s.pos,s.pos);
    }
}
sealed class _FENCE : PATTERN {
    readonly PATTERN? _P; public _FENCE(){_P=null;} public _FENCE(PATTERN p){_P=p;}
    public override IEnumerable<Slice> γ() {
        if (_P==null) { var s=Ϣ.Top; yield return new Slice(s.pos,s.pos); throw new F("FENCE"); }
        else { foreach (var s in _P.γ()) yield return s; }
    }
}
sealed class _LEN : PATTERN {
    readonly int _n; public _LEN(int n){_n=n;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top;
        if (st.pos+_n<=st.subject.Length)
            { int p=st.pos; st.pos+=_n; yield return new Slice(p,st.pos); st.pos=p; }
    }
}
sealed class _TAB : PATTERN {
    readonly int _n; public _TAB(int n){_n=n;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top;
        if (_n<=st.subject.Length&&_n>=st.pos)
            { int p=st.pos; st.pos=_n; yield return new Slice(p,_n); st.pos=p; }
    }
}
sealed class _RTAB : PATTERN {
    readonly int _n; public _RTAB(int n){_n=n;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int abs=st.subject.Length-_n;
        if (_n<=st.subject.Length&&abs>=st.pos)
            { int p=st.pos; st.pos=abs; yield return new Slice(p,abs); st.pos=p; }
    }
}
sealed class _REM : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int p=st.pos;
        st.pos=st.subject.Length; yield return new Slice(p,st.pos); st.pos=p;
    }
}
sealed class _ARB : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int p=st.pos;
        while (st.pos<=st.subject.Length) { yield return new Slice(p,st.pos); st.pos++; }
        st.pos=p;
    }
}
sealed class _MARB : PATTERN {
    readonly _ARB _a=new(); public override IEnumerable<Slice> γ() => _a.γ(); }
sealed class _ANY : PATTERN {
    readonly string _c; public _ANY(string c){_c=c;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top;
        if (st.pos<st.subject.Length&&_c.IndexOf(st.subject[st.pos])>=0)
            { int p=st.pos; st.pos++; yield return new Slice(p,st.pos); st.pos=p; }
    }
}
sealed class _NOTANY : PATTERN {
    readonly string _c; public _NOTANY(string c){_c=c;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top;
        if (st.pos<st.subject.Length&&_c.IndexOf(st.subject[st.pos])<0)
            { int p=st.pos; st.pos++; yield return new Slice(p,st.pos); st.pos=p; }
    }
}
sealed class _SPAN : PATTERN {
    readonly string _c; public _SPAN(string c){_c=c;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int p=st.pos;
        while (st.pos<st.subject.Length&&_c.IndexOf(st.subject[st.pos])>=0) st.pos++;
        if (st.pos>p) { yield return new Slice(p,st.pos); st.pos=p; }
    }
}
sealed class _NSPAN : PATTERN {
    readonly string _c; public _NSPAN(string c){_c=c;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int p=st.pos;
        while (st.pos<st.subject.Length&&_c.IndexOf(st.subject[st.pos])>=0) st.pos++;
        yield return new Slice(p,st.pos); st.pos=p;
    }
}
sealed class _BREAK : PATTERN {
    readonly string _c; public _BREAK(string c){_c=c;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int p=st.pos;
        while (st.pos<st.subject.Length&&_c.IndexOf(st.subject[st.pos])<0) st.pos++;
        if (st.pos<st.subject.Length) { yield return new Slice(p,st.pos); st.pos=p; }
    }
}
sealed class _BREAKX : PATTERN {
    readonly _BREAK _b; public _BREAKX(string c){_b=new _BREAK(c);}
    public override IEnumerable<Slice> γ() => _b.γ();
}
sealed class _ARBNO : PATTERN {
    readonly PATTERN _P; public _ARBNO(PATTERN p){_P=p;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int pos0=st.pos;
        var Ag=new List<IEnumerator<Slice>>(); int cursor=0;
        while (cursor>=0) {
            if (cursor>=Ag.Count) { yield return new Slice(pos0,st.pos); }
            if (cursor>=Ag.Count) Ag.Add(_P.γ().GetEnumerator());
            if (Ag[cursor].MoveNext()) cursor++;
            else { Ag[cursor].Dispose(); Ag.RemoveAt(cursor); cursor--; }
        }
        foreach (var e in Ag) e.Dispose();
    }
}
sealed class _MARBNO : PATTERN {
    readonly _ARBNO _a; public _MARBNO(PATTERN p){_a=new _ARBNO(p);}
    public override IEnumerable<Slice> γ() => _a.γ();
}
sealed class _BAL : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int pos0=st.pos; int nest=0;
        st.pos++;
        while (st.pos<=st.subject.Length) {
            char ch=st.subject[st.pos-1];
            if      (ch=='(') nest++;
            else if (ch==')') nest--;
            if      (nest< 0)                             break;
            else if (nest> 0&&st.pos>=st.subject.Length)  break;
            else if (nest==0) yield return new Slice(pos0,st.pos);
            st.pos++;
        }
        st.pos=pos0;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// Stage 6 — new implementations
// ═════════════════════════════════════════════════════════════════════════════

// ── _δ — immediate match assignment (SNOBOL4: P $ N) ─────────────────────────
//
// Writes env[N] = matched substring on every yield from P.
// Permanent — not rolled back when backtracking resumes the generator.
// Python: for _1 in P.γ(): _env._g[N] = STRING(subject[_1]); yield _1

sealed class _δ : PATTERN
{
    readonly PATTERN _P; readonly string _N;
    public _δ(PATTERN p, string n) { _P=p; _N=n; }

    public override IEnumerable<Slice> γ()
    {
        foreach (var sl in _P.γ()) {
            Env.Set(_N, Ϣ.Top.subject.Substring(sl.Start, sl.Stop-sl.Start));
            yield return sl;
        }
    }
}

// ── _Δ — conditional match assignment (SNOBOL4: P . N) ───────────────────────
//
// Pushes a closure onto cstack before yielding; pops it on backtrack.
// SEARCH executes cstack only after the whole match succeeds.
// Python: cstack.append(f"{N}=STRING(subject[s:e])"); yield; cstack.pop()

sealed class _Δ : PATTERN
{
    readonly PATTERN _P; readonly string _N;
    public _Δ(PATTERN p, string n) { _P=p; _N=n; }

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        foreach (var sl in _P.γ()) {
            var captured = st.subject.Substring(sl.Start, sl.Stop-sl.Start);
            var name     = _N;
            Action act   = () => Env.Set(name, captured);
            st.cstack.Add(act);
            yield return sl;
            st.cstack.RemoveAt(st.cstack.Count-1);
        }
    }
}

// ── _Θ — immediate cursor assignment ─────────────────────────────────────────
//
// Writes env[N] = current cursor position immediately, permanently.
// Python: _env._g[N] = Ϣ[-1].pos; yield slice(pos,pos)  [no pop]

sealed class _Θ : PATTERN
{
    readonly string _N; public _Θ(string n){_N=n;}

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        Env.Set(_N, st.pos);
        yield return new Slice(st.pos, st.pos);
    }
}

// ── _θ — conditional cursor assignment ───────────────────────────────────────
//
// Defers env[N] = cursor position until whole match succeeds.
// Python: cstack.append(f"{N}={pos}"); yield; cstack.pop()

sealed class _θ : PATTERN
{
    readonly string _N; public _θ(string n){_N=n;}

    public override IEnumerable<Slice> γ()
    {
        var st   = Ϣ.Top;
        var pos  = st.pos;
        var name = _N;
        Action act = () => Env.Set(name, pos);
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count-1);
    }
}

// ── _Λ — immediate predicate ──────────────────────────────────────────────────
//
// Calls test() right now; yields a zero-length match iff truthy.
// Python: if callable(expr) and expr(): yield slice(pos,pos)

sealed class _Λ : PATTERN
{
    readonly Func<bool> _test; public _Λ(Func<bool> t){_test=t;}

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        if (_test()) yield return new Slice(st.pos, st.pos);
    }
}

// ── _λ — conditional action ───────────────────────────────────────────────────
//
// Defers action until whole match succeeds.
// Python: cstack.append(callable); yield; cstack.pop()

sealed class _λ : PATTERN
{
    readonly Action _cmd; public _λ(Action c){_cmd=c;}

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        st.cstack.Add(_cmd);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count-1);
    }
}

// ── _ζ — deferred pattern reference ──────────────────────────────────────────
//
// Looks up env[name] as a PATTERN at match time.
// Enables forward references and recursive grammars:
//   G["X"] = somePattern;
//   var P = σ("(") + ζ("X") + σ(")");   // ζ resolves X when match runs
//
// Python: P = _env._g[N]; yield from P.γ()

sealed class _ζ : PATTERN
{
    readonly string _N; public _ζ(string n){_N=n;}

    public override IEnumerable<Slice> γ()
    {
        var P = (PATTERN)Env.Get(_N);
        foreach (var s in P.γ()) yield return s;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// S4 — factory functions
// ═════════════════════════════════════════════════════════════════════════════

static class S4
{
    // V6 carry-forward
    public static PATTERN Σ(params PATTERN[] ap)  => new _Σ(ap);
    public static PATTERN Π(params PATTERN[] ap)  => new _Π(ap);
    public static PATTERN π(PATTERN p)            => new _π(p);
    public static PATTERN σ(string s)             => new _σ(s);
    public static PATTERN POS(int n)              => new _POS(n);
    public static PATTERN RPOS(int n)             => new _RPOS(n);
    public static PATTERN ε()                     => new _ε();
    public static PATTERN FAIL()                  => new _FAIL();
    public static PATTERN ABORT()                 => new _ABORT();
    public static PATTERN SUCCEED()               => new _SUCCEED();
    public static PATTERN α()                     => new _α();
    public static PATTERN ω()                     => new _ω();
    public static PATTERN FENCE()                 => new _FENCE();
    public static PATTERN FENCE(PATTERN p)        => new _FENCE(p);
    public static PATTERN LEN(int n)              => new _LEN(n);
    public static PATTERN TAB(int n)              => new _TAB(n);
    public static PATTERN RTAB(int n)             => new _RTAB(n);
    public static PATTERN REM()                   => new _REM();
    public static PATTERN ARB()                   => new _ARB();
    public static PATTERN MARB()                  => new _MARB();
    public static PATTERN ANY(string c)           => new _ANY(c);
    public static PATTERN NOTANY(string c)        => new _NOTANY(c);
    public static PATTERN SPAN(string c)          => new _SPAN(c);
    public static PATTERN NSPAN(string c)         => new _NSPAN(c);
    public static PATTERN BREAK(string c)         => new _BREAK(c);
    public static PATTERN BREAKX(string c)        => new _BREAKX(c);
    public static PATTERN ARBNO(PATTERN p)        => new _ARBNO(p);
    public static PATTERN MARBNO(PATTERN p)       => new _MARBNO(p);
    public static PATTERN BAL()                   => new _BAL();
    // Stage 6
    public static PATTERN δ(PATTERN p, string n)  => new _δ(p, n);
    public static PATTERN Δ(PATTERN p, string n)  => new _Δ(p, n);
    public static PATTERN Θ(string n)             => new _Θ(n);
    public static PATTERN θ(string n)             => new _θ(n);
    public static PATTERN Λ(Func<bool> t)         => new _Λ(t);
    public static PATTERN λ(Action c)             => new _λ(c);
    public static PATTERN ζ(string n)             => new _ζ(n);
    // Env registration
    public static void GLOBALS(Dictionary<string,object> g) => Env.GLOBALS(g);
}

// ── Engine — executes cstack after successful match ───────────────────────────

static class Engine
{
    public static Slice? SEARCH(string S, PATTERN P, bool exc=false)
    {
        for (int c=0; c<=S.Length; c++) {
            var state = new MatchState(c, S);
            Ϣ.Push(state);
            bool popped = false;
            try {
                foreach (var sl in P.γ()) {
                    Ϣ.Pop(); popped = true;
                    foreach (var act in state.cstack) act();   // fire deferred actions
                    return sl;
                }
            }
            catch (F) {
                if (!popped) { Ϣ.Pop(); popped=true; }
                if (exc) throw;
                return null;
            }
            finally { if (!popped) Ϣ.Pop(); }
        }
        if (exc) throw new F("FAIL");
        return null;
    }

    public static Slice? MATCH    (string S,PATTERN P,bool exc=false) => SEARCH(S,POS(0)+P,exc);
    public static Slice? FULLMATCH(string S,PATTERN P,bool exc=false) => SEARCH(S,POS(0)+P+RPOS(0),exc);
}

// ── Test harness ──────────────────────────────────────────────────────────────

static class T
{
    static int _pass, _fail;
    public static void Match   (string l,string s,PATTERN P) => Rep(l,Engine.FULLMATCH(s,P)!=null);
    public static void NoMatch (string l,string s,PATTERN P) => Rep(l,Engine.FULLMATCH(s,P)==null);
    public static void Found   (string l,string s,PATTERN P) => Rep(l,Engine.SEARCH(s,P)!=null);
    public static void NotFound(string l,string s,PATTERN P) => Rep(l,Engine.SEARCH(s,P)==null);
    public static void Eq(string l, object? a, object? b) => Rep(l, Equals(a,b));
    static void Rep(string l,bool ok) {
        if(ok)_pass++;else _fail++;
        Console.WriteLine($"  {(ok?"PASS":"FAIL")}  {l}");
    }
    public static void Summary() => Console.WriteLine($"\n=== {_pass} passed, {_fail} failed ===");
    public static void Section(string t) => Console.WriteLine($"\n── {t} ──");
}

// ═════════════════════════════════════════════════════════════════════════════
// Tests
// ═════════════════════════════════════════════════════════════════════════════

class Program
{
    const string DIGITS = "0123456789";
    const string ALPHA  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    const string ALNUM  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    // The one shared SNOBOL environment for all tests
    static readonly Dictionary<string,object> G = new();

    static string Gs(string k) => G.TryGetValue(k, out var v) ? v.ToString()! : "<unset>";
    static int    Gi(string k) => G.TryGetValue(k, out var v) ? (int)v : -1;

    static void Main()
    {
        GLOBALS(G);
        Console.WriteLine("=== SNOBOL4cs V7  —  Stage 6: δ · Δ · Θ · θ · Λ · λ · ζ ===");
        Test_δ();
        Test_Δ();
        Test_Θ();
        Test_θ();
        Test_Λ();
        Test_λ();
        Test_percent_operator();
        Test_ζ_forward_ref();
        Test_ζ_recursive();
        Test_regression();
        T.Summary();
    }

    // ── δ — immediate match assignment ────────────────────────────────────────
    static void Test_δ()
    {
        T.Section("δ  immediate match assignment");

        Engine.FULLMATCH("42", POS(0) + δ(SPAN(DIGITS), "num") + RPOS(0));
        T.Eq("δ captures \"42\"", Gs("num"), "42");

        Engine.FULLMATCH("hello", POS(0) + δ(SPAN(ALPHA), "word") + RPOS(0));
        T.Eq("δ captures \"hello\"", Gs("word"), "hello");

        // δ fires permanently — even when outer match later fails
        G["num"] = "before";
        Engine.FULLMATCH("123", POS(0) + δ(SPAN(DIGITS), "num") + σ("NOPE") + RPOS(0));
        T.Eq("δ permanent: written even on outer failure", Gs("num"), "123");

        // δ with alternation
        Engine.FULLMATCH("cat", POS(0) + δ(σ("cat") | σ("dog"), "pet") + RPOS(0));
        T.Eq("δ(cat|dog) on \"cat\"", Gs("pet"), "cat");
        Engine.FULLMATCH("dog", POS(0) + δ(σ("cat") | σ("dog"), "pet") + RPOS(0));
        T.Eq("δ(cat|dog) on \"dog\"", Gs("pet"), "dog");
    }

    // ── Δ — conditional match assignment ─────────────────────────────────────
    static void Test_Δ()
    {
        T.Section("Δ  conditional match assignment");

        Engine.FULLMATCH("hello", POS(0) + Δ(SPAN(ALPHA), "word") + RPOS(0));
        T.Eq("Δ fires on success", Gs("word"), "hello");

        // Δ does NOT fire when the overall match fails
        G["word"] = "before";
        Engine.FULLMATCH("abc123", POS(0) + Δ(SPAN(ALPHA), "word") + σ("NOPE") + RPOS(0));
        T.Eq("Δ not fired on failure", Gs("word"), "before");

        // Δ captures the value that contributed to the successful match,
        // not an intermediate backtracked one
        Engine.FULLMATCH("abc", POS(0) + Δ(σ("ab") | σ("abc"), "part") + RPOS(0));
        T.Eq("Δ captures the match that succeeded: \"abc\"", Gs("part"), "abc");

        // Multiple Δ in sequence: both fire on success
        Engine.FULLMATCH("hello42",
            POS(0) + Δ(SPAN(ALPHA), "w") + Δ(SPAN(DIGITS), "n") + RPOS(0));
        T.Eq("Δ sequence: word",   Gs("w"), "hello");
        T.Eq("Δ sequence: digits", Gs("n"), "42");
    }

    // ── Θ — immediate cursor assignment ──────────────────────────────────────
    static void Test_Θ()
    {
        T.Section("Θ  immediate cursor assignment");

        Engine.FULLMATCH("hello", POS(0) + σ("hel") + Θ("cp") + σ("lo") + RPOS(0));
        T.Eq("Θ after \"hel\" = 3", Gi("cp"), 3);

        Engine.FULLMATCH("abc", POS(0) + Θ("start") + REM() + RPOS(0));
        T.Eq("Θ at start = 0", Gi("start"), 0);

        // Θ is immediate — writes even when outer match fails
        G["cp"] = -1;
        Engine.FULLMATCH("hi", POS(0) + σ("h") + Θ("cp") + σ("NOPE") + RPOS(0));
        T.Eq("Θ immediate: written even on outer failure", Gi("cp"), 1);
    }

    // ── θ — conditional cursor assignment ────────────────────────────────────
    static void Test_θ()
    {
        T.Section("θ  conditional cursor assignment");

        Engine.FULLMATCH("hello", POS(0) + σ("hel") + θ("cp") + σ("lo") + RPOS(0));
        T.Eq("θ fires on success: cp = 3", Gi("cp"), 3);

        G["cp"] = -1;
        Engine.FULLMATCH("hi", POS(0) + σ("h") + θ("cp") + σ("NOPE") + RPOS(0));
        T.Eq("θ not fired on failure: cp = -1", Gi("cp"), -1);

        // θ vs Θ contrast in same pattern: Θ writes, θ does not, when match fails
        G["imm"] = -1; G["cond"] = -1;
        Engine.FULLMATCH("xy", POS(0) + σ("x") + Θ("imm") + θ("cond") + σ("NOPE") + RPOS(0));
        T.Eq("Θ fired (imm=1)",    Gi("imm"),  1);
        T.Eq("θ not fired (cond=-1)", Gi("cond"), -1);
    }

    // ── Λ — immediate predicate ───────────────────────────────────────────────
    static void Test_Λ()
    {
        T.Section("Λ  immediate predicate / guard");

        T.Match  ("Λ(true) passes",  "abc", POS(0) + Λ(()=>true)  + REM() + RPOS(0));
        T.NoMatch("Λ(false) blocks", "abc", POS(0) + Λ(()=>false) + REM() + RPOS(0));

        // Λ as numeric guard: capture digits, check value
        PATTERN big = POS(0) + δ(SPAN(DIGITS), "n") + Λ(()=>int.Parse(Gs("n"))>10) + RPOS(0);
        T.Match  ("Λ guard: 42 > 10",    "42", big);
        T.NoMatch("Λ guard: 5 not > 10", "5",  big);
        T.Match  ("Λ guard: 11 > 10",    "11", big);

        // Λ is evaluated immediately — counts actual calls
        int calls = 0;
        Engine.FULLMATCH("x", POS(0) + Λ(()=>{calls++;return true;}) + REM() + RPOS(0));
        T.Eq("Λ called exactly once", calls, 1);
    }

    // ── λ — conditional action ────────────────────────────────────────────────
    static void Test_λ()
    {
        T.Section("λ  conditional action");

        // λ fires after successful match
        int fired = 0;
        Engine.FULLMATCH("hi", POS(0) + λ(()=>fired++) + REM() + RPOS(0));
        T.Eq("λ fires on success", fired, 1);

        // λ does NOT fire when match fails
        fired = 0;
        Engine.FULLMATCH("hi", POS(0) + λ(()=>fired++) + σ("NOPE") + RPOS(0));
        T.Eq("λ not fired on failure", fired, 0);

        // λ used to run post-match logic (classic SNOBOL4 idiom).
        // Important: λ closures read env *at cstack fire time* (after the full
        // match).  To capture multiple fields, use unique env keys per field so
        // later δ writes don't overwrite earlier ones before λ reads them.
        int processed = 0;
        PATTERN fields =
            POS(0)
            + δ(BREAK(","), "f0") + σ(",")
            + δ(BREAK(","), "f1") + σ(",")
            + δ(REM(),       "f2")
            + λ(()=> processed = 3)
            + RPOS(0);
        Engine.FULLMATCH("a,bb,ccc", fields);
        T.Eq("λ CSV: f0 = \"a\"",    Gs("f0"), "a");
        T.Eq("λ CSV: f1 = \"bb\"",   Gs("f1"), "bb");
        T.Eq("λ CSV: f2 = \"ccc\"",  Gs("f2"), "ccc");
        T.Eq("λ CSV: action fired",  processed, 3);
    }

    // ── % operator — shorthand for Δ ─────────────────────────────────────────
    static void Test_percent_operator()
    {
        T.Section("P % \"name\"  operator shorthand for Δ");

        Engine.FULLMATCH("world",
            POS(0) + (SPAN(ALPHA) % "w") + RPOS(0));
        T.Eq("SPAN(ALPHA) % \"w\" captures \"world\"", Gs("w"), "world");

        Engine.FULLMATCH("hello42",
            POS(0) + (SPAN(ALPHA) % "word") + (SPAN(DIGITS) % "num") + RPOS(0));
        T.Eq("% sequence: word",   Gs("word"), "hello");
        T.Eq("% sequence: digits", Gs("num"),  "42");

        // % does not fire on failure — same semantics as Δ
        G["w"] = "before";
        Engine.FULLMATCH("abc", POS(0) + (SPAN(ALPHA) % "w") + σ("NOPE") + RPOS(0));
        T.Eq("% not fired on failure", Gs("w"), "before");
    }

    // ── ζ — deferred pattern reference (forward reference) ───────────────────
    static void Test_ζ_forward_ref()
    {
        T.Section("ζ  deferred pattern reference");

        // Store pattern in env, reference it by name
        G["WORD"] = POS(0) + SPAN(ALPHA) + RPOS(0);
        T.Match  ("ζ(WORD) resolves from env: \"hello\"", "hello", ζ("WORD"));
        T.NoMatch("ζ(WORD) no match \"123\"",             "123",   ζ("WORD"));

        // ζ re-evaluates at each match — updating env changes behaviour
        G["P"] = σ("foo");
        T.Match  ("ζ(P) = σ(foo) matches \"foo\"", "foo", POS(0)+ζ("P")+RPOS(0));
        G["P"] = σ("bar");
        T.Match  ("ζ(P) = σ(bar) after update matches \"bar\"", "bar", POS(0)+ζ("P")+RPOS(0));
        T.NoMatch("ζ(P) = σ(bar) no match \"foo\"", "foo", POS(0)+ζ("P")+RPOS(0));
    }

    // ── ζ — recursive grammar (the real payoff) ───────────────────────────────
    //
    // A simple expression parser using ζ for left-recursion avoidance.
    // Grammar:  expr  ::=  atom ( ('+' | '-') atom )*
    //           atom  ::=  digit+  |  '(' expr ')'
    //
    // We build this with ζ("expr") to allow atom to reference expr before
    // expr is fully constructed — the classic forward-reference case.

    static void Test_ζ_recursive()
    {
        T.Section("ζ  recursive grammar (expr parser)");

        // atom = SPAN(DIGITS) | '(' + ζ("expr") + ')'
        PATTERN atom =
              SPAN(DIGITS)
            | σ("(") + ζ("expr") + σ(")");

        // expr = atom + ARBNO( ('+' | '-') + atom )
        PATTERN expr = atom + ARBNO((σ("+") | σ("-")) + atom);

        // Register expr by name so ζ("expr") can find it
        G["expr"] = expr;

        PATTERN full = POS(0) + expr + RPOS(0);

        string[] yes = { "1", "42", "1+2", "10-3+4", "(1)", "(1+2)", "1+(2+3)", "(1+2)-(3+4)" };
        string[] no  = { "", "+", "1+", "(1", "1)", "1++2" };

        foreach (var s in yes) T.Match  ($"expr \"{s}\"", s, full);
        foreach (var s in no)  T.NoMatch($"expr no \"{s}\"", s, full);
    }

    // ── Regression ────────────────────────────────────────────────────────────
    static void Test_regression()
    {
        T.Section("Regression: prior stages");

        // BEAD
        PATTERN bead = POS(0) + Π(σ("B"),σ("F"),σ("L"),σ("R"))
            + Π(σ("E"),σ("EA")) + Π(σ("D"),σ("DS")) + RPOS(0);
        T.Match  ("BEAD \"READS\"", "READS", bead);
        T.NoMatch("BEAD \"BID\"",   "BID",   bead);

        // identifier
        PATTERN ident = POS(0)+ANY(ALPHA)+NSPAN(ALNUM)+RPOS(0);
        T.Match  ("ident \"CamelCase\"", "CamelCase", ident);
        T.NoMatch("ident \"1bad\"",      "1bad",      ident);

        // real_number
        PATTERN real = POS(0)+~ANY("+-")+SPAN(DIGITS)+~(σ(".")+NSPAN(DIGITS))+RPOS(0);
        T.Match  ("real \"+3.14\"", "+3.14", real);
        T.NoMatch("real \"abc\"",   "abc",   real);

        // ARBNO CSV
        PATTERN csvid = ANY(ALPHA)+NSPAN(ALNUM);
        PATTERN csv   = POS(0)+csvid+ARBNO(σ(",")+csvid)+RPOS(0);
        T.Match  ("csv \"a,b,c\"",    "a,b,c",   csv);
        T.NoMatch("csv \"a,,b\"",     "a,,b",    csv);
    }
}