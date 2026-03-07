// SNOBOL4cs_v6.cs  —  SNOBOL4 pattern engine in C#, Version 6
//
// Stage 5 — Repetition + Balanced:
//   ARBNO(P)   zero-or-more repetitions of P, shortest first
//   MARBNO(P)  alias for ARBNO
//   BAL        match one balanced parenthesised token (yields multiple times)
//
// Carried forward unchanged from V5:
//   σ Σ Π π POS RPOS ε FAIL ABORT SUCCEED α ω FENCE
//   LEN TAB RTAB REM ARB MARB
//   ANY NOTANY SPAN NSPAN BREAK BREAKX
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using static S4;

sealed class F : Exception { public F(string m) : base(m) {} }

sealed class MatchState
{
    public int pos; public string subject;
    public MatchState(int p, string s) { pos=p; subject=s; }
}

static class Ϣ
{
    static readonly Stack<MatchState> _s = new();
    public static void       Push(MatchState s) => _s.Push(s);
    public static void       Pop()              => _s.Pop();
    public static MatchState Top                => _s.Peek();
}

readonly struct Slice
{
    public readonly int Start, Stop;
    public Slice(int s, int e) { Start=s; Stop=e; }
    public override string ToString() => $"[{Start}:{Stop}]";
}

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
}

// ── V5 carry-forward ──────────────────────────────────────────────────────────

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
    readonly _ARB _arb=new();
    public override IEnumerable<Slice> γ() => _arb.γ();
}
sealed class _ANY : PATTERN {
    readonly string _c; public _ANY(string c){_c=c;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top;
        if (st.pos<st.subject.Length && _c.IndexOf(st.subject[st.pos])>=0)
            { int p=st.pos; st.pos++; yield return new Slice(p,st.pos); st.pos=p; }
    }
}
sealed class _NOTANY : PATTERN {
    readonly string _c; public _NOTANY(string c){_c=c;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top;
        if (st.pos<st.subject.Length && _c.IndexOf(st.subject[st.pos])<0)
            { int p=st.pos; st.pos++; yield return new Slice(p,st.pos); st.pos=p; }
    }
}
sealed class _SPAN : PATTERN {
    readonly string _c; public _SPAN(string c){_c=c;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int p=st.pos;
        while (st.pos<st.subject.Length && _c.IndexOf(st.subject[st.pos])>=0) st.pos++;
        if (st.pos>p) { yield return new Slice(p,st.pos); st.pos=p; }
    }
}
sealed class _NSPAN : PATTERN {
    readonly string _c; public _NSPAN(string c){_c=c;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int p=st.pos;
        while (st.pos<st.subject.Length && _c.IndexOf(st.subject[st.pos])>=0) st.pos++;
        yield return new Slice(p,st.pos); st.pos=p;
    }
}
sealed class _BREAK : PATTERN {
    readonly string _c; public _BREAK(string c){_c=c;}
    public override IEnumerable<Slice> γ() {
        var st=Ϣ.Top; int p=st.pos;
        while (st.pos<st.subject.Length && _c.IndexOf(st.subject[st.pos])<0) st.pos++;
        if (st.pos<st.subject.Length) { yield return new Slice(p,st.pos); st.pos=p; }
    }
}
sealed class _BREAKX : PATTERN {
    readonly _BREAK _b; public _BREAKX(string c){_b=new _BREAK(c);}
    public override IEnumerable<Slice> γ() => _b.γ();
}

// ═════════════════════════════════════════════════════════════════════════════
// Stage 5 — new implementations
// ═════════════════════════════════════════════════════════════════════════════

// ── _ARBNO — zero-or-more repetitions of P, shortest first ───────────────────
//
// Python:
//   cursor = 0
//   Ag = []
//   while cursor >= 0:
//       if cursor >= len(Ag):           ← all active generators succeeded
//           yield slice(pos0, pos)      ← offer current extent
//       if cursor >= len(Ag):
//           Ag.append(P.γ())            ← add one more repetition
//       try:
//           next(Ag[cursor])            ← advance that repetition
//           cursor += 1                 ← success: try one more
//       except StopIteration:
//           cursor -= 1                 ← backtrack: last rep exhausted
//           Ag.pop()
//
// The key insight: `next(Ag[cursor])` advances pos as a side-effect.
// We only need to know if P succeeded (moved the cursor), not what it matched.
// After yield, the surrounding Σ may backtrack into us; we resume the while
// loop and try extending by one more repetition — or, if the last one is
// exhausted, we pop and try a shorter prefix.
//
// In C# we use a List<IEnumerator<Slice>> as the dynamic generator array.

sealed class _ARBNO : PATTERN
{
    readonly PATTERN _P;
    public _ARBNO(PATTERN p) { _P = p; }

    public override IEnumerable<Slice> γ()
    {
        var st   = Ϣ.Top;
        int pos0 = st.pos;

        var Ag = new List<IEnumerator<Slice>>();
        int cursor = 0;

        while (cursor >= 0)
        {
            // All generators up to cursor have succeeded once — yield current span
            if (cursor >= Ag.Count)
            {
                yield return new Slice(pos0, st.pos);
                // After yield: backtracking requested, try one more repetition
            }

            // Extend Ag if needed
            if (cursor >= Ag.Count)
                Ag.Add(_P.γ().GetEnumerator());

            // Advance this repetition's generator
            if (Ag[cursor].MoveNext())
            {
                cursor++;           // succeeded: go deeper
            }
            else
            {
                Ag[cursor].Dispose();
                Ag.RemoveAt(cursor);
                cursor--;           // failed: backtrack
            }
        }

        // Clean up any remaining enumerators
        foreach (var e in Ag) e.Dispose();
    }

    public override string ToString() => $"ARBNO(...)";
}

// ── _MARBNO — alias for ARBNO ─────────────────────────────────────────────────

sealed class _MARBNO : PATTERN
{
    readonly _ARBNO _a;
    public _MARBNO(PATTERN p) { _a = new _ARBNO(p); }
    public override IEnumerable<Slice> γ() => _a.γ();
    public override string ToString() => "MARBNO(...)";
}

// ── _BAL — match one balanced parenthesised token ────────────────────────────
//
// Python:
//   pos0 = pos; nest = 0
//   pos += 1                        ← consume the first character unconditionally
//   while pos <= len(subject):
//       ch = subject[pos-1]         ← look at char we just advanced past
//       if ch == '(':  nest += 1
//       if ch == ')':  nest -= 1
//       if nest < 0:   break        ← unmatched ')' — stop
//       elif nest > 0 and pos >= len(subject): break   ← unclosed '(' — stop
//       elif nest == 0: yield slice(pos0, pos)          ← balanced point
//       pos += 1
//   pos = pos0
//
// BAL advances pos by 1 before the first check, so it always consumes at least
// one character. It yields at every point where nesting depth returns to 0.
// This means it can yield multiple times (with increasing lengths).
//
// Example on "(a)(b)":
//   pos=0: advance to 1, ch='(' → nest=1; advance to 2, ch='a' → nest=1;
//          advance to 3, ch=')' → nest=0, yield [0:3]; advance to 4, ch='('...
//   ... eventually also yields [0:6] for the full "(a)(b)".
//
// On "(a+b)":
//   yields [0:5] when the outer ')' brings nest back to 0.
//
// BAL on a bare token like "abc" (no parens):
//   pos=0: advance to 1, ch='a' → nest=0, yield [0:1];
//          advance to 2, ch='b' → nest=0, yield [0:2]; etc.
//   So BAL on a non-paren char yields each prefix.

sealed class _BAL : PATTERN
{
    public override IEnumerable<Slice> γ()
    {
        var st   = Ϣ.Top;
        int pos0 = st.pos;
        int nest = 0;

        st.pos++;   // advance one character unconditionally

        while (st.pos <= st.subject.Length)
        {
            char ch = st.subject[st.pos - 1];   // char we just advanced past

            if      (ch == '(') nest++;
            else if (ch == ')') nest--;

            if      (nest <  0)                                   break;  // unmatched ')'
            else if (nest >  0 && st.pos >= st.subject.Length)   break;  // unclosed '('
            else if (nest == 0)
                yield return new Slice(pos0, st.pos);                     // balanced

            st.pos++;
        }

        st.pos = pos0;   // restore cursor on backtrack
    }

    public override string ToString() => "BAL()";
}

// ═════════════════════════════════════════════════════════════════════════════
// S4 — factory functions
// ═════════════════════════════════════════════════════════════════════════════

static class S4
{
    // V5 carry-forward
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
    // Stage 5
    public static PATTERN ARBNO(PATTERN p)        => new _ARBNO(p);
    public static PATTERN MARBNO(PATTERN p)       => new _MARBNO(p);
    public static PATTERN BAL()                   => new _BAL();
}

// ── Engine ────────────────────────────────────────────────────────────────────

static class Engine
{
    public static Slice? SEARCH(string S, PATTERN P, bool exc=false)
    {
        for (int c=0; c<=S.Length; c++) {
            Ϣ.Push(new MatchState(c,S));
            bool popped=false;
            try {
                foreach (var sl in P.γ()) { Ϣ.Pop(); popped=true; return sl; }
            }
            catch (F) {
                if (!popped){Ϣ.Pop();popped=true;}
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
    public static void Slice(string l,string s,PATTERN P,int start,int stop) {
        var r=Engine.SEARCH(s,P);
        Rep(l,r!=null&&r.Value.Start==start&&r.Value.Stop==stop);
    }
    public static void Throws(string l,string s,PATTERN P) {
        bool ok=false;
        try { Engine.SEARCH(s,P,exc:true); } catch(F){ok=true;}
        Rep(l,ok);
    }
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

    static void Main()
    {
        Console.WriteLine("=== SNOBOL4cs V6  —  Stage 5: ARBNO · MARBNO · BAL ===");
        Test_ARBNO();
        Test_BAL();
        Test_ARBNO_patterns();  // test_01.py Arb patterns with ARBNO
        Test_BAL_patterns();    // test_01.py Bal patterns
        Test_regression();
        T.Summary();
    }

    // ── ARBNO ─────────────────────────────────────────────────────────────────
    // ARBNO(P) matches zero-or-more repetitions of P, shortest first.
    static void Test_ARBNO()
    {
        T.Section("ARBNO(P)");

        // Zero repetitions = always matches empty
        PATTERN A = ARBNO(σ("ab"));
        T.Slice  ("ARBNO(σ(ab)) first match = [0:0]",       "ababab", A, 0, 0);

        // ARBNO fullmatches zero repetitions of P on empty string
        T.Match  ("ARBNO(σ(x)) fullmatches \"\"",            "",       ARBNO(σ("x")));

        // ARBNO with POS+RPOS forces all repetitions
        T.Match  ("ARBNO(σ(ab)) fullmatches \"ababab\"",     "ababab", POS(0)+ARBNO(σ("ab"))+RPOS(0));
        T.Match  ("ARBNO(σ(ab)) fullmatches \"\"",           "",       POS(0)+ARBNO(σ("ab"))+RPOS(0));
        T.NoMatch("ARBNO(σ(ab)) no match \"ababx\"",         "ababx",  POS(0)+ARBNO(σ("ab"))+RPOS(0));

        // ARBNO of a char-class: zero-or-more digits
        PATTERN digits_star = POS(0) + ARBNO(ANY(DIGITS)) + RPOS(0);
        T.Match  ("ARBNO(ANY(digits)) fullmatches \"123\"",  "123",    digits_star);
        T.Match  ("ARBNO(ANY(digits)) fullmatches \"\"",     "",       digits_star);
        T.NoMatch("ARBNO(ANY(digits)) no match \"12a\"",     "12a",    digits_star);

        // Shortest-first: SEARCH finds empty match first
        T.Slice  ("ARBNO(ANY(digits)) shortest = [0:0] in \"123\"","123",
                  ARBNO(ANY(DIGITS)), 0, 0);

        // ARBNO forces greedy match only when combined with anchors or suffix
        PATTERN greedy = POS(0) + ARBNO(ANY(DIGITS)) + RPOS(0);
        T.Match  ("ARBNO greedy with anchors matches \"9876\"","9876", greedy);

        // MARBNO is identical
        T.Match  ("MARBNO(σ(ab)) fullmatches \"ababab\"",   "ababab", POS(0)+MARBNO(σ("ab"))+RPOS(0));
        T.Match  ("MARBNO(σ(ab)) fullmatches \"\"",         "",       POS(0)+MARBNO(σ("ab"))+RPOS(0));
    }

    // ── BAL ───────────────────────────────────────────────────────────────────
    // BAL matches one balanced token: either a non-paren char,
    // or a '(...)' group (possibly nested), yielding at each balanced point.
    static void Test_BAL()
    {
        T.Section("BAL()");

        // Single char (no parens) — BAL yields it
        T.Slice  ("BAL on \"a\" = [0:1]",                   "a",      BAL(), 0, 1);

        // Simple paren group
        T.Slice  ("BAL on \"(x)\" = [0:3]",                 "(x)",    BAL(), 0, 3);

        // Nested parens
        T.Slice  ("BAL on \"((a))\" = [0:5]",               "((a))",  BAL(), 0, 5);

        // Unbalanced opening — BAL can't close the paren, but SEARCH advances
        // cursor and finds single-char matches in the non-paren region
        T.Found   ("BAL finds chars inside \"(unclosed\"",  "(unclosed", BAL());

        // Unbalanced closing — BAL stops before it
        // "(a)" followed by ")" — BAL on "(a))" yields [0:3], but then
        // the next char is ')' which causes nest < 0, stopping the generator.
        // SEARCH returns the first balanced match [0:3].
        T.Slice  ("BAL on \"(a))\" = [0:3] (stops before extra ')'",
                  "(a))",  BAL(), 0, 3);

        // Empty parens — "()" is balanced
        T.Slice  ("BAL on \"()\" = [0:2]",                  "()",     BAL(), 0, 2);

        // BAL in a sequence: extract first balanced token
        PATTERN token = POS(0) + BAL() + RPOS(0);
        T.Match  ("BAL fullmatches \"(a+b)\"",               "(a+b)",  token);
        T.Match  ("BAL fullmatches \"x\"",                   "x",      token);
        T.Match  ("BAL fullmatches \"(a)b\" (yields [0:4] eventually)", "(a)b",   token);

        // BAL yields progressively longer matches — SEARCH gives shortest
        // On "ab": BAL yields [0:1] first (just "a"), then [0:2] ("ab")
        T.Slice  ("BAL shortest on \"ab\" = [0:1]",          "ab",     BAL(), 0, 1);
    }

    // ── ARBNO combinatorial (test_01.py Arb patterns) ─────────────────────────
    // From test_01.py: patterns built with ARBNO rather than ARB
    static void Test_ARBNO_patterns()
    {
        T.Section("ARBNO combinatorial (test_01.py-style)");

        // CSV-like: comma-separated identifiers
        PATTERN ident   = ANY(ALPHA) + NSPAN(ALNUM);
        PATTERN csvline = POS(0) + ident + ARBNO(σ(",") + ident) + RPOS(0);

        T.Match  ("CSV: \"a\"",               "a",          csvline);
        T.Match  ("CSV: \"a,b\"",             "a,b",        csvline);
        T.Match  ("CSV: \"a,b,c\"",           "a,b,c",      csvline);
        T.Match  ("CSV: \"foo,bar,baz\"",      "foo,bar,baz",csvline);
        T.NoMatch("CSV: \"\"",                "",            csvline);
        T.NoMatch("CSV: \"a,\"",              "a,",          csvline);
        T.NoMatch("CSV: \",a\"",              ",a",          csvline);

        // Repetition of a group: one-or-more (using Σ trick: P + ARBNO(P))
        PATTERN one_or_more_digits = SPAN(DIGITS);  // simpler
        PATTERN zero_or_more       = ARBNO(ANY(DIGITS));
        PATTERN non_empty_num      = POS(0) + ANY(DIGITS) + ARBNO(ANY(DIGITS)) + RPOS(0);

        T.Match  ("one-or-more digits: \"0\"",       "0",    non_empty_num);
        T.Match  ("one-or-more digits: \"123\"",     "123",  non_empty_num);
        T.NoMatch("one-or-more digits: \"\"",        "",     non_empty_num);
        T.NoMatch("one-or-more digits: \"12a\"",     "12a",  non_empty_num);

        // Nested ARBNO: ARBNO of ARBNO(P) — matches any number of groups
        PATTERN ab_star_star = POS(0) + ARBNO(ARBNO(σ("a")) + σ("b")) + RPOS(0);
        T.Match  ("ARBNO(ARBNO(a)+b) matches \"b\"",        "b",      ab_star_star);
        T.Match  ("ARBNO(ARBNO(a)+b) matches \"ab\"",       "ab",     ab_star_star);
        T.Match  ("ARBNO(ARBNO(a)+b) matches \"aab\"",      "aab",    ab_star_star);
        T.Match  ("ARBNO(ARBNO(a)+b) matches \"ababaab\"",  "ababaab",ab_star_star);
        T.Match  ("ARBNO(ARBNO(a)+b) matches \"\"",         "",       ab_star_star);
        T.NoMatch("ARBNO(ARBNO(a)+b) no match \"a\"",       "a",      ab_star_star);
    }

    // ── BAL combinatorial (test_01.py Bal patterns) ───────────────────────────
    static void Test_BAL_patterns()
    {
        T.Section("BAL combinatorial (test_01.py-style)");

        // test_01.py Bal: POS(0) + BAL() % 'OUTPUT' + RPOS(0)
        // (without assignment for now) — just test the structural match
        PATTERN Bal = POS(0) + BAL() + RPOS(0);

        // BAL yields at every point where nest==0, including bare non-paren chars
        // and sequences like "(a)(b)" where it yields [0:3] then [0:6].
        // Strings starting with ')' yield nothing from pos 0 (nest goes negative).
        string[] yes = { "a", "x", "()", "(a)", "(a+b)", "((x))", "(())", "(a)(b)" };
        string[] no  = { "", "(", "(a", ")", "a)" };

        foreach (var s in yes) T.Match  ($"Bal \"{s}\"", s, Bal);
        foreach (var s in no)  T.NoMatch($"Bal no \"{s}\"", s, Bal);

        // BAL used to extract balanced expressions from a larger string
        PATTERN find_bal = σ("=") + BAL();
        T.Found  ("find_bal in \"x=(a+b)\"",   "x=(a+b)",   find_bal);
        T.Found  ("find_bal in \"y=z\"",        "y=z",       find_bal);
        T.NotFound("find_bal in \"(no-eq)\"",   "(no-eq)",   find_bal);

        // Comma-separated balanced expressions: BAL + ARBNO(σ(",") + BAL())
        PATTERN bal_list = POS(0) + BAL() + ARBNO(σ(",") + BAL()) + RPOS(0);
        T.Match  ("bal_list: \"a\"",             "a",         bal_list);
        T.Match  ("bal_list: \"(x),y\"",         "(x),y",     bal_list);
        T.Match  ("bal_list: \"(a),(b),(c)\"",   "(a),(b),(c)",bal_list);
        T.NoMatch("bal_list: \"(a,b\"",          "(a,b",      bal_list);
    }

    // ── Regressions ───────────────────────────────────────────────────────────
    static void Test_regression()
    {
        T.Section("Regression: V1-V5 patterns");

        // BEAD
        PATTERN bead =
              POS(0) + Π(σ("B"),σ("F"),σ("L"),σ("R"))
            + Π(σ("E"),σ("EA")) + Π(σ("D"),σ("DS")) + RPOS(0);
        string[] byes={"BED","FEAD","LEADS","READS"};
        string[] bnos={"BID","BREAD",""};
        foreach (var w in byes) T.Match  ($"BEAD \"{w}\"",w,bead);
        foreach (var w in bnos) T.NoMatch($"BEAD \"{w}\"",w,bead);

        // identifier
        PATTERN ident = POS(0)+ANY(ALPHA)+NSPAN(ALNUM)+RPOS(0);
        T.Match  ("ident \"Hello\"",  "Hello",  ident);
        T.NoMatch("ident \"1bad\"",   "1bad",   ident);

        // real_number
        PATTERN real =
              POS(0) + ~ANY("+-") + SPAN(DIGITS)
            + ~(σ(".") + NSPAN(DIGITS)) + RPOS(0);
        T.Match  ("real \"3.14\"",    "3.14",   real);
        T.Match  ("real \"-7\"",      "-7",     real);
        T.NoMatch("real \"abc\"",     "abc",    real);
    }
}
