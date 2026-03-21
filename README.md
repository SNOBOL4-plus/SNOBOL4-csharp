# snobol4csharp

**SNOBOL4 pattern matching for C# — first-class patterns as .NET objects**

[![License: LGPL v3](https://img.shields.io/badge/License-LGPL%20v3-blue.svg)](https://www.gnu.org/licenses/lgpl-3.0)
[![Tests](https://img.shields.io/badge/tests-263%20%2F%200%20failures-brightgreen)](https://github.com/snobol4ever/snobol4csharp)

> *Part of the [snobol4ever](https://github.com/snobol4ever) project — SNOBOL4 everywhere, on every platform.*

---

## What This Is

snobol4csharp is a C# port of the SNOBOL4 pattern engine — full backtracking, composable patterns, recursive grammars — as idiomatic .NET objects. Written by Jeffrey Cooper, M.D.

SNOBOL4 patterns are values. They compose. They backtrack. They capture intermediate results with plain C# delegates. They reference themselves recursively through the `ζ` (zeta) operator. They can express BNF grammars, NLP taggers, and source code parsers directly — no yacc, no ANTLR, no separate grammar formalism.

This is the same pattern vocabulary available in [snobol4python](https://github.com/snobol4ever/snobol4python), ported to C# with a delegate-based capture API that feels native to the language.

---

## Quick Start

```csharp
using SNOBOL4;

// Build a pattern
var digit = Primitives.ANY("0123456789");
var digits = digit + Primitives.ARBNO(digit);

// Match against a subject
string subject = "abc123def";
var result = PatternMatch.Match(subject, digits);

// Capture with a delegate
string captured = null;
var pat = Primitives.SPAN("abc") + Primitives.SPAN("0123456789").Capture(v => captured = v);
PatternMatch.Match("abc123", pat);
// captured → "123"

// Recursive patterns via ζ (zeta)
Pattern balanced = null;
balanced = Primitives.LIT("(")
    + Primitives.ARBNO(Primitives.ζ(() => balanced) | Primitives.ANY("abc"))
    + Primitives.LIT(")");
PatternMatch.Match("(a(b)c)", balanced);  // ✅
```

---

## The Pattern Vocabulary

Full SNOBOL4/SPITBOL primitive set — identical semantics to CSNOBOL4 2.3.3:

| Primitive | Matches |
|-----------|---------|
| `LIT(s)` | Literal string `s` |
| `ANY(s)` | Any single character in `s` |
| `NOTANY(s)` | Any single character not in `s` |
| `SPAN(s)` | Longest run of characters in `s` |
| `BREAK(s)` | Longest run of characters not in `s` |
| `BREAKX(s)` | Like BREAK but resumes on backtrack |
| `ARB` | Any string (shortest to longest on backtrack) |
| `ARBNO(p)` | Zero or more repetitions of pattern `p` |
| `BAL` | Balanced parentheses |
| `LEN(n)` | Exactly `n` characters |
| `POS(n)` | Cursor at position `n` from left |
| `RPOS(n)` | Cursor at position `n` from right |
| `TAB(n)` | Advance cursor to position `n` |
| `RTAB(n)` | Advance cursor to position `n` from right |
| `REM` | Remainder of subject |
| `FENCE` | Prevent backtracking past this point |
| `FENCE(p)` | Match `p` without backtracking |
| `ABORT` | Immediately fail the entire match |
| `FAIL` | Always fail (force backtracking) |
| `SUCCEED` | Always succeed (loop until exhausted) |
| `ζ(fn)` | Recursive patterns via deferred lambda |

Composition operators:

| Operator | Meaning |
|----------|---------|
| `p1 + p2` | Sequential: p1 then p2 |
| `p1 \| p2` | Alternation: try p1, then p2 |
| `.Capture(delegate)` | Immediate assign on match |
| `.CursorCapture(delegate)` | Record cursor position |
| `.RegexBridge(regex)` | Integrate a .NET Regex as a pattern node |

---

## Shift-Reduce Parse-Tree Stack

snobol4csharp includes a shift-reduce parse-tree construction mechanism that runs *inside* the pattern match — accumulating AST nodes during the match itself, with no separate parse phase.

```csharp
// shift(p, tag) — match p and push a node onto the parse stack
// reduce(tag, n) — pop n items and emit a tree node

var expr = Shift(integer, "NUM") + ARBNO(
    Shift(ANY("+-"), "OP") + Shift(integer, "NUM") + Reduce("BINOP", 3)
);
PatternMatch.Match("1+2+3", expr);
var tree = GetParseTree();
```

This is the same mechanism used in snobol4python, expressed in C# with delegates instead of lambdas. It is the core of the more complex applications below.

---

## Test Coverage

**263 tests / 0 failures** (as of 2026-03-07)

| Test Suite | What It Validates |
|------------|-------------------|
| `Tests_Primitives` | All 21 pattern primitives |
| `Tests_01` | identifier, real_number, bead, bal, arb — basic grammars |
| `Tests_Arbno` | ARBNO patterns with nested recursion |
| `Tests_RE_Grammar` | Recursive RE grammar via shift-reduce |
| `Tests_CLAWS` | CLAWS5 NLP part-of-speech corpus parser |
| `Tests_TreeBank` | Penn Treebank parenthesized-tree parser |
| `Tests_Porter` | Porter Stemmer — **23,531-word corpus**, 0 failures |
| `Tests_Snobol4Parser` | Full SNOBOL4 source code parser |
| `Tests_JSON` | ⚠️ Disabled — pending port to delegate-capture API |

The Porter Stemmer test is the workload stress test: 23,531 words, each stemmed using a pattern-based implementation of the Porter algorithm. Zero failures means the backtracking engine is correct under sustained load across a realistic corpus.

The Penn Treebank and CLAWS5 tests validate the shift-reduce stack against two well-known NLP datasets. These are non-trivial grammars — recursive tree structures and multi-field POS tag sequences — that exercise the full depth of the Byrd Box backtracking model.

---

## The Regex Bridge

snobol4csharp includes a bridge that lets you embed a .NET `Regex` as a node inside a SNOBOL4 pattern:

```csharp
var hexColor = Primitives.LIT("#") + Primitives.RegexBridge(new Regex("[0-9a-fA-F]{6}"));
```

The SNOBOL4 pattern engine handles the outer composition and backtracking. The regex handles the inner match. This is useful for integrating existing regex patterns incrementally while migrating toward the full SNOBOL4 model — or for cases where a regex is simply the most concise way to express a fixed-format field.

---

## Why SNOBOL4 Patterns Over Regex

Regular expressions can only recognize regular grammars (Chomsky Type 3). SNOBOL4 patterns have no such ceiling:

| Grammar type | Regex | SNOBOL4 patterns |
|-------------|:-----:|:----------------:|
| Type 3 — Regular | ✅ | ✅ |
| Type 2 — Context-free (e.g. `{aⁿbⁿ}`, balanced parens) | ❌ | ✅ |
| Type 1 — Context-sensitive (e.g. `{aⁿbⁿcⁿ}`) | ❌ | ✅ |
| Type 0 — Turing-complete | ❌ | ✅ |

Context-free grammars — the kind used for most real programming languages and structured data formats — require either a separate parser (ANTLR, Roslyn, etc.) or SNOBOL4 patterns. Balanced parentheses, nested structures, and mutual recursion are all expressible directly as pattern values. The Porter Stemmer, Penn Treebank, and CLAWS5 parsers in this test suite are proofs of concept for real-world grammars.

The performance story: the snobol4x ASM backend matches `(a|b)*abb` at 33 ns vs PCRE2 JIT at 77 ns (2.3×). On pathological inputs like `(a+)+b`, PCRE2 backtracks exponentially; snobol4x detects failure structurally in 0.7 ns vs 25 ns (33×). These are native compiler numbers — snobol4csharp runs on .NET and has not been benchmarked against PCRE2 directly, but the structural advantage (no exponential backtracking on pathological inputs) applies to all Byrd Box implementations.

---

## The Byrd Box Model

Every pattern node in snobol4csharp is a Byrd Box — four execution states:

- **α** (proceed) — normal entry, cursor at current position
- **β** (recede) — re-entry after backtrack from child
- **γ** (succeed) — match succeeded, advance cursor
- **ω** (concede) — match failed, restore cursor

In C#, this is implemented as a state machine over pattern node objects. Sequential composition routes γ of one node to α of the next. Alternation saves the cursor on ω and restores it before the next alternative. ARBNO loops γ→α until ω exits. The `ζ` operator creates deferred references for recursive grammars — the C# equivalent of SNOBOL4's `*var` indirect pattern reference.

This is the same conceptual model used across the entire snobol4ever matrix — in Clojure (`match.clj`'s `loop/case` state machine), in native x86-64 ASM (labeled `nasm` blocks), in JVM bytecode (ASM-generated `.class` files), and in .NET MSIL (snobol4dotnet's ThreadedExecuteLoop). Same four ports. Different substrates.

---

## Relationship to snobol4ever

snobol4csharp is the C# pattern library arm of the [snobol4ever](https://github.com/snobol4ever) project. For the full SNOBOL4/SPITBOL *language* on .NET — complete compiler, runtime, GOTO execution model, DEFINE/DATA/FIELD, CODE(), EVAL(), TRACE, and LOAD/UNLOAD plugin system — see [snobol4dotnet](https://github.com/snobol4ever/snobol4dotnet).

The relationship is analogous to [snobol4python](https://github.com/snobol4ever/snobol4python): a focused, composable pattern library for one platform ecosystem, built on the same Byrd Box engine as the full-language implementations.

---

## License

LGPL v3. See [LICENSE](LICENSE).

---

## Acknowledgments

**Ralph Griswold, Ivan Polonsky, David Farber** — SNOBOL4, Bell Labs, 1962–1967.

**Phil Budne** — CSNOBOL4, oracle for all correctness validation.

**Lawrence Byrd** — The four-port model (1980).

**Todd Proebsting** — *Simple Translation of Goal-Directed Evaluation* (1996). The Byrd Box as code generation strategy.

**Jeffrey Cooper, M.D.** — snobol4csharp author. Fifty years of love for the language.

**Lon Jones Cherryholmes** — snobol4ever architecture.

---

*snobol4all. snobol4now. snobol4ever.*
