// Regex.cs -- SNOBOL4 pattern library
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SNOBOL4
{
public static class RxCache {
    static readonly Dictionary<string, Regex> _c = new();
    public static Regex Get(string pat) {
        if (!_c.TryGetValue(pat, out var rx))
            _c[pat] = rx = new Regex(pat, RegexOptions.Multiline | RegexOptions.Compiled);
        return rx;
    }
}

// ── Φ — immediate regex match ─────────────────────────────────────────────────
public sealed class _Φ : PATTERN {
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
public sealed class _φ : PATTERN {
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

}
