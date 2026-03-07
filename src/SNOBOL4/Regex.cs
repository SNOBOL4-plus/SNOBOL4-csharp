// Regex.cs — .NET regex patterns integrated into the SNOBOL4 engine
//
// Φ and φ embed a compiled .NET regular expression as a SNOBOL4 PATTERN.
// The match is always anchored to the current cursor position; if the regex
// matches elsewhere in the string it is ignored.
//
//   Φ(rx) — immediate regex: named groups are written to Env right away,
//            even if the outer match later fails (like Δ for match values)
//   φ(rx) — conditional regex: named group captures are deferred via cstack
//            and written to Env only when the whole match commits (like δ)
//
// Named groups in the regex pattern become Env keys automatically:
//   φ(@"(?P<year>\d{4})-(?P<month>\d{2})")
//   → on success: Env["year"] = "2024", Env["month"] = "03"
//
// RxCache compiles each distinct pattern string once and reuses the Regex
// object for all subsequent matches.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SNOBOL4
{

// ── RxCache — compiled regex cache ───────────────────────────────────────────
// Compiling a Regex is expensive.  Patterns are compiled once on first use
// and stored here for reuse.  RegexOptions.Compiled generates IL for the
// pattern so repeated matches are fast.
public static class RxCache {
    static readonly Dictionary<string, Regex> _c = new();
    public static Regex Get(string pat) {
        if (!_c.TryGetValue(pat, out var rx))
            _c[pat] = rx = new Regex(pat, RegexOptions.Multiline | RegexOptions.Compiled);
        return rx;
    }
}

// ── Φ — immediate regex ───────────────────────────────────────────────────────
// Runs the regex anchored at st.pos.  On success, writes all named capture
// groups to Env immediately (not via cstack) and yields the matched slice.
// Group "0" (the whole match) and purely numeric groups are skipped — only
// named groups become Env entries.
public sealed class _Φ : PATTERN {
    readonly string _pat;
    public _Φ(string p) { _pat = p; }
    public override IEnumerable<Slice> γ() {
        var st = Ϣ.Top;
        var m  = RxCache.Get(_pat).Match(st.subject, st.pos);
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

// ── φ — conditional regex ─────────────────────────────────────────────────────
// Like Φ but defers named group writes via cstack so they are rolled back if
// the outer match fails.  Each named group that succeeded pushes its own
// closure; all are popped together on backtrack.
public sealed class _φ : PATTERN {
    readonly string _pat;
    public _φ(string p) { _pat = p; }
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
                    var nm = g.Name; var v = g.Value;
                    st.cstack.Add(() => Env.Set(nm, v));
                    pushed++;
                }
            }
            yield return new Slice(p, st.pos);
            // Backtrack: remove exactly the closures we pushed, in reverse order
            for (int i = 0; i < pushed; i++)
                st.cstack.RemoveAt(st.cstack.Count - 1);
            st.pos = p;
        }
    }
}

}
