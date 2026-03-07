// TestBase.cs — shared constants and helpers for all test classes
//
// Every test class inherits from TestBase, which provides:
//   • Character-class constants (DIGITS, ALPHA, etc.)
//   • FreshEnv() — resets Env to a clean dictionary before each test
//   • Match / NoMatch / Found helpers that wrap Engine calls as assertions
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using SNOBOL4;
using static SNOBOL4.S4;
using Xunit;

namespace SNOBOL4.Tests
{
    public abstract class TestBase
    {
        protected const string DIGITS = "0123456789";
        protected const string UCASE  = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        protected const string LCASE  = "abcdefghijklmnopqrstuvwxyz";
        protected const string ALPHA  = UCASE + LCASE;
        protected const string ALNUM  = ALPHA + DIGITS;

        // Reset Env to a clean slate — used by tests that rely on specific
        // env contents or that inspect what was (or was not) written.
        protected static void FreshEnv() => Env.GLOBALS(new Dictionary<string, object>());

        // Assertion helpers — thin wrappers so test bodies stay readable
        protected static void AssertMatch  (string subject, PATTERN p) =>
            Assert.NotNull(Engine.FULLMATCH(subject, p));
        protected static void AssertNoMatch(string subject, PATTERN p) =>
            Assert.Null(Engine.FULLMATCH(subject, p));
        protected static void AssertFound  (string subject, PATTERN p) =>
            Assert.NotNull(Engine.SEARCH(subject, p));
        protected static void AssertNotFound(string subject, PATTERN p) =>
            Assert.Null(Engine.SEARCH(subject, p));
    }
}
