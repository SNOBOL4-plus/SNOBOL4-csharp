// Core.cs -- foundation types for the SNOBOL4 pattern engine
using System;
using System.Collections.Generic;

namespace SNOBOL4
{
    public sealed class F : Exception { public F(string m) : base(m) {} }

    public static class Env
    {
        static Dictionary<string,object>? _g;
        public static void GLOBALS(Dictionary<string,object> g) => _g = g;
        public static Dictionary<string,object> G => _g ?? throw new InvalidOperationException("Call Env.GLOBALS() before matching.");
        public static void   Set(string k, object v) => G[k] = v;
        public static object Get(string k) => G.TryGetValue(k, out var v) ? v : throw new KeyNotFoundException(k);
        public static bool   Has(string k) => _g != null && _g.ContainsKey(k);
        public static string Str(string k) => G.TryGetValue(k, out var v) ? v?.ToString() ?? "" : "";
        public static int    Int(string k) => G.TryGetValue(k, out var v) ? Convert.ToInt32(v) : 0;
    }

    public sealed class MatchState
    {
        public int pos; public readonly string subject;
        public readonly List<Action>       cstack = new();
        public readonly List<int>          istack = new();
        public int                         itop   = -1;
        public readonly List<List<object>> vstack = new();
        public MatchState(int p, string s) { pos = p; subject = s; }
    }

    public static class Ϣ
    {
        static readonly Stack<MatchState> _s = new();
        public static void       Push(MatchState s) => _s.Push(s);
        public static void       Pop()              => _s.Pop();
        public static MatchState Top               => _s.Peek();
    }

    public readonly struct Slice
    {
        public readonly int Start, Stop;
        public Slice(int s, int e) { Start = s; Stop = e; }
        public int    Length             => Stop - Start;
        public string Of(string subject) => subject.Substring(Start, Length);
        public override string ToString()=> $"[{Start}:{Stop}]";
    }

    public abstract class PATTERN
    {
        public abstract IEnumerable<Slice> γ();
        public static PATTERN operator+(PATTERN p, PATTERN q)
        {
            if (p is _Σ ps){var a=new PATTERN[ps._AP.Length+1];ps._AP.CopyTo(a,0);a[ps._AP.Length]=q;return new _Σ(a);}
            return new _Σ(p,q);
        }
        public static PATTERN operator|(PATTERN p, PATTERN q)
        {
            if (p is _Π pp){var a=new PATTERN[pp._AP.Length+1];pp._AP.CopyTo(a,0);a[pp._AP.Length]=q;return new _Π(a);}
            return new _Π(p,q);
        }
        public static PATTERN operator~(PATTERN p) => new _π(p);
        public static PATTERN operator%(PATTERN p, string n) => new _Δ(p,n);
    }
}
