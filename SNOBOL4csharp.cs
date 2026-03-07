// SNOBOL4cs_v3.cs  —  SNOBOL4 pattern engine in C#, Version 3
//
// Change from V2: factory functions.
//
//   Before:  new POS(0) + new FENCE() + new σ("ab") + new RPOS(0)
//   After:   POS(0) + FENCE() + σ("ab") + RPOS(0)
//
// Convention:
//   Implementation classes  →  underscore prefix: _σ, _Σ, _POS, _FENCE, …
//   Public factory functions →  exact SNOBOL4 name: σ(), Σ(), POS(), FENCE(), …
//
// All factory functions live in one static class S4 and are pulled into scope
// with a top-level  using static S4;  so call sites need no qualifier.
//
// Everything else (γ protocol, Ϣ stack, Engine) is unchanged from V2.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using static S4;           // brings all factory functions into global scope

// ── F — match-failure / abort exception ──────────────────────────────────────

sealed class F : Exception
{
    public F(string msg) : base(msg) { }
}

// ── MatchState ────────────────────────────────────────────────────────────────

sealed class MatchState
{
    public int    pos;
    public string subject;
    public MatchState(int pos, string subject) { this.pos = pos; this.subject = subject; }
}

// ── Ϣ — global match-state stack ─────────────────────────────────────────────

static class Ϣ
{
    static readonly Stack<MatchState> _stack = new();
    public static void       Push(MatchState s) => _stack.Push(s);
    public static void       Pop()              => _stack.Pop();
    public static MatchState Top                => _stack.Peek();
}

// ── Slice ─────────────────────────────────────────────────────────────────────

readonly struct Slice
{
    public readonly int Start, Stop;
    public Slice(int start, int stop) { Start = start; Stop = stop; }
    public override string ToString() => $"[{Start}:{Stop}]";
}

// ── PATTERN base class ────────────────────────────────────────────────────────

abstract class PATTERN
{
    public abstract IEnumerable<Slice> γ();

    // P + Q  →  _Σ(P, Q)
    public static PATTERN operator +(PATTERN p, PATTERN q)
    {
        if (p is _Σ ps) { var ap = new PATTERN[ps._AP.Length + 1]; ps._AP.CopyTo(ap, 0); ap[ps._AP.Length] = q; return new _Σ(ap); }
        return new _Σ(p, q);
    }

    // P | Q  →  _Π(P, Q)
    public static PATTERN operator |(PATTERN p, PATTERN q)
    {
        if (p is _Π pp) { var ap = new PATTERN[pp._AP.Length + 1]; pp._AP.CopyTo(ap, 0); ap[pp._AP.Length] = q; return new _Π(ap); }
        return new _Π(p, q);
    }

    // ~P  →  π(P)
    public static PATTERN operator ~(PATTERN p) => new _π(p);
}

// ═════════════════════════════════════════════════════════════════════════════
// Implementation classes  (underscore prefix — not part of the public API)
// ═════════════════════════════════════════════════════════════════════════════

// ── _σ ───────────────────────────────────────────────────────────────────────

sealed class _σ : PATTERN
{
    readonly string _s;
    public _σ(string s) { _s = s; }

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top; int pos0 = st.pos;
        if (pos0 + _s.Length <= st.subject.Length &&
            string.CompareOrdinal(st.subject, pos0, _s, 0, _s.Length) == 0)
        {
            st.pos = pos0 + _s.Length;
            yield return new Slice(pos0, st.pos);
            st.pos = pos0;
        }
    }

    public override string ToString() => $"σ(\"{_s}\")";
}

// ── _Σ — sequence ─────────────────────────────────────────────────────────────

sealed class _Σ : PATTERN
{
    internal readonly PATTERN[] _AP;
    public _Σ(params PATTERN[] ap) { _AP = ap; }

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top; int pos0 = st.pos; int n = _AP.Length;
        var Ag = new IEnumerator<Slice>?[n];
        int cursor = 0;
        while (cursor >= 0)
        {
            if (cursor >= n) { yield return new Slice(pos0, st.pos); cursor--; continue; }
            if (Ag[cursor] == null) Ag[cursor] = _AP[cursor].γ().GetEnumerator();
            if (Ag[cursor]!.MoveNext()) cursor++;
            else { Ag[cursor]!.Dispose(); Ag[cursor] = null; cursor--; }
        }
        foreach (var e in Ag) e?.Dispose();
    }

    public override string ToString() => $"Σ(*{_AP.Length})";
}

// ── _Π — alternation ──────────────────────────────────────────────────────────

sealed class _Π : PATTERN
{
    internal readonly PATTERN[] _AP;
    public _Π(params PATTERN[] ap) { _AP = ap; }

    public override IEnumerable<Slice> γ()
    {
        foreach (var P in _AP) foreach (var s in P.γ()) yield return s;
    }

    public override string ToString() => $"Π(*{_AP.Length})";
}

// ── _POS / _RPOS ──────────────────────────────────────────────────────────────

sealed class _POS : PATTERN
{
    readonly int _n;
    public _POS(int n) { _n = n; }
    public override IEnumerable<Slice> γ()
    { var st = Ϣ.Top; if (st.pos == _n) yield return new Slice(st.pos, st.pos); }
}

sealed class _RPOS : PATTERN
{
    readonly int _n;
    public _RPOS(int n) { _n = n; }
    public override IEnumerable<Slice> γ()
    { var st = Ϣ.Top; if (st.pos == st.subject.Length - _n) yield return new Slice(st.pos, st.pos); }
}

// ── _ε ────────────────────────────────────────────────────────────────────────

sealed class _ε : PATTERN
{
    public override IEnumerable<Slice> γ()
    { var st = Ϣ.Top; yield return new Slice(st.pos, st.pos); }
    public override string ToString() => "ε()";
}

// ── _FAIL ─────────────────────────────────────────────────────────────────────

sealed class _FAIL : PATTERN
{
    public override IEnumerable<Slice> γ() { yield break; }
    public override string ToString() => "FAIL()";
}

// ── _ABORT ────────────────────────────────────────────────────────────────────

sealed class _ABORT : PATTERN
{
    public override IEnumerable<Slice> γ()
    {
        throw new F("ABORT");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
    public override string ToString() => "ABORT()";
}

// ── _SUCCEED ──────────────────────────────────────────────────────────────────

sealed class _SUCCEED : PATTERN
{
    public override IEnumerable<Slice> γ()
    { var st = Ϣ.Top; while (true) yield return new Slice(st.pos, st.pos); }
    public override string ToString() => "SUCCEED()";
}

// ── _π — optional ────────────────────────────────────────────────────────────

sealed class _π : PATTERN
{
    readonly PATTERN _P;
    public _π(PATTERN p) { _P = p; }
    public override IEnumerable<Slice> γ()
    {
        foreach (var s in _P.γ()) yield return s;
        var st = Ϣ.Top; yield return new Slice(st.pos, st.pos);
    }
    public override string ToString() => $"π({_P})";
}

// ── _α / _ω — line anchors ────────────────────────────────────────────────────

sealed class _α : PATTERN
{
    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        if (st.pos == 0 || (st.pos > 0 && st.subject[st.pos - 1] == '\n'))
            yield return new Slice(st.pos, st.pos);
    }
    public override string ToString() => "α()";
}

sealed class _ω : PATTERN
{
    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        if (st.pos == st.subject.Length ||
            (st.pos < st.subject.Length && st.subject[st.pos] == '\n'))
            yield return new Slice(st.pos, st.pos);
    }
    public override string ToString() => "ω()";
}

// ── _FENCE ────────────────────────────────────────────────────────────────────

sealed class _FENCE : PATTERN
{
    readonly PATTERN? _P;
    public _FENCE()           { _P = null; }
    public _FENCE(PATTERN p)  { _P = p; }

    public override IEnumerable<Slice> γ()
    {
        if (_P == null)
        {
            var st = Ϣ.Top;
            yield return new Slice(st.pos, st.pos);
            throw new F("FENCE");
        }
        else
        {
            foreach (var s in _P.γ()) yield return s;
        }
    }
    public override string ToString() => _P == null ? "FENCE()" : $"FENCE({_P})";
}

// ═════════════════════════════════════════════════════════════════════════════
// S4 — factory functions (the public API)
//
// Every name here matches the SNOBOL4 / Python convention exactly.
// "using static S4" at the top of the file brings all of these into scope.
// ═════════════════════════════════════════════════════════════════════════════

static class S4
{
    // Combinators
    public static PATTERN Σ(params PATTERN[] ap)  => new _Σ(ap);
    public static PATTERN Π(params PATTERN[] ap)  => new _Π(ap);
    public static PATTERN π(PATTERN p)            => new _π(p);

    // String literal
    public static PATTERN σ(string s)             => new _σ(s);

    // Position anchors
    public static PATTERN POS(int n)              => new _POS(n);
    public static PATTERN RPOS(int n)             => new _RPOS(n);

    // Trivials
    public static PATTERN ε()                     => new _ε();
    public static PATTERN FAIL()                  => new _FAIL();
    public static PATTERN ABORT()                 => new _ABORT();
    public static PATTERN SUCCEED()               => new _SUCCEED();

    // Line anchors
    public static PATTERN α()                     => new _α();
    public static PATTERN ω()                     => new _ω();

    // Fence — two overloads, one name
    public static PATTERN FENCE()                 => new _FENCE();
    public static PATTERN FENCE(PATTERN p)        => new _FENCE(p);
}

// ═════════════════════════════════════════════════════════════════════════════
// Engine
// ═════════════════════════════════════════════════════════════════════════════

static class Engine
{
    public static Slice? SEARCH(string S, PATTERN P, bool exc = false)
    {
        for (int cursor = 0; cursor <= S.Length; cursor++)
        {
            Ϣ.Push(new MatchState(cursor, S));
            bool popped = false;
            try
            {
                foreach (var slyce in P.γ())
                { Ϣ.Pop(); popped = true; return slyce; }
            }
            catch (F)
            {
                if (!popped) { Ϣ.Pop(); popped = true; }
                if (exc) throw;
                return null;
            }
            finally
            {
                if (!popped) Ϣ.Pop();
            }
        }
        if (exc) throw new F("FAIL");
        return null;
    }

    public static Slice? MATCH(string S, PATTERN P, bool exc = false)
        => SEARCH(S, POS(0) + P, exc);

    public static Slice? FULLMATCH(string S, PATTERN P, bool exc = false)
        => SEARCH(S, POS(0) + P + RPOS(0), exc);
}

// ═════════════════════════════════════════════════════════════════════════════
// Test harness
// ═════════════════════════════════════════════════════════════════════════════

static class T
{
    static int _pass, _fail;

    public static void Match   (string lbl, string s, PATTERN P) => Report(lbl, Engine.FULLMATCH(s, P) != null);
    public static void NoMatch (string lbl, string s, PATTERN P) => Report(lbl, Engine.FULLMATCH(s, P) == null);
    public static void Found   (string lbl, string s, PATTERN P) => Report(lbl, Engine.SEARCH(s, P) != null);
    public static void NotFound(string lbl, string s, PATTERN P) => Report(lbl, Engine.SEARCH(s, P) == null);

    public static void Slice(string lbl, string s, PATTERN P, int start, int stop)
    {
        var r = Engine.SEARCH(s, P);
        Report(lbl, r != null && r.Value.Start == start && r.Value.Stop == stop);
    }

    public static void Throws(string lbl, string s, PATTERN P)
    {
        bool ok = false;
        try   { Engine.SEARCH(s, P, exc: true); }
        catch (F) { ok = true; }
        Report(lbl, ok);
    }

    static void Report(string lbl, bool ok)
    {
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {lbl}");
    }

    public static void Summary() => Console.WriteLine($"\n=== {_pass} passed, {_fail} failed ===");
    public static void Section(string title) => Console.WriteLine($"\n── {title} ──");
}

// ═════════════════════════════════════════════════════════════════════════════
// Tests  —  all call sites now use factory syntax, no  new X()  anywhere
// ═════════════════════════════════════════════════════════════════════════════

class Program
{
    static void Main()
    {
        Console.WriteLine("=== SNOBOL4cs V3  —  factory functions ===");

        Test_ε();
        Test_FAIL();
        Test_ABORT();
        Test_SUCCEED();
        Test_π();
        Test_α();
        Test_ω();
        Test_FENCE_variable();
        Test_FENCE_function();
        Test_BEAD();

        T.Summary();
    }

    // ── ε ─────────────────────────────────────────────────────────────────────
    static void Test_ε()
    {
        T.Section("ε (epsilon)");
        T.Match   ("ε fullmatches \"\"",                         "",    ε());
        T.NoMatch ("ε does not fullmatch \"a\"",                 "a",   ε());
        T.Slice   ("SEARCH finds ε at [0:0] in \"hi\"",          "hi",  ε(), 0, 0);
        T.Match   ("POS(0)+ε+σ(x)+RPOS(0) matches \"x\"",       "x",   POS(0) + ε() + σ("x") + RPOS(0));
        T.NoMatch ("POS(0)+ε+σ(x)+RPOS(0) no match \"y\"",      "y",   POS(0) + ε() + σ("x") + RPOS(0));
    }

    // ── FAIL ──────────────────────────────────────────────────────────────────
    static void Test_FAIL()
    {
        T.Section("FAIL");
        T.NotFound("FAIL finds nothing in \"\"",                 "",    FAIL());
        T.NotFound("FAIL finds nothing in \"abc\"",              "abc", FAIL());
        T.Match   ("FAIL|σ(ok) matches \"ok\"",                  "ok",  POS(0) + (FAIL() | σ("ok")) + RPOS(0));
        T.NoMatch ("FAIL|σ(ok) no match \"no\"",                 "no",  POS(0) + (FAIL() | σ("ok")) + RPOS(0));
        T.NoMatch ("σ(a)+FAIL no match \"a\"",                   "a",   POS(0) + σ("a") + FAIL() + RPOS(0));
    }

    // ── ABORT ─────────────────────────────────────────────────────────────────
    static void Test_ABORT()
    {
        T.Section("ABORT");
        T.NotFound("ABORT returns null (exc=false)",             "abc", ABORT());
        T.Throws  ("ABORT raises F (exc=true) in \"abc\"",       "abc", POS(0) + ABORT());
        T.NotFound("ABORT|σ(x) aborts, no match",               "x",   ABORT() | σ("x"));
    }

    // ── SUCCEED ───────────────────────────────────────────────────────────────
    static void Test_SUCCEED()
    {
        T.Section("SUCCEED");
        T.Slice   ("SEARCH(\"hi\", SUCCEED()) = [0:0]",          "hi",  SUCCEED(), 0, 0);
        T.Match   ("SUCCEED() fullmatches \"\"",                  "",    SUCCEED());
        T.NotFound("SUCCEED+FENCE() aborts entire search",        "ab",  SUCCEED() + FENCE() + σ("b"));
    }

    // ── π (optional) ─────────────────────────────────────────────────────────
    static void Test_π()
    {
        T.Section("π (optional, ~P)");
        PATTERN opt_s = POS(0) + σ("cat") + ~σ("s") + RPOS(0);
        T.Match   ("σ(cat)+~σ(s) matches \"cats\"",              "cats", opt_s);
        T.Match   ("σ(cat)+~σ(s) matches \"cat\"",               "cat",  opt_s);
        T.NoMatch ("σ(cat)+~σ(s) no match \"ca\"",               "ca",   opt_s);
        T.Match   ("~ε fullmatches \"\"",                         "",     POS(0) + ~ε() + RPOS(0));

        PATTERN sign = ~(σ("+") | σ("-"));
        PATTERN P    = POS(0) + sign + σ("1") + RPOS(0);
        T.Match   ("~(+|-)+σ(1) matches \"+1\"",                 "+1",   P);
        T.Match   ("~(+|-)+σ(1) matches \"-1\"",                 "-1",   P);
        T.Match   ("~(+|-)+σ(1) matches \"1\"",                  "1",    P);
        T.NoMatch ("~(+|-)+σ(1) no match \"x1\"",                "x1",   P);
    }

    // ── α (BOL) ───────────────────────────────────────────────────────────────
    static void Test_α()
    {
        T.Section("α (BOL anchor)");
        T.Match   ("α+σ(hi) matches \"hi\"",                     "hi",       α() + σ("hi") + RPOS(0));
        T.NoMatch ("α+σ(hi) no match \" hi\"",                   " hi",      α() + σ("hi") + RPOS(0));
        T.Found   ("α+σ(two) found in \"one\\ntwo\"",            "one\ntwo", α() + σ("two"));
        T.NotFound("α+σ(xxx) not found in \"one\\ntwo\"",        "one\ntwo", α() + σ("xxx"));
        T.NotFound("α not in middle of line",                    "onetwo",   σ("one") + α());
    }

    // ── ω (EOL) ───────────────────────────────────────────────────────────────
    static void Test_ω()
    {
        T.Section("ω (EOL anchor)");
        T.Found   ("σ(hi)+ω found in \"hi\"",                    "hi",       σ("hi") + ω());
        T.Found   ("σ(hi)+ω found in \"hi\\nthere\"",            "hi\nthere",σ("hi") + ω());
        T.Found   ("σ(one)+ω found in \"one\\ntwo\"",            "one\ntwo", σ("one") + ω());
        T.NotFound("σ(on)+ω not found in \"one\"",               "one",      σ("on") + ω());
        T.Found   ("σ(end)+ω at true end",                       "the end",  σ("end") + ω());
    }

    // ── FENCE() — commit point ────────────────────────────────────────────────
    static void Test_FENCE_variable()
    {
        T.Section("FENCE() — commit point");
        T.Match   ("without FENCE: σ(a)|σ(ab) matches \"ab\"",   "ab",
                   POS(0) + (σ("a") | σ("ab")) + RPOS(0));
        T.NoMatch ("with FENCE: σ(a)+FENCE()|σ(ab) aborts on \"ab\"", "ab",
                   POS(0) + (σ("a") + FENCE() | σ("ab")) + RPOS(0));
        T.Match   ("σ(ok)+FENCE() matches \"ok\" cleanly",       "ok",
                   POS(0) + σ("ok") + FENCE() + RPOS(0));
    }

    // ── FENCE(P) — function form ──────────────────────────────────────────────
    static void Test_FENCE_function()
    {
        T.Section("FENCE(P) — function form");
        PATTERN P = POS(0) + FENCE(σ("a") | σ("ab")) + RPOS(0);
        T.Match   ("FENCE(σ(a)|σ(ab)) matches \"a\"",            "a",  P);
        T.Match   ("FENCE(P) still yields all of P alternatives", "ab", P);
        PATTERN Q = POS(0) + (FENCE(σ("a")) | σ("xy")) + RPOS(0);
        T.Match   ("FENCE(σ(a))|σ(xy) outer-alt still works on \"xy\"", "xy", Q);
    }

    // ── BEAD regression ───────────────────────────────────────────────────────
    static void Test_BEAD()
    {
        T.Section("BEAD regression");

        PATTERN test_one =
              POS(0)
            + Π(σ("B"), σ("F"), σ("L"), σ("R"))
            + Π(σ("E"), σ("EA"))
            + Π(σ("D"), σ("DS"))
            + RPOS(0);

        string[] yes = {
            "BED","FED","LED","RED",
            "BEAD","FEAD","LEAD","READ",
            "BEDS","FEDS","LEDS","REDS",
            "BEADS","FEADS","LEADS","READS",
        };
        string[] no = { "BID", "BREAD", "ED", "BEDSS", "" };

        foreach (var w in yes) T.Match  ($"BEAD matches \"{w}\"",    w, test_one);
        foreach (var w in no)  T.NoMatch($"BEAD no match \"{w}\"",   w, test_one);
    }
}
