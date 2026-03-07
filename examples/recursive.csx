#!/usr/bin/env dotnet-script
#r "../src/SNOBOL4/bin/Debug/net8.0/SNOBOL4.dll"

// recursive.csx — mutually recursive grammars with ζ
//
// Run from the project root:
//   dotnet-script examples/recursive.csx
//
// ζ(() => p) defers a pattern reference to match time, so local variables
// can refer to each other before they are assigned.

using SNOBOL4;
using static SNOBOL4.S4;

const string DIGITS = "0123456789";
const string ALPHA  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

// ── Simple arithmetic expression: digits and parenthesised subexpressions ─────
//
//   expr  ::=  atom ( '+' atom )*
//   atom  ::=  DIGITS  |  '(' expr ')'

PATTERN? expr = null;
PATTERN? atom = null;

atom = SPAN(DIGITS)
     | σ("(") + ζ(() => expr!) + σ(")");

expr = atom! + ARBNO(σ("+") + atom!);

var fullExpr = POS(0) + expr + RPOS(0);

string[] tests = { "1", "1+2", "(1+2)+3", "((4))", "1+(2+3)" };
foreach (var s in tests)
    Console.WriteLine($"{s,-15} {(Engine.FULLMATCH(s, fullExpr) != null ? "match" : "no match")}");

Console.WriteLine();

// ── Nested list: ( item* )  where item is a word or a nested list ─────────────

PATTERN? list = null;
PATTERN? item = null;
var SP = NSPAN(" ");

item = SPAN(ALPHA) + SP
     | ζ(() => list!) + SP;

list = σ("(") + SP + ARBNO(item!) + σ(")");

var fullList = POS(0) + list + RPOS(0);

string[] lists = {
    "()",
    "(a)",
    "(a b c)",
    "((a b) c)",
    "(a (b (c d)) e)"
};
foreach (var s in lists)
    Console.WriteLine($"{s,-20} {(Engine.FULLMATCH(s, fullList) != null ? "match" : "no match")}");
