// ShiftReduce.cs — parse-tree construction patterns
//
// These patterns build an abstract syntax tree (AST) during a match by
// maintaining two stacks on MatchState:
//
//   istack / itop — integer counter stack
//   vstack        — value (node) stack
//
// The idiom mirrors a classic shift-reduce parser embedded inside a SNOBOL4
// pattern.  As sub-patterns match, Shift pushes typed leaf nodes; Reduce pops
// N children and wraps them in a parent node; Pop moves the completed root
// into Env for the caller to inspect.
//
// All three patterns work through the cstack mechanism: they push an Action
// before yielding and pop it on backtrack.  Engine.SEARCH fires the surviving
// actions in order after the whole match commits — so the tree is built only
// when the match succeeds.
//
// ── nPush / nInc / nPop ──────────────────────────────────────────────────────
// A separate integer counter stack tracks how many children have been shifted
// since the most recent nPush.  Reduce() with no explicit count reads this
// to know how many vstack items to collect.
//
//   nPush  — push a new counter (0) onto istack
//   nInc   — increment the top counter by 1
//   nPop   — pop the top counter
//
// Typical grammar fragment for a list of two or more items:
//
//   nPush() + item % (Slot)_.x + nInc() +
//   ARBNO(sep + item % (Slot)_.y + nInc()) +
//   nPop() + Reduce("list")
//
// ── Shift / Reduce / Pop ─────────────────────────────────────────────────────
// Shift(tag)        — push  [tag]            onto vstack
// Shift(tag, func)  — push  [tag, func()]    onto vstack  (func called at commit)
// Reduce(tag)       — pop istack[itop] items, wrap as [tag, child, ...], push
// Reduce(tag, n)    — pop exactly n items
// Pop(name)         — move vstack top into Env[name]
//
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;

namespace SNOBOL4
{

public sealed class _nPush : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        // Push two actions: increment itop, then push a fresh 0 counter.
        // Order matters — itop must be bumped before the 0 is appended so
        // that nInc/nPop can find it at istack[itop].
        Action a1 = () => { st.itop += 1; };
        Action a2 = () => { st.istack.Add(0); };
        st.cstack.Add(a1);
        st.cstack.Add(a2);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

public sealed class _nInc : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        Action act = () => { st.istack[st.itop] += 1; };
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

public sealed class _nPop : PATTERN {
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        // Pop in reverse order to nPush: remove the counter value first,
        // then decrement itop.
        Action a1 = () => { st.istack.RemoveAt(st.istack.Count - 1); };
        Action a2 = () => { st.itop -= 1; };
        st.cstack.Add(a1);
        st.cstack.Add(a2);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

public sealed class _Shift : PATTERN {
    readonly string        _tag;
    readonly Func<object>? _val;   // null → tag-only form
    public _Shift()                              { _tag = ""; _val = null; }   // empty placeholder
    public _Shift(string tag)                    { _tag = tag; _val = null; }
    public _Shift(string tag, Func<object> val)  { _tag = tag; _val = val; }

    public override IEnumerable<Slice> γ() {
        var st  = Ϣ.Top;
        var tag = _tag;
        var val = _val;
        Action act = () => {
            var node = new List<object> { tag };
            // val() is called at commit time, after all conditional captures
            // have fired, so it can safely read Env values set by % patterns
            if (val != null) node.Add(val());
            st.vstack.Add(node);
        };
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

public sealed class _Reduce : PATTERN {
    readonly string      _tag;
    readonly Func<object>? _dynTag;  // null = use _tag; non-null = call at commit
    readonly int         _x;   // -1 = read from istack top;  >=0 = explicit count

    // Tags listed here are "transparent" when they have exactly one child:
    // rather than wrapping [tag, child] the single child is left as-is.
    // This keeps the tree clean for grammar rules that exist only for grouping.
    static readonly HashSet<string> _transparent =
        new() { "Σ", "Π", "snoExprList", "|", ".." };

    public _Reduce(string tag, int x = -1)               { _tag = tag; _dynTag = null; _x = x; }
    public _Reduce(Func<object> dynTag, int x = -1)      { _tag = "";  _dynTag = dynTag; _x = x; }

    public override IEnumerable<Slice> γ() {
        var st  = Ϣ.Top;
        var tag    = _tag;
        var dynTag = _dynTag;
        var x   = _x;
        Action act = () => {
            string resolvedTag = dynTag != null ? (dynTag()?.ToString() ?? "") : tag;
            int n = (x == -1) ? (st.itop >= 0 ? st.istack[st.itop] : 0) : x;

            // Reduce("Σ", 0) — the empty concatenation becomes an ε leaf
            if (n == 0 && resolvedTag == "Σ") {
                st.vstack.Add(new List<object> { "ε" });
                return;
            }

            // Single-child transparent tag — pass the child through unchanged
            if (n == 1 && _transparent.Contains(resolvedTag)) return;

            // General case: pop the bottom n items from vstack, wrap as a node.
            var node  = new List<object> { resolvedTag };
            int start  = Math.Max(0, st.vstack.Count - n);
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

public sealed class _Pop : PATTERN {
    readonly Action<List<object>> _set;
    public _Pop(Action<List<object>> set) { _set = set; }

    public override IEnumerable<Slice> γ() {
        var st   = Ϣ.Top;
        var set = _set;
        Action act = () => {
            // Move the completed root node from vstack to the caller's variable
            var top = st.vstack[st.vstack.Count - 1];
            st.vstack.RemoveAt(st.vstack.Count - 1);
            set(top);
        };
        st.cstack.Add(act);
        yield return new Slice(st.pos, st.pos);
        st.cstack.RemoveAt(st.cstack.Count - 1);
    }
}

}
