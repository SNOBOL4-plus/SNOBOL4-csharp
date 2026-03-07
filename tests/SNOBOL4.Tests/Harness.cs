// Harness.cs -- test runner (T class)
using System;
using SNOBOL4;

namespace SNOBOL4.Tests
{
static class T {
    static int _pass, _fail;
    public static void Match   (string l, string s, PATTERN P) => Rep(l, Engine.FULLMATCH(s, P) != null);
    public static void NoMatch (string l, string s, PATTERN P) => Rep(l, Engine.FULLMATCH(s, P) == null);
    public static void Found   (string l, string s, PATTERN P) => Rep(l, Engine.SEARCH(s, P) != null);
    public static void NotFound(string l, string s, PATTERN P) => Rep(l, Engine.SEARCH(s, P) == null);
    public static void IsSlice (string l, string s, PATTERN P, int a, int b) {
        var r = Engine.SEARCH(s, P); Rep(l, r != null && r.Value.Start == a && r.Value.Stop == b); }
    public static void Eq(string l, object? a, object? b) => Rep(l, Equals(a, b));
    static void Rep(string l, bool ok) {
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {l}");
    }
    public static void Summary() =>
        Console.WriteLine($"\n=== {_pass} passed, {_fail} failed ===");
    public static void Section(string t) =>
        Console.WriteLine($"\n── {t} ──");
}

}
