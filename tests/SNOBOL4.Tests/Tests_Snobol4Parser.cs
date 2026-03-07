// Tests_Snobol4Parser.cs — SNOBOL4 parser (ported from transl8r_SNOBOL4.py)
//
// Implements the Compiland grammar from the Python translator, ported to the
// v16/v17 C# delegate API.  The single test parses roman.inc and asserts that
// the syntax tree is well-formed, then prints it to the test output.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;
using Xunit.Abstractions;

namespace SNOBOL4.Tests
{
    public class Tests_Snobol4Parser : TestBase
    {
        readonly ITestOutputHelper _out;
        public Tests_Snobol4Parser(ITestOutputHelper output) => _out = output;

        // ── Character classes ─────────────────────────────────────────────────
        const string ALNUM_DOT_UNDER = "." + DIGITS + UCASE + "_" + LCASE;

        // ── Shared capture variables ──────────────────────────────────────────
        // These are per-parse locals threaded via closures.  We use a single
        // ParseContext object so all patterns share the same binding.

        class Ctx {
            public string tx  = "";
            public string nm  = "";
            public string sor = "";          // "S" or "F"
            public string brackets = "";     // "()" or "<>"
            public List<object>? tree = null;
        }

        // ── Whitespace patterns ───────────────────────────────────────────────

        static PATTERN MakeNl()  => σ("\n");

        // ς — significant whitespace: spaces/tabs, with optional line continuation
        static PATTERN Makeς(Ctx c) =>
              SPAN(" \t") + FENCE(MakeNl() + (σ("+") | σ(".")) + FENCE(SPAN(" \t") | ε()) | ε())
            | MakeNl()    + (σ("+") | σ(".")) + FENCE(SPAN(" \t") | ε());

        // η — optional whitespace
        static PATTERN Makeη(Ctx c) => Makeς(c) | ε();

        // ── Lexical patterns ──────────────────────────────────────────────────

        static PATTERN MakeInteger(Ctx c) => SPAN(DIGITS) % (v => c.tx = v);

        static PATTERN MakeDQ(Ctx c) =>
            (σ("\"") + BREAK("\"\n") + σ("\"")) % (v => c.tx = v);

        static PATTERN MakeSQ(Ctx c) =>
            (σ("'") + BREAK("'\n") + σ("'")) % (v => c.tx = v);

        static PATTERN MakeString(Ctx c) => MakeSQ(c) | MakeDQ(c);

        static PATTERN MakeReal(Ctx c) =>
            ( SPAN(DIGITS)
            + (σ(".") + FENCE(SPAN(DIGITS) | ε()) | ε())
            + (σ("E") | σ("e"))
            + (σ("+") | σ("-") | ε())
            + SPAN(DIGITS)
            | SPAN(DIGITS) + σ(".") + FENCE(SPAN(DIGITS) | ε())
            ) % (v => c.tx = v);

        static PATTERN MakeId(Ctx c) =>
            (ANY(UCASE + LCASE) + FENCE(SPAN(ALNUM_DOT_UNDER) | ε())) % (v => c.nm = v);

        // ── Keyword sets ──────────────────────────────────────────────────────

        static readonly HashSet<string> Functions = new() {
            "ANY","APPLY","ARBNO","ARG","ARRAY","ATAN","BACKSPACE","BREAK","BREAKX",
            "CHAR","CHOP","CLEAR","CODE","COLLECT","CONVERT","COPY","COS","DATA",
            "DATATYPE","DATE","DEFINE","DETACH","DIFFER","DUMP","DUPL","EJECT",
            "ENDFILE","EQ","EVAL","EXIT","EXP","FENCE","FIELD","GE","GT","HOST",
            "IDENT","INPUT","INTEGER","ITEM","LE","LEN","LEQ","LGE","LGT","LLE",
            "LLT","LN","LNE","LOAD","LOCAL","LPAD","LT","NE","NOTANY","OPSYN","OUTPUT",
            "POS","PROTOTYPE","REMDR","REPLACE","REVERSE","REWIND","RPAD","RPOS",
            "RSORT","RTAB","SET","SETEXIT","SIN","SIZE","SORT","SPAN","SQRT","STOPTR",
            "SUBSTR","TAB","TABLE","TAN","TIME","TRACE","TRIM","UNLOAD"
        };
        static readonly HashSet<string> BuiltinVars  = new() { "ABORT","ARB","BAL","FAIL","FENCE","INPUT","OUTPUT","REM","TERMINAL" };
        static readonly HashSet<string> SpecialNms   = new() { "ABORT","CONTINUE","END","FRETURN","NRETURN","RETURN","SCONTINUE","START" };
        static readonly HashSet<string> ProtKwds     = new() { "ABORT","ALPHABET","ARB","BAL","FAIL","FENCE","FILE","FNCLEVEL",
                                                                "LASTFILE","LASTLINE","LASTNO","LCASE","LINE","REM","RTNTYPE",
                                                                "STCOUNT","STNO","SUCCEED","UCASE" };
        static readonly HashSet<string> UnprotKwds   = new() { "ABEND","ANCHOR","CASE","CODE","COMPARE","DUMP","ERRLIMIT",
                                                                "ERRTEXT","ERRTYPE","FTRACE","INPUT","MAXLNGTH","OUTPUT",
                                                                "PROFILE","STLIMIT","TRACE","TRIM","FULLSCAN" };

        // φ-based keyword patterns — match whole-word and capture into c.nm
        static PATTERN MakeFunction (Ctx c) => φ(@"\b(?<nm>" + string.Join("|", Functions)  + @")\b", (n,v) => { if(n=="nm") c.nm=v; });
        static PATTERN MakeBuiltinVar(Ctx c) => φ(@"\b(?<nm>" + string.Join("|", BuiltinVars)+ @")\b", (n,v) => { if(n=="nm") c.nm=v; });
        static PATTERN MakeSpecialNm (Ctx c) => φ(@"\b(?<nm>" + string.Join("|", SpecialNms) + @")\b", (n,v) => { if(n=="nm") c.nm=v; });
        static PATTERN MakeProtKwd   (Ctx c) => φ(@"\&(?<nm>" + string.Join("|", ProtKwds)   + @")\b", (n,v) => { if(n=="nm") c.nm=v; });
        static PATTERN MakeUnprotKwd (Ctx c) => φ(@"\&(?<nm>" + string.Join("|", UnprotKwds) + @")\b", (n,v) => { if(n=="nm") c.nm=v; });

        // ── Operator delimiters (τ) ───────────────────────────────────────────

        static PATTERN τ(string op, Ctx c) {
            var η = Makeη(c);
            var ς = Makeς(c);
            return op switch {
                "="  => ς + σ("=")  + ς,
                "?"  => ς + σ("?")  + ς,
                "|"  => ς + σ("|")  + ς,
                "+"  => ς + σ("+")  + ς,
                "-"  => ς + σ("-")  + ς,
                "/"  => ς + σ("/")  + ς,
                "*"  => ς + σ("*")  + ς,
                "^"  => ς + σ("^")  + ς,
                "!"  => ς + σ("!")  + ς,
                "**" => ς + σ("**") + ς,
                "$"  => ς + σ("$")  + ς,
                "."  => ς + σ(".")  + ς,
                "&"  => ς + σ("&")  + ς,
                "@"  => ς + σ("@")  + ς,
                "#"  => ς + σ("#")  + ς,
                "%"  => ς + σ("%")  + ς,
                "~"  => ς + σ("~")  + ς,
                ","  => η + σ(",")  + η,
                "("  => σ("(")      + η,
                "["  => σ("[")      + η,
                "<"  => σ("<")      + η,
                ")"  => η + σ(")"),
                "]"  => η + σ("]"),
                ">"  => η + σ(">"),
                _    => throw new Exception($"τ: unknown op '{op}'")
            };
        }

        // ── Expression grammar factory ────────────────────────────────────────
        // Everything is built fresh per parse call so closures capture the
        // correct Ctx.  Forward refs use ζ(() => ref!).

        class Grammar {
            public PATTERN? Expr    = null;
            public PATTERN? XList   = null;
            public PATTERN? ExprList= null;
            public PATTERN? Expr16  = null;
            public PATTERN? Expr14  = null;
            public PATTERN? Expr13  = null;
            public PATTERN? Expr12  = null;
            public PATTERN? Expr11  = null;
            public PATTERN? Expr10  = null;
            public PATTERN? Expr9   = null;
            public PATTERN? Expr8   = null;
            public PATTERN? Expr7   = null;
            public PATTERN? Expr6   = null;
            public PATTERN? Expr5   = null;
            public PATTERN? X4      = null;
            public PATTERN? Expr4   = null;
            public PATTERN? X3      = null;
            public PATTERN? Expr3   = null;
            public PATTERN? Expr2   = null;
            public PATTERN? Expr1   = null;
            public PATTERN? Expr0   = null;
            public PATTERN? Command = null;
            public PATTERN? Commands= null;
            // Goto helpers
            public PATTERN? Target  = null;
            public PATTERN? SorF    = null;
            public PATTERN? Goto    = null;
            // Statement parts
            public PATTERN? Stmt    = null;
            public PATTERN? Compiland = null;
        }

        static Grammar BuildGrammar(Ctx c) {
            var g  = new Grammar();
            var nl = MakeNl();

            // Lexicals
            var Id         = MakeId(c);
            var Integer    = MakeInteger(c);
            var String_    = MakeString(c);
            var Real       = MakeReal(c);
            var Function   = MakeFunction(c);
            var BuiltinVar = MakeBuiltinVar(c);
            var SpecialNm  = MakeSpecialNm(c);
            var ProtKwd    = MakeProtKwd(c);
            var UnprotKwd  = MakeUnprotKwd(c);
            var η          = Makeη(c);
            var ς          = Makeς(c);

            // ── Expr17 (primary) ──────────────────────────────────────────────
            var Expr17 = FENCE(
                  ( nPush()
                  + τ("(", c) + ζ(() => g.Expr!)
                  + ( τ(",", c) + ζ(() => g.XList!) + Reduce(",", -2)
                    | ε()                            + Reduce("()", 1)
                    )
                  + τ(")", c)
                  + nPop()
                  )
                | Function   + Shift("Function",  () => (object)c.nm.ToUpper())
                             + τ("(", c) + ζ(() => g.ExprList!) + τ(")", c)
                             + Reduce("Call", 2)
                | Id         + Shift("Id",         () => (object)c.nm)
                             + τ("(", c) + ζ(() => g.ExprList!) + τ(")", c)
                             + Reduce("Call", 2)
                | BuiltinVar + Shift("BuiltinVar", () => (object)c.nm)
                | SpecialNm  + Shift("SpecialNm",  () => (object)c.nm)
                | Id         + Shift("Id",         () => (object)c.nm)
                | String_              % (v => c.tx = v) + Shift("String",  () => (object)c.tx)
                | Real                 % (v => c.tx = v) + Shift("Real",    () => (object)c.tx)
                | Integer              % (v => c.tx = v) + Shift("Integer", () => (object)c.tx)
            );

            // ── Expr16 (subscript/indirect) ───────────────────────────────────
            g.Expr16 =
                  nInc()
                + ( τ("[", c) + ζ(() => g.ExprList!) + τ("]", c)
                  | τ("<", c) + ζ(() => g.ExprList!) + τ(">", c)
                  )
                + FENCE(ζ(() => g.Expr16!) | ε());

            var Expr15 = Expr17 + FENCE(nPush() + g.Expr16 + Reduce("[]", -2) + nPop() | ε());

            // ── Expr14 (unary prefix operators) ──────────────────────────────
            g.Expr14 =
                  σ("@") + ζ(() => g.Expr14!) + Reduce("@", 1)
                | σ("~") + ζ(() => g.Expr14!) + Reduce("~", 1)
                | σ("?") + ζ(() => g.Expr14!) + Reduce("?", 1)
                | ProtKwd                       + Shift("ProtKwd",   () => (object)c.nm)
                | UnprotKwd                     + Shift("UnprotKwd", () => (object)c.nm)
                | σ("&") + ζ(() => g.Expr14!) + Reduce("&", 1)
                | σ("+") + ζ(() => g.Expr14!) + Reduce("+", 1)
                | σ("-") + ζ(() => g.Expr14!) + Reduce("-", 1)
                | σ("*") + ζ(() => g.Expr14!) + Reduce("*", 1)
                | σ("$") + ζ(() => g.Expr14!) + Reduce("$", 1)
                | σ(".") + ζ(() => g.Expr14!) + Reduce(".", 1)
                | σ("!") + ζ(() => g.Expr14!) + Reduce("!", 1)
                | σ("%") + ζ(() => g.Expr14!) + Reduce("%", 1)
                | σ("/") + ζ(() => g.Expr14!) + Reduce("/", 1)
                | σ("#") + ζ(() => g.Expr14!) + Reduce("#", 1)
                | σ("=") + ζ(() => g.Expr14!) + Reduce("=", 1)
                | σ("|") + ζ(() => g.Expr14!) + Reduce("|", 1)
                | Expr15;

            // ── Binary/precedence chain ───────────────────────────────────────
            g.Expr13 = g.Expr14 + FENCE(τ("~",c) + ζ(()=>g.Expr13!) + Reduce("~",2) | ε());
            g.Expr12 = g.Expr13 + FENCE(τ("$",c) + ζ(()=>g.Expr12!) + Reduce("$",2)
                                       |τ(".",c)  + ζ(()=>g.Expr12!) + Reduce(".",2) | ε());
            g.Expr11 = g.Expr12 + FENCE((τ("^",c)|τ("!",c)|τ("**",c)) + ζ(()=>g.Expr11!) + Reduce("^",2) | ε());
            g.Expr10 = g.Expr11 + FENCE(τ("%",c) + ζ(()=>g.Expr10!) + Reduce("%",2) | ε());
            g.Expr9  = g.Expr10 + FENCE(τ("*",c) + ζ(()=>g.Expr9!)  + Reduce("*",2) | ε());
            g.Expr8  = g.Expr9  + FENCE(τ("/",c) + ζ(()=>g.Expr8!)  + Reduce("/",2) | ε());
            g.Expr7  = g.Expr8  + FENCE(τ("#",c) + ζ(()=>g.Expr7!)  + Reduce("#",2) | ε());
            g.Expr6  = g.Expr7  + FENCE(τ("+",c) + ζ(()=>g.Expr6!)  + Reduce("+",2)
                                        |τ("-",c) + ζ(()=>g.Expr6!)  + Reduce("-",2) | ε());
            g.Expr5  = g.Expr6  + FENCE(τ("@",c) + ζ(()=>g.Expr5!)  + Reduce("@",2) | ε());

            // ── Concatenation (space-separated) ──────────────────────────────
            g.X4    = nInc() + g.Expr5 + FENCE(ς + ζ(() => g.X4!)  | ε());
            g.Expr4 = nPush() + g.X4  + Reduce("..", -1) + nPop();
            g.X3    = nInc() + g.Expr4 + FENCE(τ("|",c) + ζ(()=>g.X3!) | ε());
            g.Expr3 = nPush() + g.X3  + Reduce("|", -1) + nPop();
            g.Expr2 = g.Expr3 + FENCE(τ("&",c) + ζ(()=>g.Expr2!) + Reduce("&",2) | ε());
            g.Expr1 = g.Expr2 + FENCE(τ("?",c) + ζ(()=>g.Expr1!) + Reduce("?",2) | ε());
            g.Expr0 = g.Expr1 + FENCE(τ("=",c) + ζ(()=>g.Expr0!) + Reduce("=",2) | ε());
            g.Expr  = g.Expr0;

            // ── ExprList ──────────────────────────────────────────────────────
            g.XList    = nInc() + (g.Expr | Shift()) + FENCE(τ(",",c) + ζ(()=>g.XList!) | ε());
            g.ExprList = nPush() + g.XList + Reduce("ExprList", -1) + nPop();

            // ── Goto, Label, Control, Comment ─────────────────────────────────
            var SGoto = (σ("S") | σ("s")) + λ(() => c.sor = "S");
            var FGoto = (σ("F") | σ("f")) + λ(() => c.sor = "F");
            g.SorF   = SGoto | FGoto;

            g.Target =
                  τ("(", c) + λ(() => c.brackets="()") + g.Expr + τ(")", c)
                | τ("<", c) + λ(() => c.brackets="<>") + g.Expr + τ(">", c);

            g.Goto = η + σ(":")
                + η
                + FENCE(
                    g.Target
                        + Reduce(() => (object)c.brackets, 1)
                        + Shift()
                  | g.SorF + g.Target
                        + Reduce(() => (object)(c.sor + c.brackets), 1)
                        + FENCE(η + g.SorF + g.Target
                            + Reduce(() => (object)(c.sor + c.brackets), 1)
                          | Shift())
                  );

            var Control = σ("-") + BREAK("\n;") % (v => c.tx = v);
            var Comment = σ("*") + BREAK("\n")  % (v => c.tx = v);
            var Label   = BREAK(" \t\n;") % (v => c.tx = v) + Shift("Label", () => (object)c.tx);

            // ── Statement ─────────────────────────────────────────────────────
            g.Stmt =
                  Label
                + ( ς
                  + g.Expr14 + Reduce("Subject", 1)
                  + FENCE(
                      Shift()
                    + ς
                    + ( σ("=") + Shift("=") + ς + g.Expr
                      | σ("=") + Shift("=") + Shift()
                      )
                    | (τ("?",c) | ς) + g.Expr1
                    + FENCE(
                        ς
                      + ( σ("=") + Shift("=") + ς + g.Expr
                        | σ("=") + Shift("=") + Shift()
                        )
                      | Shift() + Shift()
                      )
                    | Shift() + Shift() + Shift()
                    )
                  | Shift() + Shift() + Shift() + Shift()
                  )
                + FENCE(g.Goto | Shift() + Shift())
                + η;

            // ── Command ───────────────────────────────────────────────────────
            g.Command =
                  nInc()
                + FENCE(
                    Comment + Shift("comment", () => (object)c.tx) + Reduce("Comment", 1) + nl
                  | Control + Shift("control", () => (object)c.tx) + Reduce("Control", 1) + (nl | σ(";"))
                  | g.Stmt  + Reduce("Stmt", 7) + (nl | σ(";"))
                  );

            g.Commands = ζ(() => g.Command!) + FENCE(ζ(() => g.Commands!) | ε());

            // ── Compiland (top level) ─────────────────────────────────────────
            g.Compiland =
                  POS(0)
                + nPush()
                + ARBNO(g.Command)
                + Reduce("Parse", -1)
                + ( φ(@"[Ee][Nn][Dd]\b", (_,__)=>{}) + BREAK("\n") + nl
                  + ARBNO(BREAK("\n") + nl)
                  | ε()
                  )
                + nPop()
                + Pop(t => c.tree = t)
                + RPOS(0);

            return g;
        }

        // ── Pretty-printer ────────────────────────────────────────────────────

        static string PrettyPrint(object? node, int indent = 0) {
            var sb = new StringBuilder();
            var pad = new string(' ', indent * 2);
            if (node is List<object> list) {
                if (list.Count == 0) { sb.Append(pad + "[]"); return sb.ToString(); }
                var tag = list[0]?.ToString() ?? "?";
                // Leaf nodes: print on one line
                if (list.Count == 2 && list[1] is string) {
                    sb.Append($"{pad}[{tag}, \"{list[1]}\"]");
                } else {
                    sb.AppendLine($"{pad}[{tag}");
                    for (int i = 1; i < list.Count; i++)
                        sb.AppendLine(PrettyPrint(list[i], indent + 1));
                    sb.Append($"{pad}]");
                }
            } else {
                sb.Append($"{pad}{node}");
            }
            return sb.ToString();
        }

        // ── Test ──────────────────────────────────────────────────────────────

        [Fact]
        public void parse_roman_inc()
        {
            string src = File.ReadAllText("roman.inc");

            var ctx     = new Ctx();
            var grammar = BuildGrammar(ctx);

            var result = Engine.SEARCH(src, grammar.Compiland!);

            Assert.True(result != null, "Compiland pattern did not match roman.inc");
            Assert.NotNull(ctx.tree);

            // Print the tree
            _out.WriteLine("=== SNOBOL4 Parse Tree for roman.inc ===");
            _out.WriteLine(PrettyPrint(ctx.tree));

            // Structural assertions
            var tree = ctx.tree!;
            Assert.Equal("Parse", tree[0]);           // root tag
            Assert.True(tree.Count > 1,               // has at least one command
                "Parse tree has no commands");

            // Should contain at least one Stmt and one Comment
            bool hasStmt    = false;
            bool hasComment = false;
            for (int i = 1; i < tree.Count; i++) {
                if (tree[i] is List<object> cmd) {
                    if (cmd.Count > 0 && cmd[0] is string tag) {
                        if (tag == "Stmt")    hasStmt    = true;
                        if (tag == "Comment") hasComment = true;
                    }
                }
            }
            Assert.True(hasComment, "Expected at least one Comment node");
            Assert.True(hasStmt,    "Expected at least one Stmt node");
        }
    }
}
