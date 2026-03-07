// Trace.cs — sliding-window pattern-match trace output
//
// TRACE(level, window) enables diagnostic output from γ() methods.
// Output goes to Console.Error by default; pass a TextWriter to redirect.
//
// Levels (increasing verbosity):
//   TraceLevel.Off      — silent (default)
//   TraceLevel.Warning  — backtracking only
//   TraceLevel.Info     — success and backtracking
//   TraceLevel.Debug    — trying, success, and backtracking
//
// Output format — each line shows a fixed-width view of the subject string
// centered on the current cursor position, followed by the pattern event:
//
//   '     hello'|   5|'42        '   SPAN("abc...") SUCCESS(0,5)=hello
//   '   hello42'|   7|'          '   SPAN("0-9...") SUCCESS(5,2)=42
//   '   hello42'|   7|'          '   RPOS(0) backtracking(7)...
//
// The left half of the window is right-aligned (text runs up to the cursor
// bar) and the right half is left-aligned (text runs away from it).  Padding
// spaces fill whichever side has fewer characters than the window half-width.
// This gives a stable fixed-width display as the cursor advances.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.IO;

namespace SNOBOL4
{
    public enum TraceLevel { Off = 0, Warning = 1, Info = 2, Debug = 3 }

    public static class Tracer
    {
        static TraceLevel _level  = TraceLevel.Off;
        static int        _window = 12;   // half-width of the context window (chars each side)
        static TextWriter _out    = Console.Error;

        public static TraceLevel Level    => _level;
        public static bool       IsDebug   => _level >= TraceLevel.Debug;
        public static bool       IsInfo    => _level >= TraceLevel.Info;
        public static bool       IsWarning => _level >= TraceLevel.Warning;

        /// <summary>
        /// Configure tracing.  Call TRACE() with no arguments to turn it off.
        /// </summary>
        public static void TRACE(TraceLevel level = TraceLevel.Off, int window = 12, TextWriter? output = null)
        {
            _level  = level;
            _window = window;
            if (output != null) _out = output;
        }

        // ── window rendering ──────────────────────────────────────────────────
        // Builds the fixed-width context string:
        //   left  = subject[max(0, pos-size) .. pos]        right-fill with spaces
        //   right = subject[pos .. min(pos+size, len)]       left-fill with spaces
        // Both halves are then repr-escaped and framed:  'left'|pos|'right'
        static string Window(MatchState st)
        {
            int pos  = st.pos;
            int size = _window;
            int len  = st.subject.Length;

            var left     = st.subject.Substring(Math.Max(0, pos - size), Math.Min(pos, size));
            var right    = st.subject.Substring(pos, Math.Min(size, len - pos));
            var padLeft  = new string(' ', Math.Max(0, size - left.Length));
            var padRight = new string(' ', Math.Max(0, size - right.Length));

            return $"'{Esc(padLeft + left)}'|{pos,4}|'{Esc(right + padRight)}'";
        }

        // Escape special characters for readable single-quoted display
        static string Esc(string s) =>
            s.Replace("\\", "\\\\").Replace("'", "\\'")
             .Replace("\n", "\\n").Replace("\r", "\\r")
             .Replace("\t", "\\t");

        // Indent by nesting depth so nested patterns are visually grouped
        static string Indent(MatchState st) => new string(' ', st.depth * 2);

        static void Emit(string win, string indent, string msg) =>
            _out.WriteLine($"{win}   {indent}{msg}");

        // ── public emit methods called from γ() ──────────────────────────────

        /// DEBUG — emitted at the start of γ(), before any match is attempted
        public static void Debug(MatchState st, string patName)
        {
            if (!IsDebug) return;
            Emit(Window(st), Indent(st), $"{patName} trying({st.pos})...");
        }

        /// INFO — emitted when a sub-pattern succeeds and yields a non-empty slice
        public static void Info(MatchState st, string patName, int start, int stop)
        {
            if (!IsInfo) return;
            var matched = st.subject.Substring(start, stop - start);
            Emit(Window(st), Indent(st), $"{patName} SUCCESS({start},{stop - start})={Esc(matched)}");
        }

        /// INFO — emitted for zero-length successes (POS, RPOS, ε, anchors, etc.)
        public static void InfoZ(MatchState st, string patName)
        {
            if (!IsInfo) return;
            Emit(Window(st), Indent(st), $"{patName} SUCCESS({st.pos},0)=");
        }

        /// WARNING — emitted when a pattern resumes after yielding (backtracking)
        public static void Warn(MatchState st, string patName)
        {
            if (!IsWarning) return;
            Emit(Window(st), Indent(st), $"{patName} backtracking({st.pos})...");
        }
    }
}
