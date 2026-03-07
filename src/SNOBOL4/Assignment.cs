// Assignment.cs -- SNOBOL4 pattern library
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
public sealed class _δ : PATTERN { readonly PATTERN _P; readonly string _N; public _δ(PATTERN p, string n) { _P = p; _N = n; }
    public override IEnumerable<Slice> γ() {
        foreach (var sl in _P.γ()) {
            Env.Set(_N, Ϣ.Top.subject.Substring(sl.Start, sl.Stop - sl.Start));
            yield return sl;
        }
    }
}

// Δ — conditional assignment (% operator in Python)
public sealed class _Δ : PATTERN { readonly PATTERN _P; readonly string _N; public _Δ(PATTERN p, string n) { _P = p; _N = n; }
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
public sealed class _Θ : PATTERN { readonly string _N; public _Θ(string n) { _N = n; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; Env.Set(_N, st.pos); yield return new Slice(st.pos, st.pos); } }

// θ — conditional position capture
public sealed class _θ : PATTERN { readonly string _N; public _θ(string n) { _N = n; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; var pos = st.pos; var nm = _N;
        Action act = () => Env.Set(nm, pos);
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

// Λ — conditional test (Func<bool>)
public sealed class _Λ : PATTERN { readonly Func<bool> _t; public _Λ(Func<bool> t) { _t = t; }
    public override IEnumerable<Slice> γ() { var st = Ϣ.Top; if (_t()) yield return new Slice(st.pos, st.pos); } }

// λ — side-effect action (Action, fires at commit via cstack)
public sealed class _λ : PATTERN { readonly Action _c; public _λ(Action c) { _c = c; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        st.cstack.Add(_c);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

// ── ζ — deferred pattern reference ───────────────────────────────────────────

// ζ(string) — look up PATTERN in Env by name (unchanged from v8)
public sealed class _ζ_name : PATTERN { readonly string _N; public _ζ_name(string n) { _N = n; }
    public override IEnumerable<Slice> γ() {
        foreach (var s in ((PATTERN)Env.Get(_N)).γ()) yield return s;
    }
}

// ζ(Func<PATTERN>) — call lambda each time γ() runs (NEW in v9)
// This is the direct equivalent of Python's ζ(lambda: re_Expression).
// The lambda is invoked on every match attempt, so it always sees the
// current value of the captured variable — enabling mutual recursion.
public sealed class _ζ_func : PATTERN { readonly Func<PATTERN> _f; public _ζ_func(Func<PATTERN> f) { _f = f; }
    public override IEnumerable<Slice> γ() {
        foreach (var s in _f().γ()) yield return s;
    }
}

}
