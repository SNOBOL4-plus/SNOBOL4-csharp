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

    static readonly Dictionary<string, object> G = new();
    static string Gs(string k) => G.TryGetValue(k, out var v) ? v?.ToString() ?? "<null>" : "<unset>";
    static List<object> Gl(string k) => G.TryGetValue(k, out var v) ? (List<object>)v : new();

    static void Main() {
        GLOBALS(G);
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
        G["tree1"] = new List<object>();
        var p1 = POS(0) + SPAN(ALPHA) % "word" + Shift("Word") + Pop("tree1") + RPOS(0);
        Engine.FULLMATCH("hello", p1);
        var t1 = Gl("tree1");
        T.Eq("Shift/Pop tag", "Word", t1.Count > 0 ? t1[0] : null);

        // Shift with value expression
        G["tree2"] = new List<object>();
        var p2 = POS(0)
               + SPAN(DIGITS) % "n"
               + Shift("Int", () => int.Parse(Gs("n")))
               + Pop("tree2")
               + RPOS(0);
        Engine.FULLMATCH("42", p2);
        var t2 = Gl("tree2");
        T.Eq("Shift tag",  "Int", t2.Count > 0 ? t2[0] : null);
        T.Eq("Shift value", 42,   t2.Count > 1 ? t2[1] : null);

        // Reduce: nPush + two Shifts + Reduce("Pair") + nPop
        G["tree3"] = new List<object>();
        var p3 = POS(0)
               + nPush()
               + SPAN(UCASE) % "a" + Shift("A") + nInc()
               + SPAN(LCASE) % "b" + Shift("B") + nInc()
               + Reduce("Pair")
               + nPop()
               + Pop("tree3")
               + RPOS(0);
        Engine.FULLMATCH("HELLOworld", p3);
        var t3 = Gl("tree3");
        T.Eq("Reduce tag",       "Pair", t3.Count > 0 ? t3[0] : null);
        T.Eq("Reduce child count", 2,    t3.Count - 1);

        // Reduce backtrack: if outer match fails, vstack should be clean
        G["treeX"] = new List<object> { "before" };
        var p4 = POS(0)
               + nPush()
               + SPAN(ALPHA) % "w" + Shift("W") + nInc()
               + Reduce("Node")
               + nPop()
               + σ("NOPE")     // forces failure
               + Pop("treeX")
               + RPOS(0);
        Engine.FULLMATCH("hello", p4);
        // treeX should remain "before" since match failed
        T.Eq("Reduce not fired on fail", "before",
            G.TryGetValue("treeX", out var tx) ? (tx is List<object> tl ? tl[0] : tx) : "<missing>");
    }

    // ── RE grammar (mirrors test_re_simple.py) ────────────────────────────────
    // re_Quantifier  = σ('*')+Shift('*') | σ('+')+Shift('+') | σ('?')+Shift('?')
    // re_Item        = σ('.')+Shift('.')
    //                | ANY(UCASE+LCASE+DIGITS)%'tx' + Shift('σ', ()=>Gs("tx"))
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
            | ANY(UCASE + LCASE + DIGITS) % "tx" + Shift("σ", () => (object)Gs("tx"))
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

        var re_RegEx = POS(0) + re_Expression + Pop("RE_tree") + RPOS(0);

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
            G.Remove("RE_tree");
            T.Match($"RE parses \"{rex}\"", rex, re_RegEx);
        }
        foreach (var bad in shouldFail) {
            G.Remove("RE_tree");
            T.NoMatch($"RE rejects \"{bad}\"", bad, re_RegEx);
        }

        // Check tree is a list
        G.Remove("RE_tree");
        Engine.FULLMATCH("A|B", re_RegEx);
        T.Eq("RE_tree is set", true, G.ContainsKey("RE_tree"));
        T.Eq("RE_tree is List<object>", true, G["RE_tree"] is List<object>);
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
        T.Eq("Φ word", "hello", Gs("word"));
        T.Eq("Φ num",  "42",    Gs("num"));

        // φ conditional
        G["ctag"] = "before";
        Engine.FULLMATCH("abc123", POS(0) + φ(@"(?<ctag>[a-z]+)") + σ("NOPE") + RPOS(0));
        T.Eq("φ silent on fail", "before", Gs("ctag"));
        G["ctag"] = "before";
        Engine.FULLMATCH("abc", POS(0) + φ(@"(?<ctag>[a-z]+)") + RPOS(0));
        T.Eq("φ fires on success", "abc", Gs("ctag"));

        // % conditional assignment
        Engine.FULLMATCH("hello42", POS(0) + (SPAN(ALPHA) % "rw") + (SPAN(DIGITS) % "rn") + RPOS(0));
        T.Eq("% word", "hello", Gs("rw"));
        T.Eq("% num",  "42",    Gs("rn"));

        // ζ(string) name lookup
        PATTERN atom2 = SPAN(DIGITS) | σ("(") + ζ("rexpr") + σ(")");
        PATTERN expr2 = atom2 + ARBNO((σ("+") | σ("-")) + atom2);
        G["rexpr"] = expr2;
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
        G["kw"] = "hello";
        var p = POS(0) + σ(() => Gs("kw")) + RPOS(0);
        T.Match  ("matches kw=hello", "hello", p);
        T.NoMatch("rejects world",    "world", p);
        G["kw"] = "world";
        T.Match  ("matches kw=world", "world", p);
        T.NoMatch("rejects hello",    "hello", p);
    }
    static void Test_POS_RPOS_deferred() {
        T.Section("POS/RPOS(Func<int>)");
        G["p"] = 2;
        var pat = POS(() => Env.Int("p")) + SPAN(DIGITS) % "found";
        G["found"] = "";
        Engine.SEARCH("ab42cd", pat);
        T.Eq("POS(2) finds 42", "42", Gs("found"));
        G["p"] = 4; G["found"] = "none";
        Engine.SEARCH("ab42cd", pat);
        T.Eq("POS(4) no digits", "none", Gs("found"));
        G["r"] = 0;
        var pr = POS(0) + SPAN(ALPHA) % "w" + RPOS(() => Env.Int("r"));
        T.Match  ("RPOS(0) full match", "hello", pr);
        G["r"] = 3;
        T.NoMatch("RPOS(3) rejects",   "hello", pr);
    }
    static void Test_LEN_TAB_RTAB_deferred() {
        T.Section("LEN/TAB/RTAB(Func<int>)");
        G["n"] = 3;
        var pl = POS(0) + LEN(() => Env.Int("n")) % "chunk" + RPOS(0);
        T.Match  ("LEN(3) abc",        "abc", pl);
        T.Eq("chunk", "abc", Gs("chunk"));
        T.NoMatch("LEN(3) rejects ab", "ab",  pl);
        G["n"] = 2; T.Match("LEN(2) ab", "ab", pl);
        G["t"] = 3;
        var pt = POS(0) + TAB(() => Env.Int("t")) % "pre" + REM() % "suf";
        Engine.FULLMATCH("abcdef", pt);
        T.Eq("TAB(3) pre", "abc", Gs("pre")); T.Eq("TAB(3) suf", "def", Gs("suf"));
        G["rt"] = 2;
        var pr2 = POS(0) + RTAB(() => Env.Int("rt")) % "body" + REM() % "tail";
        Engine.FULLMATCH("abcdef", pr2);
        T.Eq("RTAB(2) body","abcd",Gs("body")); T.Eq("RTAB(2) tail","ef",Gs("tail"));
    }
    static void Test_charset_deferred() {
        T.Section("ANY/NOTANY/SPAN/NSPAN/BREAK/BREAKX(Func<string>)");
        G["ch"] = "aeiou";
        var pa = POS(0) + ANY(() => Gs("ch")) + RPOS(0);
        T.Match("ANY vowel a","a",pa); T.NoMatch("ANY vowel b","b",pa);
        G["ch"] = "xyz";
        T.Match("ANY xyz x","x",pa); T.NoMatch("ANY xyz a","a",pa);
        G["ex"] = "aeiou";
        var pna = POS(0) + NOTANY(() => Gs("ex")) + RPOS(0);
        T.Match("NOTANY vowel b","b",pna); T.NoMatch("NOTANY vowel a","a",pna);
        G["cs"] = DIGITS;
        var ps = POS(0) + SPAN(() => Gs("cs")) % "tok" + RPOS(0);
        T.Match("SPAN digits 123","123",ps); T.NoMatch("SPAN digits abc","abc",ps);
        G["cs"] = ALPHA;
        T.Match("SPAN alpha abc","abc",ps); T.NoMatch("SPAN alpha 123","123",ps);
        G["cs"] = DIGITS;
        var pns = POS(0) + NSPAN(() => Gs("cs")) % "tok" + RPOS(0);
        T.Match("NSPAN digits","123",pns); T.Match("NSPAN empty ok","",pns);
        G["dlm"] = ":";
        var pb = POS(0)+BREAK(()=>Gs("dlm"))%"k"+σ(()=>Gs("dlm"))+REM()%"v";
        Engine.SEARCH("name:val", pb);
        T.Eq("BREAK colon key","name",Gs("k")); T.Eq("BREAK colon val","val",Gs("v"));
        G["dlm"] = "="; Engine.SEARCH("x=42", pb);
        T.Eq("BREAK eq key","x",Gs("k")); T.Eq("BREAK eq val","42",Gs("v"));
        G["dlm"] = ",";
        var pbx = POS(0)+BREAKX(()=>Gs("dlm"))%"f"+σ(()=>Gs("dlm"))+REM()%"r";
        Engine.SEARCH("a,b,c", pbx);
        T.Eq("BREAKX comma first","a",Gs("f")); T.Eq("BREAKX comma rest","b,c",Gs("r"));
        G["dlm"] = "|"; Engine.SEARCH("x|y|z", pbx);
        T.Eq("BREAKX pipe first","x",Gs("f")); T.Eq("BREAKX pipe rest","y|z",Gs("r"));
    }
    static void Test_combined_deferred() {
        T.Section("Combined deferred args");
        G["valid"] = "";
        Engine.FULLMATCH("0123", POS(0)+SPAN(ALNUM)%"valid"+RPOS(0));
        T.Eq("captured charset","0123",Gs("valid"));
        var chk = POS(0)+SPAN(()=>Gs("valid"))+RPOS(0);
        T.Match("SPAN accepts 031","031",chk); T.NoMatch("SPAN rejects 456","456",chk);
        G["op"] = "+=";
        var p3 = BREAK(()=>Gs("op"))%"lhs"+σ(()=>Gs("op"))+REM()%"rhs";
        Engine.SEARCH("count+=1", p3);
        T.Eq("op lhs","count",Gs("lhs")); T.Eq("op rhs","1",Gs("rhs"));
    }

}
}
