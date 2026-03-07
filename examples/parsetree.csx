#!/usr/bin/env dotnet-script
#r "../src/SNOBOL4/bin/Debug/net8.0/SNOBOL4.dll"

// parsetree.csx — build a parse tree with Shift / Reduce / Pop
//
// Run from the project root:
//   dotnet-script examples/parsetree.csx
//
// Shift pushes a leaf node onto the value stack.
// Reduce pops N children and wraps them in a labelled list.
// Pop retrieves the finished tree into a C# variable.
// nPush / nInc / nPop count children automatically when N isn't known in advance.

using System;
using System.Collections.Generic;
using SNOBOL4;
using static SNOBOL4.S4;

const string ALPHA  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
const string DIGITS = "0123456789";

// ── Pretty-printer ────────────────────────────────────────────────────────────
static void Print(object node, string indent = "")
{
    if (node is List<object> list) {
        Console.WriteLine($"{indent}({list[0]}");
        for (int i = 1; i < list.Count; i++)
            Print(list[i], indent + "  ");
        Console.WriteLine($"{indent})");
    } else {
        Console.WriteLine($"{indent}{node}");
    }
}

// ── Grammar: simple word list ─────────────────────────────────────────────────
//
//   wordlist  ::=  word ( ' ' word )*
//   word      ::=  ALPHA+

string w = "";
List<object>? tree = null;

var word     = SPAN(ALPHA) % (v => w = v) + Shift("Word", () => w) + nInc();
var wordList = POS(0) + nPush() + word + ARBNO(σ(" ") + word) + nPop()
             + Reduce("WordList") + Pop(t => tree = t) + RPOS(0);

Engine.FULLMATCH("the quick brown fox", wordList);

Console.WriteLine("── WordList ─────────────────");
Print(tree!);
Console.WriteLine();

// ── Grammar: assignment statement  name = value ───────────────────────────────
string lhs = "", rhs = "";
List<object>? stmt = null;

var nameP  = SPAN(ALPHA) % (v => lhs = v) + Shift("Name",  () => lhs);
var valueP = SPAN(ALPHA + DIGITS) % (v => rhs = v) + Shift("Value", () => rhs);
var assign = POS(0) + nameP + NSPAN(" ") + σ("=") + NSPAN(" ")
           + valueP + Reduce("Assign", 2) + Pop(t => stmt = t) + RPOS(0);

Engine.FULLMATCH("count = 42", assign);

Console.WriteLine("── Assign ───────────────────");
Print(stmt!);
