// Assignment.cs — pattern assignment and predicate operators
//
// The four Greek pairs follow the same convention:
//   Upper-case — immediate: fires as the sub-pattern matches, permanent
//   Lower-case — conditional: deferred via cstack, fires only on full commit
//
//   Δ / δ   match-value assignment    (SNOBOL4  P $ N  /  P . N)
//   Θ / θ   cursor-position capture   (writes pos rather than matched text)
//   Λ / λ   predicate / action        (Func<bool> guard  /  Action side-effect)
//   ζ       deferred pattern ref      (Func<PATTERN> lambda for forward refs)
//
// Assignment targets are plain C# setter delegates — Action<string> for value
// capture and Action<int> for cursor capture.  No global environment or string
// names are involved:
//
//   string word = "";
//   SPAN(ALPHA) % (v => word = v)    — conditional: fires on commit
//   SPAN(ALPHA) * (v => word = v)    — immediate:   fires on sub-match
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
// Calls set(matchedString) on every yield from P.
// Permanent — the write is not rolled back when backtracking resumes the
// generator.  Use when you need the value even if the outer match fails.
public sealed class _Δ : PATTERN
{
    readonly PATTERN        _P;
    readonly Action<string> _set;
    public _Δ(PATTERN p, Action<string> set) { _P = p; _set = set; }
    public override IEnumerable<Slice> γ() {
        foreach (var sl in _P.γ()) {
            _set(Ϣ.Top.subject.Substring(sl.Start, sl.Stop - sl.Start));
            yield return sl;
        }
    }
}

// ── δ — conditional match-value assignment  (SNOBOL4: P . N) ─────────────────
// Pushes a capture closure onto cstack before yielding; pops it on backtrack.
// Engine.SEARCH executes cstack only after the whole match succeeds, so the
// write is rolled back automatically if the outer match fails.
// This is the normal capture operator — used via  P % (v => x = v).
public sealed class _δ : PATTERN
{
    readonly PATTERN        _P;
    readonly Action<string> _set;
    public _δ(PATTERN p, Action<string> set) { _P = p; _set = set; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        foreach (var sl in _P.γ()) {
            var cap = st.subject.Substring(sl.Start, sl.Stop - sl.Start);
            var set = _set;
            Action act = () => set(cap);
            st.cstack.Add(act);
            yield return sl;
            st.cstack.RemoveAt(st.cstack.Count - 1);
        }
    }
}

// ── Θ — immediate cursor-position capture ────────────────────────────────────
// Calls set(pos) immediately.  Not rolled back on backtrack.
public sealed class _Θ : PATTERN
{
    readonly Action<int> _set; public _Θ(Action<int> set) { _set = set; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        _set(st.pos);
        yield return new Slice(st.pos, st.pos);
    }
}

// ── θ — conditional cursor-position capture ──────────────────────────────────
// Defers the cursor write to cstack; fires only on full match commit.
public sealed class _θ : PATTERN
{
    readonly Action<int> _set; public _θ(Action<int> set) { _set = set; }
    public override IEnumerable<Slice> γ() {
        var st  = Ϣ.Top;
        var pos = st.pos;
        var set = _set;
        Action act = () => set(pos);
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

// ── Λ — immediate predicate ───────────────────────────────────────────────────
// Evaluates the Func<bool> right now.  Yields (zero-length match) if true,
// fails if false.  Used as a guard on a previously captured value:
//   string n = "";
//   SPAN(DIGITS) % (v => n = v) + Λ(() => n.Length == 4)
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
// Resolves the actual PATTERN at match time by invoking the lambda.
// This is the mechanism for forward references and mutually recursive grammars:
//
//   PATTERN? expr = null;
//   var term = σ("(") + ζ(() => expr!) + σ(")");
//   expr = term | SPAN(ALPHA);
//
// The lambda is called on every match attempt so it always sees the current
// value of the closed-over variable.
public sealed class _ζ : PATTERN
{
    readonly Func<PATTERN> _f; public _ζ(Func<PATTERN> f) { _f = f; }
    public override IEnumerable<Slice> γ() {
        foreach (var s in _f().γ()) yield return s;
    }
}

}
