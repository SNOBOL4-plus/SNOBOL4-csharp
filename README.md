# SNOBOL4cs

A C# port of the SNOBOL4 pattern-matching engine, ported from Python SNOBOL4python v0.5.0.

## Layout

```
SNOBOL4cs/
  src/SNOBOL4/           library (SNOBOL4.dll)
    Core.cs              F  Env  MatchState  Ϣ  Slice  PATTERN
    Primitives.cs        σ Σ Π π  POS RPOS LEN TAB RTAB  ANY SPAN BREAK ...
    Assignment.cs        δ Δ Θ θ Λ λ ζ
    ShiftReduce.cs       nPush nInc nPop  Shift Reduce Pop
    Regex.cs             Φ φ
    S4.cs                S4 factory + Engine
  tests/SNOBOL4.Tests/  console test runner
    Harness.cs           T (PASS/FAIL output)
    Tests.cs             100 tests, Stage 8 + Stage 9
```

## Build

```
dotnet build SNOBOL4cs.sln
dotnet run --project tests/SNOBOL4.Tests
```

## Stage 9: deferred callable args

All 12 primitives accept Func<T> resolvers evaluated at match time:

```csharp
G["sep"] = ",";
var csv = BREAK(() => Env.Str("sep")) % "field" + σ(() => Env.Str("sep"));
```

## Status

100 / 100 tests passing.
