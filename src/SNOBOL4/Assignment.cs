// Assignment.cs — pattern assignment and predicate operators
//
// The four Greek pairs, all following the same convention:
//   Upper-case — immediate: fires as the sub-pattern matches, permanent
//   Lower-case — conditional: deferred via cstack, fires only on full commit
//
//   Δ / δ   match-value assignment    (SNOBOL4  P $ N  /  P . N)
//   Θ / θ   cursor-position capture   (writes pos rather than matched text)
//   Λ / λ   predicate / action        (Func<bool> guard  /  Action side-effect)
//   ζ       deferred pattern ref      (resolves PATTERN from Env at match time)
//
// The cstack mechanism (defined on MatchState in Core.cs):
//   Conditional patterns push an Action before yielding and pop it on backtrack.
//   Engine.SEARCH fires all surviving cstack actions after the first successful
//   yield — the "commit" step.  Only the actions on the winning path execute.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
// ── Δ — immediate match-value assignment  (SNOBOL4: P $ N) ───────────────────
// Writes Env[N] = matched substring on every yield from P.
// Permanent — the write is not rolled back when backtracking resumes the
// generator.  Use this when you need the value even if the outer match fails,
// e.g. to inspect partial progress during debugging.
public sealed class _Δ : PATTERN
{
    readonly PATTERN _P; readonly string _N;
    public _Δ(PATTERN p, string n) { _P = p; _N = n; }
    public override IEnumerable<Slice> γ() {
        foreach (var sl in _P.γ()) {
            Env.Set(_N, Ϣ.Top.subject.Substring(sl.Start, sl.Stop - sl.Start));
            yield return sl;
        }
    }
}

// ── δ — conditional match-value assignment  (SNOBOL4: P . N) ─────────────────
// Pushes a capture closure onto cstack before yielding; pops it on backtrack.
// Engine.SEARCH executes cstack only after the whole match succeeds, so the
// write is rolled back automatically if the outer match fails.
// This is the normal capture operator — used via P % "name" or P % (Slot)_.x.
public sealed class _δ : PATTERN
{
    readonly PATTERN _P; readonly string _N;
    public _δ(PATTERN p, string n) { _P = p; _N = n; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        foreach (var sl in _P.γ()) {
            var cap = st.subject.Substring(sl.Start, sl.Stop - sl.Start);
            var nm  = _N;
            Action act = () => Env.Set(nm, cap);
            st.cstack.Add(act);
            yield return sl;
            st.cstack.RemoveAt(st.cstack.Count - 1);
        }
    }
}

// ── Θ — immediate cursor-position capture ────────────────────────────────────
// Records the current cursor position into Env[N] immediately.
// Like Δ, this is permanent and not rolled back on backtrack.
public sealed class _Θ : PATTERN
{
    readonly string _N; public _Θ(string n) { _N = n; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        Env.Set(_N, st.pos);
        yield return new Slice(st.pos, st.pos);
    }
}

// ── θ — conditional cursor-position capture ──────────────────────────────────
// Defers the cursor write to cstack; fires only on full match commit.
public sealed class _θ : PATTERN
{
    readonly string _N; public _θ(string n) { _N = n; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top; var pos = st.pos; var nm = _N;
        Action act = () => Env.Set(nm, pos);
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

// ── Λ — immediate predicate ───────────────────────────────────────────────────
// Evaluates the Func<bool> right now.  Yields (zero-length match) if true,
// fails if false.  Used as a guard condition on a previously captured value:
//   SPAN(DIGITS) * (Slot)_.n + Λ(() => ((Slot)_.n).Length == 4)
public sealed class _Λ : PATTERN
{
    readonly Func<bool> _t; public _Λ(Func<bool> t) { _t = t; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        if (_t()) yield return new Slice(st.pos, st.pos);
    }
}

// ── λ — conditional action ────────────────────────────────────────────────────
// Pushes an Action onto cstack; it fires only on full match commit.
// Use for side effects that should not happen if the match ultimately fails.
public sealed class _λ : PATTERN
{
    readonly Action _c; public _λ(Action c) { _c = c; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        st.cstack.Add(_c);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

// ── ζ — deferred pattern reference ───────────────────────────────────────────
// Resolves the actual PATTERN at match time rather than at construction time.
// This is the key mechanism for forward references and mutually recursive
// grammars: you can name a pattern before it is defined.

// ζ(string) — looks up Env[name] as a PATTERN each time γ() is called.
// The name must resolve to a PATTERN object in Env at match time.
public sealed class _ζ_name : PATTERN
{
    readonly string _N; public _ζ_name(string n) { _N = n; }
    public override IEnumerable<Slice> γ() {
        foreach (var s in ((PATTERN)Env.Get(_N)).γ()) yield return s;
    }
}

// ζ(Func<PATTERN>) — invokes the lambda each time γ() runs.
// Enables mutual recursion between C# local variables without needing
// string names in Env.  The lambda is called on every match attempt so
// it always captures the current value of the closed-over variable:
//
//   PATTERN? expr = null;
//   var term  = σ("(") + ζ(() => expr!) + σ(")");
//   expr = term | SPAN(ALPHA);
public sealed class _ζ_func : PATTERN
{
    readonly Func<PATTERN> _f; public _ζ_func(Func<PATTERN> f) { _f = f; }
    public override IEnumerable<Slice> γ() {
        foreach (var s in _f().γ()) yield return s;
    }
}

}
