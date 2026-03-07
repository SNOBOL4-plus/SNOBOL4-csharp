// Core.cs -- foundation types for the SNOBOL4 pattern engine
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace SNOBOL4
{
    public sealed class F : Exception { public F(string m) : base(m) {} }

    // ── Env ───────────────────────────────────────────────────────────────────
    // Pre-wired to SnobolEnv._g at startup. GLOBALS(dict) kept for compat.
    public static class Env
    {
        internal static Dictionary<string,object> _g = new();
        public static void GLOBALS(Dictionary<string,object> g) { _g = g; }
        public static Dictionary<string,object> G  => _g;
        public static void   Set(string k, object v) => _g[k] = v;
        public static object Get(string k) => _g.TryGetValue(k, out var v) ? v : throw new KeyNotFoundException(k);
        public static bool   Has(string k) => _g.ContainsKey(k);
        public static string Str(string k) => _g.TryGetValue(k, out var v) ? v?.ToString() ?? "" : "";
        public static int    Int(string k) => _g.TryGetValue(k, out var v) ? Convert.ToInt32(v) : 0;
    }

    // ── Slot ──────────────────────────────────────────────────────────────────
    // Named reference into Env._g. Returned by SnobolEnv.TryGetMember.
    // Implicit conversions allow natural use in lambda bodies:
    //   string s = _.name;   int n = _.count;   (PATTERN)_.jElement
    public sealed class Slot
    {
        public readonly string Name;
        public Slot(string name) { Name = name; }

        public object?  Value  => Env._g.TryGetValue(Name, out var v) ? v : null;
        public int      Length => Env.Str(Name).Length;

        public static implicit operator string(Slot s)  => Env.Str(s.Name);
        public static implicit operator int(Slot s)     => Env.Int(s.Name);
        public static implicit operator bool(Slot s)    =>
            Env._g.TryGetValue(s.Name, out var v) && Convert.ToBoolean(v);
        public static implicit operator PATTERN(Slot s) =>
            Env._g.TryGetValue(s.Name, out var v) && v is PATTERN p
                ? p : throw new InvalidCastException($"Slot '{s.Name}' is not a PATTERN");

        public override string ToString() => Env.Str(Name);
    }

    // ── SnobolEnv ─────────────────────────────────────────────────────────────
    // Dynamic object — property reads return Slot, writes go into Env._g.
    //   _.foo          → Slot("foo")         use >> to bind to pattern
    //   _.foo = value  → Env._g["foo"]=value
    //   (string)_.foo  → implicit via Slot
    public sealed class SnobolEnv : DynamicObject
    {
        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            result = new Slot(binder.Name);
            return true;
        }
        public override bool TrySetMember(SetMemberBinder binder, object? value)
        {
            if (value == null) Env._g.Remove(binder.Name);
            else               Env._g[binder.Name] = value;
            return true;
        }
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
        // % — conditional assignment (existing, string key)
        public static PATTERN operator%(PATTERN p, string n) => new _Δ(p,n);
        // % Slot — conditional assignment (Δ) — same precedence as % string, higher than +
        public static PATTERN operator%(PATTERN p, Slot s)   => new _Δ(p, s.Name);
        // * Slot — immediate assignment (δ) — higher than +, binds to nearest left pattern
        public static PATTERN operator*(PATTERN p, Slot s)   => new _δ(p, s.Name);
    }
}
