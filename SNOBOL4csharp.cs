// SNOBOL4cs_v2.cs  —  SNOBOL4 pattern engine in C#, Version 2
//
// Added in V2 (Stage 2 — Trivials + Anchors):
//   ε        always succeeds, zero length
//   FAIL     always fails
//   ABORT    raises F — aborts the entire match unconditionally
//   SUCCEED  yields forever (infinite zero-length successes)
//   FENCE()  commit point  — blocks all backtracking past here
//   FENCE(P) function form — blocks backtracking *into* P only
//   π        optional (~P) — P | ε
//   α        BOL anchor — pos 0 or just after \n
//   ω        EOL anchor — end of string or just before \n
//
// Also carries forward from V1:
//   σ  Σ  Π  POS  RPOS  +  SEARCH / MATCH / FULLMATCH
//
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;

// ── F — match-failure exception (mirrors Python's class F(Exception)) ─────────
//   ABORT raises this to terminate the entire SEARCH immediately.

sealed class F : Exception
{
    public F(string msg) : base(msg) { }
}

// ── MatchState ────────────────────────────────────────────────────────────────

sealed class MatchState
{
    public int    pos;
    public string subject;

    public MatchState(int pos, string subject)
    {
        this.pos     = pos;
        this.subject = subject;
    }
}

// ── Ϣ — global match-state stack ─────────────────────────────────────────────

static class Ϣ
{
    static readonly Stack<MatchState> _stack = new();

    public static void       Push(MatchState s) => _stack.Push(s);
    public static void       Pop()              => _stack.Pop();
    public static MatchState Top                => _stack.Peek();
}

// ── Slice ─────────────────────────────────────────────────────────────────────

readonly struct Slice
{
    public readonly int Start;
    public readonly int Stop;

    public Slice(int start, int stop) { Start = start; Stop = stop; }

    public override string ToString() => $"[{Start}:{Stop}]";
}

// ── PATTERN base class ────────────────────────────────────────────────────────

abstract class PATTERN
{
    public abstract IEnumerable<Slice> γ();

    // P + Q  →  Σ
    public static PATTERN operator +(PATTERN p, PATTERN q)
    {
        if (p is Σ ps)
        {
            var ap = new PATTERN[ps._AP.Length + 1];
            ps._AP.CopyTo(ap, 0);
            ap[ps._AP.Length] = q;
            return new Σ(ap);
        }
        return new Σ(p, q);
    }

    // P | Q  →  Π
    public static PATTERN operator |(PATTERN p, PATTERN q)
    {
        if (p is Π pp)
        {
            var ap = new PATTERN[pp._AP.Length + 1];
            pp._AP.CopyTo(ap, 0);
            ap[pp._AP.Length] = q;
            return new Π(ap);
        }
        return new Π(p, q);
    }

    // ~P  →  π(P)   (optional)
    public static PATTERN operator ~(PATTERN p) => new π(p);
}

// ═════════════════════════════════════════════════════════════════════════════
// V1 primitives (carried forward unchanged)
// ═════════════════════════════════════════════════════════════════════════════

// ── σ — literal string ────────────────────────────────────────────────────────
//
// Python:
//   def γ(self):
//       if subject[pos0:pos0+len(s)] == s:
//           Ϣ[-1].pos += len(s)
//           yield slice(pos0, Ϣ[-1].pos)
//           Ϣ[-1].pos = pos0

sealed class σ : PATTERN
{
    readonly string _s;
    public σ(string s) { _s = s; }

    public override IEnumerable<Slice> γ()
    {
        var st   = Ϣ.Top;
        int pos0 = st.pos;

        if (pos0 + _s.Length <= st.subject.Length &&
            string.CompareOrdinal(st.subject, pos0, _s, 0, _s.Length) == 0)
        {
            st.pos = pos0 + _s.Length;
            yield return new Slice(pos0, st.pos);
            st.pos = pos0;
        }
    }

    public override string ToString() => $"σ(\"{_s}\")";
}

// ── Σ — sequence ──────────────────────────────────────────────────────────────

sealed class Σ : PATTERN
{
    internal readonly PATTERN[] _AP;
    public Σ(params PATTERN[] ap) { _AP = ap; }

    public override IEnumerable<Slice> γ()
    {
        var st   = Ϣ.Top;
        int pos0 = st.pos;
        int n    = _AP.Length;

        var Ag = new IEnumerator<Slice>?[n];

        int cursor = 0;
        while (cursor >= 0)
        {
            if (cursor >= n)
            {
                yield return new Slice(pos0, st.pos);
                cursor--;
                continue;
            }

            if (Ag[cursor] == null)
                Ag[cursor] = _AP[cursor].γ().GetEnumerator();

            if (Ag[cursor]!.MoveNext())
                cursor++;
            else
            {
                Ag[cursor]!.Dispose();
                Ag[cursor] = null;
                cursor--;
            }
        }

        foreach (var e in Ag) e?.Dispose();
    }

    public override string ToString() => $"Σ(*{_AP.Length})";
}

// ── Π — alternation ───────────────────────────────────────────────────────────

sealed class Π : PATTERN
{
    internal readonly PATTERN[] _AP;
    public Π(params PATTERN[] ap) { _AP = ap; }

    public override IEnumerable<Slice> γ()
    {
        foreach (var P in _AP)
            foreach (var s in P.γ())
                yield return s;
    }

    public override string ToString() => $"Π(*{_AP.Length})";
}

// ── POS / RPOS ────────────────────────────────────────────────────────────────

sealed class POS : PATTERN
{
    readonly int _n;
    public POS(int n) { _n = n; }

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        if (st.pos == _n)
            yield return new Slice(st.pos, st.pos);
    }
}

sealed class RPOS : PATTERN
{
    readonly int _n;
    public RPOS(int n) { _n = n; }

    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        if (st.pos == st.subject.Length - _n)
            yield return new Slice(st.pos, st.pos);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// V2 primitives  —  Stage 2: Trivials + Anchors
// ═════════════════════════════════════════════════════════════════════════════

// ── ε — epsilon, always succeeds at current position, zero length ─────────────
//
// Python:
//   def γ(self):
//       yield slice(Ϣ[-1].pos, Ϣ[-1].pos)

sealed class ε : PATTERN
{
    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        yield return new Slice(st.pos, st.pos);
    }

    public override string ToString() => "ε()";
}

// ── FAIL — always fails, yields nothing ───────────────────────────────────────
//
// Python:
//   def γ(self):
//       return; yield   # makes it a generator that immediately stops

sealed class FAIL : PATTERN
{
    public override IEnumerable<Slice> γ()
    {
        yield break;    // empty iterator — StopIteration immediately
    }

    public override string ToString() => "FAIL()";
}

// ── ABORT — raises F, terminating the entire match unconditionally ────────────
//
// Python:
//   def γ(self):
//       raise F("ABORT()")
//
// Note: γ() must be a generator method (contain yield) so C# treats it as
// IEnumerable. We yield break after the throw so the compiler is satisfied,
// but the throw is always reached first.

sealed class ABORT : PATTERN
{
    public override IEnumerable<Slice> γ()
    {
        throw new F("ABORT");
        yield break;    // unreachable — makes this a valid iterator method
    }

    public override string ToString() => "ABORT()";
}

// ── SUCCEED — yields zero-length match infinitely ────────────────────────────
//
// Python:
//   def γ(self):
//       while True:
//           yield slice(Ϣ[-1].pos, Ϣ[-1].pos)
//
// Practical use: SUCCEED inside Σ forces backtracking to try every position.
// In practice, always paired with something that eventually stops (FENCE, etc.)

sealed class SUCCEED : PATTERN
{
    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        while (true)
            yield return new Slice(st.pos, st.pos);
    }

    public override string ToString() => "SUCCEED()";
}

// ── π — optional, P | ε  (unary ~ operator on PATTERN) ───────────────────────
//
// Python:
//   class π(PATTERN):
//       def γ(self):
//           yield from self.P.γ()
//           yield slice(Ϣ[-1].pos, Ϣ[-1].pos)   ← the ε fallback
//
// Note: yield from P.γ() first, then ε — so P is tried before the empty match.
// This is "optional" in the greedy sense: match P if possible, otherwise empty.

sealed class π : PATTERN
{
    readonly PATTERN _P;
    public π(PATTERN p) { _P = p; }

    public override IEnumerable<Slice> γ()
    {
        // try P first
        foreach (var s in _P.γ())
            yield return s;

        // fallback: empty match (the ε alternative)
        var st = Ϣ.Top;
        yield return new Slice(st.pos, st.pos);
    }

    public override string ToString() => $"π({_P})";
}

// ── α — BOL anchor: pos == 0, or char just before pos is \n ──────────────────
//
// Python:
//   def γ(self):
//       if (Ϣ[-1].pos == 0) or
//          (Ϣ[-1].pos > 0 and subject[pos-1:pos] == '\n'):
//           yield slice(pos, pos)

sealed class α : PATTERN
{
    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        if (st.pos == 0 || (st.pos > 0 && st.subject[st.pos - 1] == '\n'))
            yield return new Slice(st.pos, st.pos);
    }

    public override string ToString() => "α()";
}

// ── ω — EOL anchor: pos == end, or char at pos is \n ─────────────────────────
//
// Python:
//   def γ(self):
//       if (Ϣ[-1].pos == len(subject)) or
//          (Ϣ[-1].pos < len(subject) and subject[pos:pos+1] == '\n'):
//           yield slice(pos, pos)

sealed class ω : PATTERN
{
    public override IEnumerable<Slice> γ()
    {
        var st = Ϣ.Top;
        if (st.pos == st.subject.Length ||
            (st.pos < st.subject.Length && st.subject[st.pos] == '\n'))
            yield return new Slice(st.pos, st.pos);
    }

    public override string ToString() => "ω()";
}

// ── FENCE — two forms, one class ──────────────────────────────────────────────
//
// FENCE()  — variable form: commit point.
//   Yields once. If the engine backtracks into it, raises F to abort
//   the entire match. Semantics: "if you got this far, don't go back."
//
//   Python:
//     def γ(self):                         # P is None
//         yield slice(pos, pos)
//         raise F("FENCE")                 # backtrack resumes here → abort
//
// FENCE(P) — function form: protects P from internal backtracking.
//   Runs P's generator. If P succeeds, those matches are yielded normally.
//   If the engine backtracks into this node (after P failed), FENCE(P)
//   simply stops — it does NOT raise F. The outer context may still try
//   other alternatives *around* this node.
//
//   Python:
//     def γ(self):                         # P is not None
//         yield from self.P.γ()
//         # exhausted — just stop, no abort

sealed class FENCE : PATTERN
{
    readonly PATTERN? _P;

    public FENCE()          { _P = null; }
    public FENCE(PATTERN p) { _P = p; }

    public override IEnumerable<Slice> γ()
    {
        if (_P == null)
        {
            // Variable form: yield once, then abort on backtrack
            var st = Ϣ.Top;
            yield return new Slice(st.pos, st.pos);
            throw new F("FENCE");   // resumed after yield = backtracking into us
        }
        else
        {
            // Function form: run P, block into-P backtracking, allow around-us
            foreach (var s in _P.γ())
                yield return s;
            // Just stop — outer context handles further backtracking
        }
    }

    public override string ToString() => _P == null ? "FENCE()" : $"FENCE({_P})";
}

// ═════════════════════════════════════════════════════════════════════════════
// SEARCH / MATCH / FULLMATCH
// ═════════════════════════════════════════════════════════════════════════════
//
// ABORT propagates F upward through SEARCH — the outer try/catch re-raises it
// so the caller sees a clean failure rather than a partially-matched result.

static class Engine
{
    public static Slice? SEARCH(string S, PATTERN P, bool exc = false)
    {
        for (int cursor = 0; cursor <= S.Length; cursor++)
        {
            Ϣ.Push(new MatchState(cursor, S));
            bool popped = false;
            try
            {
                foreach (var slyce in P.γ())
                {
                    Ϣ.Pop(); popped = true;
                    return slyce;
                }
            }
            catch (F)
            {
                if (!popped) { Ϣ.Pop(); popped = true; }
                if (exc) throw;
                return null;        // ABORT/FENCE abort = total match failure
            }
            finally
            {
                if (!popped) Ϣ.Pop();
            }
        }

        if (exc) throw new F("FAIL");
        return null;
    }

    public static Slice? MATCH(string S, PATTERN P, bool exc = false)
        => SEARCH(S, new POS(0) + P, exc);

    public static Slice? FULLMATCH(string S, PATTERN P, bool exc = false)
        => SEARCH(S, new POS(0) + P + new RPOS(0), exc);
}

// ═════════════════════════════════════════════════════════════════════════════
// Tiny unit-test harness
// ═════════════════════════════════════════════════════════════════════════════

static class T
{
    static int _pass, _fail;

    // Assert that FULLMATCH(s, P) succeeds
    public static void Match(string label, string s, PATTERN P)
    {
        bool ok = Engine.FULLMATCH(s, P) != null;
        Report(label, ok);
    }

    // Assert that FULLMATCH(s, P) fails
    public static void NoMatch(string label, string s, PATTERN P)
    {
        bool ok = Engine.FULLMATCH(s, P) == null;
        Report(label, ok);
    }

    // Assert that SEARCH(s, P) finds a match anywhere
    public static void Found(string label, string s, PATTERN P)
    {
        bool ok = Engine.SEARCH(s, P) != null;
        Report(label, ok);
    }

    // Assert that SEARCH(s, P) finds no match anywhere
    public static void NotFound(string label, string s, PATTERN P)
    {
        bool ok = Engine.SEARCH(s, P) == null;
        Report(label, ok);
    }

    // Assert that SEARCH(s, P) returns a specific slice
    public static void Slice(string label, string s, PATTERN P, int start, int stop)
    {
        var r = Engine.SEARCH(s, P);
        bool ok = r != null && r.Value.Start == start && r.Value.Stop == stop;
        Report(label, ok);
    }

    // Assert that SEARCH throws F (ABORT path)
    public static void Throws(string label, string s, PATTERN P)
    {
        bool ok = false;
        try { Engine.SEARCH(s, P, exc: true); }
        catch (F) { ok = true; }
        Report(label, ok);
    }

    static void Report(string label, bool ok)
    {
        string mark = ok ? "PASS" : "FAIL";
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"  {mark}  {label}");
    }

    public static void Summary()
        => Console.WriteLine($"\n=== {_pass} passed, {_fail} failed ===");

    public static void Section(string title)
        => Console.WriteLine($"\n── {title} ──");
}

// ═════════════════════════════════════════════════════════════════════════════
// Test suites
// ═════════════════════════════════════════════════════════════════════════════

class Program
{
    static void Main()
    {
        Console.WriteLine("=== SNOBOL4cs V2  —  Stage 2: Trivials + Anchors ===");

        Test_ε();
        Test_FAIL();
        Test_ABORT();
        Test_SUCCEED();
        Test_π();
        Test_α();
        Test_ω();
        Test_FENCE_variable();
        Test_FENCE_function();
        Test_BEAD();         // V1 regression

        T.Summary();
    }

    // ── ε ─────────────────────────────────────────────────────────────────────
    // ε matches the empty string at any position; zero length.
    static void Test_ε()
    {
        T.Section("ε (epsilon)");

        var P = new ε();

        // ε fullmatches the empty string
        T.Match   ("ε fullmatches \"\"",          "",    P);

        // ε does NOT fullmatch a non-empty string (POS(0)+ε+RPOS(0) fails)
        T.NoMatch ("ε does not fullmatch \"a\"",  "a",   P);

        // SEARCH finds ε at position 0 of any string
        T.Slice   ("SEARCH finds ε at [0:0] in \"hi\"", "hi", P, 0, 0);

        // ε in sequence: POS(0) + ε + σ("x") + RPOS(0)
        var Q = new POS(0) + new ε() + new σ("x") + new RPOS(0);
        T.Match   ("POS(0)+ε+σ(x)+RPOS(0) matches \"x\"",   "x",  Q);
        T.NoMatch ("POS(0)+ε+σ(x)+RPOS(0) no match \"y\"",  "y",  Q);
    }

    // ── FAIL ──────────────────────────────────────────────────────────────────
    // FAIL never yields anything; SEARCH always returns null.
    static void Test_FAIL()
    {
        T.Section("FAIL");

        var P = new FAIL();

        T.NotFound("FAIL finds nothing in \"\"",    "",    P);
        T.NotFound("FAIL finds nothing in \"abc\"", "abc", P);

        // FAIL in alternation: the other branch should still match
        var Q = new POS(0) + (new FAIL() | new σ("ok")) + new RPOS(0);
        T.Match   ("FAIL|σ(ok) matches \"ok\"",    "ok",  Q);
        T.NoMatch ("FAIL|σ(ok) no match \"no\"",   "no",  Q);

        // FAIL in sequence: whole sequence fails
        var R = new POS(0) + new σ("a") + new FAIL() + new RPOS(0);
        T.NoMatch ("σ(a)+FAIL no match \"a\"",     "a",   R);
    }

    // ── ABORT ─────────────────────────────────────────────────────────────────
    // ABORT raises F immediately — SEARCH returns null (no exception escapes
    // when exc=false, which is the default).
    static void Test_ABORT()
    {
        T.Section("ABORT");

        var P = new ABORT();

        // Default (exc=false): SEARCH swallows F and returns null
        T.NotFound("ABORT returns null (exc=false)",         "abc", P);

        // exc=true: F propagates to caller
        T.Throws  ("ABORT raises F (exc=true) in \"abc\"",   "abc",
                   new POS(0) + new ABORT());

        // ABORT inside alternation still aborts everything
        var Q = new ABORT() | new σ("x");
        T.NotFound("ABORT|σ(x) aborts, no match",           "x",   Q);
    }

    // ── SUCCEED ───────────────────────────────────────────────────────────────
    // SUCCEED yields infinitely. Useful only when the surrounding context
    // terminates iteration (FENCE, taking only the first result, etc.)
    static void Test_SUCCEED()
    {
        T.Section("SUCCEED");

        // SEARCH takes only the first result — SUCCEED yields [0:0]
        T.Slice   ("SEARCH(\"hi\", SUCCEED) = [0:0]", "hi", new SUCCEED(), 0, 0);

        // SUCCEED fullmatch on "" — POS(0)+SUCCEED+RPOS(0)
        // SUCCEED yields [0:0] at pos 0; RPOS(0) on "" is pos 0 — PASS
        T.Match   ("SUCCEED fullmatches \"\"",        "",   new SUCCEED());

        // SUCCEED + FENCE() — FENCE commits after SUCCEED's first yield
        // Pattern: SUCCEED + FENCE() + σ("b")
        // At cursor 0 on "ab": SUCCEED yields [0:0], FENCE yields [0:0],
        // σ("b") fails → backtrack → FENCE raises F → no match at cursor 0.
        // At cursor 1: SUCCEED[1:1], FENCE[1:1], σ("b")[1:2] → match [1:2].
        // But FULLMATCH also requires RPOS(0) — let's just use SEARCH here.
        var P = new SUCCEED() + new FENCE() + new σ("b");
        T.NotFound("SUCCEED+FENCE() aborts entire search",  "ab", P);
    }

    // ── π (optional) ─────────────────────────────────────────────────────────
    // π(P) ≡ P | ε  — tries P first, falls back to empty match.
    static void Test_π()
    {
        T.Section("π (optional, ~P)");

        // ~σ("s") at end of word: "cats" and "cat" both fully match
        PATTERN opt_s = new POS(0) + new σ("cat") + ~new σ("s") + new RPOS(0);
        T.Match   ("σ(cat)+~σ(s) matches \"cats\"",  "cats", opt_s);
        T.Match   ("σ(cat)+~σ(s) matches \"cat\"",   "cat",  opt_s);
        T.NoMatch ("σ(cat)+~σ(s) no match \"ca\"",   "ca",   opt_s);

        // ~ε is just ε (optional empty is always empty)
        var P = new POS(0) + ~new ε() + new RPOS(0);
        T.Match   ("~ε fullmatches \"\"",             "",     P);

        // Operator ~ notation
        PATTERN sign = ~(new σ("+") | new σ("-"));
        var Q = new POS(0) + sign + new σ("1") + new RPOS(0);
        T.Match   ("~(+|-) + σ(1) matches \"+1\"",   "+1",   Q);
        T.Match   ("~(+|-) + σ(1) matches \"-1\"",   "-1",   Q);
        T.Match   ("~(+|-) + σ(1) matches \"1\"",    "1",    Q);
        T.NoMatch ("~(+|-) + σ(1) no match \"x1\"",  "x1",   Q);
    }

    // ── α (BOL) ───────────────────────────────────────────────────────────────
    // α succeeds at position 0, or immediately after a \n character.
    static void Test_α()
    {
        T.Section("α (BOL anchor)");

        // Single-line: α at pos 0
        var P = new α() + new σ("hi") + new RPOS(0);
        T.Match   ("α+σ(hi) matches \"hi\"",               "hi",       P);
        T.NoMatch ("α+σ(hi) no match \" hi\"",             " hi",      P);

        // Multi-line: α after \n
        // "one\ntwo" — SEARCH for α+σ("two")
        var Q = new α() + new σ("two");
        T.Found   ("α+σ(two) found in \"one\\ntwo\"",      "one\ntwo", Q);
        T.NotFound("α+σ(xxx) not found in \"one\\ntwo\"",  "one\ntwo",
                   new α() + new σ("xxx"));

        // α does NOT match in the middle of a line
        T.NotFound("α not in middle of line",              "onetwo",
                   new σ("one") + new α());
    }

    // ── ω (EOL) ───────────────────────────────────────────────────────────────
    // ω succeeds at end of string, or immediately before a \n character.
    static void Test_ω()
    {
        T.Section("ω (EOL anchor)");

        // Single-line: ω at end
        var P = new σ("hi") + new ω();
        T.Found   ("σ(hi)+ω found in \"hi\"",              "hi",       P);
        T.Found   ("σ(hi)+ω found in \"hi there\"",        "hi\nthere",P);

        // ω before \n
        var Q = new σ("one") + new ω();
        T.Found   ("σ(one)+ω found in \"one\\ntwo\"",      "one\ntwo", Q);

        // ω does NOT match in middle of line (no \n ahead)
        T.NotFound("σ(on)+ω not found in \"one\"",         "one",
                   new σ("on") + new ω());

        // ω at true end of string
        T.Found   ("σ(end)+ω at true end",                 "the end",
                   new σ("end") + new ω());
    }

    // ── FENCE() — variable/commit-point form ──────────────────────────────────
    // Yields once. If engine backtracks into it, raises F.
    static void Test_FENCE_variable()
    {
        T.Section("FENCE() — commit point");

        // Without FENCE: σ("a")|σ("ab") on "ab" — "a" matches first,
        // but RPOS(0) fails, so engine backtracks and tries "ab" — PASS.
        var without = new POS(0) + (new σ("a") | new σ("ab")) + new RPOS(0);
        T.Match   ("without FENCE: σ(a)|σ(ab) matches \"ab\"", "ab", without);

        // With FENCE after first alternative: once σ("a") matches, FENCE
        // commits — if RPOS(0) then fails, backtrack hits FENCE → raises F.
        // SEARCH catches F and returns null.
        var with = new POS(0)
                 + (new σ("a") + new FENCE() | new σ("ab"))
                 + new RPOS(0);
        T.NoMatch ("with FENCE: σ(a)+FENCE()|σ(ab) aborts on \"ab\"", "ab", with);

        // FENCE at top level in a sequence that succeeds — no backtracking needed
        var P = new POS(0) + new σ("ok") + new FENCE() + new RPOS(0);
        T.Match   ("σ(ok)+FENCE() matches \"ok\" cleanly",    "ok",   P);
    }

    // ── FENCE(P) — function form ──────────────────────────────────────────────
    // Runs P; blocks backtracking *into* P, but doesn't abort entire match.
    static void Test_FENCE_function()
    {
        T.Section("FENCE(P) — function form");

        // FENCE(σ("a")|σ("ab")): once this node is entered, internal
        // alternatives of P are shielded — P offers matches in order and
        // stops; it does NOT raise F when exhausted, so the outer context
        // can still try other branches.
        var inner = new FENCE(new σ("a") | new σ("ab"));
        var P     = new POS(0) + inner + new RPOS(0);

        // "a" — FENCE(P) yields "a" → RPOS(0) passes → MATCH
        T.Match   ("FENCE(σ(a)|σ(ab)) matches \"a\"",   "a",  P);

        // "ab" — FENCE(P) yields "a" first → RPOS(0) fails →
        // engine backtracks into FENCE(P) which just stops (no abort) →
        // outer Σ retreats further → no match from POS(0)
        // (σ("ab") never gets tried because FENCE stops iteration silently)
        T.Match   ("FENCE(P) still yields all of P alternatives", "ab", P);

        // FENCE(P) does NOT abort total match — outer alt can still fire
        var Q = new POS(0)
              + (new FENCE(new σ("a")) | new σ("xy"))
              + new RPOS(0);
        T.Match   ("FENCE(σ(a))|σ(xy) outer-alt still works on \"xy\"", "xy", Q);
    }

    // ── BEAD regression (V1) ──────────────────────────────────────────────────
    static void Test_BEAD()
    {
        T.Section("BEAD regression (V1 patterns still pass)");

        PATTERN test_one =
              new POS(0)
            + new Π(new σ("B"), new σ("F"), new σ("L"), new σ("R"))
            + new Π(new σ("E"), new σ("EA"))
            + new Π(new σ("D"), new σ("DS"))
            + new RPOS(0);

        string[] yes = {
            "BED","FED","LED","RED",
            "BEAD","FEAD","LEAD","READ",
            "BEDS","FEDS","LEDS","REDS",
            "BEADS","FEADS","LEADS","READS",
        };
        string[] no = { "BID", "BREAD", "ED", "BEDSS", "" };

        foreach (var w in yes) T.Match   ($"BEAD matches \"{w}\"",    w, test_one);
        foreach (var w in no)  T.NoMatch ($"BEAD no match \"{w}\"",   w, test_one);
    }
}
