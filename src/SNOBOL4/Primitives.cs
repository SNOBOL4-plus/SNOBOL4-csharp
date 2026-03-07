// Primitives.cs -- SNOBOL4 pattern library
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
// ════════════════════════════════════════════════════════════════════════════
// Primitive patterns (carried forward from v8, unchanged)
// ════════════════════════════════════════════════════════════════════════════

public sealed class _σ : PATTERN {
    readonly string?       _s;
    readonly Func<string>? _f;
    public _σ(string s)       { _s = s; _f = null; }
    public _σ(Func<string> f) { _f = f; _s = null; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int p = st.pos;
        var s = _f != null ? _f() : _s!;
        if (p + s.Length <= st.subject.Length &&
            string.CompareOrdinal(st.subject, p, s, 0, s.Length) == 0) {
            st.pos = p + s.Length; yield return new Slice(p, st.pos); st.pos = p;
        }
    }
}

public sealed class _Σ : PATTERN {
    internal readonly PATTERN[] _AP; public _Σ(params PATTERN[] ap) { _AP = ap; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int p0 = st.pos; int n = _AP.Length;
        var Ag = new IEnumerator<Slice>?[n]; int c = 0;
        while (c >= 0) {
            if (c >= n) { yield return new Slice(p0, st.pos); c--; continue; }
            if (Ag[c] == null) Ag[c] = _AP[c].γ().GetEnumerator();
            if (Ag[c]!.MoveNext()) c++; else { Ag[c]!.Dispose(); Ag[c] = null; c--; }
        }
        foreach (var e in Ag) e?.Dispose();
    }
}

public sealed class _Π : PATTERN {
    internal readonly PATTERN[] _AP; public _Π(params PATTERN[] ap) { _AP = ap; }
    public override IEnumerable<Slice> γ() {
        foreach (var P in _AP) foreach (var s in P.γ()) yield return s;
    }
}

public sealed class _POS : PATTERN {
    readonly int?       _n;
    readonly Func<int>? _f;
    public _POS(int n)       { _n = n; _f = null; }
    public _POS(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        int n = _f != null ? _f() : _n!.Value;
        if (s.pos == n) yield return new Slice(s.pos, s.pos); } }

public sealed class _RPOS : PATTERN {
    readonly int?       _n;
    readonly Func<int>? _f;
    public _RPOS(int n)       { _n = n; _f = null; }
    public _RPOS(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        int n = _f != null ? _f() : _n!.Value;
        if (s.pos == s.subject.Length - n) yield return new Slice(s.pos, s.pos); } }

public sealed class _ε       : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top; yield return new Slice(s.pos, s.pos); } }

public sealed class _FAIL    : PATTERN {
    public override IEnumerable<Slice> γ() { yield break; } }

public sealed class _ABORT   : PATTERN {
    public override IEnumerable<Slice> γ() {
        throw new F("ABORT");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}

public sealed class _SUCCEED : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top; while (true) yield return new Slice(s.pos, s.pos); } }

public sealed class _π : PATTERN { readonly PATTERN _P; public _π(PATTERN p) { _P = p; }
    public override IEnumerable<Slice> γ() {
        foreach (var s in _P.γ()) yield return s;
        var st = Ϣ.Top; yield return new Slice(st.pos, st.pos);
    }
}

public sealed class _α : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        if (s.pos == 0 || (s.pos > 0 && s.subject[s.pos - 1] == '\n'))
            yield return new Slice(s.pos, s.pos); } }

public sealed class _ω : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        if (s.pos == s.subject.Length || (s.pos < s.subject.Length && s.subject[s.pos] == '\n'))
            yield return new Slice(s.pos, s.pos); } }

public sealed class _FENCE : PATTERN {
    readonly PATTERN? _P; public _FENCE() { _P = null; } public _FENCE(PATTERN p) { _P = p; }
    public override IEnumerable<Slice> γ() {
        if (_P == null) { var s = Ϣ.Top; yield return new Slice(s.pos, s.pos); throw new F("FENCE"); }
        else { foreach (var s in _P.γ()) yield return s; }
    }
}

public sealed class _LEN : PATTERN {
    readonly int?       _n;
    readonly Func<int>? _f;
    public _LEN(int n)       { _n = n; _f = null; }
    public _LEN(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        int n = _f != null ? _f() : _n!.Value;
        if (st.pos + n <= st.subject.Length)
            { int p = st.pos; st.pos += n; yield return new Slice(p, st.pos); st.pos = p; } } }

public sealed class _TAB : PATTERN {
    readonly int?       _n;
    readonly Func<int>? _f;
    public _TAB(int n)       { _n = n; _f = null; }
    public _TAB(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        int n = _f != null ? _f() : _n!.Value;
        if (n <= st.subject.Length && n >= st.pos)
            { int p = st.pos; st.pos = n; yield return new Slice(p, n); st.pos = p; } } }

public sealed class _RTAB : PATTERN {
    readonly int?       _n;
    readonly Func<int>? _f;
    public _RTAB(int n)       { _n = n; _f = null; }
    public _RTAB(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        int n = _f != null ? _f() : _n!.Value;
        int abs = st.subject.Length - n;
        if (n <= st.subject.Length && abs >= st.pos)
            { int p = st.pos; st.pos = abs; yield return new Slice(p, abs); st.pos = p; } } }

public sealed class _REM : PATTERN {
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; int p = st.pos;
        st.pos = st.subject.Length; yield return new Slice(p, st.pos); st.pos = p; } }

public sealed class _ARB : PATTERN {
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; int p = st.pos;
        while (st.pos <= st.subject.Length) { yield return new Slice(p, st.pos); st.pos++; }
        st.pos = p; } }

public sealed class _MARB : PATTERN { readonly _ARB _a = new();
    public override IEnumerable<Slice> γ() => _a.γ(); }

public sealed class _ANY : PATTERN {
    readonly string? _c; readonly Func<string>? _f;
    public _ANY(string c)       { _c = c; _f = null; }
    public _ANY(Func<string> f) { _f = f; _c = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        var c = _f != null ? _f() : _c!;
        if (st.pos < st.subject.Length && c.IndexOf(st.subject[st.pos]) >= 0)
            { int p = st.pos; st.pos++; yield return new Slice(p, st.pos); st.pos = p; } } }

public sealed class _NOTANY : PATTERN {
    readonly string? _c; readonly Func<string>? _f;
    public _NOTANY(string c)       { _c = c; _f = null; }
    public _NOTANY(Func<string> f) { _f = f; _c = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        var c = _f != null ? _f() : _c!;
        if (st.pos < st.subject.Length && c.IndexOf(st.subject[st.pos]) < 0)
            { int p = st.pos; st.pos++; yield return new Slice(p, st.pos); st.pos = p; } } }

public sealed class _SPAN : PATTERN {
    readonly string? _c; readonly Func<string>? _f;
    public _SPAN(string c)       { _c = c; _f = null; }
    public _SPAN(Func<string> f) { _f = f; _c = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        var c = _f != null ? _f() : _c!; int p = st.pos;
        while (st.pos < st.subject.Length && c.IndexOf(st.subject[st.pos]) >= 0) st.pos++;
        if (st.pos > p) { yield return new Slice(p, st.pos); st.pos = p; } } }

public sealed class _NSPAN : PATTERN {
    readonly string? _c; readonly Func<string>? _f;
    public _NSPAN(string c)       { _c = c; _f = null; }
    public _NSPAN(Func<string> f) { _f = f; _c = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        var c = _f != null ? _f() : _c!; int p = st.pos;
        while (st.pos < st.subject.Length && c.IndexOf(st.subject[st.pos]) >= 0) st.pos++;
        yield return new Slice(p, st.pos); st.pos = p; } }

public sealed class _BREAK : PATTERN {
    readonly string? _c; readonly Func<string>? _f;
    public _BREAK(string c)       { _c = c; _f = null; }
    public _BREAK(Func<string> f) { _f = f; _c = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        var c = _f != null ? _f() : _c!; int p = st.pos;
        while (st.pos < st.subject.Length && c.IndexOf(st.subject[st.pos]) < 0) st.pos++;
        if (st.pos < st.subject.Length) { yield return new Slice(p, st.pos); st.pos = p; } } }

public sealed class _BREAKX : PATTERN {
    readonly _BREAK _b;
    public _BREAKX(string c)       { _b = new _BREAK(c); }
    public _BREAKX(Func<string> f) { _b = new _BREAK(f); }
    public override IEnumerable<Slice> γ() => _b.γ(); }
public sealed class _ARBNO : PATTERN { readonly PATTERN _P; public _ARBNO(PATTERN p) { _P = p; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int pos0 = st.pos;
        var Ag = new List<IEnumerator<Slice>>(); int cursor = 0;
        while (cursor >= 0) {
            if (cursor >= Ag.Count) { yield return new Slice(pos0, st.pos); }
            if (cursor >= Ag.Count) Ag.Add(_P.γ().GetEnumerator());
            if (Ag[cursor].MoveNext()) cursor++;
            else { Ag[cursor].Dispose(); Ag.RemoveAt(cursor); cursor--; }
        }
        foreach (var e in Ag) e.Dispose();
    }
}

public sealed class _MARBNO : PATTERN { readonly _ARBNO _a; public _MARBNO(PATTERN p) { _a = new _ARBNO(p); }
    public override IEnumerable<Slice> γ() => _a.γ(); }

public sealed class _BAL : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int pos0 = st.pos; int nest = 0;
        st.pos++;
        while (st.pos <= st.subject.Length) {
            char ch = st.subject[st.pos - 1];
            if (ch == '(') nest++; else if (ch == ')') nest--;
            if      (nest < 0)                                   break;
            else if (nest > 0 && st.pos >= st.subject.Length)   break;
            else if (nest == 0) yield return new Slice(pos0, st.pos);
            st.pos++;
        }
        st.pos = pos0;
    }
}

// ── Assignment patterns ───────────────────────────────────────────────────────

// δ — immediate assignment (@ operator in Python)
}
