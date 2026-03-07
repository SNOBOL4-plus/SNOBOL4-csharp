// Regex.cs — .NET regex patterns integrated into the SNOBOL4 engine
//
// Φ and φ embed a compiled .NET regular expression as a SNOBOL4 PATTERN.
// The match is always anchored to the current cursor position.
//
//   Φ(rx, onCapture) — immediate: calls onCapture(name, value) for each named
//                       group right away, even if the outer match later fails
//   φ(rx, onCapture) — conditional: defers onCapture calls via cstack so they
//                       fire only when the whole match commits
//
// The onCapture delegate receives the group name and matched value:
//   var groups = new Dictionary<string,string>();
//   Φ(@"(?<year>\d{4})-(?<month>\d{2})", (n, v) => groups[n] = v)
//
// RxCache compiles each distinct pattern string once and reuses the object.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SNOBOL4
{

// ── RxCache — compiled regex cache ───────────────────────────────────────────
// Compiling a Regex is expensive.  Each distinct pattern string is compiled
// once and stored here; subsequent matches reuse the Regex object.
public static class RxCache {
    static readonly Dictionary<string, Regex> _c = new();
    public static Regex Get(string pat) {
        if (!_c.TryGetValue(pat, out var rx))
            _c[pat] = rx = new Regex(pat, RegexOptions.Multiline | RegexOptions.Compiled);
        return rx;
    }
}

// ── Φ — immediate regex ───────────────────────────────────────────────────────
// Runs the regex anchored at st.pos.  On success, calls onCapture(name, value)
// for every named group immediately — not deferred, not rolled back.
public sealed class _Φ : PATTERN {
    readonly string                _pat;
    readonly Action<string,string> _on;
    public _Φ(string p, Action<string,string> on) { _pat = p; _on = on; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        var m  = RxCache.Get(_pat).Match(st.subject, st.pos);
        if (m.Success && m.Index == st.pos) {
            int p = st.pos; st.pos = m.Index + m.Length;
            foreach (Group g in m.Groups) {
                if (g.Name == "0") continue;
                if (int.TryParse(g.Name, out _)) continue;
                if (g.Success) _on(g.Name, g.Value);
            }
            yield return new Slice(p, st.pos);
            st.pos = p;
        }
    }
}

// ── φ — conditional regex ─────────────────────────────────────────────────────
// Like Φ but defers onCapture calls via cstack so they fire only on commit
// and are rolled back automatically if the outer match fails.
public sealed class _φ : PATTERN {
    readonly string                _pat;
    readonly Action<string,string> _on;
    public _φ(string p, Action<string,string> on) { _pat = p; _on = on; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        var m  = RxCache.Get(_pat).Match(st.subject, st.pos);
        if (m.Success && m.Index == st.pos) {
            int p      = st.pos;
            st.pos     = m.Index + m.Length;
            int pushed = 0;
            foreach (Group g in m.Groups) {
                if (g.Name == "0") continue;
                if (int.TryParse(g.Name, out _)) continue;
                if (g.Success) {
                    var nm = g.Name; var v = g.Value; var on = _on;
                    st.cstack.Add(() => on(nm, v));
                    pushed++;
                }
            }
            yield return new Slice(p, st.pos);
            for (int i = 0; i < pushed; i++)
                st.cstack.RemoveAt(st.cstack.Count - 1);
            st.pos = p;
        }
    }
}

}
