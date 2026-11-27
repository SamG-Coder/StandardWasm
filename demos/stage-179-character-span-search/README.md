# Stage 179 · Character span search

Run `npm run demo -- stage-179-character-span-search` with the .NET 10
SDK, the x86 Windows runtime, Node, and Microsoft Edge. The runner uses
the same compiled .NET Standard 2.1 assembly in each runtime.

Both search paths in the original CoreLib specialization obtain raw
interior references through `MemoryMarshal.GetReference`. StandardWasm
recognizes the exact platform `IndexOfAny<char>(ReadOnlySpan<char>, char, char)`
signature and scans the existing tracked view directly. It compares
unsigned UTF-16 code units and returns the first matching relative index
or minus one. Managed and native-backed views retain their slice bounds.

The fixture covers aliases, offsets, high code units, NUL characters,
duplicate needles, empty views, misses, and evaluation order. Results go
to `artifacts/stage-179-character-span-search/results.json`.
