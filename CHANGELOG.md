# Changelog

All notable changes to SNOBOL4csharp are documented here.
The project was developed in a single session on 2026-03-07,
building stage by stage from a single-file prototype to a
full multi-project .NET 8 library.

---

## [Unreleased]

- `Tests_JSON.cs` — JSON parser test pending port to delegate-capture API

---

## Stage 17 — SNOBOL4 source parser + dynamic Reduce

- **New:** `Tests_Snobol4Parser.cs` — ports the Compiland grammar from
  `transl8r_SNOBOL4.py`; parses real SNOBOL4 source code (`roman.inc`)
  and asserts the syntax tree is well-formed
- **New:** `roman.inc` — sample SNOBOL4 source: Roman numeral converter
- **Engine:** `Shift()` — zero-argument placeholder node
- **Engine:** `Reduce(Func<object> dynTag)` — tag resolved at commit time
  from a captured value rather than fixed at construction time
- **Design note:** `ParseContext` class introduced to thread per-parse
  locals through all closures, preventing state pollution across
  `Engine.SEARCH` cursor attempts

---

## Stage 16 — Porter Stemmer corpus validation

- **New:** `Tests_Porter.cs` — Porter Stemmer algorithm ported from
  `CSCE_5200_Homework_3.ipynb` as a SNOBOL4 pattern pipeline
- **New:** `porter_voc.txt` / `porter_output.txt` — 23,531-word
  vocabulary and expected stems corpus (~350 KB total)
- **Key insight documented:** `*` (immediate) capture fires on every
  `Engine.SEARCH` cursor attempt including failed ones; factory methods
  with per-call closed-over locals are required to avoid pollution

---

## Stage 15 — `Action<string>` delegate capture API

- **Breaking change:** assignment operators redesigned around typed
  C# delegates, eliminating `Env` string-key indirection for everyday
  capture patterns
- `P % (Action<string> set)` — conditional capture (SNOBOL4 `P . N`)
- `P * (Action<string> set)` — immediate capture (SNOBOL4 `P $ N`)
- `_Δ` / `_δ` gain `Action<string>` constructors alongside retained
  Env-keyed string forms
- `_Θ` / `_θ` gain `Action<int>` constructors for cursor-position capture
- `Φ` / `φ` gain `Action<string,string>` named-group callback
- `Pop(Action<List<object>>)` for direct tree-stack capture
- `ζ` unified to `Func<PATTERN>` lambda; Env-name form retained via
  `ζ(string)` factory for backward compatibility
- All test files updated; `Env.Str()` calls removed throughout

---

## Stage 14 — xUnit test framework + real-corpus tests

- **Replaced** monolithic console runner (`Harness.cs`, `Tests.cs`,
  `Tests.Stage10.cs`) with a proper xUnit 2.9.0 test project
- **New:** `TestBase` abstract class — `FreshEnv()`, `AssertMatch` /
  `AssertNoMatch` / `AssertFound` / `AssertNotFound` helpers
- **New test classes:** `Tests_Primitives`, `Tests_01`, `Tests_Arbno`,
  `Tests_RE_Grammar`, `Tests_Env`, `Tests_CLAWS`, `Tests_TreeBank`
- **New:** `Tests_CLAWS.cs` — parses `CLAWS5inTASA.dat` (~65 KB NLP
  corpus) and builds a `mem[sentenceNum][word][tag]` frequency table
- **New:** `Tests_TreeBank.cs` — parses `VBGinTASA.dat` (~98 KB Penn
  Treebank trees) into a nested `List<object>` AST using `ζ(lambda)`
- **New:** `hello.csx` — dotnet-script quickstart example
- `SNOBOL4.Tests.csproj` upgraded to xUnit + Microsoft.NET.Test.Sdk

---

## Stage 13 — Operator fix, documentation pass, trace completion

- **Breaking fix:** `%` and `*` operator semantics were swapped in v12;
  corrected: `%` = conditional/deferred, `*` = immediate/permanent
- **Documentation:** structured headers and inline comments added to
  every source file explaining design rationale and Python equivalences
- `ζ(Func<>)` mutual-recursion idiom documented with a concrete example

---

## Stage 12 — Sliding-window trace instrumentation

- **New:** `Trace.cs` — `TraceLevel` enum, `Tracer` static class
- `TRACE(level, window, output)` — configure; `TRACE()` to silence
- Context window renders `'left'| pos|'right'` matching SNOBOL4python
  format; chars escaped (`\\n`, `\\r`, `\\t`)
- All primitives in `Primitives.cs` instrumented with
  `Debug` / `Info` / `InfoZ` / `Warn` calls
- `Σ` / `Π` track call `depth` for indented trace output
- `MatchState` gains `depth` field; `Ϣ` stack made thread-local
  (`[ThreadStatic]`) for concurrent safety
- `TRACE()` factory exposed in `S4`

---

## Stage 11 — Dynamic environment API: `SnobolEnv`, `Slot`, `_`

- **New:** `Slot` — live named reference into `Env._g` with implicit
  conversions to `string`, `int`, `bool`, `PATTERN`
- **New:** `SnobolEnv : DynamicObject` — thread-local proxy;
  property reads return `Slot`, writes go into `Env._g`
- `static dynamic _` — global accessor; `_.word` reads/writes `"word"`
- `P % Slot` and `P * Slot` operator overloads (later superseded by
  `Action<string>` form in Stage 15)
- `Env._g` initialized eagerly; `GLOBALS()` kept for compatibility
- All tests migrated from `G` dictionary and `Gs()` helpers to
  `_.name` notation

---

## Stage 10 — Python test suite ported to C#

- **New:** `Tests.Stage10.cs` (645 lines, 22 test methods) — direct
  port of four Python test modules (`test_01`, `test_arbno`,
  `test_re_simple`, `test_json`); each method mirrors its Python
  counterpart exactly
- Module-level patterns defined as `static readonly` fields matching
  Python module-scope definitions

---

## Stage 9 — Multi-project solution refactor

- **Breaking:** `SNOBOL4csharp.cs` single file retired; split into
  six focused files under `src/SNOBOL4/`
- **New layout:** `Core.cs` · `Primitives.cs` · `Assignment.cs` ·
  `ShiftReduce.cs` · `Regex.cs` · `S4.cs`
- `tests/SNOBOL4.Tests/` project introduced with `Harness.cs` +
  `Tests.cs`
- All types moved to `namespace SNOBOL4`
- **Stage 9 feature:** all 12 argument-bearing primitives gain
  `Func<T>` deferred-resolver overloads (evaluated at match time)
- `Env` upgraded: `GLOBALS(dict)` required before matching;
  `Has()`, `Str()`, `Int()` typed accessors added

---

## Stage 8 — Integer counter stack, parse-tree value stack, lambda ζ

- **New:** `nPush` / `nInc` / `nPop` — integer counter stack on
  `MatchState`, fully deferred via `cstack`, backtrack-safe
- **New:** `Shift(tag)` / `Shift(tag, expr)` — push leaf/value nodes
  onto parse-tree `vstack`
- **New:** `Reduce(tag)` / `Reduce(tag, n)` — pop and wrap children
- **New:** `Pop(name)` — pop `vstack` top into `Env`
- **New:** `ζ(Func<PATTERN>)` — lambda overload enabling mutually
  recursive grammars without name registration in `Env`
- Unlocks `test_re_simple.py` RE grammar and `test_json.py` JSON parser

---

## Stage 7 — Regex bridge: `Φ` (immediate) and `φ` (conditional)

- **New:** `Regex.cs` — `RxCache` compiled regex cache, lazy-populated,
  `Multiline | Compiled`
- `Φ(pat)` — regex anchored at cursor; named groups written to `Env`
  immediately (permanent, like `δ`)
- `φ(pat)` — same anchoring; named group writes deferred via `cstack`
  and rolled back on backtrack (like `Δ`)
- Only named groups `(?<n>...)` captured; numeric/unnamed skipped

---

## Stage 6 — Assignment patterns, predicates, deferred references

- **New:** `Assignment.cs`
- `Env` singleton — flat `Dictionary<string,object>` variable space
- `δ(P, name)` — immediate assignment on every yield (SNOBOL4 `P $ N`)
- `Δ(P, name)` — conditional assignment, committed on match success
- `Θ` / `θ` — immediate and conditional cursor-position capture
- `Λ(func)` — runtime predicate gate (`Func<bool>`)
- `λ(action)` — deferred action on success (`Action`)
- `ζ(name)` — deferred pattern lookup by name for forward references
- `MatchState` gains `cstack`; `Engine.SEARCH` flushes on success

---

## Stage 5 — Repetition and balanced-paren matching

- `ARBNO(P)` — lazy zero-or-more repetitions, shortest first;
  recursive generator with full backtracking
- `MARBNO(P)` — alias for ARBNO
- `BAL()` — balanced parenthesis matching with depth tracking;
  yields multiple times; handles nested parens and bare tokens

---

## Stage 4 — Character-class primitives

- `ANY(chars)` / `NOTANY(chars)` — single-char set membership
- `SPAN(chars)` / `NSPAN(chars)` — greedy multi-char scanning
  (one-or-more vs zero-or-more)
- `BREAK(chars)` / `BREAKX(chars)` — scan forward until a set
  member is found
- All use `HashSet<char>` for O(1) membership testing
- `LEN`, `TAB`, `RTAB`, `REM`, `ARB`, `MARB` added

---

## Stage 3 — Factory function API

- All implementation classes renamed with `_` prefix (`_σ`, `_Σ`,
  `_Π`, `_FENCE`, etc.) — hidden from callers
- `S4` static class exposes clean factory functions with exact SNOBOL4
  names: `σ()`, `Σ()`, `Π()`, `POS()`, `RPOS()`, `ε()`, …
- `using static S4` at top of file — call sites read as pure SNOBOL4
  with no `new` keyword anywhere

---

## Stage 2 — Trivials, anchors, FENCE, optional operator

- `ε` / `FAIL` / `ABORT` / `SUCCEED` — trivial/control-flow primitives
- `π` (`~P`) — optional operator, `P | ε`
- `α` / `ω` — BOL and EOL line anchors
- `FENCE()` — commit point: yields once, raises `F` on backtrack
- `FENCE(P)` — function form: blocks into-P backtracking
- Global `Ϣ` match-state stack; `γ()` no longer takes arguments
- `Σ` and `Π` upgraded to N-ary with proper enumerator management
- `F` exception class for ABORT/FENCE propagation

---

## Stage 1 — Core pattern engine

- `Pattern.cs` — abstract `PATTERN` base with `γ(MatchState)` generator
- `Str` (literal), `Pos`, `RPos` (cursor guards), `Alt` (`|`),
  `Concat` (`+`), `Engine.Search()` / `Engine.FullMatch()`
- `BeadTests.cs` — 16 positive + 4 negative cases for the classic
  SNOBOL4 bead pattern
- `.NET` solution scaffolding (`SNOBOL4.csproj`, `SNOBOL4.Tests.csproj`,
  `SNOBOL4.sln`)
