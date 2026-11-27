# Stage 197 · Character span IndexOf

The compiler recognizes the exact platform `MemoryExtensions.IndexOf<char>(ReadOnlySpan<char>, char)` overload and uses the same bounded UTF-16 search as the two-character `IndexOfAny` overload. It reads the view's backing storage at its current offset and returns the first relative match or `-1`. Other generic instantiations and overloads retain their managed dispatch.

Run `npm run demo -- stage-197-character-span-index-of`. Supply a prebuilt compiler with `STANDARDWASM_COMPILER_PATH` and `--no-rebuild` to pin the run.

The same .NET Standard 2.1 assembly checks sliced strings, array mutations, default and empty spans, a null string, native stack storage, and operand order. An exhaustive case checks both a match and a mismatch for every UTF-16 code unit.
