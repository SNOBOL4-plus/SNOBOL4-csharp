# SNOBOL4csharp

A SNOBOL4-style pattern matching library for C# (.NET 8).

## Setup (once)

```bash
# Install .NET 8 if not present
# Ubuntu/Debian:
sudo apt install dotnet-sdk-8.0

# Install the script runner
dotnet tool install -g dotnet-script

# Add tools to your PATH — put this in ~/.bashrc or ~/.zshrc
export PATH="$PATH:$HOME/.dotnet/tools"

# Build the library (from the SNOBOL4csharp/ folder)
dotnet build -c Debug src/SNOBOL4
```

## Running a program

Write a `.csx` file, then run it like a Python script:

```bash
dotnet-script myprogram.csx
```

## A .csx program

```csharp
#!/usr/bin/env dotnet-script
#r "src/SNOBOL4/bin/Debug/net8.0/SNOBOL4.dll"

using SNOBOL4;
using static SNOBOL4.S4;

const string ALPHA  = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
const string DIGITS = "0123456789";
const string ALNUM  = ALPHA + DIGITS;

// Match a SNOBOL4 identifier: letter followed by zero or more alphanumerics
var ident = POS(0) + ANY(ALPHA) + NSPAN(ALNUM) + RPOS(0);

Console.WriteLine(Engine.FULLMATCH("Hello",  ident) != null);  // True
Console.WriteLine(Engine.FULLMATCH("1bad",   ident) != null);  // False
Console.WriteLine(Engine.FULLMATCH("abc123", ident) != null);  // True

// Capture with conditional assignment (fires only on full match)
var words = POS(0) + SPAN(ALPHA) % (Slot)_.word + RPOS(0);
Engine.FULLMATCH("hello", words);
Console.WriteLine((string)(Slot)_.word);   // hello
```

## Running the tests

```bash
dotnet run --project tests/SNOBOL4.Tests
```

## The #r line

The `#r` directive tells dotnet-script where the compiled library lives.
If you move your `.csx` file, adjust the path accordingly — it must point
to `SNOBOL4.dll` relative to where you run the script from.

For convenience, keep your `.csx` files in the `SNOBOL4csharp/` root folder
and the `#r` line stays the same for all of them.
