# Stage 188 · Readonly character reference identity

The exact platform `Unsafe.As<char, char>` keeps the location from `MemoryMarshal.GetReference(ReadOnlySpan<char>)` and its readonly provenance. The compiler already lowers packed character reinterpretations through a shared carrier. This identity adds no address conversion or copy.

Run `npm run demo -- stage-188-readonly-character-reference-identity`. Supply a prebuilt compiler with `STANDARDWASM_COMPILER_PATH` and `--no-rebuild` to pin the run.

The same C# assembly checks sliced string, array and stack storage on Windows x86 CLR, Node and Edge. The array case closes the generic source type to `char` and observes a write through the array after creating the span. The fixture also checks null and one-past-end anchors and the writable overload. For readonly provenance, a write and an unverified `ref char` call produce diagnostics at their consumer IL offsets.

The upstream resource path still requires an implementation for passing tracked readonly references into the original ordinal comparison helpers.
