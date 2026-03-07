// ShiftReduce.cs -- SNOBOL4 pattern library
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
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

public sealed class _nPush : PATTERN {
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

public sealed class _Shift : PATTERN {
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

public sealed class _Reduce : PATTERN {
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

public sealed class _Pop : PATTERN {
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
}
