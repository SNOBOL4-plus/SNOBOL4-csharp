// TestBase.cs — shared constants and helpers for all test classes
// ─────────────────────────────────────────────────────────────────────────────
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
