// SNOBOL4cs_v5.cs  —  SNOBOL4 pattern engine in C#, Version 5
//
// Stage 4 — Character-class primitives:
//   ANY(chars)     match exactly one character from chars
//   NOTANY(chars)  match exactly one character NOT in chars
//   SPAN(chars)    match one-or-more characters from chars (longest, yields once)
//   NSPAN(chars)   match zero-or-more characters from chars (yields once, never fails)
//   BREAK(chars)   match zero-or-more chars UP TO (not including) a char in chars;
//                  succeeds only if that terminator is actually present
//   BREAKX(chars)  alias for BREAK
//
// Together these unlock identifier/real_number patterns from test_01.py.
//
// Carried forward unchanged: σ Σ Π π POS RPOS ε FAIL ABORT SUCCEED α ω FENCE
//                             LEN TAB RTAB REM ARB MARB
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

// ── V4 carry-forward ──────────────────────────────────────────────────────────

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

// ═════════════════════════════════════════════════════════════════════════════
// Stage 4 — Character-class implementations
// ═════════════════════════════════════════════════════════════════════════════

// ── _ANY — match exactly one character from the set ───────────────────────────
//
// Python:
//   if pos < len(subject) and subject[pos] in chars:
//       pos += 1
//       yield slice(pos-1, pos)
//       pos -= 1
//
// Yields at most once (0 or 1 match). Always single-char.

sealed class _ANY : PATTERN
{
    readonly string _chars;
    public _ANY(string chars) { _chars = chars; }

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        if (st.pos < st.subject.Length && _chars.IndexOf(st.subject[st.pos]) >= 0)
        {
            int p = st.pos;
            st.pos++;
            yield return new Slice(p, st.pos);
            st.pos = p;
        }
    }

    public override string ToString() => $"ANY(\"{_chars}\")";
}

// ── _NOTANY — match exactly one character NOT in the set ──────────────────────
//
// Python:
//   if pos < len(subject) and subject[pos] not in chars:
//       pos += 1
//       yield slice(pos-1, pos)
//       pos -= 1

sealed class _NOTANY : PATTERN
{
    readonly string _chars;
    public _NOTANY(string chars) { _chars = chars; }

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        if (st.pos < st.subject.Length && _chars.IndexOf(st.subject[st.pos]) < 0)
        {
            int p = st.pos;
            st.pos++;
            yield return new Slice(p, st.pos);
            st.pos = p;
        }
    }

    public override string ToString() => $"NOTANY(\"{_chars}\")";
}

// ── _SPAN — match one-or-more characters from the set ─────────────────────────
//
// Python:
//   while pos < len(subject) and subject[pos] in chars: pos += 1
//   if pos > pos0:            ← requires at least one character
//       yield slice(pos0, pos)
//       pos = pos0
//
// SPAN fails on an empty match. It yields exactly once (non-backtracking).

sealed class _SPAN : PATTERN
{
    readonly string _chars;
    public _SPAN(string chars) { _chars = chars; }

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top; int p = st.pos;
        while (st.pos < st.subject.Length && _chars.IndexOf(st.subject[st.pos]) >= 0)
            st.pos++;
        if (st.pos > p)                         // at least one char consumed
        {
            yield return new Slice(p, st.pos);
            st.pos = p;
        }
    }

    public override string ToString() => $"SPAN(\"{_chars}\")";
}

// ── _NSPAN — match zero-or-more characters from the set ──────────────────────
//
// Python:
//   while pos < len(subject) and subject[pos] in chars: pos += 1
//   yield slice(pos0, pos)    ← always yields, even on zero chars
//   pos = pos0
//
// NSPAN never fails. The "N" stands for "Null allowed" (zero-length OK).

sealed class _NSPAN : PATTERN
{
    readonly string _chars;
    public _NSPAN(string chars) { _chars = chars; }

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top; int p = st.pos;
        while (st.pos < st.subject.Length && _chars.IndexOf(st.subject[st.pos]) >= 0)
            st.pos++;
        yield return new Slice(p, st.pos);      // always yield, even zero-length
        st.pos = p;
    }

    public override string ToString() => $"NSPAN(\"{_chars}\")";
}

// ── _BREAK — scan forward until a char in the set is found ────────────────────
//
// Python:
//   while pos < len(subject) and subject[pos] not in chars: pos += 1
//   if pos < len(subject):    ← the break char must actually be present
//       yield slice(pos0, pos)
//       pos = pos0
//
// Key points:
//   • BREAK *can* yield a zero-length match (if the break char is at pos0).
//   • BREAK fails if end-of-string is reached without finding the break char.
//   • Like SPAN, it yields at most once (non-backtracking).

sealed class _BREAK : PATTERN
{
    readonly string _chars;
    public _BREAK(string chars) { _chars = chars; }

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top; int p = st.pos;
        while (st.pos < st.subject.Length && _chars.IndexOf(st.subject[st.pos]) < 0)
            st.pos++;
        if (st.pos < st.subject.Length)         // terminator found — not at end
        {
            yield return new Slice(p, st.pos);
            st.pos = p;
        }
    }

    public override string ToString() => $"BREAK(\"{_chars}\")";
}

// ── _BREAKX — alias for BREAK (Python: class BREAKX(BREAK): pass) ────────────
//
// In full SNOBOL4, BREAKX differs from BREAK in that on backtrack it advances
// past the break character and tries again. The pure-Python backend treats them
// identically. We follow suit here; BREAKX will be differentiated if/when a
// full backtracking variant is needed.

sealed class _BREAKX : PATTERN
{
    readonly _BREAK _brk;
    public _BREAKX(string chars) { _brk = new _BREAK(chars); }
    public override IEnumerable<Slice> γ() => _brk.γ();
    public override string ToString() => $"BREAKX(\"{_brk}\")";
}

// ═════════════════════════════════════════════════════════════════════════════
// S4 — factory functions
// ═════════════════════════════════════════════════════════════════════════════

static class S4
{
    // V4 carry-forward
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
    // Stage 4
    public static PATTERN ANY(string chars)       => new _ANY(chars);
    public static PATTERN NOTANY(string chars)    => new _NOTANY(chars);
    public static PATTERN SPAN(string chars)      => new _SPAN(chars);
    public static PATTERN NSPAN(string chars)     => new _NSPAN(chars);
    public static PATTERN BREAK(string chars)     => new _BREAK(chars);
    public static PATTERN BREAKX(string chars)    => new _BREAKX(chars);
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
    const string UCASE  = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string LCASE  = "abcdefghijklmnopqrstuvwxyz";
    const string ALPHA  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    const string ALNUM  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    static void Main()
    {
        Console.WriteLine("=== SNOBOL4cs V5  —  Stage 4: ANY · NOTANY · SPAN · NSPAN · BREAK · BREAKX ===");

        Test_ANY();
        Test_NOTANY();
        Test_SPAN();
        Test_NSPAN();
        Test_BREAK();
        Test_BREAKX();
        Test_identifier();      // test_01.py identifier pattern
        Test_real_number();     // test_01.py real_number pattern
        Test_BEAD();            // V1 regression

        T.Summary();
    }

    // ── ANY ───────────────────────────────────────────────────────────────────
    // ANY(chars) matches exactly one character that appears in chars.
    static void Test_ANY()
    {
        T.Section("ANY(chars)");

        // Basic single-char match
        T.Slice  ("ANY(abc) at 'a' = [0:1]",           "abc",   ANY("abc"), 0, 1);
        T.Found  ("ANY(digits) finds digit in \"a3b\"", "a3b",  ANY(DIGITS));
        T.NotFound("ANY(digits) not in \"abc\"",        "abc",  ANY(DIGITS));

        // ANY matches exactly one char
        T.Match  ("ANY(abc) fullmatches \"a\"",          "a",    ANY("abc"));
        T.NoMatch("ANY(abc) no match \"ab\"",            "ab",   ANY("abc"));
        T.NoMatch("ANY(abc) no match \"\"",              "",     ANY("abc"));
        T.NoMatch("ANY(abc) no match \"x\"",             "x",    ANY("abc"));

        // ANY in sequence
        T.Match  ("ANY(+-)+SPAN(digits) matches \"+42\"", "+42",
                  POS(0)+ANY("+-")+SPAN(DIGITS)+RPOS(0));
        T.Match  ("ANY(+-)+SPAN(digits) matches \"-7\"",  "-7",
                  POS(0)+ANY("+-")+SPAN(DIGITS)+RPOS(0));
    }

    // ── NOTANY ────────────────────────────────────────────────────────────────
    // NOTANY(chars) matches exactly one character that does NOT appear in chars.
    static void Test_NOTANY()
    {
        T.Section("NOTANY(chars)");

        T.Match  ("NOTANY(digits) fullmatches \"a\"",    "a",    NOTANY(DIGITS));
        T.NoMatch("NOTANY(digits) no match \"3\"",       "3",    NOTANY(DIGITS));
        T.NoMatch("NOTANY(digits) no match \"\"",        "",     NOTANY(DIGITS));

        // NOTANY in sequence: one non-digit followed by digits
        T.Match  ("NOTANY(digits)+SPAN(digits) = \"a3\"","a3",
                  POS(0)+NOTANY(DIGITS)+SPAN(DIGITS)+RPOS(0));

        // Complement relationship: ANY(x)|NOTANY(x) covers all single chars
        T.Match  ("ANY(a)|NOTANY(a) matches any char \"b\"","b",
                  POS(0)+(ANY("a")|NOTANY("a"))+RPOS(0));
        T.Match  ("ANY(a)|NOTANY(a) matches char \"a\"",    "a",
                  POS(0)+(ANY("a")|NOTANY("a"))+RPOS(0));
    }

    // ── SPAN ──────────────────────────────────────────────────────────────────
    // SPAN(chars) matches one-or-more characters from chars. Fails on empty.
    static void Test_SPAN()
    {
        T.Section("SPAN(chars)");

        T.Slice  ("SPAN(digits) in \"123abc\" = [0:3]",  "123abc", SPAN(DIGITS), 0, 3);
        T.Match  ("SPAN(digits) fullmatches \"0123\"",   "0123",   SPAN(DIGITS));
        T.NoMatch("SPAN(digits) no match \"abc\"",       "abc",    SPAN(DIGITS));
        T.NoMatch("SPAN(digits) no match \"\"",          "",       SPAN(DIGITS));

        // SPAN requires at least one char — not same as NSPAN
        T.NotFound("SPAN(digits) not found in all-alpha","abcde",  SPAN(DIGITS));

        // SPAN is greedy and non-backtracking: consumes all matching chars at once
        // σ("12")+RPOS(0) after SPAN(digits) on "123" — SPAN took "123", σ("12") fails
        T.NoMatch("SPAN greedy: takes all digits, σ(12) can't match after","123",
                  POS(0)+SPAN(DIGITS)+σ("12")+RPOS(0));

        // But SPAN followed by more of the same — use in sequence
        T.Match  ("SPAN(alpha)+SPAN(digits) matches \"abc123\"","abc123",
                  POS(0)+SPAN(ALPHA)+SPAN(DIGITS)+RPOS(0));
    }

    // ── NSPAN ─────────────────────────────────────────────────────────────────
    // NSPAN(chars) matches zero-or-more chars from set. Never fails.
    static void Test_NSPAN()
    {
        T.Section("NSPAN(chars)");

        // Zero-length match when no chars match
        T.Slice  ("NSPAN(digits) on \"abc\" = [0:0]",   "abc",    NSPAN(DIGITS), 0, 0);

        // Consumes all matching chars
        T.Slice  ("NSPAN(digits) on \"123abc\" = [0:3]","123abc", NSPAN(DIGITS), 0, 3);

        // NSPAN never fails — fullmatches anything via zero-length match
        T.Match  ("NSPAN(digits) fullmatches \"\"",     "",       NSPAN(DIGITS));
        T.Match  ("NSPAN(digits) fullmatches \"123\"",  "123",    NSPAN(DIGITS));

        // Optional prefix: NSPAN(alpha) + SPAN(digits)
        T.Match  ("NSPAN(alpha)+SPAN(digits) matches \"abc123\"","abc123",
                  POS(0)+NSPAN(ALPHA)+SPAN(DIGITS)+RPOS(0));
        T.Match  ("NSPAN(alpha)+SPAN(digits) matches \"123\"","123",
                  POS(0)+NSPAN(ALPHA)+SPAN(DIGITS)+RPOS(0));

        // Difference from SPAN: NSPAN succeeds on empty, SPAN does not
        T.Found  ("NSPAN(digits) found (empty) in all-alpha","abcde", NSPAN(DIGITS));
        T.NotFound("SPAN(digits) NOT found in all-alpha",    "abcde", SPAN(DIGITS));
    }

    // ── BREAK ─────────────────────────────────────────────────────────────────
    // BREAK(chars) scans forward until a char in chars is found.
    // Fails if end-of-string is reached without finding the break char.
    // Yields the scanned prefix (may be zero-length if break char is at pos0).
    static void Test_BREAK()
    {
        T.Section("BREAK(chars)");

        // Basic: scan up to colon
        T.Slice  ("BREAK(:) in \"key:val\" = [0:3]",    "key:val", BREAK(":"), 0, 3);

        // Zero-length match: break char is right at current pos
        T.Slice  ("BREAK(:) at start of \":val\" = [0:0]",":val",  BREAK(":"), 0, 0);

        // Fail: no break char present
        T.NotFound("BREAK(:) not found in \"nocolon\"", "nocolon", BREAK(":"));

        // BREAK then consume the delimiter with ANY
        T.Match  ("BREAK(:)+ANY(:)+REM matches \"key:val\"","key:val",
                  POS(0)+BREAK(":")+ANY(":")+REM()+RPOS(0));

        // BREAK is non-backtracking: yields exactly once
        // After BREAK consumes prefix, there's no retry with shorter prefix
        T.Found  ("BREAK(.) in \"a.b.c\" finds \"a\"",  "a.b.c",
                  POS(0)+BREAK(".")+ANY(".")+σ("b"));
    }

    // ── BREAKX ────────────────────────────────────────────────────────────────
    // BREAKX is an alias for BREAK in the pure backend.
    static void Test_BREAKX()
    {
        T.Section("BREAKX(chars) — alias for BREAK");

        T.Slice  ("BREAKX(:) in \"key:val\" = [0:3]",   "key:val", BREAKX(":"), 0, 3);
        T.NotFound("BREAKX(:) not found in \"nocolon\"","nocolon", BREAKX(":"));
        T.Match  ("BREAKX(:)+ANY(:)+REM matches \"k:v\"","k:v",
                  POS(0)+BREAKX(":")+ANY(":")+REM()+RPOS(0));
    }

    // ── identifier pattern (from test_01.py) ──────────────────────────────────
    // A SNOBOL4 identifier: starts with a letter, followed by letters or digits.
    // Python test_01.py: identifier = POS(0) + ANY(UCASE+LCASE) + NSPAN(UCASE+LCASE+DIGITS) + RPOS(0)
    static void Test_identifier()
    {
        T.Section("identifier (test_01.py)");

        PATTERN identifier = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);

        string[] yes = { "a", "A", "abc", "Hello", "x1", "CamelCase", "abc123", "X99" };
        string[] no  = { "", "1abc", "_abc", "123", " abc", "abc def" };

        foreach (var s in yes) T.Match  ($"identifier \"{s}\"", s, identifier);
        foreach (var s in no)  T.NoMatch($"not identifier \"{s}\"", s, identifier);
    }

    // ── real_number pattern (from test_01.py) ─────────────────────────────────
    // A real number: optional sign, digits, optional decimal part.
    // Python test_01.py:
    //   real_number = POS(0) + ANY("+-") (optional) + SPAN(DIGITS)
    //               + (σ(".") + NSPAN(DIGITS)) (optional) + RPOS(0)
    static void Test_real_number()
    {
        T.Section("real_number (test_01.py)");

        PATTERN real_number =
              POS(0)
            + ~ANY("+-")
            + SPAN(DIGITS)
            + ~(σ(".") + NSPAN(DIGITS))
            + RPOS(0);

        string[] yes = { "0", "42", "3.14", "+7", "-3", "+3.14", "-0.5", "100.", "007" };
        string[] no  = { "", ".", "+", "abc", "1.2.3", " 3", "3 .14" };

        foreach (var s in yes) T.Match  ($"real \"{s}\"", s, real_number);
        foreach (var s in no)  T.NoMatch($"not real \"{s}\"", s, real_number);
    }

    // ── BEAD regression ───────────────────────────────────────────────────────
    static void Test_BEAD()
    {
        T.Section("BEAD regression");
        PATTERN test_one =
              POS(0)
            + Π(σ("B"),σ("F"),σ("L"),σ("R"))
            + Π(σ("E"),σ("EA"))
            + Π(σ("D"),σ("DS"))
            + RPOS(0);
        string[] yes={"BED","FED","LED","RED","BEAD","FEAD","LEAD","READ",
                      "BEDS","FEDS","LEDS","REDS","BEADS","FEADS","LEADS","READS"};
        string[] no ={"BID","BREAD","ED","BEDSS",""};
        foreach (var w in yes) T.Match  ($"BEAD \"{w}\"",w,test_one);
        foreach (var w in no)  T.NoMatch($"BEAD \"{w}\"",w,test_one);
    }
}
