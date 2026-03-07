// Tests.Stage10.cs — parallel to Python test suite
//
//  test_01.py       → Test_01_*
//  test_arbno.py    → Test_Arbno_*
//  test_re_simple.py → Test_RE_*
//  test_json.py     → Test_JSON_*
//
// Each C# method mirrors one Python test function exactly:
// same name (snake→Pascal with prefix), same inputs, same assertions.
// Module-level patterns are static readonly fields, mirroring Python's
// module-scope definitions.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using SNOBOL4;
using static SNOBOL4.S4;

namespace SNOBOL4.Tests
{
static class Tests10
{

    const string DIGITS = "0123456789";
    const string UCASE  = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    const string LCASE  = "abcdefghijklmnopqrstuvwxyz";
    const string ALPHA  = UCASE + LCASE;
    const string ALNUM  = UCASE + LCASE + DIGITS;

    // ─────────────────────────────────────────────────────────────────────────
    // Patterns from test_01.py  (module-level definitions)
    // ─────────────────────────────────────────────────────────────────────────

    // identifier = POS(0) + ANY(UCASE+LCASE)
    //            + FENCE(SPAN("."+DIGITS+UCASE+"_"+LCASE) | ε()) + RPOS(0)
    static readonly PATTERN identifier =
          POS(0)
        + ANY(ALPHA)
        + FENCE(SPAN("." + DIGITS + UCASE + "_" + LCASE) | ε())
        + RPOS(0);

    // real_number — two branches (scientific | plain decimal)
    //   SPAN(DIGITS)@'whole' + [.SPAN?]@'fract' + (E|e) + [+-] + SPAN@'exp'
    // | SPAN(DIGITS)@'whole' + . + SPAN?@'fract'
    static readonly PATTERN real_number =
          POS(0)
        + (   (    SPAN(DIGITS) % (Slot)S4._.whole
               +   (σ(".") + FENCE(SPAN(DIGITS) | ε()) % (Slot)S4._.fract | ε())
               +   (σ("E") | σ("e"))
               +   (σ("+") | σ("-") | ε())
               +   SPAN(DIGITS) % (Slot)S4._.exp
              )
           |  (    SPAN(DIGITS) % (Slot)S4._.whole
               +   σ(".")
               +   FENCE(SPAN(DIGITS) | ε()) % (Slot)S4._.fract
              )
          )
        + RPOS(0);

    // test_one — BEAD (same pattern as prior stages)
    static readonly PATTERN test_one =
          POS(0)
        + Π(σ("B"), σ("F"), σ("L"), σ("R"))
        + Π(σ("E"), σ("EA"))
        + Π(σ("D"), σ("DS"))
        + RPOS(0);

    // Bal = POS(0) + BAL() + RPOS(0)
    static readonly PATTERN Bal = POS(0) + BAL() + RPOS(0);

    // Arb = POS(0) + ARB() + RPOS(0)
    static readonly PATTERN Arb = POS(0) + ARB() + RPOS(0);

    // ─────────────────────────────────────────────────────────────────────────
    // Patterns from test_arbno.py  (module-level definitions)
    // ─────────────────────────────────────────────────────────────────────────

    // As = POS(0) + ARBNO(σ('a')) + RPOS(0)
    static readonly PATTERN As =
          POS(0) + ARBNO(σ("a")) + RPOS(0);

    // Alist = POS(0) + (σ('a')|σ('b')) + ARBNO(σ(',')+(σ('a')|σ('b'))) + RPOS(0)
    static readonly PATTERN Alist =
          POS(0)
        + (σ("a") | σ("b"))
        + ARBNO(σ(",") + (σ("a") | σ("b")))
        + RPOS(0);

    // Pairs = POS(0) + ARBNO(σ('AA') | LEN(2) | σ('XX')) + RPOS(0)
    static readonly PATTERN Pairs =
          POS(0)
        + ARBNO(σ("AA") | LEN(2) | σ("XX"))
        + RPOS(0);

    // ─────────────────────────────────────────────────────────────────────────
    // Patterns from test_re_simple.py  (module-level definitions)
    // ─────────────────────────────────────────────────────────────────────────
    //
    // re_Quantifier = σ('*')+Shift('*') | σ('+')+Shift('+') | σ('?')+Shift('?')
    // re_Item       = σ('.')+Shift('.') | σ('\\')+ANY(...) | ANY(alnum)+Shift('σ',tx)
    //               | σ('(') + ζ(re_Expression) + σ(')')
    // re_Factor     = re_Item + (re_Quantifier + Reduce('ς',2) | ε())
    // re_Term       = nPush() + ARBNO(re_Factor + nInc()) + Reduce('Σ') + nPop()
    // re_Expression = nPush() + re_Term + nInc()
    //               + ARBNO(σ('|') + re_Term + nInc()) + Reduce('Π') + nPop()
    // re_RegEx      = POS(0) + re_Expression + Pop('RE_tree') + RPOS(0)

    static readonly PATTERN re_Quantifier =
          σ("*") + Shift("*")
        | σ("+") + Shift("+")
        | σ("?") + Shift("?");

    static readonly PATTERN re_Item =
          σ(".") + Shift(".")
        | σ("\\") + ANY(".\\"  + "(|*+?)") % (Slot)S4._.tx + Shift("σ", () => (string)(Slot)S4._.tx)
        | ANY(ALPHA + DIGITS) % (Slot)S4._.tx + Shift("σ", () => (string)(Slot)S4._.tx)
        | σ("(") + ζ(() => re_Expression) + σ(")");

    static readonly PATTERN re_Factor =
          re_Item + (re_Quantifier + Reduce("ς", 2) | ε());

    static readonly PATTERN re_Term =
          nPush() + ARBNO(re_Factor + nInc()) + Reduce("Σ") + nPop();

    static readonly PATTERN re_Expression =
          nPush()
        + re_Term + nInc()
        + ARBNO(σ("|") + re_Term + nInc())
        + Reduce("Π")
        + nPop();

    static readonly PATTERN re_RegEx =
          POS(0) + re_Expression + Pop((Slot)S4._.RE_tree) + RPOS(0);

    // ─────────────────────────────────────────────────────────────────────────
    // Patterns from test_json.py  (module-level definitions)
    // ─────────────────────────────────────────────────────────────────────────

    // ς(s) = (SPAN(" \t\r\n") | ε()) + σ(s)   — skip whitespace then literal
    static PATTERN WS(string s) => (SPAN(" \t\r\n") | ε()) + σ(s);

    static readonly PATTERN jInt =
        (FENCE(σ("+") | σ("-") | ε()) + SPAN(DIGITS)) % (Slot)S4._.jxN;

    static readonly PATTERN jEscChar =
          σ("\\")
        + (   ANY("ntbrf\"\\/'")
            | ANY("01234567") + FENCE(ANY("01234567") | ε())
            | ANY("0123") + ANY("01234567") + ANY("01234567")
            | σ("u") + SPAN("0123456789ABCDEFabcdef") % (Slot)S4._.jxHex + Λ(() => ((Slot)S4._.jxHex).Length == 4)
          );

    static readonly PATTERN jNullVal =
          σ("null") + ε() % (Slot)S4._.jxVal;

    static readonly PATTERN jTrueFalse =
          (σ("true") | σ("false")) % (Slot)S4._.jxVal;

    static readonly PATTERN jIdent =
          ANY(UCASE + "_" + LCASE)
        + FENCE(SPAN(UCASE + "_" + LCASE + DIGITS) | ε());

    static readonly PATTERN jString =
          σ("\"")
        + ((ARBNO(BREAK("\"\\n") | jEscChar)) % (Slot)S4._.jxVal)
        + σ("\"");

    static readonly PATTERN jStrVal = jString;   // no JSONDecode transform needed

    static readonly PATTERN jBoolVal =
          jTrueFalse
        | σ("\"") + jTrueFalse + σ("\"");

    static readonly PATTERN jRealVal =
        ((σ("+") | σ("-") | ε()) + SPAN(DIGITS) + σ(".") + SPAN(DIGITS)) % (Slot)S4._.jxVal;

    static readonly PATTERN jIntVal =
          (jInt % (Slot)S4._.jxVal)
        | σ("\"") + (jInt % (Slot)S4._.jxVal) + σ("\"");

    // Each jNNN: capture directly into target var via δ (immediate), then Λ checks length
    static readonly PATTERN jYYYY = δ(SPAN(DIGITS), "jxYYYY") + Λ(() => ((Slot)S4._.jxYYYY).Length == 4);
    static readonly PATTERN jMM   = δ(SPAN(DIGITS), "jxMM")   + Λ(() => ((Slot)S4._.jxMM).Length   == 2);
    static readonly PATTERN jDD   = δ(SPAN(DIGITS), "jxDD")   + Λ(() => ((Slot)S4._.jxDD).Length   == 2);
    static readonly PATTERN jhh   = δ(SPAN(DIGITS), "jxhh")   + Λ(() => ((Slot)S4._.jxhh).Length   == 2);
    static readonly PATTERN jmm2  = δ(SPAN(DIGITS), "jxmm")   + Λ(() => ((Slot)S4._.jxmm).Length   == 2);
    static readonly PATTERN jss   = δ(SPAN(DIGITS), "jxss")   + Λ(() => ((Slot)S4._.jxss).Length   == 2);
    static readonly PATTERN jNum3 = δ(SPAN(DIGITS), "jxN3")   + Λ(() => ((Slot)S4._.jxN3).Length   == 3);
    static readonly PATTERN jNum4 = δ(SPAN(DIGITS), "jxN4")   + Λ(() => ((Slot)S4._.jxN4).Length   == 4);
    static readonly PATTERN jNum2 = δ(SPAN(DIGITS), "jxN2")   + Λ(() => ((Slot)S4._.jxN2).Length   == 2);
    static readonly PATTERN jDate = jYYYY + σ("-") + jMM + σ("-") + jDD;
    static readonly PATTERN jTime = jhh + σ(":") + jmm2 + σ(":") + jss;

    static readonly PATTERN jDatetime =
          σ("\"")
        + (   jDate + σ("T") + jTime + σ(".") + (jNum3 | ε()) + σ("Z")
            | jDate + σ("T") + jTime + σ("+") + jNum4
            | jDate + σ("T") + jTime + σ("+") + jNum2 + σ(":") + jNum2
            | jDate + σ("T") + jTime
            | jDate + σ(" ") + jTime
            | jDate
          )
        + σ("\"");

    // jElement, jArray, jObject, jField are mutually recursive → ζ(λ)
    static PATTERN jElement =>
          WS("")
        + (   jRealVal    + Shift("Real",    () => double.Parse((string)(Slot)S4._.jxVal,
                                                    System.Globalization.CultureInfo.InvariantCulture))
            | jIntVal     + Shift("Integer", () => (object)int.Parse((string)(Slot)S4._.jxVal))
            | jBoolVal    + Shift("Bool",    () => (object)((string)(Slot)S4._.jxVal == "true"))
            | jDatetime   + Shift("Datetime",() => (object)ParseDatetime())
            | jStrVal     + Shift("String",  () => (object)(string)(Slot)S4._.jxVal)
            | jNullVal    + Shift("Null")
            | ζ(() => jArray)
            | ζ(() => jObject)
          );

    static readonly PATTERN jVar =
          WS("\"") + (jIdent | jInt) % (Slot)S4._.jxVar + σ("\"");

    static PATTERN jField =>
          jVar + Shift("Name", () => (object)(string)(Slot)S4._.jxVar)
        + WS(":") + jElement
        + Reduce("Attribute", 2);

    static PATTERN jObject =>
          WS("{") + nPush()
        + π(jField + nInc() + ARBNO(WS(",") + jField + nInc()))
        + WS("}") + Reduce("Object") + nPop()
        + FENCE();

    static PATTERN jArray =>
          WS("[") + nPush()
        + π(jElement + nInc() + ARBNO(WS(",") + jElement + nInc()))
        + WS("]") + Reduce("Array") + nPop()
        + FENCE();

    static readonly PATTERN jJSON =
          ζ(() => jObject) + Reduce("JSON", 1);

    static readonly PATTERN jRecognizer =
          POS(0) + FENCE() + jJSON + (SPAN(" \t\r\n") | ε()) + Pop((Slot)S4._.JSON_tree) + RPOS(0);

    static (int y, int m, int d, int hh, int mm, int ss) ParseDatetime() =>
        ( int.Parse((string)(Slot)S4._.jxYYYY), int.Parse((string)(Slot)S4._.jxMM),  int.Parse((string)(Slot)S4._.jxDD),
          int.Parse((string)(Slot)S4._.jxhh),   int.Parse((string)(Slot)S4._.jxmm), int.Parse((string)(Slot)S4._.jxss) );

    // ─────────────────────────────────────────────────────────────────────────
    // Tree traversal (mirrors Python Traverse + OBJECT)
    // ─────────────────────────────────────────────────────────────────────────
    static object Traverse(List<object> tree)
    {
        var tag = (string)tree[0];
        switch (tag) {
            case "JSON":      return Traverse((List<object>)tree[1]);
            case "Object": {
                var d = new Dictionary<string, object>();
                for (int i = 1; i < tree.Count; i++) {
                    var (k, v) = TraverseAttr((List<object>)tree[i]);
                    d[k] = v;
                }
                return d;
            }
            case "Array": {
                var lst = new List<object>();
                for (int i = 1; i < tree.Count; i++)
                    lst.Add(Traverse((List<object>)tree[i]));
                return lst;
            }
            case "Attribute": return (Traverse((List<object>)tree[1]), Traverse((List<object>)tree[2]));
            case "Name":      return tree[1];
            case "Real":      return tree[1];
            case "Integer":   return tree[1];
            case "String":    return tree[1];
            case "Bool":      return tree[1];
            case "Datetime":  return tree[1];
            case "Null":      return null!;
            default: throw new Exception($"Traverse: unknown tag '{tag}'");
        }
    }
    static (string k, object v) TraverseAttr(List<object> node)
    {
        var inner = (ValueTuple<object, object>)Traverse(node);
        return ((string)inner.Item1, inner.Item2);
    }

    // =========================================================================
    // Main — wire all sections in
    // =========================================================================
    public static void Run()
    {
        Console.WriteLine("\n=== Stage 10: Python test suite (test_01 · test_arbno · test_re_simple · test_json) ===");

        // ── test_01.py ────────────────────────────────────────────────────────
        Test_01_identifier_matches();
        Test_01_identifier_no_match();
        Test_01_real_number_matches();
        Test_01_real_number_no_match();
        Test_01_bead_matches();
        Test_01_bead_no_match();
        Test_01_bal_matches();
        Test_01_bal_no_match();
        Test_01_arb_matches();

        // ── test_arbno.py ─────────────────────────────────────────────────────
        Test_Arbno_As_matches();
        Test_Arbno_As_no_match();
        Test_Arbno_Alist_matches();
        Test_Arbno_Alist_no_match();
        Test_Arbno_Pairs_matches();
        Test_Arbno_Pairs_no_match();

        // ── test_re_simple.py ─────────────────────────────────────────────────
        Test_RE_parses();
        Test_RE_tree_is_list();
        Test_RE_no_parse();
        // ── test_json.py ─────────────────────────────────── TODO
        // ── v12 SnobolEnv / Slot / >> ────────────────────────────────────────
        Test_SnobolEnv();
    }

    // =========================================================================
    // test_01.py
    // =========================================================================

    // @pytest.mark.parametrize("s", ["Id_99","A","Z","abc","X1","a.b","A_B_C"])
    // def test_identifier_matches(s): assert s in identifier
    static void Test_01_identifier_matches()
    {
        T.Section("test_01 · identifier matches");
        foreach (var s in new[]{ "Id_99","A","Z","abc","X1","a.b","A_B_C" })
            T.Match($"identifier \"{s}\"", s, identifier);
    }

    // @pytest.mark.parametrize("s", ["","9abc","_abc","a b"])
    // def test_identifier_no_match(s): assert s not in identifier
    static void Test_01_identifier_no_match()
    {
        T.Section("test_01 · identifier no match");
        foreach (var s in new[]{ "","9abc","_abc","a b" })
            T.NoMatch($"not identifier \"{s}\"", s, identifier);
    }

    // @pytest.mark.parametrize("s", ["12.99E+3","1.0E0","0.5E-10","99.E+1","3.14","0.0","100.001"])
    // def test_real_number_matches(s): assert s in real_number
    static void Test_01_real_number_matches()
    {
        T.Section("test_01 · real_number matches");
        foreach (var s in new[]{ "12.99E+3","1.0E0","0.5E-10","99.E+1","3.14","0.0","100.001" })
            T.Match($"real \"{s}\"", s, real_number);
    }

    // @pytest.mark.parametrize("s", ["","abc","1",".5","1.2.3"])
    // def test_real_number_no_match(s): assert s not in real_number
    static void Test_01_real_number_no_match()
    {
        T.Section("test_01 · real_number no match");
        foreach (var s in new[]{ "","abc","1",".5","1.2.3" })
            T.NoMatch($"not real \"{s}\"", s, real_number);
    }

    // @pytest.mark.parametrize("word", ["BED","FED",..."READS"])
    // def test_one_matches(word): assert word in test_one
    static void Test_01_bead_matches()
    {
        T.Section("test_01 · BEAD matches");
        foreach (var w in new[]{
            "BED","FED","LED","RED",
            "BEAD","FEAD","LEAD","READ",
            "BEDS","FEDS","LEDS","REDS",
            "BEADS","FEADS","LEADS","READS" })
            T.Match($"BEAD \"{w}\"", w, test_one);
    }

    // @pytest.mark.parametrize("word", ["BID","BREAD","ED","BEDSS",""])
    // def test_one_no_match(word): assert word not in test_one
    static void Test_01_bead_no_match()
    {
        T.Section("test_01 · BEAD no match");
        foreach (var w in new[]{ "BID","BREAD","ED","BEDSS","" })
            T.NoMatch($"not BEAD \"{w}\"", w, test_one);
    }

    // @pytest.mark.parametrize("expr", ["(A+B)","A+B()","A()+B","X","XYZ",...])
    // def test_bal_matches(expr): assert expr in Bal
    static void Test_01_bal_matches()
    {
        T.Section("test_01 · BAL matches");
        foreach (var s in new[]{
            "(A+B)","A+B()","A()+B","X","XYZ",
            "A(B*C) (E/F)G+H","( (A+ ( B*C) ) +D)","(0+(1*9))","((A+(B*C))+D)" })
            T.Match($"BAL \"{s}\"", s, Bal);
    }

    // @pytest.mark.parametrize("expr", ["",")A+B(","A+B)","(A+B","A+B())","((A+B)"])
    // def test_bal_no_match(expr): assert expr not in Bal
    static void Test_01_bal_no_match()
    {
        T.Section("test_01 · BAL no match");
        foreach (var s in new[]{ "",")A+B(","A+B)","(A+B","A+B())","((A+B)" })
            T.NoMatch($"not BAL \"{s}\"", s, Bal);
    }

    // @pytest.mark.parametrize("s", ["","$","$$","$$$","hello","1 2 3"])
    // def test_arb_matches(s): assert s in Arb
    static void Test_01_arb_matches()
    {
        T.Section("test_01 · ARB matches");
        foreach (var s in new[]{ "","$","$$","$$$","hello","1 2 3" })
            T.Match($"ARB \"{s}\"", s, Arb);
    }

    // =========================================================================
    // test_arbno.py
    // =========================================================================

    // @pytest.mark.parametrize("s", ["","a","aa","aaa","aaaa"])
    // def test_as_matches(s): assert s in As
    static void Test_Arbno_As_matches()
    {
        T.Section("test_arbno · As matches");
        foreach (var s in new[]{ "","a","aa","aaa","aaaa" })
            T.Match($"As \"{s}\"", s, As);
    }

    // @pytest.mark.parametrize("s", ["b","ab","ba","aab","aaab"])
    // def test_as_no_match(s): assert s not in As
    static void Test_Arbno_As_no_match()
    {
        T.Section("test_arbno · As no match");
        foreach (var s in new[]{ "b","ab","ba","aab","aaab" })
            T.NoMatch($"not As \"{s}\"", s, As);
    }

    // @pytest.mark.parametrize("s", ["a","b","a,a","a,b","b,a","b,b","a,a,a","b,b,b","a,a,a,a"])
    // def test_alist_matches(s): assert s in Alist
    static void Test_Arbno_Alist_matches()
    {
        T.Section("test_arbno · Alist matches");
        foreach (var s in new[]{ "a","b","a,a","a,b","b,a","b,b","a,a,a","b,b,b","a,a,a,a" })
            T.Match($"Alist \"{s}\"", s, Alist);
    }

    // @pytest.mark.parametrize("s", ["","",a",","a,","a,,a","a,c","c"])
    // def test_alist_no_match(s): assert s not in Alist
    static void Test_Arbno_Alist_no_match()
    {
        T.Section("test_arbno · Alist no match");
        foreach (var s in new[]{ "","",",a","a,","a,,a","a,c","c" })
            T.NoMatch($"not Alist \"{s}\"", s, Alist);
    }

    // @pytest.mark.parametrize("s", ["","AA","XX","AB","AAXX","AABB","XXAA","AABBCC"])
    // def test_pairs_matches(s): assert s in Pairs
    static void Test_Arbno_Pairs_matches()
    {
        T.Section("test_arbno · Pairs matches");
        foreach (var s in new[]{ "","AA","XX","AB","AAXX","AABB","XXAA","AABBCC" })
            T.Match($"Pairs \"{s}\"", s, Pairs);
    }

    // @pytest.mark.parametrize("s", ["CCXXAA$","A","AAA"])
    // def test_pairs_no_match(s): assert s not in Pairs
    static void Test_Arbno_Pairs_no_match()
    {
        T.Section("test_arbno · Pairs no match");
        foreach (var s in new[]{ "CCXXAA$","A","AAA" })
            T.NoMatch($"not Pairs \"{s}\"", s, Pairs);
    }

    // =========================================================================
    // test_re_simple.py
    // =========================================================================

    // @pytest.mark.parametrize("rex", ["","A","AA","A*","A+","A?","A|B",...])
    // def test_re_parses(rex): assert rex in re_RegEx
    static void Test_RE_parses()
    {
        T.Section("test_re_simple · parses");
        var cases = new[]{
            // empty and single characters
            "", "A", "AA", "AAA",
            // quantifiers
            "A*", "A+", "A?",
            // alternation
            "A|B", "A|BC", "AB|C",
            // grouping with alternation
            "(A|)", "(A|B)*", "(A|B)+", "(A|B)?", "(A|B)C", "(A|)*",
            // nested grouping
            "A|(BC)", "(AB|CD)", "(AB*|CD*)", "((AB)*|(CD)*)", "(A|(BC))", "((AB)|C)", "(Ab|(CD))",
            // complex
            "A(A|B)*B",
        };
        foreach (var rex in cases) {
            GLOBALS(new Dictionary<string,object>());
            T.Match($"re_parses \"{rex}\"", rex, re_RegEx);
        }
    }

    // def test_re_tree_is_tuple(rex='A|B'):
    //     assert 'A|B' in re_RegEx
    //     assert isinstance(results["RE_tree"], list)
    //     assert len(results['RE_tree']) >= 1
    static void Test_RE_tree_is_list()
    {
        T.Section("test_re_simple · tree is list");
        GLOBALS(new Dictionary<string,object>());
        Engine.FULLMATCH("A|B", re_RegEx);
        var treeOk = Env.G.TryGetValue("RE_tree", out var tree) && tree is List<object> lst && lst.Count >= 1;
        T.Eq("RE_tree is List with >=1 element", true, treeOk);
    }

    // @pytest.mark.parametrize("bad", ["(",")",  "*", "+"])
    // def test_re_no_parse(bad): assert bad not in re_RegEx
    static void Test_RE_no_parse()
    {
        T.Section("test_re_simple · no parse");
        foreach (var bad in new[]{ "(", ")", "*", "+" }) {
            GLOBALS(new Dictionary<string,object>());
            T.NoMatch($"re_no_parse \"{bad}\"", bad, re_RegEx);
        }
    }

    // =========================================================================
    // test_json.py
    // =========================================================================

    static readonly string JSON_sample = @"{  ""list"":
      [ {
        ""id"": 1,
        ""first_name"": ""Jeanette"",
        ""last_name"": ""Penddreth"",
        ""email"": ""jpenddreth0@census.gov"",
        ""gender"": ""Female"",
        ""average"": +0.75,
        ""single"": true,
        ""ip_address"": ""26.58.193.2"",
        ""start_date"": ""2025-02-06""
        }
      , {
        ""id"": 2,
        ""first_name"": ""Giavani"",
        ""last_name"": ""Frediani"",
        ""email"": ""gfrediani1@senate.gov"",
        ""gender"": ""Male"",
        ""average"": -1.25,
        ""single"": false,
        ""ip_address"": ""229.179.4.212"",
        ""start_date"": ""2024-12-31""
        }
      ]
}";

    // Parsed data, built once
    static Dictionary<string, object>? _jsonRoot;
    static Dictionary<string, object> JsonRoot()
    {
        if (_jsonRoot != null) return _jsonRoot;
        GLOBALS(new Dictionary<string,object>());
        var r = Engine.FULLMATCH(JSON_sample, jRecognizer);
        if (r == null || !Env.G.TryGetValue("JSON_tree", out var raw))
            throw new Exception("JSON parse failed");
        _jsonRoot = (Dictionary<string, object>)Traverse((List<object>)raw);
        return _jsonRoot;
    }
    static List<object> JsonList() =>
        (List<object>)JsonRoot()["list"];
    static Dictionary<string, object> Rec(int i) =>
        (Dictionary<string, object>)JsonList()[i];

    // def test_json_recognizes(): assert JSON_sample in jRecognizer
    static void Test_JSON_recognizes()
    {
        T.Section("test_json · recognizes");
        GLOBALS(new Dictionary<string,object>());
        T.Match("JSON_sample recognized", JSON_sample, jRecognizer);
    }

    // def test_json_list_length(json_data): assert len(json_data.list) == 2
    static void Test_JSON_list_length()
    {
        T.Section("test_json · list length");
        T.Eq("list length == 2", 2, JsonList().Count);
    }

    // ── Record 0: Jeanette Penddreth ─────────────────────────────────────────
    // def test_r0_id          : assert json_data.list[0].id           == 1
    // def test_r0_first_name  : assert json_data.list[0].first_name   == "Jeanette"
    // def test_r0_last_name   : assert json_data.list[0].last_name    == "Penddreth"
    // def test_r0_email       : assert json_data.list[0].email        == "jpenddreth0@census.gov"
    // def test_r0_gender      : assert json_data.list[0].gender       == "Female"
    // def test_r0_average     : assert json_data.list[0].average      == approx(0.75)
    // def test_r0_single      : assert json_data.list[0].single       == True
    // def test_r0_ip_address  : assert json_data.list[0].ip_address   == "26.58.193.2"
    // def test_r0_start_date  : assert json_data.list[0].start_date   == datetime(2025,2,6)
    static void Test_JSON_record0()
    {
        T.Section("test_json · record 0 (Jeanette Penddreth)");
        var r = Rec(0);
        T.Eq("r0 id",           1,                          r["id"]);
        T.Eq("r0 first_name",   "Jeanette",                 r["first_name"]);
        T.Eq("r0 last_name",    "Penddreth",                r["last_name"]);
        T.Eq("r0 email",        "jpenddreth0@census.gov",   r["email"]);
        T.Eq("r0 gender",       "Female",                   r["gender"]);
        T.Eq("r0 average",      0.75,                       r["average"]);
        T.Eq("r0 single",       true,                       r["single"]);
        T.Eq("r0 ip_address",   "26.58.193.2",              r["ip_address"]);
        var d0 = ((int y,int m,int d,int hh,int mm,int ss))r["start_date"];
        T.Eq("r0 start_date", (2025,2,6,0,0,0), (d0.y,d0.m,d0.d,d0.hh,d0.mm,d0.ss));
    }

    // ── Record 1: Giavani Frediani ────────────────────────────────────────────
    // def test_r1_id          : assert json_data.list[1].id           == 2
    // def test_r1_first_name  : assert json_data.list[1].first_name   == "Giavani"
    // def test_r1_last_name   : assert json_data.list[1].last_name    == "Frediani"
    // def test_r1_email       : assert json_data.list[1].email        == "gfrediani1@senate.gov"
    // def test_r1_gender      : assert json_data.list[1].gender       == "Male"
    // def test_r1_average     : assert json_data.list[1].average      == approx(-1.25)
    // def test_r1_single      : assert json_data.list[1].single       == False
    // def test_r1_ip_address  : assert json_data.list[1].ip_address   == "229.179.4.212"
    // def test_r1_start_date  : assert json_data.list[1].start_date   == datetime(2024,12,31)
    static void Test_JSON_record1()
    {
        T.Section("test_json · record 1 (Giavani Frediani)");
        var r = Rec(1);
        T.Eq("r1 id",           2,                          r["id"]);
        T.Eq("r1 first_name",   "Giavani",                  r["first_name"]);
        T.Eq("r1 last_name",    "Frediani",                 r["last_name"]);
        T.Eq("r1 email",        "gfrediani1@senate.gov",    r["email"]);
        T.Eq("r1 gender",       "Male",                     r["gender"]);
        T.Eq("r1 average",      -1.25,                      r["average"]);
        T.Eq("r1 single",       false,                      r["single"]);
        T.Eq("r1 ip_address",   "229.179.4.212",            r["ip_address"]);
        var d1 = ((int y,int m,int d,int hh,int mm,int ss))r["start_date"];
        T.Eq("r1 start_date", (2024,12,31,0,0,0), (d1.y,d1.m,d1.d,d1.hh,d1.mm,d1.ss));
    }

    // ── jInt ─────────────────────────────────────────────────────────────────
    static void Test_JSON_jInt()
    {
        T.Section("test_json · jInt");
        foreach (var s in new[]{ "0","1","42","123","+7","-3","+99","-100" }) {
            GLOBALS(new Dictionary<string,object>());
            Engine.FULLMATCH(s, jInt);
            T.Eq($"jInt \"{s}\" captured", s.TrimStart('+'), (string)(Slot)S4._.jxN.TrimStart('+'));
        }
        // jInt does not match empty or letters
        T.Eq("jInt no match \"\"",    null, (object?)Engine.FULLMATCH("",    jInt));
        T.Eq("jInt no match \"abc\"", null, (object?)Engine.FULLMATCH("abc", jInt));
    }

    // ── jBoolVal ─────────────────────────────────────────────────────────────
    static void Test_JSON_jBoolVal()
    {
        T.Section("test_json · jBoolVal");
        GLOBALS(new Dictionary<string,object>());
        Engine.FULLMATCH("true",  jBoolVal); T.Eq("jBoolVal true",  "true",  (string)(Slot)S4._.jxVal);
        Engine.FULLMATCH("false", jBoolVal); T.Eq("jBoolVal false", "false", (string)(Slot)S4._.jxVal);
        T.Eq("jBoolVal no match \"yes\"", null, (object?)Engine.FULLMATCH("yes", jBoolVal));
    }

    // ── jRealVal ─────────────────────────────────────────────────────────────
    static void Test_JSON_jRealVal()
    {
        T.Section("test_json · jRealVal");
        foreach (var (s, expected) in new[]{
            ("+0.75", "+0.75"), ("-1.25", "-1.25"), ("3.14", "3.14"), ("0.0", "0.0") }) {
            GLOBALS(new Dictionary<string,object>());
            var r = Engine.FULLMATCH(s, jRealVal);
            T.Eq($"jRealVal \"{s}\" matched",  true, r != null);
            T.Eq($"jRealVal \"{s}\" captured", expected, (string)(Slot)S4._.jxVal);
        }
        // integers and bare signs do not match
        foreach (var s in new[]{ "42", "+", "abc" })
            T.Eq($"jRealVal no match \"{s}\"", null, (object?)Engine.FULLMATCH(s, jRealVal));
    }

    // ── jString ──────────────────────────────────────────────────────────────
    static void Test_JSON_jString()
    {
        T.Section("test_json · jString");
        foreach (var (input, expected) in new[]{
            ("\"hello\"",           "hello"),
            ("\"\"",                ""),
            ("\"hello world\"",     "hello world"),
            ("\"jpenddreth0@census.gov\"", "jpenddreth0@census.gov"),
            ("\"26.58.193.2\"",     "26.58.193.2"),
        }) {
            GLOBALS(new Dictionary<string,object>());
            var r = Engine.FULLMATCH(input, jString);
            T.Eq($"jString {input} matched",  true,     r != null);
            T.Eq($"jString {input} captured", expected, (string)(Slot)S4._.jxVal);
        }
        // not a string
        T.Eq("jString no match bare word", null, (object?)Engine.FULLMATCH("hello", jString));
    }

    // ── jDate ────────────────────────────────────────────────────────────────
    static void Test_JSON_jDate()
    {
        T.Section("test_json · jDate (via jDatetime)");
        foreach (var (input, y, m, d) in new[]{
            ("\"2025-02-06\"", 2025, 2,  6),
            ("\"2024-12-31\"", 2024, 12, 31),
            ("\"2000-01-01\"", 2000, 1,  1),
        }) {
            GLOBALS(new Dictionary<string,object>());
            var r = Engine.FULLMATCH(input, jDatetime);
            T.Eq($"jDatetime {input} matched", true, r != null);
            T.Eq($"jDatetime {input} YYYY", y.ToString(), (string)(Slot)S4._.jxYYYY);
            T.Eq($"jDatetime {input} MM",   m.ToString("D2"), (string)(Slot)S4._.jxMM);
            T.Eq($"jDatetime {input} DD",   d.ToString("D2"), (string)(Slot)S4._.jxDD);
        }
    }

    // ── jElement scalars ─────────────────────────────────────────────────────
    static void Test_JSON_jElement_scalars()
    {
        T.Section("test_json · jElement scalars");

        // integer
        GLOBALS(new Dictionary<string,object>());
        var r = Engine.SEARCH("1", jElement);
        T.Eq("jElement integer matched", true, r != null);

        // real
        GLOBALS(new Dictionary<string,object>());
        r = Engine.SEARCH("+0.75", jElement);
        T.Eq("jElement real matched", true, r != null);

        // bool
        GLOBALS(new Dictionary<string,object>());
        r = Engine.SEARCH("true", jElement);
        T.Eq("jElement bool matched", true, r != null);

        // string
        GLOBALS(new Dictionary<string,object>());
        r = Engine.SEARCH("\"hello\"", jElement);
        T.Eq("jElement string matched", true, r != null);

        // null
        GLOBALS(new Dictionary<string,object>());
        r = Engine.SEARCH("null", jElement);
        T.Eq("jElement null matched", true, r != null);
    }
    // =========================================================================
    // SnobolEnv / Slot / >> operator — new v12 API
    // =========================================================================
    static void Test_SnobolEnv()
    {
        T.Section("v12 · SnobolEnv _ and >> operator");

        // _.x = value and (string)_.x round-trip
        S4._.word = "hello";
        T.Eq("_.word set/get", "hello", (string)(Slot)S4._.word);

        // % Slot — conditional assignment — fires only on full match success
        S4._.w = "before";
        Engine.FULLMATCH("abc", POS(0) + (SPAN(ALPHA) % (Slot)S4._.w) + RPOS(0));
        T.Eq("% Slot fires on success", "abc", (string)(Slot)S4._.w);

        S4._.w = "before";
        Engine.FULLMATCH("abc123", POS(0) + (SPAN(ALPHA) % (Slot)S4._.w) + σ("NOPE") + RPOS(0));
        T.Eq("% Slot silent on failure", "before", (string)(Slot)S4._.w);

        // Slot implicit int conversion
        S4._.n = 42;
        int n = (Slot)S4._.n;
        T.Eq("Slot implicit int", 42, n);

        // Unset _.x returns "" — SNOBOL4 NULL semantics
        // Use a fresh env so we know the key is absent
        GLOBALS(new Dictionary<string,object>());
        string unset = (Slot)S4._.unset_key;
        T.Eq("unset slot is empty string", "", unset);
        int unsetInt = (Slot)S4._.unset_key;
        T.Eq("unset slot int is 0", 0, unsetInt);

        // * Slot — immediate assignment — fires even on outer failure
        S4._.imm = "before";
        Engine.FULLMATCH("abc123", POS(0) + (SPAN(ALPHA) * (Slot)S4._.imm) + σ("NOPE") + RPOS(0));
        T.Eq("* Slot fires immediately", "abc", (string)(Slot)S4._.imm);

        // ζ(Slot) — pattern stored on _ resolved at match time
        S4._.pat = SPAN(DIGITS);
        T.Match("ζ(Slot) resolves PATTERN", "123", POS(0) + ζ((Slot)S4._.pat) + RPOS(0));
        S4._.pat = SPAN(ALPHA);
        T.Match("ζ(Slot) re-resolves after update", "abc", POS(0) + ζ((Slot)S4._.pat) + RPOS(0));
    }

}
}