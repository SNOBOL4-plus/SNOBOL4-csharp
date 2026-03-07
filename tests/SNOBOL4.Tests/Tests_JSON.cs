// Tests_JSON.cs — mirrors test_json.py
//
// Builds a complete JSON recognizer from SNOBOL4 patterns and verifies it
// against a two-record sample.  The grammar uses Shift/Reduce/Pop to build
// a parse tree, then Traverse() converts it to plain C# collections.
//
// All capture uses the delegate API introduced in Stage 15:
//   P % (v => local = v)   — conditional capture
//   P * (v => local = v)   — immediate capture (needed before Λ guards)
// No Slot, no Env, no FreshEnv().
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Globalization;
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public class Tests_JSON : TestBase
    {
        // ── Whitespace helper ─────────────────────────────────────────────────
        // WS(s) — optional whitespace then the literal s
        static PATTERN WS(string s) => (SPAN(" \t\r\n") | ε()) + σ(s);

        // ── Scalar patterns ───────────────────────────────────────────────────
        // Each factory method creates a fresh pattern with its own closed-over
        // locals.  This is required because * (immediate) fires on every cursor
        // attempt including failed ones, so static fields would be polluted.

        static PATTERN MakejInt(Action<string> setN)
            => (FENCE(σ("+") | σ("-") | ε()) + SPAN(DIGITS)) % setN;

        static PATTERN MakejEscChar(Action<string> setHex) =>
              σ("\\")
            + (   ANY("ntbrf\"\\/'" )
                | ANY("01234567") + FENCE(ANY("01234567") | ε())
                | ANY("0123") + ANY("01234567") + ANY("01234567")
                | σ("u") + SPAN("0123456789ABCDEFabcdef") % setHex
              );

        // jString — captures into setVal; hex scratch via setHex
        static PATTERN MakejString(Action<string> setVal, Action<string> setHex)
        {
            var esc = MakejEscChar(setHex);
            return σ("\"")
                 + ((ARBNO(BREAK("\"\\\n") | esc)) % setVal)
                 + σ("\"");
        }

        // ── Grammar factory ───────────────────────────────────────────────────
        // Returns the top-level recognizer pattern and delivers the finished
        // parse tree via onTree.  All locals are captured by closure.

        static PATTERN MakeRecognizer(Action<List<object>> onTree)
        {
            // per-parse scratch variables
            string jxN    = "";
            string jxVal  = "";
            string jxVar  = "";
            string jxHex  = "";
            string jxYYYY = "", jxMM = "", jxDD  = "";
            string jxhh   = "", jxmm = "", jxss  = "";
            string jxN2   = "", jxN3 = "", jxN4  = "";
            // Snapshot variables — written conditionally (%) alongside the
            // immediate (*) writes, so committed values are backtrack-clean.
            string sYYYY = "", sMM = "", sDD = "";
            string shh   = "", smm = "", sss = "";
            (int y,int mo,int d,int hh,int mi,int ss) dtSnap = default;

            // ── Scalar sub-patterns ───────────────────────────────────────────
            var jInt  = MakejInt(v => jxN = v);

            // Date/time components:
            //   * writes immediately so the Λ length guard can fire right away
            //   % writes conditionally so the committed value is backtrack-clean
            var jYYYY = SPAN(DIGITS) * (v => jxYYYY = v) % (v => sYYYY = v) + Λ(() => jxYYYY.Length == 4);
            var jMM   = SPAN(DIGITS) * (v => jxMM   = v) % (v => sMM   = v) + Λ(() => jxMM.Length   == 2);
            var jDD   = SPAN(DIGITS) * (v => jxDD   = v) % (v => sDD   = v) + Λ(() => jxDD.Length   == 2);
            var jhh   = SPAN(DIGITS) * (v => jxhh   = v) % (v => shh   = v) + Λ(() => jxhh.Length   == 2);
            var jmm2  = SPAN(DIGITS) * (v => jxmm   = v) % (v => smm   = v) + Λ(() => jxmm.Length   == 2);
            var jss   = SPAN(DIGITS) * (v => jxss   = v) % (v => sss   = v) + Λ(() => jxss.Length   == 2);
            var jNum3 = SPAN(DIGITS) * (v => jxN3   = v) + Λ(() => jxN3.Length   == 3);
            var jNum4 = SPAN(DIGITS) * (v => jxN4   = v) + Λ(() => jxN4.Length   == 4);
            var jNum2 = SPAN(DIGITS) * (v => jxN2   = v) + Λ(() => jxN2.Length   == 2);

            var jDate = jYYYY + σ("-") + jMM + σ("-") + jDD;
            var jTime = jhh   + σ(":") + jmm2 + σ(":") + jss;

            var jDatetime =
                  σ("\"")
                + (   jDate + σ("T") + jTime + σ(".") + (jNum3 | ε()) + σ("Z")
                    | jDate + σ("T") + jTime + σ("+") + jNum4
                    | jDate + σ("T") + jTime + σ("+") + jNum2 + σ(":") + jNum2
                    | jDate + σ("T") + jTime
                    | jDate + σ(" ") + jTime
                    | jDate
                  )
                + σ("\"")
                + λ(() => {
                    dtSnap = ( int.Parse(sYYYY), int.Parse(sMM), int.Parse(sDD),
                               shh == "" ? 0 : int.Parse(shh),
                               smm == "" ? 0 : int.Parse(smm),
                               sss == "" ? 0 : int.Parse(sss) );
                  });

            var jString   = MakejString(v => jxVal = v, v => jxHex = v);
            var jNullVal  = σ("null") + ε() % (v => jxVal = v);
            var jTrueFalse = (σ("true") | σ("false")) % (v => jxVal = v);
            var jIdent    = ANY(UCASE + "_" + LCASE)
                          + FENCE(SPAN(UCASE + "_" + LCASE + DIGITS) | ε());

            var jStrVal  = jString;
            var jBoolVal = jTrueFalse | σ("\"") + jTrueFalse + σ("\"");
            var jRealVal = ((σ("+") | σ("-") | ε()) + SPAN(DIGITS) + σ(".") + SPAN(DIGITS))
                          % (v => jxVal = v);
            var jIntVal  = (jInt % (v => jxVal = v))
                         | σ("\"") + (jInt % (v => jxVal = v)) + σ("\"");

            var jVar = WS("\"") + (jIdent | jInt) % (v => jxVar = v) + σ("\"");

            // ── Datetime helper ───────────────────────────────────────────────
            (int y, int m, int d, int hh, int mm, int ss) ParseDatetime() =>
                ( int.Parse(jxYYYY), int.Parse(jxMM), int.Parse(jxDD),
                  jxhh == "" ? 0 : int.Parse(jxhh),
                  jxmm == "" ? 0 : int.Parse(jxmm),
                  jxss == "" ? 0 : int.Parse(jxss) );

            // ── Mutually recursive element / field / object / array ───────────
            PATTERN? jElement = null;
            PATTERN? jField   = null;
            PATTERN? jObject  = null;
            PATTERN? jArray   = null;

            jElement =
                  WS("")
                + (   jRealVal  + Shift("Real",     () => (object)double.Parse(jxVal, CultureInfo.InvariantCulture))
                    | jIntVal   + Shift("Integer",  () => (object)int.Parse(jxVal))
                    | jBoolVal  + Shift("Bool",     () => (object)(jxVal == "true"))
                    | jDatetime + Shift("Datetime", () => (object)dtSnap)
                    | jStrVal   + Shift("String",   () => (object)jxVal)
                    | jNullVal  + Shift("Null")
                    | ζ(() => jArray!)
                    | ζ(() => jObject!)
                  );

            jField =
                  jVar + Shift("Name", () => (object)jxVar)
                + WS(":") + ζ(() => jElement!)
                + Reduce("Attribute", 2);

            jObject =
                  WS("{") + nPush()
                + π(ζ(() => jField!) + nInc() + ARBNO(WS(",") + ζ(() => jField!) + nInc()))
                + WS("}") + Reduce("Object") + nPop()
                + FENCE();

            jArray =
                  WS("[") + nPush()
                + π(ζ(() => jElement!) + nInc() + ARBNO(WS(",") + ζ(() => jElement!) + nInc()))
                + WS("]") + Reduce("Array") + nPop()
                + FENCE();

            var jJSON       = ζ(() => jObject!) + Reduce("JSON", 1);
            var jRecognizer =
                  POS(0) + FENCE() + jJSON
                + (SPAN(" \t\r\n") | ε())
                + Pop(onTree)
                + RPOS(0);

            return jRecognizer;
        }

        // ── Tree traversal ────────────────────────────────────────────────────

        static object Traverse(List<object> tree)
        {
            var tag = (string)tree[0];
            switch (tag) {
                case "JSON":   return Traverse((List<object>)tree[1]);
                case "Object": {
                    var d = new Dictionary<string, object>();
                    for (int i = 1; i < tree.Count; i++) {
                        var attr = (List<object>)tree[i];
                        var key  = (string)Traverse((List<object>)attr[1]);
                        var val  = Traverse((List<object>)attr[2]);
                        d[key] = val;
                    }
                    return d;
                }
                case "Array": {
                    var lst = new List<object>();
                    for (int i = 1; i < tree.Count; i++)
                        lst.Add(Traverse((List<object>)tree[i]));
                    return lst;
                }
                case "Attribute": return tree;   // handled by Object case directly
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

        // ── Sample JSON ───────────────────────────────────────────────────────

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

        // ── Parsed result (lazy, cached) ──────────────────────────────────────

        static Dictionary<string, object>? _jsonRoot;
        static Dictionary<string, object> JsonRoot()
        {
            if (_jsonRoot != null) return _jsonRoot;
            List<object>? raw = null;
            var pat = MakeRecognizer(t => raw = t);
            var r   = Engine.FULLMATCH(JSON_sample, pat);
            if (r == null || raw == null)
                throw new Exception("JSON parse failed");
            _jsonRoot = (Dictionary<string, object>)Traverse(raw);
            return _jsonRoot;
        }

        static List<object>               JsonList() => (List<object>)JsonRoot()["list"];
        static Dictionary<string, object> Rec(int i) => (Dictionary<string, object>)JsonList()[i];

        // ── Recognition ───────────────────────────────────────────────────────

        [Fact]
        public void json_recognizes_sample()
        {
            List<object>? tree = null;
            var pat = MakeRecognizer(t => tree = t);
            AssertMatch(JSON_sample, pat);
            Assert.NotNull(tree);
        }

        [Fact]
        public void json_list_length_is_2() =>
            Assert.Equal(2, JsonList().Count);

        // ── Record 0: Jeanette Penddreth ──────────────────────────────────────

        [Fact] public void r0_id()         => Assert.Equal(1,                        Rec(0)["id"]);
        [Fact] public void r0_first_name() => Assert.Equal("Jeanette",               Rec(0)["first_name"]);
        [Fact] public void r0_last_name()  => Assert.Equal("Penddreth",              Rec(0)["last_name"]);
        [Fact] public void r0_email()      => Assert.Equal("jpenddreth0@census.gov", Rec(0)["email"]);
        [Fact] public void r0_gender()     => Assert.Equal("Female",                 Rec(0)["gender"]);
        [Fact] public void r0_average()    => Assert.Equal(0.75,                     Rec(0)["average"]);
        [Fact] public void r0_single()     => Assert.Equal(true,                     Rec(0)["single"]);
        [Fact] public void r0_ip_address() => Assert.Equal("26.58.193.2",            Rec(0)["ip_address"]);

        [Fact]
        public void r0_start_date()
        {
            var d = ((int y, int m, int d, int hh, int mm, int ss))Rec(0)["start_date"];
            Assert.Equal((2025, 2, 6, 0, 0, 0), (d.y, d.m, d.d, d.hh, d.mm, d.ss));
        }

        // ── Record 1: Giavani Frediani ────────────────────────────────────────

        [Fact] public void r1_id()         => Assert.Equal(2,                        Rec(1)["id"]);
        [Fact] public void r1_first_name() => Assert.Equal("Giavani",                Rec(1)["first_name"]);
        [Fact] public void r1_last_name()  => Assert.Equal("Frediani",               Rec(1)["last_name"]);
        [Fact] public void r1_email()      => Assert.Equal("gfrediani1@senate.gov",  Rec(1)["email"]);
        [Fact] public void r1_gender()     => Assert.Equal("Male",                   Rec(1)["gender"]);
        [Fact] public void r1_average()    => Assert.Equal(-1.25,                    Rec(1)["average"]);
        [Fact] public void r1_single()     => Assert.Equal(false,                    Rec(1)["single"]);
        [Fact] public void r1_ip_address() => Assert.Equal("229.179.4.212",          Rec(1)["ip_address"]);

        [Fact]
        public void r1_start_date()
        {
            var d = ((int y, int m, int d, int hh, int mm, int ss))Rec(1)["start_date"];
            Assert.Equal((2024, 12, 31, 0, 0, 0), (d.y, d.m, d.d, d.hh, d.mm, d.ss));
        }

        // ── Scalar pattern unit tests ─────────────────────────────────────────

        [Theory]
        [InlineData("0")]   [InlineData("1")]   [InlineData("42")]  [InlineData("123")]
        [InlineData("+7")]  [InlineData("-3")]  [InlineData("+99")] [InlineData("-100")]
        public void jInt_matches(string s)
        {
            string cap = "";
            var pat = MakejInt(v => cap = v);
            Assert.NotNull(Engine.FULLMATCH(s, pat));
            Assert.Equal(s, cap);
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        public void jInt_no_match(string s)
        {
            string cap = "";
            Assert.Null(Engine.FULLMATCH(s, MakejInt(v => cap = v)));
        }

        [Theory]
        [InlineData("+0.75", "+0.75")]
        [InlineData("-1.25", "-1.25")]
        [InlineData("3.14",  "3.14")]
        [InlineData("0.0",   "0.0")]
        public void jRealVal_captures(string input, string expected)
        {
            string cap = "";
            var pat = ((σ("+") | σ("-") | ε()) + SPAN(DIGITS) + σ(".") + SPAN(DIGITS))
                     % (v => cap = v);
            Assert.NotNull(Engine.FULLMATCH(input, pat));
            Assert.Equal(expected, cap);
        }

        [Theory]
        [InlineData("\"hello\"",                  "hello")]
        [InlineData("\"\"",                       "")]
        [InlineData("\"hello world\"",            "hello world")]
        [InlineData("\"jpenddreth0@census.gov\"", "jpenddreth0@census.gov")]
        [InlineData("\"26.58.193.2\"",            "26.58.193.2")]
        public void jString_captures(string input, string expected)
        {
            string cap = "", hex = "";
            var pat = MakejString(v => cap = v, v => hex = v);
            Assert.NotNull(Engine.FULLMATCH(input, pat));
            Assert.Equal(expected, cap);
        }

        [Theory]
        [InlineData("true",  true)]
        [InlineData("false", false)]
        public void jBoolVal_captures(string input, bool expected)
        {
            string cap = "";
            var pat = (σ("true") | σ("false")) % (v => cap = v);
            Assert.NotNull(Engine.FULLMATCH(input, pat));
            Assert.Equal(expected, cap == "true");
        }

        [Theory]
        [InlineData("\"2025-02-06\"", 2025, 2,  6)]
        [InlineData("\"2024-12-31\"", 2024, 12, 31)]
        [InlineData("\"2000-01-01\"", 2000, 1,  1)]
        public void jDatetime_captures_date(string input, int y, int m, int d)
        {
            List<object>? tree = null;
            // Use the full recognizer to exercise the datetime branch
            var pat = MakeRecognizer(t => tree = t);
            // wrap in a minimal JSON object to route through jDatetime
            var json = $"{{\"dt\": {input}}}";
            Assert.NotNull(Engine.FULLMATCH(json, pat));
            // date is in tree; just check via Traverse
            var root = (Dictionary<string,object>)Traverse(tree!);
            var dt   = ((int ry, int rm, int rd, int rhh, int rmm, int rss))root["dt"];
            Assert.Equal(y, dt.ry);
            Assert.Equal(m, dt.rm);
            Assert.Equal(d, dt.rd);
        }

        [Theory]
        [InlineData("1")]
        [InlineData("+0.75")]
        [InlineData("true")]
        [InlineData("\"hello\"")]
        public void jElement_matches_scalar(string input)
        {
            // Build a minimal recognizer context and search for jElement
            // We cannot easily isolate jElement, so wrap in a trivial array.
            List<object>? tree = null;
            var pat = MakeRecognizer(t => tree = t);
            Assert.NotNull(Engine.FULLMATCH("{\"x\": " + input + "}", pat));
            Assert.NotNull(tree);
        }
    }
}
