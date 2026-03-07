// Primitives.cs — core SNOBOL4 pattern primitives
//
// Every pattern here implements γ() as an IEnumerable<Slice> generator.
// The generator contract:
//   • Set st.pos to the end of the match before yielding the Slice.
//   • Reset st.pos to the original position after the yield returns
//     (i.e. when MoveNext() is called again by a backtracking _Σ).
//   • Yield nothing if the pattern cannot match at the current position.
//   • Patterns that can match multiple ways (ARB, ARBNO, BAL) yield each
//     alternative in turn; the containing _Σ calls MoveNext() for the next.
//
// Trace calls are guarded by Tracer.IsXxx so there is zero overhead when
// tracing is off.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
// ── helpers ───────────────────────────────────────────────────────────────────
static class P_ {
    // Q() wraps a string in double-quotes for trace/ToString display
    public static string Q(string s) =>
        $"\"{s.Replace("\"", "\\\"").Replace("\n", "\\n")}\"";
}

// ── σ — literal string match ──────────────────────────────────────────────────
// Succeeds if the subject at the current position starts with the given string.
// The Func<string> overload evaluates the string at match time, enabling
// patterns whose literal text comes from the environment:
//   σ(() => (string)(Slot)_.keyword)
public sealed class _σ : PATTERN {
    readonly string?       _s;
    readonly Func<string>? _f;
    public _σ(string s)       { _s = s; _f = null; }
    public _σ(Func<string> f) { _f = f; _s = null; }
    string Name => $"σ({P_.Q(_f != null ? "?" : _s!)})";
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

// ── _Σ — sequence (concatenation) ────────────────────────────────────────────
// Tries each sub-pattern in order.  Uses an enumerator array indexed by a
// cursor integer so backtracking just decrements the cursor and calls
// MoveNext() again on the previous enumerator — no recursion needed.
// The PATTERN.operator+ flattens adjacent _Σ nodes into a single flat array.
public sealed class _Σ : PATTERN {
    internal readonly PATTERN[] _AP;
    public _Σ(params PATTERN[] ap) { _AP = ap; }
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
            if (Ag[c]!.MoveNext()) c++;
            else { Ag[c]!.Dispose(); Ag[c] = null; c--; }
        }
        st.depth--;
        foreach (var e in Ag) e?.Dispose();
    }
}

// ── _Π — alternation ──────────────────────────────────────────────────────────
// Tries each alternative in left-to-right order, yielding all matches from
// each before moving on.  The PATTERN.operator| flattens nested _Π nodes.
public sealed class _Π : PATTERN {
    internal readonly PATTERN[] _AP;
    public _Π(params PATTERN[] ap) { _AP = ap; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; st.depth++;
        foreach (var P in _AP) foreach (var s in P.γ()) yield return s;
        st.depth--;
    }
}

// ── _π — optional  (~P) ───────────────────────────────────────────────────────
// Yields all matches from P first, then yields a zero-length match at the
// current position.  This is P | ε written as a single class to avoid building
// a two-element _Π.
public sealed class _π : PATTERN {
    readonly PATTERN _P; public _π(PATTERN p) { _P = p; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; st.depth++;
        foreach (var s in _P.γ()) yield return s;
        yield return new Slice(st.pos, st.pos);   // the ε alternative
        st.depth--;
    }
    public override string ToString() => $"π({_P})";
}

// ── _POS / _RPOS — absolute and right-relative cursor anchors ────────────────
// POS(n)  succeeds only when the cursor is exactly at position n from the left.
// RPOS(n) succeeds only when the cursor is exactly n characters from the right.
// Both accept a Func<int> for values computed at match time.
public sealed class _POS : PATTERN {
    readonly int?       _n; readonly Func<int>? _f;
    public _POS(int n)       { _n = n; _f = null; }
    public _POS(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int n = _f != null ? _f() : _n!.Value;
        Tracer.Debug(st, $"POS({n})");
        if (st.pos == n) {
            Tracer.InfoZ(st, $"POS({n})");
            yield return new Slice(st.pos, st.pos);
            Tracer.Warn(st, $"POS({n})");
        }
    }
}

public sealed class _RPOS : PATTERN {
    readonly int?       _n; readonly Func<int>? _f;
    public _RPOS(int n)       { _n = n; _f = null; }
    public _RPOS(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int n = _f != null ? _f() : _n!.Value;
        Tracer.Debug(st, $"RPOS({n})");
        if (st.pos == st.subject.Length - n) {
            Tracer.InfoZ(st, $"RPOS({n})");
            yield return new Slice(st.pos, st.pos);
            Tracer.Warn(st, $"RPOS({n})");
        }
    }
}

// ── Trivial patterns ──────────────────────────────────────────────────────────

// ε — always succeeds with a zero-length match at the current position
public sealed class _ε : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top; yield return new Slice(s.pos, s.pos); }
    public override string ToString() => "ε()";
}

// FAIL — always fails; useful as a no-op alternative or to force backtracking
public sealed class _FAIL : PATTERN {
    public override IEnumerable<Slice> γ() { yield break; }
    public override string ToString() => "FAIL()";
}

// ABORT — throws F, terminating the entire match unconditionally.
// Unlike FAIL, ABORT cannot be recovered from by backtracking.
public sealed class _ABORT : PATTERN {
    public override IEnumerable<Slice> γ() {
        throw new F("ABORT");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
    public override string ToString() => "ABORT()";
}

// SUCCEED — yields infinite zero-length matches; forces the engine to keep
// trying all alternatives.  Rarely used in practice.
public sealed class _SUCCEED : PATTERN {
    public override IEnumerable<Slice> γ() {
        var s = Ϣ.Top; while (true) yield return new Slice(s.pos, s.pos);
    }
    public override string ToString() => "SUCCEED()";
}

// ── Line anchors ──────────────────────────────────────────────────────────────
// α — succeeds at the start of the string or immediately after a newline
// ω — succeeds at the end of the string or immediately before a newline
public sealed class _α : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        if (s.pos == 0 || s.subject[s.pos - 1] == '\n')
            yield return new Slice(s.pos, s.pos); }
    public override string ToString() => "α()";
}

public sealed class _ω : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        if (s.pos == s.subject.Length || s.subject[s.pos] == '\n')
            yield return new Slice(s.pos, s.pos); }
    public override string ToString() => "ω()";
}

// ── FENCE ─────────────────────────────────────────────────────────────────────
// FENCE()  — zero-argument form: succeeds once, then throws F on backtrack,
//            cutting all further alternatives.  Used to commit to a parse path.
// FENCE(P) — wraps P: yields all of P's matches but suppresses backtracking
//            into P from outside (P's internal backtracking still works).
public sealed class _FENCE : PATTERN {
    readonly PATTERN? _P;
    public _FENCE()           { _P = null; }
    public _FENCE(PATTERN p)  { _P = p; }
    public override IEnumerable<Slice> γ() {
        if (_P == null) {
            var s = Ϣ.Top;
            yield return new Slice(s.pos, s.pos);
            throw new F("FENCE");   // reached only on backtrack attempt
        }
        else { foreach (var s in _P.γ()) yield return s; }
    }
    public override string ToString() => _P == null ? "FENCE()" : $"FENCE({_P})";
}

// ── LEN / TAB / RTAB / REM — length and position advance ─────────────────────
// LEN(n)   — matches exactly n characters
// TAB(n)   — advances to absolute position n (from left); fails if already past
// RTAB(n)  — advances to position len-n (from right)
// REM()    — matches everything from current position to end of string
// All accept Func<int> for values computed at match time.

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
            { int p = st.pos; st.pos = n; yield return new Slice(p, n); st.pos = p; }
    }
}

public sealed class _RTAB : PATTERN {
    readonly int?       _n; readonly Func<int>? _f;
    public _RTAB(int n)       { _n = n; _f = null; }
    public _RTAB(Func<int> f) { _f = f; _n = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        int n = _f != null ? _f() : _n!.Value;
        int abs = st.subject.Length - n;
        if (n <= st.subject.Length && abs >= st.pos)
            { int p = st.pos; st.pos = abs; yield return new Slice(p, abs); st.pos = p; }
    }
}

public sealed class _REM : PATTERN {
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; int p = st.pos;
        st.pos = st.subject.Length; yield return new Slice(p, st.pos); st.pos = p; }
    public override string ToString() => "REM()";
}

// ── ARB / MARB ────────────────────────────────────────────────────────────────
// ARB — matches any string (including empty), yielding shortest first.
// Tries length 0, 1, 2, … in order; the containing _Σ takes whichever length
// allows the rest of the pattern to succeed.
// MARB is an alias (reserved for a future "maximal" variant).
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

// ── Character-class primitives ────────────────────────────────────────────────
// ANY(chars)    — matches exactly one character that is in chars
// NOTANY(chars) — matches exactly one character that is NOT in chars
// SPAN(chars)   — matches one or more characters all in chars (longest, one yield)
// NSPAN(chars)  — matches zero or more characters all in chars (always succeeds)
// BREAK(chars)  — matches zero or more characters NOT in chars, stopping before
//                 the first char that IS in chars; fails at end-of-string
// BREAKX(chars) — alias for BREAK
//
// All accept Func<string> to compute the charset at match time.
//
// The difference between SPAN and NSPAN:
//   SPAN  requires at least one character to match; fails on empty.
//   NSPAN always succeeds, yielding an empty slice if no characters match.
//   NSPAN is the idiomatic suffix on an identifier: ANY(ALPHA) + NSPAN(ALNUM)
//   matches a single letter followed by zero or more alphanumerics.

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
            { int p = st.pos; st.pos++; yield return new Slice(p, st.pos); st.pos = p; }
    }
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

public sealed class _NSPAN : PATTERN {
    readonly string? _c; readonly Func<string>? _f;
    public _NSPAN(string c)       { _c = c; _f = null; }
    public _NSPAN(Func<string> f) { _f = f; _c = null; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        var c = _f != null ? _f() : _c!; int p = st.pos;
        Tracer.Debug(st, $"NSPAN({P_.Q(c)})");
        while (st.pos < st.subject.Length && c.IndexOf(st.subject[st.pos]) >= 0) st.pos++;
        // Always yield — zero-length is a valid NSPAN result
        Tracer.Info(st, $"NSPAN({P_.Q(c)})", p, st.pos);
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
        // Succeed only if the break character was actually found (not end-of-string)
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

// ── ARBNO ─────────────────────────────────────────────────────────────────────
// Matches zero or more repetitions of P, yielding each prefix (shortest first).
// Implemented with a dynamic list of enumerators — one per repetition tried.
// The cursor advances through the list; backtracking pops the last enumerator
// and retries the previous one.  This avoids recursion and handles arbitrarily
// deep repetition without stack overflow.
// MARBNO is an alias (reserved for a future maximal variant).
public sealed class _ARBNO : PATTERN {
    readonly PATTERN _P; public _ARBNO(PATTERN p) { _P = p; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int pos0 = st.pos;
        Tracer.Debug(st, $"ARBNO({_P})");
        var Ag = new List<IEnumerator<Slice>>(); int cursor = 0;
        st.depth++;
        while (cursor >= 0) {
            if (cursor >= Ag.Count) {
                // All current repetitions matched — yield the accumulated extent
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

public sealed class _MARBNO : PATTERN {
    readonly _ARBNO _a; public _MARBNO(PATTERN p) { _a = new _ARBNO(p); }
    public override IEnumerable<Slice> γ() => _a.γ();
}

// ── BAL — balanced-parenthesis match ─────────────────────────────────────────
// Yields all prefixes of the subject starting at the current position that are
// "balanced": the same number of '(' and ')' characters, with nesting never
// going negative.  Yields shortest first (one character, two, etc.) so the
// containing pattern can use the shortest balanced prefix that allows the rest
// to succeed.
//
// Note: the subject character just before the first yield position is always
// one past the last consumed character because the loop increments pos before
// the nest==0 check.
public sealed class _BAL : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int pos0 = st.pos; int nest = 0;
        Tracer.Debug(st, "BAL()");
        st.pos++;
        while (st.pos <= st.subject.Length) {
            char ch = st.subject[st.pos - 1];
            if      (ch == '(') nest++;
            else if (ch == ')') nest--;
            if      (nest < 0)                                  break;  // unmatched close
            else if (nest > 0 && st.pos >= st.subject.Length)  break;  // unclosed open at end
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

}
