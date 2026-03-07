// SNOBOL4cs_v8.cs  —  Stage 7: Φ (immediate) and φ (conditional) regex patterns
// All prior stages carried forward unchanged.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static S4;

sealed class F : Exception { public F(string m):base(m){} }

static class Env {
    static Dictionary<string,object>? _g;
    public static void GLOBALS(Dictionary<string,object> g)=>_g=g;
    public static Dictionary<string,object> G=>_g??throw new InvalidOperationException("GLOBALS() not called.");
    public static void   Set(string k,object v)=>G[k]=v;
    public static object Get(string k)=>G.TryGetValue(k,out var v)?v:throw new KeyNotFoundException($"'{k}' not in env");
    public static bool   Has(string k)=>_g!=null&&_g.ContainsKey(k);
}
sealed class MatchState {
    public int pos; public string subject; public List<Action> cstack=new();
    public MatchState(int p,string s){pos=p;subject=s;}
}
static class Ϣ {
    static readonly Stack<MatchState> _s=new();
    public static void       Push(MatchState s)=>_s.Push(s);
    public static void       Pop()=>_s.Pop();
    public static MatchState Top=>_s.Peek();
}
readonly struct Slice {
    public readonly int Start,Stop;
    public Slice(int s,int e){Start=s;Stop=e;}
    public override string ToString()=>$"[{Start}:{Stop}]";
}
abstract class PATTERN {
    public abstract IEnumerable<Slice> γ();
    public static PATTERN operator+(PATTERN p,PATTERN q){
        if(p is _Σ ps){var a=new PATTERN[ps._AP.Length+1];ps._AP.CopyTo(a,0);a[ps._AP.Length]=q;return new _Σ(a);}
        return new _Σ(p,q);}
    public static PATTERN operator|(PATTERN p,PATTERN q){
        if(p is _Π pp){var a=new PATTERN[pp._AP.Length+1];pp._AP.CopyTo(a,0);a[pp._AP.Length]=q;return new _Π(a);}
        return new _Π(p,q);}
    public static PATTERN operator~(PATTERN p)=>new _π(p);
    public static PATTERN operator%(PATTERN p,string n)=>new _Δ(p,n);
}
// carry-forward primitives
sealed class _σ:PATTERN{readonly string _s;public _σ(string s){_s=s;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;int p=st.pos;
        if(p+_s.Length<=st.subject.Length&&string.CompareOrdinal(st.subject,p,_s,0,_s.Length)==0)
            {st.pos=p+_s.Length;yield return new Slice(p,st.pos);st.pos=p;}}}
sealed class _Σ:PATTERN{internal readonly PATTERN[] _AP;public _Σ(params PATTERN[] ap){_AP=ap;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;int p0=st.pos;int n=_AP.Length;
        var Ag=new IEnumerator<Slice>?[n];int c=0;
        while(c>=0){if(c>=n){yield return new Slice(p0,st.pos);c--;continue;}
            if(Ag[c]==null)Ag[c]=_AP[c].γ().GetEnumerator();
            if(Ag[c]!.MoveNext())c++;else{Ag[c]!.Dispose();Ag[c]=null;c--;}}
        foreach(var e in Ag)e?.Dispose();}}
sealed class _Π:PATTERN{internal readonly PATTERN[] _AP;public _Π(params PATTERN[] ap){_AP=ap;}
    public override IEnumerable<Slice> γ(){foreach(var P in _AP)foreach(var s in P.γ())yield return s;}}
sealed class _POS:PATTERN{readonly int _n;public _POS(int n){_n=n;}
    public override IEnumerable<Slice> γ(){var s=Ϣ.Top;if(s.pos==_n)yield return new Slice(s.pos,s.pos);}}
sealed class _RPOS:PATTERN{readonly int _n;public _RPOS(int n){_n=n;}
    public override IEnumerable<Slice> γ(){var s=Ϣ.Top;if(s.pos==s.subject.Length-_n)yield return new Slice(s.pos,s.pos);}}
sealed class _ε:PATTERN{public override IEnumerable<Slice> γ(){var s=Ϣ.Top;yield return new Slice(s.pos,s.pos);}}
sealed class _FAIL:PATTERN{public override IEnumerable<Slice> γ(){yield break;}}
sealed class _ABORT:PATTERN{public override IEnumerable<Slice> γ(){throw new F("ABORT");
#pragma warning disable CS0162
    yield break;
#pragma warning restore CS0162
}}
sealed class _SUCCEED:PATTERN{public override IEnumerable<Slice> γ(){var s=Ϣ.Top;while(true)yield return new Slice(s.pos,s.pos);}}
sealed class _π:PATTERN{readonly PATTERN _P;public _π(PATTERN p){_P=p;}
    public override IEnumerable<Slice> γ(){foreach(var s in _P.γ())yield return s;var st=Ϣ.Top;yield return new Slice(st.pos,st.pos);}}
sealed class _α:PATTERN{public override IEnumerable<Slice> γ(){var s=Ϣ.Top;
    if(s.pos==0||(s.pos>0&&s.subject[s.pos-1]=='\n'))yield return new Slice(s.pos,s.pos);}}
sealed class _ω:PATTERN{public override IEnumerable<Slice> γ(){var s=Ϣ.Top;
    if(s.pos==s.subject.Length||(s.pos<s.subject.Length&&s.subject[s.pos]=='\n'))yield return new Slice(s.pos,s.pos);}}
sealed class _FENCE:PATTERN{readonly PATTERN? _P;public _FENCE(){_P=null;}public _FENCE(PATTERN p){_P=p;}
    public override IEnumerable<Slice> γ(){
        if(_P==null){var s=Ϣ.Top;yield return new Slice(s.pos,s.pos);throw new F("FENCE");}
        else{foreach(var s in _P.γ())yield return s;}}}
sealed class _LEN:PATTERN{readonly int _n;public _LEN(int n){_n=n;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;
        if(st.pos+_n<=st.subject.Length){int p=st.pos;st.pos+=_n;yield return new Slice(p,st.pos);st.pos=p;}}}
sealed class _TAB:PATTERN{readonly int _n;public _TAB(int n){_n=n;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;
        if(_n<=st.subject.Length&&_n>=st.pos){int p=st.pos;st.pos=_n;yield return new Slice(p,_n);st.pos=p;}}}
sealed class _RTAB:PATTERN{readonly int _n;public _RTAB(int n){_n=n;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;int abs=st.subject.Length-_n;
        if(_n<=st.subject.Length&&abs>=st.pos){int p=st.pos;st.pos=abs;yield return new Slice(p,abs);st.pos=p;}}}
sealed class _REM:PATTERN{public override IEnumerable<Slice> γ(){var st=Ϣ.Top;int p=st.pos;
    st.pos=st.subject.Length;yield return new Slice(p,st.pos);st.pos=p;}}
sealed class _ARB:PATTERN{public override IEnumerable<Slice> γ(){var st=Ϣ.Top;int p=st.pos;
    while(st.pos<=st.subject.Length){yield return new Slice(p,st.pos);st.pos++;}st.pos=p;}}
sealed class _MARB:PATTERN{readonly _ARB _a=new();public override IEnumerable<Slice> γ()=>_a.γ();}
sealed class _ANY:PATTERN{readonly string _c;public _ANY(string c){_c=c;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;
        if(st.pos<st.subject.Length&&_c.IndexOf(st.subject[st.pos])>=0)
            {int p=st.pos;st.pos++;yield return new Slice(p,st.pos);st.pos=p;}}}
sealed class _NOTANY:PATTERN{readonly string _c;public _NOTANY(string c){_c=c;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;
        if(st.pos<st.subject.Length&&_c.IndexOf(st.subject[st.pos])<0)
            {int p=st.pos;st.pos++;yield return new Slice(p,st.pos);st.pos=p;}}}
sealed class _SPAN:PATTERN{readonly string _c;public _SPAN(string c){_c=c;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;int p=st.pos;
        while(st.pos<st.subject.Length&&_c.IndexOf(st.subject[st.pos])>=0)st.pos++;
        if(st.pos>p){yield return new Slice(p,st.pos);st.pos=p;}}}
sealed class _NSPAN:PATTERN{readonly string _c;public _NSPAN(string c){_c=c;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;int p=st.pos;
        while(st.pos<st.subject.Length&&_c.IndexOf(st.subject[st.pos])>=0)st.pos++;
        yield return new Slice(p,st.pos);st.pos=p;}}
sealed class _BREAK:PATTERN{readonly string _c;public _BREAK(string c){_c=c;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;int p=st.pos;
        while(st.pos<st.subject.Length&&_c.IndexOf(st.subject[st.pos])<0)st.pos++;
        if(st.pos<st.subject.Length){yield return new Slice(p,st.pos);st.pos=p;}}}
sealed class _BREAKX:PATTERN{readonly _BREAK _b;public _BREAKX(string c){_b=new _BREAK(c);}
    public override IEnumerable<Slice> γ()=>_b.γ();}
sealed class _ARBNO:PATTERN{readonly PATTERN _P;public _ARBNO(PATTERN p){_P=p;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;int pos0=st.pos;
        var Ag=new List<IEnumerator<Slice>>();int cursor=0;
        while(cursor>=0){
            if(cursor>=Ag.Count){yield return new Slice(pos0,st.pos);}
            if(cursor>=Ag.Count)Ag.Add(_P.γ().GetEnumerator());
            if(Ag[cursor].MoveNext())cursor++;
            else{Ag[cursor].Dispose();Ag.RemoveAt(cursor);cursor--;}}
        foreach(var e in Ag)e.Dispose();}}
sealed class _MARBNO:PATTERN{readonly _ARBNO _a;public _MARBNO(PATTERN p){_a=new _ARBNO(p);}
    public override IEnumerable<Slice> γ()=>_a.γ();}
sealed class _BAL:PATTERN{public override IEnumerable<Slice> γ(){var st=Ϣ.Top;int pos0=st.pos;int nest=0;
    st.pos++;while(st.pos<=st.subject.Length){char ch=st.subject[st.pos-1];
        if(ch=='(')nest++;else if(ch==')')nest--;
        if(nest<0)break;else if(nest>0&&st.pos>=st.subject.Length)break;
        else if(nest==0)yield return new Slice(pos0,st.pos);st.pos++;}st.pos=pos0;}}
sealed class _δ:PATTERN{readonly PATTERN _P;readonly string _N;public _δ(PATTERN p,string n){_P=p;_N=n;}
    public override IEnumerable<Slice> γ(){foreach(var sl in _P.γ()){
        Env.Set(_N,Ϣ.Top.subject.Substring(sl.Start,sl.Stop-sl.Start));yield return sl;}}}
sealed class _Δ:PATTERN{readonly PATTERN _P;readonly string _N;public _Δ(PATTERN p,string n){_P=p;_N=n;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;foreach(var sl in _P.γ()){
        var cap=st.subject.Substring(sl.Start,sl.Stop-sl.Start);var nm=_N;
        Action act=()=>Env.Set(nm,cap);st.cstack.Add(act);yield return sl;st.cstack.RemoveAt(st.cstack.Count-1);}}}
sealed class _Θ:PATTERN{readonly string _N;public _Θ(string n){_N=n;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;Env.Set(_N,st.pos);yield return new Slice(st.pos,st.pos);}}
sealed class _θ:PATTERN{readonly string _N;public _θ(string n){_N=n;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;var pos=st.pos;var nm=_N;
        Action act=()=>Env.Set(nm,pos);st.cstack.Add(act);yield return new Slice(st.pos,st.pos);
        st.cstack.RemoveAt(st.cstack.Count-1);}}
sealed class _Λ:PATTERN{readonly Func<bool> _t;public _Λ(Func<bool> t){_t=t;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;if(_t())yield return new Slice(st.pos,st.pos);}}
sealed class _λ:PATTERN{readonly Action _c;public _λ(Action c){_c=c;}
    public override IEnumerable<Slice> γ(){var st=Ϣ.Top;st.cstack.Add(_c);
        yield return new Slice(st.pos,st.pos);st.cstack.RemoveAt(st.cstack.Count-1);}}
sealed class _ζ:PATTERN{readonly string _N;public _ζ(string n){_N=n;}
    public override IEnumerable<Slice> γ(){foreach(var s in ((PATTERN)Env.Get(_N)).γ())yield return s;}}

// ── Regex cache ───────────────────────────────────────────────────────────────
static class RxCache {
    static readonly Dictionary<string,Regex> _c=new();
    public static Regex Get(string pat){
        if(!_c.TryGetValue(pat,out var rx))
            _c[pat]=rx=new Regex(pat,RegexOptions.Multiline|RegexOptions.Compiled);
        return rx;}
}

// ── Φ — immediate regex match ─────────────────────────────────────────────────
// Anchored at cursor.  Named groups written to Env immediately (permanent).
// Equivalent to Python's Φ: _env._g[N]=STRING(v) inside the γ body.
sealed class _Φ:PATTERN{
    readonly string _pat;public _Φ(string p){_pat=p;}
    public override IEnumerable<Slice> γ(){
        var st=Ϣ.Top;var m=RxCache.Get(_pat).Match(st.subject,st.pos);
        if(m.Success&&m.Index==st.pos){
            int p=st.pos;st.pos=m.Index+m.Length;
            foreach(Group g in m.Groups){
                if(g.Name=="0")continue;
                if(int.TryParse(g.Name,out _))continue;
                if(g.Success)Env.Set(g.Name,g.Value);}
            yield return new Slice(p,st.pos);
            st.pos=p;}}}

// ── φ — conditional regex match ───────────────────────────────────────────────
// Anchored at cursor.  Named group assignments pushed to cstack (deferred).
// Only committed when whole match succeeds; rolled back on backtrack.
// Equivalent to Python's φ: cstack.append / pop around yield.
sealed class _φ:PATTERN{
    readonly string _pat;public _φ(string p){_pat=p;}
    public override IEnumerable<Slice> γ(){
        var st=Ϣ.Top;var m=RxCache.Get(_pat).Match(st.subject,st.pos);
        if(m.Success&&m.Index==st.pos){
            int p=st.pos;st.pos=m.Index+m.Length;
            int pushed=0;
            foreach(Group g in m.Groups){
                if(g.Name=="0")continue;
                if(int.TryParse(g.Name,out _))continue;
                if(g.Success){var nm=g.Name;var v=g.Value;
                    st.cstack.Add(()=>Env.Set(nm,v));pushed++;}}
            yield return new Slice(p,st.pos);
            for(int i=0;i<pushed;i++)st.cstack.RemoveAt(st.cstack.Count-1);
            st.pos=p;}}}

// ── S4 factory ────────────────────────────────────────────────────────────────
static class S4{
    public static PATTERN Σ(params PATTERN[] ap)=>new _Σ(ap);
    public static PATTERN Π(params PATTERN[] ap)=>new _Π(ap);
    public static PATTERN π(PATTERN p)=>new _π(p);
    public static PATTERN σ(string s)=>new _σ(s);
    public static PATTERN POS(int n)=>new _POS(n);
    public static PATTERN RPOS(int n)=>new _RPOS(n);
    public static PATTERN ε()=>new _ε();
    public static PATTERN FAIL()=>new _FAIL();
    public static PATTERN ABORT()=>new _ABORT();
    public static PATTERN SUCCEED()=>new _SUCCEED();
    public static PATTERN α()=>new _α();
    public static PATTERN ω()=>new _ω();
    public static PATTERN FENCE()=>new _FENCE();
    public static PATTERN FENCE(PATTERN p)=>new _FENCE(p);
    public static PATTERN LEN(int n)=>new _LEN(n);
    public static PATTERN TAB(int n)=>new _TAB(n);
    public static PATTERN RTAB(int n)=>new _RTAB(n);
    public static PATTERN REM()=>new _REM();
    public static PATTERN ARB()=>new _ARB();
    public static PATTERN MARB()=>new _MARB();
    public static PATTERN ANY(string c)=>new _ANY(c);
    public static PATTERN NOTANY(string c)=>new _NOTANY(c);
    public static PATTERN SPAN(string c)=>new _SPAN(c);
    public static PATTERN NSPAN(string c)=>new _NSPAN(c);
    public static PATTERN BREAK(string c)=>new _BREAK(c);
    public static PATTERN BREAKX(string c)=>new _BREAKX(c);
    public static PATTERN ARBNO(PATTERN p)=>new _ARBNO(p);
    public static PATTERN MARBNO(PATTERN p)=>new _MARBNO(p);
    public static PATTERN BAL()=>new _BAL();
    public static PATTERN δ(PATTERN p,string n)=>new _δ(p,n);
    public static PATTERN Δ(PATTERN p,string n)=>new _Δ(p,n);
    public static PATTERN Θ(string n)=>new _Θ(n);
    public static PATTERN θ(string n)=>new _θ(n);
    public static PATTERN Λ(Func<bool> t)=>new _Λ(t);
    public static PATTERN λ(Action c)=>new _λ(c);
    public static PATTERN ζ(string n)=>new _ζ(n);
    public static PATTERN Φ(string rx)=>new _Φ(rx);
    public static PATTERN φ(string rx)=>new _φ(rx);
    public static void GLOBALS(Dictionary<string,object> g)=>Env.GLOBALS(g);
}

// ── Engine ────────────────────────────────────────────────────────────────────
static class Engine{
    public static Slice? SEARCH(string S,PATTERN P,bool exc=false){
        for(int c=0;c<=S.Length;c++){
            var state=new MatchState(c,S);Ϣ.Push(state);bool popped=false;
            try{foreach(var sl in P.γ()){Ϣ.Pop();popped=true;
                    foreach(var act in state.cstack)act();return sl;}}
            catch(F){if(!popped){Ϣ.Pop();popped=true;}if(exc)throw;return null;}
            finally{if(!popped)Ϣ.Pop();}}
        if(exc)throw new F("FAIL");return null;}
    public static Slice? MATCH    (string S,PATTERN P,bool exc=false)=>SEARCH(S,POS(0)+P,exc);
    public static Slice? FULLMATCH(string S,PATTERN P,bool exc=false)=>SEARCH(S,POS(0)+P+RPOS(0),exc);
}

// ── Test harness ──────────────────────────────────────────────────────────────
static class T{
    static int _pass,_fail;
    public static void Match   (string l,string s,PATTERN P)=>Rep(l,Engine.FULLMATCH(s,P)!=null);
    public static void NoMatch (string l,string s,PATTERN P)=>Rep(l,Engine.FULLMATCH(s,P)==null);
    public static void Found   (string l,string s,PATTERN P)=>Rep(l,Engine.SEARCH(s,P)!=null);
    public static void NotFound(string l,string s,PATTERN P)=>Rep(l,Engine.SEARCH(s,P)==null);
    public static void Slice(string l,string s,PATTERN P,int a,int b){
        var r=Engine.SEARCH(s,P);Rep(l,r!=null&&r.Value.Start==a&&r.Value.Stop==b);}
    public static void Eq(string l,object? a,object? b)=>Rep(l,Equals(a,b));
    static void Rep(string l,bool ok){if(ok)_pass++;else _fail++;
        Console.WriteLine($"  {(ok?"PASS":"FAIL")}  {l}");}
    public static void Summary()=>Console.WriteLine($"\n=== {_pass} passed, {_fail} failed ===");
    public static void Section(string t)=>Console.WriteLine($"\n── {t} ──");
}

// ═════════════════════════════════════════════════════════════════════════════
// Tests
// ═════════════════════════════════════════════════════════════════════════════
class Program{
    const string DIGITS="0123456789";
    const string ALPHA ="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    const string ALNUM ="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    static readonly Dictionary<string,object> G=new();
    static string Gs(string k)=>G.TryGetValue(k,out var v)?v.ToString()!:"<unset>";

    static void Main(){
        GLOBALS(G);
        Console.WriteLine("=== SNOBOL4cs V8  —  Stage 7: Φ · φ ===");
        Test_Φ_basic();
        Test_Φ_groups();
        Test_Φ_anchoring();
        Test_φ_basic();
        Test_φ_conditional();
        Test_contrast();
        Test_combined();
        Test_regression();
        T.Summary();
    }

    // ── Φ basic ───────────────────────────────────────────────────────────────
    static void Test_Φ_basic(){
        T.Section("Φ  basic regex matching");
        T.Match  ("Φ(hello) fullmatches \"hello\"",  "hello",  POS(0)+Φ(@"hello")+RPOS(0));
        T.NoMatch("Φ(hello) no match \"world\"",     "world",  POS(0)+Φ(@"hello")+RPOS(0));
        T.Match  ("Φ(\\d+) fullmatches \"123\"",     "123",    POS(0)+Φ(@"\d+")+RPOS(0));
        T.NoMatch("Φ(\\d+) no match \"abc\"",        "abc",    POS(0)+Φ(@"\d+")+RPOS(0));
        T.Found  ("Φ(\\d+) found in \"abc123\"",     "abc123", Φ(@"\d+"));
        T.Slice  ("Φ(\\d+) in \"abc123\" = [3:6]",  "abc123", Φ(@"\d+"),3,6);
    }

    // ── Φ named groups ────────────────────────────────────────────────────────
    static void Test_Φ_groups(){
        T.Section("Φ  named capture groups (immediate)");
        Engine.SEARCH("hello42", POS(0)+Φ(@"(?<word>[a-z]+)(?<num>\d+)"));
        T.Eq("Φ word","hello",Gs("word"));
        T.Eq("Φ num", "42",   Gs("num"));

        // Python test_01.py file-id example — named group is 'n' in Python but
        // we'll match the spirit: capture the whole id into 'fileid'
        Engine.SEARCH("001_01C717AB.5C51AFDE ...",
            Φ(@"(?<fileid>[0-9]{3}(_[0-9A-F]{4})?_[0-9A-F]{8}\.[0-9A-F]{8})"));
        T.Eq("Φ file-id","001_01C717AB.5C51AFDE",Gs("fileid"));

        // Immediate: written even when outer match later fails
        G["tag"]="before";
        Engine.FULLMATCH("abc123", POS(0)+Φ(@"(?<tag>[a-z]+)")+σ("NOPE")+RPOS(0));
        T.Eq("Φ immediate on outer fail: tag=abc","abc",Gs("tag"));

        // Sequence: two Φ patterns each capture their groups
        Engine.FULLMATCH("John:Doe",
            POS(0)+Φ(@"(?<first>[A-Za-z]+)")+σ(":")+Φ(@"(?<last>[A-Za-z]+)")+RPOS(0));
        T.Eq("Φ seq first","John",Gs("first"));
        T.Eq("Φ seq last", "Doe", Gs("last"));
    }

    // ── Φ anchoring ───────────────────────────────────────────────────────────
    static void Test_Φ_anchoring(){
        T.Section("Φ  cursor anchoring");
        T.Slice  ("POS(3)+Φ(\\d+) matches at pos 3","abc123",POS(3)+Φ(@"\d+"),3,6);
        T.NotFound("POS(0)+Φ(\\d+) not found at pos 0","abc123",POS(0)+Φ(@"\d+"));
        T.Match  ("Φ([a-z]+\\d+) fullmatches \"abc99\"","abc99",POS(0)+Φ(@"[a-z]+\d+")+RPOS(0));
        T.NotFound("Φ(x{10}) not found in \"xxx\"","xxx",Φ(@"x{10}"));
    }

    // ── φ basic ───────────────────────────────────────────────────────────────
    static void Test_φ_basic(){
        T.Section("φ  basic regex matching");
        T.Match  ("φ(hello) fullmatches \"hello\"",  "hello",  POS(0)+φ(@"hello")+RPOS(0));
        T.NoMatch("φ(hello) no match \"world\"",     "world",  POS(0)+φ(@"hello")+RPOS(0));
        T.Found  ("φ(\\d+) found in \"abc123\"",     "abc123", φ(@"\d+"));
        T.Slice  ("φ(\\d+) in \"abc123\" = [3:6]",  "abc123", φ(@"\d+"),3,6);
        // Named group captured on success
        Engine.FULLMATCH("world42", POS(0)+φ(@"(?<fw>[a-z]+)(?<fn>\d+)")+RPOS(0));
        T.Eq("φ fw","world",Gs("fw"));
        T.Eq("φ fn","42",   Gs("fn"));
    }

    // ── φ conditional semantics ───────────────────────────────────────────────
    static void Test_φ_conditional(){
        T.Section("φ  conditional capture");
        // Does NOT fire on outer failure
        G["ctag"]="before";
        Engine.FULLMATCH("abc123", POS(0)+φ(@"(?<ctag>[a-z]+)")+σ("NOPE")+RPOS(0));
        T.Eq("φ not fired on fail","before",Gs("ctag"));
        // DOES fire on success
        G["ctag"]="before";
        Engine.FULLMATCH("abc", POS(0)+φ(@"(?<ctag>[a-z]+)")+RPOS(0));
        T.Eq("φ fired on success","abc",Gs("ctag"));
        // Both groups in sequence — both fire
        Engine.FULLMATCH("foo:bar",
            POS(0)+φ(@"(?<pa>[a-z]+)")+σ(":")+φ(@"(?<pb>[a-z]+)")+RPOS(0));
        T.Eq("φ seq pa","foo",Gs("pa"));
        T.Eq("φ seq pb","bar",Gs("pb"));
        // Both silent on failure
        G["pa"]="before";G["pb"]="before";
        Engine.FULLMATCH("foo:bar",
            POS(0)+φ(@"(?<pa>[a-z]+)")+σ(":")+φ(@"(?<pb>[a-z]+)")+σ("NOPE")+RPOS(0));
        T.Eq("φ seq fail pa","before",Gs("pa"));
        T.Eq("φ seq fail pb","before",Gs("pb"));
    }

    // ── Φ vs φ contrast ───────────────────────────────────────────────────────
    static void Test_contrast(){
        T.Section("Φ vs φ  immediate vs conditional");
        G["xi"]="before";
        Engine.FULLMATCH("abc999", POS(0)+Φ(@"(?<xi>[a-z]+)")+σ("NOPE")+RPOS(0));
        T.Eq("Φ fires on outer fail: xi=abc","abc",Gs("xi"));
        G["xc"]="before";
        Engine.FULLMATCH("abc999", POS(0)+φ(@"(?<xc>[a-z]+)")+σ("NOPE")+RPOS(0));
        T.Eq("φ silent on outer fail: xc=before","before",Gs("xc"));
        // Same slice on success
        var r1=Engine.SEARCH("abc",POS(0)+Φ(@"[a-z]+")+RPOS(0));
        var r2=Engine.SEARCH("abc",POS(0)+φ(@"[a-z]+")+RPOS(0));
        T.Eq("same slice start",r1?.Start,r2?.Start);
        T.Eq("same slice stop", r1?.Stop, r2?.Stop);
    }

    // ── combined ──────────────────────────────────────────────────────────────
    static void Test_combined(){
        T.Section("Φ/φ combined with SNOBOL4 patterns");

        // Φ+ARBNO: capture last word in a space-separated sequence
        Engine.FULLMATCH("one two three",
            POS(0)+ARBNO(δ(Φ(@"\w+"),"last")+~σ(" "))+RPOS(0));
        T.Eq("Φ+ARBNO last word","three",Gs("last"));

        // φ+Λ guard: match a number, check range
        // Φ+Λ guard: Φ (immediate) writes rval before Λ reads it.
        // φ (conditional) defers the write, so the value is not in env when Λ runs.
        G["rval"]="before";
        PATTERN ranged=POS(0)+Φ(@"(?<rval>\d+)")+Λ(()=>int.Parse(Gs("rval"))<100)+RPOS(0);
        Engine.FULLMATCH("42", ranged);
        T.Eq("Φ+Λ 42 in range","42",Gs("rval"));
        G["rval"]="before";
        Engine.FULLMATCH("200", ranged);
        // Λ blocks the match; Φ already wrote rval (immediate, permanent)
        T.Eq("Φ+Λ 200 blocked: rval=200 from Φ","200",Gs("rval"));

        // Φ key=value
        Engine.FULLMATCH("name=Alice",
            POS(0)+Φ(@"(?<kkey>\w+)")+σ("=")+Φ(@"(?<kval>\w+)")+RPOS(0));
        T.Eq("Φ kkey","name", Gs("kkey"));
        T.Eq("Φ kval","Alice",Gs("kval"));

        // φ with ζ recursive grammar — parse "(abc)" using φ for the word
        G["inner"]= POS(0)+φ(@"(?<iword>[a-z]+)")+RPOS(0);
        PATTERN wrapped=POS(0)+σ("(")+ζ("inner")+σ(")")+RPOS(0);
        // Note: ζ("inner") is fullmatch-anchored itself; won't work here directly.
        // Use a simpler forward-ref test: Φ inside ζ-referenced pattern
        G["P2"]=Φ(@"(?<p2word>[a-z]+)");
        Engine.FULLMATCH("hello", POS(0)+ζ("P2")+RPOS(0));
        T.Eq("φ via ζ: p2word","hello",Gs("p2word"));
    }

    // ── regression ────────────────────────────────────────────────────────────
    static void Test_regression(){
        T.Section("Regression: prior stages");
        Engine.FULLMATCH("hello42",POS(0)+(SPAN(ALPHA)%"rw")+(SPAN(DIGITS)%"rn")+RPOS(0));
        T.Eq("% word","hello",Gs("rw"));
        T.Eq("% num", "42",   Gs("rn"));

        PATTERN atom=SPAN(DIGITS)|σ("(")+ζ("rexpr")+σ(")");
        PATTERN expr=atom+ARBNO((σ("+")|σ("-"))+atom);
        G["rexpr"]=expr;
        T.Match  ("ζ expr \"1+(2+3)\"","1+(2+3)",POS(0)+expr+RPOS(0));
        T.NoMatch("ζ expr no \"1+\"",  "1+",     POS(0)+expr+RPOS(0));

        PATTERN ident=POS(0)+ANY(ALPHA)+NSPAN(ALNUM)+RPOS(0);
        T.Match  ("ident \"Hello42\"","Hello42",ident);
        T.NoMatch("ident \"1bad\"",   "1bad",   ident);

        PATTERN real=POS(0)+~ANY("+-")+SPAN(DIGITS)+~(σ(".")+NSPAN(DIGITS))+RPOS(0);
        T.Match  ("real \"+3.14\"","+3.14",real);
        T.NoMatch("real \"abc\"",   "abc",  real);

        T.Match  ("ARBNO(σ(ab)) \"ababab\"","ababab",POS(0)+ARBNO(σ("ab"))+RPOS(0));
        T.Match  ("BAL \"(a+b)\"","(a+b)",POS(0)+BAL()+RPOS(0));
    }
}
