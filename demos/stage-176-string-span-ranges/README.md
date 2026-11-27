# Stage 176 · String span ranges

Run `npm run demo -- stage-176-string-span-ranges` with the .NET 10 SDK,
the x86 Windows runtime, Node, and Microsoft Edge. The runner uses the
same compiled .NET Standard 2.1 assembly on all three runtimes.

The linker recognizes the platform `MemoryExtensions.AsSpan` overloads
that take a string and zero, one, or two integer range arguments. Ranged
overloads retain the string's original UTF-16 backing storage and reuse
the span slice's range checks. Null text supplies the default zero-length view.

The cases cover UTF-16 code units, copying from an offset range, nested
slices, null and empty views, and typed exceptions for invalid or extreme
ranges. The evaluation-order case confirms the string and both indices
are evaluated before the failure. Results go to
`artifacts/stage-176-string-span-ranges/results.json`.
