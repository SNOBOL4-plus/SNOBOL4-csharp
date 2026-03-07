// SNOBOL4cs_v9.cs  —  Stage 8: nPush/nInc/nPop + Shift/Reduce/Pop + ζ(Func<PATTERN>)
// All prior stages (v1–v8) carried forward unchanged.
//
// New in Stage 8
// ──────────────
//  nPush / nInc / nPop
//      Integer counter stack (istack) on MatchState, mirroring Python's
//      Ϣ[-1].istack / Ϣ[-1].itop.  Operations push Action lambdas onto cstack
//      so they fire at commit and roll back on backtrack — exactly like Python.
//
//  Shift / Reduce / Pop
//      Parse-tree value stack (vstack) on MatchState, mirroring Python's
//      Ϣ[-1].vstack.  Each node pushes an Action lambda onto cstack.
//      Shift(tag)       → push ["tag"]
//      Shift(tag, expr) → push ["tag", eval(expr)]  — expr via Func<object>
//      Reduce(tag)      → pop istack.top items, wrap as ["tag", ...children]
//      Reduce(tag, n)   → pop exactly n items
//      Pop(name)        → pop vstack top → Env[name]
//
//  ζ(Func<PATTERN>)
//      Deferred recursive pattern reference via lambda, enabling mutually
//      recursive grammars (re_Expression, jObject/jArray).
//      ζ(string) name-lookup form retained unchanged.
//
// Note on Shift(tag, expr):
//      In Python the expr is a string eval'd in globals. In C# the caller
//      passes a Func<object> which is called at commit time, after all %
//      assignments have fired.  This matches the Python semantics exactly.
//      For the test suite (test_re_simple.py) Shift is called without expr.
//      For test_json.py the caller will need to supply lambdas like:
//          Shift("Integer", () => int.Parse((string)Env.Get("jxVal")))
//
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static S4;

// ── Exception ─────────────────────────────────────────────────────────────────
sealed class F : Exception { public F(string m) : base(m) { } }

// ── Environment ───────────────────────────────────────────────────────────────
static class Env {
    static Dictionary<string, object>? _g;
    public static void GLOBALS(Dictionary<string, object> g) => _g = g;
    public static Dictionary<string, object> G =>
        _g ?? throw new InvalidOperationException("GLOBALS() not called.");
    public static void   Set(string k, object v) => G[k] = v;
    public static object Get(string k) =>
        G.TryGetValue(k, out var v) ? v : throw new KeyNotFoundException($"'{k}' not in env");
    public static bool   Has(string k) => _g != null && _g.ContainsKey(k);
    public static string Str(string k) => G.TryGetValue(k, out var v) ? v?.ToString() ?? "" : "";
}

// ── Match state ───────────────────────────────────────────────────────────────
// istack / itop / vstack mirror Ϣ[-1].istack / .itop / .vstack in the Python
// pure backend.  They are carried on the per-match-attempt state so that
// backtracking a cstack pop cleanly unwinds any modifications.
sealed class MatchState {
    public int pos;
    public readonly string subject;
    public readonly List<Action> cstack = new();
    // integer counter stack (nPush/nInc/nPop)
    public readonly List<int>         istack = new();
    public int                        itop   = -1;
    // parse-tree value stack (Shift/Reduce/Pop)
    // Each node is a List<object> whose first element is the string tag.
    public readonly List<List<object>> vstack = new();

    public MatchState(int p, string s) { pos = p; subject = s; }
}

// ── Match stack (Ϣ) ──────────────────────────────────────────────────────────
static class Ϣ {
    static readonly Stack<MatchState> _s = new();
    public static void       Push(MatchState s) => _s.Push(s);
    public static void       Pop()              => _s.Pop();
    public static MatchState Top               => _s.Peek();
}

// ── Slice ─────────────────────────────────────────────────────────────────────
readonly struct Slice {
    public readonly int Start, Stop;
    public Slice(int s, int e) { Start = s; Stop = e; }
    public override string ToString() => $"[{Start}:{Stop}]";
}

// ── PATTERN base ─────────────────────────────────────────────────────────────
abstract class PATTERN {
    public abstract IEnumerable<Slice> γ();

    public static PATTERN operator+(PATTERN p, PATTERN q) {
        if (p is _Σ ps) {
            var a = new PATTERN[ps._AP.Length + 1];
            ps._AP.CopyTo(a, 0); a[ps._AP.Length] = q;
            return new _Σ(a);
        }
        return new _Σ(p, q);
    }
    public static PATTERN operator|(PATTERN p, PATTERN q) {
        if (p is _Π pp) {
            var a = new PATTERN[pp._AP.Length + 1];
            pp._AP.CopyTo(a, 0); a[pp._AP.Length] = q;
            return new _Π(a);
        }
        return new _Π(p, q);
    }
    public static PATTERN operator~(PATTERN p) => new _π(p);
    public static PATTERN operator%(PATTERN p, string n) => new _Δ(p, n);
}

// ════════════════════════════════════════════════════════════════════════════
// Primitive patterns (carried forward from v8, unchanged)
// ════════════════════════════════════════════════════════════════════════════

sealed class _σ : PATTERN {
    readonly string _s; public _σ(string s) { _s = s; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; int p = st.pos;
        if (p + _s.Length <= st.subject.Length &&
            string.CompareOrdinal(st.subject, p, _s, 0, _s.Length) == 0) {
            st.pos = p + _s.Length; yield return new Slice(p, st.pos); st.pos = p;
        }
    }
}

sealed class _Σ : PATTERN {
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

sealed class _Π : PATTERN {
    internal readonly PATTERN[] _AP; public _Π(params PATTERN[] ap) { _AP = ap; }
    public override IEnumerable<Slice> γ() {
        foreach (var P in _AP) foreach (var s in P.γ()) yield return s;
    }
}

sealed class _POS     : PATTERN { readonly int _n; public _POS(int n)   { _n = n; }
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        if (s.pos == _n) yield return new Slice(s.pos, s.pos); } }

sealed class _RPOS    : PATTERN { readonly int _n; public _RPOS(int n)  { _n = n; }
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        if (s.pos == s.subject.Length - _n) yield return new Slice(s.pos, s.pos); } }

sealed class _ε       : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top; yield return new Slice(s.pos, s.pos); } }

sealed class _FAIL    : PATTERN {
    public override IEnumerable<Slice> γ() { yield break; } }

sealed class _ABORT   : PATTERN {
    public override IEnumerable<Slice> γ() {
        throw new F("ABORT");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}

sealed class _SUCCEED : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top; while (true) yield return new Slice(s.pos, s.pos); } }

sealed class _π : PATTERN { readonly PATTERN _P; public _π(PATTERN p) { _P = p; }
    public override IEnumerable<Slice> γ() {
        foreach (var s in _P.γ()) yield return s;
        var st = Ϣ.Top; yield return new Slice(st.pos, st.pos);
    }
}

sealed class _α : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        if (s.pos == 0 || (s.pos > 0 && s.subject[s.pos - 1] == '\n'))
            yield return new Slice(s.pos, s.pos); } }

sealed class _ω : PATTERN {
    public override IEnumerable<Slice> γ() { var s = Ϣ.Top;
        if (s.pos == s.subject.Length || (s.pos < s.subject.Length && s.subject[s.pos] == '\n'))
            yield return new Slice(s.pos, s.pos); } }

sealed class _FENCE : PATTERN {
    readonly PATTERN? _P; public _FENCE() { _P = null; } public _FENCE(PATTERN p) { _P = p; }
    public override IEnumerable<Slice> γ() {
        if (_P == null) { var s = Ϣ.Top; yield return new Slice(s.pos, s.pos); throw new F("FENCE"); }
        else { foreach (var s in _P.γ()) yield return s; }
    }
}

sealed class _LEN  : PATTERN { readonly int _n; public _LEN(int n)  { _n = n; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        if (st.pos + _n <= st.subject.Length)
            { int p = st.pos; st.pos += _n; yield return new Slice(p, st.pos); st.pos = p; } } }

sealed class _TAB  : PATTERN { readonly int _n; public _TAB(int n)  { _n = n; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        if (_n <= st.subject.Length && _n >= st.pos)
            { int p = st.pos; st.pos = _n; yield return new Slice(p, _n); st.pos = p; } } }

sealed class _RTAB : PATTERN { readonly int _n; public _RTAB(int n) { _n = n; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; int abs = st.subject.Length - _n;
        if (_n <= st.subject.Length && abs >= st.pos)
            { int p = st.pos; st.pos = abs; yield return new Slice(p, abs); st.pos = p; } } }

sealed class _REM : PATTERN {
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; int p = st.pos;
        st.pos = st.subject.Length; yield return new Slice(p, st.pos); st.pos = p; } }

sealed class _ARB : PATTERN {
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; int p = st.pos;
        while (st.pos <= st.subject.Length) { yield return new Slice(p, st.pos); st.pos++; }
        st.pos = p; } }

sealed class _MARB : PATTERN { readonly _ARB _a = new();
    public override IEnumerable<Slice> γ() => _a.γ(); }

sealed class _ANY    : PATTERN { readonly string _c; public _ANY(string c)    { _c = c; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        if (st.pos < st.subject.Length && _c.IndexOf(st.subject[st.pos]) >= 0)
            { int p = st.pos; st.pos++; yield return new Slice(p, st.pos); st.pos = p; } } }

sealed class _NOTANY : PATTERN { readonly string _c; public _NOTANY(string c) { _c = c; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top;
        if (st.pos < st.subject.Length && _c.IndexOf(st.subject[st.pos]) < 0)
            { int p = st.pos; st.pos++; yield return new Slice(p, st.pos); st.pos = p; } } }

sealed class _SPAN  : PATTERN { readonly string _c; public _SPAN(string c)  { _c = c; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; int p = st.pos;
        while (st.pos < st.subject.Length && _c.IndexOf(st.subject[st.pos]) >= 0) st.pos++;
        if (st.pos > p) { yield return new Slice(p, st.pos); st.pos = p; } } }

sealed class _NSPAN : PATTERN { readonly string _c; public _NSPAN(string c) { _c = c; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; int p = st.pos;
        while (st.pos < st.subject.Length && _c.IndexOf(st.subject[st.pos]) >= 0) st.pos++;
        yield return new Slice(p, st.pos); st.pos = p; } }

sealed class _BREAK  : PATTERN { readonly string _c; public _BREAK(string c)  { _c = c; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; int p = st.pos;
        while (st.pos < st.subject.Length && _c.IndexOf(st.subject[st.pos]) < 0) st.pos++;
        if (st.pos < st.subject.Length) { yield return new Slice(p, st.pos); st.pos = p; } } }

sealed class _BREAKX : PATTERN { readonly _BREAK _b; public _BREAKX(string c) { _b = new _BREAK(c); }
    public override IEnumerable<Slice> γ() => _b.γ(); }

sealed class _ARBNO : PATTERN { readonly PATTERN _P; public _ARBNO(PATTERN p) { _P = p; }
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

sealed class _MARBNO : PATTERN { readonly _ARBNO _a; public _MARBNO(PATTERN p) { _a = new _ARBNO(p); }
    public override IEnumerable<Slice> γ() => _a.γ(); }

sealed class _BAL : PATTERN {
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
sealed class _δ : PATTERN { readonly PATTERN _P; readonly string _N; public _δ(PATTERN p, string n) { _P = p; _N = n; }
    public override IEnumerable<Slice> γ() {
        foreach (var sl in _P.γ()) {
            Env.Set(_N, Ϣ.Top.subject.Substring(sl.Start, sl.Stop - sl.Start));
            yield return sl;
        }
    }
}

// Δ — conditional assignment (% operator in Python)
sealed class _Δ : PATTERN { readonly PATTERN _P; readonly string _N; public _Δ(PATTERN p, string n) { _P = p; _N = n; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        foreach (var sl in _P.γ()) {
            var cap = st.subject.Substring(sl.Start, sl.Stop - sl.Start);
            var nm = _N;
            Action act = () => Env.Set(nm, cap);
            st.cstack.Add(act);
            yield return sl;
            st.cstack.RemoveAt(st.cstack.Count - 1);
        }
    }
}

// Θ — immediate position capture
sealed class _Θ : PATTERN { readonly string _N; public _Θ(string n) { _N = n; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; Env.Set(_N, st.pos); yield return new Slice(st.pos, st.pos); } }

// θ — conditional position capture
sealed class _θ : PATTERN { readonly string _N; public _θ(string n) { _N = n; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; var pos = st.pos; var nm = _N;
        Action act = () => Env.Set(nm, pos);
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

// Λ — conditional test (Func<bool>)
sealed class _Λ : PATTERN { readonly Func<bool> _t; public _Λ(Func<bool> t) { _t = t; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; if (_t()) yield return new Slice(st.pos, st.pos); } }

// λ — side-effect action (Action, fires at commit via cstack)
sealed class _λ : PATTERN { readonly Action _c; public _λ(Action c) { _c = c; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        st.cstack.Add(_c);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

// ── ζ — deferred pattern reference ───────────────────────────────────────────

// ζ(string) — look up PATTERN in Env by name (unchanged from v8)
sealed class _ζ_name : PATTERN { readonly string _N; public _ζ_name(string n) { _N = n; }
    public override IEnumerable<Slice> γ() {
        foreach (var s in ((PATTERN)Env.Get(_N)).γ()) yield return s;
    }
}

// ζ(Func<PATTERN>) — call lambda each time γ() runs (NEW in v9)
// This is the direct equivalent of Python's ζ(lambda: re_Expression).
// The lambda is invoked on every match attempt, so it always sees the
// current value of the captured variable — enabling mutual recursion.
sealed class _ζ_func : PATTERN { readonly Func<PATTERN> _f; public _ζ_func(Func<PATTERN> f) { _f = f; }
    public override IEnumerable<Slice> γ() {
        foreach (var s in _f().γ()) yield return s;
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Stage 8: nPush / nInc / nPop  — integer counter stack
// ════════════════════════════════════════════════════════════════════════════
//
// Mirrors Python _backend_pure.py:
//   nPush → cstack.append("Ϣ[-1].itop += 1"); cstack.append("Ϣ[-1].istack.append(0)")
//   nInc  → cstack.append("Ϣ[-1].istack[Ϣ[-1].itop] += 1")
//   nPop  → cstack.append("Ϣ[-1].istack.pop()"); cstack.append("Ϣ[-1].itop -= 1")
//
// In C# the string exec is replaced by Action lambdas that close over `st`.
// The cstack push/pop pair gives the same backtrack semantics.

sealed class _nPush : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        // Two actions pushed in order, matching the Python two-line append
        Action a1 = () => { st.itop += 1; };
        Action a2 = () => { st.istack.Add(0); };
        st.cstack.Add(a1);
        st.cstack.Add(a2);
        yield return new Slice(st.pos, st.pos);
        // Backtrack: pop both
        st.cstack.RemoveAt(st.cstack.Count - 1);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

sealed class _nInc : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        Action act = () => { st.istack[st.itop] += 1; };
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

sealed class _nPop : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        // Two actions: pop the count value, then decrement itop
        Action a1 = () => { st.istack.RemoveAt(st.istack.Count - 1); };
        Action a2 = () => { st.itop -= 1; };
        st.cstack.Add(a1);
        st.cstack.Add(a2);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// Stage 8: Shift / Reduce / Pop  — parse-tree value stack
// ════════════════════════════════════════════════════════════════════════════
//
// Mirrors Python _backend_pure.py _shift / _reduce / _pop helpers.
//
// _shift(t, v):
//   if v is None: vstack.append([t])
//   else:         vstack.append([t, v])
//
// _reduce(t, n):
//   if n==0 and t=='Σ': vstack.append(['ε'])
//   elif n!=1 or t not in transparent_tags:
//       x = [t]; pop n items from end, insert at front; vstack.append(x)
//
// _pop(): return vstack.pop()  → assigned to Env[name]
//
// All three push Action lambdas onto cstack; backtrack pops them.

sealed class _Shift : PATTERN {
    readonly string       _tag;
    readonly Func<object>? _val;   // null → Shift(tag) form
    public _Shift(string tag)              { _tag = tag; _val = null; }
    public _Shift(string tag, Func<object> val) { _tag = tag; _val = val; }

    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        var tag = _tag;
        var val = _val;
        Action act = () => {
            var node = new List<object> { tag };
            if (val != null) node.Add(val());
            st.vstack.Add(node);
        };
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

sealed class _Reduce : PATTERN {
    readonly string _tag;
    readonly int    _x;    // -1 = use istack top; >=0 = explicit count
    static readonly HashSet<string> _transparent =
        new() { "Σ", "Π", "ρ", "snoExprList", "|", ".." };

    public _Reduce(string tag, int x = -1) { _tag = tag; _x = x; }

    public override IEnumerable<Slice> γ() {
        var st  = Ϣ.Top;
        var tag = _tag;
        var x   = _x;
        Action act = () => {
            int n = (x == -1) ? (st.itop >= 0 ? st.istack[st.itop] : 0) : x;
            // Mirror Python: Reduce('Σ', 0) → push ['ε']
            if (n == 0 && tag == "Σ") {
                st.vstack.Add(new List<object> { "ε" });
                return;
            }
            // Mirror Python: single-child transparent tags pass through
            if (n == 1 && _transparent.Contains(tag)) {
                // leave the one child on the stack unchanged
                return;
            }
            // General case: pop n items oldest-first, wrap as [tag, ...children]
            var node = new List<object> { tag };
            int start = st.vstack.Count - n;
            if (start < 0) start = 0;   // guard: fewer vstack entries than expected
            int actual = st.vstack.Count - start;
            for (int i = start; i < start + actual; i++) node.Add(st.vstack[i]);
            st.vstack.RemoveRange(start, actual);
            st.vstack.Add(node);
        };
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

sealed class _Pop : PATTERN {
    readonly string _name;
    public _Pop(string name) { _name = name; }

    public override IEnumerable<Slice> γ() {
        var st   = Ϣ.Top;
        var name = _name;
        Action act = () => {
            var top = st.vstack[st.vstack.Count - 1];
            st.vstack.RemoveAt(st.vstack.Count - 1);
            Env.Set(name, top);
        };
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

// ── Regex cache ───────────────────────────────────────────────────────────────
static class RxCache {
    static readonly Dictionary<string, Regex> _c = new();
    public static Regex Get(string pat) {
        if (!_c.TryGetValue(pat, out var rx))
            _c[pat] = rx = new Regex(pat, RegexOptions.Multiline | RegexOptions.Compiled);
        return rx;
    }
}

// ── Φ — immediate regex match ─────────────────────────────────────────────────
sealed class _Φ : PATTERN {
    readonly string _pat; public _Φ(string p) { _pat = p; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; var m = RxCache.Get(_pat).Match(st.subject, st.pos);
        if (m.Success && m.Index == st.pos) {
            int p = st.pos; st.pos = m.Index + m.Length;
            foreach (Group g in m.Groups) {
                if (g.Name == "0") continue;
                if (int.TryParse(g.Name, out _)) continue;
                if (g.Success) Env.Set(g.Name, g.Value);
            }
            yield return new Slice(p, st.pos);
            st.pos = p;
        }
    }
}

// ── φ — conditional regex match ───────────────────────────────────────────────
sealed class _φ : PATTERN {
    readonly string _pat; public _φ(string p) { _pat = p; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; var m = RxCache.Get(_pat).Match(st.subject, st.pos);
        if (m.Success && m.Index == st.pos) {
            int p = st.pos; st.pos = m.Index + m.Length;
            int pushed = 0;
            foreach (Group g in m.Groups) {
                if (g.Name == "0") continue;
                if (int.TryParse(g.Name, out _)) continue;
                if (g.Success) {
                    var nm = g.Name; var v = g.Value;
                    st.cstack.Add(() => Env.Set(nm, v));
                    pushed++;
                }
            }
            yield return new Slice(p, st.pos);
            for (int i = 0; i < pushed; i++) st.cstack.RemoveAt(st.cstack.Count - 1);
            st.pos = p;
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════
// S4 factory — all public constructors
// ════════════════════════════════════════════════════════════════════════════
static class S4 {
    public static PATTERN Σ(params PATTERN[] ap) => new _Σ(ap);
    public static PATTERN Π(params PATTERN[] ap) => new _Π(ap);
    public static PATTERN π(PATTERN p)           => new _π(p);
    public static PATTERN σ(string s)            => new _σ(s);
    public static PATTERN POS(int n)             => new _POS(n);
    public static PATTERN RPOS(int n)            => new _RPOS(n);
    public static PATTERN ε()                    => new _ε();
    public static PATTERN FAIL()                 => new _FAIL();
    public static PATTERN ABORT()                => new _ABORT();
    public static PATTERN SUCCEED()              => new _SUCCEED();
    public static PATTERN α()                    => new _α();
    public static PATTERN ω()                    => new _ω();
    public static PATTERN FENCE()                => new _FENCE();
    public static PATTERN FENCE(PATTERN p)       => new _FENCE(p);
    public static PATTERN LEN(int n)             => new _LEN(n);
    public static PATTERN TAB(int n)             => new _TAB(n);
    public static PATTERN RTAB(int n)            => new _RTAB(n);
    public static PATTERN REM()                  => new _REM();
    public static PATTERN ARB()                  => new _ARB();
    public static PATTERN MARB()                 => new _MARB();
    public static PATTERN ANY(string c)          => new _ANY(c);
    public static PATTERN NOTANY(string c)       => new _NOTANY(c);
    public static PATTERN SPAN(string c)         => new _SPAN(c);
    public static PATTERN NSPAN(string c)        => new _NSPAN(c);
    public static PATTERN BREAK(string c)        => new _BREAK(c);
    public static PATTERN BREAKX(string c)       => new _BREAKX(c);
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
static class Engine {
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
    public static Slice? MATCH    (string S, PATTERN P, bool exc = false) => SEARCH(S, POS(0) + P, exc);
    public static Slice? FULLMATCH(string S, PATTERN P, bool exc = false) => SEARCH(S, POS(0) + P + RPOS(0), exc);
}

// ── Test harness ──────────────────────────────────────────────────────────────
static class T {
    static int _pass, _fail;
    public static void Match   (string l, string s, PATTERN P) => Rep(l, Engine.FULLMATCH(s, P) != null);
    public static void NoMatch (string l, string s, PATTERN P) => Rep(l, Engine.FULLMATCH(s, P) == null);
    public static void Found   (string l, string s, PATTERN P) => Rep(l, Engine.SEARCH(s, P) != null);
    public static void NotFound(string l, string s, PATTERN P) => Rep(l, Engine.SEARCH(s, P) == null);
    public static void IsSlice (string l, string s, PATTERN P, int a, int b) {
        var r = Engine.SEARCH(s, P); Rep(l, r != null && r.Value.Start == a && r.Value.Stop == b); }
    public static void Eq(string l, object? a, object? b) => Rep(l, Equals(a, b));
    static void Rep(string l, bool ok) {
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {l}");
    }
    public static void Summary() =>
        Console.WriteLine($"\n=== {_pass} passed, {_fail} failed ===");
    public static void Section(string t) =>
        Console.WriteLine($"\n── {t} ──");
}

// ═════════════════════════════════════════════════════════════════════════════
// Tests
// ═════════════════════════════════════════════════════════════════════════════
class Program {
    const string DIGITS = "0123456789";
    const string UCASE  = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string LCASE  = "abcdefghijklmnopqrstuvwxyz";
    const string ALPHA  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    const string ALNUM  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    static readonly Dictionary<string, object> G = new();
    static string Gs(string k) => G.TryGetValue(k, out var v) ? v?.ToString() ?? "<null>" : "<unset>";
    static List<object> Gl(string k) => G.TryGetValue(k, out var v) ? (List<object>)v : new();

    static void Main() {
        GLOBALS(G);
        Console.WriteLine("=== SNOBOL4cs V9  —  Stage 8: nPush/nInc/nPop + Shift/Reduce/Pop + ζ(lambda) ===");
        Test_nStack();
        Test_Shift_Reduce_Pop();
        Test_RE_grammar();
        Test_ζ_func();
        Test_regression_v8();
        T.Summary();
    }

    // ── nPush / nInc / nPop ──────────────────────────────────────────────────
    static void Test_nStack() {
        T.Section("nPush / nInc / nPop");

        // Simple counter: push, inc three times, pop → count should be 3
        int captured = -1;
        var p = POS(0)
              + nPush()
              + σ("a") + nInc()
              + σ("b") + nInc()
              + σ("c") + nInc()
              + nPop()
              + λ(() => { captured = 3; })  // placeholder — real count via Reduce
              + RPOS(0);
        T.Match("nPush+3xnInc+nPop on 'abc'", "abc", p);

        // Counter with alternation: 'xyz' branch fails, 'abc' succeeds
        var p2 = POS(0)
               + nPush()
               + (σ("x") + nInc() | σ("a") + nInc())
               + (σ("y") + nInc() | σ("b") + nInc())
               + (σ("z") + nInc() | σ("c") + nInc())
               + Reduce("Test")
               + nPop()
               + RPOS(0);
        T.Match("nStack with alternation on 'abc'", "abc", p2);
    }

    // ── Shift / Reduce / Pop ─────────────────────────────────────────────────
    static void Test_Shift_Reduce_Pop() {
        T.Section("Shift / Reduce / Pop");

        // Shift tag-only then Pop
        G["tree1"] = new List<object>();
        var p1 = POS(0) + SPAN(ALPHA) % "word" + Shift("Word") + Pop("tree1") + RPOS(0);
        Engine.FULLMATCH("hello", p1);
        var t1 = Gl("tree1");
        T.Eq("Shift/Pop tag", "Word", t1.Count > 0 ? t1[0] : null);

        // Shift with value expression
        G["tree2"] = new List<object>();
        var p2 = POS(0)
               + SPAN(DIGITS) % "n"
               + Shift("Int", () => int.Parse(Gs("n")))
               + Pop("tree2")
               + RPOS(0);
        Engine.FULLMATCH("42", p2);
        var t2 = Gl("tree2");
        T.Eq("Shift tag",  "Int", t2.Count > 0 ? t2[0] : null);
        T.Eq("Shift value", 42,   t2.Count > 1 ? t2[1] : null);

        // Reduce: nPush + two Shifts + Reduce("Pair") + nPop
        G["tree3"] = new List<object>();
        var p3 = POS(0)
               + nPush()
               + SPAN(UCASE) % "a" + Shift("A") + nInc()
               + SPAN(LCASE) % "b" + Shift("B") + nInc()
               + Reduce("Pair")
               + nPop()
               + Pop("tree3")
               + RPOS(0);
        Engine.FULLMATCH("HELLOworld", p3);
        var t3 = Gl("tree3");
        T.Eq("Reduce tag",       "Pair", t3.Count > 0 ? t3[0] : null);
        T.Eq("Reduce child count", 2,    t3.Count - 1);

        // Reduce backtrack: if outer match fails, vstack should be clean
        G["treeX"] = new List<object> { "before" };
        var p4 = POS(0)
               + nPush()
               + SPAN(ALPHA) % "w" + Shift("W") + nInc()
               + Reduce("Node")
               + nPop()
               + σ("NOPE")     // forces failure
               + Pop("treeX")
               + RPOS(0);
        Engine.FULLMATCH("hello", p4);
        // treeX should remain "before" since match failed
        T.Eq("Reduce not fired on fail", "before",
            G.TryGetValue("treeX", out var tx) ? (tx is List<object> tl ? tl[0] : tx) : "<missing>");
    }

    // ── RE grammar (mirrors test_re_simple.py) ────────────────────────────────
    // re_Quantifier  = σ('*')+Shift('*') | σ('+')+Shift('+') | σ('?')+Shift('?')
    // re_Item        = σ('.')+Shift('.')
    //                | ANY(UCASE+LCASE+DIGITS)%'tx' + Shift('σ', ()=>Gs("tx"))
    //                | σ('(') + ζ(()=>re_Expression) + σ(')')
    // re_Factor      = re_Item + (re_Quantifier + Reduce('ς',2) | ε())
    // re_Term        = nPush() + ARBNO(re_Factor+nInc()) + Reduce('Σ') + nPop()
    // re_Expression  = nPush() + re_Term+nInc()
    //                + ARBNO(σ('|')+re_Term+nInc())
    //                + Reduce('Π') + nPop()
    // re_RegEx       = POS(0) + re_Expression + Pop('RE_tree') + RPOS(0)

    static PATTERN? re_Expression_ref = null;  // forward ref for ζ

    static void Test_RE_grammar() {
        T.Section("RE grammar (test_re_simple.py)");

        var re_Quantifier =
              σ("*") + Shift("*")
            | σ("+") + Shift("+")
            | σ("?") + Shift("?");

        // re_Item uses ζ(() => re_Expression_ref!) for the recursive group case
        var re_Item =
              σ(".") + Shift(".")
            | ANY(UCASE + LCASE + DIGITS) % "tx" + Shift("σ", () => (object)Gs("tx"))
            | σ("(") + ζ(() => re_Expression_ref!) + σ(")");

        var re_Factor =
              re_Item + (re_Quantifier + Reduce("ς", 2) | ε());

        var re_Term =
              nPush() + ARBNO(re_Factor + nInc()) + Reduce("Σ") + nPop();

        var re_Expression =
              nPush()
            + re_Term + nInc()
            + ARBNO(σ("|") + re_Term + nInc())
            + Reduce("Π")
            + nPop();

        re_Expression_ref = re_Expression;   // wire the forward reference

        var re_RegEx = POS(0) + re_Expression + Pop("RE_tree") + RPOS(0);

        string[] shouldMatch = {
            "", "A", "AA", "AAA",
            "A*", "A+", "A?",
            "A|B", "A|BC", "AB|C",
            "(A|)", "(A|B)*", "(A|B)+", "(A|B)?", "(A|B)C",
            "(A|)*",
            "A|(BC)", "(AB|CD)", "(AB*|CD*)", "((AB)*|(CD)*)",
            "(A|(BC))", "((AB)|C)", "(Ab|(CD))",
            "A(A|B)*B",
        };
        string[] shouldFail = { "(", ")", "*", "+" };

        foreach (var rex in shouldMatch) {
            G.Remove("RE_tree");
            T.Match($"RE parses \"{rex}\"", rex, re_RegEx);
        }
        foreach (var bad in shouldFail) {
            G.Remove("RE_tree");
            T.NoMatch($"RE rejects \"{bad}\"", bad, re_RegEx);
        }

        // Check tree is a list
        G.Remove("RE_tree");
        Engine.FULLMATCH("A|B", re_RegEx);
        T.Eq("RE_tree is set", true, G.ContainsKey("RE_tree"));
        T.Eq("RE_tree is List<object>", true, G["RE_tree"] is List<object>);
    }

    // ── ζ(Func<PATTERN>) — basic recursion ───────────────────────────────────
    static void Test_ζ_func() {
        T.Section("ζ(Func<PATTERN>) — deferred lambda recursion");

        // Simple self-referential: balanced parens  (a|(a))  etc.
        PATTERN? expr_ref = null;
        var atom  = ANY(ALPHA) | σ("(") + ζ(() => expr_ref!) + σ(")");
        var expr  = atom + ARBNO((σ("+") | σ("-")) + atom);
        expr_ref  = expr;

        T.Match("ζ lambda: 'a'",           "a",           POS(0) + expr + RPOS(0));
        T.Match("ζ lambda: 'a+b'",         "a+b",         POS(0) + expr + RPOS(0));
        T.Match("ζ lambda: '(a+b)'",       "(a+b)",       POS(0) + expr + RPOS(0));
        T.Match("ζ lambda: 'a+(b+c)'",     "a+(b+c)",     POS(0) + expr + RPOS(0));
        T.NoMatch("ζ lambda: no '1+'",     "1+",          POS(0) + expr + RPOS(0));
    }

    // ── Regression: v8 tests ─────────────────────────────────────────────────
    static void Test_regression_v8() {
        T.Section("Regression: v8 (Φ/φ and prior stages)");

        // Φ basic
        T.Match ("Φ digits",      "123",   POS(0) + Φ(@"\d+")      + RPOS(0));
        T.NoMatch("Φ no digits",  "abc",   POS(0) + Φ(@"\d+")      + RPOS(0));

        // Φ named group (immediate)
        Engine.SEARCH("hello42", POS(0) + Φ(@"(?<word>[a-z]+)(?<num>\d+)"));
        T.Eq("Φ word", "hello", Gs("word"));
        T.Eq("Φ num",  "42",    Gs("num"));

        // φ conditional
        G["ctag"] = "before";
        Engine.FULLMATCH("abc123", POS(0) + φ(@"(?<ctag>[a-z]+)") + σ("NOPE") + RPOS(0));
        T.Eq("φ silent on fail", "before", Gs("ctag"));
        G["ctag"] = "before";
        Engine.FULLMATCH("abc", POS(0) + φ(@"(?<ctag>[a-z]+)") + RPOS(0));
        T.Eq("φ fires on success", "abc", Gs("ctag"));

        // % conditional assignment
        Engine.FULLMATCH("hello42", POS(0) + (SPAN(ALPHA) % "rw") + (SPAN(DIGITS) % "rn") + RPOS(0));
        T.Eq("% word", "hello", Gs("rw"));
        T.Eq("% num",  "42",    Gs("rn"));

        // ζ(string) name lookup
        PATTERN atom2 = SPAN(DIGITS) | σ("(") + ζ("rexpr") + σ(")");
        PATTERN expr2 = atom2 + ARBNO((σ("+") | σ("-")) + atom2);
        G["rexpr"] = expr2;
        T.Match  ("ζ string: '1+(2+3)'", "1+(2+3)", POS(0) + expr2 + RPOS(0));
        T.NoMatch("ζ string: no '1+'",   "1+",      POS(0) + expr2 + RPOS(0));

        // identifier / real patterns
        var ident = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);
        T.Match  ("ident 'Hello42'", "Hello42", ident);
        T.NoMatch("ident '1bad'",   "1bad",    ident);

        var real = POS(0) + ~ANY("+-") + SPAN(DIGITS) + ~(σ(".") + NSPAN(DIGITS)) + RPOS(0);
        T.Match  ("real '+3.14'", "+3.14", real);
        T.NoMatch("real 'abc'",   "abc",   real);

        T.Match("ARBNO 'ababab'", "ababab", POS(0) + ARBNO(σ("ab")) + RPOS(0));
        T.Match("BAL '(a+b)'",   "(a+b)",  POS(0) + BAL()           + RPOS(0));
    }
}
