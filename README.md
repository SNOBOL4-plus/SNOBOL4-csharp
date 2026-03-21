# SNOBOL4csharp
[![License: LGPL v3](https://img.shields.io/badge/License-LGPL_v3-blue.svg)](https://www.gnu.org/licenses/lgpl-3.0)

A C# port of the SNOBOL4 pattern-matching engine, ported from
[SNOBOL4python](https://github.com/LCherryholmes/SNOBOL4python).
Part of the SNOBOL4 language-family project alongside
**SNOBOL4python** and **SNOBOL4clojure**.

Patterns are first-class objects. Matching is lazy and backtracking.
Captures use plain C# delegates — no global state, no string keys.

```csharp
using SNOBOL4;
using static SNOBOL4.S4;

const string ALPHA  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
const string DIGITS = "0123456789";
const string ALNUM  = ALPHA + DIGITS;

string word = "", rest = "";

var ident = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);
var csv   = BREAK(",") % (v => word = v) + σ(",") + REM() % (v => rest = v);

Engine.FULLMATCH("Hello123", ident);          // → [0:8]
Engine.SEARCH("apple,42", csv);              // word="apple"  rest="42"
```

---

## Layout

```
SNOBOL4csharp/
  src/SNOBOL4/               library  (SNOBOL4.dll)
    Core.cs                  PATTERN  Slice  MatchState  Ϣ  F
    Primitives.cs            σ Σ Π π  POS RPOS  LEN TAB RTAB REM
                             ARB MARB  ANY NOTANY  SPAN NSPAN
                             BREAK BREAKX  ARBNO MARBNO  BAL
                             FENCE  ε FAIL ABORT SUCCEED  α ω
    Assignment.cs            Δ δ  Θ θ  Λ λ  ζ
    ShiftReduce.cs           nPush nInc nPop  Shift Reduce Pop
    Regex.cs                 Φ φ  RxCache
    Trace.cs                 Tracer  TraceLevel  TRACE()
    S4.cs                    S4 factory + Engine

  tests/SNOBOL4.Tests/       xUnit test suite
    TestBase.cs              shared constants and assertion helpers
    Tests_Primitives.cs      all primitives
    Tests_01.cs              identifier, real_number, bead, bal, arb
    Tests_Arbno.cs           ARBNO patterns
    Tests_RE_Grammar.cs      recursive RE grammar with Shift/Reduce
    Tests_Env.cs             capture operators, TRACE, NSPAN
    Tests_CLAWS.cs           CLAWS5 NLP corpus parser
    Tests_TreeBank.cs        Penn Treebank parenthesized-tree parser
    Tests_Porter.cs          Porter Stemmer — 23,531-word corpus
    Tests_Snobol4Parser.cs   SNOBOL4 source code parser

  examples/                  .csx script examples
    hello.csx                identifier pattern quickstart

  SNOBOL4csharp.sln
  snapshot.sh                zip a dated snapshot
```

---

## Setup

```bash
# Install .NET 8
# Ubuntu/Debian:
sudo apt install dotnet-sdk-8.0

# Install the dotnet-script runner (for .csx files)
dotnet tool install -g dotnet-script
export PATH="$PATH:$HOME/.dotnet/tools"   # add to ~/.bashrc or ~/.zshrc

# Build the library
dotnet build -c Debug src/SNOBOL4

# Run the tests
dotnet test tests/SNOBOL4.Tests
```

---

## Running a .csx script

```bash
dotnet-script examples/hello.csx
```

The `#r` line at the top of every `.csx` file references the compiled
library relative to the project root:

```csharp
#!/usr/bin/env dotnet-script
#r "src/SNOBOL4/bin/Debug/net8.0/SNOBOL4.dll"

using SNOBOL4;
using static SNOBOL4.S4;
```

---

## Pattern primitives

### Literal and position

| Primitive | Matches |
|-----------|---------|
| `σ("s")` | the literal string `s` |
| `σ(() => expr)` | string evaluated at match time |
| `POS(n)` | only when cursor is at position `n` |
| `RPOS(n)` | only when `n` characters remain |
| `LEN(n)` | exactly `n` characters |
| `TAB(n)` | advances cursor to position `n` |
| `RTAB(n)` | advances to `n` characters from end |
| `REM()` | rest of subject from cursor |

### Character classes

| Primitive | Matches |
|-----------|---------|
| `ANY("abc")` | exactly one character from the set |
| `NOTANY("abc")` | exactly one character not in the set |
| `SPAN("abc")` | one or more characters from the set |
| `NSPAN("abc")` | zero or more from the set (never fails) |
| `BREAK("abc")` | zero or more chars up to (not including) a set member |
| `BREAKX("abc")` | alias for BREAK |

All character-class primitives also accept `Func<string>` for sets
evaluated at match time:

```csharp
string sep = ",";
var field = BREAK(() => sep) % (v => last = v) + σ(() => sep);
```

### Combinators

| Expression | Meaning |
|------------|---------|
| `P + Q` | P then Q (concatenation) |
| `P \| Q` | P or Q (alternation, left first) |
| `~P` | optional P — P or empty |
| `ARBNO(P)` | zero or more P, shortest first |
| `BAL()` | one balanced parenthesised token |
| `ARB()` | zero or more of any character, shortest first |

### Anchors and control

| Primitive | Meaning |
|-----------|---------|
| `α()` | beginning of line (pos 0 or after `\n`) |
| `ω()` | end of line (end of string or before `\n`) |
| `ε()` | always succeeds, zero length |
| `FAIL()` | always fails |
| `ABORT()` | terminates the entire match immediately |
| `SUCCEED()` | yields infinitely (use with FENCE) |
| `FENCE()` | commit point — throws on backtrack |
| `FENCE(P)` | protects P from external backtracking |

---

## Capture

Captures bind matched substrings to C# local variables via delegates.
Two forms: **conditional** (fires only on full match commit) and
**immediate** (fires on every sub-match, even if the outer match later fails).

```csharp
string first = "", last = "";

// Conditional — the normal form  (SNOBOL4: P . N)
var name = SPAN(ALPHA) % (v => first = v)
         + σ(" ")
         + SPAN(ALPHA) % (v => last  = v);

Engine.SEARCH("John Smith", name);
// first="John"  last="Smith"
```

```csharp
// Immediate — use when you need the value even on partial matches  (SNOBOL4: P $ N)
string initial = "";
var tag = ANY(ALPHA) * (v => initial = v) + SPAN(ALNUM);
```

Operator precedence: `%` and `*` bind tighter than `+`, so:

```csharp
SPAN(ALPHA) + ANY(DIGITS) % (v => d = v)
// parses as: SPAN(ALPHA) + (ANY(DIGITS) % (v => d = v))
```

### Cursor capture

```csharp
int startPos = 0;
var p = Θ(pos => startPos = pos) + SPAN(ALPHA);   // immediate
var q = θ(pos => startPos = pos) + SPAN(ALPHA);   // conditional
```

### Predicate and action

```csharp
// Λ — inline guard: fails if the lambda returns false
string n = "";
var fourDigits = SPAN(DIGITS) % (v => n = v) + Λ(() => n.Length == 4);

// λ — side effect queued to commit
string word = "";
var p = SPAN(ALPHA) % (v => word = v) + λ(() => Console.WriteLine(word));
```

---

## Recursive patterns with ζ

`ζ(() => p)` defers the pattern reference to match time, enabling
mutually recursive grammars from C# local variables:

```csharp
PATTERN? expr = null;
PATTERN? atom = null;

atom = SPAN(DIGITS)
     | σ("(") + ζ(() => expr!) + σ(")");

expr = atom! + ARBNO(σ("+") + atom!);

Engine.FULLMATCH("(1+2)+3", POS(0) + expr + RPOS(0));
```

---

## Regex bridge

Embed a compiled .NET regex as a SNOBOL4 pattern, anchored at the
current cursor. Named groups are delivered via a callback:

```csharp
string year = "", month = "";

var date = φ(@"(?<year>\d{4})-(?<month>\d{2})", (name, val) => {
    if (name == "year")  year  = val;
    if (name == "month") month = val;
});

Engine.SEARCH("date: 2024-03", date);
// year="2024"  month="03"
```

- `Φ` — immediate: groups written on every sub-match (permanent)
- `φ` — conditional: groups written only on full match commit

---

## Parse-tree construction

`Shift`, `Reduce`, and `Pop` build a nested `List<object>` AST during
matching, fully backtrack-safe via the cstack mechanism:

```csharp
string d = "";
List<object>? tree = null;

var digit  = ANY(DIGITS) % (v => d = v) + Shift("Digit", () => d);
var digits = nPush() + digit + nInc()
           + ARBNO(digit + nInc())
           + nPop() + Reduce("Digits");
var root   = POS(0) + digits + Pop(t => tree = t) + RPOS(0);

Engine.FULLMATCH("123", root);
// tree = ["Digits", ["Digit","1"], ["Digit","2"], ["Digit","3"]]
```

---

## Tracing

```csharp
TRACE(TraceLevel.Info);                          // to Console.Error
TRACE(TraceLevel.Debug, window: 8, output: sw);  // to a StringWriter
TRACE();                                         // silence
```

Levels: `Off` (default) · `Warning` (backtracking only) · `Info`
(success + backtracking) · `Debug` (all attempts).

Output format mirrors SNOBOL4python:

```
'      hello'|   0|'      '   SPAN("abc...") SUCCESS(0,5)=hello
```

---

## Engine entry points

```csharp
Slice? Engine.SEARCH   (string S, PATTERN P, bool exc = false)
Slice? Engine.MATCH    (string S, PATTERN P, bool exc = false)
Slice? Engine.FULLMATCH(string S, PATTERN P, bool exc = false)
```

- `SEARCH` — slides the pattern across the subject, trying every start position
- `MATCH` — anchors at position 0
- `FULLMATCH` — anchors at both ends

All return a `Slice?` — the matched span — or `null` on failure.
Pass `exc: true` to throw `F` instead of returning `null`.

A `Slice` carries `.Start`, `.Stop`, `.Length`, and `.Of(subject)`.

---

## Status

All xUnit tests passing. `Tests_JSON.cs` is disabled pending a port
of the JSON grammar to the current delegate-capture API.

---

## Relationship to snobol4ever

snobol4csharp is the C# pattern library arm of the [snobol4ever](https://github.com/snobol4ever) project. For the full SNOBOL4/SPITBOL *language* on .NET — complete compiler, runtime, GOTO execution model, DEFINE/DATA/FIELD, CODE(), EVAL(), TRACE, and LOAD/UNLOAD plugin system — see [snobol4dotnet](https://github.com/snobol4ever/snobol4dotnet).

The `ζ` deferred reference, `Φ`/`φ` regex bridge, shift-reduce parser stack, and Byrd Box execution model (`α`/`β`/`γ`/`ω` — proceed/recede/succeed/concede) are shared concepts with [snobol4python](https://github.com/snobol4ever/snobol4python), expressed in C# with delegates instead of Python lambdas.

---

## Acknowledgments

- **Jeffrey Cooper, M.D.** — snobol4csharp author
- **Lon Jones Cherryholmes** — snobol4ever architecture, SNOBOL4python (the port source)
- **Phil Budne** — CSNOBOL4, oracle for correctness validation
- **Ralph Griswold, Ivan Polonsky, David Farber** — SNOBOL4, Bell Labs, 1962–1967
- **Lawrence Byrd** — the four-port model (1980)
- **Todd Proebsting** — *Simple Translation of Goal-Directed Evaluation* (1996)

---

## License

LGPL-3.0-or-later. See [LICENSE](LICENSE).
