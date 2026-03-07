// Primitives.cs -- SNOBOL4 pattern primitives with TRACE instrumentation
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
// ── helpers ───────────────────────────────────────────────────────────────────
// Q() — quote a string for display in trace output
static class P_ { public static string Q(string s) => $"\"{s.Replace("\"","\\\"").Replace("\n","\\n")}\""; }

public sealed class _σ : PATTERN {
    readonly string?       _s;
    readonly Func<string>? _f;
    public _σ(string s)       { _s = s; _f = null; }
    public _σ(Func<string> f) { _f = f; _s = null; }
    string Name { get { var s = _f != null ? "?" : _s!; return $"σ({P_.Q(s)})"; } }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int p = st.pos;
        var s = _f != null ? _f() : _s!;
        Tracer.Debug(st, Name);
        if (p + s.Length <= st.subject.Length &&
            string.CompareOrdinal(st.subject, p, s, 0, s.Length) == 0) {
            st.pos = p + s.Length;
            Tracer.Info(st, Name, p, st.pos);
            yield return new Slice(p, st.pos);
            Tracer.Warn(st, Name);
            st.pos = p;
        }
    }
    public override string ToString() => Name;
}

public sealed class _Σ : PATTERN {
    internal readonly PATTERN[] _AP; public _Σ(params PATTERN[] ap) { _AP = ap; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int p0 = st.pos; int n = _AP.Length;
        var Ag = new IEnumerator<Slice>?[n]; int c = 0;
        st.depth++;
        while (c >= 0) {
            if (c >= n) {
                Tracer.Info(st, $"Σ(*{n})", p0, st.pos);
                yield return new Slice(p0, st.pos);
                Tracer.Warn(st, $"Σ(*{n})");
                c--; continue;
            }
            if (Ag[c] == null) Ag[c] = _AP[c].γ().GetEnumerator();
            if (Ag[c]!.MoveNext()) c++; else { Ag[c]!.Dispose(); Ag[c] = null; c--; }
        }
        st.depth--;
        foreach (var e in Ag) e?.Dispose();
    }
}

public sealed class _Π : PATTERN {
    internal readonly PATTERN[] _AP; public _Π(params PATTERN[] ap) { _AP = ap; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; st.depth++;
        foreach (var P in _AP) foreach (var s in P.γ()) yield return s;
        st.depth--;
    }
}

public sealed class _POS : PATTERN {
    readonly int?       _n; readonly Func<int>? _f;
    public _POS(int n)       { _n = n; _f = null; }
    public _POS(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        int n = _f != null ? _f() : _n!.Value;
        Tracer.Debug(st, $"POS({n})");
        if (st.pos == n) { Tracer.InfoZ(st, $"POS({n})"); yield return new Slice(st.pos, st.pos); Tracer.Warn(st, $"POS({n})"); }
    }
}

public sealed class _RPOS : PATTERN {
    readonly int?       _n; readonly Func<int>? _f;
    public _RPOS(int n)       { _n = n; _f = null; }
    public _RPOS(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        int n = _f != null ? _f() : _n!.Value;
        Tracer.Debug(st, $"RPOS({n})");
        if (st.pos == st.subject.Length - n) { Tracer.InfoZ(st, $"RPOS({n})"); yield return new Slice(st.pos, st.pos); Tracer.Warn(st, $"RPOS({n})"); }
    }
}

public sealed class _ε : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top; yield return new Slice(s.pos, s.pos); }
    public override string ToString() => "ε()";
}

public sealed class _FAIL : PATTERN {
    public override IEnumerable<Slice> γ() { yield break; }
    public override string ToString() => "FAIL()";
}

public sealed class _ABORT : PATTERN {
    public override IEnumerable<Slice> γ() {
        throw new F("ABORT");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
    public override string ToString() => "ABORT()";
}

public sealed class _SUCCEED : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top; while (true) yield return new Slice(s.pos, s.pos); }
    public override string ToString() => "SUCCEED()";
}

public sealed class _π : PATTERN { readonly PATTERN _P; public _π(PATTERN p) { _P = p; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; st.depth++;
        foreach (var s in _P.γ()) yield return s;
        yield return new Slice(st.pos, st.pos);
        st.depth--;
    }
    public override string ToString() => $"π({_P})";
}

public sealed class _α : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        if (s.pos == 0 || (s.pos > 0 && s.subject[s.pos - 1] == '\n'))
            yield return new Slice(s.pos, s.pos); }
    public override string ToString() => "α()";
}

public sealed class _ω : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        if (s.pos == s.subject.Length || (s.pos < s.subject.Length && s.subject[s.pos] == '\n'))
            yield return new Slice(s.pos, s.pos); }
    public override string ToString() => "ω()";
}

public sealed class _FENCE : PATTERN {
    readonly PATTERN? _P; public _FENCE() { _P = null; } public _FENCE(PATTERN p) { _P = p; }
    public override IEnumerable<Slice> γ() {
        if (_P == null) { var s = Ϣ.Top; yield return new Slice(s.pos, s.pos); throw new F("FENCE"); }
        else { foreach (var s in _P.γ()) yield return s; }
    }
    public override string ToString() => _P == null ? "FENCE()" : $"FENCE({_P})";
}

public sealed class _LEN : PATTERN {
    readonly int?       _n; readonly Func<int>? _f;
    public _LEN(int n)       { _n = n; _f = null; }
    public _LEN(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        int n = _f != null ? _f() : _n!.Value;
        Tracer.Debug(st, $"LEN({n})");
        if (st.pos + n <= st.subject.Length) {
            int p = st.pos; st.pos += n;
            Tracer.Info(st, $"LEN({n})", p, st.pos);
            yield return new Slice(p, st.pos);
            Tracer.Warn(st, $"LEN({n})");
            st.pos = p;
        }
    }
}

public sealed class _TAB : PATTERN {
    readonly int?       _n; readonly Func<int>? _f;
    public _TAB(int n)       { _n = n; _f = null; }
    public _TAB(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        int n = _f != null ? _f() : _n!.Value;
        if (n <= st.subject.Length && n >= st.pos)
            { int p = st.pos; st.pos = n; yield return new Slice(p, n); st.pos = p; } }
}

public sealed class _RTAB : PATTERN {
    readonly int?       _n; readonly Func<int>? _f;
    public _RTAB(int n)       { _n = n; _f = null; }
    public _RTAB(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        int n = _f != null ? _f() : _n!.Value;
        int abs = st.subject.Length - n;
        if (n <= st.subject.Length && abs >= st.pos)
            { int p = st.pos; st.pos = abs; yield return new Slice(p, abs); st.pos = p; } }
}

public sealed class _REM : PATTERN {
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; int p = st.pos;
        st.pos = st.subject.Length; yield return new Slice(p, st.pos); st.pos = p; }
    public override string ToString() => "REM()";
}

public sealed class _ARB : PATTERN {
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; int p = st.pos;
        Tracer.Debug(st, "ARB()");
        while (st.pos <= st.subject.Length) {
            Tracer.Info(st, "ARB()", p, st.pos);
            yield return new Slice(p, st.pos);
            Tracer.Warn(st, "ARB()");
            st.pos++;
        }
        st.pos = p;
    }
    public override string ToString() => "ARB()";
}

public sealed class _MARB : PATTERN { readonly _ARB _a = new();
    public override IEnumerable<Slice> γ() => _a.γ(); }

public sealed class _ANY : PATTERN {
    readonly string? _c; readonly Func<string>? _f;
    public _ANY(string c)       { _c = c; _f = null; }
    public _ANY(Func<string> f) { _f = f; _c = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        var c = _f != null ? _f() : _c!;
        Tracer.Debug(st, $"ANY({P_.Q(c)})");
        if (st.pos < st.subject.Length && c.IndexOf(st.subject[st.pos]) >= 0) {
            int p = st.pos; st.pos++;
            Tracer.Info(st, $"ANY({P_.Q(c)})", p, st.pos);
            yield return new Slice(p, st.pos);
            Tracer.Warn(st, $"ANY({P_.Q(c)})");
            st.pos = p;
        }
    }
    public override string ToString() => $"ANY({P_.Q(_c ?? "?")})";
}

public sealed class _NOTANY : PATTERN {
    readonly string? _c; readonly Func<string>? _f;
    public _NOTANY(string c)       { _c = c; _f = null; }
    public _NOTANY(Func<string> f) { _f = f; _c = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        var c = _f != null ? _f() : _c!;
        if (st.pos < st.subject.Length && c.IndexOf(st.subject[st.pos]) < 0)
            { int p = st.pos; st.pos++; yield return new Slice(p, st.pos); st.pos = p; } }
    public override string ToString() => $"NOTANY({P_.Q(_c ?? "?")})";
}

public sealed class _SPAN : PATTERN {
    readonly string? _c; readonly Func<string>? _f;
    public _SPAN(string c)       { _c = c; _f = null; }
    public _SPAN(Func<string> f) { _f = f; _c = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        var c = _f != null ? _f() : _c!; int p = st.pos;
        Tracer.Debug(st, $"SPAN({P_.Q(c)})");
        while (st.pos < st.subject.Length && c.IndexOf(st.subject[st.pos]) >= 0) st.pos++;
        if (st.pos > p) {
            Tracer.Info(st, $"SPAN({P_.Q(c)})", p, st.pos);
            yield return new Slice(p, st.pos);
            Tracer.Warn(st, $"SPAN({P_.Q(c)})");
            st.pos = p;
        }
    }
    public override string ToString() => $"SPAN({P_.Q(_c ?? "?")})";
}

// ── NSPAN — zero-or-more from charset, always succeeds (never fails) ──────────
// Python: yields once even on zero-length match; no failure path
// "N" stands for "Null allowed" — unlike SPAN, accepts empty match
public sealed class _NSPAN : PATTERN {
    readonly string? _c; readonly Func<string>? _f;
    public _NSPAN(string c)       { _c = c; _f = null; }
    public _NSPAN(Func<string> f) { _f = f; _c = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        var c = _f != null ? _f() : _c!; int p = st.pos;
        Tracer.Debug(st, $"NSPAN({P_.Q(c)})");
        while (st.pos < st.subject.Length && c.IndexOf(st.subject[st.pos]) >= 0) st.pos++;
        Tracer.Info(st, $"NSPAN({P_.Q(c)})", p, st.pos);  // always info — never fails
        yield return new Slice(p, st.pos);
        Tracer.Warn(st, $"NSPAN({P_.Q(c)})");
        st.pos = p;
    }
    public override string ToString() => $"NSPAN({P_.Q(_c ?? "?")})";
}

public sealed class _BREAK : PATTERN {
    readonly string? _c; readonly Func<string>? _f;
    public _BREAK(string c)       { _c = c; _f = null; }
    public _BREAK(Func<string> f) { _f = f; _c = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        var c = _f != null ? _f() : _c!; int p = st.pos;
        Tracer.Debug(st, $"BREAK({P_.Q(c)})");
        while (st.pos < st.subject.Length && c.IndexOf(st.subject[st.pos]) < 0) st.pos++;
        if (st.pos < st.subject.Length) {
            Tracer.Info(st, $"BREAK({P_.Q(c)})", p, st.pos);
            yield return new Slice(p, st.pos);
            Tracer.Warn(st, $"BREAK({P_.Q(c)})");
            st.pos = p;
        }
    }
    public override string ToString() => $"BREAK({P_.Q(_c ?? "?")})";
}

public sealed class _BREAKX : PATTERN {
    readonly _BREAK _b;
    public _BREAKX(string c)       { _b = new _BREAK(c); }
    public _BREAKX(Func<string> f) { _b = new _BREAK(f); }
    public override IEnumerable<Slice> γ() => _b.γ();
    public override string ToString() => $"BREAKX(...)";
}

public sealed class _ARBNO : PATTERN { readonly PATTERN _P; public _ARBNO(PATTERN p) { _P = p; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int pos0 = st.pos;
        Tracer.Debug(st, $"ARBNO({_P})");
        var Ag = new List<IEnumerator<Slice>>(); int cursor = 0;
        st.depth++;
        while (cursor >= 0) {
            if (cursor >= Ag.Count) {
                Tracer.Info(st, $"ARBNO({_P})", pos0, st.pos);
                yield return new Slice(pos0, st.pos);
                Tracer.Warn(st, $"ARBNO({_P})");
            }
            if (cursor >= Ag.Count) Ag.Add(_P.γ().GetEnumerator());
            if (Ag[cursor].MoveNext()) cursor++;
            else { Ag[cursor].Dispose(); Ag.RemoveAt(cursor); cursor--; }
        }
        st.depth--;
        foreach (var e in Ag) e.Dispose();
    }
    public override string ToString() => $"ARBNO({_P})";
}

public sealed class _MARBNO : PATTERN { readonly _ARBNO _a; public _MARBNO(PATTERN p) { _a = new _ARBNO(p); }
    public override IEnumerable<Slice> γ() => _a.γ(); }

public sealed class _BAL : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int pos0 = st.pos; int nest = 0;
        Tracer.Debug(st, "BAL()");
        st.pos++;
        while (st.pos <= st.subject.Length) {
            char ch = st.subject[st.pos - 1];
            if (ch == '(') nest++; else if (ch == ')') nest--;
            if      (nest < 0)                                   break;
            else if (nest > 0 && st.pos >= st.subject.Length)   break;
            else if (nest == 0) {
                Tracer.Info(st, "BAL()", pos0, st.pos);
                yield return new Slice(pos0, st.pos);
                Tracer.Warn(st, "BAL()");
            }
            st.pos++;
        }
        st.pos = pos0;
    }
    public override string ToString() => "BAL()";
}

// ── Assignment patterns ───────────────────────────────────────────────────────
// δ — immediate assignment (@ operator in Python)

}
