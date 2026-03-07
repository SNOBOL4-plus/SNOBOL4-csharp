#!/usr/bin/env dotnet-script
#r "src/SNOBOL4/bin/Debug/net8.0/SNOBOL4.dll"

using SNOBOL4;
using static SNOBOL4.S4;

const string ALPHA  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
const string DIGITS = "0123456789";
const string ALNUM  = ALPHA + DIGITS;

var ident = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);

string[] words = { "Hello", "abc123", "X", "1bad", "", "good_name" };
foreach (var w in words)
    Console.WriteLine($"{w,-12} {(Engine.FULLMATCH(w, ident) != null ? "identifier" : "not identifier")}");
