#!/usr/bin/env dotnet-script
#r "../src/SNOBOL4/bin/Debug/net8.0/SNOBOL4.dll"

// capture.csx — conditional and immediate capture operators
//
// Run from the project root:
//   dotnet-script examples/capture.csx

using SNOBOL4;
using static SNOBOL4.S4;

const string ALPHA  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
const string DIGITS = "0123456789";

// ── Conditional capture: % fires only on a successful full match ──────────────
string first = "", last = "";

var fullName =
    SPAN(ALPHA) % (v => first = v) +
    σ(" ") +
    SPAN(ALPHA) % (v => last  = v);

Engine.SEARCH("John Smith", fullName);
Console.WriteLine($"first={first}  last={last}");   // first=John  last=Smith

// ── Multiple fields from a CSV line ──────────────────────────────────────────
string name = "", age = "", city = "";

var csvLine =
    BREAK(",") % (v => name = v) + σ(",") +
    BREAK(",") % (v => age  = v) + σ(",") +
    REM()      % (v => city = v);

Engine.SEARCH("Alice,34,Dallas", csvLine);
Console.WriteLine($"name={name}  age={age}  city={city}");

// ── Conditional capture rolls back on failure ─────────────────────────────────
string result = "unchanged";

var p = SPAN(DIGITS) % (v => result = v) + σ("!");  // requires trailing !

Engine.SEARCH("42",  p);   // fails — no "!"
Console.WriteLine($"after failed match: result={result}");   // unchanged

Engine.SEARCH("42!", p);   // succeeds
Console.WriteLine($"after successful match: result={result}");  // 42

// ── Cursor position capture ───────────────────────────────────────────────────
int startPos = -1, endPos = -1;

var located =
    θ(pos => startPos = pos) +
    SPAN(ALPHA) +
    θ(pos => endPos   = pos);

Engine.SEARCH("  hello  ", located);
Console.WriteLine($"word found at [{startPos}:{endPos}]");   // [2:7]
