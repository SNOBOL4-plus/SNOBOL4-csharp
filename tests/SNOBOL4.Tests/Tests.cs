// Tests.cs -- all SNOBOL4 tests (Stage 8 + Stage 9)
using System;
using System.Collections.Generic;
using SNOBOL4;
using static SNOBOL4.S4;

namespace SNOBOL4.Tests
{
static class Tests {
    const string DIGITS = "0123456789";
    const string UCASE  = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string LCASE  = "abcdefghijklmnopqrstuvwxyz";
    const string ALPHA  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    const string ALNUM  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";


    static void Main() {
        Console.WriteLine("=== SNOBOL4cs V10  —  Stage 9: deferred callable args ===");
        Test_nStack();
        Test_Shift_Reduce_Pop();
        Test_RE_grammar();
        Test_ζ_func();
        Test_regression_v8();
        Test_σ_deferred();
        Test_POS_RPOS_deferred();
        Test_LEN_TAB_RTAB_deferred();
        Test_charset_deferred();
        Test_combined_deferred();
        Tests10.Run();
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
        S4._.tree1 = new List<object>();
        var p1 = POS(0) + SPAN(ALPHA) % (Slot)S4._.word + Shift("Word") + Pop((Slot)S4._.tree1) + RPOS(0);
        Engine.FULLMATCH("hello", p1);
        var t1 = (List<object>)((Slot)S4._.tree1).Value!;
        T.Eq("Shift/Pop tag", "Word", t1.Count > 0 ? t1[0] : null);

        // Shift with value expression
        S4._.tree2 = new List<object>();
        var p2 = POS(0)
               + SPAN(DIGITS) % (Slot)S4._.n
               + Shift("Int", () => int.Parse((string)(Slot)S4._.n))
               + Pop((Slot)S4._.tree2)
               + RPOS(0);
        Engine.FULLMATCH("42", p2);
        var t2 = (List<object>)((Slot)S4._.tree2).Value!;
        T.Eq("Shift tag",  "Int", t2.Count > 0 ? t2[0] : null);
        T.Eq("Shift value", 42,   t2.Count > 1 ? t2[1] : null);

        // Reduce: nPush + two Shifts + Reduce("Pair") + nPop
        S4._.tree3 = new List<object>();
        var p3 = POS(0)
               + nPush()
               + SPAN(UCASE) % (Slot)S4._.a + Shift("A") + nInc()
               + SPAN(LCASE) % (Slot)S4._.b + Shift("B") + nInc()
               + Reduce("Pair")
               + nPop()
               + Pop((Slot)S4._.tree3)
               + RPOS(0);
        Engine.FULLMATCH("HELLOworld", p3);
        var t3 = (List<object>)((Slot)S4._.tree3).Value!;
        T.Eq("Reduce tag",       "Pair", t3.Count > 0 ? t3[0] : null);
        T.Eq("Reduce child count", 2,    t3.Count - 1);

        // Reduce backtrack: if outer match fails, vstack should be clean
        S4._.treeX = new List<object> { "before" };
        var p4 = POS(0)
               + nPush()
               + SPAN(ALPHA) % (Slot)S4._.w + Shift("W") + nInc()
               + Reduce("Node")
               + nPop()
               + σ("NOPE")     // forces failure
               + Pop((Slot)S4._.treeX)
               + RPOS(0);
        Engine.FULLMATCH("hello", p4);
        // treeX should remain "before" since match failed
        T.Eq("Reduce not fired on fail", "before",
            Env.G.TryGetValue("treeX", out var tx) ? (tx is List<object> tl ? tl[0] : tx) : "<missing>");
    }

    // ── RE grammar (mirrors test_re_simple.py) ────────────────────────────────
    // re_Quantifier  = σ('*')+Shift('*') | σ('+')+Shift('+') | σ('?')+Shift('?')
    // re_Item        = σ('.')+Shift('.')
    //                | ANY(UCASE+LCASE+DIGITS)%'tx' + Shift('σ', ()=>(string)(Slot)S4._.tx)
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
            | ANY(UCASE + LCASE + DIGITS) % (Slot)S4._.tx + Shift("σ", () => (object)(string)(Slot)S4._.tx)
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

        var re_RegEx = POS(0) + re_Expression + Pop((Slot)S4._.RE_tree) + RPOS(0);

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
            Env.G.Remove("RE_tree");
            T.Match($"RE parses \"{rex}\"", rex, re_RegEx);
        }
        foreach (var bad in shouldFail) {
            Env.G.Remove("RE_tree");
            T.NoMatch($"RE rejects \"{bad}\"", bad, re_RegEx);
        }

        // Check tree is a list
        Env.G.Remove("RE_tree");
        Engine.FULLMATCH("A|B", re_RegEx);
        T.Eq("RE_tree is set", true, Env.G.ContainsKey("RE_tree"));
        T.Eq("RE_tree is List<object>", true, Env.G["RE_tree"] is List<object>);
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
        T.Eq("Φ word", "hello", (string)(Slot)S4._.word);
        T.Eq("Φ num",  "42",    (string)(Slot)S4._.num);

        // φ conditional
        S4._.ctag = "before";
        Engine.FULLMATCH("abc123", POS(0) + φ(@"(?<ctag>[a-z]+)") + σ("NOPE") + RPOS(0));
        T.Eq("φ silent on fail", "before", (string)(Slot)S4._.ctag);
        S4._.ctag = "before";
        Engine.FULLMATCH("abc", POS(0) + φ(@"(?<ctag>[a-z]+)") + RPOS(0));
        T.Eq("φ fires on success", "abc", (string)(Slot)S4._.ctag);

        // % conditional assignment
        Engine.FULLMATCH("hello42", POS(0) + (SPAN(ALPHA) % (Slot)S4._.rw) + (SPAN(DIGITS) % (Slot)S4._.rn) + RPOS(0));
        T.Eq("% word", "hello", (string)(Slot)S4._.rw);
        T.Eq("% num",  "42",    (string)(Slot)S4._.rn);

        // ζ(string) name lookup
        PATTERN atom2 = SPAN(DIGITS) | σ("(") + ζ("rexpr") + σ(")");
        PATTERN expr2 = atom2 + ARBNO((σ("+") | σ("-")) + atom2);
        S4._.rexpr = expr2;
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

    static void Test_σ_deferred() {
        T.Section("σ(Func<string>)");
        S4._.kw = "hello";
        var p = POS(0) + σ(() => (string)(Slot)S4._.kw) + RPOS(0);
        T.Match  ("matches kw=hello", "hello", p);
        T.NoMatch("rejects world",    "world", p);
        S4._.kw = "world";
        T.Match  ("matches kw=world", "world", p);
        T.NoMatch("rejects hello",    "hello", p);
    }
    static void Test_POS_RPOS_deferred() {
        T.Section("POS/RPOS(Func<int>)");
        S4._.p = 2;
        var pat = POS(() => (int)(Slot)S4._.p) + SPAN(DIGITS) % (Slot)S4._.found;
        S4._.found = "";
        Engine.SEARCH("ab42cd", pat);
        T.Eq("POS(2) finds 42", "42", (string)(Slot)S4._.found);
        S4._.p = 4; S4._.found = "none";
        Engine.SEARCH("ab42cd", pat);
        T.Eq("POS(4) no digits", "none", (string)(Slot)S4._.found);
        S4._.r = 0;
        var pr = POS(0) + SPAN(ALPHA) % (Slot)S4._.w + RPOS(() => (int)(Slot)S4._.r);
        T.Match  ("RPOS(0) full match", "hello", pr);
        S4._.r = 3;
        T.NoMatch("RPOS(3) rejects",   "hello", pr);
    }
    static void Test_LEN_TAB_RTAB_deferred() {
        T.Section("LEN/TAB/RTAB(Func<int>)");
        S4._.n = 3;
        var pl = POS(0) + LEN(() => (int)(Slot)S4._.n) % (Slot)S4._.chunk + RPOS(0);
        T.Match  ("LEN(3) abc",        "abc", pl);
        T.Eq("chunk", "abc", (string)(Slot)S4._.chunk);
        T.NoMatch("LEN(3) rejects ab", "ab",  pl);
        S4._.n = 2; T.Match("LEN(2) ab", "ab", pl);
        S4._.t = 3;
        var pt = POS(0) + TAB(() => (int)(Slot)S4._.t) % (Slot)S4._.pre + REM() % (Slot)S4._.suf;
        Engine.FULLMATCH("abcdef", pt);
        T.Eq("TAB(3) pre", "abc", (string)(Slot)S4._.pre); T.Eq("TAB(3) suf", "def", (string)(Slot)S4._.suf);
        S4._.rt = 2;
        var pr2 = POS(0) + RTAB(() => (int)(Slot)S4._.rt) % (Slot)S4._.body + REM() % (Slot)S4._.tail;
        Engine.FULLMATCH("abcdef", pr2);
        T.Eq("RTAB(2) body","abcd",(string)(Slot)S4._.body); T.Eq("RTAB(2) tail","ef",(string)(Slot)S4._.tail);
    }
    static void Test_charset_deferred() {
        T.Section("ANY/NOTANY/SPAN/NSPAN/BREAK/BREAKX(Func<string>)");
        S4._.ch = "aeiou";
        var pa = POS(0) + ANY(() => (string)(Slot)S4._.ch) + RPOS(0);
        T.Match("ANY vowel a","a",pa); T.NoMatch("ANY vowel b","b",pa);
        S4._.ch = "xyz";
        T.Match("ANY xyz x","x",pa); T.NoMatch("ANY xyz a","a",pa);
        S4._.ex = "aeiou";
        var pna = POS(0) + NOTANY(() => (string)(Slot)S4._.ex) + RPOS(0);
        T.Match("NOTANY vowel b","b",pna); T.NoMatch("NOTANY vowel a","a",pna);
        S4._.cs = DIGITS;
        var ps = POS(0) + SPAN(() => (string)(Slot)S4._.cs) % (Slot)S4._.tok + RPOS(0);
        T.Match("SPAN digits 123","123",ps); T.NoMatch("SPAN digits abc","abc",ps);
        S4._.cs = ALPHA;
        T.Match("SPAN alpha abc","abc",ps); T.NoMatch("SPAN alpha 123","123",ps);
        S4._.cs = DIGITS;
        var pns = POS(0) + NSPAN(() => (string)(Slot)S4._.cs) % (Slot)S4._.tok + RPOS(0);
        T.Match("NSPAN digits","123",pns); T.Match("NSPAN empty ok","",pns);
        S4._.dlm = ":";
        var pb = POS(0)+BREAK(()=>(string)(Slot)S4._.dlm)%"k"+σ(()=>(string)(Slot)S4._.dlm)+REM()%"v";
        Engine.SEARCH("name:val", pb);
        T.Eq("BREAK colon key","name",(string)(Slot)S4._.k); T.Eq("BREAK colon val","val",(string)(Slot)S4._.v);
        S4._.dlm = "="; Engine.SEARCH("x=42", pb);
        T.Eq("BREAK eq key","x",(string)(Slot)S4._.k); T.Eq("BREAK eq val","42",(string)(Slot)S4._.v);
        S4._.dlm = ",";
        var pbx = POS(0)+BREAKX(()=>(string)(Slot)S4._.dlm)%"f"+σ(()=>(string)(Slot)S4._.dlm)+REM()%"r";
        Engine.SEARCH("a,b,c", pbx);
        T.Eq("BREAKX comma first","a",(string)(Slot)S4._.f); T.Eq("BREAKX comma rest","b,c",(string)(Slot)S4._.r);
        S4._.dlm = "|"; Engine.SEARCH("x|y|z", pbx);
        T.Eq("BREAKX pipe first","x",(string)(Slot)S4._.f); T.Eq("BREAKX pipe rest","y|z",(string)(Slot)S4._.r);
    }
    static void Test_combined_deferred() {
        T.Section("Combined deferred args");
        S4._.valid = "";
        Engine.FULLMATCH("0123", POS(0)+SPAN(ALNUM)%"valid"+RPOS(0));
        T.Eq("captured charset","0123",(string)(Slot)S4._.valid);
        var chk = POS(0)+SPAN(()=>(string)(Slot)S4._.valid)+RPOS(0);
        T.Match("SPAN accepts 031","031",chk); T.NoMatch("SPAN rejects 456","456",chk);
        S4._.op = "+=";
        var p3 = BREAK(()=>(string)(Slot)S4._.op)%"lhs"+σ(()=>(string)(Slot)S4._.op)+REM()%"rhs";
        Engine.SEARCH("count+=1", p3);
        T.Eq("op lhs","count",(string)(Slot)S4._.lhs); T.Eq("op rhs","1",(string)(Slot)S4._.rhs);
    }

}
}
