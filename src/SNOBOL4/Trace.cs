// Trace.cs — SNOBOL4 pattern-match tracing
//
// Mirrors Python's DEBUG_formatter + logging levels exactly:
//
//   TRACE(TraceLevel.Debug)    — trying, backtracking, success (verbose)
//   TRACE(TraceLevel.Info)     — success + backtracking only
//   TRACE(TraceLevel.Warning)  — backtracking only
//   TRACE(TraceLevel.Off)      — silent (default)
//
// Output format (same as Python):
//
//   'left context'|  pos|'right context'   PATTERN SUCCESS(start,len)=matched
//   'left context'|  pos|'right context'   PATTERN backtracking...
//
// The left side is right-aligned (reversed padding), the right side left-aligned.
// This gives a fixed-width window centered on the current cursor position.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.IO;

namespace SNOBOL4
{
    public enum TraceLevel { Off = 0, Warning = 1, Info = 2, Debug = 3 }

    public static class Tracer
    {
        // ── configuration ─────────────────────────────────────────────────────
        static TraceLevel _level  = TraceLevel.Off;
        static int        _window = 12;   // half-window size (chars each side)
        static TextWriter _out    = Console.Error;

        public static TraceLevel Level  => _level;
        public static bool IsDebug   => _level >= TraceLevel.Debug;
        public static bool IsInfo    => _level >= TraceLevel.Info;
        public static bool IsWarning => _level >= TraceLevel.Warning;

        /// <summary>
        /// Configure tracing.  Call with no args to turn off.
        /// </summary>
        public static void TRACE(TraceLevel level = TraceLevel.Off, int window = 12, TextWriter? output = null)
        {
            _level  = level;
            _window = window;
            if (output != null) _out = output;
        }

        // ── window rendering ──────────────────────────────────────────────────
        // Python:
        //   left  = subject[max(0, pos-size) : pos]
        //   right = subject[pos : min(pos+size, len)]
        //   pad_left  = ' ' * max(0, size - len(left))
        //   pad_right = ' ' * max(0, size - len(right))
        //   return f"{repr(pad_left+left)}|{pos:4d}|{repr(right+pad_right)}"
        static string Window(MatchState st)
        {
            int pos  = st.pos;
            int size = _window;
            int len  = st.subject.Length;

            var left     = st.subject.Substring(Math.Max(0, pos - size), Math.Min(pos, size));
            var right    = st.subject.Substring(pos, Math.Min(size, len - pos));
            var padLeft  = new string(' ', Math.Max(0, size - left.Length));
            var padRight = new string(' ', Math.Max(0, size - right.Length));

            // repr-style: wrap in single quotes, escape specials
            return $"'{Esc(padLeft + left)}'|{pos,4}|'{Esc(right + padRight)}'";
        }

        static string Esc(string s) =>
            s.Replace("\\", "\\\\").Replace("'", "\\'")
             .Replace("\n", "\\n").Replace("\r", "\\r")
             .Replace("\t", "\\t");

        static string Indent(MatchState st) => new string(' ', st.depth * 2);

        // ── emit helpers ──────────────────────────────────────────────────────
        static void Emit(string win, string indent, string msg) =>
            _out.WriteLine($"{win}   {indent}{msg}");

        // ── public trace calls (called from γ() methods) ──────────────────────

        /// DEBUG — "trying" at entry to γ()
        public static void Debug(MatchState st, string patName)
        {
            if (!IsDebug) return;
            Emit(Window(st), Indent(st), $"{patName} trying({st.pos})...");
        }

        /// INFO — match succeeded, yielding a slice
        public static void Info(MatchState st, string patName, int start, int stop)
        {
            if (!IsInfo) return;
            var matched = st.subject.Substring(start, stop - start);
            Emit(Window(st), Indent(st),
                 $"{patName} SUCCESS({start},{stop - start})={Esc(matched)}");
        }

        /// INFO — zero-length match (POS, RPOS, ε, etc.)
        public static void InfoZ(MatchState st, string patName)
        {
            if (!IsInfo) return;
            Emit(Window(st), Indent(st), $"{patName} SUCCESS({st.pos},0)=");
        }

        /// WARNING — backtracking after yield
        public static void Warn(MatchState st, string patName)
        {
            if (!IsWarning) return;
            Emit(Window(st), Indent(st), $"{patName} backtracking({st.pos})...");
        }
    }
}
